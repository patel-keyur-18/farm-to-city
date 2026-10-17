using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public class LandTests
    {
        // Spec 002 AC-1.
        [Test]
        public void NewGame_InheritedPlot()
        {
            Simulation sim = TestSim.NewFarm();
            LandState land = sim.State.Land;
            GameDefs defs = sim.Config.Game;

            Parcel home = land.Parcels.Single(p => p.Owned);
            Assert.That(defs.Map.Parcels[home.Id].Id, Is.EqualTo("home"));
            Assert.That(home.Protected, Is.True);

            Tile[] tiles = land.Tiles.Where(t => t.ParcelId == home.Id).ToArray();
            Assert.That(tiles.Count(t => t.Terrain == Terrain.Farm), Is.EqualTo(2));
            Tile yard = tiles.Single(t => t.Terrain == Terrain.Yard);

            Assert.That(land.Buildings.Select(b => defs.Buildings[b.Def].Id), Is.EqualTo(new[] { "hut", "well" }));
            Assert.That(land.Buildings.All(b => b.TileId == yard.Id), Is.True);
            Assert.That(tiles.Where(t => t.Terrain == Terrain.Farm).Select(t => t.Fertility), Is.All.EqualTo(Fixed.FromInt(45)));
            Assert.That(land.Slots.Count(s => s.Kind == SlotKind.Yard && s.TileId == yard.Id), Is.EqualTo(4));

            // Every other parcel starts unowned and unprotected.
            Assert.That(land.Parcels.Where(p => p.Id != home.Id).All(p => !p.Owned && !p.Protected), Is.True);
        }
    }
}
