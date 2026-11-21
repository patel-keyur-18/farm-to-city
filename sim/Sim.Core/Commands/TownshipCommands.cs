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

    /// <summary>Advertise a civic post (spec 023 §3.7): within <c>staffHours</c> a villager of the old village takes it. No negotiation; the pay is the content's.</summary>
    public sealed class PostCivicJob : TimedCommand
    {
        /// <summary>The building, an index of <c>CivicDef</c> (0 watchman's post, 1 community hall).</summary>
        public int Civic { get; }

        public PostCivicJob(long at, int civic) : base(at) { Civic = civic; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = CivicRules.PostJob(sim, Civic);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>Let the villager go (spec 023 §3.7): the post is free and covers nobody until it is advertised again.</summary>
    public sealed class DismissStaff : TimedCommand
    {
        public int Civic { get; }

        public DismissStaff(long at, int civic) : base(at) { Civic = civic; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = CivicRules.Dismiss(sim, Civic);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>
    /// Answers a petition of your households (US-150, spec 026 §3.2): promise it by <c>Closes</c> closes (one of the content's choices: 1, 2 or 4), or decline it with 0.
    /// Doing the thing answers it with no command at all, at the next close.
    /// </summary>
    public sealed class AnswerPetition : TimedCommand
    {
        public int PetitionId { get; }

        /// <summary>The closes to promise it by, or 0 to decline.</summary>
        public int Closes { get; }

        public AnswerPetition(long at, int petitionId, int closes) : base(at) { PetitionId = petitionId; Closes = closes; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = PetitionRules.Answer(sim, PetitionId, Closes);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>
    /// Schedules this season's festival (US-149, spec 026 §3.1): at the community hall or the banyan square, paid by the treasury or as your gift, on one of the next
    /// <c>daysAhead</c> days. Paid at once and not undone.
    /// </summary>
    public sealed class ScheduleFestival : TimedCommand
    {
        /// <summary>0 the community hall, 1 the banyan square.</summary>
        public int Venue { get; }

        /// <summary>0 the treasury, 1 your gift.</summary>
        public int Payer { get; }

        /// <summary>1 tomorrow, 2 in two days, 3 in three.</summary>
        public int DaysAhead { get; }

        public ScheduleFestival(long at, int venue, int payer, int daysAhead) : base(at) { Venue = venue; Payer = payer; DaysAhead = daysAhead; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = FestivalRules.Schedule(sim, Venue, Payer, DaysAhead);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>
    /// Answers an event's card (US-154, spec 027 §3.1): the choice's cost is paid and its effects applied. A card nobody answers takes its default when its clock runs
    /// out, by the engine, not by this command.
    /// </summary>
    public sealed class AnswerEvent : TimedCommand
    {
        public int EventId { get; }

        /// <summary>The choice, from 0 in the card's order.</summary>
        public int Choice { get; }

        public AnswerEvent(long at, int eventId, int choice) : base(at) { EventId = eventId; Choice = choice; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = EventRules.Answer(sim, EventId, Choice, false);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>Crop insurance on or off (US-155, spec 027 §3.5): it covers the field crops sown after now; a crop already sown keeps the policy it was sown with.</summary>
    public sealed class SetCropInsurance : TimedCommand
    {
        public bool On { get; }

        public SetCropInsurance(long at, bool on) : base(at) { On = on; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!InsuranceRules.Available(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            InsuranceRules.State(sim).CropOn = On;
            return CommandResult.Ok;
        }
    }

    /// <summary>Property insurance on or off (US-155, spec 027 §3.6): on, it starts at the next close; off, it ends at once with no refund.</summary>
    public sealed class SetPropertyInsurance : TimedCommand
    {
        public bool On { get; }

        public SetPropertyInsurance(long at, bool on) : base(at) { On = on; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!InsuranceRules.Available(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            if (On && InsuranceRules.PropertyValue(sim) <= 0) return CommandResult.Reject(RejectReason.NoBuildings);
            InsuranceRules.SetProperty(sim, On);
            return CommandResult.Ok;
        }
    }

    /// <summary>One business's insurance on or off (US-155, spec 027 §3.6): the same rules; the business pays its own premium.</summary>
    public sealed class SetBusinessInsurance : TimedCommand
    {
        public long BusinessId { get; }
        public bool On { get; }

        public SetBusinessInsurance(long at, long businessId, bool on) : base(at) { BusinessId = businessId; On = on; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!InsuranceRules.Available(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Status != Business.BusinessStatus.Open && b.Status != Business.BusinessStatus.Building) return CommandResult.Reject(RejectReason.BusinessClosed);
            InsuranceRules.SetBusiness(sim, b, On);
            return CommandResult.Ok;
        }
    }

    /// <summary>Accepts the trader's offer of a fixed price for a staple (US-156, spec 027 §3.7): binding; a shortfall at delivery is settled at what the price rose above it.</summary>
    public sealed class AcceptContract : TimedCommand
    {
        public long OfferId { get; }

        public AcceptContract(long at, long offerId) : base(at) { OfferId = offerId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = ContractRules.Accept(sim, OfferId);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }
}
