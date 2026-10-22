using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>Loads <c>content/livestock.json</c> (spec 012 §4) and checks its bounds and its links to the map, the shed and the eras.</summary>
        private static LivestockDefs LoadLivestock(string json, int shed, List<BuildingDef> buildings, MapDef map, MarketDefs market, OnboardingDefs onboarding)
        {
            const string root = "livestock";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "livestock", "animals");
            BuildingDef shedDef = buildings[shed];
            if (shedDef.Animals < 1) throw new ContentException("buildings.cattle_shed.animals", "a cattle shed must hold at least one animal");

            string lp = root + ".livestock";
            JObject l = Object(Required(o, root, "livestock"), lp);
            OnlyFields(l, lp, "realDaysPerGameDay", "feedHour", "shedTile", "unlockEra", "householdLitresPerDay", "milkBasePerLitre", "milkPerFatPoint",
                "commonsShare", "minMilkFactor", "hungryBelow", "hungerGraceDays", "hungerHealthLoss", "healthRecoveryPerDay", "minHealth", "feedWeights",
                "lowDays", "diseaseChance", "unvaccinatedFactor", "sickHealthFactor", "sickHealthLoss", "vaccineCost", "vetVisitCost", "lactationCurve", "names");

            int shedTile = BoundedInt(l, lp, "shedTile", 0, map.Tiles.Count - 1);
            if (map.Tiles[shedTile].Terrain != Terrain.Yard) throw new ContentException(Join(lp, "shedTile"), "the shed goes on a yard acre");
            string eraId = String(Required(l, lp, "unlockEra"), Join(lp, "unlockEra"));
            int era = 0;
            if (onboarding != null)
            {
                era = -1;
                for (int i = 0; i < onboarding.Eras.Count; i++) if (onboarding.Eras[i].Id == eraId) era = i;
                if (era < 0) throw new ContentException(Join(lp, "unlockEra"), "unknown era '" + eraId + "'");
            }

            string wp = Join(lp, "feedWeights");
            JObject w = Object(Required(l, lp, "feedWeights"), wp);
            OnlyFields(w, wp, "green", "straw", "feed");
            Fixed green = Bounded(w, wp, "green", "0", "1"), straw = Bounded(w, wp, "straw", "0", "1"), feed = Bounded(w, wp, "feed", "0", "1");
            Fixed sum = green + straw + feed, eps = Fixed.Parse("0.000001");
            if (sum < Fixed.One - eps || sum > Fixed.One + eps) throw new ContentException(wp, "the weights must add up to 1");

            var def = new LivestockDef
            {
                RealDaysPerGameDay = Bounded(l, lp, "realDaysPerGameDay", "1", "60"),
                FeedHour = BoundedInt(l, lp, "feedHour", 0, 23),
                ShedTile = shedTile,
                UnlockEra = eraId,
                HouseholdLitresPerDay = Bounded(l, lp, "householdLitresPerDay", "0", "10"),
                MilkBasePerLitre = Bounded(l, lp, "milkBasePerLitre", "0", "200"),
                MilkPerFatPoint = Bounded(l, lp, "milkPerFatPoint", "0", "50"),
                CommonsShare = SeasonFactors(l, lp, "commonsShare", "0", "1"),
                MinMilkFactor = Bounded(l, lp, "minMilkFactor", "0", "1"),
                HungryBelow = Bounded(l, lp, "hungryBelow", "0", "1"),
                HungerGraceDays = BoundedInt(l, lp, "hungerGraceDays", 1, 30),
                HungerHealthLoss = Bounded(l, lp, "hungerHealthLoss", "0", "1"),
                HealthRecoveryPerDay = Bounded(l, lp, "healthRecoveryPerDay", "0", "1"),
                MinHealth = Bounded(l, lp, "minHealth", "0.1", "1"),
                GreenWeight = green,
                StrawWeight = straw,
                FeedWeight = feed,
                LowDays = BoundedInt(l, lp, "lowDays", 1, 10),
                DiseaseChance = Bounded(l, lp, "diseaseChance", "0", "0.2"),
                UnvaccinatedFactor = Bounded(l, lp, "unvaccinatedFactor", "1", "10"),
                SickHealthFactor = Bounded(l, lp, "sickHealthFactor", "0", "10"),
                SickHealthLoss = Bounded(l, lp, "sickHealthLoss", "0", "0.5"),
                VaccineCost = Money(Required(l, lp, "vaccineCost"), Join(lp, "vaccineCost")),
                VetVisitCost = Money(Required(l, lp, "vetVisitCost"), Join(lp, "vetVisitCost")),
                LactationCurve = Curve(l, lp, "lactationCurve"),
                Names = Names(l, lp),
            };

            var animals = new List<AnimalDef>();
            List<JToken> tokens = NonEmptyArray(o, root, "animals");
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".animals", i);
                JObject a = Object(tokens[i], p);
                OnlyFields(a, p, "id", "nameKey", "price", "milkPerDay", "fatPercent", "seasonMilk", "milkDays", "dryDays", "ration", "dryFeedFactor",
                    "labourHoursPerDay", "dungCompostKgPerDay", "calfPrice", "sellShare");
                string id = UniqueId(a, p, animals.ConvertAll(x => x.Id));
                long price = Money(Required(a, p, "price"), Join(p, "price"));
                if (price <= 0) throw new ContentException(Join(p, "price"), "must be more than 0");
                string rp = Join(p, "ration");
                JObject r = Object(Required(a, p, "ration"), rp);
                OnlyFields(r, rp, "green", "straw", "feed");
                int milkDays = BoundedInt(a, p, "milkDays", 1, 40);
                Fixed last = def.LactationCurve[def.LactationCurve.Count - 1].X;
                if (Fixed.FromInt(milkDays) > last) throw new ContentException(Join(p, "milkDays"), "the lactation curve must reach day " + milkDays);
                animals.Add(new AnimalDef(id, String(Required(a, p, "nameKey"), Join(p, "nameKey")), price,
                    Bounded(a, p, "milkPerDay", "0", "40"), Bounded(a, p, "fatPercent", "0", "15"), SeasonFactors(a, p, "seasonMilk", "0", "2"),
                    milkDays, BoundedInt(a, p, "dryDays", 0, 20),
                    Bounded(r, rp, "green", "0", "100"), Bounded(r, rp, "straw", "0", "40"), Bounded(r, rp, "feed", "0", "20"),
                    Bounded(a, p, "dryFeedFactor", "0", "1"), Bounded(a, p, "labourHoursPerDay", "0", "8"),
                    Bounded(a, p, "dungCompostKgPerDay", "0", "50"), Money(Required(a, p, "calfPrice"), Join(p, "calfPrice")),
                    Bounded(a, p, "sellShare", "0", "1")));
            }
            return new LivestockDefs(def, animals, shed, era);
        }

        private static List<Fixed> SeasonFactors(JObject o, string path, string name, string min, string max)
        {
            string p = Join(path, name);
            JObject s = Object(Required(o, path, name), p);
            OnlyFields(s, p, SeasonIds);
            var list = new List<Fixed>();
            foreach (string season in SeasonIds) list.Add(Bounded(s, p, season, min, max));
            return list;
        }

        /// <summary>[[day, factor], …] from day 0, ascending (spec 012 §3.2).</summary>
        private static List<CurvePoint> Curve(JObject o, string path, string name)
        {
            string cp = Join(path, name);
            List<JToken> points = NonEmptyArray(o, path, name);
            var curve = new List<CurvePoint>();
            for (int i = 0; i < points.Count; i++)
            {
                string p = Index(cp, i);
                List<JToken> xy = Array(points[i], p);
                if (xy.Count != 2) throw new ContentException(p, "expected [day, factor]");
                Fixed x = Number(xy[0], p + "[0]"), y = Number(xy[1], p + "[1]");
                Range(x, Fixed.Zero, Fixed.FromInt(60), p + "[0]");
                Range(y, Fixed.Zero, Fixed.FromInt(2), p + "[1]");
                if (i > 0 && x <= curve[i - 1].X) throw new ContentException(p + "[0]", "days must be ascending");
                curve.Add(new CurvePoint(x, y));
            }
            if (curve.Count < 2 || curve[0].X != Fixed.Zero) throw new ContentException(cp, "must start at day 0 with at least 2 points");
            return curve;
        }

        private static List<string> Names(JObject o, string path)
        {
            List<JToken> tokens = NonEmptyArray(o, path, "names");
            var names = new List<string>();
            for (int i = 0; i < tokens.Count; i++)
            {
                string n = String(tokens[i], Index(Join(path, "names"), i));
                if (names.Contains(n)) throw new ContentException(Index(Join(path, "names"), i), "duplicate name '" + n + "'");
                names.Add(n);
            }
            if (names.Count < 8) throw new ContentException(Join(path, "names"), "needs at least 8 names");
            return names;
        }
    }
}
