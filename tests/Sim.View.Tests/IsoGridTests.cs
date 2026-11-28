using NUnit.Framework;
using Sim.View.Content;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The map camera's default and closest zoom (spec 032 §3.4, AC-4 and AC-5).</summary>
    public class IsoGridTests
    {
        private const float Aspect = 2.16f; // an iPhone 13 held sideways

        private static CameraDef Cam() { return UiLoader.Load(ViewKit.ReadContent("ui.json")).Camera; }

        /// <summary>The acres a view shows across the screen: its width in world units over one tile's width (one acre, 2 × the half width).</summary>
        private static float AcresAcross(IsoGrid g, float halfHeight, float aspect) { return halfHeight * 2f * aspect / (g.HalfWidth * 2f); }

        // AC-4: the default view is 3 acres across, the closest zoom is 2 acres across, the farthest is unchanged.
        [Test]
        public void DefaultAndClosestZoom()
        {
            var g = new IsoGrid(16, 12, 0.5f);
            CameraDef def = Cam();
            Assert.That((def.DefaultAcresAcross, def.ClosestAcresAcross), Is.EqualTo((3f, 2f)), "content/ui.json camera");
            var cam = new MapCamera(g, Aspect, def.DefaultAcresAcross, def.ClosestAcresAcross);
            Assert.That(AcresAcross(g, cam.DefaultHalfHeight, Aspect), Is.EqualTo(3f).Within(1e-3f), "default: 3 acres across");
            Assert.That(AcresAcross(g, cam.MinHalfHeight, Aspect), Is.EqualTo(2f).Within(1e-3f), "closest: 2 acres across");
            float farthest = new MapCamera(g, Aspect).MaxHalfHeight;
            Assert.That(cam.MaxHalfHeight, Is.EqualTo(farthest), "the closest and default views don't move the farthest");
            Assert.That(farthest, Is.EqualTo(3.80625f).Within(1e-3f), "farthest: the whole map, as before");
            cam.Focus(4, 3, cam.DefaultHalfHeight);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.DefaultHalfHeight).Within(1e-5f), "a view focused on the yard opens at the default");

            cam.Zoom(0.001f, cam.CentreX, cam.CentreY);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.MinHalfHeight), "pinch in stops at 2 acres");
        }

        // AC-5: a saved camera outside the new bounds opens clamped into them (a town saved with the old 3-acre closest zoom is still inside; one
        // zoomed in further than 2 acres, or out past the whole map, is brought back).
        [Test]
        public void SavedZoomClamped()
        {
            var g = new IsoGrid(16, 12, 0.5f);
            var cam = new MapCamera(g, Aspect, 3f, 2f);
            cam.FocusWorld(0f, -2f, 0.01f);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.MinHalfHeight), "too close: clamped to 2 acres across");
            cam.FocusWorld(0f, -2f, 1000f);
            Assert.That(cam.HalfHeight, Is.EqualTo(cam.MaxHalfHeight), "too far: clamped to the whole map");
            cam.FocusWorld(0f, -2f, 0.6f);
            Assert.That(cam.HalfHeight, Is.EqualTo(0.6f), "inside the bounds it is kept");
        }
    }
}
