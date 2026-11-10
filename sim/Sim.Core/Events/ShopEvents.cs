namespace Sim.Core.Events
{
    /// <summary>A shop's hour (spec 017 §5): the customers who chose it, those it served (cups or grams) and what they paid.</summary>
    public sealed class ShopSales : SimEvent
    {
        public long BusinessId { get; }
        public long Customers { get; }
        public long Served { get; }
        public long Amount { get; }

        public ShopSales(long simTime, long businessId, long customers, long served, long amount) : base(simTime)
        {
            BusinessId = businessId;
            Customers = customers;
            Served = served;
            Amount = amount;
        }

        protected override string Describe() { return "ShopSales(id=" + BusinessId + ", customers=" + Customers + ", served=" + Served + ", amount=" + Amount + ")"; }
    }

    /// <summary>A newcomer set up (spec 017 §3.7): its kind, its pitch (−1: the cart by the square), and whether a gap drew it (else your prices).</summary>
    public sealed class RivalOpened : SimEvent
    {
        public Definitions.ShopKind Kind { get; }
        public int Pitch { get; }
        public bool Gap { get; }

        public RivalOpened(long simTime, Definitions.ShopKind kind, int pitch, bool gap) : base(simTime)
        {
            Kind = kind;
            Pitch = pitch;
            Gap = gap;
        }

        protected override string Describe() { return "RivalOpened(" + Kind + ", pitch=" + Pitch + ", gap=" + Gap + ")"; }
    }

    /// <summary>A newcomer packed up after too few customers (spec 017 §3.7).</summary>
    public sealed class RivalClosed : SimEvent
    {
        public Definitions.ShopKind Kind { get; }
        public int Pitch { get; }

        public RivalClosed(long simTime, Definitions.ShopKind kind, int pitch) : base(simTime)
        {
            Kind = kind;
            Pitch = pitch;
        }

        protected override string Describe() { return "RivalClosed(" + Kind + ", pitch=" + Pitch + ")"; }
    }

    /// <summary>The village grocer or tea stall changed its price (ADR 0095 §3): a cut, or back up a step when its customers return.</summary>
    public sealed class RivalPriceCut : SimEvent
    {
        public string RivalId { get; }
        public Definitions.ShopKind Kind { get; }
        public Maths.Fixed PriceFactor { get; }
        public bool Raised { get; }

        public RivalPriceCut(long simTime, string rivalId, Definitions.ShopKind kind, Maths.Fixed priceFactor, bool raised) : base(simTime)
        {
            RivalId = rivalId;
            Kind = kind;
            PriceFactor = priceFactor;
            Raised = raised;
        }

        protected override string Describe() { return "RivalPriceCut(" + RivalId + ", " + PriceFactor + (Raised ? ", back up" : "") + ")"; }
    }
}
