using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Town;

namespace Sim.Core.Commands
{
    /// <summary>
    /// The petition for the paved link road (spec 023 §3.3, US-146): from the Village at <c>petitionHouseholds</c> households of yours, from cash
    /// or an ordinary co-op loan within the bank's room. The share is a gift to the village (5260), outside farm profit; the district builds it
    /// in <c>days</c> and the road is never yours.
    /// </summary>
    public sealed class PetitionRoad : TimedCommand
    {
        /// <summary>A bank loan toward it, or null to pay in cash.</summary>
        public Financing Financing { get; }

        public PetitionRoad(long at, Financing financing = null) : base(at) { Financing = financing; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = RoadRules.CanPetition(sim);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long cost = RoadRules.Price(sim);
            int lender = -1;
            long loan = 0;
            if (Financing != null)
            {
                if (Financing.Amount > cost) return CommandResult.Reject(RejectReason.InvalidAmount);
                r = CreditRules.Check(sim, Financing.LenderId, Financing.Amount, Financing.TermSeasons, 0, out lender);
                if (r != RejectReason.None) return CommandResult.Reject(r);
                loan = Financing.Amount;
            }
            if (!sim.State.Ledger.CanAfford(cost - loan)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            if (Financing != null) CreditSystem.Create(sim, lender, loan, Financing.TermSeasons);

            long t = sim.State.Clock.SimTime;
            RoadState s = RoadRules.State(sim);
            s.Status = RoadStatus.Building;
            s.StartedAt = t;
            s.ReadyAt = t + RoadRules.Def(sim).Days * ClockDef.SecondsPerDay;
            s.Share = cost;
            sim.State.Ledger.Post(t, sim.Period, "ledger.petitionRoad", "road", new Posting(Account.GiftsToVillage, cost), new Posting(Account.Cash, -cost));
            s.JobId = sim.ScheduleJob(RoadRules.Job, 0, s.ReadyAt).Id;
            sim.Emit(new RoadPetitioned(t, cost, s.ReadyAt));
            return CommandResult.Ok;
        }
    }
}
