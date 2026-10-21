using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 010 (the 16 × 12 village map): the shipped layout and the rules for the village's land.</summary>
    public partial class ContentTests
    {
        private static int Id(int x, int y) { return y * 16 + x; }

        /// <summary>A Phase 1 (8 × 6) tile id on the 16 × 12 map (spec 010 §3.4).</summary>
        public static int Remap(int old) { return (old / 8 + 3) * 16 + old % 8 + 2; }

        private static bool Village(Terrain t) { return t == Terrain.Road || t == Terrain.Village || t == Terrain.Commons || t == Terrain.Bank; }

        // Spec 010 AC-1.
        [Test]
        public void Map_Phase2Layout()
        {
            MapDef map = TestContent.Game().Map;
            Assert.That((map.Width, map.Height), Is.EqualTo((16, 12)));
            var road = new HashSet<int>(Enumerable.Range(0, 16).Select(x => Id(x, 11))
                .Concat(Enumerable.Range(3, 8).Select(y => Id(10, y))).Concat(Enumerable.Range(8, 3).Select(y => Id(7, y))));
            Assert.That(road.Count, Is.EqualTo(27));
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                int x = i % 16, y = i / 16;
                Terrain want = road.Contains(i) ? Terrain.Road
                    : x >= 11 && x <= 14 && y >= 3 && y <= 6 ? Terrain.Village
                    : y == 0 ? (x <= 7 ? Terrain.Commons : Terrain.Bank)
                    : map.Tiles[i].Terrain;
                Assert.That(map.Tiles[i].Terrain, Is.EqualTo(want), "tile " + i);
            }
            Assert.That(map.Tiles.Count(t => t.Terrain == Terrain.Village), Is.EqualTo(16));
            foreach (ParcelDef p in map.Parcels.Where(p => p.Tiles.Any(t => Village(map.Tiles[t].Terrain))))
            {
                Assert.That(p.ForSale, Is.False, p.Id);
                Assert.That(p.Tiles.All(t => Village(map.Tiles[t].Terrain)), Is.True, p.Id + " holds no farm tile");
            }
            Assert.That(map.Parcels.SelectMany(p => p.Tiles).OrderBy(t => t), Is.EqualTo(Enumerable.Range(0, 192)), "every tile in exactly one parcel");

            GameDefs defs = TestContent.Game();
            Assert.That(defs.Start.InheritedParcels.SelectMany(p => map.Parcels[p].Tiles).OrderBy(t => t),
                Is.EqualTo(new[] { 84, 85, 86, 87, 100, 101, 102, 103, 119 }));
            Assert.That(defs.Land.WellTiles, Is.EqualTo(new[] { 86, 102 }));
            Assert.That(map.Places.Count, Is.EqualTo(16));
            Assert.That(map.Places.All(p => map.Tiles[p.Tile].Terrain == Terrain.Village), Is.True);
            Assert.That(map.Places.Select(p => p.Kind).Distinct(), Is.EquivalentTo(new[]
                { "house", "grocery", "tea_stall", "chemist", "dealer", "temple", "council_hall", "school_plot", "banyan", "pond" }));
        }

        // Spec 010 AC-2: the Phase 1 farm sits unchanged at columns 2–9, rows 3–8 (saves/map-8x6.json is the Phase 1 map, frozen).
        [Test]
        public void Map_OldFarmInsideUnchanged()
        {
            JObject old = JObject.Parse(File.ReadAllText(TestContent.TestFile("saves/map-8x6.json")));
            JArray oldTiles = (JArray)old["tiles"];
            MapDef map = TestContent.Game().Map;
            GameDefs defs = TestContent.Game();
            for (int i = 0; i < oldTiles.Count; i++)
            {
                TileDef t = map.Tiles[Remap(i)];
                if (i == 45) { Assert.That(t.Terrain, Is.EqualTo(Terrain.Road), "the farm lane takes s5"); continue; }
                JToken o = oldTiles[i];
                Assert.That(t.Terrain.ToString().ToLowerInvariant(), Is.EqualTo((string)o["terrain"]), "tile " + i);
                Assert.That(defs.Soils[t.Soil].Id, Is.EqualTo((string)o["soil"]), "tile " + i);
                Assert.That(defs.Drainages[t.Drainage].Id, Is.EqualTo((string)o["drainage"]), "tile " + i);
                Assert.That(t.Fertility, Is.EqualTo(Fixed.FromInt((int)o["fertility"])), "tile " + i);
                Assert.That(t.Moisture, Is.EqualTo(Fixed.FromInt((int)o["moisture"])), "tile " + i);
            }
            foreach (JToken p in (JArray)old["parcels"])
            {
                string id = (string)p["id"];
                if (id == "s5") { Assert.That(map.IndexOfParcel("s5"), Is.EqualTo(-1)); continue; }
                ParcelDef now = map.Parcels[map.IndexOfParcel(id)];
                Assert.That(now.Tiles, Is.EqualTo(((JArray)p["tiles"]).Select(t => Remap((int)t))), id);
            }
        }

        // Spec 010 AC-3.
        [Test]
        public void Map_BuyableAcres()
        {
            GameDefs defs = TestContent.Game();
            var inherited = new HashSet<int>(defs.Start.InheritedParcels);
            int acres = defs.Map.Parcels.Where((p, i) => p.ForSale && !inherited.Contains(i)).Sum(p => p.Tiles.Count);
            Assert.That(acres, Is.InRange(60, 80));
            Assert.That(acres, Is.EqualTo(78), "spec 010 §3.2: 38 of today's acres and 40 new");
        }

        // Spec 010 AC-3b: 1.05 touching a road, the lane or the village, or on riverside row 2; 0.95 the far west strip; else 1.0.
        [Test]
        public void Map_LocationFactors()
        {
            MapDef map = TestContent.Game().Map;
            foreach (ParcelDef p in map.Parcels)
            {
                if (p.Tiles.All(t => Village(map.Tiles[t].Terrain))) continue;
                bool touches = p.Tiles.Any(t => Neighbours(t).Any(n => map.Tiles[n].Terrain == Terrain.Road || map.Tiles[n].Terrain == Terrain.Village));
                string want = touches || p.Tiles.Any(t => t / 16 == 2) ? "1.05"
                    : p.Tiles.All(t => t % 16 <= 1 && t / 16 >= 3 && t / 16 <= 10) ? "0.95" : "1.0";
                Assert.That(p.LocationFactor, Is.EqualTo(Fixed.Parse(want)), p.Id);
            }
        }

        private static IEnumerable<int> Neighbours(int t)
        {
            int x = t % 16, y = t / 16;
            if (x > 0) yield return t - 1;
            if (x < 15) yield return t + 1;
            if (y > 0) yield return t - 16;
            if (y < 11) yield return t + 16;
        }
    }

    public partial class LandTests
    {
        private static CommandResult Buy(Simulation sim, string id) { return sim.Execute(new BuyParcel(sim.State.Clock.SimTime, id)); }

        // Spec 010 AC-4.
        [Test]
        public void Buy_NotForSale()
        {
            Simulation sim = NewFarm(game: ParcelTests.Rich());
            ulong before = sim.State.ComputeHash();
            foreach (string id in new[] { "nn3", "road", "centre", "commons", "bank" })
                Assert.That(Buy(sim, id).Reason, Is.EqualTo(RejectReason.NotForSale), id);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), "nothing changes");
            Assert.That(Buy(sim, "s2").Accepted, Is.True, "a neighbour's parcel for sale, next to the farm");
        }

        // Spec 010 AC-4: roads are never owned, so they don't connect land.
        [Test]
        public void Buy_RoadsDontConnect()
        {
            Simulation sim = NewFarm(game: ParcelTests.Rich());
            Assert.That(Buy(sim, "s2").Accepted, Is.True); // (6, 7) and (6, 8), next to home_b
            Assert.That(Buy(sim, "s8").Accepted, Is.True); // (6, 9) and (6, 10), next to s2
            Assert.That(Buy(sim, "s9").Reason, Is.EqualTo(RejectReason.NotAdjacent), "s9 touches s8 only across the farm lane");
        }

        // Spec 010 AC-5.
        [Test]
        public void VillageLand_NeverWorked()
        {
            Simulation sim = NewFarm(game: ParcelTests.Rich());
            foreach (int tile in new[] { 176, 135, 59, 0, 8 }) // the district road, the farm lane, a village place, the commons, the bank
            {
                Assert.That(sim.Execute(new Sow(0, tile, "radish", SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.NotOwned), "tile " + tile);
                Assert.That(sim.Execute(new BuildHandHut(0, tile)).Reason, Is.EqualTo(RejectReason.NotQuarters), "tile " + tile);
                Assert.That(sim.State.Land.Slots.Any(s => s.TileId == tile), Is.False, "tile " + tile);
            }
        }
    }
}
