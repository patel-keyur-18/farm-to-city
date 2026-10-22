using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Market
{
    /// <summary>The market and store part of the game state (spec 004 §4). Per-good arrays are in good order.</summary>
    public sealed class MarketState
    {
        public Fixed PriceIndex { get; internal set; }

        /// <summary>What the trader has bought today, per good, in quantity units. Reset when the truck arrives (or, without a truck, at each daily pass).</summary>
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

        /// <summary>Whether the content has the trader's truck (spec 011): only then are the fields below kept, hashed and saved.</summary>
        internal bool Trading;

        /// <summary>The truck is at the farm (spec 011 §3.1); the load it has bought since it arrived, in grams.</summary>
        public bool TruckHere { get; internal set; }
        public long TruckLoadToday { get; internal set; }

        /// <summary>Each produce good's daily price factor (spec 011 §3.1b); 1 for inputs.</summary>
        internal Fixed[] DailyFactor;

        /// <summary>The truck's price set this morning and yesterday's, for the good's sheet ("yesterday 5.30, ▲6%"); 0 = not known.</summary>
        internal Fixed[] TruckPrice;
        internal Fixed[] TruckPriceYesterday;

        /// <summary>What hands' SELL TO TRADER orders stored while the truck was away or full, sold at the next arrival (spec 011 §3.1).</summary>
        internal long[] HeldForTruck;

        /// <summary>Grams added to the store by a harvest or picking since the last 6:00 pass (spec 011 §3.3).</summary>
        internal long[] FreshToday;

        /// <summary>Today's supermarket orders, in id order (spec 011 §3.3), and the next order's id.</summary>
        internal System.Collections.Generic.List<SupermarketOrder> Orders = new System.Collections.Generic.List<SupermarketOrder>();
        public long NextOrderId { get; internal set; } = 1;

        internal MarketState(int goods, int unmetWindow)
        {
            FreshToday = new long[goods];
            DailyFactor = new Fixed[goods];
            TruckPrice = new Fixed[goods];
            TruckPriceYesterday = new Fixed[goods];
            for (int g = 0; g < goods; g++) DailyFactor[g] = Fixed.One;
            HeldForTruck = new long[goods];
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

        public Fixed DailyFactorOf(int good) { return DailyFactor[good]; }

        /// <summary>Yesterday's truck price, or null when not known (a new game or a migrated town's first day).</summary>
        public Fixed? TruckPriceYesterdayOf(int good) { return TruckPriceYesterday[good] == Fixed.Zero ? (Fixed?)null : TruckPriceYesterday[good]; }

        public long HeldForTruckOf(int good) { return HeldForTruck[good]; }

        public long FreshTodayOf(int good) { return FreshToday[good]; }

        /// <summary>Today's supermarket orders in id order (delivered, part delivered or open).</summary>
        public System.Collections.Generic.IReadOnlyList<SupermarketOrder> SupermarketOrders { get { return Orders; } }

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
                Trading = Trading,
                TruckHere = TruckHere,
                TruckLoadToday = TruckLoadToday,
                DailyFactor = (Fixed[])DailyFactor.Clone(),
                TruckPrice = (Fixed[])TruckPrice.Clone(),
                TruckPriceYesterday = (Fixed[])TruckPriceYesterday.Clone(),
                HeldForTruck = (long[])HeldForTruck.Clone(),
                FreshToday = (long[])FreshToday.Clone(),
                Orders = Orders.ConvertAll(o => o.Clone()),
                NextOrderId = NextOrderId,
            };
            for (int g = 0; g < Unmet.Length; g++) c.Unmet[g] = (Fixed[])Unmet[g].Clone();
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteFixed(PriceIndex);
            h.WriteInt(TraderBoughtToday.Length);
            foreach (long q in TraderBoughtToday) h.WriteLong(q);
            foreach (long q in Stock) h.WriteLong(q);
            foreach (long c in StockCost) h.WriteLong(c);
            if (!h.Hashing) h.WriteInt(Unmet.Length == 0 ? 0 : Unmet[0].Length); // the content's window, checked on load
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
            if (!Trading) return; // Phase 1 content (the golden replays) hashes as before
            h.WriteBool(TruckHere);
            h.WriteLong(TruckLoadToday);
            for (int g = 0; g < Stock.Length; g++)
            {
                h.WriteFixed(DailyFactor[g]);
                h.WriteFixed(TruckPrice[g]);
                h.WriteFixed(TruckPriceYesterday[g]);
                h.WriteLong(HeldForTruck[g]);
                h.WriteLong(FreshToday[g]);
            }
            h.WriteLong(NextOrderId);
            h.WriteInt(Orders.Count);
            foreach (SupermarketOrder o in Orders) o.WriteTo(h);
        }

        /// <summary>Per-good arrays are written in the save's good order and put back in the running content's.</summary>
        internal static MarketState ReadFrom(SaveReader r, int unmetWindow, Definitions.TruckDef truck, long simTime)
        {
            Fixed index = r.ReadFixed();
            int goods = r.ReadCount();
            if (goods != r.SavedCount(ContentTable.Good) || goods != r.Count(ContentTable.Good))
                throw new SaveContentException("the content has " + r.Count(ContentTable.Good) + " goods, the save " + goods);
            var s = new MarketState(goods, unmetWindow) { PriceIndex = index };
            foreach (long[] a in new[] { s.TraderBoughtToday, s.Stock, s.StockCost })
                for (int g = 0; g < goods; g++) a[r.Slot(ContentTable.Good, g)] = r.ReadLong();
            int window = r.ReadInt();
            if (window != unmetWindow) throw new SaveContentException("the unmet-demand window is " + unmetWindow + " hours, the save's " + window);
            for (int g = 0; g < goods; g++)
            {
                int at = r.Slot(ContentTable.Good, g);
                s.Price[at] = r.ReadFixed();
                s.LastRegional[at] = r.ReadFixed();
                s.Offered[at] = r.ReadLong();
                s.MinPrice[at] = r.ReadFixed();
                for (int i = 0; i < window; i++) s.Unmet[at][i] = r.ReadFixed();
                s.RailHits[at] = r.ReadLong();
            }
            s.UnmetHead = r.ReadInt();
            s.Hours = r.ReadLong();
            s.Trip = r.ReadBool() ? MandiTrip.ReadFrom(r) : null;
            s.LastTripDay = r.ReadLong();
            if (truck == null) return s;
            s.Trading = true;
            if (r.Schema < 6)
            {
                // Spec 011 §4: the truck is here by the save's hour, its load is what the trader bought today.
                s.TruckHere = truck.HereAt((int)(simTime % Definitions.ClockDef.SecondsPerDay / 3600));
                foreach (long q in s.TraderBoughtToday) s.TruckLoadToday += q;
                return s;
            }
            s.TruckHere = r.ReadBool();
            s.TruckLoadToday = r.ReadLong();
            for (int g = 0; g < goods; g++)
            {
                int at = r.Slot(ContentTable.Good, g);
                s.DailyFactor[at] = r.ReadFixed();
                s.TruckPrice[at] = r.ReadFixed();
                s.TruckPriceYesterday[at] = r.ReadFixed();
                s.HeldForTruck[at] = r.ReadLong();
                s.FreshToday[at] = r.ReadLong();
            }
            s.NextOrderId = r.ReadLong();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Orders.Add(SupermarketOrder.ReadFrom(r));
            return s;
        }
    }
}

