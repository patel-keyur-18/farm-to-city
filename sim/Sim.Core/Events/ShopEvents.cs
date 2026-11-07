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
}
