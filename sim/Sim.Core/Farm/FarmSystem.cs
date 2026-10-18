using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Farm
{
    /// <summary>
    /// Crops, the kitchen garden and fruit trees (spec 003). Growing crops accrue their factors between accrual
    /// points (§3.7): every hourly and daily pass and every sow, ready, clear and plan change. Between two points
    /// every rate is constant, so the result never depends on how time is advanced.
    /// </summary>
    internal sealed class FarmSystem : ISimSystem
    {
        public const string CropJob = "crop.grow";
        public const string GardenJob = "garden.grow";

        public SystemOrder Order { get { return SystemOrder.Agriculture; } }

        public static void Initialise(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            FarmDefs farm = defs.Farm;
            var state = new FarmState
            {
                LastAccrual = sim.State.Clock.SimTime,
                SavedSeedAge = new Fixed[farm.Crops.Count],
                HasSavedSeed = new bool[farm.Crops.Count],
            };
            for (int c = 0; c < farm.Crops.Count; c++)
            {
                Fixed? age = defs.Start.SavedSeedAge[c];
                state.HasSavedSeed[c] = age.HasValue;
                state.SavedSeedAge[c] = age ?? Fixed.Zero;
            }
            long beds = 0;
            foreach (Building b in sim.State.Land.Buildings) beds += defs.Buildings[b.Def].GardenBeds;
            for (long i = 1; i <= beds; i++) state.Beds.Add(new GardenBed { Id = i, RemainingGrams = farm.Farm.GardenSeasonOutputGrams });
            state.Founder = FounderRules.Start(sim);
            sim.State.Farm = state;
            Refresh(sim);
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            switch (pass)
            {
                case ItemClass.Hourly:
                case ItemClass.Daily:
                    // Hourly: after spec 002's moisture update and the new hour's weather. Daily: the founder's new day
                    // follows in the Labour system, which refreshes the rates again.
                    Accrue(sim);
                    Refresh(sim);
                    break;
                case ItemClass.Seasonal:
                    foreach (GardenBed b in sim.State.Farm.Beds) b.RemainingGrams = sim.Config.Game.Farm.Farm.GardenSeasonOutputGrams;
                    break;
            }
        }

        // ---- accrual (§3.7) ----

        /// <summary>Adds rate × (now − lastAccrual) to every growing crop's sums and to the founder's accruals.</summary>
        internal static void Accrue(Simulation sim)
        {
            FarmState farm = sim.State.Farm;
            long t = sim.State.Clock.SimTime;
            long dt = t - farm.LastAccrual;
            if (dt <= 0) return;
            foreach (Crop c in farm.Crops)
            {
                if (c.State != CropState.Growing) continue;
                c.WaterSum += c.WaterRate.MulRatio(dt, 1);
                c.CareSum += farm.CareRatio.MulRatio(dt, 1);
                c.WeatherSum += c.WeatherRate.MulRatio(dt, 1);
            }
            FounderRules.Accrue(sim, dt);
            farm.LastAccrual = t;
        }

        /// <summary>Refreshes the rates in force from now: each growing crop's water and weather, and the shared care ratio.</summary>
        internal static void Refresh(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            FarmState farm = sim.State.Farm;
            int weather = sim.State.Weather.WeatherOf(Calendar.SpellAt(sim.State.Clock.SimTime, sim.Clock));
            Fixed demand = Fixed.Zero;
            foreach (Crop c in farm.Crops)
            {
                if (c.State != CropState.Growing) continue;
                CropDef def = defs.Farm.Crops[c.Def];
                Tile tile = sim.State.Land.Tiles[c.TileId];
                c.WaterRate = FarmRules.WaterFactor(defs.Farm.Farm, def, tile.Moisture);
                c.WeatherRate = defs.Farm.Farm.WeatherYieldFactor[weather];
                demand += def.LabourPerAcrePerDay + tile.WellLift.MulRatio(24, 1) * defs.Farm.Farm.BucketHoursPerMoisturePoint;
            }
            Fixed cap = defs.Farm.Farm.CareCap;
            if (demand == Fixed.Zero) { farm.CareRatio = cap; return; }
            Fixed ratio = FounderRules.FieldHours(sim) / demand;
            farm.CareRatio = ratio > cap ? cap : ratio;
        }

        /// <summary>Labour needed today by the growing crops, hours a day (for Why?).</summary>
        public static Fixed LabourDemand(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            Fixed demand = Fixed.Zero;
            foreach (Crop c in sim.State.Farm.Crops)
                if (c.State == CropState.Growing)
                    demand += defs.Farm.Crops[c.Def].LabourPerAcrePerDay
                        + sim.State.Land.Tiles[c.TileId].WellLift.MulRatio(24, 1) * defs.Farm.Farm.BucketHoursPerMoisturePoint;
            return demand;
        }

        // ---- jobs ----

        /// <summary>A crop's growth ends: the factors close, the yield is frozen, fertility changes, and it waits Ready (§3.3).</summary>
        internal sealed class CropGrowth : IJobHandler
        {
            public string JobType { get { return CropJob; } }
            public SystemOrder System { get { return SystemOrder.Agriculture; } }

            public void OnDue(Simulation sim, Job job)
            {
                Accrue(sim);
                FarmState farm = sim.State.Farm;
                Crop c = farm.Crops.Find(x => x.Id == job.TargetId);
                GameDefs defs = sim.Config.Game;
                CropDef def = defs.Farm.Crops[c.Def];
                FertiliserDef fert = defs.Farm.Fertilisers[c.Fertiliser];
                long g = def.GrowthSeconds;
                Fixed share = FarmRules.CycleShare(sim.Clock, def);

                c.Breakdown = FarmRules.Yield(defs.Farm.Farm, def.SeasonYieldPerAcre * share, c.SoilFactor,
                    c.WaterSum.MulRatio(1, g), c.CareSum.MulRatio(1, g), FarmRules.SeedFactor(def, c.Seed, c.SeedAge),
                    c.WeatherSum.MulRatio(1, g), fert.YieldFactor, Fixed.One); // CropHealth: pests join with US-046
                c.State = CropState.Ready;

                // Fertility for the cycle's share (§3.10).
                Fixed use = def.FertilityPerSeason * share;
                if (use != Fixed.Zero)
                    LandSystem.AdjustFertility(sim, c.TileId, -use, use > Fixed.Zero ? FertilityReason.CropUse : FertilityReason.Lentils);
                Fixed f = fert.FertilityPerSeason * share;
                if (f != Fixed.Zero)
                    LandSystem.AdjustFertility(sim, c.TileId, f, f < Fixed.Zero ? FertilityReason.Fertiliser : FertilityReason.Compost);

                sim.CompleteJob(job);
                c.JobId = 0;
                Refresh(sim);
                sim.Emit(new CropReady(sim.State.Clock.SimTime, c.TileId, def.Id, c.Breakdown.Grams));
            }
        }

        /// <summary>A garden bed's crop is ready; it waits like a field crop (§3.13).</summary>
        internal sealed class GardenGrowth : IJobHandler
        {
            public string JobType { get { return GardenJob; } }
            public SystemOrder System { get { return SystemOrder.Agriculture; } }

            public void OnDue(Simulation sim, Job job)
            {
                GardenBed bed = sim.State.Farm.Beds.Find(b => b.Id == job.TargetId);
                bed.State = BedState.Ready;
                bed.JobId = 0;
                sim.CompleteJob(job);
            }
        }
    }
}
