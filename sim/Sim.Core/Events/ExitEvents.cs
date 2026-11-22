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

    /// <summary>An investor, a partner or a rival made an offer for a business (spec 028 §3.4); <c>Kind</c> is the <c>DealKind</c>.</summary>
    public sealed class DealOffered : SimEvent
    {
        public long OfferId { get; }
        public long BusinessId { get; }
        public int Kind { get; }

        public DealOffered(long simTime, long offerId, long businessId, int kind) : base(simTime) { OfferId = offerId; BusinessId = businessId; Kind = kind; }

        protected override string Describe() { return "DealOffered(offer=" + OfferId + ", business=" + BusinessId + ", kind=" + Kind + ")"; }
    }

    /// <summary>An offer was taken, declined or lapsed (spec 028 §3.4).</summary>
    public sealed class DealAnswered : SimEvent
    {
        public long OfferId { get; }
        public long BusinessId { get; }
        public int Kind { get; }
        public bool Accepted { get; }
        public bool Lapsed { get; }

        public DealAnswered(long simTime, long offerId, long businessId, int kind, bool accepted, bool lapsed) : base(simTime)
        {
            OfferId = offerId; BusinessId = businessId; Kind = kind; Accepted = accepted; Lapsed = lapsed;
        }

        protected override string Describe() { return "DealAnswered(offer=" + OfferId + ", business=" + BusinessId + ", kind=" + Kind + (Accepted ? ", accepted" : Lapsed ? ", lapsed" : ", declined") + ")"; }
    }

    /// <summary>An outside owner's share was bought back (spec 028 §3.4); <c>Kind</c> is the <c>StakeKind</c>.</summary>
    public sealed class ShareBoughtBack : SimEvent
    {
        public long BusinessId { get; }
        public int Kind { get; }
        public long Price { get; }

        public ShareBoughtBack(long simTime, long businessId, int kind, long price) : base(simTime) { BusinessId = businessId; Kind = kind; Price = price; }

        protected override string Describe() { return "ShareBoughtBack(business=" + BusinessId + ", kind=" + Kind + ", price=" + Price + ")"; }
    }
}
