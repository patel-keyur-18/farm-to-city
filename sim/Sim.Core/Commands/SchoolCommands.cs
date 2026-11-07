using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.People;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Builds the village's school on its plot (spec 018 §3.1, §5; ADR 0095 §5): from the Hamlet, once, from cash or an ordinary co-op
    /// loan within the bank's existing room (the school adds nothing to it: it's the district's once built). Its cost is a gift to the
    /// village (5260), outside farm profit; it opens after <c>buildHours</c>.
    /// </summary>
    public sealed class BuildSchool : TimedCommand
    {
        /// <summary>A bank loan toward it, or null to pay in cash.</summary>
        public Financing Financing { get; }

        public BuildSchool(long at, Financing financing = null) : base(at) { Financing = financing; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = SchoolRules.CanBuild(sim);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long cost = SchoolRules.Price(sim);
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
            SchoolState s = SchoolRules.State(sim);
            s.Status = SchoolStatus.Building;
            s.StartedAt = t;
            s.ReadyAt = t + SchoolRules.Def(sim).BuildHours * 3600L;
            s.Cost = cost;
            sim.State.Ledger.Post(t, sim.Period, "ledger.buildSchool", "school", new Posting(Account.GiftsToVillage, cost), new Posting(Account.Cash, -cost));
            s.JobId = sim.ScheduleJob(SchoolRules.Job, 0, s.ReadyAt).Id;
            sim.Emit(new SchoolStarted(t, cost, s.ReadyAt));
            return CommandResult.Ok;
        }
    }
}
