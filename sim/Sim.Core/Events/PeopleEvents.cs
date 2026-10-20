using System.Collections.Generic;
using Sim.Core.People;

namespace Sim.Core.Events
{
    /// <summary>Day labourers hired for the rest of the day (spec 006 §3.2).</summary>
    public sealed class DayLabourersHired : SimEvent
    {
        public IReadOnlyList<long> NeighbourIds { get; }
        public long Cost { get; }

        public DayLabourersHired(long simTime, IReadOnlyList<long> neighbourIds, long cost) : base(simTime)
        {
            NeighbourIds = neighbourIds;
            Cost = cost;
        }

        protected override string Describe() { return "DayLabourersHired(ids=" + string.Join(",", NeighbourIds) + ", cost=" + Cost + ")"; }
    }

    /// <summary>The day labourers went home at the daily pass.</summary>
    public sealed class DayLabourersLeft : SimEvent
    {
        public int Count { get; }

        public DayLabourersLeft(long simTime, int count) : base(simTime) { Count = count; }

        protected override string Describe() { return "DayLabourersLeft(count=" + Count + ")"; }
    }

    /// <summary>A hand's hut was started (spec 006 §3.4).</summary>
    public sealed class ConstructionStarted : SimEvent
    {
        public long ConstructionId { get; }
        public int TileId { get; }
        public long Cost { get; }
        public long ReadyAt { get; }

        public ConstructionStarted(long simTime, long constructionId, int tileId, long cost, long readyAt) : base(simTime)
        {
            ConstructionId = constructionId;
            TileId = tileId;
            Cost = cost;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "ConstructionStarted(id=" + ConstructionId + ", tile=" + TileId + ", cost=" + Cost + ", readyAt=" + ReadyAt + ")"; }
    }

    /// <summary>A hand's hut is finished: a hand can move in (spec 006 §3.4).</summary>
    public sealed class HandHutBuilt : SimEvent
    {
        public long BuildingId { get; }
        public int TileId { get; }

        public HandHutBuilt(long simTime, long buildingId, int tileId) : base(simTime)
        {
            BuildingId = buildingId;
            TileId = tileId;
        }

        protected override string Describe() { return "HandHutBuilt(building=" + BuildingId + ", tile=" + TileId + ")"; }
    }

    public sealed class JobPosted : SimEvent
    {
        public bool AboveMarket { get; }
        public long Cost { get; }
        public long ExpiresAt { get; }

        public JobPosted(long simTime, bool aboveMarket, long cost, long expiresAt) : base(simTime)
        {
            AboveMarket = aboveMarket;
            Cost = cost;
            ExpiresAt = expiresAt;
        }

        protected override string Describe() { return "JobPosted(aboveMarket=" + AboveMarket + ", cost=" + Cost + ", expiresAt=" + ExpiresAt + ")"; }
    }

    public sealed class JobPostExpired : SimEvent
    {
        public JobPostExpired(long simTime) : base(simTime) { }

        protected override string Describe() { return "JobPostExpired"; }
    }

    public sealed class ApplicantArrived : SimEvent
    {
        public long ApplicantId { get; }

        public ApplicantArrived(long simTime, long applicantId) : base(simTime) { ApplicantId = applicantId; }

        protected override string Describe() { return "ApplicantArrived(id=" + ApplicantId + ")"; }
    }

    /// <summary>A day labourer hired for six full days asks to move in as the resident hand (spec 006 §3.3, ADR 0041).</summary>
    public sealed class RegularAsksToMoveIn : SimEvent
    {
        public long ApplicantId { get; }
        public long NeighbourId { get; }

        public RegularAsksToMoveIn(long simTime, long applicantId, long neighbourId) : base(simTime)
        {
            ApplicantId = applicantId;
            NeighbourId = neighbourId;
        }

        protected override string Describe() { return "RegularAsksToMoveIn(applicant=" + ApplicantId + ", neighbour=" + NeighbourId + ")"; }
    }

    public sealed class ApplicantLeft : SimEvent
    {
        public long ApplicantId { get; }

        public ApplicantLeft(long simTime, long applicantId) : base(simTime) { ApplicantId = applicantId; }

        protected override string Describe() { return "ApplicantLeft(id=" + ApplicantId + ")"; }
    }

    public sealed class ApplicantInterviewed : SimEvent
    {
        public long ApplicantId { get; }
        public string Trait { get; }

        public ApplicantInterviewed(long simTime, long applicantId, string trait) : base(simTime)
        {
            ApplicantId = applicantId;
            Trait = trait;
        }

        protected override string Describe() { return "ApplicantInterviewed(id=" + ApplicantId + ", trait=" + Trait + ")"; }
    }

    public sealed class OfferDeclined : SimEvent
    {
        public long ApplicantId { get; }

        public OfferDeclined(long simTime, long applicantId) : base(simTime) { ApplicantId = applicantId; }

        protected override string Describe() { return "OfferDeclined(id=" + ApplicantId + ")"; }
    }

    /// <summary>An offer was accepted: the applicant is now a hand under contract (spec 006 §3.6).</summary>
    public sealed class HandHired : SimEvent
    {
        public long ApplicantId { get; }
        public long HandId { get; }
        public long Wage { get; }
        public long Prepaid { get; }

        public HandHired(long simTime, long applicantId, long handId, long wage, long prepaid) : base(simTime)
        {
            ApplicantId = applicantId;
            HandId = handId;
            Wage = wage;
            Prepaid = prepaid;
        }

        protected override string Describe() { return "HandHired(applicant=" + ApplicantId + ", hand=" + HandId + ", wage=" + Wage + ", prepaid=" + Prepaid + ")"; }
    }

