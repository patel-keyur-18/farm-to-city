using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class TreeTests
    {
        private const long S = FarmTest.S;

        /// <summary>The first yard slot of the hut (after the 12 field-edge slots of the 6 inherited acres).</summary>
        private static long YardSlot(Simulation sim) { return sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard).Id; }

        private static TreeSlot EdgeSlot(Simulation sim, int tile) { return sim.State.Land.Slots.First(s => s.Kind == SlotKind.FieldEdge && s.TileId == tile); }

        // Spec 003 AC-24.
        [Test]
        public void Plant_Mature_Shade()
        {
            Simulation sim = FarmTest.Farm();
            TreeSlot edge = EdgeSlot(sim, 18);
            Tile tile = sim.State.Land.Tiles[18];
            Fixed shade0 = LandRules.ShadeFactor(sim, tile);
            long cash = sim.State.Ledger.Cash;

            Assert.That(sim.Execute(new PlantTree(0, edge.Id, "banana")).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(150_00));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(150_00));
            Assert.That(edge.TreeId, Is.Not.EqualTo(0));
            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(shade0), "saplings cast no shade");
            Assert.That(sim.Execute(new PlantTree(0, edge.Id, "mango")).Reason, Is.EqualTo(RejectReason.SlotOccupied));
            Assert.That(sim.Execute(new PlantTree(0, 999, "mango")).Reason, Is.EqualTo(RejectReason.UnknownSlot));
            Assert.That(sim.Execute(new PlantTree(0, YardSlot(sim), "apple")).Reason, Is.EqualTo(RejectReason.UnknownTree));

            long matures = 10_080 * 60L;
            sim.AdvanceTo(matures - 1);
            Assert.That(sim.State.Farm.TreeIn(edge.Id).State, Is.EqualTo(TreeState.Growing));
            sim.DrainEvents();
            sim.AdvanceTo(matures);
            Assert.That(sim.State.Farm.TreeIn(edge.Id).State, Is.EqualTo(TreeState.Mature));
            Assert.That(sim.DrainEvents().OfType<TreeMatured>().Count(), Is.EqualTo(1));
            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(Fixed.One - sim.Config.Game.Land.ShadePerMatureTree));

            // Net worth counts the tree at book value (spec 005 AC-14's tree term).
            Assert.That(Reporting.NetWorth(sim).TreesAtBook, Is.EqualTo(150_00));

            var hill = Content.ContentLoader.LoadGame(f => f == "trees.json"
                ? TestContent.ReadContent(f).Replace("\"terrain\": \"any\" },\n    { \"id\": \"papaya\"", "\"terrain\": \"hill\" },\n    { \"id\": \"papaya\"")
                : TestContent.ReadContent(f), TestContent.Weather());
            Simulation hills = NewFarm(game: hill);
            Assert.That(hills.Execute(new PlantTree(0, YardSlot(hills), "banana")).Reason, Is.EqualTo(RejectReason.TerrainNotAllowed));
        }

        // Spec 003 AC-25.
        [Test]
        public void Fruiting_OnePerSeason()
        {
            Simulation sim = FarmTest.Farm();
            long yard = YardSlot(sim);
            Assert.That(sim.Execute(new PlantTree(0, yard, "mango")).Accepted, Is.True);
            Tree mango = sim.State.Farm.TreeIn(yard);
            sim.AdvanceTo(6 * S); // matures at day 24, the start of a Monsoon
            Assert.That(mango.State, Is.EqualTo(TreeState.Mature));
            Assert.That(mango.RipeGrams, Is.EqualTo(0), "Monsoon: no mango");
            sim.AdvanceTo(9 * S - 1);
            Assert.That(mango.RipeGrams, Is.EqualTo(0));
            sim.AdvanceTo(9 * S); // Summer
            Assert.That(mango.RipeGrams, Is.EqualTo(120_000));
            sim.AdvanceTo(13 * S); // the next Summer: the first batch is still unpicked, so no second
            Assert.That(mango.RipeGrams, Is.EqualTo(120_000));

            int good = FarmTest.Good(sim, "mango");
            long floorValue = Market.MarketRules.Value(sim, good, 120_000, Market.MarketRules.Floor(sim, good));
            Assert.That(sim.Execute(new PickFruit(13 * S, yard)).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(good), Is.EqualTo(120_000));
            Assert.That(mango.Returns, Is.EqualTo(floorValue));
            Assert.That(sim.Execute(new PickFruit(13 * S, yard)).Reason, Is.EqualTo(RejectReason.NoRipeFruit));
            sim.AdvanceTo(16 * S);
            Assert.That(mango.RipeGrams, Is.EqualTo(0), "Spring, Monsoon, Winter: none");
            sim.AdvanceTo(17 * S);
            Assert.That(mango.RipeGrams, Is.EqualTo(120_000));

            // A banana matures mid-Summer (day 7) and fruits at once, then once every season while picked.
            Simulation b = FarmTest.Farm();
            long slot = YardSlot(b);
            Assert.That(b.Execute(new PlantTree(0, slot, "banana")).Accepted, Is.True);
            b.AdvanceTo(7 * Day);
            Tree banana = b.State.Farm.TreeIn(slot);
            Assert.That(banana.RipeGrams, Is.EqualTo(8_000));
            Assert.That(b.Execute(new PickFruit(7 * Day, slot)).Accepted, Is.True);
            b.AdvanceTo(8 * Day); // Monsoon starts
            Assert.That(banana.RipeGrams, Is.EqualTo(8_000));
        }

        // Spec 003 AC-26.
        [Test]
        public void Lifespan_AndRemoval()
        {
            Simulation sim = FarmTest.Farm();
            long slot = YardSlot(sim);
            Assert.That(sim.Execute(new PlantTree(0, slot, "banana")).Accepted, Is.True);
            long t = 7 * Day;
            for (int k = 0; k < 4; k++)
            {
                sim.AdvanceTo(t);
                sim.DrainEvents();
                Assert.That(sim.Execute(new PickFruit(t, slot)).Accepted, Is.True, "batch " + k);
                t = (t / S + 1) * S; // the next season start
            }
            Assert.That(sim.State.Farm.TreeIn(slot), Is.Null);
            Assert.That(sim.State.Land.Slots.Single(s => s.Id == slot).TreeId, Is.EqualTo(0));
            TreeSpent spent = sim.DrainEvents().OfType<TreeSpent>().Single();
            Assert.That(spent.BookValue, Is.EqualTo(150_00));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Entries.Single(e => e.DescriptionKey == "ledger.treeRemoved").Postings
                .Single(p => p.Account == Account.LossOnAssetSales).Amount, Is.EqualTo(150_00));

            // RemoveTree at any time, even a sapling: same accounting; its maturity job is cancelled.
            Assert.That(sim.Execute(new PlantTree(t, slot, "guava")).Accepted, Is.True);
            Assert.That(sim.Execute(new RemoveTree(t + 60, slot)).Accepted, Is.True);
            Assert.That(sim.State.Farm.TreeIn(slot), Is.Null);
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(0));
            Assert.That(sim.UpcomingJobs(t + 30 * Day).Any(j => j.Type == FarmSystem.TreeJob), Is.False);
            Assert.That(sim.Execute(new RemoveTree(t + 60, slot)).Reason, Is.EqualTo(RejectReason.NoTree));
            sim.AdvanceTo(t + 30 * Day);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }
    }
}
