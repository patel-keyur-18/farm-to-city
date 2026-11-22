namespace Sim.Core.Events
{
    /// <summary>A business was listed for sale (spec 028 §3.3): when a buyer completes, and whether it is a rushed sale.</summary>
    public sealed class BusinessListed : SimEvent
    {
        public long BusinessId { get; }
        public long CompletesAt { get; }
        public bool Rushed { get; }

        public BusinessListed(long simTime, long businessId, long completesAt, bool rushed) : base(simTime)
        {
            BusinessId = businessId; CompletesAt = completesAt; Rushed = rushed;
        }

        protected override string Describe() { return "BusinessListed(business=" + BusinessId + ", completes=" + CompletesAt + (Rushed ? ", rushed" : "") + ")"; }
    }

    /// <summary>A listing was cancelled before a buyer completed (spec 028 §3.3).</summary>
    public sealed class BusinessSaleCancelled : SimEvent
    {
        public long BusinessId { get; }

        public BusinessSaleCancelled(long simTime, long businessId) : base(simTime) { BusinessId = businessId; }

        protected override string Describe() { return "BusinessSaleCancelled(business=" + BusinessId + ")"; }
    }

    /// <summary>A business was sold (spec 028 §3.3): the price, and what came to you after the tax and severance.</summary>
    public sealed class BusinessSold : SimEvent
    {
        public long BusinessId { get; }
        public long SaleId { get; }
        public long Price { get; }
        public long ToYou { get; }

        public BusinessSold(long simTime, long businessId, long saleId, long price, long toYou) : base(simTime)
        {
            BusinessId = businessId; SaleId = saleId; Price = price; ToYou = toYou;
        }

        protected override string Describe() { return "BusinessSold(business=" + BusinessId + ", price=" + Price + ", toYou=" + ToYou + ")"; }
    }
}
