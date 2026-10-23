using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 010 §3.3–3.7 on screen: the 16 × 12 grid, the camera, the edge, places and land cards.</summary>
    public partial class MapTests
    {
        // Spec 010 AC-9.
        [Test]
        public void Grid16x12_PickAndBounds()
        {
            var grid = new IsoGrid(16, 12, 0.5f);
            for (int id = 0; id < 192; id++)
            {
                (float x, float y) = grid.Centre(id % 16, id / 16);
                Assert.That(grid.Pick(x, y), Is.EqualTo(id), "tile " + id);
            }
            (float ox, float oy) = grid.Centre(-1, 3);
            Assert.That(grid.Pick(ox, oy), Is.EqualTo(-1), "past the edge");
            (float fx, float fy) = grid.Centre(16, 11);
            Assert.That(grid.Pick(fx, fy), Is.EqualTo(-1));
        }

        // Spec 010 AC-9: the camera opens on the yard, and zoomed out the whole map fits.
        [Test]
        public void Camera_OpensOnYard()
        {
            Simulation sim = ViewKit.NewGame();
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That((map.HomeX, map.HomeY), Is.EqualTo((7f, 5.5f)), "the middle of the 2-acre yard (tiles 87 and 103)");
            var grid = new IsoGrid(map.Width, map.Height, 0.5f);
            var cam = new MapCamera(grid, 16f / 9f);
            cam.Focus(7, 5, cam.MinHalfHeight * 2);
            (float yx, float yy) = grid.Centre(7, 5);
            Assert.That((cam.CentreX, cam.CentreY), Is.EqualTo((yx, yy)));
            var b = grid.Bounds();
            cam.FocusWorld(0, 0, cam.MaxHalfHeight);
            Assert.That(cam.HalfHeight * 2, Is.GreaterThanOrEqualTo(b.top - b.bottom), "the whole map fits zoomed out");
        }

        // Spec 010 AC-10: past the map the screen shows the valley. The background is the meadow (never sky-blue, Unity); over it
        // the ring of edge tiles, and the river and the road run on past anything the camera's clamp can show.
        [Test]
        public void Edge_CoversView()
        {
            var grid = new IsoGrid(16, 12, 0.5f);
            var tiles = EdgeView.Tiles(16, 12);
            var river = new System.Collections.Generic.HashSet<(int, int)>(tiles.Where(t => t.Kind == EdgeKind.River).Select(t => (t.X, t.Y)));
            var road = new System.Collections.Generic.HashSet<(int, int)>(tiles.Where(t => t.Kind == EdgeKind.Road).Select(t => (t.X, t.Y)));
            foreach (float aspect in new[] { 2532f / 1170f, 16f / 9f, 4f / 3f })
            {
                var cam = new MapCamera(grid, aspect);
                foreach (float h in new[] { cam.MaxHalfHeight, cam.MinHalfHeight, (cam.MaxHalfHeight + cam.MinHalfHeight) / 2 })
                    foreach ((float dx, float dy) in new[] { (-1000f, 0f), (1000f, 0f), (0f, -1000f), (0f, 1000f), (-1000f, -1000f), (1000f, 1000f), (-1000f, 1000f), (1000f, -1000f), (0f, 0f) })
                    {
                        cam.FocusWorld(0, -3, h);
                        cam.Pan(dx, dy);
                        float w = cam.HalfHeight * aspect;
                        for (int i = 0; i <= 40; i++)
                            for (int j = 0; j <= 40; j++)
                            {
                                (float fx, float fy) = grid.ToTile(cam.CentreX - w + 2 * w * i / 40f, cam.CentreY - cam.HalfHeight + 2 * cam.HalfHeight * j / 40f);
                                int tx = (int)System.Math.Floor(fx + 0.5f), ty = (int)System.Math.Floor(fy + 0.5f);
                                if (ty >= -EdgeView.RiverWidth && ty < 0) Assert.That(river.Contains((tx, ty)), Is.True, "river at " + tx + "," + ty);
                                if (ty == 11 && (tx < 0 || tx >= 16)) Assert.That(road.Contains((tx, ty)), Is.True, "road at " + tx);
                            }
                    }
            }
            // The ring hides the map's sides: every tile next to the map is an edge tile.
            var all = new System.Collections.Generic.HashSet<(int, int)>(tiles.Select(t => (t.X, t.Y)));
            for (int x = -1; x <= 16; x++) Assert.That(all.Contains((x, -1)) && all.Contains((x, 12)), Is.True);
            for (int y = 0; y < 12; y++) Assert.That(all.Contains((-1, y)) && all.Contains((16, y)), Is.True);
            Assert.That(EdgeView.Kind(5, -1, 16, 12), Is.EqualTo(EdgeKind.River), "the river past row 0");
            Assert.That(EdgeView.Kind(-2, 5, 16, 12), Is.EqualTo(EdgeKind.Hill), "hills behind, past column 0");
            Assert.That(EdgeView.Kind(-3, 11, 16, 12), Is.EqualTo(EdgeKind.Road), "the district road runs on");
            Assert.That(EdgeView.Kind(18, 11, 16, 12), Is.EqualTo(EdgeKind.Road));
        }

        // Spec 010 AC-11.
        [Test]
        public void PlaceAndParcelCards()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            var places = sim.Config.Game.Map.Places;
            int Tile(string kind) { return places.First(p => p.Kind == kind).Tile; }

            SheetVm chemist = FieldSheets.ForTile(ctx, Tile("chemist"));
            Assert.That(chemist.Title, Is.EqualTo("The chemist"));
            Assert.That(chemist.Actions.Single().Id, Is.EqualTo("medicine"));
            SheetVm dealer = FieldSheets.ForTile(ctx, Tile("dealer"));
            Assert.That(dealer.Actions.Single().Opens.Value.Kind, Is.EqualTo("BuyInputs"));
            SheetVm temple = FieldSheets.ForTile(ctx, Tile("temple"));
            Assert.That((temple.Title, temple.Actions.Count), Is.EqualTo(("The temple", 0)));
            Assert.That(FieldSheets.ForTile(ctx, Tile("school_plot")).Subtitle, Does.Contain("first school"));

            // ADR 0061: the dairy co-op, on the side lane by the farm's lane: its price a litre by fat, and cattle with the Hamlet.
            SheetVm dairy = FieldSheets.ForTile(ctx, Tile("dairy"));
            Assert.That(dairy.Title, Is.EqualTo("The dairy co-op"));
            Assert.That(dairy.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Buffalo milk, 7.0% fat", "Cow milk, 4.0% fat", "This morning from you" }));
            Assert.That(dairy.Lines[0].Value, Does.EndWith("a litre"));
            Assert.That((dairy.Lines[2].Value, dairy.Actions.Count), Is.EqualTo(("Cattle come with the Hamlet", 0)));
            sim.State.Era.Current = sim.Config.Game.Livestock.UnlockEra;
            dairy = FieldSheets.ForTile(ViewKit2.Context(sim), Tile("dairy"));
            Assert.That(dairy.Lines[2].Value, Is.EqualTo("Nothing yet: no animal in milk"));
            Assert.That(dairy.Actions.Single().Opens.Value, Is.EqualTo(new SheetRequest("Tile", 104)), "the way to your cattle yard");

            SheetVm road = FieldSheets.ForTile(ctx, 176);
            Assert.That((road.Title, road.Lines.Count, road.Actions.Count), Is.EqualTo(("The road", 0, 0)), "the village's land: never priced");
            Assert.That(FieldSheets.ForTile(ctx, 0).Title, Is.EqualTo("The grazing commons"));
            Assert.That(FieldSheets.ForTile(ctx, 8).Title, Is.EqualTo("The river bank"));

            SheetVm neighbour = FieldSheets.ForTile(ctx, 16); // row 1: nn1, Ramesh's
            Assert.That((neighbour.Title, neighbour.Subtitle), Is.EqualTo(("Ramesh's farm", "Not for sale · 2 acres")));
            Assert.That(neighbour.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Soil" }), "no price");
            Assert.That(neighbour.Actions, Is.Empty);
            SheetVm forSale = FieldSheets.ForTile(ctx, 53); // n1
            Assert.That(forSale.Title, Is.EqualTo("Land for sale"));
            Assert.That(forSale.Actions.Any(a => a.Id == "buy"), Is.True);

            MapVm map = MapView.Build(ctx);
            Assert.That(map.Tiles.Where(t => t.ForSaleSign).All(t => sim.Config.Game.Map.Parcels[t.ParcelId].ForSale), Is.True, "signs only on land for sale");
            Assert.That(map.Tiles.Count(t => t.ForSaleSign), Is.EqualTo(sim.Config.Game.Map.Parcels.Count(p => p.ForSale) - 4), "one sign a parcel for sale (not the inherited four)");
            Assert.That(map.Tiles.Where(t => t.Kind == TileKind.Unowned).All(t => t.NeighbourCrop == null || t.NeighbourCrop == "potato" || t.NeighbourCrop == "radish"), Is.True, "Spring: potato or radish");
            Assert.That(map.Tiles.Where(t => t.Kind == TileKind.Village).Count(t => t.Place != null), Is.EqualTo(16));
            Assert.That(map.Tiles.Single(t => t.Id == 176).RoadLinks, Is.EqualTo(2 | 1), "the district road's west end runs on off the map");
        }
    }
}
