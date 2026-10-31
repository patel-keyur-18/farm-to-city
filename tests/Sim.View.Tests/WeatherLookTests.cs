using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core;
using Sim.View.Content;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The weather on the map (spec 008 §3.4, AC-19 and AC-20, ADR 0083 §13–18).</summary>
    public class WeatherLookTests
    {
        private const float Frame = 1f / 60f;

        private static WeatherLookDef Def() { return UiLoader.Load(ViewKit.ReadContent("ui.json")).WeatherLook; }

        private static List<WeatherFrame> Run(WeatherLook look, string spell, float seconds, bool reduced = false)
        {
            var frames = new List<WeatherFrame>();
            for (float t = 0; t < seconds; t += Frame) frames.Add(look.Step(Frame, spell, reduced));
            return frames;
        }

        // AC-19: the look is the spell's (every weather of weather.json has one), eased over easeSeconds at a change; the developer panel's
        // pick wins while set, and the spell's look comes back when it's cleared.
        [Test]
        public void FollowsSpell()
        {
            WeatherLookDef def = Def();
            Simulation sim = ViewKit.NewGame();
            foreach (var w in sim.Config.Weather.Types) Assert.That(def.Style(w.Id), Is.Not.Null, "a look for " + w.Id);
            Assert.That(def.EaseSeconds, Is.EqualTo(3f));

            var look = new WeatherLook(def);
            WeatherFrame f = look.Step(Frame, sim.CurrentWeather.Id, false);
            Assert.That((f.To.Id, f.Weight), Is.EqualTo((sim.CurrentWeather.Id, 1f)), "the first frame shows the spell at once");

            Run(look, "sun", 5);
            f = look.Step(Frame, "rain", false);
            Assert.That((f.From.Id, f.To.Id), Is.EqualTo(("sun", "rain")));
            Assert.That(f.Weight, Is.LessThan(0.01f));
            List<WeatherFrame> fade = Run(look, "rain", def.EaseSeconds);
            Assert.That(fade[fade.Count / 2].Weight, Is.InRange(0.4f, 0.6f), "halfway through the fade");
            Assert.That(fade.Zip(fade.Skip(1), (a, b) => b.Weight >= a.Weight).All(x => x), Is.True, "eases one way");
            Assert.That(look.Step(Frame, "rain", false).Weight, Is.EqualTo(1f));

            look.Override = "frost"; // the developer panel's Weather look ▸
            Run(look, "rain", 4);
            Assert.That(look.Step(Frame, "rain", false).To.Id, Is.EqualTo("frost"));
            look.Override = null;
            Run(look, "rain", 4);
            Assert.That(look.Step(Frame, "rain", false).To.Id, Is.EqualTo("rain"));

            // Night keeps less of the added light (haze, mist, frost's pale wash).
            Assert.That(look.Step(Frame, "rain", false, 0.55f).Light, Is.EqualTo(def.NightLight).Within(0.01f));
            Assert.That(look.Step(Frame, "rain", false, 1f).Light, Is.EqualTo(1f));
        }

        // AC-19: with reduced motion nothing moves (Moving false: the presenter freezes rain, clouds and splashes), there's no glow and no
        // gust in a storm; the thunder still comes (Sound decides), and a change still fades.
        [Test]
        public void ReducedMotion_Still()
        {
            var look = new WeatherLook(Def());
            List<WeatherFrame> storm = Run(look, "storm", 120, reduced: true);
            Assert.That(storm.All(f => !f.Moving && f.Glow == 0f && f.Gust == 0f), Is.True);
            Assert.That(storm.Count(f => f.Thunder), Is.GreaterThanOrEqualTo(3), "thunder keeps its own time");
            WeatherFrame change = look.Step(Frame, "sun", true);
            Assert.That(change.Weight, Is.LessThan(0.01f), "a change of weather still fades");
            Assert.That(Def().ReducedStreakAlpha, Is.LessThan(1f), "still streaks are lighter");
        }

        // AC-20: ten minutes of storm at 60 frames a second: every glow rises over at least 0.3 s, comes at least minGap (15 s) after the
        // last and no later than maxGap (25 s), never 3 in a second; the thunder follows 1–3 s after each.
        [Test]
        public void Lightning_NoFlash()
        {
            WeatherLookDef def = Def();
            GlowDef g = def.Style("storm").Glow;
            Assert.That((g.RiseSeconds, g.MinGap, g.MaxGap), Is.EqualTo((0.4f, 15f, 25f)));
            var look = new WeatherLook(def, 7);
            List<WeatherFrame> frames = Run(look, "storm", 600);
            List<float> onsets = frames.Select((f, i) => (f, i)).Where(x => x.f.Lightning).Select(x => x.i * Frame).ToList();
            Assert.That(onsets.Count, Is.InRange(600 / 25 - 1, 600 / 15));
            Assert.That(onsets[0], Is.GreaterThanOrEqualTo(g.MinGap), "never straight away");
            List<float> gaps = onsets.Zip(onsets.Skip(1), (a, b) => b - a).ToList();
            Assert.That(gaps.Min(), Is.GreaterThanOrEqualTo(g.MinGap - Frame));
            Assert.That(gaps.Max(), Is.LessThanOrEqualTo(g.MaxGap + Frame));
            for (int i = 0; i < frames.Count; i++)
                Assert.That(frames.Skip(i).Take(60).Count(f => f.Lightning), Is.LessThanOrEqualTo(1), "never 3 (or even 2) in a second");

            // The glow climbs to its peak over the rise (no jump from dark to bright), then fades.
            int start = (int)System.Math.Round(onsets[0] / Frame);
            List<float> rise = frames.Skip(start - 1).Take((int)(g.RiseSeconds / Frame) + 1).Select(f => f.Glow).ToList(); // from the dark frame before
            Assert.That(rise.Zip(rise.Skip(1), (a, b) => b - a).Max(), Is.LessThanOrEqualTo(g.Peak * Frame / 0.3f + 1e-4f), "no faster than a 0.3 s rise");
            Assert.That(frames.Max(f => f.Glow), Is.LessThanOrEqualTo(g.Peak + 1e-4f));
            List<int> thunder = frames.Select((f, i) => (f, i)).Where(x => x.f.Thunder).Select(x => x.i).ToList();
            Assert.That(thunder.Count, Is.InRange(onsets.Count - 1, onsets.Count));
            Assert.That(thunder[0] * Frame - onsets[0], Is.InRange(1f, 3f + Frame));

            // No-flash bounds hold in the data itself: the loader refuses a faster rise or a glow more than once a second.
            string json = ViewKit.ReadContent("ui.json");
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(json.Replace("\"riseSeconds\": 0.4", "\"riseSeconds\": 0.1"))).FieldPath, Does.EndWith("riseSeconds"));
            Assert.That(Assert.Throws<ContentException>(() => UiLoader.Load(json.Replace("\"minGap\": 15", "\"minGap\": 0.5"))).FieldPath, Does.EndWith("minGap"));
            Assert.That(Run(new WeatherLook(def), "rain", 120).All(f => f.Glow == 0f && !f.Lightning), Is.True, "only a storm has lightning");
        }
    }
}
