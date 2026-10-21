using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Time;

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
            foreach (Hand h in s.Hands)
            {
                if (h.Contract != ContractState.Active) continue;
                foreach (StandingOrder o in s.Orders.ToArray())
                {
                    if (!HasActionLeft(sim, h)) break;
                    if (o.HandId == h.Id) Act(sim, h, o);
                }
            }
        }

        /// <summary>
        /// A new order starts at once (ADR 0040): the hand works its target now if they haven't used this hour's action. The same
        /// counter limits the hourly pass, so a hand never does more than orderActionsPerHour in one sim hour.
        /// </summary>
        public static void ActNow(Simulation sim, Hand h, StandingOrder o)
        {
            if (h.Contract == ContractState.Active && HasActionLeft(sim, h)) Act(sim, h, o);
        }

        private static bool HasActionLeft(Simulation sim, Hand h)
        {
            long hour = sim.State.Clock.SimTime / 3600;
            if (h.ActionHour != hour)
            {
                h.ActionHour = hour;
                h.ActionsInHour = 0;
            }
            return h.ActionsInHour < sim.Config.Game.People.Hand.OrderActionsPerHour;
        }

        private static void Act(Simulation sim, Hand h, StandingOrder o)
        {
            if (o.Kind == OrderTargetKind.Field ? Field(sim, h, o) : Tree(sim, h, o)) h.ActionsInHour++;
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
            int pick = CropToSow(sim, o);
            if (pick < 0)
            {
                Wait(sim, o, OrderWait.OutOfSeason);
                return false;
            }
            CropDef crop = defs.Farm.Crops[pick];
            long cash = sim.State.Ledger.Cash;
            // The order's seed source is for its own crop; the hand's other picks use certified seed.
            CommandResult r = new Sow(t, tile, crop.Id, pick == o.Crop ? o.Seed : SeedSource.Certified, defs.Farm.Fertilisers[o.Fertiliser].Id).ApplyNow(sim);
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

        /// <summary>
        /// What the hand sows (ADR 0048 §1): the order's crop while it is in season; otherwise, or when the choice is his (crop −1),
        /// the in-season crop with the best expected profit per season on this field.
        /// </summary>
        internal static int CropToSow(Simulation sim, StandingOrder o)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            if (o.Crop >= 0 && farm.Crops[o.Crop].SowSeasons[(int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock)]) return o.Crop;
            return FarmRules.BestCrop(sim, sim.State.Land.Tiles[(int)o.Target], farm.Fertilisers[o.Fertiliser]);
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
                    // The minimum is for the order's own crop; the hand's other picks keep the good's current minimum (ADR 0048 §1).
                    if (o.Kind == OrderTargetKind.Tree || (o.Crop >= 0 && sim.Config.Game.Farm.Crops[o.Crop].OutputGood == good)) m.MinPrice[good] = o.MinPrice;
                }
            }
            Done(sim, h, o, action, goodId, quantity, sim.State.Ledger.Cash - cash);
            if (o.Once)
            {
                // A this-season-only order is finished once its crop is in: the field is the player's again.
                sim.State.People.Orders.Remove(o);
                sim.Emit(new OrderCompleted(sim.State.Clock.SimTime, h.Id, o.Kind, o.Target));
            }
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