namespace Sim.Core.Market
{
    /// <summary>A city supermarket's order for one good, delivered by midnight, paid at 6:00 next morning (spec 011 §3.3).</summary>
    public sealed class SupermarketOrder
    {
        public long Id { get; internal set; }
        public int Good { get; internal set; }

        /// <summary>The amount asked for and what has been delivered, in grams.</summary>
        public long Amount { get; internal set; }
        public long Delivered { get; internal set; }

        /// <summary>The order's price, coins per kg: P_regional at posting × (1 + premium).</summary>
        public Fixed Price { get; internal set; }

        /// <summary>The premium over P_regional at posting (for the Why?).</summary>
        public Fixed Premium { get; internal set; }
        public long PostedAt { get; internal set; }

        public long Left { get { return Amount - Delivered; } }

        internal SupermarketOrder Clone() { return (SupermarketOrder)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Good, Good);
            h.WriteLong(Amount);
            h.WriteLong(Delivered);
            h.WriteFixed(Price);
            h.WriteFixed(Premium);
            h.WriteLong(PostedAt);
        }

        internal static SupermarketOrder ReadFrom(SaveReader r)
        {
            return new SupermarketOrder
            {
                Id = r.ReadLong(), Good = r.ReadRef(ContentTable.Good), Amount = r.ReadLong(), Delivered = r.ReadLong(),
                Price = r.ReadFixed(), Premium = r.ReadFixed(), PostedAt = r.ReadLong(),
            };
        }
    }
}
