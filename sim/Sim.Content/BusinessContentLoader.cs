using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>Loads <c>content/businesses.json</c> (spec 016 §4) and checks its bounds and its links to the goods and eras.</summary>
        private static BusinessDefs LoadBusinesses(string json, MarketDefs market, OnboardingDefs onboarding)
        {
            const string root = "businesses";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "area", "funding", "types");
            var d = new BusinessDefs();

            string ap = root + ".area";
            JObject a = Object(Required(o, root, "area"), ap);
            OnlyFields(a, ap, "catchmentPeople", "grainPerPersonPerRealDay", "nextVillageFee", "feeTolerance");
            d.CatchmentPeople = BoundedInt(a, ap, "catchmentPeople", 0, 100000);
            d.GrainPerPersonPerRealDay = Bounded(a, ap, "grainPerPersonPerRealDay", "0", "5");
            d.NextVillageFee = Bounded(a, ap, "nextVillageFee", "0", "100");
            d.FeeTolerance = Bounded(a, ap, "feeTolerance", "0", "100");

            string fp = root + ".funding";
            JObject f = Object(Required(o, root, "funding"), fp);
            OnlyFields(f, fp, "businessLtv", "termSeasons", "overdraftLimit", "overdraftRate", "closingSaleShare", "lender", "guaranteeTermSeasons");
            d.BusinessLtv = Bounded(f, fp, "businessLtv", "0", "1");
            d.TermSeasons = BoundedInt(f, fp, "termSeasons", 1, 40);
            d.OverdraftLimit = Bounded(f, fp, "overdraftLimit", "0", "4");
            d.OverdraftRate = Bounded(f, fp, "overdraftRate", "0", "0.5");
            d.ClosingSaleShare = Bounded(f, fp, "closingSaleShare", "0", "1");
            d.LenderId = String(Required(f, fp, "lender"), fp + ".lender");
            d.GuaranteeTermSeasons = BoundedInt(f, fp, "guaranteeTermSeasons", 1, 40);

            List<JToken> tokens = NonEmptyArray(o, root, "types");
            var types = new List<BusinessTypeDef>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".types", i);
                JObject t = Object(tokens[i], p);
                OnlyFields(t, p, "id", "nameKey", "era", "building", "equipment", "buildHours", "worker", "workerSkill", "wage", "kgPerHour", "powerPerKg", "repairsShare",
                    "openHour", "closeHour", "fee", "ownPerDay", "input", "outputs", "recipes", "shops", "takesMilk", "neighbourMilkPremium",
                    "kind", "pitchBuilding", "capacityPerHour", "goods", "cup");
                var x = new BusinessTypeDef { Id = UniqueId(t, p, types.ConvertAll(y => y.Id)), NameKey = String(Required(t, p, "nameKey"), p + ".nameKey") };
                string era = String(Required(t, p, "era"), p + ".era");
                x.Era = -1;
                for (int e = 0; e < onboarding.Eras.Count; e++) if (onboarding.Eras[e].Id == era) x.Era = e;
                if (x.Era < 0) throw new ContentException(p + ".era", "unknown era '" + era + "'");
                JObject b = Object(Required(t, p, "building"), p + ".building"), q = Object(Required(t, p, "equipment"), p + ".equipment");
                x.BuildingValue = Money(Required(b, p + ".building", "value"), p + ".building.value");
                x.BuildingLifeSeasons = BoundedInt(b, p + ".building", "lifeSeasons", 1, 400);
                x.EquipmentValue = Money(Required(q, p + ".equipment", "value"), p + ".equipment.value");
                x.EquipmentLifeSeasons = BoundedInt(q, p + ".equipment", "lifeSeasons", 1, 400);
                x.BuildHours = BoundedInt(t, p, "buildHours", 1, 240);
                x.Worker = String(Required(t, p, "worker"), p + ".worker");
                string skill = t["workerSkill"] == null ? "crafting" : String(t["workerSkill"], p + ".workerSkill");
                if (skill != "crafting" && skill != "farming") throw new ContentException(p + ".workerSkill", "expected crafting or farming");
                x.WorkerFarms = skill == "farming";
                x.Wage = Money(Required(t, p, "wage"), p + ".wage");
                Shop(x, t, p, market);
                // A stall or tea stall sells what it's given, at its own rate (capacityPerHour); the others work kg an hour.
                x.GramsPerHour = x.IsShop && t["recipes"] == null && t["kgPerHour"] == null ? x.Kind == ShopKind.Veg ? x.CapacityPerHour : 0
                    : (Bounded(t, p, "kgPerHour", "1", "10000") * Fixed.FromInt(1000)).RoundToLong();
                x.PowerPerKg = t["powerPerKg"] == null ? Fixed.Zero : Bounded(t, p, "powerPerKg", "0", "100");
                x.RepairsShare = Bounded(t, p, "repairsShare", "0", "1");
                x.OpenHour = BoundedInt(t, p, "openHour", 0, 23);
                x.CloseHour = BoundedInt(t, p, "closeHour", x.OpenHour + 1, 24);
                x.Fee = t["fee"] == null ? Fixed.Zero : Bounded(t, p, "fee", "0", "100");
                x.OwnPerDay = t["ownPerDay"] == null ? 0 : (Bounded(t, p, "ownPerDay", "0", "100000") * Fixed.FromInt(1000)).RoundToLong();
                x.TakesMilk = t["takesMilk"] != null && Bool(t["takesMilk"], p + ".takesMilk");
                x.NeighbourMilkPremium = t["neighbourMilkPremium"] == null ? Fixed.Zero : Bounded(t, p, "neighbourMilkPremium", "0", "100");
                if (t["recipes"] != null)
                {
                    if (t["input"] != null || t["outputs"] != null) throw new ContentException(p + ".recipes", "a type works by recipes or by an input and outputs, not both");
                    Recipes(x, t, p, market);
                    types.Add(x);
                    continue;
                }
                if (t["shops"] != null) throw new ContentException(p + ".shops", "only a type with recipes sells to the shops");
                if (x.IsShop) // a stall or tea stall: no input or outputs of its own
                {
                    if (t["input"] != null || t["outputs"] != null) throw new ContentException(p + ".input", "a shop sells goods, it doesn't grind them");
                    types.Add(x);
                    continue;
                }
                x.Input = Good(market, t, p, "input");
                JObject outs = Object(Required(t, p, "outputs"), p + ".outputs");
                var outputs = new List<BusinessOutput>();
                Fixed total = Fixed.Zero;
                foreach (JProperty prop in outs.Properties())
                {
                    int g = market.IndexOf(prop.Name);
                    if (g < 0) throw new ContentException(Join(p + ".outputs", prop.Name), "unknown good");
                    Fixed y = Number(prop.Value, Join(p + ".outputs", prop.Name));
                    Range(y, Fixed.Zero, Fixed.One, Join(p + ".outputs", prop.Name));
                    total += y;
                    outputs.Add(new BusinessOutput(g, y));
                }
                if (total > Fixed.One) throw new ContentException(p + ".outputs", "yields add up to more than 1");
                x.Outputs = outputs.AsReadOnly();
                types.Add(x);
            }
            d.Types = types.AsReadOnly();
            return d;
        }

        /// <summary>A shop's fields (spec 017 §3.1): its kind, a pitch's building, its rate a real hour, the goods it may put out, a cup of chai.</summary>
        private static void Shop(BusinessTypeDef x, JObject t, string p, MarketDefs market)
        {
            if (t["kind"] == null)
            {
                foreach (string f in new[] { "pitchBuilding", "capacityPerHour", "goods", "cup" })
                    if (t[f] != null) throw new ContentException(Join(p, f), "only a shop (with a kind) has this");
                return;
            }
            string kind = String(t["kind"], p + ".kind");
            x.Kind = kind == "veg" ? ShopKind.Veg : kind == "chai" ? ShopKind.Chai : kind == "sweets" ? ShopKind.Sweets
                : throw new ContentException(p + ".kind", "expected veg, chai or sweets");
            if ((x.Kind == ShopKind.Sweets) != (t["recipes"] != null)) throw new ContentException(p + ".recipes", "the sweet shop, and only it, makes by recipes");
            x.CapacityPerHour = BoundedInt(t, p, "capacityPerHour", 1, 100000) * (x.Kind == ShopKind.Chai ? 1L : 1000L);
            if (t["pitchBuilding"] != null)
            {
                JObject b = Object(t["pitchBuilding"], p + ".pitchBuilding");
                x.PitchBuildingValue = Money(Required(b, p + ".pitchBuilding", "value"), p + ".pitchBuilding.value");
                x.PitchBuildingLifeSeasons = BoundedInt(b, p + ".pitchBuilding", "lifeSeasons", 1, 400);
            }
            var goods = new List<int>();
            if (t["goods"] != null)
            {
                if (x.Kind == ShopKind.Sweets) throw new ContentException(p + ".goods", "the sweet shop sells what it makes");
                List<JToken> list = NonEmptyArray(t, p, "goods");
                for (int i = 0; i < list.Count; i++)
                {
                    string id = String(list[i], Index(p + ".goods", i));
                    int g = market.IndexOf(id);
                    if (g < 0 || market.Goods[g].Kind != GoodKind.Produce) throw new ContentException(Index(p + ".goods", i), "not a produce good '" + id + "'");
                    if (goods.Contains(g)) throw new ContentException(Index(p + ".goods", i), "listed twice");
                    goods.Add(g);
                }
            }
            x.Goods = goods.AsReadOnly();
            if ((x.Kind == ShopKind.Chai) != (t["cup"] != null)) throw new ContentException(p + ".cup", "the tea stall, and only it, sells cups");
            if (x.Kind == ShopKind.Chai)
            {
                if (t["takesMilk"] == null || !Bool(t["takesMilk"], p + ".takesMilk")) throw new ContentException(p + ".takesMilk", "the tea stall takes milk");
                string cp = p + ".cup";
                JObject c = Object(t["cup"], cp);
                OnlyFields(c, cp, "milkLitres", "supplies", "snackKg");
                x.CupMilkMl = (Bounded(c, cp, "milkLitres", "0.001", "1") * Fixed.FromInt(1000)).RoundToLong();
                x.CupSupplies = Bounded(c, cp, "supplies", "0", "100");
                x.SnackGrams = (Bounded(c, cp, "snackKg", "0", "1") * Fixed.FromInt(1000)).RoundToLong();
            }
            else if (t["takesMilk"] != null && Bool(t["takesMilk"], p + ".takesMilk")) throw new ContentException(p + ".takesMilk", "of the shops, only the tea stall takes milk");
        }

        /// <summary>Recipes (spec 016 §3.9c): each a batch of goods into one product; the shops' amounts a person for its products.</summary>
        private static void Recipes(BusinessTypeDef x, JObject t, string p, MarketDefs market)
        {
            var recipes = new List<RecipeDef>();
            var products = new List<int>();
            List<JToken> tokens = NonEmptyArray(t, p, "recipes");
            for (int i = 0; i < tokens.Count; i++)
            {
                string rp = Index(p + ".recipes", i);
                JObject r = Object(tokens[i], rp);
                OnlyFields(r, rp, "id", "inputs", "output", "outputKg", "fuel", "supplies");
                var inputs = new List<KeyValuePair<int, long>>();
                JObject ins = Object(Required(r, rp, "inputs"), rp + ".inputs");
                foreach (JProperty prop in ins.Properties())
                {
                    string ip = Join(rp + ".inputs", prop.Name);
                    int g = market.IndexOf(prop.Name);
                    if (g < 0) throw new ContentException(ip, "unknown good");
                    Fixed kg = Number(prop.Value, ip);
                    Range(kg, Fixed.FromRatio(1, 1000), Fixed.FromInt(10000), ip);
                    inputs.Add(new KeyValuePair<int, long>(g, kg.MulRatio(1000, 1).RoundToLong()));
                }
                if (inputs.Count == 0) throw new ContentException(rp + ".inputs", "needs at least one input");
                int output = Good(market, r, rp, "output");
                if (inputs.Exists(kv => kv.Key == output)) throw new ContentException(rp + ".output", "a recipe can't make its own input");
                recipes.Add(new RecipeDef
                {
                    Id = UniqueId(r, rp, recipes.ConvertAll(y => y.Id)), Inputs = inputs.AsReadOnly(), Output = output,
                    OutputGrams = (Bounded(r, rp, "outputKg", "0.001", "10000") * Fixed.FromInt(1000)).RoundToLong(),
                    Fuel = r["fuel"] == null ? Fixed.Zero : Bounded(r, rp, "fuel", "0", "100000"),
                    Supplies = r["supplies"] == null ? Fixed.Zero : Bounded(r, rp, "supplies", "0", "100000"),
                });
                if (!products.Contains(output)) products.Add(output);
            }
            x.Recipes = recipes.AsReadOnly();
            x.Products = products.AsReadOnly();
            var shops = new List<KeyValuePair<int, Fixed>>();
            if (t["shops"] != null)
            {
                string sp = p + ".shops";
                foreach (JProperty prop in Object(t["shops"], sp).Properties())
                {
                    int g = market.IndexOf(prop.Name);
                    if (g < 0 || !products.Contains(g)) throw new ContentException(Join(sp, prop.Name), "not one of its products");
                    Fixed kg = Number(prop.Value, Join(sp, prop.Name));
                    Range(kg, Fixed.Zero, Fixed.FromInt(10), Join(sp, prop.Name));
                    shops.Add(new KeyValuePair<int, Fixed>(g, kg));
                }
            }
            x.Shops = shops.AsReadOnly();
        }

        private static int Good(MarketDefs market, JObject o, string p, string field)
        {
            string id = String(Required(o, p, field), Join(p, field));
            int g = market.IndexOf(id);
            if (g < 0) throw new ContentException(Join(p, field), "unknown good '" + id + "'");
            return g;
        }
    }
}
