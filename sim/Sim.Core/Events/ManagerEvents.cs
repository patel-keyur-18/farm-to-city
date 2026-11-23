namespace Sim.Core.Events
{
    /// <summary>A manager took a business over (spec 028 §3.2): the base wage for the rest of the season paid from its cash.</summary>
    public sealed class ManagerHired : SimEvent
    {
        public long BusinessId { get; }
        public long WorkerId { get; }
        public long Wage { get; }
        public long Prepaid { get; }

        public ManagerHired(long simTime, long businessId, long workerId, long wage, long prepaid) : base(simTime)
        {
            BusinessId = businessId; WorkerId = workerId; Wage = wage; Prepaid = prepaid;
        }

        protected override string Describe() { return "ManagerHired(business=" + BusinessId + ", manager=" + WorkerId + ", wage=" + Wage + ", prepaid=" + Prepaid + ")"; }
    }

    /// <summary>A manager's contract ended (spec 028 §3.2): you let him go, he took an offer, or his wage could not be paid.</summary>
    public sealed class ManagerLeft : SimEvent
    {
        public long BusinessId { get; }
        public long WorkerId { get; }
        public long Severance { get; }

        public ManagerLeft(long simTime, long businessId, long workerId, long severance) : base(simTime)
        {
            BusinessId = businessId; WorkerId = workerId; Severance = severance;
        }

        protected override string Describe() { return "ManagerLeft(business=" + BusinessId + ", manager=" + WorkerId + ", severance=" + Severance + ")"; }
    }

    /// <summary>A manager reported at a close (spec 028 §3.2): his target met or missed, and the share of the profit he was paid.</summary>
    public sealed class ManagerReported : SimEvent
    {
        public long BusinessId { get; }
        public long WorkerId { get; }
        public bool Met { get; }
        public long Share { get; }

        public ManagerReported(long simTime, long businessId, long workerId, bool met, long share) : base(simTime)
        {
            BusinessId = businessId; WorkerId = workerId; Met = met; Share = share;
        }

        protected override string Describe() { return "ManagerReported(business=" + BusinessId + ", manager=" + WorkerId + ", met=" + Met + ", share=" + Share + ")"; }
    }
}
