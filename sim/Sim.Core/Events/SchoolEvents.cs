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

    /// <summary>How a citizen learned to read (spec 018 §5).</summary>
    public enum LiterateHow
    {
        School = 0,
        AdultClass = 1,
    }

    /// <summary>A citizen can read: they finished primary at the village's school or an adult class (spec 018 §5).</summary>
    public sealed class CitizenLiterate : SimEvent
    {
        public long CitizenId { get; }
        public int Name { get; }
        public LiterateHow How { get; }

        public CitizenLiterate(long simTime, long citizenId, int name, LiterateHow how) : base(simTime)
        {
            CitizenId = citizenId;
            Name = name;
            How = how;
        }

        protected override string Describe() { return "CitizenLiterate(" + CitizenId + ", " + How + ")"; }
    }

    /// <summary>The founder started an adult class of <see cref="Pupils"/> (ADR 0095 §4), a gift of <see cref="Cost"/>.</summary>
    public sealed class AdultClassStarted : SimEvent
    {
        public int Pupils { get; }
        public long Cost { get; }
        public long EndsAt { get; }

        public AdultClassStarted(long simTime, int pupils, long cost, long endsAt) : base(simTime)
        {
            Pupils = pupils;
            Cost = cost;
            EndsAt = endsAt;
        }

        protected override string Describe() { return "AdultClassStarted(" + Pupils + ", cost=" + Cost + ")"; }
    }

    /// <summary>A Concept Codex card was found (spec 018 §5): the toast, Menu → Codex.</summary>
    public sealed class CodexCardFound : SimEvent
    {
        public string CardId { get; }

        public CodexCardFound(long simTime, string cardId) : base(simTime) { CardId = cardId; }

        protected override string Describe() { return "CodexCardFound(" + CardId + ")"; }
    }
}
