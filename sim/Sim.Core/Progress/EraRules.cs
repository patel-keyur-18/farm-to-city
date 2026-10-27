using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Maths;

namespace Sim.Core.Progress
{
    /// <summary>
    /// The era check (spec 009 §3.6, ADR 0030, 0032), checked after every field harvest and at every season close, after the
    /// books. The next era unlocks once, when all its conditions hold: field harvests on enough different owned acres in one
    /// season with their mean Care at least the bound, enough hands' huts built, and enough seasons with farm profit above 0.
    /// </summary>
    public static class EraRules
    {
        /// <summary>A field harvest (spec 003 §3.3): counts its acre and Care towards this season, then checks.</summary>
        internal static void OnHarvest(Simulation sim, int tileId, Fixed care)
        {
            EraState e = sim.State.Era;
            if (e == null) return;
            long period = sim.Period;
            if (e.SeasonPeriod != period)
            {
                e.SeasonPeriod = period;
                e.SeasonAcres.Clear();
                e.SeasonCareSum = Fixed.Zero;
                e.SeasonHarvests = 0;
            }
            int at = e.SeasonAcres.BinarySearch(tileId);
            if (at < 0) e.SeasonAcres.Insert(~at, tileId);
            e.SeasonCareSum += care;
            e.SeasonHarvests++;
            Changed(sim, period);
        }

        /// <summary>A season close (spec 005 §3.4): a profitable season counts, then the closing season is checked.</summary>
        internal static void OnSeasonClosed(Simulation sim, long farmProfit, long closing)
        {
            EraState e = sim.State.Era;
            if (e == null) return;
            if (farmProfit > 0)
            {
                e.ProfitableSeasons++;
                Changed(sim, closing);
            }
            else Check(sim, closing);
        }

        private static void Changed(Simulation sim, long period)
        {
            sim.Emit(new EraProgress(sim.State.Clock.SimTime, Acres(sim, period), MeanCare(sim, period), Huts(sim), sim.State.Era.ProfitableSeasons));
            Check(sim, period);
        }

        private static void Check(Simulation sim, long period)
        {
            EraState e = sim.State.Era;
            var eras = sim.Config.Game.Onboarding.Eras;
            if (e.Current + 1 >= eras.Count) return;
            EraDef next = eras[e.Current + 1];
            if (next.Manual || !Met(sim, next, period)) return;
            Unlock(sim);
        }

        /// <summary>Moves to the next era (once its gate is met, or by the developer panel); false at the last era or without eras.</summary>
        internal static bool Unlock(Simulation sim)
        {
            EraState e = sim.State.Era;
            var eras = sim.Config.Game.Onboarding?.Eras;
            if (e == null || eras == null || e.Current + 1 >= eras.Count) return false;
            e.Current++;
            e.UnlockedAt = sim.State.Clock.SimTime;
            sim.Emit(new EraUnlocked(sim.State.Clock.SimTime, eras[e.Current].Id));
            return true;
        }

        /// <summary>True when every condition of <paramref name="era"/> holds for season <paramref name="period"/>.</summary>
        public static bool Met(Simulation sim, EraDef era, long period)
        {
            EraState e = sim.State.Era;
            if (era.AcresHarvestedInSeason > 0)
            {
                if (Acres(sim, period) < era.AcresHarvestedInSeason) return false;
                if (e.SeasonCareSum < era.MeanCareAtLeast.MulRatio(e.SeasonHarvests, 1)) return false;
            }
            return Huts(sim) >= era.HandHutsAtLeast && e.ProfitableSeasons >= era.ProfitableSeasonsAtLeast;
        }

        /// <summary>The different acres, still owned, with a field harvest in season <paramref name="period"/>.</summary>
        public static int Acres(Simulation sim, long period)
        {
            EraState e = sim.State.Era;
            if (e.SeasonPeriod != period) return 0;
            LandState land = sim.State.Land;
            int n = 0;
            foreach (int t in e.SeasonAcres) if (land.Parcels[land.Tiles[t].ParcelId].Owned) n++;
            return n;
        }

        /// <summary>The mean Care of season <paramref name="period"/>'s field harvests, or zero before the first.</summary>
        public static Fixed MeanCare(Simulation sim, long period)
        {
            EraState e = sim.State.Era;
            return e.SeasonPeriod != period || e.SeasonHarvests == 0 ? Fixed.Zero : e.SeasonCareSum.MulRatio(1, e.SeasonHarvests);
        }

        /// <summary>Hands' huts built (one under construction doesn't count).</summary>
        public static int Huts(Simulation sim)
        {
            if (sim.Config.Game.People == null) return 0;
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == sim.Config.Game.People.HandHut) n++;
            return n;
        }
    }
}
