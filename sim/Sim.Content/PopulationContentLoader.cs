using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>Loads <c>content/population.json</c> (spec 014 §4) and checks its bounds and its links to the goods and eras.</summary>
        private static PopulationDefs LoadPopulation(string json, int house, MarketDefs market, OnboardingDefs onboarding)
        {
            const string root = "population";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "life", "budget", "basket", "needs", "flows", "house", "villages");
            var d = new PopulationDefs { House = house };

            string lp = root + ".life";
            JObject l = Object(Required(o, root, "life"), lp);
            OnlyFields(l, lp, "lifeYearsPerSeason", "adultFrom", "elderFrom", "retireAge", "minHealth", "healthLossPerSeason", "healthGainPerSeason",
                "primaryEducationShare", "applicantAge", "migratedHandAge");
            d.LifeYearsPerSeason = BoundedInt(l, lp, "lifeYearsPerSeason", 1, 4);
            d.AdultFrom = BoundedInt(l, lp, "adultFrom", 14, 21);
            d.ElderFrom = BoundedInt(l, lp, "elderFrom", d.AdultFrom + 1, 80);
            d.RetireAge = BoundedInt(l, lp, "retireAge", d.AdultFrom + 1, 80);
            d.MinHealth = Bounded(l, lp, "minHealth", "0", "1");
            d.HealthLossPerSeason = Bounded(l, lp, "healthLossPerSeason", "0", "1");
            d.HealthGainPerSeason = Bounded(l, lp, "healthGainPerSeason", "0", "1");
            d.PrimaryEducationShare = Bounded(l, lp, "primaryEducationShare", "0", "1");
            AgeRange(l, lp, "applicantAge", d.AdultFrom, d.RetireAge - 1, out int aMin, out int aMax);
            AgeRange(l, lp, "migratedHandAge", d.AdultFrom, d.RetireAge - 1, out int hMin, out int hMax);
            d.ApplicantAgeMin = aMin; d.ApplicantAgeMax = aMax; d.MigratedHandAgeMin = hMin; d.MigratedHandAgeMax = hMax;

            string bp = root + ".budget";
            JObject b = Object(Required(o, root, "budget"), bp);
            OnlyFields(b, bp, "remitShare", "outsideDaysShare", "engelCurve", "clothingShare", "healthShare", "educationShare", "leisureOfRest",
                "foodBasketPerPerson", "childEquivalent", "elderEquivalent");
            d.RemitShare = Bounded(b, bp, "remitShare", "0", "1");
            d.OutsideDaysShare = Bounded(b, bp, "outsideDaysShare", "0", "1");
            string ep = Join(bp, "engelCurve");
            List<JToken> points = Array(Required(b, bp, "engelCurve"), ep);
            if (points.Count < 2) throw new ContentException(ep, "the Engel curve needs at least 2 points");
            var curve = new List<EngelPoint>();
            for (int i = 0; i < points.Count; i++)
            {
                string pp = Index(ep, i);
                List<JToken> pair = Array(points[i], pp);
                if (pair.Count != 2) throw new ContentException(pp, "a point is [income, food share]");
                long income = Money(pair[0], pp + "[0]");
                Fixed share = Number(pair[1], pp + "[1]");
                Range(share, Fixed.Zero, Fixed.One, pp + "[1]");
                if (i > 0 && (income <= curve[i - 1].Income || share > curve[i - 1].FoodShare))
                    throw new ContentException(pp, "incomes rise and the food share never rises (Engel's law)");
                curve.Add(new EngelPoint(income, share));
            }
            if (curve[0].Income != 0) throw new ContentException(ep, "the curve starts at income 0");
            d.EngelCurve = curve.AsReadOnly();
            d.ClothingShare = Bounded(b, bp, "clothingShare", "0", "0.3");
            d.HealthShare = Bounded(b, bp, "healthShare", "0", "0.3");
            d.EducationShare = Bounded(b, bp, "educationShare", "0", "0.3");
            if (curve[0].FoodShare + d.ClothingShare + d.HealthShare + d.EducationShare > Fixed.One)
                throw new ContentException(bp, "food, clothing, health and education can't take more than the whole income");
            d.LeisureOfRest = Bounded(b, bp, "leisureOfRest", "0", "1");
            d.FoodBasketPerPerson = Money(Required(b, bp, "foodBasketPerPerson"), Join(bp, "foodBasketPerPerson"));
            d.ChildEquivalent = Bounded(b, bp, "childEquivalent", "0.1", "1");
            d.ElderEquivalent = Bounded(b, bp, "elderEquivalent", "0.1", "1");

            string kp = root + ".basket";
            JObject k = Object(Required(o, root, "basket"), kp);
            OnlyFields(k, kp, "realDaysPerGameDay", "kgPerRealDay");
            d.RealDaysPerGameDay = Bounded(k, kp, "realDaysPerGameDay", "1", "60");
            string gp = Join(kp, "kgPerRealDay");
            JObject goods = Object(Required(k, kp, "kgPerRealDay"), gp);
            var basket = new List<BasketItem>();
            // In good order, never JSON object order.
            for (int g = 0; g < market.Goods.Count; g++)
            {
                if (goods[market.Goods[g].Id] == null) continue;
                if (market.Goods[g].Kind != GoodKind.Produce) throw new ContentException(Join(gp, market.Goods[g].Id), "the basket holds food only");
                basket.Add(new BasketItem(g, Bounded(goods, gp, market.Goods[g].Id, "0", "2")));
            }
            foreach (JProperty p in goods.Properties())
                if (market.IndexOf(p.Name) < 0) throw new ContentException(Join(gp, p.Name), "unknown good '" + p.Name + "'");
            d.Basket = basket.AsReadOnly();

            string np = root + ".needs";
            JObject n = Object(Required(o, root, "needs"), np);
            OnlyFields(n, np, "survivalWeight", "securityWeight", "belongingWeight", "growthWeight", "gate", "chemist", "belonging", "leisure");
            d.SurvivalWeight = Bounded(n, np, "survivalWeight", "0", "1");
            d.SecurityWeight = Bounded(n, np, "securityWeight", "0", "1");
            d.BelongingWeight = Bounded(n, np, "belongingWeight", "0", "1");
            d.GrowthWeight = Bounded(n, np, "growthWeight", "0", "1");
            Fixed sum = d.SurvivalWeight + d.SecurityWeight + d.BelongingWeight + d.GrowthWeight, eps = Fixed.Parse("0.000001");
            if (sum < Fixed.One - eps || sum > Fixed.One + eps) throw new ContentException(np, "the weights must add up to 1");
            d.Gate = Bounded(n, np, "gate", "0.1", "1");
            d.Chemist = Bounded(n, np, "chemist", "0", "1");
            d.Belonging = Bounded(n, np, "belonging", "0", "1");
            d.Leisure = Bounded(n, np, "leisure", "0", "1");

            string fp = root + ".flows";
            JObject f = Object(Required(o, root, "flows"), fp);
            OnlyFields(f, fp, "birthChance", "crowdedBirthFactor", "birthAge", "maxChildren", "marriageChance", "marriageAge", "deathBase", "deathDoublingYears",
                "deathCap", "poorHealthBelow", "poorHealthDeathFactor", "mourning", "familyEveryHours", "familyJitterHours", "familySize", "leaveBelow", "leaveSeasons",
                "leaveChance", "rentBehindSeasons");
            d.BirthChance = Bounded(f, fp, "birthChance", "0", "1");
            d.CrowdedBirthFactor = Bounded(f, fp, "crowdedBirthFactor", "0", "1");
            AgeRange(f, fp, "birthAge", d.AdultFrom, d.ElderFrom, out int bMin, out int bMax);
            d.BirthAgeMin = bMin; d.BirthAgeMax = bMax;
            d.MaxChildren = BoundedInt(f, fp, "maxChildren", 0, 8);
            d.MarriageChance = Bounded(f, fp, "marriageChance", "0", "1");
            AgeRange(f, fp, "marriageAge", d.AdultFrom, d.ElderFrom, out int mMin, out int mMax);
            d.MarriageAgeMin = mMin; d.MarriageAgeMax = mMax;
            d.DeathBase = Bounded(f, fp, "deathBase", "0", "1");
            d.DeathDoublingYears = BoundedInt(f, fp, "deathDoublingYears", 1, 40);
            d.DeathCap = Bounded(f, fp, "deathCap", "0", "1");
            d.PoorHealthBelow = Bounded(f, fp, "poorHealthBelow", "0", "1");
            d.PoorHealthDeathFactor = Bounded(f, fp, "poorHealthDeathFactor", "1", "5");
            d.Mourning = BoundedInt(f, fp, "mourning", 0, 50);
            d.FamilyEveryHours = BoundedInt(f, fp, "familyEveryHours", 1, 240);
            d.FamilyJitterHours = BoundedInt(f, fp, "familyJitterHours", 0, d.FamilyEveryHours - 1);
            AgeRange(f, fp, "familySize", 2, 8, out int sMin, out int sMax);
            d.FamilySizeMin = sMin; d.FamilySizeMax = sMax;
            d.LeaveBelow = BoundedInt(f, fp, "leaveBelow", 0, 100);
            d.LeaveSeasons = BoundedInt(f, fp, "leaveSeasons", 1, 8);
            d.LeaveChance = Bounded(f, fp, "leaveChance", "0", "1");
            d.RentBehindSeasons = BoundedInt(f, fp, "rentBehindSeasons", 1, 8);

            string hp = root + ".house";
            JObject h = Object(Required(o, root, "house"), hp);
            OnlyFields(h, hp, "unlockEra", "capacity", "perAcre", "rentPerSeason");
            d.HouseUnlockEra = String(Required(h, hp, "unlockEra"), Join(hp, "unlockEra"));
            bool known = onboarding == null;
            if (onboarding != null) foreach (EraDef e in onboarding.Eras) if (e.Id == d.HouseUnlockEra) known = true;
            if (!known)
                throw new ContentException(Join(hp, "unlockEra"), "unknown era '" + d.HouseUnlockEra + "'");
            d.HouseCapacity = BoundedInt(h, hp, "capacity", 1, 20);
            if (d.HouseCapacity < d.FamilySizeMax) throw new ContentException(Join(hp, "capacity"), "a house must hold the largest arriving family");
            d.HousesPerAcre = BoundedInt(h, hp, "perAcre", 1, 8);
            d.RentPerSeason = Money(Required(h, hp, "rentPerSeason"), Join(hp, "rentPerSeason"));

            string vp = root + ".villages";
            List<JToken> villages = Array(Required(o, root, "villages"), vp);
            if (villages.Count == 0) throw new ContentException(vp, "at least one home village");
            var ids = new List<string>();
            for (int i = 0; i < villages.Count; i++)
            {
                string id = String(villages[i], Index(vp, i));
                if (ids.Contains(id)) throw new ContentException(Index(vp, i), "duplicate village '" + id + "'");
                ids.Add(id);
            }
            d.Villages = ids.AsReadOnly();
            return d;
        }

        private static void AgeRange(JObject o, string path, string name, int low, int high, out int min, out int max)
        {
            string p = Join(path, name);
            JObject r = Object(Required(o, path, name), p);
            OnlyFields(r, p, "min", "max");
            min = BoundedInt(r, p, "min", low, high);
            max = BoundedInt(r, p, "max", min, high);
        }
    }
}
