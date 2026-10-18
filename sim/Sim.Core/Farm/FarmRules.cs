using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Farm
{
    /// <summary>Spec 003's pure formulas: per-season realism (§3.1), the water factor (§3.5), seed (§3.9) and yield (§3.4).</summary>
    public static class FarmRules
    {
        /// <summary>Labour needed today by the growing crops, hours a day (spec 003 §3.6; the care ratio's Why?).</summary>
        public static Fixed LabourDemand(Simulation sim) { return FarmSystem.LabourDemand(sim); }

        private static readonly Fixed Hundred = Fixed.FromInt(100);

        /// <summary>cycleShare = growthSeconds ÷ SecondsPerSeason.</summary>
        public static Fixed CycleShare(ClockDef clock, CropDef crop) { return Fixed.FromRatio(crop.GrowthSeconds, clock.SecondsPerSeason); }

        /// <summary>A per-acre-per-season kg amount for one cycle, in grams: round(kg × cycleShare × 1000).</summary>
        public static long CycleGrams(ClockDef clock, CropDef crop, Fixed kgPerSeason)
        {
            return (kgPerSeason * CycleShare(clock, crop)).MulRatio(1000, 1).RoundToLong();
        }

        /// <summary>The piecewise water factor of spec 003 §3.5.</summary>
        public static Fixed WaterFactor(FarmDef farm, CropDef crop, Fixed m)
        {
            if (m < crop.MoistureMin) return farm.WaterDryFloor + (Fixed.One - farm.WaterDryFloor) * m / crop.MoistureMin;
            if (m <= crop.MoistureMax) return Fixed.One;
            return Fixed.One - (Fixed.One - farm.WaterWetFloor) * (m - crop.MoistureMax) / (Hundred - crop.MoistureMax);
        }

        /// <summary>Certified 1; saved seed of age a: max(floor, 1 − loss × a).</summary>
        public static Fixed SeedFactor(CropDef crop, SeedSource source, Fixed age)
        {
            if (source == SeedSource.Certified) return Fixed.One;
            Fixed f = Fixed.One - crop.SavedSeedLossPerSeason * age;
            return f < crop.SavedSeedFloor ? crop.SavedSeedFloor : f;
        }

        /// <summary>
        /// Yield = Base × Soil × Water × Care × Seed × Weather × Tools × Fertiliser × CropHealth, left to right with
        /// each step rounded, capped at yieldCap × Base, then converted to grams (spec 003 §3.4).
        /// </summary>
        public static YieldBreakdown Yield(FarmDef farm, Fixed baseKg, Fixed soil, Fixed water, Fixed care, Fixed seed, Fixed weather,
            Fixed fertiliser, Fixed cropHealth)
        {
            Fixed product = baseKg * soil * water * care * seed * weather * farm.ToolsFactor * fertiliser * cropHealth;
            Fixed cap = farm.YieldCap * baseKg;
            bool capped = product > cap;
            Fixed kg = capped ? cap : product;
            return new YieldBreakdown
            {
                Base = baseKg, Soil = soil, Water = water, Care = care, Seed = seed, Weather = weather, Tools = farm.ToolsFactor,
                Fertiliser = fertiliser, CropHealth = cropHealth, Uncapped = product, Capped = capped,
                Grams = kg.MulRatio(1000, 1).RoundToLong(),
            };
        }
    }
}
