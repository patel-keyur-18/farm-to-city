using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The flowing river and the ground's variants (US-273, ADR 0184): the loop, and that every rendered frame and variant exists at one size.</summary>
    public class WaterFlowTests
    {
        private sealed record Entry(string Name, int W, int H, double PivotX, double PivotY);

        private static Entry[] Manifest()
        {
            string path = Path.Combine(ViewKit.RepoRoot, "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender", "sprites.json");
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            return doc.RootElement.GetProperty("sprites").EnumerateArray()
                .Select(e => new Entry(e.GetProperty("name").GetString(), e.GetProperty("w").GetInt32(), e.GetProperty("h").GetInt32(), e.GetProperty("pivotX").GetDouble(), e.GetProperty("pivotY").GetDouble()))
                .ToArray();
        }

        // 12 frames at 10 a second: a loop of 1.2 s that comes back to frame 0, every frame in range, none skipped.
        [Test]
        public void TheLoopIsTwelveFramesAtTenAndComesBack()
        {
            Assert.That(WaterFlow.Frame(0f, false), Is.EqualTo(0));
            Assert.That(WaterFlow.Frame(0.95f, false), Is.EqualTo(9));
            Assert.That(WaterFlow.Frame(1.19f, false), Is.EqualTo(11));
            Assert.That(WaterFlow.Frame(1.2f, false), Is.EqualTo(0), "back to the start after 1.2 s");
            int last = WaterFlow.Frame(0f, false);
            for (int i = 1; i < 600; i++)
            {
                int f = WaterFlow.Frame(i * 0.1f + 0.01f, false);
                Assert.That(f, Is.InRange(0, WaterFlow.Frames - 1));
                Assert.That(f, Is.EqualTo((last + 1) % WaterFlow.Frames), "one frame at a time");
                last = f;
            }
        }

        // Reduced motion holds the first frame at any time.
        [Test]
        public void ReducedMotionHoldsFrameZero()
        {
            foreach (float t in new[] { 0f, 0.35f, 7.7f, 1234.5f }) Assert.That(WaterFlow.Frame(t, true), Is.EqualTo(0));
        }

        // The ground and edge water each have all 12 frames, every frame the same size and pivot as the others (a swap never moves the river).
        [Test]
        public void EveryWaterFrameIsRenderedAtOneSizeAndPivot()
        {
            Entry[] all = Manifest();
            foreach (string prefix in new[] { "ground_water", "edge_water" })
            {
                Entry[] frames = Enumerable.Range(0, WaterFlow.Frames).Select(i => all.SingleOrDefault(e => e.Name == prefix + "_" + i)).ToArray();
                Assert.That(frames, Has.None.Null, prefix + " has all " + WaterFlow.Frames + " frames");
                Assert.That(frames.Select(f => (f.W, f.H)).Distinct().Count(), Is.EqualTo(1), prefix + " frames share one size");
                Assert.That(frames.Select(f => (Math.Round(f.PivotX, 4), Math.Round(f.PivotY, 4))).Distinct().Count(), Is.EqualTo(1), prefix + " frames share one pivot");
            }
        }

        // Grass, earth, soil and scrub each have every variant the hash can pick, at the base's size and pivot (a variant never moves an acre).
        [Test]
        public void EveryGroundVariantIsRenderedAtTheBaseSizeAndPivot()
        {
            Entry[] all = Manifest();
            foreach (string kind in new[] { "ground_grass", "ground_earth", "ground_soil", "ground_scrub" })
            {
                Entry baseEntry = all.Single(e => e.Name == kind);
                for (int v = 1; v < GroundVariants.Count; v++)
                {
                    Entry variant = all.SingleOrDefault(e => e.Name == kind + "_" + v);
                    Assert.That(variant, Is.Not.Null, kind + " variant " + v);
                    Assert.That((variant.W, variant.H), Is.EqualTo((baseEntry.W, baseEntry.H)), kind + "_" + v + " size");
                    Assert.That(variant.PivotX, Is.EqualTo(baseEntry.PivotX).Within(1e-4), kind + "_" + v + " pivot x");
                    Assert.That(variant.PivotY, Is.EqualTo(baseEntry.PivotY).Within(1e-4), kind + "_" + v + " pivot y");
                }
            }
        }
    }
}
