namespace Sim.Core.Events
{
    /// <summary>A supervisor starts at the farm (spec 028 §3.9): his wage for the rest of the season paid from your cash.</summary>
    public sealed class SupervisorHired : SimEvent
    {
        public long Wage { get; }
        public long Paid { get; }

        public SupervisorHired(long simTime, long wage, long paid) : base(simTime) { Wage = wage; Paid = paid; }

        protected override string Describe() { return "SupervisorHired(wage=" + Wage + ", paid=" + Paid + ")"; }
    }

    /// <summary>The supervisor goes (let go, or his wage could not be paid at the close).</summary>
    public sealed class SupervisorLeft : SimEvent
    {
        public bool Unpaid { get; }

        public SupervisorLeft(long simTime, bool unpaid) : base(simTime) { Unpaid = unpaid; }

        protected override string Describe() { return "SupervisorLeft(unpaid=" + Unpaid + ")"; }
    }

    /// <summary>A worker is hurt at work (spec 028 §3.9): out for <see cref="Hours"/>; the bill paid is <see cref="Bill"/>.</summary>
    public sealed class AccidentHappened : SimEvent
    {
        public long Place { get; }
        public long WorkerId { get; }
        public int Hours { get; }
        public bool Severe { get; }
        public long Bill { get; }

        public AccidentHappened(long simTime, long place, long workerId, int hours, bool severe, long bill) : base(simTime)
        {
            Place = place;
            WorkerId = workerId;
            Hours = hours;
            Severe = severe;
            Bill = bill;
        }

        protected override string Describe() { return "AccidentHappened(place=" + Place + ", worker=" + WorkerId + ", hours=" + Hours + ", severe=" + Severe + ", bill=" + Bill + ")"; }
    }

    /// <summary>Two workers quarrel (spec 028 §3.9) and a card waits; or, with a supervisor, he settles it (<see cref="Settled"/>).</summary>
    public sealed class ConflictRaised : SimEvent
    {
        public long Place { get; }
        public bool Settled { get; }

        public ConflictRaised(long simTime, long place, bool settled) : base(simTime) { Place = place; Settled = settled; }

        protected override string Describe() { return "ConflictRaised(place=" + Place + ", settled=" + Settled + ")"; }
    }

    /// <summary>A place's workers form an association and ask for a raise (spec 028 §3.9).</summary>
    public sealed class AssociationFormed : SimEvent
    {
        public long Place { get; }

        public AssociationFormed(long simTime, long place) : base(simTime) { Place = place; }

        protected override string Describe() { return "AssociationFormed(place=" + Place + ")"; }
    }

    /// <summary>A card nobody answered was answered by its default, live or away (spec 028 §3.9, ADR 0166 gap 4).</summary>
    public sealed class HrEventDefaulted : SimEvent
    {
        public long EventId { get; }
        public People.HrEventKind Kind { get; }
        public int Choice { get; }
        public long Place { get; }
        public long WorkerId { get; }

        public HrEventDefaulted(long simTime, long eventId, People.HrEventKind kind, int choice, long place, long workerId) : base(simTime)
        {
            EventId = eventId;
            Kind = kind;
            Choice = choice;
            Place = place;
            WorkerId = workerId;
        }

        protected override string Describe() { return "HrEventDefaulted(" + EventId + ", " + Kind + ", choice=" + Choice + ", place=" + Place + ", worker=" + WorkerId + ")"; }
    }

    /// <summary>What a chancy answer came to (spec 028 §3.9): the talk that settled a quarrel or not, the negotiation an association agreed to or not.</summary>
    public sealed class HrOutcome : SimEvent
    {
        public People.HrEventKind Kind { get; }
        public int Choice { get; }
        public bool Yes { get; }

        public HrOutcome(long simTime, People.HrEventKind kind, int choice, bool yes) : base(simTime)
        {
            Kind = kind;
            Choice = choice;
            Yes = yes;
        }

        protected override string Describe() { return "HrOutcome(" + Kind + ", choice=" + Choice + ", yes=" + Yes + ")"; }
    }
}
