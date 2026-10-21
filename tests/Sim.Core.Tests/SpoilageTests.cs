using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class StoreTests
    {
        // Spec 004 AC-13: round(q × rate) a day, stock not on offer first, GoodsSpoiled; inputs expensed at average cost.
        [Test]
        public void Spoilage_Daily()
        {
            Simulation sim = NewFarm(game: Quiet());
            int radish = Good(sim, "radish"), seed = Good(sim, "seed_potato");
            sim.State.Market.Stock[radish] = 100_000;
            Assert.That(sim.Execute(new Offer(0, "radish", 60_000, Maths.Fixed.FromInt(1000))).Accepted, Is.True); // never sells
            long seedBook = sim.State.Market.StockCostOf(seed);
            sim.DrainEvents();

            sim.AdvanceTo(Day);
            Assert.That(sim.State.Market.InStore(radish), Is.EqualTo(75_000));
            Assert.That(sim.State.Market.OnOffer(radish), Is.EqualTo(60_000), "stock not on offer goes first");
            var spoiled = sim.DrainEvents().OfType<GoodsSpoiled>().ToList();
            Assert.That(spoiled.Single(e => e.GoodId == "radish").Quantity, Is.EqualTo(25_000));
            Assert.That(spoiled.Single(e => e.GoodId == "radish").Source, Is.EqualTo(SpoilSource.Store));

            // 600 kg of seed potato × 0.001 = 600 g, expensed at average cost to 5000 (Dr), 1200 (Cr).
            Assert.That(spoiled.Single(e => e.GoodId == "seed_potato").Quantity, Is.EqualTo(600));
            Assert.That(sim.State.Ledger.Balance(Account.SeedsAndPlanting), Is.EqualTo(seedBook - sim.State.Market.StockCostOf(seed)));
            Assert.That(seedBook - sim.State.Market.StockCostOf(seed), Is.EqualTo(13_20)); // 600 g of 600 kg booked at 13,200

            for (long d = 2; d <= 4; d++) sim.AdvanceTo(d * Day);
            // 100 → 75 → 56.25 → 42.187 → 31.640 kg (the day's loss rounded to the gram); the offer now holds what's left.
            Assert.That(sim.State.Market.InStore(radish), Is.EqualTo(31_640));
            Assert.That(sim.State.Market.OnOffer(radish), Is.EqualTo(31_640));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }
    }

    public partial class CropTests
    {
        // Spec 003 AC-38 (ADR 0034): ready crops and ripe fruit wait 72 h without loss, then spoil daily.
        [Test]
        public void ReadyGrace_ThenSpoils()
        {
            Simulation sim = FarmTest.Farm();
            Assert.That(sim.Execute(new Sow(0, 85, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(2 * Hour);
            Crop c = FarmTest.Crop(sim, 85);
            long grams = c.HarvestGrams;
            sim.DrainEvents();

            // Daily passes at 24, 48 and 72 h fall inside the grace (ready at 2 h); the one at 96 h is past it.
            sim.AdvanceTo(72 * Hour);
            Assert.That(c.HarvestGrams, Is.EqualTo(grams));
            Assert.That(sim.DrainEvents().OfType<GoodsSpoiled>().Count(e => e.Source == SpoilSource.Field), Is.EqualTo(0));
            sim.AdvanceTo(96 * Hour);
            long lost = sim.Config.Game.Market.Goods[FarmTest.Good(sim, "radish")].SpoilagePerDay.MulRatio(grams, 1).RoundToLong();
            Assert.That(c.HarvestGrams, Is.EqualTo(grams - lost));
            GoodsSpoiled e = sim.DrainEvents().OfType<GoodsSpoiled>().Single(x => x.Source == SpoilSource.Field);
            Assert.That((e.GoodId, e.Quantity, e.Where), Is.EqualTo(("radish", lost, 85L)));
            int radish = FarmTest.Good(sim, "radish");
            long before = sim.State.Market.InStore(radish);
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85)).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(radish) - before, Is.EqualTo(grams - lost));

            // An away advance of 72 h from readiness loses nothing.
            Simulation away = FarmTest.Farm();
            Assert.That(away.Execute(new Sow(0, 85, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            away.AdvanceTo(2 * Hour);
            long g = FarmTest.Crop(away, 85).HarvestGrams;
            away.AdvanceTo(74 * Hour, Time.AdvanceMode.Away);
            Assert.That(FarmTest.Crop(away, 85).HarvestGrams, Is.EqualTo(g));

            // A ripe mango batch spoils after its grace too, and a batch rotted to under 1 kg is gone (the tree can fruit again).
            Simulation t = FarmTest.Farm();
            long slot = t.State.Land.Slots.First(s => s.Kind == Land.SlotKind.Yard).Id;
            Assert.That(t.Execute(new PlantTree(0, slot, "mango")).Accepted, Is.True);
            Tree mango = t.State.Farm.TreeIn(slot);
            t.AdvanceTo(9 * FarmTest.S); // Summer: a 120 kg batch
            Assert.That(mango.RipeGrams, Is.EqualTo(120_000));
            t.AdvanceTo(9 * FarmTest.S + 4 * Day); // ripe at 9S; the passes at +1 to +3 days are within the 72 h, +4 is past it
            Assert.That(mango.RipeGrams, Is.EqualTo(96_000));
            t.AdvanceTo(13 * FarmTest.S); // a year later: rotted away
            Assert.That(mango.RipeGrams, Is.EqualTo(120_000), "the old batch rotted to nothing, so the next Summer's batch came");
        }
    }
}