    public sealed class ContractRenewed : SimEvent
    {
        public long HandId { get; }
        public long Wage { get; }
        public long Paid { get; }

        public ContractRenewed(long simTime, long handId, long wage, long paid) : base(simTime)
        {
            HandId = handId;
            Wage = wage;
            Paid = paid;
        }

        protected override string Describe() { return "ContractRenewed(hand=" + HandId + ", wage=" + Wage + ", paid=" + Paid + ")"; }
    }

    /// <summary>Renewal couldn't be paid: the hand stops work and waits out the grace (spec 006 §3.6).</summary>
    public sealed class ContractLapsed : SimEvent
    {
        public long HandId { get; }
        public long Needed { get; }
        public long Cash { get; }

        public ContractLapsed(long simTime, long handId, long needed, long cash) : base(simTime)
        {
            HandId = handId;
            Needed = needed;
            Cash = cash;
        }

        protected override string Describe() { return "ContractLapsed(hand=" + HandId + ", needed=" + Needed + ", cash=" + Cash + ")"; }
    }

    /// <summary>A lapsed hand left after the grace or at the next close (spec 006 §3.6).</summary>
    public sealed class HandLeft : SimEvent
    {
        public long HandId { get; }

        public HandLeft(long simTime, long handId) : base(simTime) { HandId = handId; }

        protected override string Describe() { return "HandLeft(hand=" + HandId + ")"; }
    }

    public sealed class HandDismissed : SimEvent
    {
        public long HandId { get; }
        public long Refund { get; }
        public long Severance { get; }

        public HandDismissed(long simTime, long handId, long refund, long severance) : base(simTime)
        {
            HandId = handId;
            Refund = refund;
            Severance = severance;
        }

        protected override string Describe() { return "HandDismissed(hand=" + HandId + ", refund=" + Refund + ", severance=" + Severance + ")"; }
    }

    /// <summary>A hand's skill grew at the season close (spec 006 §3.7).</summary>
    public sealed class HandSkillGrew : SimEvent
    {
        public long HandId { get; }
        public Maths.Fixed Skill { get; }

        public HandSkillGrew(long simTime, long handId, Maths.Fixed skill) : base(simTime)
        {
            HandId = handId;
            Skill = skill;
        }

        protected override string Describe() { return "HandSkillGrew(hand=" + HandId + ", skill=" + Skill + ")"; }
    }

    public sealed class StandingOrderSet : SimEvent
    {
        public long HandId { get; }
        public OrderTargetKind Kind { get; }
        public long Target { get; }

        public StandingOrderSet(long simTime, long handId, OrderTargetKind kind, long target) : base(simTime)
        {
            HandId = handId;
            Kind = kind;
            Target = target;
        }

        protected override string Describe() { return "StandingOrderSet(hand=" + HandId + ", " + Kind + "=" + Target + ")"; }
    }

    public sealed class StandingOrderCleared : SimEvent
    {
        public long HandId { get; }
        public OrderTargetKind Kind { get; }
        public long Target { get; }

        public StandingOrderCleared(long simTime, long handId, OrderTargetKind kind, long target) : base(simTime)
        {
            HandId = handId;
            Kind = kind;
            Target = target;
        }

        protected override string Describe() { return "StandingOrderCleared(hand=" + HandId + ", " + Kind + "=" + Target + ")"; }
    }

    /// <summary>A hand carried out an order (spec 006 §3.8): what, how much, and the cash it moved (sales +, inputs −).</summary>
    public sealed class OrderExecuted : SimEvent
    {
        public long HandId { get; }
        public OrderTargetKind Kind { get; }
        public long Target { get; }
        public OrderAction Action { get; }
        public string GoodId { get; }
        public long Quantity { get; }
        public long Amount { get; }

        public OrderExecuted(long simTime, long handId, OrderTargetKind kind, long target, OrderAction action, string goodId, long quantity, long amount)
            : base(simTime)
        {
            HandId = handId;
            Kind = kind;
            Target = target;
            Action = action;
            GoodId = goodId;
            Quantity = quantity;
            Amount = amount;
        }

        protected override string Describe()
        {
            return "OrderExecuted(hand=" + HandId + ", " + Kind + "=" + Target + ", action=" + Action + ", good=" + GoodId + ", quantity=" + Quantity + ", amount=" + Amount + ")";
        }
    }

    /// <summary>An order can't act now (spec 006 §3.8); emitted once each time the reason changes.</summary>
    public sealed class OrderWaiting : SimEvent
    {
        public long HandId { get; }
        public OrderTargetKind Kind { get; }
        public long Target { get; }
        public OrderWait Reason { get; }

        public OrderWaiting(long simTime, long handId, OrderTargetKind kind, long target, OrderWait reason) : base(simTime)
        {
            HandId = handId;
            Kind = kind;
            Target = target;
            Reason = reason;
        }

        protected override string Describe() { return "OrderWaiting(hand=" + HandId + ", " + Kind + "=" + Target + ", reason=" + Reason + ")"; }
    }

    /// <summary>A this-season-only order finished: its harvest or pick is in, and the order is gone.</summary>
    public sealed class OrderCompleted : SimEvent
    {
        public long HandId { get; }
        public OrderTargetKind Kind { get; }
        public long Target { get; }

        public OrderCompleted(long simTime, long handId, OrderTargetKind kind, long target) : base(simTime)
        {
            HandId = handId;
            Kind = kind;
            Target = target;
        }

        protected override string Describe() { return "OrderCompleted(hand=" + HandId + ", " + Kind + "=" + Target + ")"; }
    }
}
