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

        /// <summary>True when the burning place is a business (<see cref="Building"/> is then its id).</summary>
        public bool Business { get; }

        public FireStruck(long simTime, long building, long bill, bool tower, bool prepared, bool business = false) : base(simTime) { Building = building; Bill = bill; Tower = tower; Prepared = prepared; Business = business; }

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
        /// <summary>The share of families it made ill, in percent (spec 029 §3.2: "{n} families were ill").</summary>
        public int FactorPercent { get; }

        public EpidemicEnded(long simTime, int factorPercent = 0) : base(simTime) { FactorPercent = factorPercent; }

        protected override string Describe() { return "EpidemicEnded"; }
    }

    /// <summary>A regional grant was paid at a close (or refused: the books and credit were not sound).</summary>
    public sealed class RegionalGrantPaid : SimEvent
    {
        public long Amount { get; }

        public RegionalGrantPaid(long simTime, long amount) : base(simTime) { Amount = amount; }

        protected override string Describe() { return "RegionalGrantPaid(" + Amount + ")"; }
    }

    /// <summary>A policy's cover began (a close charged its first premium, or its premium again after a lapse): 0 crop, 1 property, 2 business; the business, if one.</summary>
    public sealed class InsuranceStarted : SimEvent
    {
        public int Policy { get; }
        public long Business { get; }

        public InsuranceStarted(long simTime, int policy, long business) : base(simTime) { Policy = policy; Business = business; }

        protected override string Describe() { return "InsuranceStarted(policy " + Policy + ", business " + Business + ")"; }
    }

    /// <summary>A policy's premium could not be paid at the close, so it ended (never a debt).</summary>
    public sealed class InsuranceLapsed : SimEvent
    {
        public int Policy { get; }
        public long Business { get; }
        public long Premium { get; }

        public InsuranceLapsed(long simTime, int policy, long business, long premium) : base(simTime) { Policy = policy; Business = business; Premium = premium; }

        protected override string Describe() { return "InsuranceLapsed(policy " + Policy + ", business " + Business + ", premium " + Premium + ")"; }
    }

    /// <summary>A premium was charged: at a sowing (crop), or at the close (property, business).</summary>
    public sealed class PremiumCharged : SimEvent
    {
        public int Policy { get; }
        public long Business { get; }
        public long Amount { get; }

        public PremiumCharged(long simTime, int policy, long business, long amount) : base(simTime) { Policy = policy; Business = business; Amount = amount; }

        protected override string Describe() { return "PremiumCharged(policy " + Policy + ", business " + Business + ", " + Amount + ")"; }
    }

    /// <summary>A claim was paid. <c>Cause</c> is an event's definition + 1, 0 the season's weather, −1 pests; <c>Target</c> the crop's definition, the building or the business; <c>Days</c> the days shut paid for.</summary>
    public sealed class ClaimPaid : SimEvent
    {
        public int Policy { get; }
        public int Cause { get; }
        public long Target { get; }
        public long Amount { get; }
        public int Days { get; }

        public ClaimPaid(long simTime, int policy, int cause, long target, long amount, int days) : base(simTime) { Policy = policy; Cause = cause; Target = target; Amount = amount; Days = days; }

        protected override string Describe() { return "ClaimPaid(policy " + Policy + ", cause " + Cause + ", target " + Target + ", " + Amount + ", days " + Days + ")"; }
    }

    /// <summary>A crop was sown uninsured because the cash could not pay its premium.</summary>
    public sealed class CropNotInsured : SimEvent
    {
        public int TileId { get; }
        public int Crop { get; }
        public long Premium { get; }

        public CropNotInsured(long simTime, int tileId, int crop, long premium) : base(simTime) { TileId = tileId; Crop = crop; Premium = premium; }

        protected override string Describe() { return "CropNotInsured(tile " + TileId + ", crop " + Crop + ", premium " + Premium + ")"; }
    }
}
