namespace Sim.Core.Events
{
    /// <summary>The cattle shed's building has started (spec 012 §3.3).</summary>
    public sealed class ShedStarted : SimEvent
    {
        public long Cost { get; }
        public long ReadyAt { get; }

        public ShedStarted(long simTime, long cost, long readyAt) : base(simTime)
        {
            Cost = cost;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "ShedStarted(cost=" + Cost + ", readyAt=" + ReadyAt + ")"; }
    }

    /// <summary>The cattle shed is finished: animals can be bought (spec 012 §3.3).</summary>
    public sealed class ShedBuilt : SimEvent
    {
        public long BuildingId { get; }

        public ShedBuilt(long simTime, long buildingId) : base(simTime) { BuildingId = buildingId; }

        protected override string Describe() { return "ShedBuilt(building=" + BuildingId + ")"; }
    }

    public sealed class AnimalBought : SimEvent
    {
        public long AnimalId { get; }
        public string Kind { get; }
        public string Name { get; }
        public long Amount { get; }

        public AnimalBought(long simTime, long animalId, string kind, string name, long amount) : base(simTime)
        {
            AnimalId = animalId;
            Kind = kind;
            Name = name;
            Amount = amount;
        }

        protected override string Describe() { return "AnimalBought(animal=" + AnimalId + ", kind=" + Kind + ", name=" + Name + ", amount=" + Amount + ")"; }
    }

    public sealed class AnimalSold : SimEvent
    {
        public long AnimalId { get; }
        public string Name { get; }
        public long Amount { get; }

        public AnimalSold(long simTime, long animalId, string name, long amount) : base(simTime)
        {
            AnimalId = animalId;
            Name = name;
            Amount = amount;
        }

        protected override string Describe() { return "AnimalSold(animal=" + AnimalId + ", name=" + Name + ", amount=" + Amount + ")"; }
    }

    /// <summary>The co-op's 6:00 collection (spec 012 §3.5): millilitres in all, what the co-op paid, and what the household kept.</summary>
    public sealed class MilkCollected : SimEvent
    {
        public long Millilitres { get; }
        public long Amount { get; }
        public long HomeMillilitres { get; }
        public long HomeAmount { get; }

        public MilkCollected(long simTime, long millilitres, long amount, long homeMillilitres, long homeAmount) : base(simTime)
        {
            Millilitres = millilitres;
            Amount = amount;
            HomeMillilitres = homeMillilitres;
            HomeAmount = homeAmount;
        }

        protected override string Describe()
        {
            return "MilkCollected(ml=" + Millilitres + ", amount=" + Amount + ", homeMl=" + HomeMillilitres + ", homeAmount=" + HomeAmount + ")";
        }
    }

    public sealed class AnimalDry : SimEvent
    {
        public long AnimalId { get; }

        public AnimalDry(long simTime, long animalId) : base(simTime) { AnimalId = animalId; }

        protected override string Describe() { return "AnimalDry(animal=" + AnimalId + ")"; }
    }

    /// <summary>She calved; the calf was sold at the fair (spec 012 §3.6).</summary>
    public sealed class CalfBorn : SimEvent
    {
        public long AnimalId { get; }
        public long Amount { get; }

        public CalfBorn(long simTime, long animalId, long amount) : base(simTime)
        {
            AnimalId = animalId;
            Amount = amount;
        }

        protected override string Describe() { return "CalfBorn(animal=" + AnimalId + ", amount=" + Amount + ")"; }
    }

    /// <summary>Straw or feed will last less than <c>lowDays</c> game days (spec 012 §3.7); once until refilled.</summary>
    public sealed class FeedLow : SimEvent
    {
        public string Good { get; }
        public long DaysLeft { get; }

        public FeedLow(long simTime, string good, long daysLeft) : base(simTime)
        {
            Good = good;
            DaysLeft = daysLeft;
        }

        protected override string Describe() { return "FeedLow(good=" + Good + ", daysLeft=" + DaysLeft + ")"; }
    }

    /// <summary>The morning's feeding (spec 012 §3.7): grams of green from each source, straw and feed given, and what it cost.</summary>
    public sealed class AnimalsFed : SimEvent
    {
        public long GreenNeed { get; }
        public long GreenGiven { get; }
        public long GreenBoughtAmount { get; }
        public long GreenSold { get; }
        public long GreenSoldAmount { get; }
        public long StrawGiven { get; }
        public long FeedGiven { get; }
        public Sim.Core.Maths.Fixed FedScore { get; }

        public AnimalsFed(long simTime, long greenNeed, long greenGiven, long greenBoughtAmount, long greenSold, long greenSoldAmount, long strawGiven,
            long feedGiven, Sim.Core.Maths.Fixed fedScore) : base(simTime)
        {
            GreenNeed = greenNeed;
            GreenGiven = greenGiven;
            GreenBoughtAmount = greenBoughtAmount;
            GreenSold = greenSold;
            GreenSoldAmount = greenSoldAmount;
            StrawGiven = strawGiven;
            FeedGiven = feedGiven;
            FedScore = fedScore;
        }

        protected override string Describe()
        {
            return "AnimalsFed(green=" + GreenGiven + "/" + GreenNeed + ", bought=" + GreenBoughtAmount + ", sold=" + GreenSold + ", straw=" + StrawGiven
                + ", feed=" + FeedGiven + ", fed=" + FedScore + ")";
        }
    }

    /// <summary>A fodder field's last cut season is over and the field is free (spec 012 §3.8b).</summary>
    public sealed class FodderEnded : SimEvent
    {
        public int TileId { get; }
        public string CropId { get; }

        public FodderEnded(long simTime, int tileId, string cropId) : base(simTime)
        {
            TileId = tileId;
            CropId = cropId;
        }

        protected override string Describe() { return "FodderEnded(tile=" + TileId + ", crop=" + CropId + ")"; }
    }

    /// <summary>No cash for the season's vaccination: she goes unvaccinated this season (spec 012 §3.10).</summary>
    public sealed class VaccinationMissed : SimEvent
    {
        public long AnimalId { get; }

        public VaccinationMissed(long simTime, long animalId) : base(simTime) { AnimalId = animalId; }

        protected override string Describe() { return "VaccinationMissed(animal=" + AnimalId + ")"; }
    }

    /// <summary>She fell ill; the vet came (and cost) or, with no cash, didn't (spec 012 §3.10).</summary>
    public sealed class AnimalSick : SimEvent
    {
        public long AnimalId { get; }
        public long Cost { get; }
        public bool VetCame { get; }

        public AnimalSick(long simTime, long animalId, long cost, bool vetCame) : base(simTime)
        {
            AnimalId = animalId;
            Cost = cost;
            VetCame = vetCame;
        }

        protected override string Describe() { return "AnimalSick(animal=" + AnimalId + ", cost=" + Cost + ", vet=" + VetCame + ")"; }
    }
}
