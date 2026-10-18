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

        /// <summary>The book value of each input's stock, 1/100 coin (account 1200, average cost; ADR 0034). 0 for produce.</summary>
        internal long[] StockCost;

        /// <summary>The local market price per produce good (inputs: 0), and the P_regional it last cleared against.</summary>
        internal Fixed[] Price;
        internal Fixed[] LastRegional;

        /// <summary>The founder's offer per good (part of the stock), grams, and its minimum price (Zero: none).</summary>
        internal long[] Offered;
        internal Fixed[] MinPrice;

        /// <summary>Unmet demand, kg per hour, the last unmetWindowHours hours per good (a ring at UnmetHead).</summary>
        internal Fixed[][] Unmet;
        internal int UnmetHead;

        /// <summary>Good-hours where the safety rail clamped the target or the price, and good-hours cleared (balancer).</summary>
        internal long[] RailHits;
        internal long Hours;

        /// <summary>The mandi trip under way, or null; the day index of the last departure (−1 = never).</summary>
        public MandiTrip Trip { get; internal set; }
        public long LastTripDay { get; internal set; } = -1;

        internal MarketState(int goods, int unmetWindow)
        {
            TraderBoughtToday = new long[goods];
            Stock = new long[goods];
            StockCost = new long[goods];
            Price = new Fixed[goods];
            LastRegional = new Fixed[goods];
            Offered = new long[goods];
            MinPrice = new Fixed[goods];
            Unmet = new Fixed[goods][];
            for (int g = 0; g < goods; g++) Unmet[g] = new Fixed[unmetWindow];
            RailHits = new long[goods];
        }

        private MarketState() { }

        public long TraderBought(int good) { return TraderBoughtToday[good]; }

        public long InStore(int good) { return Stock[good]; }

        /// <summary>The book value of an input's stock (average cost), 1/100 coin.</summary>
        public long StockCostOf(int good) { return StockCost[good]; }

        /// <summary>The local market price of a produce good, coins per kg.</summary>
        public Fixed MarketPrice(int good) { return Price[good]; }

        public long OnOffer(int good) { return Offered[good]; }

        /// <summary>The offer's minimum price, or null.</summary>
        public Fixed? OfferMinPrice(int good) { return MinPrice[good] == Fixed.Zero ? (Fixed?)null : MinPrice[good]; }

        /// <summary>Unmet demand of the last <c>hoursBack</c> + 1 hour (0 = the latest), kg.</summary>
        public Fixed UnmetAt(int good, int hoursBack)
        {
            Fixed[] ring = Unmet[good];
            return ring[((UnmetHead - 1 - hoursBack) % ring.Length + ring.Length) % ring.Length];
        }

        public long RailHitsOf(int good) { return RailHits[good]; }

        public long MarketHours { get { return Hours; } }

        internal MarketState Clone()
        {
            var c = new MarketState
            {
                PriceIndex = PriceIndex,
                TraderBoughtToday = (long[])TraderBoughtToday.Clone(),
                Stock = (long[])Stock.Clone(),
                StockCost = (long[])StockCost.Clone(),
                Price = (Fixed[])Price.Clone(),
                LastRegional = (Fixed[])LastRegional.Clone(),
                Offered = (long[])Offered.Clone(),
                MinPrice = (Fixed[])MinPrice.Clone(),
                Unmet = new Fixed[Unmet.Length][],
                UnmetHead = UnmetHead,
                RailHits = (long[])RailHits.Clone(),
                Hours = Hours,
                Trip = Trip?.Clone(),
                LastTripDay = LastTripDay,
            };
            for (int g = 0; g < Unmet.Length; g++) c.Unmet[g] = (Fixed[])Unmet[g].Clone();
            return c;
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteFixed(PriceIndex);
            h.WriteInt(TraderBoughtToday.Length);
            foreach (long q in TraderBoughtToday) h.WriteLong(q);
            foreach (long q in Stock) h.WriteLong(q);
            foreach (long c in StockCost) h.WriteLong(c);
            for (int g = 0; g < Stock.Length; g++)
            {
                h.WriteFixed(Price[g]);
                h.WriteFixed(LastRegional[g]);
                h.WriteLong(Offered[g]);
                h.WriteFixed(MinPrice[g]);
                foreach (Fixed u in Unmet[g]) h.WriteFixed(u);
                h.WriteLong(RailHits[g]);
            }
            h.WriteInt(UnmetHead);
            h.WriteLong(Hours);
            h.WriteBool(Trip != null);
            if (Trip != null) Trip.WriteTo(h);
            h.WriteLong(LastTripDay);
        }
    }
}
