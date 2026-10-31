using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Market
{
    /// <summary>
    /// The city supermarket's daily orders (spec 011 §3.3, ADR 0055): posted at the truck's arrival, for produce harvested
    /// today, delivered by midnight to the collection point, booked as owed (1300) and paid at the next 6:00.
    /// </summary>
    public static class Supermarket
    {
        /// <summary>Orders have begun: the content has them and the first session is over (or skipped; spec 011 §10 G6).</summary>
        public static bool Started(Simulation sim)
        {
            if (sim.Config.Game.Market.Orders == null) return false;
            Progress.GuidanceState g = sim.State.Guidance;
            return g == null || g.Skipped || g.Step >= sim.Config.Game.Onboarding.Steps.Count;
        }

        /// <summary>Deliveries are open from 6:00 to midnight: the truck's hours, whether or not it has left full.</summary>
        public static bool DeliveryOpen(Simulation sim)
        {
            TruckDef truck = sim.Config.Game.Market.Truck;
            return truck != null && truck.HereAt(MarketRules.HourOfDay(sim));
        }

        /// <summary>When deliveries close next (today's midnight pass while open).</summary>
        public static long CloseTime(Simulation sim)
        {
            long t = sim.State.Clock.SimTime;
            long close = t - t % ClockDef.SecondsPerDay + sim.Config.Game.Market.Truck.LeaveHour * 3600L;
            return close <= t ? close + ClockDef.SecondsPerDay : close;
        }

        /// <summary>Fresh stock: what was harvested today and is still in store off the stall (oldest stock leaves first).</summary>
        public static long FreshStock(Simulation sim, int good)
        {
            MarketState m = sim.State.Market;
            long free = m.Stock[good] - m.Offered[good];
            return System.Math.Max(0, System.Math.Min(m.FreshToday[good], free));
        }

        /// <summary>The open order for a good with something left, while deliveries are open; null otherwise.</summary>
        public static SupermarketOrder OpenOrderFor(Simulation sim, int good)
        {
            if (sim.Config.Game.Market.Orders == null || !DeliveryOpen(sim)) return null;
            foreach (SupermarketOrder o in sim.State.Market.Orders)
                if (o.Good == good && o.Left > 0) return o;
            return null;
        }

        /// <summary>One acre's base harvest of a crop for one sowing, grams: seasonYieldPerAcre × cycleShare (spec 003).</summary>
        public static long BaseSowing(Simulation sim, CropDef crop) { return FarmRules.CycleGrams(sim.Clock, crop, crop.SeasonYieldPerAcre); }

        /// <summary>The latest sowing time at which the crop ripens before deliveries close.</summary>
        public static long SowBy(Simulation sim, CropDef crop) { return CloseTime(sim) - crop.GrowthSeconds - 1; }

        /// <summary>
        /// What a harvest of <paramref name="grams"/> ready at <paramref name="readyAt"/> fetches for the hand's choice (spec 011
        /// §3.3, AC-11): the order price up to what's left of an open order when he can deliver it (harvested at an hourly pass
        /// before the close), the trader's floor beyond it.
        /// </summary>
        public static long HarvestValue(Simulation sim, int good, long grams, long readyAt)
        {
            Fixed floor = MarketRules.Floor(sim, good);
            SupermarketOrder o = OpenOrderFor(sim, good);
            long pass = (readyAt + 3599) / 3600 * 3600;
            if (o == null || pass >= CloseTime(sim)) return MarketRules.Value(sim, good, grams, floor);
            long q = System.Math.Min(grams, o.Left);
            return MarketRules.Value(sim, good, q, o.Price) + MarketRules.Value(sim, good, grams - q, floor);
        }

        /// <summary>Takes q of fresh stock to the collection point: Dr 1300 Owed by buyers, Cr the sales account (spec 011 §3.3).</summary>
        internal static long Deliver(Simulation sim, SupermarketOrder o, long q, bool byHand)
        {
            MarketState m = sim.State.Market;
            Store.Take(sim, o.Good, q);
            m.FreshToday[o.Good] -= q;
            o.Delivered += q;
            long amount = MarketRules.Value(sim, o.Good, q, o.Price);
            string good = sim.Config.Game.Market.Goods[o.Good].Id;
            if (amount > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.supermarketSale", "order:" + o.Id,
                    new Posting(Account.OwedByBuyers, amount), new Posting(Store.SalesAccount(sim, o.Good), -amount));
            sim.State.Finance.Sales.Add(sim, o.Good, SoldTo.Orders, amount);
            sim.Emit(new OrderDelivered(sim.State.Clock.SimTime, o.Id, good, q, amount, byHand));
            return amount;
        }

        /// <summary>The 6:00 payment of everything owed: Dr Cash, Cr 1300 (spec 011 §3.3).</summary>
        internal static void Pay(Simulation sim)
        {
            long owed = sim.State.Ledger.Balance(Account.OwedByBuyers);
            if (owed <= 0) return;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.supermarketPaid", "supermarket",
                new Posting(Account.Cash, owed), new Posting(Account.OwedByBuyers, -owed));
            sim.Emit(new OrderPaid(sim.State.Clock.SimTime, owed));
        }

        /// <summary>Yesterday's orders lapse with what's left (no penalty); fresh stock starts again.</summary>
        internal static void Lapse(Simulation sim)
        {
            MarketState m = sim.State.Market;
            foreach (SupermarketOrder o in m.Orders)
                if (o.Left > 0) sim.Emit(new OrderLapsed(sim.State.Clock.SimTime, o.Id, sim.Config.Game.Market.Goods[o.Good].Id, o.Left));
            m.Orders.Clear();
            System.Array.Clear(m.FreshToday, 0, m.FreshToday.Length);
        }

        /// <summary>
        /// The day's orders (spec 011 §3.3): perDay crop orders among the field crops in season that ripen within the delivery
        /// window (without repeats while two or more can), then greensPerDay garden-green orders. Stream Orders, counter = day,
        /// key = the draw.
        /// </summary>
        internal static void Post(Simulation sim)
        {
            OrderDef def = sim.Config.Game.Market.Orders;
            TruckDef truck = sim.Config.Game.Market.Truck;
            FarmDefs farm = sim.Config.Game.Farm;
            MarketState m = sim.State.Market;
            long t = sim.State.Clock.SimTime;
            ulong day = (ulong)(t / ClockDef.SecondsPerDay);
            ulong key = 0;
            Fixed U() { return Fixed.FromRaw((long)(Rng.Next(sim.State.Seed, RngStream.Orders, key++, day) >> 32)); }

            int season = (int)Calendar.SeasonAt(t, sim.Clock);
            long window = (truck.LeaveHour - truck.ArriveHour + 24) % 24 * 3600L;
            var eligible = new List<int>();
            for (int i = 0; i < farm.Crops.Count; i++)
                if (!farm.Crops[i].Fodder && farm.Crops[i].SowSeasons[season] && farm.Crops[i].GrowthSeconds <= window) eligible.Add(i); // fodder is never sold to a shop
            var pool = new List<int>(eligible);
            var posted = new List<SupermarketOrder>();
            for (int n = 0; n < def.PerDay && pool.Count > 0; n++)
            {
                int pick = pool[(int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.Orders, key++, day), (ulong)pool.Count)];
                if (eligible.Count >= 2) pool.Remove(pick);
                CropDef crop = farm.Crops[pick];
                long amount = Lots(BaseSowing(sim, crop), def.SowingsMin + (def.SowingsMax - def.SowingsMin) * U(), def.LotGrams);
                posted.Add(New(sim, crop.OutputGood, amount, def.PremiumMin + (def.PremiumMax - def.PremiumMin) * U()));
            }

            var greens = new List<GardenCropDef>();
            foreach (GardenCropDef g in farm.GardenCrops)
            {
                bool field = false;
                foreach (CropDef c in farm.Crops) field |= c.OutputGood == g.Good;
                if (!field) greens.Add(g);
            }
            for (int n = 0; n < def.GreensPerDay && greens.Count > 0; n++)
            {
                GardenCropDef g = greens[(int)Rng.Below(Rng.Next(sim.State.Seed, RngStream.Orders, key++, day), (ulong)greens.Count)];
                long amount = Lots(g.HarvestGrams, def.GreenPickingsMin + (def.GreenPickingsMax - def.GreenPickingsMin) * U(), def.GreensLotGrams);
                posted.Add(New(sim, g.Good, amount, def.PremiumMin + (def.PremiumMax - def.PremiumMin) * U()));
            }
            if (posted.Count > 0) sim.Emit(new OrdersPosted(t, posted.ConvertAll(o => o.Clone())));
        }

        /// <summary>round_down_to(lot, base × factor), at least one lot.</summary>
        private static long Lots(long baseGrams, Fixed factor, long lot)
        {
            long g = factor.MulRatio(baseGrams, 1).FloorToLong();
            return System.Math.Max(lot, g / lot * lot);
        }

        private static SupermarketOrder New(Simulation sim, int good, long amount, Fixed premium)
        {
            premium = Fixed.FromRatio(premium.MulRatio(100, 1).RoundToLong(), 100); // rounded to 0.01
            MarketState m = sim.State.Market;
            var o = new SupermarketOrder
            {
                Id = m.NextOrderId++, Good = good, Amount = amount, Premium = premium,
                Price = MarketRules.RegionalPrice(sim, good) * (Fixed.One + premium), PostedAt = sim.State.Clock.SimTime,
            };
            m.Orders.Add(o);
            return o;
        }
    }
}
