using Sim.Core.People;

namespace Sim.Core.Events
{
    /// <summary>A family house was started (spec 014 §3.4).</summary>
    public sealed class FamilyHouseStarted : SimEvent
    {
        public long ConstructionId { get; }
        public int TileId { get; }
        public long Cost { get; }
        public long ReadyAt { get; }

        public FamilyHouseStarted(long simTime, long constructionId, int tileId, long cost, long readyAt) : base(simTime)
        {
            ConstructionId = constructionId;
            TileId = tileId;
            Cost = cost;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "FamilyHouseStarted(id=" + ConstructionId + ", tile=" + TileId + ", cost=" + Cost + ")"; }
    }

    /// <summary>A family house is finished (spec 014 §3.4).</summary>
    public sealed class FamilyHouseBuilt : SimEvent
    {
        public long BuildingId { get; }
        public int TileId { get; }

        public FamilyHouseBuilt(long simTime, long buildingId, int tileId) : base(simTime)
        {
            BuildingId = buildingId;
            TileId = tileId;
        }

        protected override string Describe() { return "FamilyHouseBuilt(building=" + BuildingId + ", tile=" + TileId + ")"; }
    }

    /// <summary>A household paid the season's rent (spec 014 §3.4), Cr 4200.</summary>
    public sealed class RentPaid : SimEvent
    {
        public long HouseholdId { get; }
        public long Amount { get; }

        public RentPaid(long simTime, long householdId, long amount) : base(simTime)
        {
            HouseholdId = householdId;
            Amount = amount;
        }

        protected override string Describe() { return "RentPaid(household=" + HouseholdId + ", amount=" + Amount + ")"; }
    }

    /// <summary>A household couldn't pay the season's rent and is <see cref="Behind"/> seasons behind (spec 014 §3.4).</summary>
    public sealed class RentMissed : SimEvent
    {
        public long HouseholdId { get; }
        public long Amount { get; }
        public int Behind { get; }

        public RentMissed(long simTime, long householdId, long amount, int behind) : base(simTime)
        {
            HouseholdId = householdId;
            Amount = amount;
            Behind = behind;
        }

        protected override string Describe() { return "RentMissed(household=" + HouseholdId + ", amount=" + Amount + ", behind=" + Behind + ")"; }
    }

    /// <summary>A household left the town (spec 014 §3.10, §3.13).</summary>
    public sealed class HouseholdLeft : SimEvent
    {
        public long HouseholdId { get; }
        public int People { get; }
        public LeaveReason Reason { get; }

        /// <summary>Its first member's name (index into the names): the household is gone from the town when the event is read.</summary>
        public int Name { get; }

        public HouseholdLeft(long simTime, long householdId, int people, LeaveReason reason, int name) : base(simTime)
        {
            HouseholdId = householdId;
            Name = name;
            People = people;
            Reason = reason;
        }

        protected override string Describe() { return "HouseholdLeft(household=" + HouseholdId + ", people=" + People + ", " + Reason + ")"; }
    }

    /// <summary>A baby was born to a household (spec 014 §3.9).</summary>
    public sealed class CitizenBorn : SimEvent
    {
        public long CitizenId { get; }
        public int Name { get; }
        public bool Female { get; }
        public long HouseholdId { get; }
        public long MotherId { get; }

        public CitizenBorn(long simTime, long citizenId, int name, bool female, long householdId, long motherId) : base(simTime)
        {
            CitizenId = citizenId;
            Name = name;
            Female = female;
            HouseholdId = householdId;
            MotherId = motherId;
        }

        protected override string Describe() { return "CitizenBorn(citizen=" + CitizenId + ", household=" + HouseholdId + ")"; }
    }

    /// <summary>
    /// A citizen married (spec 014 §3.9, ADR 0070): a son's bride moved in (<see cref="SpouseId"/>; into a free house when
    /// <see cref="HomeHouseholdId"/> differs from <see cref="HouseholdId"/>), or a daughter moved to her husband's village.
    /// </summary>
    public sealed class CitizenMarried : SimEvent
    {
        public long CitizenId { get; }

        /// <summary>Index into the names: a daughter who married away may be gone from the town when the event is read.</summary>
        public int Name { get; }
        public bool MovedAway { get; }
        public long SpouseId { get; }

        /// <summary>The bride's home village, or the one the daughter moved to.</summary>
        public int Village { get; }

        /// <summary>The parents' household, and the household the couple lives in.</summary>
        public long HouseholdId { get; }
        public long HomeHouseholdId { get; }

