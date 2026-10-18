using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        private static readonly string[] SeasonIds = { "spring", "summer", "monsoon", "winter" };

        /// <summary>Loads <c>goods.json</c>, <c>region.json</c> and <c>market.json</c> (spec 004 §4).</summary>
        private static MarketDefs LoadMarketDefs(string goodsJson, string regionJson, string marketJson)
        {
            List<GoodDef> goods = LoadGoods(goodsJson);
            RegionDef region = LoadRegion(regionJson, goods);
            const string root = "market";
            JObject o = Object(Parse(marketJson, root), root);
            OnlyFields(o, root, "market", "mandi", "economy");
            return new MarketDefs(goods, region, LoadEconomy(o, root), LoadMarket(o, root), LoadMandi(o, root));
        }

        private static List<GoodDef> LoadGoods(string json)
        {
            const string root = "goods";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "goods");
            List<JToken> tokens = NonEmptyArray(o, root, "goods");
            var list = new List<GoodDef>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".goods", i);
                JObject g = Object(tokens[i], p);
                OnlyFields(g, p, "id", "nameKey", "kind", "unit", "basePrice", "elasticity", "spoilagePerDay", "sales",
                    "edible", "weightKg", "priceControlled");
                string id = UniqueId(g, p, list.ConvertAll(x => x.Id));
                string kind = String(Required(g, p, "kind"), p + ".kind");
                if (kind != "produce" && kind != "input") throw new ContentException(p + ".kind", "expected produce or input");
                bool produce = kind == "produce";
                string unit = String(Required(g, p, "unit"), p + ".unit");
                GoodUnit u = unit == "kg" ? GoodUnit.Kg : unit == "litre" ? GoodUnit.Litre : unit == "each" ? GoodUnit.Each
                    : throw new ContentException(p + ".unit", "expected kg, litre or each");

                Fixed price = Number(Required(g, p, "basePrice"), p + ".basePrice");
                if (price <= Fixed.Zero) throw new ContentException(p + ".basePrice", "must be more than 0");
                Fixed elasticity = Fixed.Zero;
                SalesKind sales = SalesKind.None;
                if (produce)
                {
                    elasticity = Bounded(g, p, "elasticity", "0.1", "3");
                    string s = String(Required(g, p, "sales"), p + ".sales");
                    sales = s == "crop" ? SalesKind.Crop : s == "fruit" ? SalesKind.Fruit
                        : throw new ContentException(p + ".sales", "expected crop or fruit");
                }
                else
                {
                    if (g["elasticity"] != null) throw new ContentException(p + ".elasticity", "inputs have no market elasticity");
                    if (g["sales"] != null) throw new ContentException(p + ".sales", "inputs have no sales account");
                }
                Fixed spoilage = g["spoilagePerDay"] == null ? Fixed.Zero : Bounded(g, p, "spoilagePerDay", "0", "1");
                bool edible = g["edible"] == null ? produce : Bool(g["edible"], p + ".edible");
                if (edible && !produce) throw new ContentException(p + ".edible", "only produce is edible");
                Fixed weight = g["weightKg"] == null ? Fixed.One : Bounded(g, p, "weightKg", "0", "100");
                bool controlled = g["priceControlled"] != null && Bool(g["priceControlled"], p + ".priceControlled");
                list.Add(new GoodDef(id, String(Required(g, p, "nameKey"), p + ".nameKey"), produce ? GoodKind.Produce : GoodKind.Input,
                    u, price, elasticity, spoilage, sales, edible, weight, controlled));
            }
            return list;
        }

        private static RegionDef LoadRegion(string json, List<GoodDef> goods)
        {
            const string root = "region";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "roadTier", "transportCostPerKg", "seasonalFactor", "traderBuyPerDay", "demandPerDay",
                "valleySupplyPerDay", "importCapacityPerDay");

            string tier = String(Required(o, root, "roadTier"), root + ".roadTier");
            string tp = root + ".transportCostPerKg";
            JObject tiers = Object(Required(o, root, "transportCostPerKg"), tp);
            Fixed transport = Fixed.Zero;
            bool found = false;
            foreach (JProperty prop in tiers.Properties())
            {
                Fixed v = Number(prop.Value, Join(tp, prop.Name));
                Range(v, Fixed.Zero, Fixed.FromInt(100), Join(tp, prop.Name));
                if (prop.Name == tier) { transport = v; found = true; }
            }
            if (!found) throw new ContentException(root + ".roadTier", "unknown road tier '" + tier + "'");

            // Stored in good order, never in JSON object order.
            string sp = root + ".seasonalFactor";
            JObject sf = Object(Required(o, root, "seasonalFactor"), sp);
            var factors = new Fixed[goods.Count][];
            foreach (JProperty prop in sf.Properties())
            {
                string gp = Join(sp, prop.Name);
                int g = IndexOfGood(goods, prop.Name, gp);
                List<JToken> values = Array(prop.Value, gp);
                if (values.Count != SeasonIds.Length) throw new ContentException(gp, "needs one factor per season (Spring, Summer, Monsoon, Winter)");
                factors[g] = new Fixed[SeasonIds.Length];
                for (int s = 0; s < values.Count; s++)
                {
                    factors[g][s] = Number(values[s], Index(gp, s));
                    Range(factors[g][s], Fixed.FromRatio(1, 2), Fixed.FromInt(2), Index(gp, s));
                }
            }
            for (int g = 0; g < goods.Count; g++)
                if (factors[g] == null) throw new ContentException(sp, "missing good '" + goods[g].Id + "'");

            long[] trader = ProducePerDay(o, root, "traderBuyPerDay", goods, false);
            long[] demand = ProducePerDay(o, root, "demandPerDay", goods, true);
            long[] valley = ProducePerDay(o, root, "valleySupplyPerDay", goods, false);
            long[] imports = ProducePerDay(o, root, "importCapacityPerDay", goods, true); // > 0: S is never zero (ADR 0013)
            return new RegionDef(tier, transport, factors, trader, demand, valley, imports);
        }

        /// <summary>A map produce good id → kg a day, every produce good present, stored in grams in good order.</summary>
        private static long[] ProducePerDay(JObject o, string root, string field, List<GoodDef> goods, bool positive)
        {
            string bp = Join(root, field);
            JObject map = Object(Required(o, root, field), bp);
            var perDay = new long[goods.Count];
            var seen = new bool[goods.Count];
            foreach (JProperty prop in map.Properties())
            {
                string gp = Join(bp, prop.Name);
                int g = IndexOfGood(goods, prop.Name, gp);
                if (goods[g].Kind != GoodKind.Produce) throw new ContentException(gp, "produce goods only");
                Fixed kg = Number(prop.Value, gp);
                Range(kg, Fixed.Zero, Fixed.FromInt(1_000_000), gp);
                if (positive && kg == Fixed.Zero) throw new ContentException(gp, "must be more than 0");
                perDay[g] = kg.MulRatio(goods[g].UnitSize, 1).RoundToLong();
                seen[g] = true;
            }
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Kind == GoodKind.Produce && !seen[g]) throw new ContentException(bp, "missing produce good '" + goods[g].Id + "'");
            return perDay;
        }

        private static EconomyDef LoadEconomy(JObject o, string root)
        {
            string ep = root + ".economy";
            JObject e = Object(Required(o, root, "economy"), ep);
            OnlyFields(e, ep, "startIndex", "inflationMinPerYear", "inflationMaxPerYear");
            Fixed start = Number(Required(e, ep, "startIndex"), ep + ".startIndex");
            if (start <= Fixed.Zero) throw new ContentException(ep + ".startIndex", "must be more than 0");
            Fixed min = Bounded(e, ep, "inflationMinPerYear", "0", "0.5");
            Fixed max = Bounded(e, ep, "inflationMaxPerYear", "0", "0.5");
            if (min > max) throw new ContentException(ep + ".inflationMaxPerYear", "must be at least inflationMinPerYear");
            return new EconomyDef(start, min, max);
        }

        private static MarketDef LoadMarket(JObject o, string root)
        {
            string p = root + ".market";
            JObject m = Object(Required(o, root, "market"), p);
            OnlyFields(m, p, "adjustmentRate", "railMin", "railMax", "offerSpreadHours", "unmetWindowHours", "minLotKg");
            Fixed k = Bounded(m, p, "adjustmentRate", "0.01", "1");
            Fixed railMin = Number(Required(m, p, "railMin"), p + ".railMin");
            if (railMin <= Fixed.Zero || railMin >= Fixed.One) throw new ContentException(p + ".railMin", "must be between 0 and 1");
            Fixed railMax = Number(Required(m, p, "railMax"), p + ".railMax");
            if (railMax <= Fixed.One || railMax > Fixed.FromInt(10)) throw new ContentException(p + ".railMax", "must be between 1 and 10");
            int spread = Int(Required(m, p, "offerSpreadHours"), p + ".offerSpreadHours");
            Range(spread, 1, 96, p + ".offerSpreadHours");
            int window = Int(Required(m, p, "unmetWindowHours"), p + ".unmetWindowHours");
            Range(window, 1, 384, p + ".unmetWindowHours");
            Fixed lot = Bounded(m, p, "minLotKg", "0.001", "1000");
            return new MarketDef(k, railMin, railMax, spread, window, lot.MulRatio(1000, 1).RoundToLong());
        }

        private static MandiDef LoadMandi(JObject o, string root)
        {
            string p = root + ".mandi";
            JObject m = Object(Required(o, root, "mandi"), p);
            OnlyFields(m, p, "tripHours", "cartCapacityKg", "cartHirePerTrip", "commissionRate", "founderHours");
            int hours = Int(Required(m, p, "tripHours"), p + ".tripHours");
            Range(hours, 1, 24, p + ".tripHours");
            Fixed cart = Bounded(m, p, "cartCapacityKg", "1", "100000");
            long hire = Money(Required(m, p, "cartHirePerTrip"), p + ".cartHirePerTrip");
            if (hire < 0) throw new ContentException(p + ".cartHirePerTrip", "must not be negative");
            Fixed commission = Bounded(m, p, "commissionRate", "0", "0.2");
            Fixed founder = Bounded(m, p, "founderHours", "0", hours.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return new MandiDef(hours, cart.MulRatio(1000, 1).RoundToLong(), hire, commission, founder);
        }

        private static int IndexOfGood(List<GoodDef> goods, string id, string path)
        {
            for (int i = 0; i < goods.Count; i++)
                if (goods[i].Id == id) return i;
            throw new ContentException(path, "unknown good '" + id + "'");
        }
    }
}
