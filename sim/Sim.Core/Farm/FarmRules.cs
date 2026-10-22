using System.Numerics;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

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

        /// <summary>
        /// One sowing on the tile as the sow sheet shows it (spec 008 §3.7, ADR 0048 §1): the yield formula with what is known now
        /// (soil, water at today's moisture, fertiliser; care, seed, weather and health at 1), the harvest at the trader's floor
        /// today, and its seed and fertiliser at today's input prices.
        /// </summary>
        public static SowingEstimate Estimate(Simulation sim, Tile tile, CropDef crop, FertiliserDef fert)
        {
            FarmDef farm = sim.Config.Game.Farm.Farm;
            long harvest = Yield(farm, crop.SeasonYieldPerAcre * CycleShare(sim.Clock, crop), LandRules.SoilFactor(sim, tile), WaterFactor(farm, crop, tile.Moisture),
                Fixed.One, Fixed.One, Fixed.One, fert.YieldFactor, Fixed.One).Grams;
            long seedGrams = CycleGrams(sim.Clock, crop, crop.SeedPerAcrePerSeason);
            long fertGrams = fert.Good < 0 ? 0 : CycleGrams(sim.Clock, crop, fert.KgPerAcrePerSeason);
            return new SowingEstimate(harvest, MarketRules.Value(sim, crop.OutputGood, harvest, MarketRules.Floor(sim, crop.OutputGood)),
                MarketRules.Value(sim, crop.SeedGood, seedGrams, MarketRules.InputPrice(sim, crop.SeedGood)),
                fert.Good < 0 ? 0 : MarketRules.Value(sim, fert.Good, fertGrams, MarketRules.InputPrice(sim, fert.Good)), fertGrams);
        }

        /// <summary>What sowing one cycle would buy now: certified seed not in the store, and fertiliser not in the store (spec 003 §3.2).</summary>
        public static long SowingCost(Simulation sim, CropDef crop, SeedSource seed, FertiliserDef fert)
        {
            long seedCost = seed == SeedSource.Certified ? Store.ShortfallCost(sim, crop.SeedGood, CycleGrams(sim.Clock, crop, crop.SeedPerAcrePerSeason)) : 0;
            return seedCost + (fert.Good < 0 ? 0 : Store.ShortfallCost(sim, fert.Good, CycleGrams(sim.Clock, crop, fert.KgPerAcrePerSeason)));
        }

        /// <summary>
        /// The hand's pick (ADR 0048 §1, ADR 0050): of the crops sowable this season whose seed and fertiliser he can pay for now,
        /// the one with the highest expected profit per season on the tile (an open order counted at its price, spec 011), profit ÷ cycleShare, compared by cross-multiplying
        /// growth times. Ties go to the earlier crop; −1 if none is in season and affordable.
        /// </summary>
        public static int BestCrop(Simulation sim, Tile tile, FertiliserDef fert)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            int season = (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            int best = -1;
            long bestProfit = 0, bestGrowth = 1;
            for (int i = 0; i < farm.Crops.Count; i++)
            {
                CropDef c = farm.Crops[i];
                if (!c.SowSeasons[season] || !sim.State.Ledger.CanAfford(SowingCost(sim, c, SeedSource.Certified, fert))) continue;
                SowingEstimate e = Estimate(sim, tile, c, fert);
                // An open supermarket order he can fill counts at its price (spec 011 §3.3, AC-11).
                long profit = Supermarket.HarvestValue(sim, c.OutputGood, e.HarvestGrams, sim.State.Clock.SimTime + c.GrowthSeconds) - e.Spend;
                if (best >= 0 && (BigInteger)profit * bestGrowth <= (BigInteger)bestProfit * c.GrowthSeconds) continue;
                best = i;
                bestProfit = profit;
                bestGrowth = c.GrowthSeconds;
            }
            return best;
        }
    }

    /// <summary>One sowing's expected harvest (grams), its value at the trader's floor and its inputs at today's prices.</summary>
    public readonly struct SowingEstimate
    {
        public long HarvestGrams { get; }
        public long Get { get; }
        public long SeedValue { get; }
        public long FertiliserValue { get; }
        public long FertiliserGrams { get; }
        public long Spend { get { return SeedValue + FertiliserValue; } }
        public long Profit { get { return Get - Spend; } }

        public SowingEstimate(long harvestGrams, long get, long seedValue, long fertiliserValue, long fertiliserGrams)
        {
            HarvestGrams = harvestGrams;
            Get = get;
            SeedValue = seedValue;
            FertiliserValue = fertiliserValue;
            FertiliserGrams = fertiliserGrams;
        }
    }
}
