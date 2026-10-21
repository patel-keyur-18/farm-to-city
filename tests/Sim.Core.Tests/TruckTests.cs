using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 011 §3.1: the trader's truck, from 6:00 to midnight or till full (ADR 0054).</summary>
    public class TruckTests
    {
        /// <summary>A content edit setting the truck's load, in kg.</summary>
        private static System.Func<string, string, string> Load(int kg, System.Func<string, string, string> edit = null)
        {
            return (f, j) =>
            {
                if (f == "market.json") j = j.Replace("\"loadKg\": 4000", "\"loadKg\": " + kg);
                return edit == null ? j : edit(f, j);
            };
        }

        /// <summary>A rich, quiet farm at <paramref name="localSeconds"/> with the first session skipped (no first-sale exception).</summary>
        private static Simulation Farm(long localSeconds, System.Func<string, string, string> edit = null)
        {
            Simulation sim = NewFarm(localSeconds: localSeconds, game: Quiet((f, j) => NoSpoilage(f, edit == null ? j : edit(f, j))));
            Ok(sim, new SkipFirstSession(localSeconds));
            return sim;
        }

        private static int G(Simulation sim, string id) { return FarmTest.Good(sim, id); }

        private static void AssertLoadIsCounts(Simulation sim)
        {
            Assert.That(sim.State.Market.TruckLoadToday, Is.EqualTo(sim.State.Market.TraderBoughtToday.Sum()), "every purchase counts on the load");
        }

        // Spec 011 AC-1.
        [Test]
        public void Day_ArrivesAndLeaves()
        {
            Simulation sim = Farm(0);
            int potato = G(sim, "potato");
            Assert.That(sim.State.Market.TruckHere, Is.False, "a game started at midnight: the truck comes at 6:00");
            sim.State.Market.TraderBoughtToday[potato] = 100_000;
            sim.State.Market.TruckLoadToday = 100_000;

            sim.AdvanceTo(6 * Hour - 1);
            Assert.That(sim.PendingEvents.OfType<TruckArrived>(), Is.Empty);
            sim.AdvanceTo(6 * Hour);
            Assert.That(sim.DrainEvents().OfType<TruckArrived>().Count(), Is.EqualTo(1));
            Assert.That(sim.State.Market.TruckHere, Is.True);
            Assert.That(sim.State.Market.TraderBought(potato), Is.EqualTo(0));
            Assert.That(sim.State.Market.TruckLoadToday, Is.EqualTo(0));

            sim.AdvanceTo(Day - 1);
            Assert.That(sim.PendingEvents.OfType<TruckLeft>(), Is.Empty);
            sim.AdvanceTo(Day);
            TruckLeft left = sim.DrainEvents().OfType<TruckLeft>().Single();
            Assert.That(left.Reason, Is.EqualTo(TruckLeaveReason.Day));
            Assert.That(sim.State.Market.TruckHere, Is.False);
            Assert.That(Farm(9 * Hour).State.Market.TruckHere, Is.True, "a game started at 9:00: the truck is here");
        }

        // Spec 011 AC-2.
        [Test]
        public void Full_LeavesAndRefuses()
        {
            Simulation sim = Farm(9 * Hour, Load(500));
            int potato = G(sim, "potato");
            sim.State.Market.Stock[potato] = 1_000_000;
            Ok(sim, new SellToTrader(9 * Hour, "potato", 400_000));
            Assert.That(sim.PendingEvents.OfType<TruckLeft>(), Is.Empty);
            Ok(sim, new SellToTrader(9 * Hour, "potato", 100_000));
            TruckLeft left = sim.DrainEvents().OfType<TruckLeft>().Single();
            Assert.That((left.Reason, left.LoadGrams), Is.EqualTo((TruckLeaveReason.Full, 500_000L)));
            Assert.That(sim.State.Market.TruckHere, Is.False);

            ulong hash = sim.State.ComputeHash();
            Assert.That(sim.Execute(new SellToTrader(9 * Hour, "potato", 1_000)).Reason, Is.EqualTo(RejectReason.TraderAway));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "nothing changes");
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(0));

            sim.AdvanceTo(Day + 2 * Hour);
            Assert.That(sim.Execute(new SellToTrader(Day + 2 * Hour, "potato", 1_000)).Reason, Is.EqualTo(RejectReason.TraderAway), "between midnight and 6:00");
            sim.AdvanceTo(Day + 6 * Hour);
            Ok(sim, new SellToTrader(Day + 6 * Hour, "potato", 1_000));
        }

        // Spec 011 AC-3.
        [Test]
        public void Capacity_SharedLoad()
        {
            Simulation sim = Farm(9 * Hour, Load(1000));
            int potato = G(sim, "potato"), wheat = G(sim, "wheat");
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(1_000_000), "the load's room, under potato's 3,000 kg");
            sim.State.Market.Stock[potato] = 2_000_000;
            sim.State.Market.Stock[wheat] = 2_000_000;

            Ok(sim, new SellToTrader(9 * Hour, "potato", 300_000));
            AssertLoadIsCounts(sim);
            Assert.That(MarketRules.TraderCapacityLeft(sim, wheat), Is.EqualTo(700_000), "one load for every crop");

            // Overflow: a harvest that doesn't fit the store sells the rest to the truck.
            Assert.That(Store.Add(sim, potato, Store.Fits(sim, potato) + 100_000, Overflow.SellRestToTrader, "test"), Is.EqualTo(RejectReason.None));
            Assert.That(sim.State.Market.TruckLoadToday, Is.EqualTo(400_000));
            AssertLoadIsCounts(sim);
            Assert.That(Store.Add(sim, potato, 600_001, Overflow.SellRestToTrader, "test"), Is.EqualTo(RejectReason.TraderFull), "more than the load's room");

            // The trader holding the floor on the stall counts too.
            Ok(sim, new Offer(9 * Hour, "wheat", 1_900_000));
            sim.AdvanceTo(12 * Hour);
            Assert.That(sim.State.Market.TraderBought(wheat), Is.GreaterThan(0));
            AssertLoadIsCounts(sim);
            Assert.That(sim.State.Market.TruckLoadToday, Is.LessThanOrEqualTo(1_000_000));
        }

        // Spec 011 AC-4.
        [Test]
        public void Price_HeldAllDay()
        {
            Simulation sim = Farm(0);
            int goods = sim.Config.Game.Market.Goods.Count;
            foreach (long day in new[] { 0L, sim.Clock.SecondsPerSeason / Day }) // an ordinary day and the day a season starts
            {
                sim.AdvanceTo(day * Day + 6 * Hour);
                var floor = new Fixed[goods];
                for (int g = 0; g < goods; g++) floor[g] = MarketRules.Floor(sim, g);
                for (long t = day * Day + 6 * Hour; t < (day + 1) * Day; t += 20 * 60)
                {
                    sim.AdvanceTo(t);
                    for (int g = 0; g < goods; g++) Assert.That(MarketRules.Floor(sim, g), Is.EqualTo(floor[g]), "good " + g + " at " + t);
                }
            }
        }

        // Spec 011 AC-5.
        [Test]
        public void Overflow_WaitsWhileAway()
        {
            Simulation sim = FarmTest.Farm(FarmTest.AnySeason(NoSpoilage));
            Ok(sim, new SkipFirstSession(0));
            Ok(sim, new Sow(0, HomeTile, "radish", SeedSource.Certified, "none"));
            Crop c = sim.State.Farm.CropOn(HomeTile);
            long night = (c.ReadyAt / Day + 1) * Day + 2 * Hour;
            sim.AdvanceTo(night);
            Assert.That(sim.State.Market.TruckHere, Is.False);
            int wheat = G(sim, "wheat");
            sim.State.Market.Stock[wheat] += Store.Fits(sim, wheat); // a full store

            Assert.That(sim.Execute(new Harvest(night, HomeTile, Overflow.SellRestToTrader)).Reason, Is.EqualTo(RejectReason.TraderAway));
            Assert.That(sim.State.Farm.CropOn(HomeTile).State, Is.EqualTo(CropState.Ready), "it waits in the field");
        }

        // Spec 011 AC-5b.
        [Test]
        public void HandSellsHeldAtSix()
        {
            Simulation sim = Start(Game((f, j) => Load(50)(f, NoSpoilage(f, j))));
            Ok(sim, new SkipFirstSession(0));
            Hand h = HireHand(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new Sow(t, HomeTile, "wheat", SeedSource.Certified, "none"));
            Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(HomeTile, "wheat", SeedSource.Certified, "none", SellRule.Trader, Fixed.Zero)));
            int wheat = G(sim, "wheat");
            long ready = sim.State.Farm.CropOn(HomeTile).ReadyAt;
            long harvestAt = (ready + Hour - 1) / Hour * Hour; // the first hourly pass once it's ready
            // The truck is away when he harvests: what he stores is held.
            sim.AdvanceTo(harvestAt - 1);
            if (MarketRules.HourOfDay(sim) >= 6) sim.State.Market.TruckHere = false;
            sim.DrainEvents();
            sim.AdvanceTo(harvestAt);
            OrderExecuted ex = sim.DrainEvents().OfType<OrderExecuted>().First(e => e.Action == OrderAction.Harvested);
            Assert.That(sim.State.Market.HeldForTruckOf(wheat), Is.EqualTo(ex.Quantity), "all of it held");
            Assert.That(ex.Amount, Is.EqualTo(0), "nothing sold");

            // The player offers some of the stock first: the hold is cut to what's left off the stall.
            long free = sim.State.Market.Stock[wheat] - sim.State.Market.Offered[wheat];
            long held = sim.State.Market.HeldForTruckOf(wheat);
            Ok(sim, new Offer(harvestAt, "wheat", free - held / 2, Fixed.FromInt(1000))); // priced off the market: it stays in store
            Assert.That(sim.State.Market.HeldForTruckOf(wheat), Is.EqualTo(held / 2));

            // At 6:00 he sells it to the truck, up to its 50 kg load; the rest stays held.
            long six = (harvestAt / Day + (MarketRules.HourOfDay(sim) >= 6 ? 1 : 0)) * Day + 6 * Hour;
            sim.AdvanceTo(six - 1);
            held = sim.State.Market.HeldForTruckOf(wheat); // he may have harvested again overnight
            sim.AdvanceTo(six);
            HandSoldHeld sold = sim.DrainEvents().OfType<HandSoldHeld>().Single();
            Assert.That((sold.GoodId, sold.Quantity), Is.EqualTo(("wheat", 50_000L)));
            Assert.That(sold.Amount, Is.EqualTo(Money.Value(50_000, MarketRules.Floor(sim, wheat), 1000)), "the new day's price");
            Assert.That(sim.State.Market.HeldForTruckOf(wheat), Is.EqualTo(held - 50_000));
            Assert.That(sim.State.Market.TruckHere, Is.False, "the held wheat filled it");
        }

        // Spec 011 AC-16.
        [Test]
        public void FirstSale_AnyHour()
        {
            Simulation sim = NewFarm(localSeconds: 2 * Hour, game: RichGame());
            int spinach = G(sim, "spinach");
            sim.State.Market.Stock[spinach] = 4_000;
            Assert.That(sim.State.Market.TruckHere, Is.False);
            Ok(sim, new SellToTrader(2 * Hour, "spinach", 2_000));
            Assert.That(sim.State.Market.TruckLoadToday, Is.EqualTo(2_000), "the load still counts");

            int firstSale = sim.Config.Game.Onboarding.Steps.ToList().FindIndex(s => s.Id == "firstSale");
            sim.State.Guidance.Step = firstSale + 1;
            Assert.That(sim.Execute(new SellToTrader(2 * Hour, "spinach", 1_000)).Reason, Is.EqualTo(RejectReason.TraderAway));
        }
    }
}
