using System.Collections.Generic;
using Sim.Core.People;

namespace Sim.Core.Events
{
    /// <summary>Labourers hired till the crops growing now are in (spec 006 §3.2, ADR 0049 §3).</summary>
    public sealed class LabourersHired : SimEvent
    {
        public IReadOnlyList<long> NeighbourIds { get; }
        public long Cost { get; }
        public long Until { get; }

        public LabourersHired(long simTime, IReadOnlyList<long> neighbourIds, long cost, long until) : base(simTime)
        {
            NeighbourIds = neighbourIds;
            Cost = cost;
            Until = until;
        }

        protected override string Describe() { return "LabourersHired(ids=" + string.Join(",", NeighbourIds) + ", cost=" + Cost + ", until=" + Until + ")"; }
    }

    /// <summary>Hired labourers harvested a ripe field that has no hand's order (ADR 0049 §3).</summary>
    public sealed class LabourersHarvested : SimEvent
    {
        public int TileId { get; }
        public long Grams { get; }

        public LabourersHarvested(long simTime, int tileId, long grams) : base(simTime)
        {
            TileId = tileId;
            Grams = grams;
        }

        protected override string Describe() { return "LabourersHarvested(tile=" + TileId + ", grams=" + Grams + ")"; }
    }

    /// <summary>Labourers went home: the crops they were hired for are in.</summary>
    public sealed class LabourersLeft : SimEvent
    {
        public int Count { get; }

        public LabourersLeft(long simTime, int count) : base(simTime) { Count = count; }

        protected override string Describe() { return "LabourersLeft(count=" + Count + ")"; }
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

        /// <summary>0 your fields, else the business (spec 015 §3.1).</summary>
        public long Place { get; }

        public JobPosted(long simTime, bool aboveMarket, long cost, long expiresAt, long place = 0) : base(simTime)
        {
            AboveMarket = aboveMarket;
            Cost = cost;
            ExpiresAt = expiresAt;
            Place = place;
        }

        protected override string Describe()
        {
            return "JobPosted(" + (Place == 0 ? "" : "place=" + Place + ", ") + "aboveMarket=" + AboveMarket + ", cost=" + Cost + ", expiresAt=" + ExpiresAt + ")";
        }
    }

    public sealed class JobPostExpired : SimEvent
    {
        public long Place { get; }

        public JobPostExpired(long simTime, long place = 0) : base(simTime) { Place = place; }

        protected override string Describe() { return Place == 0 ? "JobPostExpired" : "JobPostExpired(place=" + Place + ")"; }
    }

    /// <summary>A reference check came back (spec 015 §3.2): the skill now ±5, and how they left their last job.</summary>
    public sealed class ReferenceChecked : SimEvent
    {
        public long ApplicantId { get; }
        public bool GoodTerms { get; }

        public ReferenceChecked(long simTime, long applicantId, bool goodTerms) : base(simTime)
        {
            ApplicantId = applicantId;
            GoodTerms = goodTerms;
        }

        protected override string Describe() { return "ReferenceChecked(id=" + ApplicantId + ", goodTerms=" + GoodTerms + ")"; }
    }

    /// <summary>A low offer refused: the applicant asks for their asked wage instead (spec 015 §3.3).</summary>
    public sealed class CounterOffer : SimEvent
    {
        public long ApplicantId { get; }
        public long Wage { get; }

        public CounterOffer(long simTime, long applicantId, long wage) : base(simTime)
        {
            ApplicantId = applicantId;
            Wage = wage;
        }

        protected override string Describe() { return "CounterOffer(id=" + ApplicantId + ", wage=" + Wage + ")"; }
    }

    /// <summary>A business's worker hired from the board (spec 015 §3.1), the rest of the season paid from its cash.</summary>
    public sealed class WorkerHired : SimEvent
    {
        public long BusinessId { get; }
        public long WorkerId { get; }
        public long Wage { get; }
        public long Prepaid { get; }

        public WorkerHired(long simTime, long businessId, long workerId, long wage, long prepaid) : base(simTime)
        {
            BusinessId = businessId;
            WorkerId = workerId;
            Wage = wage;
            Prepaid = prepaid;
        }

        protected override string Describe() { return "WorkerHired(business=" + BusinessId + ", worker=" + WorkerId + ", wage=" + Wage + ", prepaid=" + Prepaid + ")"; }
    }

    /// <summary>A business's worker left: let go, retired, or gone from the town (spec 015 §3.9); the business waits for a hire or you.</summary>
    public sealed class WorkerLeft : SimEvent
    {
        public long BusinessId { get; }
        public long WorkerId { get; }
        public long Severance { get; }

        public WorkerLeft(long simTime, long businessId, long workerId, long severance) : base(simTime)
        {
            BusinessId = businessId;
            WorkerId = workerId;
            Severance = severance;
        }

        protected override string Describe() { return "WorkerLeft(business=" + BusinessId + ", worker=" + WorkerId + ", severance=" + Severance + ")"; }
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

    /// <summary>A hand turned the retirement age (spec 014 §3.2): not renewed; gone home to his village, or staying with his family.</summary>
    public sealed class CitizenRetired : SimEvent
    {
        public long CitizenId { get; }
        public long HandId { get; }

        /// <summary>Index into the names: he may already be gone from the town.</summary>
        public int Name { get; }
        public int Age { get; }

        /// <summary>He lived alone in his hut and went home to his village (the hut is free).</summary>
        public bool WentHome { get; }

        public CitizenRetired(long simTime, long citizenId, long handId, int name, int age, bool wentHome) : base(simTime)
        {
            CitizenId = citizenId;
            HandId = handId;
            Name = name;
            Age = age;
            WentHome = wentHome;
        }

        protected override string Describe() { return "CitizenRetired(citizen=" + CitizenId + ", hand=" + HandId + ", age=" + Age + ", home=" + WentHome + ")"; }
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
