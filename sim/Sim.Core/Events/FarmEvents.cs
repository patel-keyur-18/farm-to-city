using Sim.Core.Farm;

namespace Sim.Core.Events
{
    /// <summary>A crop was sown (spec 003 §5).</summary>
    public sealed class CropSown : SimEvent
    {
        public int TileId { get; }
        public string CropId { get; }
        public long ReadyAt { get; }
        public long Cost { get; }

        public CropSown(long simTime, int tileId, string cropId, long readyAt, long cost) : base(simTime)
        {
            TileId = tileId;
            CropId = cropId;
            ReadyAt = readyAt;
            Cost = cost;
        }

        protected override string Describe() { return "CropSown(tile=" + TileId + ", crop=" + CropId + ", readyAt=" + ReadyAt + ", cost=" + Cost + ")"; }
    }

    /// <summary>A crop's growth ended; its yield is frozen and it waits (spec 003 §5).</summary>
    public sealed class CropReady : SimEvent
    {
        public int TileId { get; }
        public string CropId { get; }
        public long Grams { get; }

        public CropReady(long simTime, int tileId, string cropId, long grams) : base(simTime)
        {
            TileId = tileId;
            CropId = cropId;
            Grams = grams;
        }

        protected override string Describe() { return "CropReady(tile=" + TileId + ", crop=" + CropId + ", g=" + Grams + ")"; }
    }

    /// <summary>A ready crop was harvested into the store (spec 003 §5), with its Why? breakdown.</summary>
    public sealed class CropHarvested : SimEvent
    {
        public int TileId { get; }
        public string CropId { get; }
        public long Grams { get; }
        public YieldBreakdown Breakdown { get; }

        public CropHarvested(long simTime, int tileId, string cropId, long grams, YieldBreakdown breakdown) : base(simTime)
        {
            TileId = tileId;
            CropId = cropId;
            Grams = grams;
            Breakdown = breakdown;
        }

        protected override string Describe() { return "CropHarvested(tile=" + TileId + ", crop=" + CropId + ", g=" + Grams + ")"; }
    }

    /// <summary>A growing or ready crop was removed without harvest (spec 003 §5).</summary>
    public sealed class FieldCleared : SimEvent
    {
        public int TileId { get; }
        public string CropId { get; }

        public FieldCleared(long simTime, int tileId, string cropId) : base(simTime)
        {
            TileId = tileId;
            CropId = cropId;
        }

        protected override string Describe() { return "FieldCleared(tile=" + TileId + ", crop=" + CropId + ")"; }
    }

    /// <summary>A garden bed was picked (spec 003 §5).</summary>
    public sealed class GardenPicked : SimEvent
    {
        public long BedId { get; }
        public string GoodId { get; }
        public long Grams { get; }
        public long RemainingGrams { get; }

        public GardenPicked(long simTime, long bedId, string goodId, long grams, long remainingGrams) : base(simTime)
        {
            BedId = bedId;
            GoodId = goodId;
            Grams = grams;
            RemainingGrams = remainingGrams;
        }

        protected override string Describe() { return "GardenPicked(bed=" + BedId + ", good=" + GoodId + ", g=" + Grams + ", left=" + RemainingGrams + ")"; }
    }
}
