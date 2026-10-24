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
        public const string TreeJob = "tree.mature";
        public const string PestJob = "crop.pest";

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
                TileRunCrop = new int[sim.State.Land.Tiles.Count],
                TileRun = new int[sim.State.Land.Tiles.Count],
            };
            for (int i = 0; i < state.TileRunCrop.Length; i++) state.TileRunCrop[i] = -1;
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
                    if (pass == ItemClass.Daily) SpoilWaiting(sim);
                    break;
                case ItemClass.Seasonal:
                    foreach (GardenBed b in sim.State.Farm.Beds) b.RemainingGrams = sim.Config.Game.Farm.Farm.GardenSeasonOutputGrams;
                    foreach (Tree t in sim.State.Farm.Trees) Ripen(sim, t);
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
                if (c.Wet) c.WetSeconds += dt;
            }
            FounderRules.Accrue(sim, dt);
            Livestock.LivestockSystem.AccrueCare(sim, dt);
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
                c.Wet = defs.Farm.Pests.WetWeather[weather];
                demand += def.LabourPerAcrePerDay + tile.WellLift.MulRatio(24, 1) * defs.Farm.Farm.BucketHoursPerMoisturePoint;
            }
            demand += Livestock.LivestockRules.LabourDemand(sim); // every animal, dry or in milk (spec 012 §3.9)
            Fixed cap = defs.Farm.Farm.CareCap;
            if (demand == Fixed.Zero) { farm.CareRatio = cap; return; }
            Fixed ratio = People.PeopleRules.CareSupply(sim) / demand;
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
            return demand + Livestock.LivestockRules.LabourDemand(sim);
        }

        /// <summary>
        /// Ready crops and ripe batches wait without loss for the grace, then lose their good's spoilage at each daily
        /// pass, like the store (ADR 0034), crops then trees in id order.
        /// </summary>
        private static void SpoilWaiting(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            long t = sim.State.Clock.SimTime, grace = defs.Farm.Farm.ReadyGraceSeconds;
            foreach (Crop c in sim.State.Farm.Crops)
            {
                if (c.State != CropState.Ready || t - c.ReadyAt <= grace) continue;
                int good = defs.Farm.Crops[c.Def].OutputGood;
                long lost = Rot(defs.Market.Goods[good].SpoilagePerDay, c.HarvestGrams);
                if (lost <= 0) continue;
                c.SpoiledGrams += lost;
                sim.Emit(new GoodsSpoiled(t, defs.Market.Goods[good].Id, lost, SpoilSource.Field, c.TileId));
            }
            foreach (Tree tree in sim.State.Farm.Trees)
            {
                if (tree.RipeGrams == 0 || t - tree.RipeAt <= grace) continue;
                int good = defs.Farm.Trees[tree.Def].FruitGood;
                long lost = Rot(defs.Market.Goods[good].SpoilagePerDay, tree.RipeGrams);
                if (lost <= 0) continue;
                tree.RipeGrams -= lost;
                sim.Emit(new GoodsSpoiled(t, defs.Market.Goods[good].Id, lost, SpoilSource.Tree, tree.SlotId));
            }
        }

        /// <summary>round(grams × rate); when under 1 kg would remain, the rest is lost too (nothing worth picking).</summary>
        private static long Rot(Fixed rate, long grams)
        {
            long lost = rate.MulRatio(grams, 1).RoundToLong();
            return lost > 0 && grams - lost < 1000 ? grams : lost;
        }

        // ---- trees (§3.12) ----

        /// <summary>A mature tree with no fruit waiting gets one batch if the current season is one of its fruiting seasons.</summary>
        internal static void Ripen(Simulation sim, Tree t)
        {
            TreeDef def = sim.Config.Game.Farm.Trees[t.Def];
            long now = sim.State.Clock.SimTime;
            if (t.State != TreeState.Mature) return;
            if (!def.FruitingSeasons[(int)Calendar.SeasonAt(now, sim.Clock)]) return;
            if (t.RipeGrams > 0)
            {
                // A batch still waiting within its grace blocks the next; one past it has been spoiling and is lost (ADR 0034).
                if (now - t.RipeAt <= sim.Config.Game.Farm.Farm.ReadyGraceSeconds) return;
                sim.Emit(new GoodsSpoiled(now, sim.Config.Game.Market.Goods[def.FruitGood].Id, t.RipeGrams, SpoilSource.Tree, t.SlotId));
                t.RipeGrams = 0;
            }
            t.RipeGrams = def.BatchGrams;
            t.RipeAt = sim.State.Clock.SimTime;
            sim.Emit(new FruitRipe(sim.State.Clock.SimTime, t.SlotId, def.Id, t.RipeGrams));
        }

        /// <summary>Removes a tree: its book value goes to 5600 Loss (spec 005 §3.3) and the slot is emptied.</summary>
        internal static void Dispose(Simulation sim, Tree t)
        {
            if (t.JobId != 0) sim.CompleteJob(sim.State.Jobs.Find(t.JobId));
            if (t.Cost > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.treeRemoved", "tree:" + t.Id,
                    new Finance.Posting(Finance.Account.LossOnAssetSales, t.Cost), new Finance.Posting(Finance.Account.Trees, -t.Cost));
            LandSystem.SetTree(sim, t.SlotId, 0);
            sim.State.Farm.Trees.Remove(t);
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
                if (def.Fodder)
                {
                    Livestock.FodderRules.End(sim, c, job);
                    return;
                }
                FertiliserDef fert = defs.Farm.Fertilisers[c.Fertiliser];
                long g = def.GrowthSeconds;
                Fixed share = FarmRules.CycleShare(sim.Clock, def);

                c.Breakdown = FarmRules.Yield(defs.Farm.Farm, def.SeasonYieldPerAcre * share, c.SoilFactor,
                    c.WaterSum.MulRatio(1, g), c.CareSum.MulRatio(1, g), FarmRules.SeedFactor(def, c.Seed, c.SeedAge),
                    c.WeatherSum.MulRatio(1, g), fert.YieldFactor, PestRules.FinalHealth(defs.Farm.Pests, c));
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

        /// <summary>
        /// The pest check at half-growth (spec 003 §3.8): the risk from the crop, its repeats, the wet share so far and a
        /// preventive spray; a seeded roll; an outbreak warns at once, and its health applies when the crop is ready.
        /// </summary>
        internal sealed class PestCheck : IJobHandler
        {
            public string JobType { get { return PestJob; } }
            public SystemOrder System { get { return SystemOrder.Agriculture; } }

            public void OnDue(Simulation sim, Job job)
            {
                Accrue(sim);
                Crop c = sim.State.Farm.Crops.Find(x => x.Id == job.TargetId);
                PestRiskDef p = sim.Config.Game.Farm.Pests;
                long elapsed = sim.State.Clock.SimTime - c.SownAt;
                Fixed wetShare = elapsed > 0 ? Fixed.FromRatio(c.WetSeconds, elapsed) : Fixed.Zero;
                Fixed risk = PestRules.Risk(p, c.Def, c.Repeats, wetShare, c.Spray == SprayState.BeforeCheck);
                c.RolledHealth = PestRules.Roll(p, sim.State.Seed, c.Id, risk, sim.State.Clock.IsAway, out Outbreak o);
                c.Outbreak = o;
                c.PestChecked = true;
                c.PestJobId = 0;
                sim.CompleteJob(job);
                if (o != Outbreak.None) sim.Emit(new PestWarning(sim.State.Clock.SimTime, c.TileId, o, c.ReadyAt));
            }
        }

        /// <summary>A sapling matures: it shades its field-edge tile from now, and fruits if this is a fruiting season (§3.12).</summary>
        internal sealed class TreeMaturity : IJobHandler
        {
            public string JobType { get { return TreeJob; } }
            public SystemOrder System { get { return SystemOrder.Agriculture; } }

            public void OnDue(Simulation sim, Job job)
            {
                Tree t = sim.State.Farm.Trees.Find(x => x.Id == job.TargetId);
                t.State = TreeState.Mature;
                t.JobId = 0;
                sim.CompleteJob(job);
                LandSystem.SetTreeMature(sim, t.SlotId, sim.Config.Game.Farm.Trees[t.Def].Shade);
                sim.Emit(new TreeMatured(sim.State.Clock.SimTime, t.SlotId, sim.Config.Game.Farm.Trees[t.Def].Id));
                Ripen(sim, t);
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
                sim.Emit(new BedReady(sim.State.Clock.SimTime, bed.Id, sim.Config.Game.Farm.GardenCrops[bed.Crop].Id));
            }
        }
    }
}
