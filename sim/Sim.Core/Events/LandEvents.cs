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
}
