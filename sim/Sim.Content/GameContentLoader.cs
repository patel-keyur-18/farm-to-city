using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        private static readonly Fixed Hundred = Fixed.FromInt(100);

        /// <summary>
        /// Loads the game content files (land, map, buildings, start, finance, goods, region, market) and checks the
        /// bounds and cross-file rules of specs 002 to 005. <paramref name="read"/> returns a file's JSON text by
        /// name, for example <c>read("land.json")</c>.
        /// </summary>
        public static GameDefs LoadGame(System.Func<string, string> read, WeatherSet weather)
        {
            var soils = new List<SoilDef>();
            var drainages = new List<DrainageDef>();
            LandDef land = LoadLand(read("land.json"), weather, soils, drainages);
            MapDef map = LoadMap(read("map.json"), soils, drainages);
            List<BuildingDef> buildings = LoadBuildings(read("buildings.json"));
            MarketDefs market = LoadMarketDefs(read("goods.json"), read("region.json"), read("market.json"));
            StartDef start = LoadStart(read("start.json"), map, buildings, market);
            LedgerDef ledger = LoadFinance(read("finance.json"));
            return new GameDefs(soils, drainages, land, map, buildings, start, ledger, market);
        }

        private static LandDef LoadLand(string json, WeatherSet weather, List<SoilDef> soils, List<DrainageDef> drainages)
        {
            const string root = "land";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "soils", "drainages", "land", "well");

            List<JToken> soilTokens = NonEmptyArray(o, root, "soils");
            for (int i = 0; i < soilTokens.Count; i++)
            {
                string p = Index(root + ".soils", i);
                JObject s = Object(soilTokens[i], p);
                OnlyFields(s, p, "id", "nameKey", "yieldFactor", "gainFactor", "lossFactor", "priceFactor", "fallowCap");
                string id = UniqueId(s, p, soils.ConvertAll(x => x.Id));
                soils.Add(new SoilDef(id, String(Required(s, p, "nameKey"), p + ".nameKey"),
                    Bounded(s, p, "yieldFactor", "0.5", "1.2"), Bounded(s, p, "gainFactor", "0.2", "2"),
                    Bounded(s, p, "lossFactor", "0.2", "3"), Bounded(s, p, "priceFactor", "0.3", "2"),
                    Bounded(s, p, "fallowCap", "0", "100")));
            }

            List<JToken> drainTokens = NonEmptyArray(o, root, "drainages");
            for (int i = 0; i < drainTokens.Count; i++)
            {
                string p = Index(root + ".drainages", i);
                JObject d = Object(drainTokens[i], p);
                OnlyFields(d, p, "id", "drainAbove", "drainRatePerHour", "priceFactor");
                string id = UniqueId(d, p, drainages.ConvertAll(x => x.Id));
                drainages.Add(new DrainageDef(id, Bounded(d, p, "drainAbove", "0", "100"),
                    Bounded(d, p, "drainRatePerHour", "0", "1"), Bounded(d, p, "priceFactor", "0.3", "2")));
            }

            string lp = root + ".land";
            JObject l = Object(Required(o, root, "land"), lp);
            OnlyFields(l, lp, "moistureByWeather", "fertilityFactorCurve", "fallowRecoveryPerSeason", "acreBasePrice",
                "fertilityPriceMin", "fertilityPriceMax", "quickSaleFactor", "fieldEdgeSlotsPerAcre",
                "shadePerMatureTree", "minShadeFactor");

            // Stored in the declared order of the weather types, never in JSON object order.
            string mp = lp + ".moistureByWeather";
            JObject m = Object(Required(l, lp, "moistureByWeather"), mp);
            var moisture = new Fixed?[weather.Types.Count];
            foreach (JProperty prop in m.Properties())
            {
                int w = weather.IndexOf(prop.Name);
                if (w < 0) throw new ContentException(Join(mp, prop.Name), "unknown weather id");
                moisture[w] = Number(prop.Value, Join(mp, prop.Name));
                Range(moisture[w].Value, Fixed.FromInt(-10), Fixed.FromInt(10), Join(mp, prop.Name));
            }
            var moistureList = new List<Fixed>();
            for (int w = 0; w < moisture.Length; w++)
            {
                if (moisture[w] == null) throw new ContentException(mp, "missing weather id '" + weather.Types[w].Id + "'");
                moistureList.Add(moisture[w].Value);
            }

            string cp = lp + ".fertilityFactorCurve";
            List<JToken> points = NonEmptyArray(l, lp, "fertilityFactorCurve");
            var curve = new List<CurvePoint>();
            for (int i = 0; i < points.Count; i++)
            {
                string p = Index(cp, i);
                List<JToken> xy = Array(points[i], p);
                if (xy.Count != 2) throw new ContentException(p, "expected [fertility, factor]");
                Fixed x = Number(xy[0], p + "[0]"), y = Number(xy[1], p + "[1]");
                Range(x, Fixed.Zero, Hundred, p + "[0]");
                Range(y, Fixed.Zero, Fixed.FromInt(2), p + "[1]");
                if (i > 0 && x <= curve[i - 1].X) throw new ContentException(p + "[0]", "fertility must be ascending");
                curve.Add(new CurvePoint(x, y));
            }
            if (curve.Count < 2 || curve[0].X != Fixed.Zero || curve[curve.Count - 1].X != Hundred)
                throw new ContentException(cp, "must run from fertility 0 to 100 with at least 2 points");

            long acrePrice = Money(Required(l, lp, "acreBasePrice"), lp + ".acreBasePrice");
            if (acrePrice <= 0) throw new ContentException(lp + ".acreBasePrice", "must be more than 0");
            Fixed fpMin = Bounded(l, lp, "fertilityPriceMin", "0.1", "3");
            Fixed fpMax = Bounded(l, lp, "fertilityPriceMax", "0.1", "3");
            if (fpMin > fpMax) throw new ContentException(lp + ".fertilityPriceMin", "must not be above fertilityPriceMax");
            int slots = Int(Required(l, lp, "fieldEdgeSlotsPerAcre"), lp + ".fieldEdgeSlotsPerAcre");
            Range(slots, 0, 4, lp + ".fieldEdgeSlotsPerAcre");

            string wp = root + ".well";
            JObject well = Object(Required(o, root, "well"), wp);
            OnlyFields(well, wp, "moistureFloor");

            return new LandDef(moistureList, curve, Bounded(l, lp, "fallowRecoveryPerSeason", "0", "25"), acrePrice,
                fpMin, fpMax, Bounded(l, lp, "quickSaleFactor", "0.3", "1"), slots,
                Bounded(l, lp, "shadePerMatureTree", "0", "0.2"), Bounded(l, lp, "minShadeFactor", "0.5", "1"),
                Bounded(well, wp, "moistureFloor", "0", "100"));
        }

        private static MapDef LoadMap(string json, List<SoilDef> soils, List<DrainageDef> drainages)
        {
            const string root = "map";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "width", "height", "tiles", "parcels");
            int width = Int(Required(o, root, "width"), root + ".width");
            Range(width, 2, 16, root + ".width");
            int height = Int(Required(o, root, "height"), root + ".height");
            Range(height, 2, 16, root + ".height");

            List<JToken> tileTokens = Array(Required(o, root, "tiles"), root + ".tiles");
            if (tileTokens.Count != width * height)
                throw new ContentException(root + ".tiles", "needs width × height = " + width * height + " tiles, has " + tileTokens.Count);
            var tiles = new List<TileDef>();
            for (int i = 0; i < tileTokens.Count; i++)
            {
                string p = Index(root + ".tiles", i);
                JObject t = Object(tileTokens[i], p);
                OnlyFields(t, p, "terrain", "soil", "drainage", "fertility", "moisture");
                string terrain = String(Required(t, p, "terrain"), p + ".terrain");
                if (terrain != "farm" && terrain != "yard") throw new ContentException(p + ".terrain", "expected farm or yard");
                tiles.Add(new TileDef(terrain == "farm" ? Terrain.Farm : Terrain.Yard,
                    Ref(t, p, "soil", soils.ConvertAll(x => x.Id)), Ref(t, p, "drainage", drainages.ConvertAll(x => x.Id)),
                    Bounded(t, p, "fertility", "0", "100"), Bounded(t, p, "moisture", "0", "100")));
            }

            List<JToken> parcelTokens = NonEmptyArray(o, root, "parcels");
            var parcels = new List<ParcelDef>();
            var owner = new int[tiles.Count];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;
            for (int i = 0; i < parcelTokens.Count; i++)
            {
                string p = Index(root + ".parcels", i);
                JObject pa = Object(parcelTokens[i], p);
                OnlyFields(pa, p, "id", "tiles", "locationFactor");
                string id = UniqueId(pa, p, parcels.ConvertAll(x => x.Id));
                List<JToken> ids = Array(Required(pa, p, "tiles"), p + ".tiles");
                if (ids.Count < 1 || ids.Count > 4) throw new ContentException(p + ".tiles", "a parcel has 1–4 tiles (has " + ids.Count + ")");
                var list = new List<int>();
                for (int k = 0; k < ids.Count; k++)
                {
                    string tp = Index(p + ".tiles", k);
                    int tile = Int(ids[k], tp);
                    Range(tile, 0, tiles.Count - 1, tp);
                    if (owner[tile] >= 0) throw new ContentException(tp, "tile " + tile + " is in two parcels");
                    owner[tile] = i;
                    list.Add(tile);
                }
                parcels.Add(new ParcelDef(id, list, Bounded(pa, p, "locationFactor", "0.5", "2")));
            }
            for (int t = 0; t < owner.Length; t++)
                if (owner[t] < 0) throw new ContentException(Index(root + ".tiles", t), "the tile is in no parcel");

            return new MapDef(width, height, tiles, parcels);
        }

        private static List<BuildingDef> LoadBuildings(string json)
        {
            const string root = "buildings";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "buildings");
            List<JToken> tokens = NonEmptyArray(o, root, "buildings");
            var list = new List<BuildingDef>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".buildings", i);
                JObject b = Object(tokens[i], p);
                OnlyFields(b, p, "id", "nameKey", "yardTreeSlots", "value", "usefulLifeSeasons", "storeCapacityKg");
                string id = UniqueId(b, p, list.ConvertAll(x => x.Id));
                int slots = Int(Required(b, p, "yardTreeSlots"), p + ".yardTreeSlots");
                Range(slots, 0, 12, p + ".yardTreeSlots");
                int life = Int(Required(b, p, "usefulLifeSeasons"), p + ".usefulLifeSeasons");
                Range(life, 1, 400, p + ".usefulLifeSeasons");
                int store = Int(Required(b, p, "storeCapacityKg"), p + ".storeCapacityKg");
                Range(store, 0, 1_000_000, p + ".storeCapacityKg");
                list.Add(new BuildingDef(id, String(Required(b, p, "nameKey"), p + ".nameKey"), slots,
                    Money(Required(b, p, "value"), p + ".value"), life, store));
            }
            return list;
        }

        private static StartDef LoadStart(string json, MapDef map, List<BuildingDef> buildings, MarketDefs market)
        {
            const string root = "start";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "inheritedParcels", "buildings", "startingCash", "store");

            string ip = root + ".inheritedParcels";
            List<JToken> parcelTokens = NonEmptyArray(o, root, "inheritedParcels");
            var inherited = new List<int>();
            var homeTiles = new List<int>();
            for (int i = 0; i < parcelTokens.Count; i++)
            {
                string p = Index(ip, i);
                string id = String(parcelTokens[i], p);
                int parcel = map.IndexOfParcel(id);
                if (parcel < 0) throw new ContentException(p, "unknown id '" + id + "'");
                if (inherited.Contains(parcel)) throw new ContentException(p, "duplicate parcel '" + id + "'");
                inherited.Add(parcel);
                homeTiles.AddRange(map.Parcels[parcel].Tiles);
            }
            bool hasYard = false;
            foreach (int t in homeTiles) hasYard |= map.Tiles[t].Terrain == Terrain.Yard;
            if (!hasYard) throw new ContentException(ip, "the inherited land needs a yard tile");

            List<JToken> tokens = NonEmptyArray(o, root, "buildings");
            var placed = new List<StartBuilding>();
            bool hasWell = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".buildings", i);
                JObject b = Object(tokens[i], p);
                OnlyFields(b, p, "building", "tile");
                int def = Ref(b, p, "building", buildings.ConvertAll(x => x.Id));
                int tile = Int(Required(b, p, "tile"), p + ".tile");
                Range(tile, 0, map.Tiles.Count - 1, p + ".tile");
                if (map.Tiles[tile].Terrain != Terrain.Yard) throw new ContentException(p + ".tile", "buildings stand on yard tiles only");
                if (!homeTiles.Contains(tile)) throw new ContentException(p + ".tile", "start buildings stand on the inherited land");
                hasWell |= buildings[def].Id == BuildingDef.WellId;
                placed.Add(new StartBuilding(def, tile));
            }
            if (!hasWell) throw new ContentException(root + ".buildings", "the inherited land needs the well");

            // The inherited store (spec 004 §3.5): quantities in each good's unit; it must fit the start buildings' store.
            long capacityGrams = 0;
            foreach (StartBuilding b in placed) capacityGrams += buildings[b.Building].StoreCapacityKg * 1000;
            var store = new List<StartStock>();
            long weight = 0;
            string sp = root + ".store";
            List<JToken> stockTokens = Array(Required(o, root, "store"), sp);
            for (int i = 0; i < stockTokens.Count; i++)
            {
                string p = Index(sp, i);
                JObject s = Object(stockTokens[i], p);
                OnlyFields(s, p, "good", "quantity");
                int good = Ref(s, p, "good", GoodIds(market));
                foreach (StartStock seen in store)
                    if (seen.Good == good) throw new ContentException(p + ".good", "duplicate good '" + market.Goods[good].Id + "'");
                GoodDef g = market.Goods[good];
                Fixed units = Number(Required(s, p, "quantity"), p + ".quantity");
                if (units <= Fixed.Zero) throw new ContentException(p + ".quantity", "must be more than 0");
                long q = units.MulRatio(g.UnitSize, 1).RoundToLong();
                store.Add(new StartStock(good, q));
                weight += Sim.Core.Market.Store.WeightGrams(g, q);
            }
            if (weight > capacityGrams)
                throw new ContentException(sp, "the inherited store weighs " + weight / 1000 + " kg, more than the store's " + capacityGrams / 1000 + " kg");

            return new StartDef(inherited, placed, Money(Required(o, root, "startingCash"), root + ".startingCash"), store);
        }

        private static LedgerDef LoadFinance(string json)
        {
            const string root = "finance";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "ledger");
            string lp = root + ".ledger";
            JObject l = Object(Required(o, root, "ledger"), lp);
            OnlyFields(l, lp, "retainSeasons");
            int retain = Int(Required(l, lp, "retainSeasons"), lp + ".retainSeasons");
            Range(retain, 1, 16, lp + ".retainSeasons");
            return new LedgerDef(retain);
        }

        // ---- helpers ----

        private static List<string> GoodIds(MarketDefs market)
        {
            var ids = new List<string>();
            foreach (GoodDef g in market.Goods) ids.Add(g.Id);
            return ids;
        }

        private static List<string> ParcelIds(MapDef map)
        {
            var ids = new List<string>();
            foreach (ParcelDef p in map.Parcels) ids.Add(p.Id);
            return ids;
        }

        private static List<JToken> NonEmptyArray(JObject o, string path, string name)
        {
            List<JToken> list = Array(Required(o, path, name), Join(path, name));
            if (list.Count == 0) throw new ContentException(Join(path, name), "must not be empty");
            return list;
        }

        private static string UniqueId(JObject o, string path, List<string> seen)
        {
            string id = String(Required(o, path, "id"), path + ".id");
            if (seen.Contains(id)) throw new ContentException(path + ".id", "duplicate id '" + id + "'");
            return id;
        }

        /// <summary>An id reference resolved to its index in <paramref name="ids"/>.</summary>
        private static int Ref(JObject o, string path, string name, List<string> ids)
        {
            string id = String(Required(o, path, name), Join(path, name));
            int i = ids.IndexOf(id);
            if (i < 0) throw new ContentException(Join(path, name), "unknown id '" + id + "'");
            return i;
        }

        private static Fixed Bounded(JObject o, string path, string name, string min, string max)
        {
            Fixed v = Number(Required(o, path, name), Join(path, name));
            Range(v, Fixed.Parse(min), Fixed.Parse(max), Join(path, name));
            return v;
        }
    }
}
