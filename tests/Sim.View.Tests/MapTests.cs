using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Land;
using Sim.View.Content;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 §3.4: the map view model, the isometric geometry, the camera and the tints.</summary>
    public partial class MapTests
    {
        private const long Hour = 3600, Day = 86400;

        [Test]
        public void NewGame_TilesSlotsAndBuildings()
        {
            Simulation sim = ViewKit.NewGame();
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.Width, Is.EqualTo(16));
            Assert.That(map.Height, Is.EqualTo(12));
            Assert.That(map.Tiles.Count, Is.EqualTo(192));
            Assert.That(map.Tiles.Count(t => t.Kind == TileKind.Farm), Is.EqualTo(6));
            Assert.That(map.Tiles.Count(t => t.Kind == TileKind.Yard), Is.EqualTo(2));
            Assert.That(map.Tiles.Count(t => t.Kind == TileKind.Unowned), Is.EqualTo(124), "neighbours' farms: 78 for sale, 46 not yet");
            Assert.That(map.Tiles.Where(t => t.WellAcre).Select(t => t.Id), Is.EqualTo(new[] { 86, 102 }));
            Assert.That(map.Tiles[87].Kind, Is.EqualTo(TileKind.Yard));
            Assert.That(map.Tiles[86].X, Is.EqualTo(6));
            Assert.That(map.Tiles[86].Y, Is.EqualTo(5));
            Assert.That(map.Tiles.Where(t => t.Kind == TileKind.Farm || t.Kind == TileKind.Yard || t.Kind == TileKind.Quarters).All(t => t.Protected), Is.True);
            Assert.That(map.Buildings.Select(b => b.DefId), Is.EqualTo(new[] { "hut", "well" }));
            Assert.That(map.Slots.Count(s => s.Yard), Is.EqualTo(4));
            Assert.That(map.Slots.Count(s => !s.Yard), Is.EqualTo(12));
            Assert.That(map.Slots.All(s => s.Stage == TreeStage.Empty), Is.True);
            Assert.That(map.Slots.All(s => s.Owned), Is.True, "the inherited land's slots: drawn as planting spots and tappable");
            Assert.That(map.Beds.Count, Is.EqualTo(2));
            Assert.That(map.Slots.Where(s => s.TileId == 86).Select(s => s.Index), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void CropStages_ByProgress_ThenReadyThenSpoiling()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new Sow(t, 86, "potato", Sim.Core.Farm.SeedSource.Certified, "none")).Accepted, Is.True); // 24 h
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.Growing1));
            Assert.That(MapView.Build(ctx).Tiles[86].CropId, Is.EqualTo("potato"));
            sim.AdvanceTo(t + 10 * Hour);
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.Growing2), "10 of 24 hours");
            sim.AdvanceTo(t + 20 * Hour);
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.Growing3), "20 of 24 hours");
            Assert.That(MapView.Build(ctx).Tiles[86].Progress, Is.InRange(0.8f, 0.85f));
            sim.AdvanceTo(t + 24 * Hour);
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.Ready));
            sim.AdvanceTo(t + 24 * Hour + 72 * Hour);
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.Ready), "the grace is inclusive");
            sim.AdvanceTo(t + 24 * Hour + 72 * Hour + 1);
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.Spoiling));
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 86, Sim.Core.Market.Overflow.SellRestToTrader)).Accepted, Is.True);
            Assert.That(MapView.Build(ctx).Tiles[86].Stage, Is.EqualTo(CropStage.None));
        }

        [Test]
        public void TreeStages_AndBeds()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            long t = sim.State.Clock.SimTime;
            long slot = sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard).Id;
            Assert.That(sim.Execute(new PlantTree(t, slot, "banana")).Accepted, Is.True); // grows 1 week
            SlotVm vm = MapView.Build(ctx).Slots.Single(s => s.Id == slot);
            Assert.That(vm.Stage, Is.EqualTo(TreeStage.Sapling));
            Assert.That(vm.TreeId, Is.EqualTo("banana"));
            sim.AdvanceTo(t + 5 * Day);
            Assert.That(MapView.Build(ctx).Slots.Single(s => s.Id == slot).Stage, Is.EqualTo(TreeStage.Young));
            sim.AdvanceTo(t + 7 * Day + Hour);
            Assert.That(MapView.Build(ctx).Slots.Single(s => s.Id == slot).Stage, Is.AnyOf(TreeStage.Mature, TreeStage.Ripe), "grown");

            Assert.That(sim.Execute(new SowGarden(sim.State.Clock.SimTime, 1, "garden_radish")).Accepted, Is.True);
            BedVm bed = MapView.Build(ctx).Beds.Single(b => b.Id == 1);
            Assert.That(bed.Stage, Is.EqualTo(CropStage.Growing1));
            Assert.That(bed.CropId, Is.EqualTo("garden_radish"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 11 * 60);
            Assert.That(MapView.Build(ctx).Beds.Single(b => b.Id == 1).Stage, Is.EqualTo(CropStage.Ready));
            Assert.That(MapView.Build(ctx).Beds.Single(b => b.Id == 2).Stage, Is.EqualTo(CropStage.None));
        }

        [Test]
        public void BoughtLand_BecomesOwnedAndUnprotected()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
            Assert.That(sim.Execute(new BuyParcel(sim.State.Clock.SimTime, "w1")).Accepted, Is.True);
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.Tiles[82].Kind, Is.EqualTo(TileKind.Farm));
            Assert.That(map.Tiles[82].Protected, Is.False);
            Assert.That(map.Tiles[82].ParcelKey, Is.EqualTo("w1"));
            Assert.That(map.Slots.Count(s => s.TileId == 82), Is.EqualTo(2));
        }

        // ---- geometry ----

        [Test]
        public void IsoGrid_CentreAndPick_RoundTrip()
        {
            var g = new IsoGrid(8, 6, 1f);
            for (int ty = 0; ty < 6; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    (float x, float y) = g.Centre(tx, ty);
                    Assert.That(g.Pick(x, y), Is.EqualTo(ty * 8 + tx), tx + "," + ty);
                    (float fx, float fy) = g.ToTile(x, y);
                    Assert.That(fx, Is.EqualTo(tx).Within(1e-4f));
                    Assert.That(fy, Is.EqualTo(ty).Within(1e-4f));
                    // Just inside each corner of the diamond is still this tile; just outside is a neighbour or off the map.
                    Assert.That(g.Pick(x + 0.9f, y), Is.EqualTo(ty * 8 + tx));
                    Assert.That(g.Pick(x, y + 0.4f * g.HalfHeight * 2f), Is.EqualTo(ty * 8 + tx));
                }
            Assert.That(g.Pick(-100, 0), Is.EqualTo(-1));
            Assert.That(g.Pick(0, 100), Is.EqualTo(-1));
        }

        [Test]
        public void IsoGrid_Bounds_ContainEveryTile()
        {
            var g = new IsoGrid(8, 6, 2f);
            var b = g.Bounds();
            for (int ty = 0; ty < 6; ty++)
                for (int tx = 0; tx < 8; tx++)
                {
                    (float x, float y) = g.Centre(tx, ty);
                    Assert.That(x - g.HalfWidth, Is.GreaterThanOrEqualTo(b.left - 1e-4f));
                    Assert.That(x + g.HalfWidth, Is.LessThanOrEqualTo(b.right + 1e-4f));
                    Assert.That(y + g.HalfHeight, Is.LessThanOrEqualTo(b.top + 1e-4f));
                    Assert.That(y - g.HalfHeight, Is.GreaterThanOrEqualTo(b.bottom - 1e-4f));
                }
        }

        [Test]
        public void MapCamera_ZoomAndPan_AreClamped()
        {
            var g = new IsoGrid(8, 6, 1f);
            var cam = new MapCamera(g, 16f / 9f);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.MaxHalfHeight), "starts zoomed out: the whole map");
            var b = g.Bounds();
            Assert.That(cam.CentreX, Is.EqualTo((b.left + b.right) / 2f).Within(1e-4f));

            cam.Zoom(0.01f, cam.CentreX, cam.CentreY);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.MinHalfHeight), "pinch in stops at about three acres");
            cam.Zoom(100f, cam.CentreX, cam.CentreY);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.MaxHalfHeight), "pinch out stops at the whole map");

            cam.Focus(4, 2, cam.MinHalfHeight);
            cam.Pan(1000, 1000);
            float halfW = cam.HalfHeight * 16f / 9f;
            Assert.That(cam.CentreX + halfW, Is.LessThanOrEqualTo(b.right + 1e-3f), "the view never leaves the map");
            Assert.That(cam.CentreY + cam.HalfHeight, Is.LessThanOrEqualTo(b.top + 1e-3f));
            cam.Pan(-5000, -5000);
            Assert.That(cam.CentreX - halfW, Is.GreaterThanOrEqualTo(b.left - 1e-3f));
            Assert.That(cam.CentreY - cam.HalfHeight, Is.GreaterThanOrEqualTo(b.bottom - cam.HalfHeight * 1.3f - 1e-3f), "only the south edge has room for a sheet");

            // A tapped point can be lifted into the top of the screen, above a bottom sheet.
            var lift = new MapCamera(g, 16f / 9f);
            lift.FocusWorld(0f, -1.5f, lift.MinHalfHeight * 2f);
            (float tx, float ty) = lift.CentreFor(0f, -1.5f, 0.2f);
            Assert.That(tx, Is.EqualTo(0f));
            Assert.That(ty, Is.EqualTo(-1.5f - lift.HalfHeight * 0.6f).Within(1e-4f), "the point ends 20% from the top");

            // Zooming keeps the point under the fingers where it was.
            var cam2 = new MapCamera(g, 16f / 9f);
            cam2.Focus(4, 2, cam2.MaxHalfHeight);
            (float px, float py) = g.Centre(4, 2);
            float offBefore = px - cam2.CentreX;
            cam2.Zoom(0.5f, px, py);
            Assert.That(px - cam2.CentreX, Is.EqualTo(offBefore * 0.5f).Within(0.05f));
        }

        // ---- tints and the look data ----

        [Test]
        public void Tint_NightDayAndSeasons()
        {
            UiDef ui = ViewKit2.Ui();
            Assert.That(ui.TintAt(2 * Hour).B, Is.EqualTo(0.8f).Within(1e-5f), "night is blue");
            Assert.That(ui.TintAt(2 * Hour).R, Is.EqualTo(0.55f).Within(1e-5f));
            Assert.That(ui.TintAt(12 * Hour).R, Is.EqualTo(1f), "day is white");
            Rgb dusk = ui.TintAt(18 * Hour + Hour / 2);
            Assert.That(dusk.B, Is.LessThan(ui.TintAt(12 * Hour).B).And.GreaterThan(ui.TintAt(2 * Hour).B - 0.3f), "dusk is warm");
            Rgb mid = ui.TintAt(20 * Hour + Hour / 2);
            Rgb a = ui.TintAt(20 * Hour), z = ui.TintAt(21 * Hour);
            Assert.That(mid.R, Is.EqualTo((a.R + z.R) / 2f).Within(1e-4f), "interpolated between hours");
            Assert.That(ui.TintAt(24 * Hour + 5 * Hour).R, Is.EqualTo(ui.TintAt(5 * Hour).R), "wraps at midnight");
            Assert.That(ui.TintAt(-Hour).R, Is.EqualTo(ui.TintAt(23 * Hour).R));

            Rgb spring = Tint.Map(ui, 0, 12 * Hour), summer = Tint.Map(ui, 1, 12 * Hour);
            Assert.That(summer.B, Is.LessThan(spring.B), "Summer is golden");
            Assert.That(Tint.Map(ui, 2, 12 * Hour).G, Is.GreaterThan(Tint.Map(ui, 2, 12 * Hour).R), "Monsoon is green");
        }

        [Test]
        public void UiLoader_Validation()
        {
            string good = ViewKit.ReadContent("ui.json");
            Assert.That(UiLoader.Load(good).Motions.Count, Is.GreaterThan(15));
            Assert.That(UiLoader.Load(good).DayTint.Count, Is.EqualTo(24));
            Assert.That(UiLoader.Load(good).Motion("TapTile").Kind, Is.EqualTo("Squash"));

            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(good.Replace("\"reducedKind\": \"Flash\"", "\"reducedKind\": \"Squash\""))).FieldPath, Does.EndWith("reducedKind"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(good.Replace("\"reducedMs\": 100", "\"reducedMs\": 500"))).FieldPath, Does.EndWith("reducedMs"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(good.Replace("\"maxFlyingTotal\": 20", "\"maxFlyingTotal\": 99"))).FieldPath, Is.EqualTo("ui.maxFlyingTotal"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(good.Replace("\"easing\": \"OutBack\"", "\"easing\": \"Bounce\""))).FieldPath, Does.EndWith("easing"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(good.Replace("[0.55, 0.6, 0.8]", "[0.55, 0.6]"))).FieldPath, Does.StartWith("ui.dayTint"));
        }

        // AC-10: reduced motion is at most 0.1 s and never bounces or flies.
        [Test]
        public void ReducedMotion()
        {
            UiDef ui = ViewKit2.Ui();
            string[] forbidden = { "Squash", "Pop", "Bob", "FlyToStore", "FlyCoins", "Shake", "Roll", "Grow", "Droop", "Scale", "SlideUp", "SlideDown", "CardIn" };
            foreach (MotionDef m in ui.Motions)
            {
                Assert.That(m.ReducedMs, Is.LessThanOrEqualTo(100), m.Trigger);
                Assert.That(forbidden, Does.Not.Contain(m.ReducedKind), m.Trigger);
            }
            Assert.That(ui.Motions.Select(m => m.Trigger).Distinct().Count(), Is.EqualTo(ui.Motions.Count), "one motion per trigger");
        }
    }
}
