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
                "startStars", "reputation", "kinds", "vegShares", "groceryShares", "marketing", "khata", "rivals", "newcomers", "reaction", "chakki");
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

            string rp = root + ".reputation";
            JObject rep = Object(Required(o, root, "reputation"), rp);
            OnlyFields(rep, rp, "served", "standard", "good", "value", "valueGoneAbove", "step", "forgivenShutDays");
            d.ScoreServed = Bounded(rep, rp, "served", "0", "10");
            d.ScoreStandard = Bounded(rep, rp, "standard", "0", "10");
            d.ScoreGood = Bounded(rep, rp, "good", "0", "10");
            d.ScoreValue = Bounded(rep, rp, "value", "0", "10");
            d.ValueGoneAbove = Bounded(rep, rp, "valueGoneAbove", "0.01", "1");
            d.StarsStep = Bounded(rep, rp, "step", "0.001", "1");
            d.ForgivenShutDays = BoundedInt(rep, rp, "forgivenShutDays", 0, 30);
            if (d.ScoreGood < d.ScoreStandard) throw new ContentException(rp + ".good", "good quality scores at least standard");

            string kp = root + ".kinds";
            JObject k = Object(Required(o, root, "kinds"), kp);
            OnlyFields(k, kp, "veg", "chai", "sweets", "meals", "grocery");
            var kinds = new ShopKindDef[6];
            kinds[(int)ShopKind.Veg] = Kind(k, kp, "veg", "kgPerPerson", market);
            kinds[(int)ShopKind.Chai] = Kind(k, kp, "chai", "cupsPerPerson", market);
            kinds[(int)ShopKind.Sweets] = Kind(k, kp, "sweets", "kgPerPerson", market);
            kinds[(int)ShopKind.Grocery] = Kind(k, kp, "grocery", "kgPerPerson", market);
            if (k["meals"] != null) // the restaurant's diners (spec 028 §3.7): how fast they stay home as prices rise; how many comes from the leisure budget
            {
                string mp = Join(kp, "meals");
                JObject m = Object(k["meals"], mp);
                OnlyFields(m, mp, "elasticity");
                kinds[(int)ShopKind.Meals] = new ShopKindDef { AdultShare = Fixed.One, Elasticity = Bounded(m, mp, "elasticity", "0", "5") };
            }
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

            string gp = root + ".groceryShares";
            var staples = new List<KeyValuePair<int, Fixed>>();
            Fixed gtotal = Fixed.Zero;
            foreach (JProperty prop in Object(Required(o, root, "groceryShares"), gp).Properties())
            {
                int g = market.IndexOf(prop.Name);
                if (g < 0 || market.Goods[g].Kind != GoodKind.Produce) throw new ContentException(Join(gp, prop.Name), "not a produce good");
                Fixed f = Number(prop.Value, Join(gp, prop.Name));
                Range(f, Fixed.Zero, Fixed.One, Join(gp, prop.Name));
                gtotal += f;
                staples.Add(new KeyValuePair<int, Fixed>(g, f));
            }
            Fixed goff = gtotal - Fixed.One;
            if (goff > Fixed.FromRatio(1, 1000) || -goff > Fixed.FromRatio(1, 1000)) throw new ContentException(gp, "shares add up to " + gtotal + ", not 1");
            d.GroceryShares = staples.AsReadOnly();

            string mkp = root + ".marketing";
            JObject mk = Object(Required(o, root, "marketing"), mkp);
            OnlyFields(mk, mkp, "budgets", "scorePer1000", "cap", "fade");
            var budgets = new List<long>();
            List<JToken> bt = NonEmptyArray(mk, mkp, "budgets");
            for (int i = 0; i < bt.Count; i++) budgets.Add(Money(bt[i], Index(mkp + ".budgets", i)));
            if (budgets[0] != 0) throw new ContentException(mkp + ".budgets", "the first (the default) is nothing");
            for (int i = 1; i < budgets.Count; i++) if (budgets[i] <= budgets[i - 1]) throw new ContentException(Index(mkp + ".budgets", i), "budgets go up");
            d.MarketBudgets = budgets.AsReadOnly();
            d.MarketPer1000 = Bounded(mk, mkp, "scorePer1000", "0", "1");
            d.MarketCap = Bounded(mk, mkp, "cap", "0", "1");
            d.MarketFade = Bounded(mk, mkp, "fade", "0", "1");

            string khp = root + ".khata";
            JObject kh = Object(Required(o, root, "khata"), khp);
            OnlyFields(kh, khp, "limits", "score", "kilos", "lowIncome", "loss", "late");
            var limits = new List<long>();
            List<JToken> lt = NonEmptyArray(kh, khp, "limits");
            for (int i = 0; i < lt.Count; i++) limits.Add(Money(lt[i], Index(khp + ".limits", i)));
            for (int i = 1; i < limits.Count; i++) if (limits[i] <= limits[i - 1]) throw new ContentException(Index(khp + ".limits", i), "limits go up");
            d.KhataLimits = limits.AsReadOnly();
            d.KhataScore = Bounded(kh, khp, "score", "0", "1");
            d.KhataKilos = Bounded(kh, khp, "kilos", "0", "1");
            d.KhataLowIncome = Money(Required(kh, khp, "lowIncome"), khp + ".lowIncome");
            d.KhataLate = Bounded(kh, khp, "late", "0", "1");
            var loss = new List<KeyValuePair<long, Fixed>>();
            List<JToken> ls = NonEmptyArray(kh, khp, "loss");
            for (int i = 0; i < ls.Count; i++)
            {
                string lp = Index(khp + ".loss", i);
                JObject l = Object(ls[i], lp);
                OnlyFields(l, lp, "under", "rate");
                long under = Money(Required(l, lp, "under"), lp + ".under");
                if ((under == 0) != (i == ls.Count - 1)) throw new ContentException(lp + ".under", "every tier but the last has an income it is under; the last has 0");
                loss.Add(new KeyValuePair<long, Fixed>(under, Bounded(l, lp, "rate", "0", "1")));
            }
            d.KhataLoss = loss.AsReadOnly();

            var rivals = new List<RivalDef>();
            List<JToken> rt = NonEmptyArray(o, root, "rivals");
            for (int i = 0; i < rt.Count; i++)
            {
                string p = Index(root + ".rivals", i);
                JObject r = Object(rt[i], p);
                OnlyFields(r, p, "id", "kind", "place", "metres", "priceFactor", "quality", "stars", "openHour", "closeHour", "capacityPerHour", "reacts");
                var x = new RivalDef { Id = UniqueId(r, p, rivals.ConvertAll(y => y.Id)), Kind = ShopKindOf(String(Required(r, p, "kind"), p + ".kind"), p + ".kind") };
                if ((r["place"] == null) == (r["metres"] == null)) throw new ContentException(p + ".place", "a rival stands on a place, or at a distance off the map");
                if (r["place"] != null) x.Tile = PlaceTile(r, p, "place", map);
                else x.Metres = BoundedInt(r, p, "metres", 0, 100000);
                x.PriceFactor = Bounded(r, p, "priceFactor", "0.5", "2");
                x.Quality = Quality(r, p);
                x.Stars = Bounded(r, p, "stars", "0", "5");
                x.OpenHour = BoundedInt(r, p, "openHour", 0, 23);
                x.CloseHour = BoundedInt(r, p, "closeHour", x.OpenHour + 1, 24);
                x.CapacityPerHour = r["capacityPerHour"] == null ? 0 : BoundedInt(r, p, "capacityPerHour", 1, 100000) * (x.Kind == ShopKind.Chai || x.Kind == ShopKind.Meals ? 1L : 1000L);
                x.Reacts = r["reacts"] != null && Bool(r["reacts"], p + ".reacts");
                rivals.Add(x);
            }
            d.Rivals = rivals.AsReadOnly();

            string np = root + ".newcomers";
            JObject n = Object(Required(o, root, "newcomers"), np);
            OnlyFields(n, np, "gap", "chance", "exitShare", "exitSeasons", "stars", "quality", "cartPlace", "cartMetres");
            d.NewcomerGap = Bounded(n, np, "gap", "0", "1");
            d.NewcomerChance = Bounded(n, np, "chance", "0", "1");
            d.NewcomerExitShare = Bounded(n, np, "exitShare", "0", "1");
            d.NewcomerExitSeasons = BoundedInt(n, np, "exitSeasons", 1, 20);
            d.NewcomerStars = Bounded(n, np, "stars", "0", "5");
            d.NewcomerQuality = Quality(n, np);
            d.CartTile = PlaceTile(n, np, "cartPlace", map);
            d.CartMetres = BoundedInt(n, np, "cartMetres", 0, 10000);

            string ap = root + ".reaction";
            JObject a = Object(Required(o, root, "reaction"), ap);
            OnlyFields(a, ap, "step", "floor", "change");
            d.RivalStep = Bounded(a, ap, "step", "0", "0.5");
            d.RivalFloor = Bounded(a, ap, "floor", "0.5", "1");
            d.ReactChange = Bounded(a, ap, "change", "0", "1");

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
                case "meals": return ShopKind.Meals;
                case "grocery": return ShopKind.Grocery;
                default: throw new ContentException(path, "expected veg, chai, sweets, meals or grocery");
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
