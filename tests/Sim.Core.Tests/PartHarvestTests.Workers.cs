using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>ADR 0111 (US-241, spec 006 AC-27, AC-28): hands and labourers harvest in parts.</summary>
    public partial class PartHarvestTests
    {
        private static void Room(Simulation sim, long roomGrams)
        {
            int potato = FarmTest.Good(sim, "potato");
            long free = Store.CapacityGrams(sim) - Store.UsedGrams(sim);
            sim.State.Market.Stock[potato] += free - roomGrams;
        }

        /// <summary>A hand with a keep-wheat order on a sown tile; <paramref name="ready"/> is the hour the wheat ripens.</summary>
        private static Simulation HandOnWheat(SellRule rule, string min, out long ready)
        {
            Simulation sim = Start(Game(NoSpoilage));
            Hand h = HireHand(sim);
            while (P(sim).Applicants.Count > 0) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Ok(sim, new SetWorkHours(sim.State.Clock.SimTime, "normal"));
            Ok(sim, new Sow(sim.State.Clock.SimTime, HomeTile, "wheat", SeedSource.Certified, "none"));
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.KeepCrop(HomeTile, "wheat", SeedSource.Certified, "none", rule, Fixed.Parse(min))));
            ready = (sim.State.Farm.CropOn(HomeTile).ReadyAt + 3599) / 3600 * 3600;
            return sim;
        }

        // Spec 006 AC-27: a hand's order takes what fits each hour; the rest stands; he waits when nothing fits; a part is one action.
        [Test]
        public void Hand_TakesWhatFits()
        {
            Simulation sim = HandOnWheat(SellRule.Store, "0", out long ready);
            int wheat = FarmTest.Good(sim, "wheat");
            Room(sim, 30_000);
            sim.DrainEvents();
            sim.AdvanceTo(ready + 3600);
            Crop c = sim.State.Farm.CropOn(HomeTile);
            Assert.That(c, Is.Not.Null, "the rest stands");
            Assert.That(c.TakenGrams, Is.EqualTo(30_000));
            Assert.That(sim.PendingEvents.OfType<OrderExecuted>().First(x => x.Action == OrderAction.Harvested).Quantity, Is.EqualTo(30_000), "the part, not the whole yield");
            Assert.That(sim.State.Market.Offered[wheat], Is.Zero);

            // Nothing fits now: he waits, takes nothing, sells nothing.
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * 3600);
            Assert.That(sim.State.Farm.CropOn(HomeTile).TakenGrams, Is.EqualTo(30_000));
            Assert.That(P(sim).Orders.Single().Waiting, Is.EqualTo(OrderWait.StoreFull));
            Assert.That(sim.PendingEvents.OfType<SoldToTrader>().Count(), Is.Zero, "nothing sold to the trader");

            // Room appears: the next hour takes the rest.
            Room(sim, 10_000_000);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 3600);
            Crop next = sim.State.Farm.CropOn(HomeTile);
            Assert.That(next == null || next.State == CropState.Growing, Is.True, "the rest came in (and he re-sowed)");
        }

        // ADR 0111: an Offer-at-a-price order never sells its overflow to the trader below its price.
        [Test]
        public void Hand_OfferOverflowIsNotSold()
        {
            Simulation sim = HandOnWheat(SellRule.Offer, "99", out long ready);
            Room(sim, 30_000);
            sim.DrainEvents();
            sim.AdvanceTo(ready + 3600);
            Assert.That(sim.State.Farm.CropOn(HomeTile).TakenGrams, Is.EqualTo(30_000));
            Assert.That(sim.PendingEvents.OfType<SoldToTrader>().Count(), Is.Zero, "nothing went to the trader");
        }

        // Spec 006 AC-28: hired labourers take parts, and with nothing fitting they stay their paid time, no refund, the field waits.
        [Test]
        public void Labourers_StayWhenFull()
        {
            Simulation sim = Start(Game(NoSpoilage));
            long t = sim.State.Clock.SimTime;
            Ok(sim, new Sow(t, HomeTile, "radish", SeedSource.Certified, "none"));
            Ok(sim, new Sow(t, HomeTile + 1, "wheat", SeedSource.Certified, "none")); // the later crop keeps them hired
            Ok(sim, new HireLabourers(t, 2));
            long until = PeopleRules.LabourUntil(sim);
            long radishReady = (sim.State.Farm.CropOn(HomeTile).ReadyAt + 3599) / 3600 * 3600;
            Assert.That(until, Is.GreaterThan(radishReady + 3600));
            sim.AdvanceTo(radishReady - 1);
            Room(sim, 20_000);
            sim.DrainEvents();
            sim.AdvanceTo(radishReady);
            LabourersHarvested e = sim.PendingEvents.OfType<LabourersHarvested>().Single();
            Assert.That(e.Grams, Is.EqualTo(20_000));
            Assert.That(sim.State.Farm.CropOn(HomeTile).TakenGrams, Is.EqualTo(20_000));

            // Nothing fits at the next passes: they take nothing, are not sent home and not refunded; the field waits.
            sim.DrainEvents();
            sim.AdvanceTo(radishReady + 3 * 3600);
            Assert.That(sim.PendingEvents.OfType<LabourersHarvested>().Count(), Is.Zero);
            Assert.That(sim.PendingEvents.OfType<LabourersLeft>().Count(), Is.Zero);
            Assert.That(PeopleRules.DayLabourHours(sim), Is.GreaterThan(Fixed.Zero), "their hours stay in the Care pool");
            Assert.That(sim.PendingEvents.OfType<SoldToTrader>().Count(), Is.Zero, "nothing sold; no refund is booked");
            Assert.That(sim.State.Farm.CropOn(HomeTile), Is.Not.Null, "the field waits");

            // Room appears while they are still hired: the rest comes in at the next hourly pass.
            Room(sim, 10_000_000);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 3600);
            Assert.That(sim.State.Farm.CropOn(HomeTile), Is.Null, "taken as room appeared");
        }
    }
}
