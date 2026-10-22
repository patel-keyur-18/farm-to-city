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
}
