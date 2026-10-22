using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Livestock
{
    /// <summary>Green fodder fields (spec 012 §3.8b): cut every morning, the soil changed for the time they stand, ended after their last cut season.</summary>
    public static class FodderRules
    {
        /// <summary>
        /// The morning's cut from every fodder field in tile order (grams): since the last cut, from its first cut on, in a cut season,
        /// seasonYieldPerAcre × time ÷ S × Soil × Water × Care × Weather × Fertiliser (the day's means), capped like any yield.
        /// </summary>
        internal static long Cut(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            FarmState farm = sim.State.Farm;
            long t = sim.State.Clock.SimTime, total = 0;
            FarmSystem.Accrue(sim);
            var crops = farm.Crops.FindAll(c => c.State == CropState.Growing && defs.Farm.Crops[c.Def].Fodder);
            crops.Sort((a, b) => a.TileId.CompareTo(b.TileId));
            foreach (Crop c in crops) total += CutField(sim, c, t);
            return total;
        }

        /// <summary>One field's cut now (0 before its first cut or outside its cut seasons); its sums and soil change restart from now.</summary>
        internal static long CutField(Simulation sim, Crop c, long t)
        {
            GameDefs defs = sim.Config.Game;
            CropDef def = defs.Farm.Crops[c.Def];
            long span = t - c.LastCut;
            if (span <= 0) return 0;
            long from = System.Math.Max(c.LastCut, c.SownAt + def.GrowthSeconds);
            long grams = 0;
            if (t > from && def.CutSeasons[(int)Calendar.SeasonAt(t - 1, sim.Clock)])
            {
                Fixed baseKg = def.SeasonYieldPerAcre.MulRatio(t - from, sim.Clock.SecondsPerSeason);
                grams = FarmRules.Yield(defs.Farm.Farm, baseKg, c.SoilFactor, c.WaterSum.MulRatio(1, span), c.CareSum.MulRatio(1, span), Fixed.One,
                    c.WeatherSum.MulRatio(1, span), defs.Farm.Fertilisers[c.Fertiliser].YieldFactor, Fixed.One).Grams;
            }
            Settle(sim, c);
            return grams;
        }

        /// <summary>The soil change for the time since the last cut (crop and fertiliser, per season, pro rata), then the sums restart.</summary>
        internal static void Settle(Simulation sim, Crop c)
        {
            GameDefs defs = sim.Config.Game;
            long t = sim.State.Clock.SimTime, span = t - c.LastCut;
            if (span <= 0) return;
            CropDef def = defs.Farm.Crops[c.Def];
            FertiliserDef fert = defs.Farm.Fertilisers[c.Fertiliser];
            Fixed use = def.FertilityPerSeason.MulRatio(span, sim.Clock.SecondsPerSeason);
            if (use != Fixed.Zero) LandSystem.AdjustFertility(sim, c.TileId, -use, use > Fixed.Zero ? FertilityReason.CropUse : FertilityReason.Lentils);
            Fixed f = fert.FertilityPerSeason.MulRatio(span, sim.Clock.SecondsPerSeason);
            if (f != Fixed.Zero) LandSystem.AdjustFertility(sim, c.TileId, f, f < Fixed.Zero ? FertilityReason.Fertiliser : FertilityReason.Compost);
            c.LastCut = t;
            c.WaterSum = Fixed.Zero;
            c.CareSum = Fixed.Zero;
            c.WeatherSum = Fixed.Zero;
        }

        /// <summary>The stand is over (its last cut season has ended): the soil change, the field cleared (§3.8b). Emits FodderEnded.</summary>
        internal static void End(Simulation sim, Crop c, Job job)
        {
            Settle(sim, c);
            sim.CompleteJob(job);
            sim.State.Farm.Crops.Remove(c);
            LandSystem.SetOccupied(sim, c.TileId, false);
            FarmSystem.Refresh(sim);
            sim.Emit(new FodderEnded(sim.State.Clock.SimTime, c.TileId, sim.Config.Game.Farm.Crops[c.Def].Id));
        }
    }
}
