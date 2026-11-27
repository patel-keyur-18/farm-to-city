using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.View.Content;

namespace Sim.View.Tests
{
    /// <summary><c>content/ui.json</c>'s map look: the camera and each weather's shadow strength (spec 032 §4, AC-6).</summary>
    public class UiContentTests
    {
        private static string Good() { return ViewKit.ReadContent("ui.json"); }

        // AC-6: the new fields load with their values and are validated against their bounds.
        [Test]
        public void CameraAndShadow()
        {
            UiDef ui = UiLoader.Load(Good());
            Assert.That((ui.Camera.DefaultAcresAcross, ui.Camera.ClosestAcresAcross), Is.EqualTo((3f, 2f)));
            var expected = new[] { ("sun", 1.0f), ("heatwave", 1.0f), ("frost", 0.8f), ("cloudy", 0.45f), ("rain", 0.25f), ("heavy_rain", 0.15f), ("storm", 0.10f) };
            foreach ((string id, float shadow) in expected)
                Assert.That(ui.WeatherLook.Style(id).Shadow, Is.EqualTo(shadow).Within(1e-3f), id + " shadow");
            Assert.That(ui.WeatherLook.Weathers.All(w => w.Shadow >= 0f && w.Shadow <= 1f), Is.True);

            // Bounds: default 2 to 6 acres, closest 1.5 to 3, and the closest zoom may not be farther than the default.
            string Bad(string from, string to) { return Good().Replace(from, to); }
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(Bad("\"defaultAcresAcross\": 3", "\"defaultAcresAcross\": 7"))).FieldPath, Is.EqualTo("ui.camera.defaultAcresAcross"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(Bad("\"closestAcresAcross\": 2", "\"closestAcresAcross\": 1"))).FieldPath, Is.EqualTo("ui.camera.closestAcresAcross"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(Bad("\"closestAcresAcross\": 2", "\"closestAcresAcross\": 3.5"))).FieldPath, Is.EqualTo("ui.camera.closestAcresAcross"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(Bad("\"shadow\": 0.25", "\"shadow\": 1.5"))).FieldPath, Does.EndWith(".shadow"));
        }
    }
}
