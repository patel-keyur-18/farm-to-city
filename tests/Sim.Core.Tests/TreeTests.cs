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
    public partial class TreeTests
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
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(60_00));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(60_00));
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
            Assert.That(Reporting.NetWorth(sim).TreesAtBook, Is.EqualTo(60_00));

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
            Simulation sim = FarmTest.Farm(FarmTest.AnySeason(TestSim.NoSpoilage)); // waiting fruit spoils after the grace: AC-38
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
            Assert.That(spent.BookValue, Is.EqualTo(60_00));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Entries.Single(e => e.DescriptionKey == "ledger.treeRemoved").Postings
                .Single(p => p.Account == Account.LossOnAssetSales).Amount, Is.EqualTo(60_00));

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

namespace Sim.Core.Tests
{
    using NUnit.Framework;
    using Sim.Core.Commands;
    using Sim.Core.Farm;

    public partial class TreeTests
    {
        // Spec 003 AC-27 (ADR 0034): the payback season for a tree already paid back and for one not yet.
        [Test]
        public void Payback()
        {
            Simulation sim = FarmTest.Farm(FarmTest.AnySeason(TestSim.NoSpoilage));
            long yard = sim.State.Land.Slots.First(s => s.Kind == Land.SlotKind.Yard).Id;
            Assert.That(sim.Execute(new PlantTree(0, yard, "mango")).Accepted, Is.True);
            Tree mango = sim.State.Farm.TreeIn(yard);

            // Young: matures on day 24 (season 6, a Monsoon); its first batch is the next Summer, season 9, which pays
            // the 350 back many times over.
            TreePayback young = TreeRules.Payback(sim, mango);
            Assert.That((young.Cost, young.Returns, young.PaidBack, young.Season), Is.EqualTo((350_00L, 0L, false, (long?)9)));

            sim.AdvanceTo(9 * FarmTest.S);
            Assert.That(TreeRules.Payback(sim, mango).Season, Is.EqualTo(9), "the batch is waiting this season");
            sim.AdvanceTo(9 * FarmTest.S + TestSim.Day);
            Assert.That(sim.Execute(new PickFruit(sim.State.Clock.SimTime, yard)).Accepted, Is.True);
            TreePayback paid = TreeRules.Payback(sim, mango);
            Assert.That((paid.PaidBack, paid.Season), Is.EqualTo((true, (long?)9)));
            Assert.That(paid.Returns, Is.GreaterThan(paid.Cost));

            // A banana whose batch is worth less than its cost needs two batches; with one batch left it never pays back.
            Simulation b = FarmTest.Farm(FarmTest.AnySeason((f, j) => f == "goods.json"
                ? j.Replace("\"id\": \"sapling_banana\", \"nameKey\": \"good.sapling_banana\", \"kind\": \"input\", \"unit\": \"each\", \"basePrice\": 60",
                            "\"id\": \"sapling_banana\", \"nameKey\": \"good.sapling_banana\", \"kind\": \"input\", \"unit\": \"each\", \"basePrice\": 400")
                : TestSim.NoSpoilage(f, j)));
            long slot = b.State.Land.Slots.First(s => s.Kind == Land.SlotKind.Yard).Id;
            Assert.That(b.Execute(new PlantTree(0, slot, "banana")).Accepted, Is.True);
            Tree banana = b.State.Farm.TreeIn(slot);
            b.AdvanceTo(7 * TestSim.Day + 1); // matures in Summer (season 1) with a batch at once: about 228 a batch
            TreePayback two = TreeRules.Payback(b, banana);
            Assert.That(two.Season, Is.EqualTo(2), "this season's batch and the next");
            banana.Batches = 3; // one batch left of 4
            Assert.That(TreeRules.Payback(b, banana).Season, Is.Null, "spent before it pays back");
        }
    }
}
