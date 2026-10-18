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
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Hourly) ClearHour(sim);
            if (pass == ItemClass.Daily) Spoil(sim);
            if (pass == ItemClass.Daily) System.Array.Clear(sim.State.Market.TraderBoughtToday, 0, sim.State.Market.TraderBoughtToday.Length);
            if (pass == ItemClass.Seasonal) StepIndex(sim);
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

        /// <summary>The hourly local market (spec 004 §3.2): each produce good in id order.</summary>
        private static void ClearHour(Simulation sim)
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
                    Elasticity = defs.Goods[g].Elasticity, DemandPerDay = defs.Region.DemandPerDay(g),
                    OtherSupplyPerDay = defs.Region.ValleySupplyPerDay(g) + defs.Region.ImportCapacityPerDay(g),
                    Eligible = !hasMin || m.MinPrice[g] <= m.Price[g] ? m.Offered[g] : 0,
                    TraderMayTake = !hasMin || m.MinPrice[g] <= floor,
                    TraderCapacityLeft = MarketRules.TraderCapacityLeft(sim, g),
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
                m.TraderBoughtToday[g] += r.ToTrader;
                if (amount > 0)
                    sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.marketSales", "market:" + defs.Goods[g].Id,
                        new Finance.Posting(Finance.Account.Cash, amount), new Finance.Posting(Store.SalesAccount(sim, g), -amount));
                sim.Emit(new Events.MarketSales(sim.State.Clock.SimTime, defs.Goods[g].Id, r.Sold, r.Price, r.ToTrader, floor, amount));
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
