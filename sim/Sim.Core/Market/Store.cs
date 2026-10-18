using System.Numerics;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Market
{
    /// <summary>What happens when goods don't fit the store (spec 004 §3.5).</summary>
    public enum Overflow
    {
        /// <summary>Rejected with StoreFull if the goods don't fit; nothing changes.</summary>
        StoreOnly = 0,

        /// <summary>Store what fits, sell the rest to the trader at the floor now; TraderFull if it can't take it.</summary>
        SellRestToTrader = 1,
    }

    /// <summary>The hut store's internal operations for specs 003 and 005 (spec 004 §3.4, §3.5).</summary>
    public static class Store
    {
        /// <summary>The weight of a quantity in grams: q × weightKg, with each-units counted as 1,000 g per kg.</summary>
        public static long WeightGrams(GoodDef good, long quantity)
        {
            BigInteger n = new BigInteger(quantity) * good.WeightKg.Raw * (1000 / good.UnitSize);
            return (long)((n + (BigInteger.One << (Fixed.FractionBits - 1))) >> Fixed.FractionBits);
        }

        /// <summary>The founder's store capacity in grams: the sum over their buildings.</summary>
        public static long CapacityGrams(Simulation sim)
        {
            long kg = 0;
            foreach (Land.Building b in sim.State.Land.Buildings) kg += sim.Config.Game.Buildings[b.Def].StoreCapacityKg;
            return kg * 1000;
        }

        /// <summary>The weight of everything in store, in grams.</summary>
        public static long UsedGrams(Simulation sim)
        {
            var goods = sim.Config.Game.Market.Goods;
            long used = 0;
            for (int g = 0; g < goods.Count; g++) used += WeightGrams(goods[g], sim.State.Market.Stock[g]);
            return used;
        }

        /// <summary>The most of a good that fits the free space (rounded down).</summary>
        public static long Fits(Simulation sim, int good)
        {
            GoodDef g = sim.Config.Game.Market.Goods[good];
            long free = CapacityGrams(sim) - UsedGrams(sim);
            if (free <= 0) return 0;
            if (g.WeightKg == Fixed.Zero) return long.MaxValue;
            // q × weightKg × (1000 / unitSize) ≤ free  ⇔  q ≤ free × 2^32 ÷ (weightKg.Raw × (1000 / unitSize))
            BigInteger q = (new BigInteger(free) << Fixed.FractionBits) / (new BigInteger(g.WeightKg.Raw) * (1000 / g.UnitSize));
            while (q > 0 && WeightGrams(g, (long)q) > free) q -= 1; // the weight is rounded to the nearest gram
            return (long)q;
        }

        /// <summary>
        /// Adds a harvest or fruit to the store (spec 004 §3.5). With SellRestToTrader the part that doesn't fit is
        /// sold to the trader at the floor now (one ledger entry, <c>SoldToTrader</c>). Returns StoreFull or
        /// TraderFull without changing anything when it can't.
        /// </summary>
        internal static Commands.RejectReason Add(Simulation sim, int good, long quantity, Overflow overflow, string reference)
        {
            long fit = Fits(sim, good);
            long rest = quantity > fit ? quantity - fit : 0;
            if (rest > 0)
            {
                if (overflow == Overflow.StoreOnly) return Commands.RejectReason.StoreFull;
                if (rest > MarketRules.TraderCapacityLeft(sim, good)) return Commands.RejectReason.TraderFull;
                SellToTrader(sim, good, rest, reference);
            }
            sim.State.Market.Stock[good] += quantity - rest;
            return Commands.RejectReason.None;
        }

        /// <summary>Takes stock out of the store, stock not on offer first (spec 004 §3.5). The caller has checked there's enough.</summary>
        internal static void Take(Simulation sim, int good, long quantity)
        {
            if (quantity < 0 || quantity > sim.State.Market.Stock[good])
                throw new System.InvalidOperationException("Can't take " + quantity + " of good " + good + " from the store.");
            MarketState m = sim.State.Market;
            m.Stock[good] -= quantity;
            if (m.Offered[good] > m.Stock[good]) m.Offered[good] = m.Stock[good]; // stock not on offer goes first
        }

        /// <summary>Sells to the trader at the current floor: Dr Cash, Cr the good's sales account. Uses trader capacity.</summary>
        internal static long SellToTrader(Simulation sim, int good, long quantity, string reference)
        {
            Fixed floor = MarketRules.Floor(sim, good);
            long amount = MarketRules.Value(sim, good, quantity, floor);
            sim.State.Market.TraderBoughtToday[good] += quantity;
            if (amount > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.soldToTrader", reference,
                    new Posting(Account.Cash, amount), new Posting(SalesAccount(sim, good), -amount));
            sim.Emit(new SoldToTrader(sim.State.Clock.SimTime, sim.Config.Game.Market.Goods[good].Id, quantity, floor, amount));
            return amount;
        }

        /// <summary>4000 Crop sales or 4010 Fruit sales.</summary>
        internal static Account SalesAccount(Simulation sim, int good)
        {
            return sim.Config.Game.Market.Goods[good].Sales == SalesKind.Fruit ? Account.FruitSales : Account.CropSales;
        }

        /// <summary>What <see cref="UseInput"/> would cost: the shortfall after stock, at today's input price.</summary>
        public static long ShortfallCost(Simulation sim, int good, long quantity)
        {
            long shortfall = quantity - System.Math.Min(quantity, sim.State.Market.Stock[good]);
            return shortfall == 0 ? 0 : MarketRules.Value(sim, good, shortfall, MarketRules.InputPrice(sim, good));
        }

        /// <summary>
        /// Uses an input (spec 004 §3.4): stock first, then the shortfall bought at today's price (Dr
        /// <paramref name="account"/>, Cr Cash). The caller has checked <see cref="ShortfallCost"/> with CanAfford.
        /// </summary>
        internal static void UseInput(Simulation sim, int good, long quantity, Account account, string reference)
        {
            long fromStock = System.Math.Min(quantity, sim.State.Market.Stock[good]);
            long shortfall = quantity - fromStock;
            long cost = ShortfallCost(sim, good, quantity);
            long book = RemoveStock(sim, good, fromStock);
            if (book + cost > 0)
            {
                var postings = new System.Collections.Generic.List<Posting> { new Posting(account, book + cost) };
                if (book > 0) postings.Add(new Posting(Account.InputsInStore, -book));
                if (cost > 0) postings.Add(new Posting(Account.Cash, -cost));
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, shortfall > 0 ? "ledger.inputsBought" : "ledger.inputsUsed", reference, postings.ToArray());
            }
            if (shortfall > 0) sim.Emit(new InputsBought(sim.State.Clock.SimTime, sim.Config.Game.Market.Goods[good].Id, shortfall, cost));
        }

        /// <summary>
        /// Takes q of an input's stock and returns its book value at average cost (cost × q ÷ stock, rounded down; the
        /// last unit takes the remainder), so 1200 always equals the inputs' book value (ADR 0034).
        /// </summary>
        internal static long RemoveStock(Simulation sim, int good, long quantity)
        {
            MarketState m = sim.State.Market;
            if (quantity == 0) return 0;
            long book = quantity == m.Stock[good] ? m.StockCost[good]
                : (long)(new BigInteger(m.StockCost[good]) * quantity / m.Stock[good]);
            m.Stock[good] -= quantity;
            m.StockCost[good] -= book;
            return book;
        }

        /// <summary>
        /// The store's value for net worth (spec 004 §3.5): produce at the trader's floor, inputs at today's input
        /// price (replacement cost, ADR 0032).
        /// </summary>
        public static long Value(Simulation sim)
        {
            var goods = sim.Config.Game.Market.Goods;
            long total = 0;
            for (int g = 0; g < goods.Count; g++)
            {
                long q = sim.State.Market.Stock[g];
                if (q == 0) continue;
                Fixed price = goods[g].Kind == GoodKind.Produce ? MarketRules.Floor(sim, g) : MarketRules.InputPrice(sim, g);
                total += MarketRules.Value(sim, g, q, price);
            }
            return total;
        }
    }
}
