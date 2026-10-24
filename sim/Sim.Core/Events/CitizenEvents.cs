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

        public HouseholdLeft(long simTime, long householdId, int people, LeaveReason reason) : base(simTime)
        {
            HouseholdId = householdId;
            People = people;
            Reason = reason;
        }

        protected override string Describe() { return "HouseholdLeft(household=" + HouseholdId + ", people=" + People + ", " + Reason + ")"; }
    }
}
