namespace Sim.Core.Events
{
    /// <summary>An acre planted as an orchard (spec 013 §3.3); <see cref="Cost"/> is its saplings with planting.</summary>
    public sealed class OrchardPlanted : SimEvent
    {
        public int TileId { get; }
        public string Tree { get; }
        public int Trees { get; }
        public long Cost { get; }

        public OrchardPlanted(long simTime, int tileId, string tree, int trees, long cost) : base(simTime) { TileId = tileId; Tree = tree; Trees = trees; Cost = cost; }

        protected override string Describe() { return "OrchardPlanted(tile=" + TileId + ", tree=" + Tree + ", trees=" + Trees + ", cost=" + Cost + ")"; }
    }

    /// <summary>An orchard's trees are grown: they bear in their seasons and shade the fields beside them from now.</summary>
    public sealed class OrchardBearing : SimEvent
    {
        public int TileId { get; }
        public string Tree { get; }

        public OrchardBearing(long simTime, int tileId, string tree) : base(simTime) { TileId = tileId; Tree = tree; }

        protected override string Describe() { return "OrchardBearing(tile=" + TileId + ", tree=" + Tree + ")"; }
    }

    /// <summary>An orchard's batch is ripe and waits to be picked.</summary>
    public sealed class OrchardRipe : SimEvent
    {
        public int TileId { get; }
        public string Tree { get; }
        public long Grams { get; }

        public OrchardRipe(long simTime, int tileId, string tree, long grams) : base(simTime) { TileId = tileId; Tree = tree; Grams = grams; }

        protected override string Describe() { return "OrchardRipe(tile=" + TileId + ", tree=" + Tree + ", grams=" + Grams + ")"; }
    }

    /// <summary>An orchard's batch picked into the store (the rest sold to the trader if asked), worth <see cref="Value"/> at the trader's price.</summary>
    public sealed class OrchardPicked : SimEvent
    {
        public int TileId { get; }
        public string Tree { get; }
        public long Grams { get; }
        public long Value { get; }

        public OrchardPicked(long simTime, int tileId, string tree, long grams, long value) : base(simTime) { TileId = tileId; Tree = tree; Grams = grams; Value = value; }

        protected override string Describe() { return "OrchardPicked(tile=" + TileId + ", tree=" + Tree + ", grams=" + Grams + ", value=" + Value + ")"; }
    }

    /// <summary>A fruit contractor bought an orchard's ripe batch on the trees (ADR 0090 §4).</summary>
    public sealed class OrchardSoldOnTrees : SimEvent
    {
        public int TileId { get; }
        public string Tree { get; }
        public long Grams { get; }
        public long Amount { get; }

        public OrchardSoldOnTrees(long simTime, int tileId, string tree, long grams, long amount) : base(simTime) { TileId = tileId; Tree = tree; Grams = grams; Amount = amount; }

        protected override string Describe() { return "OrchardSoldOnTrees(tile=" + TileId + ", tree=" + Tree + ", grams=" + Grams + ", amount=" + Amount + ")"; }
    }

    /// <summary>An orchard gone: spent after its last harvest (banana, papaya), cleared, or gone with its land (<see cref="Amount"/> its book value then).</summary>
    public sealed class OrchardGone : SimEvent
    {
        public int TileId { get; }
        public string Tree { get; }
        public OrchardEnd End { get; }
        public long Amount { get; }

        public OrchardGone(long simTime, int tileId, string tree, OrchardEnd end, long amount) : base(simTime) { TileId = tileId; Tree = tree; End = end; Amount = amount; }

        protected override string Describe() { return "OrchardGone(tile=" + TileId + ", tree=" + Tree + ", end=" + End + ", amount=" + Amount + ")"; }
    }

    public enum OrchardEnd
    {
        Spent = 0,
        Cleared = 1,
        WithTheLand = 2,
    }
}
