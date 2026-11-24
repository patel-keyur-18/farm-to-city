using Sim.Core.Definitions;

namespace Sim.Core.Events
{
    /// <summary>An entry was written to the Chronicle (spec 029 §5): the Chronicle sheet and the bell read it.</summary>
    public sealed class ChronicleWritten : SimEvent
    {
        public ChronicleKind Kind { get; }

        public ChronicleWritten(long simTime, ChronicleKind kind) : base(simTime) { Kind = kind; }

        protected override string Describe() { return "ChronicleWritten(" + Kind + ")"; }
    }

    /// <summary>A loan's balance reached nothing (spec 029 §3.2, gap 10): the lender, and what the loan was for.</summary>
    public sealed class LoanCleared : SimEvent
    {
        public long LoanId { get; }
        public string LenderId { get; }
        public long Amount { get; }

        public LoanCleared(long simTime, long loanId, string lenderId, long amount) : base(simTime) { LoanId = loanId; LenderId = lenderId; Amount = amount; }

        protected override string Describe() { return "LoanCleared(" + LoanId + ")"; }
    }

    /// <summary>A fire's repair was decided and paid (spec 029 §3.2, gap 10): what the repair cost.</summary>
    public sealed class FireRepaired : SimEvent
    {
        public long Target { get; }
        public bool Business { get; }
        public long Cost { get; }

        public FireRepaired(long simTime, long target, bool business, long cost) : base(simTime) { Target = target; Business = business; Cost = cost; }

        protected override string Describe() { return "FireRepaired(" + Target + ")"; }
    }

    /// <summary>The Almanac of a year was written at its Winter close (spec 029 §3.4): the year (1-based).</summary>
    public sealed class AlmanacWritten : SimEvent
    {
        public long Year { get; }

        public AlmanacWritten(long simTime, long year) : base(simTime) { Year = year; }

        protected override string Describe() { return "AlmanacWritten(" + Year + ")"; }
    }

    /// <summary>A citizen's story moved a step (spec 029 §3.6): the arc (<c>StoryArc</c>), the step (1 to 4), the citizen, and the stall's kind or the age or count the line says.</summary>
    public sealed class StoryStepped : SimEvent
    {
        public int Arc { get; }
        public int Step { get; }
        public long CitizenId { get; }
        public int N { get; }
        public string Kind { get; }

        public StoryStepped(long simTime, int arc, int step, long citizenId, int n, string kind) : base(simTime)
        {
            Arc = arc; Step = step; CitizenId = citizenId; N = n; Kind = kind;
        }

        protected override string Describe() { return "StoryStepped(arc=" + Arc + ", step=" + Step + ", citizen=" + CitizenId + ")"; }
    }
}
