namespace Sim.Core.Events
{
    /// <summary>The pump on the well is installed (spec 013 §3.1).</summary>
    public sealed class PumpInstalled : SimEvent
    {
        public long Cost { get; }

        public PumpInstalled(long simTime, long cost) : base(simTime) { Cost = cost; }

        protected override string Describe() { return "PumpInstalled(cost=" + Cost + ")"; }
    }

    /// <summary>A borewell is drilled on a tile (ADR 0086).</summary>
    public sealed class BorewellDrilled : SimEvent
    {
        public int TileId { get; }
        public long Cost { get; }

        public BorewellDrilled(long simTime, int tileId, long cost) : base(simTime) { TileId = tileId; Cost = cost; }

        protected override string Describe() { return "BorewellDrilled(tile=" + TileId + ", cost=" + Cost + ")"; }
    }

    /// <summary>A canal channel is dug on a tile (spec 013 §3.1).</summary>
    public sealed class ChannelBuilt : SimEvent
    {
        public int TileId { get; }
        public long Cost { get; }

        public ChannelBuilt(long simTime, int tileId, long cost) : base(simTime) { TileId = tileId; Cost = cost; }

        protected override string Describe() { return "ChannelBuilt(tile=" + TileId + ", cost=" + Cost + ")"; }
    }

    /// <summary>Drip is laid on a tile; <see cref="Subsidy"/> is what the government paid of its price (spec 013 §3.1).</summary>
    public sealed class DripLaid : SimEvent
    {
        public int TileId { get; }
        public long Cost { get; }
        public long Subsidy { get; }

        public DripLaid(long simTime, int tileId, long cost, long subsidy) : base(simTime) { TileId = tileId; Cost = cost; Subsidy = subsidy; }

        protected override string Describe() { return "DripLaid(tile=" + TileId + ", cost=" + Cost + ", subsidy=" + Subsidy + ")"; }
    }

    /// <summary>The acres a pumped source waters changed (1 the pump, 2 the borewell).</summary>
    public sealed class WaterAcresSet : SimEvent
    {
        public int Source { get; }
        public int Acres { get; }

        public WaterAcresSet(long simTime, int source, int acres) : base(simTime) { Source = source; Acres = acres; }

        protected override string Describe() { return "WaterAcresSet(source=" + Source + ", acres=" + Acres + ")"; }
    }
}
