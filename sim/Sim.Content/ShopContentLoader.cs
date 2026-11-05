using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>Loads <c>content/shops.json</c> (spec 017 §4) and checks its bounds and its links to the goods and the map's places.</summary>
        private static ShopDefs LoadShops(string json, MarketDefs market, MapDef map)
        {
            const string root = "shops";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "villagePeople", "openHour", "closeHour", "metresPerAcre", "square", "pitches", "pitchRent", "retailMarkup", "pricePresets", "weights",
                "startStars", "kinds", "vegShares", "rivals", "chakki");
            var d = new ShopDefs
            {
                VillagePeople = BoundedInt(o, root, "villagePeople", 0, 100000),
                OpenHour = BoundedInt(o, root, "openHour", 0, 23),
                MetresPerAcre = BoundedInt(o, root, "metresPerAcre", 1, 10000),
                Pitches = BoundedInt(o, root, "pitches", 0, 10),
                PitchRent = Money(Required(o, root, "pitchRent"), root + ".pitchRent"),
                RetailMarkup = Bounded(o, root, "retailMarkup", "1", "5"),
                StartStars = Bounded(o, root, "startStars", "0", "5"),
            };
            d.CloseHour = BoundedInt(o, root, "closeHour", d.OpenHour + 1, 24);
            d.SquareTile = PlaceTile(o, root, "square", map);

            var presets = new List<Fixed>();
            List<JToken> pt = NonEmptyArray(o, root, "pricePresets");
            for (int i = 0; i < pt.Count; i++)
            {
                Fixed f = Number(pt[i], Index(root + ".pricePresets", i));
                Range(f, Fixed.FromRatio(1, 2), Fixed.FromInt(2), Index(root + ".pricePresets", i));
                if (presets.Contains(f)) throw new ContentException(Index(root + ".pricePresets", i), "listed twice");
                presets.Add(f);
            }
            if (presets[0] != Fixed.One) throw new ContentException(root + ".pricePresets", "the first (the default) is the reference, 1.0");
            d.PricePresets = presets.AsReadOnly();

            string wp = root + ".weights";
            JObject w = Object(Required(o, root, "weights"), wp);
            OnlyFields(w, wp, "price", "quality", "stars", "distancePer100m", "waitPer10Minutes", "floor");
            d.WPrice = Bounded(w, wp, "price", "0", "10");
            d.WQuality = Bounded(w, wp, "quality", "0", "10");
            d.WStars = Bounded(w, wp, "stars", "0", "10");
            d.WDistance = Bounded(w, wp, "distancePer100m", "0", "10");
            d.WWait = Bounded(w, wp, "waitPer10Minutes", "0", "10");
            d.AttractionFloor = Bounded(w, wp, "floor", "0.001", "1");

            string kp = root + ".kinds";
            JObject k = Object(Required(o, root, "kinds"), kp);
            OnlyFields(k, kp, "veg", "chai", "sweets");
            var kinds = new ShopKindDef[4];
            kinds[(int)ShopKind.Veg] = Kind(k, kp, "veg", "kgPerPerson", market);
            kinds[(int)ShopKind.Chai] = Kind(k, kp, "chai", "cupsPerPerson", market);
            kinds[(int)ShopKind.Sweets] = Kind(k, kp, "sweets", "kgPerPerson", market);
            d.Kinds = kinds;

            string vp = root + ".vegShares";
            var shares = new List<KeyValuePair<int, Fixed>>();
            Fixed total = Fixed.Zero;
            foreach (JProperty prop in Object(Required(o, root, "vegShares"), vp).Properties())
            {
                int g = market.IndexOf(prop.Name);
                if (g < 0 || !market.Goods[g].Edible) throw new ContentException(Join(vp, prop.Name), "not an edible produce good");
                Fixed f = Number(prop.Value, Join(vp, prop.Name));
                Range(f, Fixed.Zero, Fixed.One, Join(vp, prop.Name));
                total += f;
                shares.Add(new KeyValuePair<int, Fixed>(g, f));
            }
            Fixed off = total - Fixed.One;
            if (off > Fixed.FromRatio(1, 1000) || -off > Fixed.FromRatio(1, 1000)) throw new ContentException(vp, "shares add up to " + total + ", not 1");
            d.VegShares = shares.AsReadOnly();

            var rivals = new List<RivalDef>();
            List<JToken> rt = NonEmptyArray(o, root, "rivals");
            for (int i = 0; i < rt.Count; i++)
            {
                string p = Index(root + ".rivals", i);
                JObject r = Object(rt[i], p);
                OnlyFields(r, p, "id", "kind", "place", "metres", "priceFactor", "quality", "stars", "openHour", "closeHour", "capacityPerHour");
                var x = new RivalDef { Id = UniqueId(r, p, rivals.ConvertAll(y => y.Id)), Kind = ShopKindOf(String(Required(r, p, "kind"), p + ".kind"), p + ".kind") };
                if ((r["place"] == null) == (r["metres"] == null)) throw new ContentException(p + ".place", "a rival stands on a place, or at a distance off the map");
                if (r["place"] != null) x.Tile = PlaceTile(r, p, "place", map);
                else x.Metres = BoundedInt(r, p, "metres", 0, 100000);
                x.PriceFactor = Bounded(r, p, "priceFactor", "0.5", "2");
                x.Quality = Quality(r, p);
                x.Stars = Bounded(r, p, "stars", "0", "5");
                x.OpenHour = BoundedInt(r, p, "openHour", 0, 23);
                x.CloseHour = BoundedInt(r, p, "closeHour", x.OpenHour + 1, 24);
                x.CapacityPerHour = r["capacityPerHour"] == null ? 0 : BoundedInt(r, p, "capacityPerHour", 1, 100000) * (x.Kind == ShopKind.Chai ? 1L : 1000L);
                rivals.Add(x);
            }
            d.Rivals = rivals.AsReadOnly();

            string cp = root + ".chakki";
            JObject c = Object(Required(o, root, "chakki"), cp);
            OnlyFields(c, cp, "metres", "quality", "stars");
            d.ChakkiMetres = BoundedInt(c, cp, "metres", 0, 100000);
            d.ChakkiQuality = Quality(c, cp);
            d.ChakkiStars = Bounded(c, cp, "stars", "0", "5");
            return d;
        }

        private static ShopKindDef Kind(JObject k, string kp, string id, string perPerson, MarketDefs market)
        {
            string p = Join(kp, id);
            JObject o = Object(Required(k, kp, id), p);
            if (id == "chai") OnlyFields(o, p, perPerson, "adultShare", "elasticity", "reference", "snackShare");
            else if (id == "sweets") OnlyFields(o, p, perPerson, "adultShare", "elasticity", "references");
            else OnlyFields(o, p, perPerson, "adultShare", "elasticity");
            var x = new ShopKindDef
            {
                PerPerson = Bounded(o, p, perPerson, "0", "100"), AdultShare = Bounded(o, p, "adultShare", "0", "1"), Elasticity = Bounded(o, p, "elasticity", "0", "5"),
            };
            if (id == "chai")
            {
                x.Reference = Bounded(o, p, "reference", "0.01", "10000");
                x.SnackShare = Bounded(o, p, "snackShare", "0", "1");
            }
            if (id == "sweets")
            {
                var refs = new List<KeyValuePair<int, Fixed>>();
                foreach (JProperty prop in Object(Required(o, p, "references"), p + ".references").Properties())
                {
                    int g = market.IndexOf(prop.Name);
                    if (g < 0) throw new ContentException(Join(p + ".references", prop.Name), "unknown good");
                    Fixed f = Number(prop.Value, Join(p + ".references", prop.Name));
                    Range(f, Fixed.FromRatio(1, 100), Fixed.FromInt(100000), Join(p + ".references", prop.Name));
                    refs.Add(new KeyValuePair<int, Fixed>(g, f));
                }
                if (refs.Count == 0) throw new ContentException(p + ".references", "needs at least one sweet");
                x.References = refs.AsReadOnly();
            }
            return x;
        }

        private static ShopKind ShopKindOf(string id, string path)
        {
            switch (id)
            {
                case "veg": return ShopKind.Veg;
                case "chai": return ShopKind.Chai;
                case "sweets": return ShopKind.Sweets;
                default: throw new ContentException(path, "expected veg, chai or sweets");
            }
        }

        private static int Quality(JObject o, string p)
        {
            string q = String(Required(o, p, "quality"), p + ".quality");
            if (q != "standard" && q != "good") throw new ContentException(p + ".quality", "expected standard or good");
            return q == "good" ? 2 : 1;
        }

        private static int PlaceTile(JObject o, string p, string field, MapDef map)
        {
            string id = String(Required(o, p, field), Join(p, field));
            foreach (PlaceDef place in map.Places) if (place.Id == id) return place.Tile;
            throw new ContentException(Join(p, field), "unknown place '" + id + "'");
        }
    }
}
