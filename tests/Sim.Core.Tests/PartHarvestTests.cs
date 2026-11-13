using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>ADR 0105 (owner, 2026-11-12): a ready field crop can be harvested in parts, up to what the store holds; the rest stands ready.</summary>
    public partial class PartHarvestTests
    {
        private const long Hour = 3600;

        /// <summary>A farm with a ready radish on tile 85 and the store filled until <paramref name="roomGrams"/> is free.</summary>
        private static Simulation ReadyCrop(long roomGrams, out int radish)
        {
            Simulation sim = FarmTest.Farm();
            Assert.That(sim.Execute(new Sow(0, 85, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(2 * Hour);
            Assert.That(FarmTest.Crop(sim, 85).State, Is.EqualTo(CropState.Ready));
            radish = FarmTest.Good(sim, "radish");
            Squeeze(sim, roomGrams);
            sim.DrainEvents();
            return sim;
        }

        /// <summary>Fills the store with potato until exactly <paramref name="roomGrams"/> of room is left.</summary>
        private static void Squeeze(Simulation sim, long roomGrams)
        {
            int potato = FarmTest.Good(sim, "potato");
            long free = Store.CapacityGrams(sim) - Store.UsedGrams(sim);
            sim.State.Market.Stock[potato] += free - roomGrams;
            Assert.That(Store.Fits(sim, FarmTest.Good(sim, "radish")), Is.EqualTo(roomGrams));
        }

        [Test]
        public void TakesWhatFits_LeavesTheRest()
        {
            Simulation sim = ReadyCrop(20_000, out int radish);
            Crop c = FarmTest.Crop(sim, 85);
            long grams = c.HarvestGrams;
            Assert.That(grams, Is.GreaterThan(20_000));
            long before = sim.State.Market.InStore(radish);
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85)).Reason, Is.EqualTo(RejectReason.StoreFull), "the old way still needs all of it");

            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85, Overflow.StorePart)).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(radish) - before, Is.EqualTo(20_000));
            c = FarmTest.Crop(sim, 85);
            Assert.That(c, Is.Not.Null, "the rest stands in the field");
            Assert.That(c.State, Is.EqualTo(CropState.Ready));
            Assert.That((c.TakenGrams, c.HarvestGrams), Is.EqualTo((20_000L, grams - 20_000)));
            Assert.That(sim.State.Land.Tiles[85].Occupied, Is.True);
            CropHarvested e = sim.DrainEvents().OfType<CropHarvested>().Single();
            Assert.That(e.Grams, Is.EqualTo(20_000));

            // Nothing fits now: rejected, nothing changes.
            Assert.That(Store.Fits(sim, radish), Is.EqualTo(0));
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85, Overflow.StorePart)).Reason, Is.EqualTo(RejectReason.StoreFull));
            Assert.That(FarmTest.Crop(sim, 85).TakenGrams, Is.EqualTo(20_000));

            // Room again: the rest comes in and the field is free.
            sim.State.Market.Stock[FarmTest.Good(sim, "potato")] = 0; // the potatoes are sold: room again
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85, Overflow.StorePart)).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(radish) - before, Is.EqualTo(grams), "all of it, once");
            Assert.That(FarmTest.Crop(sim, 85), Is.Null);
            Assert.That(sim.State.Land.Tiles[85].Occupied, Is.False);
        }

        // When all of it fits, StorePart is exactly the ordinary harvest (same town, same hash).
        [Test]
        public void AllFits_SameAsOrdinaryHarvest()
        {
            Simulation a = ReadyCrop(10_000_000, out _);
            Simulation b = ReadyCrop(10_000_000, out _);
            Assert.That(a.Execute(new Harvest(a.State.Clock.SimTime, 85)).Accepted, Is.True);
            Assert.That(b.Execute(new Harvest(b.State.Clock.SimTime, 85, Overflow.StorePart)).Accepted, Is.True);
            Assert.That(b.State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()));
        }

        // What waits in the field spoils after the grace like any ready crop; what was taken is not counted as spoiled.
        [Test]
        public void RestSpoilsAsUsual()
        {
            Simulation sim = ReadyCrop(20_000, out _);
            long total = FarmTest.Crop(sim, 85).Breakdown.Grams;
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85, Overflow.StorePart)).Accepted, Is.True);
            sim.AdvanceTo(sim.State.Clock.SimTime + 200 * Hour);
            Crop c = FarmTest.Crop(sim, 85);
            Assert.That(c.SpoiledGrams, Is.GreaterThan(0));
            Assert.That(c.HarvestGrams + c.SpoiledGrams + c.TakenGrams, Is.EqualTo(total));
        }

        // The part-harvested crop is saved (schema 17), reads back to the same hash, and a town without one hashes as before.
        [Test]
        public void SavesAndReads()
        {
            Simulation sim = ReadyCrop(20_000, out _);
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 85, Overflow.StorePart)).Accepted, Is.True);
            SimConfig config = SaveTests.Config();
            byte[] bytes = SaveCodec.Write(sim.State, config, Wall0);
            LoadResult r = SaveCodec.Read(bytes, config, SaveCodec.SchemaVersion);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Farm.CropOn(85).TakenGrams, Is.EqualTo(20_000));
        }
    }
}
