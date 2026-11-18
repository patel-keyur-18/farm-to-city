namespace Sim.Core.Events
{
    /// <summary>A parcel was bought (spec 002 §5).</summary>
    public sealed class ParcelBought : SimEvent
    {
        public string ParcelId { get; }
        public long Price { get; }
        public int Acres { get; }

        public ParcelBought(long simTime, string parcelId, long price, int acres) : base(simTime)
        {
            ParcelId = parcelId;
            Price = price;
            Acres = acres;
        }

        protected override string Describe() { return "ParcelBought(parcel=" + ParcelId + ", price=" + Price + ", acres=" + Acres + ")"; }
    }

    /// <summary>A bought parcel was sold quickly to a neighbour (spec 002 §5).</summary>
    public sealed class ParcelSold : SimEvent
    {
        public string ParcelId { get; }
        public long Price { get; }
        public long CostBasis { get; }

        public ParcelSold(long simTime, string parcelId, long price, long costBasis) : base(simTime)
        {
            ParcelId = parcelId;
            Price = price;
            CostBasis = costBasis;
        }

        protected override string Describe() { return "ParcelSold(parcel=" + ParcelId + ", price=" + Price + ", costBasis=" + CostBasis + ")"; }
    }

    /// <summary>A parcel was listed for a slow sale (spec 024 §3.4): it sells at the price fixed now when its days are up.</summary>
    public sealed class ParcelListed : SimEvent
    {
        public string ParcelId { get; }
        public long Price { get; }
        public long EndsAt { get; }

        public ParcelListed(long simTime, string parcelId, long price, long endsAt) : base(simTime) { ParcelId = parcelId; Price = price; EndsAt = endsAt; }

        protected override string Describe() { return "ParcelListed(parcel=" + ParcelId + ", price=" + Price + ", endsAt=" + EndsAt + ")"; }
    }

    /// <summary>A listing was taken off the market before it sold.</summary>
    public sealed class SaleCancelled : SimEvent
    {
        public string ParcelId { get; }

        public SaleCancelled(long simTime, string parcelId) : base(simTime) { ParcelId = parcelId; }

        protected override string Describe() { return "SaleCancelled(parcel=" + ParcelId + ")"; }
    }

    /// <summary>A slow sale's days came and the acre could not be sold (a loan now secured on it, or selling it would strand land): the listing is off, nothing was sold.</summary>
    public sealed class SaleFailed : SimEvent
    {
        public string ParcelId { get; }
        public string Reason { get; }

        public SaleFailed(long simTime, string parcelId, string reason) : base(simTime) { ParcelId = parcelId; Reason = reason; }

        protected override string Describe() { return "SaleFailed(parcel=" + ParcelId + ", reason=" + Reason + ")"; }
    }

    /// <summary>
    /// The season-close reconciliation failed (spec 005 §3.1): a bug. With <c>SimOptions.StrictChecks</c> the sim
    /// throws instead; player builds log this event.
    /// </summary>
    public sealed class LedgerReconciliationFailed : SimEvent
    {
        public string Reason { get; }

        public LedgerReconciliationFailed(long simTime, string reason) : base(simTime) { Reason = reason; }

        protected override string Describe() { return "LedgerReconciliationFailed(" + Reason + ")"; }
    }
}

namespace Sim.Core.Events
{
    /// <summary>Every season close, after settlement (spec 005 §5).</summary>
    public sealed class SeasonClosed : SimEvent
    {
        public long Period { get; }
        public Finance.SeasonReport Report { get; }

        public SeasonClosed(long simTime, long period, Finance.SeasonReport report) : base(simTime)
        {
            Period = period;
            Report = report;
        }

        protected override string Describe()
        {
            return "SeasonClosed(period=" + Period + ", farmProfit=" + Report.FarmProfit + ", net=" + Report.NetResult
                + ", netWorth=" + Report.NetWorth.Total + ")";
        }
    }

    /// <summary>
    /// At each season close, a field of yours whose soil fell this season or that sat dry under a crop (spec 018 §3.4: what the farm
    /// advisor notices): the season's net fertility change, its fertility now, and the hours a crop on it was below its moisture minimum.
    /// </summary>
    public sealed class FieldSeasonClosed : SimEvent
    {
        public int TileId { get; }
        public Maths.Fixed FertilityChange { get; }
        public Maths.Fixed Fertility { get; }
        public long DryHours { get; }

        public FieldSeasonClosed(long simTime, int tileId, Maths.Fixed fertilityChange, Maths.Fixed fertility, long dryHours) : base(simTime)
        {
            TileId = tileId;
            FertilityChange = fertilityChange;
            Fertility = fertility;
            DryHours = dryHours;
        }

        protected override string Describe() { return "FieldSeasonClosed(tile=" + TileId + ", change=" + FertilityChange + ", dry=" + DryHours + "h)"; }
    }
}
