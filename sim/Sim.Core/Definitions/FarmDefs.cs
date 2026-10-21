using System;
using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>A field crop from <c>content/crops.json</c> (spec 003 §4). Quantities are per acre per season (§3.1).</summary>
    public sealed class CropDef
    {
        public string Id { get; }
        public string NameKey { get; }

        /// <summary>Indices into <see cref="MarketDefs.Goods"/>.</summary>
        public int OutputGood { get; }
        public int SeedGood { get; }

        /// <summary>Indexed by season (Spring, Summer, Monsoon, Winter).</summary>
        public IReadOnlyList<bool> SowSeasons { get; }
        public int GrowthMinutes { get; }
        public Fixed SeasonYieldPerAcre { get; }
        public Fixed SeedPerAcrePerSeason { get; }
        public Fixed MoistureMin { get; }
        public Fixed MoistureMax { get; }
        public Fixed LabourPerAcrePerDay { get; }

        /// <summary>Fertility used per season of growth; negative restores (lentils).</summary>
        public Fixed FertilityPerSeason { get; }
        public bool CanSaveSeed { get; }
        public Fixed SavedSeedLossPerSeason { get; }
        public Fixed SavedSeedFloor { get; }

        public CropDef(string id, string nameKey, int outputGood, int seedGood, IReadOnlyList<bool> sowSeasons, int growthMinutes,
            Fixed seasonYieldPerAcre, Fixed seedPerAcrePerSeason, Fixed moistureMin, Fixed moistureMax, Fixed labourPerAcrePerDay,
            Fixed fertilityPerSeason, bool canSaveSeed, Fixed savedSeedLossPerSeason, Fixed savedSeedFloor)
        {
            Id = id;
            NameKey = nameKey;
            OutputGood = outputGood;
            SeedGood = seedGood;
            SowSeasons = new List<bool>(sowSeasons).AsReadOnly();
            GrowthMinutes = growthMinutes;
            SeasonYieldPerAcre = seasonYieldPerAcre;
            SeedPerAcrePerSeason = seedPerAcrePerSeason;
            MoistureMin = moistureMin;
            MoistureMax = moistureMax;
            LabourPerAcrePerDay = labourPerAcrePerDay;
            FertilityPerSeason = fertilityPerSeason;
            CanSaveSeed = canSaveSeed;
            SavedSeedLossPerSeason = savedSeedLossPerSeason;
            SavedSeedFloor = savedSeedFloor;
        }

        public long GrowthSeconds { get { return GrowthMinutes * 60L; } }
    }

    /// <summary>A kitchen-garden crop (spec 003 §3.13): no factors, a fixed basket per picking.</summary>
    public sealed class GardenCropDef
    {
        public string Id { get; }
        public string NameKey { get; }
        public int Good { get; }
        public int SeedGood { get; }
        public int GrowthMinutes { get; }

        /// <summary>Grams per picking.</summary>
        public long HarvestGrams { get; }
        public long SeedGramsPerSowing { get; }

        public GardenCropDef(string id, string nameKey, int good, int seedGood, int growthMinutes, long harvestGrams, long seedGramsPerSowing)
        {
            Id = id;
            NameKey = nameKey;
            Good = good;
            SeedGood = seedGood;
            GrowthMinutes = growthMinutes;
            HarvestGrams = harvestGrams;
            SeedGramsPerSowing = seedGramsPerSowing;
        }
    }

    /// <summary><c>FarmDef</c> from <c>content/farming.json</c> (spec 003 §4).</summary>
    public sealed class FarmDef
    {
        public Fixed YieldCap { get; }
        public Fixed CareCap { get; }
        public Fixed ToolsFactor { get; }

        /// <summary>Indexed like <see cref="WeatherSet.Types"/>.</summary>
        public IReadOnlyList<Fixed> WeatherYieldFactor { get; }
        public Fixed WaterDryFloor { get; }
        public Fixed WaterWetFloor { get; }
        public Fixed BucketHoursPerMoisturePoint { get; }

        /// <summary>Grams per bed per season.</summary>
        public long GardenSeasonOutputGrams { get; }

        /// <summary>How long a ready crop or ripe batch waits without loss before it spoils (ADR 0034), seconds.</summary>
        public long ReadyGraceSeconds { get; }

        public FarmDef(Fixed yieldCap, Fixed careCap, Fixed toolsFactor, IReadOnlyList<Fixed> weatherYieldFactor, Fixed waterDryFloor,
            Fixed waterWetFloor, Fixed bucketHoursPerMoisturePoint, long gardenSeasonOutputGrams, long readyGraceSeconds)
        {
            ReadyGraceSeconds = readyGraceSeconds;
            YieldCap = yieldCap;
            CareCap = careCap;
            ToolsFactor = toolsFactor;
            WeatherYieldFactor = new List<Fixed>(weatherYieldFactor).AsReadOnly();
            WaterDryFloor = waterDryFloor;
            WaterWetFloor = waterWetFloor;
            BucketHoursPerMoisturePoint = bucketHoursPerMoisturePoint;
            GardenSeasonOutputGrams = gardenSeasonOutputGrams;
        }
    }

    /// <summary>None, compost or chemical (spec 003 §4).</summary>
    public sealed class FertiliserDef
    {
        public string Id { get; }

        /// <summary>The input good, or −1 for none.</summary>
        public int Good { get; }
        public Fixed KgPerAcrePerSeason { get; }
        public Fixed YieldFactor { get; }
        public Fixed FertilityPerSeason { get; }

        /// <summary>Scales the soil's drying while a crop grown with it stands (ADR 0051): compost 0.8 holds water, 1 elsewhere.</summary>
        public Fixed DryingFactor { get; }

        public FertiliserDef(string id, int good, Fixed kgPerAcrePerSeason, Fixed yieldFactor, Fixed fertilityPerSeason, Fixed dryingFactor)
        {
            Id = id;
            Good = good;
            KgPerAcrePerSeason = kgPerAcrePerSeason;
            YieldFactor = yieldFactor;
            FertilityPerSeason = fertilityPerSeason;
            DryingFactor = dryingFactor;
        }
    }

    /// <summary>A fruit tree from <c>content/trees.json</c> (spec 003 §4, ADR 0021).</summary>
    public sealed class TreeDef
    {
        public string Id { get; }
        public string NameKey { get; }
        public int SaplingGood { get; }
        public int FruitGood { get; }
        public int GrowthMinutes { get; }
        public IReadOnlyList<bool> FruitingSeasons { get; }

        /// <summary>Grams per batch.</summary>
        public long BatchGrams { get; }

        /// <summary>Batches before the tree is spent; 0 = the whole game.</summary>
        public int HarvestLimit { get; }

        /// <summary>Hill-only trees (apple, later) can't be planted on the Phase 1 map.</summary>
        public bool HillOnly { get; }

        /// <summary>The share of a field-edge acre's yield a mature tree shades away (ADR 0037).</summary>
        public Fixed Shade { get; }

        public TreeDef(string id, string nameKey, int saplingGood, int fruitGood, int growthMinutes, IReadOnlyList<bool> fruitingSeasons,
            long batchGrams, int harvestLimit, bool hillOnly, Fixed shade)
        {
            Shade = shade;
            Id = id;
            NameKey = nameKey;
            SaplingGood = saplingGood;
            FruitGood = fruitGood;
            GrowthMinutes = growthMinutes;
            FruitingSeasons = new List<bool>(fruitingSeasons).AsReadOnly();
            BatchGrams = batchGrams;
            HarvestLimit = harvestLimit;
            HillOnly = hillOnly;
        }

        public long GrowthSeconds { get { return GrowthMinutes * 60L; } }
    }

    /// <summary>A diet plan (spec 003 §4, ADR 0010).</summary>
    public sealed class DietPlanDef
    {
        public const string SubsistenceId = "subsistence";

        public string Id { get; }
        public string NameKey { get; }

        /// <summary>Money per season.</summary>
        public long CostPerSeason { get; }
        public Fixed Nutrition { get; }
        public Fixed FoodShare { get; }

        public DietPlanDef(string id, string nameKey, long costPerSeason, Fixed nutrition, Fixed foodShare)
        {
            Id = id;
            NameKey = nameKey;
            CostPerSeason = costPerSeason;
            Nutrition = nutrition;
            FoodShare = foodShare;
        }
    }

    /// <summary>A working-hours option (spec 003 §4).</summary>
    public sealed class WorkHoursDef
    {
        public const string NormalId = "normal";

        public string Id { get; }
        public Fixed Hours { get; }
        public Fixed FatigueChange { get; }

        public WorkHoursDef(string id, Fixed hours, Fixed fatigueChange)
        {
            Id = id;
            Hours = hours;
            FatigueChange = fatigueChange;
        }
    }

    /// <summary><c>FounderDef</c> from <c>content/founder.json</c> (spec 003 §4).</summary>
    public sealed class FounderDef
    {
        public int StartDietPlan { get; }
        public int StartWorkHours { get; }
        public Fixed MaxFatigue { get; }
        public Fixed RecoveryPerDay { get; }
        public Fixed GoodNutrition { get; }
        public Fixed NutritionPenalty { get; }
        public Fixed SafeFatigue { get; }
        public Fixed FatiguePenalty { get; }
        public Fixed MinHealth { get; }
        public Fixed SickChanceAtMinHealth { get; }

        /// <summary>Money per hour.</summary>
        public long DayLabourWagePerHour { get; }
        public Fixed NoWarmClothingPenalty { get; }
        public long MedicineCost { get; }
        public Fixed MedicineHealthGain { get; }

        /// <summary>Percent: 0, 25, 50, 75 or 100.</summary>
        public int StartDayLabourShare { get; }
        public bool StartEatFromStore { get; }

        public FounderDef(int startDietPlan, int startWorkHours, Fixed maxFatigue, Fixed recoveryPerDay, Fixed goodNutrition,
            Fixed nutritionPenalty, Fixed safeFatigue, Fixed fatiguePenalty, Fixed minHealth, Fixed sickChanceAtMinHealth,
            long dayLabourWagePerHour, Fixed noWarmClothingPenalty, long medicineCost, Fixed medicineHealthGain,
            int startDayLabourShare, bool startEatFromStore)
        {
            StartDietPlan = startDietPlan;
            StartWorkHours = startWorkHours;
            MaxFatigue = maxFatigue;
            RecoveryPerDay = recoveryPerDay;
            GoodNutrition = goodNutrition;
            NutritionPenalty = nutritionPenalty;
            SafeFatigue = safeFatigue;
            FatiguePenalty = fatiguePenalty;
            MinHealth = minHealth;
            SickChanceAtMinHealth = sickChanceAtMinHealth;
            DayLabourWagePerHour = dayLabourWagePerHour;
            NoWarmClothingPenalty = noWarmClothingPenalty;
            MedicineCost = medicineCost;
            MedicineHealthGain = medicineHealthGain;
            StartDayLabourShare = startDayLabourShare;
            StartEatFromStore = startEatFromStore;
        }
    }

    /// <summary>Spec 003's content: crops, the garden, farming, trees and the founder.</summary>
    /// <summary>Pests, disease and spraying (spec 003 §3.8, ADR 0014), from <c>farming.json</c>'s <c>pests</c>.</summary>
    public sealed class PestRiskDef
    {
        /// <summary>Base risk per crop index.</summary>
        public IReadOnlyList<Fixed> BaseRisk { get; }
        public Fixed MonocultureStep { get; }
        public int MonocultureMaxRepeats { get; }
        public Fixed WetStep { get; }

        /// <summary>Per weather index: does it count as wet (rain, heavy rain, storm)?</summary>
        public IReadOnlyList<bool> WetWeather { get; }
        public int SprayGood { get; }

        /// <summary>The spray dose per acre per season, in the spray good's units (millilitres).</summary>
        public Fixed SprayPerAcrePerSeason { get; }
        public Fixed SprayRiskFactor { get; }
        public Fixed SprayDamageFactor { get; }
        public Fixed OutbreakHealthMin { get; }
        public Fixed OutbreakHealthMax { get; }
        public Fixed SevereShare { get; }
        public Fixed SevereHealth { get; }

        public PestRiskDef(IReadOnlyList<Fixed> baseRisk, Fixed monocultureStep, int monocultureMaxRepeats, Fixed wetStep,
            IReadOnlyList<bool> wetWeather, int sprayGood, Fixed sprayPerAcrePerSeason, Fixed sprayRiskFactor, Fixed sprayDamageFactor,
            Fixed outbreakHealthMin, Fixed outbreakHealthMax, Fixed severeShare, Fixed severeHealth)
        {
            BaseRisk = new List<Fixed>(baseRisk).AsReadOnly();
            MonocultureStep = monocultureStep;
            MonocultureMaxRepeats = monocultureMaxRepeats;
            WetStep = wetStep;
            WetWeather = new List<bool>(wetWeather).AsReadOnly();
            SprayGood = sprayGood;
            SprayPerAcrePerSeason = sprayPerAcrePerSeason;
            SprayRiskFactor = sprayRiskFactor;
            SprayDamageFactor = sprayDamageFactor;
            OutbreakHealthMin = outbreakHealthMin;
            OutbreakHealthMax = outbreakHealthMax;
            SevereShare = severeShare;
            SevereHealth = severeHealth;
        }
    }

    public sealed class FarmDefs
    {
        public IReadOnlyList<CropDef> Crops { get; }
        public IReadOnlyList<GardenCropDef> GardenCrops { get; }
        public FarmDef Farm { get; }
        public IReadOnlyList<FertiliserDef> Fertilisers { get; }
        public IReadOnlyList<TreeDef> Trees { get; }
        public FounderDef Founder { get; }
        public IReadOnlyList<DietPlanDef> DietPlans { get; }
        public IReadOnlyList<WorkHoursDef> WorkHours { get; }
        public PestRiskDef Pests { get; }

        public FarmDefs(IReadOnlyList<CropDef> crops, IReadOnlyList<GardenCropDef> gardenCrops, FarmDef farm,
            IReadOnlyList<FertiliserDef> fertilisers, IReadOnlyList<TreeDef> trees, FounderDef founder,
            IReadOnlyList<DietPlanDef> dietPlans, IReadOnlyList<WorkHoursDef> workHours, PestRiskDef pests)
        {
            Pests = pests ?? throw new ArgumentNullException(nameof(pests));
            Crops = new List<CropDef>(crops).AsReadOnly();
            GardenCrops = new List<GardenCropDef>(gardenCrops).AsReadOnly();
            Farm = farm ?? throw new ArgumentNullException(nameof(farm));
            Fertilisers = new List<FertiliserDef>(fertilisers).AsReadOnly();
            Trees = new List<TreeDef>(trees).AsReadOnly();
            Founder = founder ?? throw new ArgumentNullException(nameof(founder));
            DietPlans = new List<DietPlanDef>(dietPlans).AsReadOnly();
            WorkHours = new List<WorkHoursDef>(workHours).AsReadOnly();
        }

        public int IndexOfCrop(string id) { for (int i = 0; i < Crops.Count; i++) if (Crops[i].Id == id) return i; return -1; }
        public int IndexOfGardenCrop(string id) { for (int i = 0; i < GardenCrops.Count; i++) if (GardenCrops[i].Id == id) return i; return -1; }
        public int IndexOfFertiliser(string id) { for (int i = 0; i < Fertilisers.Count; i++) if (Fertilisers[i].Id == id) return i; return -1; }
        public int IndexOfTree(string id) { for (int i = 0; i < Trees.Count; i++) if (Trees[i].Id == id) return i; return -1; }
        public int IndexOfDietPlan(string id) { for (int i = 0; i < DietPlans.Count; i++) if (DietPlans[i].Id == id) return i; return -1; }
        public int IndexOfWorkHours(string id) { for (int i = 0; i < WorkHours.Count; i++) if (WorkHours[i].Id == id) return i; return -1; }
    }
}
