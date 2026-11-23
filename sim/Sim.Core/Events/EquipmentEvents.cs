namespace Sim.Core.Events
{
    /// <summary>A machine is bought (spec 013 §3.2); <see cref="Cost"/> includes its bullocks.</summary>
    public sealed class MachineBought : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }
        public long Cost { get; }

        public MachineBought(long simTime, long machineId, string kind, long cost) : base(simTime) { MachineId = machineId; Kind = kind; Cost = cost; }

        protected override string Describe() { return "MachineBought(id=" + MachineId + ", kind=" + Kind + ", cost=" + Cost + ")"; }
    }

    /// <summary>A machine is leased for the rest of the season, renewed at each close (spec 013 §3.2).</summary>
    public sealed class MachineLeased : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }
        public long Cost { get; }

        public MachineLeased(long simTime, long machineId, string kind, long cost) : base(simTime) { MachineId = machineId; Kind = kind; Cost = cost; }

        protected override string Describe() { return "MachineLeased(id=" + MachineId + ", kind=" + Kind + ", cost=" + Cost + ")"; }
    }

    /// <summary>A machine is hired with its driver until midnight (spec 013 §3.2).</summary>
    public sealed class MachineHired : SimEvent
    {
        public string Kind { get; }
        public long Cost { get; }

        public MachineHired(long simTime, string kind, long cost) : base(simTime) { Kind = kind; Cost = cost; }

        protected override string Describe() { return "MachineHired(kind=" + Kind + ", cost=" + Cost + ")"; }
    }

    /// <summary>A bought machine (and its bullocks) is sold.</summary>
    public sealed class MachineSold : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }
        public long Amount { get; }

        public MachineSold(long simTime, long machineId, string kind, long amount) : base(simTime) { MachineId = machineId; Kind = kind; Amount = amount; }

        protected override string Describe() { return "MachineSold(id=" + MachineId + ", kind=" + Kind + ", amount=" + Amount + ")"; }
    }

    /// <summary>A leased machine goes back: you gave it back, or its lease couldn't be paid at the close.</summary>
    public sealed class MachineReturned : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }

        public MachineReturned(long simTime, long machineId, string kind) : base(simTime) { MachineId = machineId; Kind = kind; }

        protected override string Describe() { return "MachineReturned(id=" + MachineId + ", kind=" + Kind + ")"; }
    }

    /// <summary>A bought machine breaks down: 24 h out once its repair is paid (<see cref="Paid"/> false: it waits for the cash).</summary>
    public sealed class MachineBroke : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }
        public long Cost { get; }
        public bool Paid { get; }

        public MachineBroke(long simTime, long machineId, string kind, long cost, bool paid) : base(simTime) { MachineId = machineId; Kind = kind; Cost = cost; Paid = paid; }

        protected override string Describe() { return "MachineBroke(id=" + MachineId + ", kind=" + Kind + ", cost=" + Cost + ", paid=" + Paid + ")"; }
    }

    /// <summary>A machine of yours worked neighbours' fields this morning (US-256): the acres, the fee paid and the diesel it burnt.</summary>
    public sealed class MachineHiredOut : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }
        public int Acres { get; }
        public long Fee { get; }
        public long Diesel { get; }

        public MachineHiredOut(long simTime, long machineId, string kind, int acres, long fee, long diesel) : base(simTime)
        {
            MachineId = machineId; Kind = kind; Acres = acres; Fee = fee; Diesel = diesel;
        }

        protected override string Describe() { return "MachineHiredOut(id=" + MachineId + ", kind=" + Kind + ", acres=" + Acres + ", fee=" + Fee + ", diesel=" + Diesel + ")"; }
    }

    /// <summary>Your tempo carried neighbours' goods on the legs you did not use yesterday (US-245): the legs, what they paid and what the diesel and wear cost.</summary>
    public sealed class TempoLetOut : SimEvent
    {
        public long MachineId { get; }
        public int Legs { get; }
        public long Pay { get; }
        public long Cost { get; }

        public TempoLetOut(long simTime, long machineId, int legs, long pay, long cost) : base(simTime)
        {
            MachineId = machineId; Legs = legs; Pay = pay; Cost = cost;
        }

        protected override string Describe() { return "TempoLetOut(id=" + MachineId + ", legs=" + Legs + ", pay=" + Pay + ", cost=" + Cost + ")"; }
    }

    /// <summary>A broken machine works again.</summary>
    public sealed class MachineRepaired : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }

        public MachineRepaired(long simTime, long machineId, string kind) : base(simTime) { MachineId = machineId; Kind = kind; }

        protected override string Describe() { return "MachineRepaired(id=" + MachineId + ", kind=" + Kind + ")"; }
    }

    /// <summary>A machine seized by foreclosure, at its resale value (with its bullocks).</summary>
    public sealed class MachineSeized : SimEvent
    {
        public long MachineId { get; }
        public string Kind { get; }
        public long Amount { get; }

        public MachineSeized(long simTime, long machineId, string kind, long amount) : base(simTime) { MachineId = machineId; Kind = kind; Amount = amount; }

        protected override string Describe() { return "MachineSeized(id=" + MachineId + ", kind=" + Kind + ", amount=" + Amount + ")"; }
    }
}
