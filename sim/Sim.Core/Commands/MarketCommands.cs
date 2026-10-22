using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Commands
{
    /// <summary>Shared validation for the market commands (spec 004 §5).</summary>
    internal static class MarketChecks
    {
        /// <summary>The good's index, or a rejection: UnknownGood, InvalidQuantity.</summary>
        public static RejectReason Good(Simulation sim, string goodId, long quantity, out int good)
        {
            good = sim.Config.Game?.Market.IndexOf(goodId) ?? -1;
            if (good < 0) return RejectReason.UnknownGood;
            if (quantity <= 0) return RejectReason.InvalidQuantity;
            return RejectReason.None;
        }

        public static bool IsProduce(Simulation sim, int good) { return sim.Config.Game.Market.Goods[good].Kind == GoodKind.Produce; }

        /// <summary>Stock that isn't on offer.</summary>
        public static long Free(Simulation sim, int good) { return sim.State.Market.Stock[good] - sim.State.Market.Offered[good]; }

        public static bool IsSapling(Simulation sim, int good)
        {
            foreach (TreeDef t in sim.Config.Game.Farm.Trees)
                if (t.SaplingGood == good) return true;
            return false;
        }
    }

    /// <summary>
    /// Puts stored produce on the local market stall (spec 004 §3.3). It sells hour by hour while it stays in the store.
    /// The minimum price, if given, applies to the good's whole offer (the latest one given; none clears it).
    /// </summary>
    public sealed class Offer : TimedCommand
    {
        public string GoodId { get; }
        public long Quantity { get; }
        public Fixed? MinPrice { get; }

        public Offer(long at, string goodId, long quantity, Fixed? minPrice = null) : base(at)
        {
            GoodId = goodId;
            Quantity = quantity;
            MinPrice = minPrice;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = MarketChecks.Good(sim, GoodId, Quantity, out int g);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            if (!MarketChecks.IsProduce(sim, g)) return CommandResult.Reject(RejectReason.NotProduce);
            if (MinPrice.HasValue && MinPrice.Value <= Fixed.Zero) return CommandResult.Reject(RejectReason.InvalidArgument);
            if (Quantity > MarketChecks.Free(sim, g)) return CommandResult.Reject(RejectReason.InsufficientStock);
            sim.State.Market.Offered[g] += Quantity;
            sim.State.Market.MinPrice[g] = MinPrice ?? Fixed.Zero;
            Store.CutHeld(sim, g);
            return CommandResult.Ok;
        }
    }

    /// <summary>Takes produce back off the stall (spec 004 §3.3); it stays in the store.</summary>
    public sealed class Withdraw : TimedCommand
    {
        public string GoodId { get; }
        public long Quantity { get; }

        public Withdraw(long at, string goodId, long quantity) : base(at)
        {
            GoodId = goodId;
            Quantity = quantity;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = MarketChecks.Good(sim, GoodId, Quantity, out int g);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            if (Quantity > sim.State.Market.Offered[g]) return CommandResult.Reject(RejectReason.NotOffered);
            sim.State.Market.Offered[g] -= Quantity;
            if (sim.State.Market.Offered[g] == 0) sim.State.Market.MinPrice[g] = Fixed.Zero;
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Sells stock not on offer to the regional trader at the floor, now, up to its capacity today (spec 004 §3.3); refused
    /// with TraderAway while the truck is away (spec 011 §3.1).
    /// </summary>
    public sealed class SellToTrader : TimedCommand
    {
        public string GoodId { get; }
        public long Quantity { get; }

        public SellToTrader(long at, string goodId, long quantity) : base(at)
        {
            GoodId = goodId;
            Quantity = quantity;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = MarketChecks.Good(sim, GoodId, Quantity, out int g);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            if (!MarketRules.TraderBuys(sim)) return CommandResult.Reject(RejectReason.TraderAway);
            if (!MarketChecks.IsProduce(sim, g)) return CommandResult.Reject(RejectReason.NotProduce);
            if (Quantity > MarketChecks.Free(sim, g)) return CommandResult.Reject(RejectReason.InsufficientStock);
            if (Quantity > MarketRules.TraderCapacityLeft(sim, g)) return CommandResult.Reject(RejectReason.TraderFull);
            Store.Take(sim, g, Quantity);
            Store.SellToTrader(sim, g, Quantity, "trader:" + GoodId);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Delivers fresh produce (harvested since 6:00) to the supermarket's collection point at the truck stop: no cost, no labour;
    /// booked as a sale owed (Dr 1300, Cr sales) and paid at 6:00 next morning (spec 011 §3.3).
    /// </summary>
    public sealed class DeliverOrder : TimedCommand
    {
        public long OrderId { get; }
        public long Quantity { get; }

        public DeliverOrder(long at, long orderId, long quantity) : base(at)
        {
            OrderId = orderId;
            Quantity = quantity;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            MarketState m = sim.State.Market;
            if (sim.Config.Game?.Market.Orders == null || OrderId <= 0 || OrderId >= m.NextOrderId) return CommandResult.Reject(RejectReason.UnknownOrder);
            SupermarketOrder o = m.Orders.Find(x => x.Id == OrderId);
            if (o == null || !Supermarket.DeliveryOpen(sim)) return CommandResult.Reject(RejectReason.OrderClosed);
            if (Quantity <= 0) return CommandResult.Reject(RejectReason.InvalidQuantity);
            if (Quantity > Supermarket.FreshStock(sim, o.Good)) return CommandResult.Reject(RejectReason.InsufficientStock);
            if (Quantity > o.Left) return CommandResult.Reject(RejectReason.OrderFilled);
            Supermarket.Deliver(sim, o, Quantity, false);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Buys an input (not saplings) into the store at today's input price (spec 004 §3.4): Dr 1200 Inputs in store,
    /// Cr Cash, at average cost (ADR 0034).
    /// </summary>
    public sealed class Buy : TimedCommand
    {
        public string GoodId { get; }
        public long Quantity { get; }

        public Buy(long at, string goodId, long quantity) : base(at)
        {
            GoodId = goodId;
            Quantity = quantity;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = MarketChecks.Good(sim, GoodId, Quantity, out int g);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            if (MarketChecks.IsProduce(sim, g) || MarketChecks.IsSapling(sim, g)) return CommandResult.Reject(RejectReason.NotAnInput);
            if (Quantity > Store.Fits(sim, g)) return CommandResult.Reject(RejectReason.StoreFull);
            long cost = MarketRules.Value(sim, g, Quantity, MarketRules.InputPrice(sim, g));
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            if (cost > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.inputsBought", "buy:" + GoodId,
                    new Posting(Account.InputsInStore, cost), new Posting(Account.Cash, -cost));
            sim.State.Market.Stock[g] += Quantity;
            sim.State.Market.StockCost[g] += cost;
            sim.Emit(new InputsBought(sim.State.Clock.SimTime, GoodId, Quantity, cost));
            return CommandResult.Ok;
        }
    }
}

namespace Sim.Core.Commands
{
    using System.Collections.Generic;
    using Sim.Core.Farm;

    /// <summary>
    /// Carts produce to the town mandi (spec 004 §3.6, ADR 0031, 0034): the goods leave the store and the cart hire is
    /// paid now; the trip takes the founder's delivered labour of MandiDef.founderHours; proceeds at return.
    /// </summary>
    public sealed class GoToMandi : TimedCommand
    {
        public IReadOnlyList<MandiLoad> Loads { get; }

        public GoToMandi(long at, IReadOnlyList<MandiLoad> loads) : base(at) { Loads = new List<MandiLoad>(loads ?? new MandiLoad[0]).AsReadOnly(); }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.Config.Game == null) return CommandResult.Reject(RejectReason.UnknownGood);
            MandiDef def = sim.Config.Game.Market.Mandi;
            int goods = sim.Config.Game.Market.Goods.Count;
            var total = new long[goods];
            long weight = 0;
            if (Loads.Count == 0) return CommandResult.Reject(RejectReason.InvalidQuantity);
            foreach (MandiLoad l in Loads)
            {
                RejectReason r = MarketChecks.Good(sim, l.GoodId, l.Quantity, out int g);
                if (r != RejectReason.None) return CommandResult.Reject(r);
                if (!MarketChecks.IsProduce(sim, g)) return CommandResult.Reject(RejectReason.NotProduce);
                total[g] += l.Quantity;
                if (total[g] > MarketChecks.Free(sim, g)) return CommandResult.Reject(RejectReason.InsufficientStock);
                weight += Store.WeightGrams(sim.Config.Game.Market.Goods[g], l.Quantity);
            }
            if (weight > def.CartCapacityGrams) return CommandResult.Reject(RejectReason.OverCartCapacity);
            MarketState m = sim.State.Market;
            long t = sim.State.Clock.SimTime;
            if (m.Trip != null) return CommandResult.Reject(RejectReason.TripUnderway);
            if (m.LastTripDay == t / Definitions.ClockDef.SecondsPerDay) return CommandResult.Reject(RejectReason.AlreadyWentToday);
            if (sim.State.Farm.Founder.SickToday) return CommandResult.Reject(RejectReason.FounderSick);
            long hire = MarketRules.Indexed(sim, def.CartHirePerTrip);
            if (!sim.State.Ledger.CanAfford(hire)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            FarmSystem.Accrue(sim);
            var trip = new MandiTrip { DepartedAt = t, ReturnAt = t + def.TripHours * 3600L, CartHire = hire };
            for (int g = 0; g < goods; g++)
            {
                if (total[g] == 0) continue;
                Store.Take(sim, g, total[g]);
                trip.Loads.Add(new KeyValuePair<int, long>(g, total[g]));
            }
            if (hire > 0)
                sim.State.Ledger.Post(t, sim.Period, "ledger.cartHire", "mandi",
                    new Posting(Account.TransportAndSelling, hire), new Posting(Account.Cash, -hire));
            m.Trip = trip;
            m.LastTripDay = t / Definitions.ClockDef.SecondsPerDay;
            trip.JobId = sim.ScheduleJob(MarketSystem.MandiJob, 0, trip.ReturnAt).Id;
            FarmSystem.Refresh(sim);
            sim.Emit(new MandiTripStarted(t, Loads, hire, trip.ReturnAt));
            return CommandResult.Ok;
        }
    }
}
