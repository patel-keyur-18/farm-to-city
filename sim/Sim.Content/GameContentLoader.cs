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
            FarmDefs farm = LoadFarmDefs(read("crops.json"), read("farming.json"), read("trees.json"), read("founder.json"), market, weather);
            StartDef start = LoadStart(read("start.json"), map, buildings, market, farm, land);
            LedgerDef ledger = LoadFinance(read("finance.json"), out CreditScoreDef scoreDef);
            // Credit is optional content (ADR 0038): a finance.json without a "credit" block means no lenders.json is read.
            CreditDefs credit = scoreDef == null ? null : new CreditDefs(scoreDef, LoadLenders(read("lenders.json"), scoreDef));
            // People are optional content too (spec 006): only buildings with a hand's hut bring people.json.
            int handHut = buildings.FindIndex(b => b.Id == PeopleDefs.HandHutId);
            PeopleDefs people = handHut < 0 ? null : LoadPeople(read("people.json"), handHut, buildings[handHut]);
            // The letter and the eras come with people (the Hamlet needs a hand's hut, spec 009 §4).
            OnboardingDefs onboarding = people == null ? null : LoadOnboarding(read("onboarding.json"), read("eras.json"));
            return new GameDefs(soils, drainages, land, map, buildings, start, ledger, market, farm, credit, people, onboarding);
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
                "minShadeFactor");

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
            OnlyFields(well, wp, "moistureFloor", "irrigatedTiles");
            var wellTiles = new List<int>();
            List<JToken> wt = NonEmptyArray(well, wp, "irrigatedTiles");
            for (int i = 0; i < wt.Count; i++)
            {
                int tile = Int(wt[i], Index(wp + ".irrigatedTiles", i));
                if (wellTiles.Contains(tile)) throw new ContentException(Index(wp + ".irrigatedTiles", i), "duplicate tile " + tile);
                wellTiles.Add(tile); // checked against the map and the inheritance in LoadStart
            }

            return new LandDef(moistureList, curve, Bounded(l, lp, "fallowRecoveryPerSeason", "0", "25"), acrePrice,
                fpMin, fpMax, Bounded(l, lp, "quickSaleFactor", "0.3", "1"), slots,
                Bounded(l, lp, "minShadeFactor", "0.5", "1"),
                Bounded(well, wp, "moistureFloor", "0", "100"), wellTiles);
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
                if (terrain != "farm" && terrain != "yard" && terrain != "quarters") throw new ContentException(p + ".terrain", "expected farm, yard or quarters");
                tiles.Add(new TileDef(terrain == "farm" ? Terrain.Farm : terrain == "yard" ? Terrain.Yard : Terrain.Quarters,
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
                OnlyFields(b, p, "id", "nameKey", "yardTreeSlots", "value", "usefulLifeSeasons", "storeCapacityKg", "restRecovery", "gardenBeds",
                    "buildHours", "housesHands");
                string id = UniqueId(b, p, list.ConvertAll(x => x.Id));
                int slots = Int(Required(b, p, "yardTreeSlots"), p + ".yardTreeSlots");
                Range(slots, 0, 12, p + ".yardTreeSlots");
                int life = Int(Required(b, p, "usefulLifeSeasons"), p + ".usefulLifeSeasons");
                Range(life, 1, 400, p + ".usefulLifeSeasons");
                int store = Int(Required(b, p, "storeCapacityKg"), p + ".storeCapacityKg");
                Range(store, 0, 1_000_000, p + ".storeCapacityKg");
                list.Add(new BuildingDef(id, String(Required(b, p, "nameKey"), p + ".nameKey"), slots,
                    Money(Required(b, p, "value"), p + ".value"), life, store, Bounded(b, p, "restRecovery", "0", "1"),
                    BoundedInt(b, p, "gardenBeds", 0, 12),
                    b["buildHours"] == null ? 0 : BoundedInt(b, p, "buildHours", 0, 24 * 30),
                    b["housesHands"] == null ? 0 : BoundedInt(b, p, "housesHands", 0, 20)));
            }
            return list;
        }

        private static StartDef LoadStart(string json, MapDef map, List<BuildingDef> buildings, MarketDefs market, FarmDefs farm, LandDef land)
        {
            const string root = "start";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "inheritedParcels", "buildings", "startingCash", "store", "savedSeedAge");

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
            for (int i = 0; i < land.WellTiles.Count; i++)
            {
                int t = land.WellTiles[i];
                if (t < 0 || t >= map.Tiles.Count || !homeTiles.Contains(t) || map.Tiles[t].Terrain != Terrain.Farm)
                    throw new ContentException(Index("land.well.irrigatedTiles", i), "tile " + t + " is not an inherited farm tile");
            }

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

            // Saved-seed age of the inherited grain (ADR 0032): crop id → seasons of growth.
            var ages = new Fixed?[farm.Crops.Count];
            string ap = root + ".savedSeedAge";
            JObject ageObj = Object(Required(o, root, "savedSeedAge"), ap);
            foreach (JProperty prop in ageObj.Properties())
            {
                int c = farm.IndexOfCrop(prop.Name);
                if (c < 0) throw new ContentException(Join(ap, prop.Name), "unknown crop '" + prop.Name + "'");
                if (!farm.Crops[c].CanSaveSeed) throw new ContentException(Join(ap, prop.Name), "this crop's seed can't be saved");
                Fixed age = Number(prop.Value, Join(ap, prop.Name));
                Range(age, Fixed.Zero, Fixed.FromInt(100), Join(ap, prop.Name));
                ages[c] = age;
            }

            return new StartDef(inherited, placed, Money(Required(o, root, "startingCash"), root + ".startingCash"), store, ages);
        }

        private static LedgerDef LoadFinance(string json, out CreditScoreDef score)
        {
            const string root = "finance";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "ledger", "credit");
            string lp = root + ".ledger";
            JObject l = Object(Required(o, root, "ledger"), lp);
            OnlyFields(l, lp, "retainSeasons");
            int retain = Int(Required(l, lp, "retainSeasons"), lp + ".retainSeasons");
            Range(retain, 1, 16, lp + ".retainSeasons");

            score = null;
            if (o["credit"] != null)
            {
                string cp = root + ".credit";
                JObject c = Object(o["credit"], cp);
                OnlyFields(c, cp, "min", "max", "startScore", "fullScore", "onTimeGain", "cleanSeasonGain", "missedPenalty",
                    "defaultPenalty", "restructurePenalty", "foreclosureScore", "finalNoticeLiveHours");
                int I(string f) { return Int(Required(c, cp, f), Join(cp, f)); }
                int min = I("min"), max = I("max");
                if (min >= max) throw new ContentException(Join(cp, "min"), "must be below max");
                int InScore(string f) { int v = I(f); Range(v, min, max, Join(cp, f)); return v; }
                int Gain(string f, int hi) { int v = I(f); Range(v, 0, hi, Join(cp, f)); return v; }
                int start = InScore("startScore"), full = InScore("fullScore"), foreclosure = InScore("foreclosureScore");
                int onTime = Gain("onTimeGain", 100), clean = Gain("cleanSeasonGain", 100);
                int missed = Gain("missedPenalty", 600), dflt = Gain("defaultPenalty", 600), restructure = Gain("restructurePenalty", 600);
                int notice = I("finalNoticeLiveHours");
                Range(notice, 1, 168, Join(cp, "finalNoticeLiveHours"));
                score = new CreditScoreDef(min, max, start, full, onTime, clean, missed, dflt, restructure, foreclosure, notice);
            }
            return new LedgerDef(retain);
        }

        private static List<LenderDef> LoadLenders(string json, CreditScoreDef score)
        {
            const string root = "lenders";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "lenders");
            List<JToken> tokens = NonEmptyArray(o, root, "lenders");
            var lenders = new List<LenderDef>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".lenders", i);
                JObject d = Object(tokens[i], p);
                OnlyFields(d, p, "id", "nameKey", "baseRate", "rateSlopePer100", "refScore", "penaltyRate", "schedule", "minTerm", "maxTerm",
                    "minAmount", "secured", "baseLimit", "collateralLtv", "incomeMultiple", "minScore", "scoreScaledLimit", "graceCloses",
                    "restructureExtraSeasons", "eraCaps");
                string id = UniqueId(d, p, lenders.ConvertAll(x => x.Id));
                if (id != LenderDef.MoneylenderId && id != LenderDef.BankId)
                    throw new ContentException(Join(p, "id"), "must be '" + LenderDef.MoneylenderId + "' or '" + LenderDef.BankId + "'");

                string sched = String(Required(d, p, "schedule"), Join(p, "schedule"));
                LoanSchedule schedule = sched == "bullet" ? LoanSchedule.Bullet : sched == "equalPrincipal" ? LoanSchedule.EqualPrincipal
                    : throw new ContentException(Join(p, "schedule"), "expected bullet or equalPrincipal");
                int minTerm = Int(Required(d, p, "minTerm"), Join(p, "minTerm")), maxTerm = Int(Required(d, p, "maxTerm"), Join(p, "maxTerm"));
                Range(minTerm, 1, 40, Join(p, "minTerm"));
                Range(maxTerm, 1, 40, Join(p, "maxTerm"));
                if (minTerm > maxTerm) throw new ContentException(Join(p, "minTerm"), "must not be above maxTerm");
                int refScore = Int(Required(d, p, "refScore"), Join(p, "refScore"));
                Range(refScore, score.Min, score.Max, Join(p, "refScore"));
                int minScore = Int(Required(d, p, "minScore"), Join(p, "minScore"));
                Range(minScore, score.Min, score.Max, Join(p, "minScore"));
                int grace = Int(Required(d, p, "graceCloses"), Join(p, "graceCloses"));
                Range(grace, 1, 8, Join(p, "graceCloses"));
                int extra = Int(Required(d, p, "restructureExtraSeasons"), Join(p, "restructureExtraSeasons"));
                Range(extra, 0, 12, Join(p, "restructureExtraSeasons"));
                long minAmount = Money(Required(d, p, "minAmount"), Join(p, "minAmount"));
                if (minAmount < Sim.Core.Finance.Money.Coin) throw new ContentException(Join(p, "minAmount"), "must be at least 1 coin");

                string ep = Join(p, "eraCaps");
                JObject caps = Object(Required(d, p, "eraCaps"), ep);
                var eraCaps = new Dictionary<string, long>();
                foreach (JProperty prop in caps.Properties()) eraCaps[prop.Name] = Money(prop.Value, Join(ep, prop.Name));
                if (!eraCaps.ContainsKey(LenderDef.HomesteadEra)) throw new ContentException(ep, "missing the '" + LenderDef.HomesteadEra + "' era cap");

                lenders.Add(new LenderDef(id, String(Required(d, p, "nameKey"), Join(p, "nameKey")),
                    Bounded(d, p, "baseRate", "0", "0.25"), Bounded(d, p, "rateSlopePer100", "0", "0.05"), refScore,
                    Bounded(d, p, "penaltyRate", "0", "0.25"), schedule, minTerm, maxTerm, minAmount,
                    Bool(Required(d, p, "secured"), Join(p, "secured")), Money(Required(d, p, "baseLimit"), Join(p, "baseLimit")),
                    Bounded(d, p, "collateralLtv", "0", "1"), Bounded(d, p, "incomeMultiple", "0", "4"), minScore,
                    Bool(Required(d, p, "scoreScaledLimit"), Join(p, "scoreScaledLimit")), grace, extra, eraCaps));
            }
            if (lenders.Count != 2 || lenders[0].Id != LenderDef.MoneylenderId || lenders[1].Id != LenderDef.BankId)
                throw new ContentException(root + ".lenders", "expected the moneylender, then the cooperative bank");
            return lenders;
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

        private static int BoundedInt(JObject o, string path, string name, int min, int max)
        {
            int v = Int(Required(o, path, name), Join(path, name));
            Range(v, min, max, Join(path, name));
            return v;
        }

        private static Fixed Bounded(JObject o, string path, string name, string min, string max)
        {
            Fixed v = Number(Required(o, path, name), Join(path, name));
            Range(v, Fixed.Parse(min), Fixed.Parse(max), Join(path, name));
            return v;
        }
    }
}
