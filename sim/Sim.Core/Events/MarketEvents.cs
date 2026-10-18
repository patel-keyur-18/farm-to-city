using Sim.Core.Maths;

namespace Sim.Core.Events
{
    /// <summary>The seasonal inflation step moved the price index (spec 004 §5).</summary>
    public sealed class PriceIndexChanged : SimEvent
    {
        public Fixed Old { get; }
        public Fixed New { get; }

        public PriceIndexChanged(long simTime, Fixed old, Fixed @new) : base(simTime)
        {
            Old = old;
            New = @new;
        }

        protected override string Describe() { return "PriceIndexChanged(" + Old + " -> " + New + ")"; }
    }

    /// <summary>Goods sold to the trader at the floor: a harvest's overflow, or SellToTrader (spec 004 §5).</summary>
    public sealed class SoldToTrader : SimEvent
    {
        public string GoodId { get; }
        public long Quantity { get; }
        public Fixed Price { get; }
        public long Amount { get; }

        public SoldToTrader(long simTime, string goodId, long quantity, Fixed price, long amount) : base(simTime)
        {
            GoodId = goodId;
            Quantity = quantity;
            Price = price;
            Amount = amount;
        }

        protected override string Describe() { return "SoldToTrader(good=" + GoodId + ", q=" + Quantity + ", price=" + Price + ", amount=" + Amount + ")"; }
    }

    /// <summary>Inputs bought: Buy, or a shortfall bought at use (spec 004 §5).</summary>
    public sealed class InputsBought : SimEvent
    {
        public string GoodId { get; }
        public long Quantity { get; }
        public long Amount { get; }

        public InputsBought(long simTime, string goodId, long quantity, long amount) : base(simTime)
        {
            GoodId = goodId;
            Quantity = quantity;
            Amount = amount;
        }

        protected override string Describe() { return "InputsBought(good=" + GoodId + ", q=" + Quantity + ", amount=" + Amount + ")"; }
    }
}
