using Sim.Core.Maths;

namespace Sim.Core.Events
{
    /// <summary>An hourly pass sold some of the founder's offer: locally and to the trader at the floor (spec 004 §5).</summary>
    public sealed class MarketSales : SimEvent
    {
        public string GoodId { get; }
        public long LocalQuantity { get; }
        public Fixed Price { get; }
        public long TraderQuantity { get; }
        public Fixed Floor { get; }
        public long Amount { get; }

        public MarketSales(long simTime, string goodId, long localQuantity, Fixed price, long traderQuantity, Fixed floor, long amount) : base(simTime)
        {
            GoodId = goodId;
            LocalQuantity = localQuantity;
            Price = price;
            TraderQuantity = traderQuantity;
            Floor = floor;
            Amount = amount;
        }

        protected override string Describe()
        {
            return "MarketSales(good=" + GoodId + ", local=" + LocalQuantity + "@" + Price + ", trader=" + TraderQuantity + "@" + Floor + ", amount=" + Amount + ")";
        }
    }

    /// <summary>Where goods spoiled: in the store, waiting ready in a field, or ripe on a tree (ADR 0034).</summary>
    public enum SpoilSource
    {
        Store = 0,
        Field = 1,
        Tree = 2,
    }

    /// <summary>Goods spoiled at the daily pass (spec 004 §5), for the report and While you were away.</summary>
    public sealed class GoodsSpoiled : SimEvent
    {
        public string GoodId { get; }
        public long Quantity { get; }
        public SpoilSource Source { get; }

        /// <summary>The tile (field) or slot (tree); 0 for the store.</summary>
        public long Where { get; }

        public GoodsSpoiled(long simTime, string goodId, long quantity, SpoilSource source, long where) : base(simTime)
        {
            GoodId = goodId;
            Quantity = quantity;
            Source = source;
            Where = where;
        }

        protected override string Describe() { return "GoodsSpoiled(good=" + GoodId + ", q=" + Quantity + ", " + Source + " " + Where + ")"; }
    }

    /// <summary>The cart left for the mandi (spec 004 §5).</summary>
    public sealed class MandiTripStarted : SimEvent
    {
        public System.Collections.Generic.IReadOnlyList<Market.MandiLoad> Loads { get; }
        public long CartHire { get; }
        public long ReturnAt { get; }

        public MandiTripStarted(long simTime, System.Collections.Generic.IReadOnlyList<Market.MandiLoad> loads, long cartHire, long returnAt) : base(simTime)
        {
            Loads = loads;
            CartHire = cartHire;
            ReturnAt = returnAt;
        }

        protected override string Describe() { return "MandiTripStarted(loads=" + Loads.Count + ", hire=" + CartHire + ", back=" + ReturnAt + ")"; }
    }

    /// <summary>The cart is back: gross sales, commission and net (spec 004 §5).</summary>
    public sealed class MandiTripReturned : SimEvent
    {
        public long Gross { get; }
        public long Commission { get; }
        public long Net { get; }

        public MandiTripReturned(long simTime, long gross, long commission, long net) : base(simTime)
        {
            Gross = gross;
            Commission = commission;
            Net = net;
        }

        protected override string Describe() { return "MandiTripReturned(gross=" + Gross + ", commission=" + Commission + ", net=" + Net + ")"; }
    }

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
