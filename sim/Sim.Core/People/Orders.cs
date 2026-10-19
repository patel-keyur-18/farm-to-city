using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;

namespace Sim.Core.People
{
    /// <summary>
    /// Standing orders (spec 006 §3.8, ADR 0039 §6): every hourly pass, live or away, each hand under an active contract (id
    /// order) works their orders in target order, at most orderActionsPerHour actions. An order acts only on a ready or empty
    /// field or a ripe tree, so it never undoes the player's crop, and it reuses the player's own commands.
    /// </summary>
    internal static class Orders
    {
        public static void Run(Simulation sim)
        {
            PeopleState s = sim.State.People;
            int perHour = sim.Config.Game.People.Hand.OrderActionsPerHour;
            foreach (Hand h in s.Hands)
            {
                if (h.Contract != ContractState.Active) continue;
                int actions = 0;
                foreach (StandingOrder o in s.Orders.ToArray())
                {
                    if (actions >= perHour) break;
                    if (o.HandId != h.Id) continue;
                    if (o.Kind == OrderTargetKind.Field ? Field(sim, h, o) : Tree(sim, h, o)) actions++;
                }
            }
        }

        private static bool Field(Simulation sim, Hand h, StandingOrder o)
        {
            GameDefs defs = sim.Config.Game;
            long t = sim.State.Clock.SimTime;
            int tile = (int)o.Target;
            if (!sim.State.Land.Parcels[sim.State.Land.Tiles[tile].ParcelId].Owned)
            {
                sim.State.People.Orders.Remove(o); // the land was sold
                return false;
            }
            Crop c = sim.State.Farm.CropOn(tile);
            if (c != null && c.State == CropState.Ready)
            {
                int good = defs.Farm.Crops[c.Def].OutputGood;
                return Collect(sim, h, o, good, new Harvest(t, tile, Overflow.SellRestToTrader), OrderAction.Harvested, c.HarvestGrams);
            }
            if (c != null) return false; // growing: nothing to do
            CropDef crop = defs.Farm.Crops[o.Crop];
            long cash = sim.State.Ledger.Cash;
            CommandResult r = new Sow(t, tile, crop.Id, o.Seed, defs.Farm.Fertilisers[o.Fertiliser].Id).ApplyNow(sim);
            if (!r.Accepted)
            {
                Wait(sim, o, r.Reason == RejectReason.OutOfSeason ? OrderWait.OutOfSeason
                    : r.Reason == RejectReason.InsufficientFunds ? OrderWait.InsufficientFunds
                    : r.Reason == RejectReason.NoSavedSeed ? OrderWait.NoSavedSeed : OrderWait.Paused);
                return false;
            }
            Done(sim, h, o, OrderAction.Sowed, defs.Market.Goods[crop.OutputGood].Id, 0, sim.State.Ledger.Cash - cash);
            return true;
        }

        private static bool Tree(Simulation sim, Hand h, StandingOrder o)
        {
            Tree tree = sim.State.Farm.TreeIn(o.Target);
            if (tree == null || tree.RipeGrams == 0) return false;
            int good = sim.Config.Game.Farm.Trees[tree.Def].FruitGood;
            return Collect(sim, h, o, good, new PickFruit(sim.State.Clock.SimTime, o.Target, Overflow.SellRestToTrader), OrderAction.Picked, tree.RipeGrams);
        }

        /// <summary>Harvests or picks (overflow to the trader so a full store never blocks it), then applies the sell rule.</summary>
        private static bool Collect(Simulation sim, Hand h, StandingOrder o, int good, TimedCommand collect, OrderAction action, long quantity)
        {
            MarketState m = sim.State.Market;
            long cash = sim.State.Ledger.Cash, before = m.Stock[good];
            CommandResult r = collect.ApplyNow(sim);
            if (!r.Accepted)
            {
                Wait(sim, o, OrderWait.StoreFull);
                return false;
            }
            long stored = m.Stock[good] - before;
            string goodId = sim.Config.Game.Market.Goods[good].Id;
            bool careless = PeopleRules.HasTrait(sim, h, x => x.Careless);
            if (stored > 0)
            {
                if (careless || o.Sell == SellRule.Trader)
                {
                    long q = System.Math.Min(stored, MarketRules.TraderCapacityLeft(sim, good));
                    if (q > 0)
                    {
                        Store.Take(sim, good, q);
                        Store.SellToTrader(sim, good, q, "hand:" + h.Id);
                    }
                }
                else if (o.Sell == SellRule.Offer)
                {
                    m.Offered[good] += stored;
                    m.MinPrice[good] = o.MinPrice;
                }
            }
            Done(sim, h, o, action, goodId, quantity, sim.State.Ledger.Cash - cash);
            return true;
        }

        private static void Done(Simulation sim, Hand h, StandingOrder o, OrderAction action, string good, long quantity, long amount)
        {
            long t = sim.State.Clock.SimTime;
            o.LastAction = action;
            o.LastAt = t;
            o.LastQuantity = quantity;
            o.LastAmount = amount;
            o.Waiting = OrderWait.None;
            sim.Emit(new OrderExecuted(t, h.Id, o.Kind, o.Target, action, good, quantity, amount));
        }

        private static void Wait(Simulation sim, StandingOrder o, OrderWait reason)
        {
            if (o.Waiting == reason) return;
            o.Waiting = reason;
            sim.Emit(new OrderWaiting(sim.State.Clock.SimTime, o.HandId, o.Kind, o.Target, reason));
        }
    }
}
