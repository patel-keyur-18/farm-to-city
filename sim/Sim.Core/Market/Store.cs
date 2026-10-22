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

        /// <summary>Store what fits, sell the rest to the trader at the floor now; TraderAway or TraderFull if it can't take it.</summary>
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

        /// <summary>
        /// The founder's capacity in grams for a place: the hut store, or the shed's feed room and straw stack, or the yard's
        /// compost pit (spec 012 §3.3): the sum over their buildings.
        /// </summary>
        public static long CapacityGrams(Simulation sim, GoodPlace place = GoodPlace.Store)
        {
            long kg = 0;
            foreach (Land.Building b in sim.State.Land.Buildings)
            {
                BuildingDef d = sim.Config.Game.Buildings[b.Def];
                kg += place == GoodPlace.Store ? d.StoreCapacityKg : place == GoodPlace.FeedRoom ? d.FeedRoomKg
                    : place == GoodPlace.StrawStack ? d.StrawStackKg : d.CompostPitKg;
            }
            return kg * 1000;
        }

        /// <summary>The weight of everything kept in a place (the hut store by default), in grams.</summary>
        public static long UsedGrams(Simulation sim, GoodPlace place = GoodPlace.Store)
        {
            var goods = sim.Config.Game.Market.Goods;
            long used = 0;
            for (int g = 0; g < goods.Count; g++)
                if (goods[g].Place == place) used += WeightGrams(goods[g], sim.State.Market.Stock[g]);
            return used;
        }

        /// <summary>The most of a good that fits the free space of its place (rounded down).</summary>
        public static long Fits(Simulation sim, int good)
        {
            GoodDef g = sim.Config.Game.Market.Goods[good];
            long free = CapacityGrams(sim, g.Place) - UsedGrams(sim, g.Place);
            if (free <= 0) return 0;
            if (g.WeightKg == Fixed.Zero) return long.MaxValue;
            // q × weightKg × (1000 / unitSize) ≤ free  ⇔  q ≤ free × 2^32 ÷ (weightKg.Raw × (1000 / unitSize))
            BigInteger q = (new BigInteger(free) << Fixed.FractionBits) / (new BigInteger(g.WeightKg.Raw) * (1000 / g.UnitSize));
            while (q > 0 && WeightGrams(g, (long)q) > free) q -= 1; // the weight is rounded to the nearest gram
            return (long)q;
        }

        /// <summary>
        /// Buys an input into its place at today's input price (spec 004 §3.4): Dr 1200, Cr Cash, at average cost (ADR 0034). The
        /// feed room and the straw stack need the cattle shed (spec 012 §3.3: <c>NoShed</c>); a place without room is
        /// <c>PlaceFull</c>, the hut store <c>StoreFull</c>. Shared by <c>Buy</c>, the shed's Fill and the hand's animals order.
        /// </summary>
        internal static Commands.RejectReason BuyInput(Simulation sim, int good, long quantity, string reference)
        {
            GoodDef def = sim.Config.Game.Market.Goods[good];
            if ((def.Place == GoodPlace.FeedRoom || def.Place == GoodPlace.StrawStack) && Livestock.LivestockRules.Shed(sim) == null)
                return Commands.RejectReason.NoShed;
            if (quantity > Fits(sim, good)) return def.Place == GoodPlace.Store ? Commands.RejectReason.StoreFull : Commands.RejectReason.PlaceFull;
            long cost = MarketRules.Value(sim, good, quantity, MarketRules.InputPrice(sim, good));
            if (!sim.State.Ledger.CanAfford(cost)) return Commands.RejectReason.InsufficientFunds;
            if (cost > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.inputsBought", reference,
                    new Posting(Account.InputsInStore, cost), new Posting(Account.Cash, -cost));
            sim.State.Market.Stock[good] += quantity;
            sim.State.Market.StockCost[good] += cost;
            Livestock.LivestockSystem.Refilled(sim, def.Place);
            sim.Emit(new InputsBought(sim.State.Clock.SimTime, def.Id, quantity, cost));
            return Commands.RejectReason.None;
        }

        /// <summary>
        /// Adds a harvest or fruit to the store (spec 004 §3.5). With SellRestToTrader the part that doesn't fit is
        /// sold to the trader at the floor now (one ledger entry, <c>SoldToTrader</c>). Returns StoreFull, TraderAway (the
        /// truck has left, spec 011 §3.1) or TraderFull without changing anything when it can't.
        /// </summary>
        internal static Commands.RejectReason Add(Simulation sim, int good, long quantity, Overflow overflow, string reference)
        {
            long fit = Fits(sim, good);
            long rest = quantity > fit ? quantity - fit : 0;
            if (rest > 0)
            {
                if (overflow == Overflow.StoreOnly) return Commands.RejectReason.StoreFull;
                if (!MarketRules.TraderBuys(sim)) return Commands.RejectReason.TraderAway;
                if (rest > MarketRules.TraderCapacityLeft(sim, good)) return Commands.RejectReason.TraderFull;
                SellToTrader(sim, good, rest, reference);
            }
            sim.State.Market.Stock[good] += quantity - rest;
            if (sim.State.Market.Trading) sim.State.Market.FreshToday[good] += quantity - rest; // a harvest: fresh today (spec 011 §3.3)
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
            CutHeld(sim, good);
        }

        /// <summary>What the hands hold for the truck is never more than the stock not on offer (spec 011 §3.1).</summary>
        internal static void CutHeld(Simulation sim, int good)
        {
            MarketState m = sim.State.Market;
            long free = m.Stock[good] - m.Offered[good];
            if (m.HeldForTruck[good] > free) m.HeldForTruck[good] = free;
        }

        /// <summary>
        /// Counts a trader purchase against the crop's daily room and, with the truck, its load; a full load makes the truck
        /// leave at once (spec 011 §3.1, TruckLeft(Full)).
        /// </summary>
        internal static void CountTraderPurchase(Simulation sim, int good, long quantity)
        {
            MarketState m = sim.State.Market;
            m.TraderBoughtToday[good] += quantity;
            Definitions.TruckDef truck = sim.Config.Game.Market.Truck;
            if (truck == null) return;
            m.TruckLoadToday += quantity;
            if (m.TruckHere && m.TruckLoadToday >= truck.LoadGrams)
            {
                m.TruckHere = false;
                sim.Emit(new TruckLeft(sim.State.Clock.SimTime, TruckLeaveReason.Full, m.TruckLoadToday));
            }
        }

        /// <summary>Sells to the trader at the current floor: Dr Cash, Cr the good's sales account. Uses trader capacity.</summary>
        internal static long SellToTrader(Simulation sim, int good, long quantity, string reference)
        {
            Fixed floor = MarketRules.Floor(sim, good);
            long amount = MarketRules.Value(sim, good, quantity, floor);
            if (amount > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.soldToTrader", reference,
                    new Posting(Account.Cash, amount), new Posting(SalesAccount(sim, good), -amount));
            sim.Emit(new SoldToTrader(sim.State.Clock.SimTime, sim.Config.Game.Market.Goods[good].Id, quantity, floor, amount));
            CountTraderPurchase(sim, good, quantity); // after the sale's event, so a full truck leaves after it
            return amount;
        }

        /// <summary>An input's expense account when used or spoiled: 5000 for seed, 5010 for fertiliser, compost and spray.</summary>
        internal static Account UseAccount(Simulation sim, int good)
        {
            GoodPlace place = sim.Config.Game.Market.Goods[good].Place;
            if (place == GoodPlace.FeedRoom || place == GoodPlace.StrawStack) return Account.AnimalFeed; // spec 012 §3.7
            foreach (CropDef c in sim.Config.Game.Farm.Crops) if (c.SeedGood == good) return Account.SeedsAndPlanting;
            foreach (GardenCropDef c in sim.Config.Game.Farm.GardenCrops) if (c.SeedGood == good) return Account.SeedsAndPlanting;
            return Account.FertiliserAndSpraying;
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
