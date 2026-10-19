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

    /// <summary>The founder is sick today: no labour (spec 003 §5).</summary>
    public sealed class FounderSick : SimEvent
    {
        public long Day { get; }

        public FounderSick(long simTime, long day) : base(simTime) { Day = day; }

        protected override string Describe() { return "FounderSick(day=" + Day + ")"; }
    }

    /// <summary>The pest check found an outbreak; its damage applies at readiness unless sprayed (spec 003 §5).</summary>
    public sealed class PestWarning : SimEvent
    {
        public int TileId { get; }
        public Outbreak Severity { get; }
        public long ReadyAt { get; }

        public PestWarning(long simTime, int tileId, Outbreak severity, long readyAt) : base(simTime)
        {
            TileId = tileId;
            Severity = severity;
            ReadyAt = readyAt;
        }

        protected override string Describe() { return "PestWarning(tile=" + TileId + ", " + Severity + ", readyAt=" + ReadyAt + ")"; }
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

    /// <summary>A garden bed's crop is ready to pick (spec 003 §3.13); notifications read it (spec 007 §3.5).</summary>
    public sealed class BedReady : SimEvent
    {
        public long BedId { get; }
        public string GardenCropId { get; }

        public BedReady(long simTime, long bedId, string gardenCropId) : base(simTime)
        {
            BedId = bedId;
            GardenCropId = gardenCropId;
        }

        protected override string Describe() { return "BedReady(bed=" + BedId + ", crop=" + GardenCropId + ")"; }
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

namespace Sim.Core.Events
{
    /// <summary>Base for the tree events of spec 003 §5: a slot, a tree kind and an amount (grams or money).</summary>
    public abstract class TreeEvent : SimEvent
    {
        public long SlotId { get; }
        public string TreeId { get; }

        protected TreeEvent(long simTime, long slotId, string treeId) : base(simTime)
        {
            SlotId = slotId;
            TreeId = treeId;
        }
    }

    public sealed class TreePlanted : TreeEvent
    {
        public long Cost { get; }
        public TreePlanted(long t, long slotId, string treeId, long cost) : base(t, slotId, treeId) { Cost = cost; }
        protected override string Describe() { return "TreePlanted(slot=" + SlotId + ", tree=" + TreeId + ", cost=" + Cost + ")"; }
    }

    public sealed class TreeMatured : TreeEvent
    {
        public TreeMatured(long t, long slotId, string treeId) : base(t, slotId, treeId) { }
        protected override string Describe() { return "TreeMatured(slot=" + SlotId + ", tree=" + TreeId + ")"; }
    }

    public sealed class FruitRipe : TreeEvent
    {
        public long Grams { get; }
        public FruitRipe(long t, long slotId, string treeId, long grams) : base(t, slotId, treeId) { Grams = grams; }
        protected override string Describe() { return "FruitRipe(slot=" + SlotId + ", tree=" + TreeId + ", g=" + Grams + ")"; }
    }

    public sealed class FruitPicked : TreeEvent
    {
        public long Grams { get; }
        public long Value { get; }
        public FruitPicked(long t, long slotId, string treeId, long grams, long value) : base(t, slotId, treeId) { Grams = grams; Value = value; }
        protected override string Describe() { return "FruitPicked(slot=" + SlotId + ", tree=" + TreeId + ", g=" + Grams + ", value=" + Value + ")"; }
    }

    public sealed class TreeSpent : TreeEvent
    {
        public long BookValue { get; }
        public TreeSpent(long t, long slotId, string treeId, long bookValue) : base(t, slotId, treeId) { BookValue = bookValue; }
        protected override string Describe() { return "TreeSpent(slot=" + SlotId + ", tree=" + TreeId + ", bookValue=" + BookValue + ")"; }
    }

    public sealed class TreeRemoved : TreeEvent
    {
        public long BookValue { get; }
        public TreeRemoved(long t, long slotId, string treeId, long bookValue) : base(t, slotId, treeId) { BookValue = bookValue; }
        protected override string Describe() { return "TreeRemoved(slot=" + SlotId + ", tree=" + TreeId + ", bookValue=" + BookValue + ")"; }
    }
}

namespace Sim.Core.Events
{
    /// <summary>Day labour paid at the daily pass for the hours since the last (spec 003 §5).</summary>
    public sealed class DayLabourPaid : SimEvent
    {
        public Maths.Fixed Hours { get; }
        public long Amount { get; }

        public DayLabourPaid(long simTime, Maths.Fixed hours, long amount) : base(simTime)
        {
            Hours = hours;
            Amount = amount;
        }

        protected override string Describe() { return "DayLabourPaid(hours=" + Hours + ", amount=" + Amount + ")"; }
    }

    /// <summary>The diet plan changed, by the player or automatically (subsistence and back) (spec 003 §5).</summary>
    public sealed class DietPlanChanged : SimEvent
    {
        public string Old { get; }
        public string New { get; }
        public bool Automatic { get; }

        public DietPlanChanged(long simTime, string old, string @new, bool automatic) : base(simTime)
        {
            Old = old;
            New = @new;
            Automatic = automatic;
        }

        protected override string Describe() { return "DietPlanChanged(" + Old + " -> " + New + (Automatic ? ", automatic" : "") + ")"; }
    }

    /// <summary>The household ate from the store at a daily pass: home use, and the garden's part of it (ADR 0032).</summary>
    public sealed class HomeUseEaten : SimEvent
    {
        public long Value { get; }
        public long GardenValue { get; }

        public HomeUseEaten(long simTime, long value, long gardenValue) : base(simTime)
        {
            Value = value;
            GardenValue = gardenValue;
        }

        protected override string Describe() { return "HomeUseEaten(value=" + Value + ", garden=" + GardenValue + ")"; }
    }

    /// <summary>A season obligation paid at settlement (spec 005 §5). Kind: Living, Contract or Rent.</summary>
    public sealed class ObligationPaid : SimEvent
    {
        public string Kind { get; }
        public string OwnerRef { get; }
        public long Amount { get; }

        public ObligationPaid(long simTime, string kind, string ownerRef, long amount) : base(simTime)
        {
            Kind = kind;
            OwnerRef = ownerRef;
            Amount = amount;
        }

        protected override string Describe() { return "ObligationPaid(" + Kind + ", " + OwnerRef + ", " + Amount + ")"; }
    }

    /// <summary>A season obligation that couldn't be paid (spec 005 §5): living costs drop to subsistence, never debt.</summary>
    public sealed class ObligationUnpaid : SimEvent
    {
        public string Kind { get; }
        public string OwnerRef { get; }
        public long Amount { get; }

        public ObligationUnpaid(long simTime, string kind, string ownerRef, long amount) : base(simTime)
        {
            Kind = kind;
            OwnerRef = ownerRef;
            Amount = amount;
        }

        protected override string Describe() { return "ObligationUnpaid(" + Kind + ", " + OwnerRef + ", " + Amount + ")"; }
    }
}
