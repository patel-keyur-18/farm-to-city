using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Town;

namespace Sim.Core.Commands
{
    /// <summary>
    /// The community share for the village clinic (spec 023 §3.1, US-148): from the Village, once, from cash or an ordinary co-op loan within the
    /// bank's room. The share is a gift to the village (5260), outside farm profit; the district builds the clinic in <c>buildHours</c>.
    /// </summary>
    public sealed class BuildClinic : TimedCommand
    {
        /// <summary>A bank loan toward it, or null to pay in cash.</summary>
        public Financing Financing { get; }

        public BuildClinic(long at, Financing financing = null) : base(at) { Financing = financing; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = ClinicRules.CanBuildClinic(sim);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long cost = ClinicRules.Price(sim);
            r = Pay.Take(sim, Financing, cost, out _);
            if (r != RejectReason.None) return CommandResult.Reject(r);

            long t = sim.State.Clock.SimTime;
            ClinicState s = ClinicRules.State(sim);
            s.Status = ClinicStatus.Building;
            s.StartedAt = t;
            s.ReadyAt = t + ClinicRules.Def(sim).BuildHours * 3600L;
            s.Share = cost;
            sim.State.Ledger.Post(t, sim.Period, "ledger.buildClinic", "clinic", new Posting(Account.GiftsToVillage, cost), new Posting(Account.Cash, -cost));
            s.JobId = sim.ScheduleJob(ClinicRules.BuildJob, 0, s.ReadyAt).Id;
            sim.Emit(new ClinicStarted(t, cost, s.ReadyAt));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// The doctor's quarters (spec 023 §3.1, US-148): a larger family house on its plot, yours (an asset at cost, depreciated like any house),
    /// reserved for the doctor. From cash or an ordinary co-op loan within the bank's room.
    /// </summary>
    public sealed class BuildDoctorQuarters : TimedCommand
    {
        public Financing Financing { get; }

        public BuildDoctorQuarters(long at, Financing financing = null) : base(at) { Financing = financing; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = ClinicRules.CanBuildQuarters(sim);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long cost = ClinicRules.QuartersPrice(sim);
            r = Pay.Take(sim, Financing, cost, out _);
            if (r != RejectReason.None) return CommandResult.Reject(r);

            long t = sim.State.Clock.SimTime;
            ClinicState s = ClinicRules.State(sim);
            s.Quarters = QuartersStatus.Building;
            s.QuartersCost = cost;
            s.QuartersReadyAt = t + ClinicRules.QuartersDef(sim).BuildHours * 3600L;
            sim.State.Ledger.Post(t, sim.Period, "ledger.buildQuarters", "quarters", new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            s.QuartersJobId = sim.ScheduleJob(ClinicRules.QuartersJob, 0, s.QuartersReadyAt).Id;
            sim.Emit(new QuartersStarted(t, cost, s.QuartersReadyAt));
            return CommandResult.Ok;
        }
    }

    /// <summary>Paying for a build from cash or a co-op loan within the bank's room, as the school and the road do.</summary>
    internal static class Pay
    {
        internal static RejectReason Take(Simulation sim, Financing financing, long cost, out long loan)
        {
            int lender = -1;
            loan = 0;
            if (financing != null)
            {
                if (financing.Amount > cost) return RejectReason.InvalidAmount;
                RejectReason r = CreditRules.Check(sim, financing.LenderId, financing.Amount, financing.TermSeasons, 0, out lender);
                if (r != RejectReason.None) return r;
                loan = financing.Amount;
            }
            if (!sim.State.Ledger.CanAfford(cost - loan)) return RejectReason.InsufficientFunds;
            if (financing != null) CreditSystem.Create(sim, lender, loan, financing.TermSeasons);
            return RejectReason.None;
        }
    }
}
