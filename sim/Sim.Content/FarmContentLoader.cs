using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>Loads <c>crops.json</c>, <c>farming.json</c>, <c>trees.json</c> and <c>founder.json</c> (spec 003 §4).</summary>
        private static FarmDefs LoadFarmDefs(string cropsJson, string farmingJson, string treesJson, string founderJson,
            MarketDefs market, WeatherSet weather)
        {
            List<CropDef> crops;
            List<GardenCropDef> garden;
            LoadCrops(cropsJson, market, out crops, out garden);
            List<FertiliserDef> fertilisers;
            FarmDef farm = LoadFarming(farmingJson, market, weather, out fertilisers);
            List<TreeDef> trees = LoadTrees(treesJson, market);
            List<DietPlanDef> plans;
            List<WorkHoursDef> hours;
            FounderDef founder = LoadFounder(founderJson, out plans, out hours);
            return new FarmDefs(crops, garden, farm, fertilisers, trees, founder, plans, hours);
        }

        private static void LoadCrops(string json, MarketDefs market, out List<CropDef> crops, out List<GardenCropDef> garden)
        {
            const string root = "crops";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "crops", "gardenCrops");
            crops = new List<CropDef>();
            List<JToken> tokens = NonEmptyArray(o, root, "crops");
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".crops", i);
                JObject c = Object(tokens[i], p);
                OnlyFields(c, p, "id", "nameKey", "outputGood", "seedGood", "sowSeasons", "growthMinutes", "seasonYieldPerAcre",
                    "seedPerAcrePerSeason", "moistureMin", "moistureMax", "labourPerAcrePerDay", "fertilityPerSeason", "canSaveSeed",
                    "savedSeedLossPerSeason", "savedSeedFloor");
                string id = UniqueId(c, p, crops.ConvertAll(x => x.Id));
                int output = GoodRef(c, p, "outputGood", market, GoodKind.Produce);
                int seed = GoodRef(c, p, "seedGood", market, GoodKind.Input);
                bool[] seasons = Seasons(c, p, "sowSeasons");
                int minutes = Int(Required(c, p, "growthMinutes"), p + ".growthMinutes");
                Range(minutes, 1, 20160, p + ".growthMinutes");
                Fixed yield = Number(Required(c, p, "seasonYieldPerAcre"), p + ".seasonYieldPerAcre");
                if (yield <= Fixed.Zero) throw new ContentException(p + ".seasonYieldPerAcre", "must be more than 0");
                Fixed seedKg = Bounded(c, p, "seedPerAcrePerSeason", "0", "100000");
                Fixed mMin = Bounded(c, p, "moistureMin", "0", "100");
                Fixed mMax = Bounded(c, p, "moistureMax", "0", "100");
                if (mMin <= Fixed.Zero || mMin >= mMax || mMax >= Fixed.FromInt(100))
                    throw new ContentException(p + ".moistureMin", "needs 0 < moistureMin < moistureMax < 100");
                bool canSave = Bool(Required(c, p, "canSaveSeed"), p + ".canSaveSeed");
                Fixed loss = Fixed.Zero, floor = Fixed.One;
                if (canSave)
                {
                    loss = Bounded(c, p, "savedSeedLossPerSeason", "0", "0.5");
                    floor = Bounded(c, p, "savedSeedFloor", "0.5", "1");
                }
                crops.Add(new CropDef(id, String(Required(c, p, "nameKey"), p + ".nameKey"), output, seed, seasons, minutes, yield, seedKg,
                    mMin, mMax, Bounded(c, p, "labourPerAcrePerDay", "0", "24"), Bounded(c, p, "fertilityPerSeason", "-20", "20"),
                    canSave, loss, floor));
            }

            garden = new List<GardenCropDef>();
            List<JToken> gTokens = NonEmptyArray(o, root, "gardenCrops");
            for (int i = 0; i < gTokens.Count; i++)
            {
                string p = Index(root + ".gardenCrops", i);
                JObject g = Object(gTokens[i], p);
                OnlyFields(g, p, "id", "nameKey", "good", "seedGood", "growthMinutes", "harvestKg", "seedGramsPerSowing");
                string id = UniqueId(g, p, garden.ConvertAll(x => x.Id));
                int minutes = Int(Required(g, p, "growthMinutes"), p + ".growthMinutes");
                Range(minutes, 1, 120, p + ".growthMinutes");
                Fixed kg = Number(Required(g, p, "harvestKg"), p + ".harvestKg");
                if (kg <= Fixed.Zero) throw new ContentException(p + ".harvestKg", "must be more than 0");
                int seedGrams = Int(Required(g, p, "seedGramsPerSowing"), p + ".seedGramsPerSowing");
                Range(seedGrams, 0, 100000, p + ".seedGramsPerSowing");
                garden.Add(new GardenCropDef(id, String(Required(g, p, "nameKey"), p + ".nameKey"),
                    GoodRef(g, p, "good", market, GoodKind.Produce), GoodRef(g, p, "seedGood", market, GoodKind.Input),
                    minutes, kg.MulRatio(1000, 1).RoundToLong(), seedGrams));
            }
        }

        private static FarmDef LoadFarming(string json, MarketDefs market, WeatherSet weather, out List<FertiliserDef> fertilisers)
        {
            const string root = "farming";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "farm", "fertilisers");
            string fp = root + ".farm";
            JObject f = Object(Required(o, root, "farm"), fp);
            OnlyFields(f, fp, "yieldCap", "careCap", "toolsFactor", "weatherYieldFactor", "waterDryFloor", "waterWetFloor",
                "bucketHoursPerMoisturePoint", "gardenSeasonOutputKg", "readyGraceHours");

            // Stored in the declared order of the weather types, never in JSON object order.
            string wp = fp + ".weatherYieldFactor";
            JObject w = Object(Required(f, fp, "weatherYieldFactor"), wp);
            var factors = new Fixed?[weather.Types.Count];
            foreach (JProperty prop in w.Properties())
            {
                int k = weather.IndexOf(prop.Name);
                if (k < 0) throw new ContentException(Join(wp, prop.Name), "unknown weather id");
                factors[k] = Number(prop.Value, Join(wp, prop.Name));
                Range(factors[k].Value, Fixed.Zero, Fixed.Parse("1.2"), Join(wp, prop.Name));
            }
            var list = new List<Fixed>();
            for (int k = 0; k < factors.Length; k++)
            {
                if (factors[k] == null) throw new ContentException(wp, "missing weather id '" + weather.Types[k].Id + "'");
                list.Add(factors[k].Value);
            }
            int grace = Int(Required(f, fp, "readyGraceHours"), fp + ".readyGraceHours");
            Range(grace, 0, 720, fp + ".readyGraceHours");
            Fixed gardenKg = Number(Required(f, fp, "gardenSeasonOutputKg"), fp + ".gardenSeasonOutputKg");
            if (gardenKg <= Fixed.Zero) throw new ContentException(fp + ".gardenSeasonOutputKg", "must be more than 0");
            var farm = new FarmDef(Bounded(f, fp, "yieldCap", "1", "3"), Bounded(f, fp, "careCap", "1", "1.5"),
                Bounded(f, fp, "toolsFactor", "0.5", "2"), list, Bounded(f, fp, "waterDryFloor", "0", "1"),
                Bounded(f, fp, "waterWetFloor", "0", "1"), Bounded(f, fp, "bucketHoursPerMoisturePoint", "0", "2"),
                gardenKg.MulRatio(1000, 1).RoundToLong(), grace * 3600L);

            fertilisers = new List<FertiliserDef>();
            List<JToken> tokens = NonEmptyArray(o, root, "fertilisers");
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".fertilisers", i);
                JObject x = Object(tokens[i], p);
                OnlyFields(x, p, "id", "good", "kgPerAcrePerSeason", "yieldFactor", "fertilityPerSeason");
                string id = UniqueId(x, p, fertilisers.ConvertAll(y => y.Id));
                int good = x["good"] == null ? -1 : GoodRef(x, p, "good", market, GoodKind.Input);
                fertilisers.Add(new FertiliserDef(id, good, Bounded(x, p, "kgPerAcrePerSeason", "0", "100000"),
                    Bounded(x, p, "yieldFactor", "0.5", "2"), Bounded(x, p, "fertilityPerSeason", "-20", "20")));
            }
            if (fertilisers.Find(x => x.Id == "none" && x.Good < 0) == null)
                throw new ContentException(root + ".fertilisers", "needs 'none' (no good)");
            return farm;
        }

        private static List<TreeDef> LoadTrees(string json, MarketDefs market)
        {
            const string root = "trees";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "trees");
            var trees = new List<TreeDef>();
            List<JToken> tokens = NonEmptyArray(o, root, "trees");
            for (int i = 0; i < tokens.Count; i++)
            {
                string p = Index(root + ".trees", i);
                JObject t = Object(tokens[i], p);
                OnlyFields(t, p, "id", "nameKey", "saplingGood", "fruitGood", "growthMinutes", "fruitingSeasons", "batchYieldPerTree",
                    "harvestLimit", "terrain");
                string id = UniqueId(t, p, trees.ConvertAll(x => x.Id));
                int sapling = GoodRef(t, p, "saplingGood", market, GoodKind.Input);
                if (market.Goods[sapling].Unit != GoodUnit.Each) throw new ContentException(p + ".saplingGood", "a sapling is sold each");
                int minutes = Int(Required(t, p, "growthMinutes"), p + ".growthMinutes");
                Range(minutes, 1, 1_000_000, p + ".growthMinutes");
                Fixed kg = Number(Required(t, p, "batchYieldPerTree"), p + ".batchYieldPerTree");
                if (kg <= Fixed.Zero) throw new ContentException(p + ".batchYieldPerTree", "must be more than 0");
                int limit = Int(Required(t, p, "harvestLimit"), p + ".harvestLimit");
                Range(limit, 0, 1000, p + ".harvestLimit");
                string terrain = String(Required(t, p, "terrain"), p + ".terrain");
                if (terrain != "any" && terrain != "hill") throw new ContentException(p + ".terrain", "expected any or hill");
                trees.Add(new TreeDef(id, String(Required(t, p, "nameKey"), p + ".nameKey"), sapling,
                    GoodRef(t, p, "fruitGood", market, GoodKind.Produce), minutes, Seasons(t, p, "fruitingSeasons"),
                    kg.MulRatio(1000, 1).RoundToLong(), limit, terrain == "hill"));
            }
            return trees;
        }

        private static FounderDef LoadFounder(string json, out List<DietPlanDef> plans, out List<WorkHoursDef> hours)
        {
            const string root = "founder";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "founder", "dietPlans", "workHours");

            plans = new List<DietPlanDef>();
            List<JToken> pTokens = NonEmptyArray(o, root, "dietPlans");
            for (int i = 0; i < pTokens.Count; i++)
            {
                string p = Index(root + ".dietPlans", i);
                JObject d = Object(pTokens[i], p);
                OnlyFields(d, p, "id", "nameKey", "costPerSeason", "nutrition", "foodShare");
                string id = UniqueId(d, p, plans.ConvertAll(x => x.Id));
                long cost = Money(Required(d, p, "costPerSeason"), p + ".costPerSeason");
                if (id == DietPlanDef.SubsistenceId && cost != 0) throw new ContentException(p + ".costPerSeason", "subsistence costs 0");
                plans.Add(new DietPlanDef(id, String(Required(d, p, "nameKey"), p + ".nameKey"), cost,
                    Bounded(d, p, "nutrition", "0.8", "1.05"), Bounded(d, p, "foodShare", "0", "1")));
            }
            if (plans.Find(x => x.Id == DietPlanDef.SubsistenceId) == null)
                throw new ContentException(root + ".dietPlans", "needs '" + DietPlanDef.SubsistenceId + "'");

            hours = new List<WorkHoursDef>();
            List<JToken> hTokens = NonEmptyArray(o, root, "workHours");
            for (int i = 0; i < hTokens.Count; i++)
            {
                string p = Index(root + ".workHours", i);
                JObject h = Object(hTokens[i], p);
                OnlyFields(h, p, "id", "hours", "fatigueChange");
                string id = UniqueId(h, p, hours.ConvertAll(x => x.Id));
                hours.Add(new WorkHoursDef(id, Bounded(h, p, "hours", "0", "14"), Bounded(h, p, "fatigueChange", "-1", "1")));
            }
            if (hours.Find(x => x.Id == WorkHoursDef.NormalId) == null)
                throw new ContentException(root + ".workHours", "needs '" + WorkHoursDef.NormalId + "'");

            string fp = root + ".founder";
            JObject f = Object(Required(o, root, "founder"), fp);
            OnlyFields(f, fp, "startDietPlan", "startWorkHours", "maxFatigue", "recoveryPerDay", "goodNutrition", "nutritionPenalty",
                "safeFatigue", "fatiguePenalty", "minHealth", "sickChanceAtMinHealth", "dayLabourWagePerHour", "noWarmClothingPenalty",
                "medicineCost", "medicineHealthGain", "startDayLabourShare", "startEatFromStore");
            int share = Int(Required(f, fp, "startDayLabourShare"), fp + ".startDayLabourShare");
            if (share % 25 != 0 || share < 0 || share > 100) throw new ContentException(fp + ".startDayLabourShare", "must be 0, 25, 50, 75 or 100");
            long wage = Money(Required(f, fp, "dayLabourWagePerHour"), fp + ".dayLabourWagePerHour");
            if (wage <= 0) throw new ContentException(fp + ".dayLabourWagePerHour", "must be more than 0");
            long medicine = Money(Required(f, fp, "medicineCost"), fp + ".medicineCost");
            if (medicine <= 0) throw new ContentException(fp + ".medicineCost", "must be more than 0");
            return new FounderDef(Ref(f, fp, "startDietPlan", plans.ConvertAll(x => x.Id)), Ref(f, fp, "startWorkHours", hours.ConvertAll(x => x.Id)),
                Bounded(f, fp, "maxFatigue", "0", "1"), Bounded(f, fp, "recoveryPerDay", "0", "1"), Bounded(f, fp, "goodNutrition", "0", "1"),
                Bounded(f, fp, "nutritionPenalty", "0", "1"), Bounded(f, fp, "safeFatigue", "0", "1"), Bounded(f, fp, "fatiguePenalty", "0", "1"),
                Bounded(f, fp, "minHealth", "0", "1"), Bounded(f, fp, "sickChanceAtMinHealth", "0", "1"), wage,
                Bounded(f, fp, "noWarmClothingPenalty", "0", "1"), medicine, Bounded(f, fp, "medicineHealthGain", "0", "1"),
                share, Bool(Required(f, fp, "startEatFromStore"), fp + ".startEatFromStore"));
        }

        /// <summary>A good reference that must be of the given kind.</summary>
        private static int GoodRef(JObject o, string path, string name, MarketDefs market, GoodKind kind)
        {
            int g = Ref(o, path, name, GoodIds(market));
            if (market.Goods[g].Kind != kind)
                throw new ContentException(Join(path, name), "'" + market.Goods[g].Id + "' must be " + (kind == GoodKind.Produce ? "a produce" : "an input") + " good");
            return g;
        }

        /// <summary>A non-empty list of season ids as a bool per season.</summary>
        private static bool[] Seasons(JObject o, string path, string name)
        {
            string p = Join(path, name);
            List<JToken> list = NonEmptyArray(o, path, name);
            var seasons = new bool[SeasonIds.Length];
            for (int i = 0; i < list.Count; i++)
            {
                string id = String(list[i], Index(p, i));
                int s = System.Array.IndexOf(SeasonIds, id);
                if (s < 0) throw new ContentException(Index(p, i), "unknown season '" + id + "'");
                seasons[s] = true;
            }
            return seasons;
        }
    }
}
