using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class OrderTests
    {
        /// <summary>No wheat pests (away rules soften outbreaks, so a live-versus-away comparison needs none).</summary>
        private static GameDefs NoPests(System.Func<string, string, string> edit = null)
        {
            return Game((f, j) =>
            {
                if (f == "farming.json") j = j.Replace("\"wheat\": 0.06", "\"wheat\": 0");
                return edit == null ? j : edit(f, j);
            });
        }

        /// <summary>A hand with no applicants left waiting (they count their wait only while live).</summary>
        private static Hand QuietHand(Simulation sim)
        {
            Hand h = HireHand(sim);
            while (P(sim).Applicants.Count > 0) sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            Ok(sim, new SetWorkHours(sim.State.Clock.SimTime, "normal"));
            return h;
        }

        private static int Steady(Simulation sim) { return sim.Config.Game.People.IndexOfTrait("steady"); }

        private static Hand SteadyHand(Simulation sim)
        {
            Hand h = QuietHand(sim);
            h.Trait1 = Steady(sim);
            h.Trait2 = sim.Config.Game.People.IndexOfTrait("hardworking");
            return h;
        }

        private static OrderSpec KeepWheat(SellRule sell = SellRule.Store, string minPrice = "0")
        {
            return OrderSpec.KeepCrop(HomeTile, "wheat", SeedSource.Certified, "none", sell, Fixed.Parse(minPrice));
        }

        // Spec 006 AC-13.
        [Test]
        public void KeepCrop_LiveAndAway()
        {
            Simulation sim = Start(NoPests());
            Hand h = SteadyHand(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new SetStandingOrder(t, h.Id, KeepWheat()));
            sim.DrainEvents();

            Simulation live = Simulation.FromState(sim.Config, sim.State.Clone());
            Simulation away = Simulation.FromState(sim.Config, sim.State.Clone());
            for (long x = t + 1000; x < t + 72 * Hour; x += 3 * Hour + 7) live.AdvanceTo(x);
            live.AdvanceTo(t + 72 * Hour);
            Ok(away, new AdvanceTo(t + 72 * Hour, AdvanceMode.Away));

            var done = live.DrainEvents().OfType<OrderExecuted>().ToList();
            Assert.That(done.Count(e => e.Action == OrderAction.Sowed), Is.GreaterThanOrEqualTo(5), "re-sown each 6-hour cycle");
            Assert.That(done.Count(e => e.Action == OrderAction.Harvested), Is.GreaterThanOrEqualTo(5));
            Assert.That(away.State.ComputeHash(), Is.EqualTo(live.State.ComputeHash()), "the same farm live or away");
            Assert.That(live.State.Farm.Crops.Count(c => c.State == CropState.Ready && live.State.Clock.SimTime - c.ReadyAt > Hour), Is.EqualTo(0),
                "no ready harvest left waiting on an ordered field");

            // Out of season (the real calendar: wheat is Winter only), the tile waits and says so once.
            Simulation real = Start(NoInflation((f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j));
            Hand rh = QuietHand(real);
            real.DrainEvents();
            Ok(real, new SetStandingOrder(real.State.Clock.SimTime, rh.Id, KeepWheat()));
            real.AdvanceTo(real.State.Clock.SimTime + 2 * Day);
            var waits = real.DrainEvents().OfType<OrderWaiting>().ToList();
            Assert.That(waits.Select(w => w.Reason), Is.EqualTo(new[] { OrderWait.OutOfSeason }));
            Assert.That(real.State.Farm.CropOn(HomeTile), Is.Null);
        }

        // ADR 0040: a new order starts at once; one action per hand per sim hour.
        [Test]
        public void StartsAtOnce_OneActionAnHour_PlayerWins()
        {
            Simulation sim = Start(NoPests());
            Hand h = SteadyHand(sim);
            long t = sim.State.Clock.SimTime / Hour * Hour + Hour;
            sim.AdvanceTo(t - 10);
            sim.DrainEvents();
            Ok(sim, new SetStandingOrder(t - 10, h.Id, KeepWheat()));
            Assert.That(sim.DrainEvents().OfType<OrderExecuted>().Single().Target, Is.EqualTo(HomeTile), "sown at once");
            Assert.That(sim.State.Farm.CropOn(HomeTile), Is.Not.Null);
            Ok(sim, new SetStandingOrder(t - 10, h.Id, OrderSpec.KeepCrop(19, "wheat", SeedSource.Certified, "none", SellRule.Store)));
            Assert.That(sim.DrainEvents().OfType<OrderExecuted>(), Is.Empty, "this hour's action is used");
            sim.AdvanceTo(t);
            Assert.That(sim.DrainEvents().OfType<OrderExecuted>().Single().Target, Is.EqualTo(19), "the next hour's one action");

            // The player sows potato on an ordered, empty field: the order never undoes it.
            Simulation p = Start(NoPests());
            Hand ph = SteadyHand(p);
            long now = p.State.Clock.SimTime;
            Ok(p, new Sow(now, HomeTile, "potato", SeedSource.Certified, "none"));
            Ok(p, new SetStandingOrder(now, ph.Id, KeepWheat()));
            p.AdvanceTo(now + 3 * Hour);
            Assert.That(p.Config.Game.Farm.Crops[p.State.Farm.CropOn(HomeTile).Def].Id, Is.EqualTo("potato"));
        }

        // Spec 006 AC-14.
        [Test]
        public void SellRules()
        {
            long Run(SellRule rule, string min, bool careless, out Simulation sim, out int good)
            {
                sim = Start(NoPests(TestSim.NoSpoilage));
                Hand h = SteadyHand(sim);
                if (careless) h.Trait1 = sim.Config.Game.People.IndexOfTrait("careless");
                long t = sim.State.Clock.SimTime;
                Ok(sim, new Sow(t, HomeTile, "wheat", SeedSource.Certified, "none"));
                Ok(sim, new SetStandingOrder(t, h.Id, KeepWheat(rule, min)));
                good = FarmTest.Good(sim, "wheat");
                long stock = sim.State.Market.Stock[good];
                sim.DrainEvents();
                long ready = sim.State.Farm.CropOn(HomeTile).ReadyAt;
                sim.AdvanceTo(ready / Hour * Hour + Hour);
                Assert.That(sim.PendingEvents.OfType<OrderExecuted>().First().Action, Is.EqualTo(OrderAction.Harvested));
                return stock;
            }

            long before = Run(SellRule.Store, "0", false, out Simulation store, out int wheat);
            long harvested = store.PendingEvents.OfType<OrderExecuted>().First().Quantity;
            Assert.That(store.State.Market.Stock[wheat] - before, Is.EqualTo(harvested));
            Assert.That(store.State.Market.Offered[wheat], Is.EqualTo(0));

            before = Run(SellRule.Offer, "26", false, out Simulation offer, out wheat);
            Assert.That(offer.State.Market.Offered[wheat], Is.EqualTo(offer.State.Market.Stock[wheat] - before));
            Assert.That(offer.State.Market.Offered[wheat], Is.GreaterThan(0));
            Assert.That(offer.State.Market.MinPrice[wheat], Is.EqualTo(Fixed.FromInt(26)));

            before = Run(SellRule.Trader, "0", false, out Simulation trader, out wheat);
            var ex = trader.PendingEvents.OfType<OrderExecuted>().First();
            long sold = trader.State.Market.TraderBoughtToday[wheat];
            Assert.That(sold, Is.GreaterThan(0));
            Assert.That(trader.State.Market.Stock[wheat] - before, Is.EqualTo(ex.Quantity - sold), "the rest stored");
            Assert.That(ex.Amount, Is.GreaterThan(0));

            Run(SellRule.Store, "0", true, out Simulation careless, out wheat);
            Assert.That(careless.State.Market.TraderBoughtToday[wheat], Is.GreaterThan(0), "a careless hand sells to the trader");
        }

        // Spec 006 AC-15.
        [Test]
        public void NeverBorrows_PausesOnLapse()
        {
            Simulation sim = Start(NoPests());
            Hand h = SteadyHand(sim);
            CashTo(sim, 0);
            long liabilities = sim.State.Ledger.Balance(Account.MoneylenderLoans) + sim.State.Ledger.Balance(Account.BankLoans);
            sim.DrainEvents();
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, KeepWheat()));
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Hour);
            Assert.That(sim.DrainEvents().OfType<OrderWaiting>().Single().Reason, Is.EqualTo(OrderWait.InsufficientFunds), "said once, when set");
            Assert.That(sim.State.Farm.CropOn(HomeTile), Is.Null);
            Assert.That(sim.State.Ledger.Balance(Account.MoneylenderLoans) + sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(liabilities));

            // Lapsed at the close: orders pause.
            CashTo(sim, 5_000 * Coin);
            sim.AdvanceTo(S);
            Assert.That(h.Contract, Is.EqualTo(ContractState.Lapsed));
            CashTo(sim, 1_000_000 * Coin);
            sim.DrainEvents();
            sim.AdvanceTo(S + 3 * Hour);
            Assert.That(sim.DrainEvents().OfType<OrderExecuted>(), Is.Empty);
        }

        // Spec 006 AC-20.
        [Test]
        public void LastAction()
        {
            Simulation sim = Start(NoPests());
            Hand h = SteadyHand(sim);
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, KeepWheat(SellRule.Trader)));
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 20 * Hour);
            OrderExecuted e = sim.DrainEvents().OfType<OrderExecuted>().Last();
            StandingOrder o = P(sim).OrderFor(OrderTargetKind.Field, HomeTile);
            Assert.That(o.LastAction, Is.EqualTo(e.Action));
            Assert.That(o.LastAt, Is.EqualTo(e.SimTime));
            Assert.That(o.LastQuantity, Is.EqualTo(e.Quantity));
            Assert.That(o.LastAmount, Is.EqualTo(e.Amount));
        }

        [Test]
        public void SetAndClear_Validation()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetStandingOrder(t, 99, KeepWheat())).Reason, Is.EqualTo(RejectReason.NoHand));
            Assert.That(sim.Execute(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(0, "wheat", SeedSource.Certified, "none", SellRule.Store))).Reason, Is.EqualTo(RejectReason.UnknownTarget));
            Assert.That(sim.Execute(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(HomeTile, "maize", SeedSource.Certified, "none", SellRule.Store))).Reason, Is.EqualTo(RejectReason.UnknownCrop));
            Assert.That(sim.Execute(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(HomeTile, "radish", SeedSource.Saved, "none", SellRule.Store))).Reason, Is.EqualTo(RejectReason.SeedCannotBeSaved));
            Assert.That(sim.Execute(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(HomeTile, "wheat", SeedSource.Certified, "gold", SellRule.Store))).Reason, Is.EqualTo(RejectReason.UnknownFertiliser));
            Assert.That(sim.Execute(new SetStandingOrder(t, h.Id, KeepWheat(SellRule.Offer, "0"))).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            int[] tiles = { 18, 19, 20, 26, 27, 28 };
            foreach (int tile in tiles) Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(tile, "wheat", SeedSource.Certified, "none", SellRule.Store)));
            long slot = sim.State.Land.Slots[0].Id;
            Assert.That(sim.Execute(new SetStandingOrder(t, h.Id, OrderSpec.PickTree(slot, SellRule.Store))).Reason, Is.EqualTo(RejectReason.TooManyOrders));
            Ok(sim, new SetStandingOrder(t, h.Id, KeepWheat(SellRule.Trader))); // replacing one is fine
            Ok(sim, new ClearStandingOrder(t, h.Id, OrderTargetKind.Field, HomeTile));
            Assert.That(sim.Execute(new ClearStandingOrder(t, h.Id, OrderTargetKind.Field, HomeTile)).Reason, Is.EqualTo(RejectReason.NoOrder));
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.PickTree(slot, SellRule.Store)));
        }
    }
}
