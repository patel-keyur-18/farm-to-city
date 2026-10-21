using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class LandTests
    {
        // Spec 002 AC-1.
        [Test]
        public void NewGame_InheritedPlot()
        {
            Simulation sim = TestSim.NewFarm();
            LandState land = sim.State.Land;
            GameDefs defs = sim.Config.Game;

            Parcel[] home = land.Parcels.Where(p => p.Owned).ToArray();
            Assert.That(home.Select(p => defs.Map.Parcels[p.Id].Id), Is.EqualTo(new[] { "home_a", "home_b", "home_c" }));
            Assert.That(home.All(p => p.Protected), Is.True);

            Tile[] tiles = land.Tiles.Where(t => land.Parcels[t.ParcelId].Owned).ToArray();
            Assert.That(tiles.Count(t => t.Terrain == Terrain.Farm), Is.EqualTo(6));
            Assert.That(tiles.Count(t => t.Terrain == Terrain.Yard), Is.EqualTo(2), "a 2-acre yard (ADR 0040)");
            Assert.That(tiles.Count(t => t.Terrain == Terrain.Quarters), Is.EqualTo(1), "the labour quarters");
            Tile yard = tiles.First(t => t.Terrain == Terrain.Yard);

            Assert.That(land.Buildings.Select(b => defs.Buildings[b.Def].Id), Is.EqualTo(new[] { "hut", "well" }));
            Assert.That(land.Buildings.All(b => b.TileId == yard.Id), Is.True);
            Assert.That(tiles.Where(t => t.Terrain == Terrain.Farm).Select(t => t.Fertility), Is.All.EqualTo(Fixed.FromInt(45)));
            Assert.That(land.Slots.Count(s => s.Kind == SlotKind.Yard && s.TileId == yard.Id), Is.EqualTo(4));

            Assert.That(land.Slots.Count(s => s.Kind == SlotKind.FieldEdge), Is.EqualTo(12));

            // Every other parcel starts unowned and unprotected.
            Assert.That(land.Parcels.Where(p => !home.Contains(p)).All(p => !p.Owned && !p.Protected), Is.True);
        }
    
        // Spec 002 AC-15.
        [Test]
        public void ChunkingInvariance_WithLand()
        {
            Simulation Build()
            {
                return Simulation.NewGame(Config(systems: new[] { new FallowScheduleSystem() }, game: TestContent.Game()),
                    new Commands.NewGame(11, Wall0, 7 * Hour + 13));
            }

            Simulation one = Build();
            long end = one.State.Clock.SimTime + one.Clock.SecondsPerYear;
            one.AdvanceTo(end);

            Simulation many = Build();
            long start = many.State.Clock.SimTime;
            var targets = new List<long>();
            for (ulong i = 0; i < 499; i++)
                targets.Add(start + (long)Rng.Below(Rng.Next(11, (RngStream)902, i, 0), (ulong)(end - start)));
            targets.Sort();
            targets.Add(end);
            foreach (long target in targets) many.AdvanceTo(target);

            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
            Assert.That(one.State.ComputeHash(), Is.Not.EqualTo(Build().State.ComputeHash()), "land state is hashed and changes");
            Assert.That(one.State.Land.Tiles[101].Fertility, Is.Not.EqualTo(Build().State.Land.Tiles[101].Fertility), "fallow recovered");
        }
    }
}
