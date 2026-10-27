using Sim.Core.Scheduling;

namespace Sim.Core.Market
{
    /// <summary>Market and store (spec 004): the hourly market, daily spoilage and the trader's reset, the seasonal price index.</summary>
    internal sealed class MarketSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Market; } }

        public static void Initialise(Simulation sim)
        {
            Definitions.MarketDefs defs = sim.Config.Game.Market;
            var m = new MarketState(defs.Goods.Count, defs.Market.UnmetWindowHours) { PriceIndex = defs.Economy.StartIndex };
            sim.State.Market = m;
            foreach (Definitions.StartStock s in sim.Config.Game.Start.Store) m.Stock[s.Good] += s.Quantity;
            for (int g = 0; g < defs.Goods.Count; g++)
            {
                if (defs.Goods[g].Kind != Definitions.GoodKind.Produce) continue;
                m.Price[g] = m.LastRegional[g] = MarketRules.RegionalPrice(sim, g);
            }
            if (defs.Truck == null) return;
            m.Trading = true;
            m.TruckHere = defs.Truck.HereAt(MarketRules.HourOfDay(sim));
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            Definitions.TruckDef truck = sim.Config.Game.Market.Truck;
            if (pass == ItemClass.Hourly)
            {
                int hour = MarketRules.HourOfDay(sim);
                if (truck != null && hour == truck.ArriveHour) Morning(sim);
                if (truck != null && hour == truck.LeaveHour) Evening(sim);
                ClearHour(sim, truck != null && truck.StallPausedAtPass(hour));
            }
            if (pass == ItemClass.Daily) Spoil(sim);
            if (pass == ItemClass.Daily && truck == null) System.Array.Clear(sim.State.Market.TraderBoughtToday, 0, sim.State.Market.TraderBoughtToday.Length);
            if (pass == ItemClass.Seasonal) StepIndex(sim);
        }

        /// <summary>
        /// The 6:00 pass before the market clears (spec 011 §3.6): the supermarket pays, the day's price factors, the truck
        /// arrives (its counts reset), the hands sell what they held for it, yesterday's orders lapse and today's are posted.
        /// </summary>
        private static void Morning(Simulation sim)
        {
            MarketState m = sim.State.Market;
            bool orders = sim.Config.Game.Market.Orders != null;
            if (orders) Supermarket.Pay(sim);
            DrawDailyFactors(sim);
            System.Array.Clear(m.TraderBoughtToday, 0, m.TraderBoughtToday.Length);
            m.TruckLoadToday = 0;
            m.TruckHere = true;
            sim.Emit(new Events.TruckArrived(sim.State.Clock.SimTime));
            SellHeld(sim);
            if (!orders) return;
            Supermarket.Lapse(sim);
            if (Supermarket.Started(sim)) Supermarket.Post(sim);
        }

        /// <summary>The midnight pass: the truck leaves if it is still here (spec 011 §3.1).</summary>
        private static void Evening(Simulation sim)
        {
            MarketState m = sim.State.Market;
            if (!m.TruckHere) return;
            m.TruckHere = false;
            sim.Emit(new Events.TruckLeft(sim.State.Clock.SimTime, Events.TruckLeaveReason.Day, m.TruckLoadToday));
        }

        /// <summary>
        /// Spec 011 §3.1b: f ← clamp(f + reversion × (1 − f) + volatility × (2u − 1), 1 ± band) for each produce good in id
        /// order, u a seeded draw (stream DailyPrice, counter = day × goods + good). Price-controlled goods don't move.
        /// </summary>
        internal static void DrawDailyFactors(Simulation sim)
        {
            Definitions.MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;
            ulong day = (ulong)(sim.State.Clock.SimTime / Definitions.ClockDef.SecondsPerDay);
            for (int g = 0; g < defs.Goods.Count; g++)
            {
                Definitions.GoodDef good = defs.Goods[g];
                if (good.Kind != Definitions.GoodKind.Produce || good.PriceControlled) continue;
                ulong r = Maths.Rng.Next(sim.State.Seed, Maths.RngStream.DailyPrice, 0, day * (ulong)defs.Goods.Count + (ulong)g);
                Maths.Fixed u = Maths.Fixed.FromRaw((long)(r >> 32));
                Maths.Fixed f = m.DailyFactor[g];
                f = f + defs.Market.DailyReversion * (Maths.Fixed.One - f) + good.DailyVolatility * (u + u - Maths.Fixed.One);
                Maths.Fixed lo = Maths.Fixed.One - good.DailyBand, hi = Maths.Fixed.One + good.DailyBand;
                m.DailyFactor[g] = f < lo ? lo : f > hi ? hi : f;
                m.TruckPriceYesterday[g] = m.TruckPrice[g];
                m.TruckPrice[g] = MarketRules.Floor(sim, g); // held all day (spec 011 §3.1)
            }
        }

        /// <summary>
        /// The hands' held sales (spec 011 §3.1): min(held, stock not on offer) of each good in id order, up to the truck's
        /// room, at the new day's price; what doesn't fit stays held.
        /// </summary>
        private static void SellHeld(Simulation sim)
        {
            Definitions.MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;
            for (int g = 0; g < defs.Goods.Count; g++)
            {
                Store.CutHeld(sim, g);
                long q = System.Math.Min(m.HeldForTruck[g], MarketRules.TraderCapacityLeft(sim, g));
                if (q <= 0) continue;
                m.HeldForTruck[g] -= q;
                Store.Take(sim, g, q);
                long amount = Store.SellToTrader(sim, g, q, "held:" + defs.Goods[g].Id);
                sim.Emit(new Events.HandSoldHeld(sim.State.Clock.SimTime, defs.Goods[g].Id, q, amount));
            }
        }

        public const string MandiJob = "mandi.trip";

        /// <summary>
        /// The cart is back from the mandi (spec 004 §3.6): the loads are sold at the town's regional price now, less the
        /// commission. Dr Cash net, Dr 5700 the commission, Cr the sales accounts the gross value. The founder's rate
        /// returns to normal (an accrual point).
        /// </summary>
        internal sealed class MandiReturn : IJobHandler
        {
            public string JobType { get { return MandiJob; } }
            public SystemOrder System { get { return SystemOrder.Market; } }

            public void OnDue(Simulation sim, Job job)
            {
                MarketState m = sim.State.Market;
                MandiTrip trip = m.Trip;
                Farm.FarmSystem.Accrue(sim);
                long crop = 0, fruit = 0;
                foreach (var load in trip.Loads)
                {
                    long v = MarketRules.Value(sim, load.Key, load.Value, MarketRules.RegionalPrice(sim, load.Key));
                    if (Store.SalesAccount(sim, load.Key) == Finance.Account.FruitSales) fruit += v; else crop += v;
                }
                long gross = crop + fruit;
                long commission = Finance.Money.MulRate(gross, sim.Config.Game.Market.Mandi.CommissionRate);
                var postings = new System.Collections.Generic.List<Finance.Posting> { new Finance.Posting(Finance.Account.Cash, gross - commission) };
                if (commission > 0) postings.Add(new Finance.Posting(Finance.Account.TransportAndSelling, commission));
                if (crop > 0) postings.Add(new Finance.Posting(Finance.Account.CropSales, -crop));
                if (fruit > 0) postings.Add(new Finance.Posting(Finance.Account.FruitSales, -fruit));
                if (gross > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.mandiSales", "mandi", postings.ToArray());
                m.Trip = null;
                sim.CompleteJob(job);
                Farm.FarmSystem.Refresh(sim);
                sim.Emit(new Events.MandiTripReturned(sim.State.Clock.SimTime, gross, commission, gross - commission));
            }
        }

        /// <summary>
        /// Daily spoilage in the store (spec 004 §3.5): round(q × spoilagePerDay) per good in id order, stock not on offer
        /// first. Spoiled inputs are expensed at average cost to their use account (5000 seed, 5010 the rest; ADR 0034).
        /// </summary>
        private static void Spoil(Simulation sim)
        {
            Definitions.MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;
            for (int g = 0; g < defs.Goods.Count; g++)
            {
                long lost = defs.Goods[g].SpoilagePerDay.MulRatio(m.Stock[g], 1).RoundToLong();
                if (lost <= 0) continue;
                if (defs.Goods[g].Kind == Definitions.GoodKind.Produce) Store.Take(sim, g, lost);
                else
                {
                    long book = Store.RemoveStock(sim, g, lost);
                    if (book > 0)
                        sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.AccrualPeriod, "ledger.inputsSpoiled", "store:" + defs.Goods[g].Id,
                            new Finance.Posting(Store.UseAccount(sim, g), book), new Finance.Posting(Finance.Account.InputsInStore, -book));
                }
                sim.Emit(new Events.GoodsSpoiled(sim.State.Clock.SimTime, defs.Goods[g].Id, lost, Events.SpoilSource.Store, 0));
            }
        }

        /// <summary>
        /// The hourly local market (spec 004 §3.2): each produce good in id order. While the stall is paused (spec 011 §3.2) the
        /// founder's offer adds nothing and the valley's market clears without it; the trader holds the floor only while here.
        /// </summary>
        private static void ClearHour(Simulation sim, bool paused)
        {
            Definitions.MarketDefs defs = sim.Config.Game.Market;
            MarketState m = sim.State.Market;
            for (int g = 0; g < defs.Goods.Count; g++)
            {
                if (defs.Goods[g].Kind != Definitions.GoodKind.Produce) continue;

                Maths.Fixed regional = FollowRegion(sim, g);

                Maths.Fixed floor = MarketRules.Floor(sim, g);
                bool hasMin = m.MinPrice[g] != Maths.Fixed.Zero;
                var input = new ClearingInput
                {
                    Price = m.Price[g], Regional = regional, Floor = floor, Ceiling = MarketRules.Ceiling(sim, g),
                    Elasticity = defs.Goods[g].Elasticity, DemandPerDay = defs.Region.DemandPerDay(g) + People.HouseholdRules.TownDemandPerDay(sim, g), // spec 014 §3.7
                    OtherSupplyPerDay = defs.Region.ValleySupplyPerDay(g) + defs.Region.ImportCapacityPerDay(g),
                    Eligible = paused ? 0 : !hasMin || m.MinPrice[g] <= m.Price[g] ? m.Offered[g] : 0,
                    TraderMayTake = !hasMin || m.MinPrice[g] <= floor,
                    TraderCapacityLeft = defs.Truck != null && !m.TruckHere ? 0 : MarketRules.TraderCapacityLeft(sim, g),
                };
                ClearingResult r = MarketClearing.Clear(defs.Market, input);
                m.Price[g] = r.Price;
                m.Unmet[g][m.UnmetHead] = r.UnmetKg;
                if (r.RailHit) m.RailHits[g]++;

                if (r.Sold + r.ToTrader == 0) continue;
                long local = MarketRules.Value(sim, g, r.Sold, r.Price);
                long trader = MarketRules.Value(sim, g, r.ToTrader, floor);
                long amount = local + trader;
                m.Offered[g] -= r.Sold + r.ToTrader;
                m.Stock[g] -= r.Sold + r.ToTrader;
                if (amount > 0)
                    sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.marketSales", "market:" + defs.Goods[g].Id,
                        new Finance.Posting(Finance.Account.Cash, amount), new Finance.Posting(Store.SalesAccount(sim, g), -amount));
                sim.Emit(new Events.MarketSales(sim.State.Clock.SimTime, defs.Goods[g].Id, r.Sold, r.Price, r.ToTrader, floor, amount));
                if (r.ToTrader > 0) Store.CountTraderPurchase(sim, g, r.ToTrader);
            }
            m.UnmetHead = (m.UnmetHead + 1) % defs.Market.UnmetWindowHours;
            m.Hours++;
        }

        /// <summary>
        /// The local price follows the region: when P_regional changed since the last clearing (a new season or an index
        /// step), the price is scaled by the same ratio (ADR 0034). Returns P_regional now.
        /// </summary>
        private static Maths.Fixed FollowRegion(Simulation sim, int g)
        {
            MarketState m = sim.State.Market;
            Maths.Fixed regional = MarketRules.RegionalPrice(sim, g);
            if (m.LastRegional[g] == Maths.Fixed.Zero)
            {
                // A good new to an older save (spec 016's atta and bran) starts at its regional price, as at a new game.
                m.Price[g] = regional;
                m.LastRegional[g] = regional;
            }
            if (regional != m.LastRegional[g])
            {
                m.Price[g] = m.Price[g].MulRatio(regional.Raw, m.LastRegional[g].Raw);
                m.LastRegional[g] = regional;
            }
            return regional;
        }

        /// <summary>
        /// The seasonal inflation step (spec 004 §3.1): i = (min + (max − min) × u) ÷ 4, u a seeded draw for the new
        /// season; PriceIndex ← PriceIndex × (1 + i).
        /// </summary>
        private static void StepIndex(Simulation sim)
        {
            Definitions.EconomyDef e = sim.Config.Game.Market.Economy;
            ulong r = Maths.Rng.Next(sim.State.Seed, Maths.RngStream.Inflation, 0, (ulong)sim.Period);
            Maths.Fixed u = Maths.Fixed.FromRaw((long)(r >> 32));
            Maths.Fixed i = (e.InflationMinPerYear + (e.InflationMaxPerYear - e.InflationMinPerYear) * u).MulRatio(1, 4);
            Maths.Fixed old = sim.State.Market.PriceIndex;
            sim.State.Market.PriceIndex = old * (Maths.Fixed.One + i);
            sim.Emit(new Events.PriceIndexChanged(sim.State.Clock.SimTime, old, sim.State.Market.PriceIndex));
            var goods = sim.Config.Game.Market.Goods;
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Kind == Definitions.GoodKind.Produce) FollowRegion(sim, g);
        }
    }
}
