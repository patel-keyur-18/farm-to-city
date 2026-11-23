namespace Sim.Core.Events
{
    /// <summary>A sale on terms made an invoice (spec 028 §3.1): who sold, who bought (a business, or a village shop by its id) and what it is worth.</summary>
    public sealed class InvoiceIssued : SimEvent
    {
        public long Seller { get; }
        public long Buyer { get; }
        public string BuyerRival { get; }
        public long Amount { get; }

        public InvoiceIssued(long simTime, long seller, long buyer, string buyerRival, long amount) : base(simTime)
        {
            Seller = seller; Buyer = buyer; BuyerRival = buyerRival; Amount = amount;
        }

        protected override string Describe() { return "InvoiceIssued(seller=" + Seller + ", buyer=" + Buyer + (BuyerRival.Length > 0 ? "/" + BuyerRival : "") + ", amount=" + Amount + ")"; }
    }

    /// <summary>An invoice was paid at its term's end, or at once when a business closed (spec 028 §3.1).</summary>
    public sealed class InvoicePaid : SimEvent
    {
        public long Seller { get; }
        public long Buyer { get; }
        public string BuyerRival { get; }
        public long Amount { get; }

        public InvoicePaid(long simTime, long seller, long buyer, string buyerRival, long amount) : base(simTime)
        {
            Seller = seller; Buyer = buyer; BuyerRival = buyerRival; Amount = amount;
        }

        protected override string Describe() { return "InvoicePaid(seller=" + Seller + ", buyer=" + Buyer + (BuyerRival.Length > 0 ? "/" + BuyerRival : "") + ", amount=" + Amount + ")"; }
    }

    /// <summary>An invoice went more than a term past due (spec 028 §3.1): overdue.</summary>
    public sealed class InvoiceOverdue : SimEvent
    {
        public long Seller { get; }
        public long Buyer { get; }
        public long Amount { get; }

        public InvoiceOverdue(long simTime, long seller, long buyer, long amount) : base(simTime)
        {
            Seller = seller; Buyer = buyer; Amount = amount;
        }

        protected override string Describe() { return "InvoiceOverdue(seller=" + Seller + ", buyer=" + Buyer + ", amount=" + Amount + ")"; }
    }

    /// <summary>A seller stopped delivering to a buyer with an overdue invoice (spec 028 §3.1's stop rule).</summary>
    public sealed class SupplyStopped : SimEvent
    {
        public long Seller { get; }
        public long Buyer { get; }

        public SupplyStopped(long simTime, long seller, long buyer) : base(simTime)
        {
            Seller = seller; Buyer = buyer;
        }

        protected override string Describe() { return "SupplyStopped(seller=" + Seller + ", buyer=" + Buyer + ")"; }
    }
}
