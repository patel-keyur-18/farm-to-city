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
            return new MarketDefs(goods, region, LoadEconomy(marketJson));
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
            OnlyFields(o, root, "roadTier", "transportCostPerKg", "seasonalFactor", "traderBuyPerDay");

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

            string bp = root + ".traderBuyPerDay";
            JObject buy = Object(Required(o, root, "traderBuyPerDay"), bp);
            var trader = new long[goods.Count];
            var seen = new bool[goods.Count];
            foreach (JProperty prop in buy.Properties())
            {
                string gp = Join(bp, prop.Name);
                int g = IndexOfGood(goods, prop.Name, gp);
                if (goods[g].Kind != GoodKind.Produce) throw new ContentException(gp, "the trader buys produce only");
                Fixed kg = Number(prop.Value, gp);
                Range(kg, Fixed.Zero, Fixed.FromInt(1_000_000), gp);
                trader[g] = kg.MulRatio(goods[g].UnitSize, 1).RoundToLong();
                seen[g] = true;
            }
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Kind == GoodKind.Produce && !seen[g]) throw new ContentException(bp, "missing produce good '" + goods[g].Id + "'");

            return new RegionDef(tier, transport, factors, trader);
        }

        private static EconomyDef LoadEconomy(string json)
        {
            const string root = "market";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "economy");
            string ep = root + ".economy";
            JObject e = Object(Required(o, root, "economy"), ep);
            OnlyFields(e, ep, "startIndex");
            Fixed start = Number(Required(e, ep, "startIndex"), ep + ".startIndex");
            if (start <= Fixed.Zero) throw new ContentException(ep + ".startIndex", "must be more than 0");
            return new EconomyDef(start);
        }

        private static int IndexOfGood(List<GoodDef> goods, string id, string path)
        {
            for (int i = 0; i < goods.Count; i++)
                if (goods[i].Id == id) return i;
            throw new ContentException(path, "unknown good '" + id + "'");
        }
    }
}
