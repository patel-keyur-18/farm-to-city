using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class SeedTests
    {
        /// <summary>No inherited seed age and no eating from the store, so saved seed comes only from these harvests.</summary>
        private static Simulation Fresh()
        {
            return FarmTest.Farm(FarmTest.AnySeason((f, j) =>
                f == "start.json" ? j.Replace("\"savedSeedAge\": { \"wheat\": 2.0, \"lentils\": 2.0 }", "\"savedSeedAge\": {}")
                : f == "founder.json" ? j.Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false") : j));
        }

        // Spec 003 AC-17.
        [Test]
        public void SavedSeed_AgesPerSeason()
        {
            Simulation sim = Fresh();
            FarmDefs defs = sim.Config.Game.Farm;
            int wheat = defs.IndexOfCrop("wheat"), potato = defs.IndexOfCrop("potato");
            int wheatGood = FarmTest.Good(sim, "wheat");

            // Wheat: one certified cycle, then saved cycles; each harvest adds cycleShare 0.0625 to the age.
            long t = 0;
            for (int k = 0; k < 16; k++)
            {
                SeedSource source = k == 0 ? SeedSource.Certified : SeedSource.Saved;
                long before = sim.State.Market.InStore(wheatGood);
                Assert.That(sim.Execute(new Sow(t, 18, "wheat", source, "none")).Accepted, Is.True, "cycle " + k);
                if (source == SeedSource.Saved) Assert.That(before - sim.State.Market.InStore(wheatGood), Is.EqualTo(1688)); // 1.6875 kg
                t += 6 * Hour;
                Assert.That(sim.Execute(new Harvest(t, 18, Overflow.SellRestToTrader)).Accepted, Is.True);
            }
            Assert.That(sim.State.Farm.SavedSeedAgeOf(wheat), Is.EqualTo(Fixed.One));
            Assert.That(sim.Execute(new Sow(t, 18, "wheat", SeedSource.Saved, "none")).Accepted, Is.True);
            Crop c = FarmTest.Crop(sim, 18);
            Assert.That(c.SeedAge, Is.EqualTo(Fixed.One));
            FarmTest.AssertNear(FarmRules.SeedFactor(defs.Crops[wheat], c.Seed, c.SeedAge), "0.98", 4);

            // Potato: 8 cycles of 0.25 = age 2.0, factor 0.90.
            for (int k = 0; k < 8; k++)
            {
                Assert.That(sim.Execute(new Sow(t, 19, "potato", k == 0 ? SeedSource.Certified : SeedSource.Saved, "none")).Accepted, Is.True, "potato " + k);
                t += Day;
                Assert.That(sim.Execute(new Harvest(t, 19, Overflow.SellRestToTrader)).Accepted, Is.True);
            }
            Assert.That(sim.State.Farm.SavedSeedAgeOf(potato), Is.EqualTo(Fixed.FromInt(2)));
            FarmTest.AssertNear(FarmRules.SeedFactor(defs.Crops[potato], SeedSource.Saved, Fixed.FromInt(2)), "0.90", 4);

            // The floors.
            Assert.That(FarmRules.SeedFactor(defs.Crops[wheat], SeedSource.Saved, Fixed.FromInt(9)), Is.EqualTo(defs.Crops[wheat].SavedSeedFloor));
            Assert.That(FarmRules.SeedFactor(defs.Crops[potato], SeedSource.Saved, Fixed.FromInt(9)), Is.EqualTo(defs.Crops[potato].SavedSeedFloor));
            Assert.That(sim.Execute(new Sow(t, 26, "radish", SeedSource.Saved, "none")).Reason, Is.EqualTo(RejectReason.SeedCannotBeSaved));
        }

        // ADR 0032: the inherited grain sows as saved seed of age 2.0 (factor 0.96).
        [Test]
        public void InheritedSeed_Age()
        {
            Simulation sim = FarmTest.Farm();
            Assert.That(sim.Execute(new Sow(0, 18, "lentils", SeedSource.Saved, "none")).Accepted, Is.True);
            Crop c = FarmTest.Crop(sim, 18);
            Assert.That(c.SeedAge, Is.EqualTo(Fixed.FromInt(2)));
            FarmTest.AssertNear(FarmRules.SeedFactor(sim.Config.Game.Farm.Crops[c.Def], c.Seed, c.SeedAge), "0.96", 4);
        }
    }

    public class GardenTests
    {
        // Spec 003 AC-32.
        [Test]
        public void SeasonOutput_AndRest()
        {
            Simulation sim = FarmTest.Farm();
            int radish = FarmTest.Good(sim, "radish");
            long t = 0;
            for (int k = 0; k < 20; k++)
            {
                Assert.That(sim.Execute(new SowGarden(t, 1, "garden_radish")).Accepted, Is.True, "sowing " + k);
                t += 10 * 60;
                Assert.That(sim.Execute(new PickGarden(t, 1)).Accepted, Is.True);
            }
            Assert.That(sim.State.Market.InStore(radish), Is.EqualTo(100_000));
            Assert.That(sim.State.Farm.Beds[0].RemainingGrams, Is.EqualTo(0));
            Assert.That(sim.Execute(new SowGarden(t, 1, "garden_radish")).Reason, Is.EqualTo(RejectReason.BedRested));
            Assert.That(sim.Execute(new SowGarden(t, 2, "garden_radish")).Accepted, Is.True, "the other bed has its own output");

            sim.AdvanceTo(FarmTest.S);
            Assert.That(sim.State.Farm.Beds[0].RemainingGrams, Is.EqualTo(100_000));
            Assert.That(sim.Execute(new SowGarden(FarmTest.S, 1, "garden_radish")).Accepted, Is.True);

            // The last picking takes only what's left: coriander (1.5 kg) on a bed with 1 kg left.
            Simulation s2 = FarmTest.Farm();
            s2.State.Farm.Beds[0].RemainingGrams = 1_000;
            Assert.That(s2.Execute(new SowGarden(0, 1, "garden_coriander")).Accepted, Is.True);
            s2.DrainEvents();
            Assert.That(s2.Execute(new PickGarden(5 * 60, 1)).Accepted, Is.True);
            GardenPicked picked = s2.DrainEvents().OfType<GardenPicked>().Single();
            Assert.That((picked.Grams, picked.RemainingGrams), Is.EqualTo((1_000L, 0L)));
        }

        // Spec 003 §3.13 Why?: "Your garden saved N coins of groceries this season" (daily eating, ADR 0032).
        [Test]
        public void GroceriesSaved()
        {
            Simulation sim = NewFarm(game: RichGame());
            Assert.That(sim.Execute(new SowGarden(0, 1, "garden_spinach")).Accepted, Is.True);
            Assert.That(sim.Execute(new PickGarden(15 * 60, 1)).Accepted, Is.True);
            sim.AdvanceTo(Day);
            // The daily pass spoils 35% of the 4 kg first (spec 004 §3.8: spoilage, then eating); spinach spoils fastest, so
            // the 2.6 kg left is eaten first, at the Spring floor 20 × 1.10 − 1.50 = 20.50.
            Assert.That(sim.State.Market.InStore(FarmTest.Good(sim, "spinach")), Is.EqualTo(0));
            Assert.That(sim.State.Farm.Founder.GardenHomeUseSeason, Is.EqualTo(53_30));
            Assert.That(sim.State.Farm.Founder.HomeUseSeason, Is.GreaterThan(53_30));
            sim.AdvanceTo(FarmTest.S);
            Assert.That(sim.State.Farm.Founder.GardenHomeUseSeason, Is.EqualTo(0), "reset for the new season");
            Assert.That(sim.State.Finance.Reports.Single().GardenHomeUse, Is.EqualTo(53_30));
        }

        // Spec 003 AC-33.
        [Test]
        public void Grow_Pick_NoLabour()
        {
            Simulation sim = FarmTest.Farm();
            Assert.That(sim.Execute(new Sow(0, 18, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            Fixed care = sim.State.Farm.CareRatio;
            long cash = sim.State.Ledger.Cash;

            Assert.That(sim.Execute(new SowGarden(0, 1, "garden_spinach")).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash), "the inherited garden seed pays for it");
            Assert.That(sim.State.Farm.CareRatio, Is.EqualTo(care), "no labour demand");
            Assert.That(sim.Execute(new SowGarden(0, 1, "garden_radish")).Reason, Is.EqualTo(RejectReason.BedBusy));
            Assert.That(sim.Execute(new PickGarden(15 * 60 - 1, 1)).Reason, Is.EqualTo(RejectReason.NotReady));
            sim.AdvanceTo(15 * 60);
            Assert.That(sim.State.Farm.Beds[0].State, Is.EqualTo(BedState.Ready));
            sim.AdvanceTo(Day); // waits
            Assert.That(sim.State.Farm.Beds[0].State, Is.EqualTo(BedState.Ready));
            Assert.That(sim.Execute(new PickGarden(Day, 1)).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(FarmTest.Good(sim, "spinach")), Is.EqualTo(4_000));
            Assert.That(sim.Execute(new SowGarden(Day, 9, "garden_spinach")).Reason, Is.EqualTo(RejectReason.UnknownBed));
            Assert.That(sim.Execute(new SowGarden(Day, 1, "garden_kale")).Reason, Is.EqualTo(RejectReason.UnknownCrop));

            // Coriander grows in exactly 5 minutes.
            Assert.That(sim.Execute(new SowGarden(Day, 2, "garden_coriander")).Accepted, Is.True);
            sim.AdvanceTo(Day + 5 * 60 - 1);
            Assert.That(sim.State.Farm.Beds[1].State, Is.EqualTo(BedState.Growing));
            sim.AdvanceTo(Day + 5 * 60);
            Assert.That(sim.State.Farm.Beds[1].State, Is.EqualTo(BedState.Ready));
        }
    }
}
