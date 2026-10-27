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
                OnlyFields(t, p, "id", "nameKey", "era", "building", "equipment", "buildHours", "worker", "wage", "kgPerHour", "powerPerKg", "repairsShare",
                    "openHour", "closeHour", "fee", "ownPerDay", "input", "outputs");
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
                x.Wage = Money(Required(t, p, "wage"), p + ".wage");
                x.GramsPerHour = (Bounded(t, p, "kgPerHour", "1", "10000") * Fixed.FromInt(1000)).RoundToLong();
                x.PowerPerKg = Bounded(t, p, "powerPerKg", "0", "100");
                x.RepairsShare = Bounded(t, p, "repairsShare", "0", "1");
                x.OpenHour = BoundedInt(t, p, "openHour", 0, 23);
                x.CloseHour = BoundedInt(t, p, "closeHour", x.OpenHour + 1, 24);
                x.Fee = Bounded(t, p, "fee", "0", "100");
                x.OwnPerDay = (Bounded(t, p, "ownPerDay", "0", "100000") * Fixed.FromInt(1000)).RoundToLong();
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

        private static int Good(MarketDefs market, JObject o, string p, string field)
        {
            string id = String(Required(o, p, field), Join(p, field));
            int g = market.IndexOf(id);
            if (g < 0) throw new ContentException(Join(p, field), "unknown good '" + id + "'");
            return g;
        }
    }
}
