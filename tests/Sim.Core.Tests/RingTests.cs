using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-253 (spec 025 §3.4, ADR 0125): the 24 × 18 map, the ring locked until the Township, its lanes and the stable tile ids.</summary>
    public class RingTests
    {
        private static Simulation Start()
        {
            Simulation sim = NewFarm(game: Game(), options: new SimOptions { DevCommandsEnabled = true });
            TreasuryTests.Era(sim, "village");
            sim.DrainEvents();
            return sim;
        }

        private static void Gift(Simulation sim, long amount)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
        }

        // AC-8: the map is 24 × 18, the old map's tiles keep their ids and move four columns east and three rows south, the ring follows.
        [Test]
        public void MapStandsInTheRing()
        {
            MapDef map = Start().Config.Game.Map;
            Assert.That((map.Width, map.Height, map.Tiles.Count), Is.EqualTo((24, 18, 432)));
            Assert.That((map.LegacyTiles, map.LegacyWidth), Is.EqualTo((192, 16)));
            Assert.That((map.X(87), map.Y(87)), Is.EqualTo((11, 8)), "the yard: old (7, 5)");
            Assert.That((map.X(0), map.Y(0)), Is.EqualTo((4, 3)), "the first old acre");
            Assert.That((map.X(191), map.Y(191)), Is.EqualTo((19, 14)), "the last old acre");
            Assert.That((map.X(192), map.Y(192)), Is.EqualTo((0, 0)), "the ring follows in row order");
            for (int t = 0; t < map.Tiles.Count; t++) Assert.That(map.TileAt(map.X(t), map.Y(t)), Is.EqualTo(t), "every acre stands once: tile " + t);
            Assert.That(map.TileAt(24, 0), Is.EqualTo(-1));
            Assert.That(map.TileAt(0, -1), Is.EqualTo(-1));
            int farm = 0, hills = 0, river = 0, lanes = 0;
            foreach (TileDef t in map.Tiles) { if (t.Terrain == Terrain.Hill) hills++; if (t.Terrain == Terrain.River) river++; if (t.Terrain == Terrain.Lane) lanes++; }
            Assert.That((hills, lanes), Is.EqualTo((42, 14)));
            Assert.That(river, Is.EqualTo(51));
            int forSale = map.Parcels.Where(p => p.Opens == "township" && p.ForSale).Sum(p => p.Tiles.Count);
            Assert.That(forSale, Is.EqualTo(53), "the acres for sale at the unlock (preview 1)");
            Assert.That(map.Parcels.Where(p => p.Opens == "hills").Sum(p => p.Tiles.Count), Is.EqualTo(42));
            Assert.That(map.Parcels.Where(p => p.Opens != null).All(p => p.Tiles.All(t => map.X(t) >= 0)), Is.True);
            Assert.That(map.Parcels.Take(56).All(p => p.Opens == null), Is.True, "the 56 parcels of the old map are not touched");
            _ = farm;
        }

        // AC-8: closed before the Township; nothing of it can be bought; its land is not simulated.
        [Test]
        public void ClosedUntilTheTownship()
        {
            Simulation sim = Start();
            MapDef map = sim.Config.Game.Map;
            int west = map.IndexOfParcel("rw1a"), hill = map.IndexOfParcel("rh1");
            Assert.That(LandRules.Locked(sim, west), Is.True);
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "rw1a")).Reason, Is.EqualTo(RejectReason.LandClosed));
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "rh1")).Reason, Is.EqualTo(RejectReason.LandClosed));
            Assert.That(LandRules.LockedParcels(sim)[west], Is.True);
            int tile = map.Parcels[west].Tiles[0];
            var before = sim.State.Land.Tiles[tile].Moisture;
            sim.State.Land.Tiles[tile].Moisture = Maths.Fixed.FromInt(5);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86400);
            Assert.That(sim.State.Land.Tiles[tile].Moisture, Is.EqualTo(Maths.Fixed.FromInt(5)), "a closed acre is not simulated");
            Assert.That(before, Is.Not.EqualTo(Maths.Fixed.Zero));
            Assert.That(sim.State.Land.Tiles[map.Parcels[map.IndexOfParcel("home_a")].Tiles[0]].Moisture, Is.Not.EqualTo(Maths.Fixed.FromInt(5)));
            _ = hill;
        }

        // AC-8: the Township opens the ring and the hills (US-147), once, with an event; an acre still needs a neighbour you own.
        [Test]
        public void TheTownshipOpensIt()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            Assert.That(sim.PendingEvents.OfType<RingOpened>().Count(), Is.EqualTo(1));
            MapDef map = sim.Config.Game.Map;
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rw1a")), Is.False);
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rs1")), Is.False);
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rh1")), Is.False, "the hills open with the ring (US-147)");
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "rw1a")).Reason, Is.EqualTo(RejectReason.NotAdjacent), "open, but beside nothing of yours");
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "rh1")).Reason, Is.EqualTo(RejectReason.NotAdjacent));
            Assert.That(LandRules.LockedParcels(sim).Count(x => x), Is.EqualTo(0));
        }

        // AC-1 (lanes): a section is a work the treasury commissions once; its acres become a road for the acres beside it.
        [Test]
        public void LanesOpenAccess()
        {
            Simulation sim = Start();
            MapDef map = sim.Config.Game.Map;
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "lane_w1")).Reason, Is.EqualTo(RejectReason.NoTreasury), "before the Township");
            TreasuryTests.Township(sim);
            Gift(sim, 500_000 * Coin);
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 100_000 * Coin));
            int beside = map.TileAt(1, 6), lane = map.TileAt(2, 6), far = map.TileAt(1, 12);
            Assert.That(sim.State.Land.Tiles[lane].Terrain, Is.EqualTo(Terrain.Lane));
            Assert.That(LeaseRules.RoadAccess(sim, beside), Is.EqualTo(0), "a track is no road");
            long cash = sim.State.Treasury.Cash;
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "lane_w1"));
            Assert.That(sim.State.Treasury.Cash, Is.LessThan(cash), "the treasury pays");
            Assert.That(LaneRules.BuiltSection(sim, 0), Is.True);
            Assert.That(LaneRules.Built(sim, lane), Is.True);
            Assert.That(LaneRules.Built(sim, map.TileAt(2, 8)), Is.False, "the next section is still a track");
            Assert.That(LeaseRules.RoadAccess(sim, beside), Is.EqualTo(1), "a lane for the acre beside it");
            Assert.That(LeaseRules.RoadAccess(sim, far), Is.EqualTo(0));
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "lane_w1")).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(sim.PendingEvents.OfType<LaneBuilt>().Single().Section, Is.EqualTo(0));
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-8: every older save loads into the 24 × 18 map with the ring closed and its tiles from content; the old acres are exactly as they were.
        [Test]
        public void OlderSavesGainTheRing()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Path.Combine(TestContent.TestFile("saves"), "schema20.ftc")), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(20));
            MapDef map = config.Game.Map;
            Assert.That(r.State.Land.Tiles.Count, Is.EqualTo(432));
            Assert.That(r.State.Land.Parcels.Count, Is.EqualTo(map.Parcels.Count));
            for (int t = 0; t < 432; t++) Assert.That(r.State.Land.Tiles[t].Id, Is.EqualTo(t));
            Assert.That(r.State.Land.Tiles[192].Terrain, Is.EqualTo(Terrain.Hill), "the ring's first acre is from content");
            Assert.That(r.State.Land.Parcels.Skip(56).Any(p => p.Owned), Is.False, "nothing of the ring is owned");
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * 86400);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }
    }
}
