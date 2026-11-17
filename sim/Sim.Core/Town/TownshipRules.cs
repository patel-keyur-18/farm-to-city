using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;

namespace Sim.Core.Town
{
    /// <summary>
    /// The Township's gate (spec 023 §3.4, US-103, ADR 0116): twenty households of yours (the doctor's counts), the clinic open with its
    /// doctor, the paved link road, three open businesses each with a profitable season, and literacy at 60% of your households' people
    /// aged 7 and over. Checked at each season close and when a goal changes; a goal once met stays met (a household leaving never takes
    /// the Township back); the era unlocks once.
    /// </summary>
    public static class TownshipRules
    {
        public static GateDef Def(Simulation sim) { return sim.Config.Game?.Township?.Gate; }

        public static TownshipState State(Simulation sim) { return sim.State.Township; }

        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Township != null; }

        /// <summary>The goals' bits in the order the sheet lists them.</summary>
        public static readonly int[] Goals = { TownshipGoal.Households, TownshipGoal.Clinic, TownshipGoal.Road, TownshipGoal.Businesses, TownshipGoal.Literacy };

        /// <summary>Households of yours now.</summary>
        public static int Households(Simulation sim) { return EraRules.Households(sim); }

        /// <summary>Open businesses of yours that have had a profitable season (spec 023 §3.4): a closed one doesn't count.</summary>
        public static int Businesses(Simulation sim)
        {
            int n = 0;
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses)
                    if (b.Status == BusinessStatus.Open && b.ProfitableSeasons >= 1) n++;
            return n;
        }

        /// <summary>Your households' people aged 7 and over who can read, in whole percent (spec 018 §3.2).</summary>
        public static int Literacy(Simulation sim) { return SchoolRules.LiteracyRate(sim); }

        /// <summary>The share of those people who can read, needed: whole percent.</summary>
        public static int LiteracyNeed(Simulation sim) { return (int)(Def(sim).Literacy * Fixed.FromInt(100)).FloorToLong(); }

        /// <summary>Whether a goal holds now.</summary>
        public static bool Holds(Simulation sim, int goal)
        {
            GateDef d = Def(sim);
            switch (goal)
            {
                case TownshipGoal.Households: return Households(sim) >= d.Households;
                case TownshipGoal.Clinic: return ClinicRules.InPost(sim);
                case TownshipGoal.Road: return RoadRules.IsBuilt(sim);
                case TownshipGoal.Businesses: return Businesses(sim) >= d.Businesses;
                case TownshipGoal.Literacy:
                    SchoolRules.Literacy(sim, out int literate, out int counted);
                    return counted > 0 && Fixed.FromInt(literate) >= Fixed.FromInt(counted) * d.Literacy;
                default: return false;
            }
        }

        /// <summary>Whether a goal is met, now or for good.</summary>
        public static bool IsMet(Simulation sim, int goal) { return (State(sim).GateMet & goal) != 0 || Holds(sim, goal); }

        /// <summary>The gate holds: every goal met, now or for good.</summary>
        public static bool Met(Simulation sim)
        {
            if (!On(sim)) return false;
            foreach (int g in Goals) if (!IsMet(sim, g)) return false;
            return true;
        }

        /// <summary>Goals that hold now are met for good; each new one is announced once.</summary>
        internal static void Latch(Simulation sim)
        {
            if (!On(sim)) return;
            TownshipState t = State(sim);
            foreach (int g in Goals)
            {
                if ((t.GateMet & g) != 0 || !Holds(sim, g)) continue;
                t.GateMet |= g;
                sim.Emit(new GateGoalMet(sim.State.Clock.SimTime, g));
            }
        }
    }
}
