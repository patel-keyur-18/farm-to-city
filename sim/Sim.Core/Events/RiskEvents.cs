namespace Sim.Core.Events
{
    /// <summary>A card came (spec 027 §3.1): the event's id in the content, whether it is a major, and the card's number.</summary>
    public sealed class EventRaised : SimEvent
    {
        public int Id { get; }
        public int Def { get; }
        public bool Major { get; }

        public EventRaised(long simTime, int id, int def, bool major) : base(simTime) { Id = id; Def = def; Major = major; }

        protected override string Describe() { return "EventRaised(id " + Id + ", def " + Def + ", major " + Major + ")"; }
    }

    /// <summary>A card was answered: the choice taken, and what it cost (negative) or paid.</summary>
    public sealed class EventAnswered : SimEvent
    {
        public int Id { get; }
        public int Def { get; }
        public int Choice { get; }
        public long Cash { get; }

        public EventAnswered(long simTime, int id, int def, int choice, long cash) : base(simTime) { Id = id; Def = def; Choice = choice; Cash = cash; }

        protected override string Describe() { return "EventAnswered(id " + Id + ", def " + Def + ", choice " + Choice + ", cash " + Cash + ")"; }
    }

    /// <summary>A card's clock ran out (or a minor came while you were away) and its default was taken, for the away card.</summary>
    public sealed class EventDefaulted : SimEvent
    {
        public int Id { get; }
        public int Def { get; }
        public int Choice { get; }
        public long Cash { get; }

        public EventDefaulted(long simTime, int id, int def, int choice, long cash) : base(simTime) { Id = id; Def = def; Choice = choice; Cash = cash; }

        protected override string Describe() { return "EventDefaulted(id " + Id + ", def " + Def + ", choice " + Choice + ", cash " + Cash + ")"; }
    }

    /// <summary>A disaster was signalled (the card is the warning): its kind, what it falls on, and when it strikes.</summary>
    public sealed class DisasterWarned : SimEvent
    {
        public int Def { get; }
        public long Target { get; }
        public long StrikeAt { get; }

        public DisasterWarned(long simTime, int def, long target, long strikeAt) : base(simTime) { Def = def; Target = target; StrikeAt = strikeAt; }

        protected override string Describe() { return "DisasterWarned(def " + Def + ", target " + Target + ", strikeAt " + StrikeAt + ")"; }
    }

    /// <summary>A signalled disaster passed without striking: you were away too long (it slid its three days).</summary>
    public sealed class DisasterPassed : SimEvent
    {
        public int Def { get; }

        public DisasterPassed(long simTime, int def) : base(simTime) { Def = def; }

        protected override string Describe() { return "DisasterPassed(def " + Def + ")"; }
    }

    /// <summary>A fire struck a building: the damage, and whether the water tower softened it.</summary>
    public sealed class FireStruck : SimEvent
    {
        public long Building { get; }
        public long Bill { get; }
        public bool Tower { get; }
        public bool Prepared { get; }

        public FireStruck(long simTime, long building, long bill, bool tower, bool prepared) : base(simTime) { Building = building; Bill = bill; Tower = tower; Prepared = prepared; }

        protected override string Describe() { return "FireStruck(building " + Building + ", bill " + Bill + ", tower " + Tower + ", prepared " + Prepared + ")"; }
    }

    /// <summary>The epidemic began (its sick-day factor in hundredths and its last day) and ended.</summary>
    public sealed class EpidemicBegan : SimEvent
    {
        public int FactorPercent { get; }
        public long Until { get; }

        public EpidemicBegan(long simTime, int factorPercent, long until) : base(simTime) { FactorPercent = factorPercent; Until = until; }

        protected override string Describe() { return "EpidemicBegan(factor " + FactorPercent + ", until " + Until + ")"; }
    }

    public sealed class EpidemicEnded : SimEvent
    {
        public EpidemicEnded(long simTime) : base(simTime) { }

        protected override string Describe() { return "EpidemicEnded"; }
    }

    /// <summary>A regional grant was paid at a close (or refused: the books and credit were not sound).</summary>
    public sealed class RegionalGrantPaid : SimEvent
    {
        public long Amount { get; }

        public RegionalGrantPaid(long simTime, long amount) : base(simTime) { Amount = amount; }

        protected override string Describe() { return "RegionalGrantPaid(" + Amount + ")"; }
    }
}
