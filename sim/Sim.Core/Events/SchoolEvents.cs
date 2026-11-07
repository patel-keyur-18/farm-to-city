namespace Sim.Core.Events
{
    /// <summary>The founder started building the village's school (spec 018 §3.1), a gift of <see cref="Cost"/>.</summary>
    public sealed class SchoolStarted : SimEvent
    {
        public long Cost { get; }
        public long ReadyAt { get; }

        public SchoolStarted(long simTime, long cost, long readyAt) : base(simTime)
        {
            Cost = cost;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "SchoolStarted(cost=" + Cost + ", ready=" + ReadyAt + ")"; }
    }

    /// <summary>The school is open and the district has posted its teacher (spec 018 §5).</summary>
    public sealed class SchoolOpened : SimEvent
    {
        public SchoolOpened(long simTime) : base(simTime) { }

        protected override string Describe() { return "SchoolOpened"; }
    }
}
