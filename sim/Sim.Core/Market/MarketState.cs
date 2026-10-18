using Sim.Core.Maths;

namespace Sim.Core.Market
{
    /// <summary>The market and store part of the game state (spec 004 §4). Per-good arrays are in good order.</summary>
    public sealed class MarketState
    {
        public Fixed PriceIndex { get; internal set; }

        /// <summary>What the trader has bought today, per good, in quantity units. Reset at each daily pass.</summary>
        internal long[] TraderBoughtToday;

        /// <summary>The hut store: quantity per good in its units (grams, ml, each), produce and inputs (spec 004 §3.5).</summary>
        internal long[] Stock;

        internal MarketState(int goods)
        {
            TraderBoughtToday = new long[goods];
            Stock = new long[goods];
        }

        public long TraderBought(int good) { return TraderBoughtToday[good]; }

        public long InStore(int good) { return Stock[good]; }

        internal MarketState Clone()
        {
            return new MarketState(0)
            {
                PriceIndex = PriceIndex,
                TraderBoughtToday = (long[])TraderBoughtToday.Clone(),
                Stock = (long[])Stock.Clone(),
            };
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteFixed(PriceIndex);
            h.WriteInt(TraderBoughtToday.Length);
            foreach (long q in TraderBoughtToday) h.WriteLong(q);
            foreach (long q in Stock) h.WriteLong(q);
        }
    }
}
