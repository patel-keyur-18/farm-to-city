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

    /// <summary>You zoned a parcel (spec 024 §3.3): <see cref="Town.Zone"/> as an int; 0 is farming again.</summary>
    public sealed class AcreZoned : SimEvent
    {
        public string ParcelId { get; }
        public int Zone { get; }

        public AcreZoned(long simTime, string parcelId, int zone) : base(simTime) { ParcelId = parcelId; Zone = zone; }

        protected override string Describe() { return "AcreZoned(parcel=" + ParcelId + ", zone=" + Zone + ")"; }
    }

    /// <summary>Builders bought a parcel (spec 024 §3.3): what you received if it was yours, else 0 (a neighbour's).</summary>
    public sealed class AcreSoldToBuilder : SimEvent
    {
        public string ParcelId { get; }
        public long Price { get; }
        public bool Yours { get; }

        public AcreSoldToBuilder(long simTime, string parcelId, long price, bool yours) : base(simTime) { ParcelId = parcelId; Price = price; Yours = yours; }

        protected override string Describe() { return "AcreSoldToBuilder(parcel=" + ParcelId + ", price=" + Price + ", yours=" + Yours + ")"; }
    }

    /// <summary>The builders' homes stand on a parcel they bought.</summary>
    public sealed class HomesBuilt : SimEvent
    {
        public string ParcelId { get; }
        public int Homes { get; }

        public HomesBuilt(long simTime, string parcelId, int homes) : base(simTime) { ParcelId = parcelId; Homes = homes; }

        protected override string Describe() { return "HomesBuilt(parcel=" + ParcelId + ", homes=" + Homes + ")"; }
    }

    /// <summary>You leased a parcel out: it is locked now and the first rent comes at the next close.</summary>
    public sealed class LeaseOut : SimEvent
    {
        public string ParcelId { get; }
        public long Rent { get; }

        public LeaseOut(long simTime, string parcelId, long rent) : base(simTime) { ParcelId = parcelId; Rent = rent; }

        protected override string Describe() { return "LeaseOut(parcel=" + ParcelId + ", rent=" + Rent + ")"; }
    }

    /// <summary>A lease ended (or was cancelled before it began): the parcel is yours to use again.</summary>
    public sealed class LeaseEnded : SimEvent
    {
        public string ParcelId { get; }

        public LeaseEnded(long simTime, string parcelId) : base(simTime) { ParcelId = parcelId; }

        protected override string Describe() { return "LeaseEnded(parcel=" + ParcelId + ")"; }
    }

    /// <summary>The rent of a lease reached your cash, at a close.</summary>
    public sealed class LandRentPaid : SimEvent
    {
        public string ParcelId { get; }
        public long Amount { get; }

        public LandRentPaid(long simTime, string parcelId, long amount) : base(simTime) { ParcelId = parcelId; Amount = amount; }

        protected override string Describe() { return "LandRentPaid(parcel=" + ParcelId + ", amount=" + Amount + ")"; }
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