        public CitizenMarried(long simTime, long citizenId, int name, bool movedAway, long spouseId, int village, long householdId, long homeHouseholdId) : base(simTime)
        {
            CitizenId = citizenId;
            Name = name;
            MovedAway = movedAway;
            SpouseId = spouseId;
            Village = village;
            HouseholdId = householdId;
            HomeHouseholdId = homeHouseholdId;
        }

        protected override string Describe()
        {
            return "CitizenMarried(citizen=" + CitizenId + ", away=" + MovedAway + ", spouse=" + SpouseId + ", home=" + HomeHouseholdId + ")";
        }
    }

    /// <summary>An elder passed away peacefully (spec 014 §3.9); <see cref="HouseEmpty"/> when they lived alone.</summary>
    public sealed class CitizenDied : SimEvent
    {
        public long CitizenId { get; }

        /// <summary>Index into the names: they may be gone from the record when the event is read.</summary>
        public int Name { get; }
        public bool Female { get; }
        public int Age { get; }
        public long HouseholdId { get; }
        public bool HouseEmpty { get; }

        public CitizenDied(long simTime, long citizenId, int name, bool female, int age, long householdId, bool houseEmpty) : base(simTime)
        {
            CitizenId = citizenId;
            Name = name;
            Female = female;
            Age = age;
            HouseholdId = householdId;
            HouseEmpty = houseEmpty;
        }

        protected override string Describe() { return "CitizenDied(citizen=" + CitizenId + ", age=" + Age + ", household=" + HouseholdId + ")"; }
    }

    /// <summary>A free family house is offered to a hand living alone (spec 014 §3.13): a card, Yes / Not now.</summary>
    public sealed class FamilyRequest : SimEvent
    {
        public long HandId { get; }
        public long HouseId { get; }

        public FamilyRequest(long simTime, long handId, long houseId) : base(simTime)
        {
            HandId = handId;
            HouseId = houseId;
        }

        protected override string Describe() { return "FamilyRequest(hand=" + HandId + ", house=" + HouseId + ")"; }
    }

    /// <summary>A family moved into a family house (spec 014 §3.10): strangers, or a hand's own family (<see cref="HandId"/> not 0).</summary>
    public sealed class FamilyMovedIn : SimEvent
    {
        public long HouseholdId { get; }
        public long HouseId { get; }
        public int People { get; }

        /// <summary>The man's name, or the hand's (index into the names).</summary>
        public int Name { get; }
        public long HandId { get; }

        public FamilyMovedIn(long simTime, long householdId, long houseId, int people, int name, long handId) : base(simTime)
        {
            HouseholdId = householdId;
            HouseId = houseId;
            People = people;
            Name = name;
            HandId = handId;
        }

        protected override string Describe() { return "FamilyMovedIn(household=" + HouseholdId + ", house=" + HouseId + ", people=" + People + ", hand=" + HandId + ")"; }
    }

    /// <summary>A business was founded and is being built (spec 016 §3.3).</summary>
    public sealed class BusinessFounded : SimEvent
    {
        public long BusinessId { get; }
        public string TypeId { get; }
        public int TileId { get; }
        public long Capital { get; }

        public BusinessFounded(long simTime, long businessId, string typeId, int tileId, long capital) : base(simTime)
        {
            BusinessId = businessId;
            TypeId = typeId;
            TileId = tileId;
            Capital = capital;
        }

        protected override string Describe() { return "BusinessFounded(id=" + BusinessId + ", " + TypeId + ", tile=" + TileId + ", capital=" + Capital + ")"; }
    }

    /// <summary>A business is built and open, with its worker (spec 016 §3.3, §3.6).</summary>
    public sealed class BusinessOpened : SimEvent
    {
        public long BusinessId { get; }
        public Business.WorkerKind Worker { get; }
        public long CitizenId { get; }

        public BusinessOpened(long simTime, long businessId, Business.WorkerKind worker, long citizenId) : base(simTime)
        {
            BusinessId = businessId;
            Worker = worker;
            CitizenId = citizenId;
        }

        protected override string Describe() { return "BusinessOpened(id=" + BusinessId + ", " + Worker + ", citizen=" + CitizenId + ")"; }
    }

    /// <summary>An hour's work at a business (spec 016 §3.4): the area's grain ground and the founder's own input, grams.</summary>
    public sealed class BusinessWorked : SimEvent
    {
        public long BusinessId { get; }
        public long AreaGrams { get; }
        public long OwnGrams { get; }

        public BusinessWorked(long simTime, long businessId, long areaGrams, long ownGrams) : base(simTime)
        {
            BusinessId = businessId;
            AreaGrams = areaGrams;
            OwnGrams = ownGrams;
        }

        protected override string Describe() { return "BusinessWorked(id=" + BusinessId + ", area=" + AreaGrams + ", own=" + OwnGrams + ")"; }
    }
}
