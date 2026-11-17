using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Progress;
using Sim.Core.Scheduling;

namespace Sim.Core.Town
{
    /// <summary>
    /// The paved link road (spec 023 §3.3, US-146, ADR 0116): from the Village, at <c>petitionHouseholds</c> households of yours, the player
    /// gives the district a community share and the district builds a road to the highway in <c>days</c>. The road is never his; it
    /// brings a better road tier, so every trader price is 0.50 a kg better at the farm gate.
    /// </summary>
    public static class RoadRules
    {
        public const string Job = "road.build";

        public static RoadDef Def(Simulation sim) { return sim.Config.Game?.Township?.Road; }

        public static RoadState State(Simulation sim) { return sim.State.Township?.Road; }

        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Township != null; }

        public static bool IsBuilt(Simulation sim) { return On(sim) && State(sim).Status == RoadStatus.Built; }

        /// <summary>The share at today's prices (a gift, indexed at the press).</summary>
        public static long Price(Simulation sim) { return MarketRules.Indexed(sim, Def(sim).Share); }

        /// <summary>Households of yours now (spec 023 §3.3): the hands' and the families', as the Village's gate counts them.</summary>
        public static int Households(Simulation sim) { return EraRules.Households(sim); }

        /// <summary>The Village era, which opens the petition (the gate's other goals are the Township's).</summary>
        public static bool Unlocked(Simulation sim)
        {
            if (sim.State.Era == null) return true;
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++)
                if (eras[i].Id == "village") return sim.State.Era.Current >= i;
            return false;
        }

        /// <summary>Why the petition can't be made now, or None (before the loan's and the cash's checks).</summary>
        public static RejectReason CanPetition(Simulation sim)
        {
            if (!On(sim)) return RejectReason.InvalidArgument;
            if (!Unlocked(sim)) return RejectReason.EraLocked;
            if (State(sim).Status != RoadStatus.None) return RejectReason.AlreadyBuilt;
            return Households(sim) < Def(sim).PetitionHouseholds ? RejectReason.TooFewHouseholds : RejectReason.None;
        }

        /// <summary>Coins a kg the trader's transport takes now: the region's tier, or the paved road's once it is built.</summary>
        public static Fixed TransportPerKg(Simulation sim)
        {
            RegionDef region = sim.Config.Game.Market.Region;
            return IsBuilt(sim) ? region.CostOfTier(Def(sim).Tier).Value : region.TransportCostPerKg;
        }

        /// <summary>What the road saves a kg: the old tier less the new (0 until it is built).</summary>
        public static Fixed SavingPerKg(Simulation sim)
        {
            RegionDef region = sim.Config.Game.Market.Region;
            return IsBuilt(sim) ? region.TransportCostPerKg - TransportPerKg(sim) : Fixed.Zero;
        }

        /// <summary>The road is finished: the tier changes for every price (spec 023 §3.3).</summary>
        internal sealed class Build : IJobHandler
        {
            public string JobType { get { return Job; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Job job)
            {
                RoadState s = State(sim);
                s.Status = RoadStatus.Built;
                s.JobId = 0;
                sim.CompleteJob(job);
                sim.Emit(new RoadBuilt(sim.State.Clock.SimTime));
            }
        }
    }
}
