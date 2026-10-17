using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Time;
using Sim.Core.Weather;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class ForecastTests
    {
        private static readonly ClockDef Clock = TestContent.Clock();
        private static readonly WeatherSet Set = TestContent.Weather();

        // Spec 001 AC-19.
        [Test]
        public void AccuracyByBucket()
        {
            // In the running sim, spells 1–4 ahead always show the actual weather.
            Simulation sim = New(seed: 5);
            for (int step = 0; step < 200; step++)
            {
                foreach (ForecastEntry f in sim.Forecast().Where(f => f.Bucket == 0))
                    Assert.That(f.Weather, Is.EqualTo(sim.State.Weather.WeatherOf(f.SpellIndex)));
                sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            }

            // 10 000 samples per bucket: spells with every season's climate and every actual weather it allows.
            var matches = new int[3];
            const int samples = 10000;
            for (int bucket = 0; bucket < 3; bucket++)
            {
                for (long s = 0; s < samples; s++)
                {
                    Season season = (Season)(s % 4);
                    ClimateDef c = Set.ClimateFor(season);
                    int actual = WeatherRules.SpellWeather(77, s, -1, c);
                    if (WeatherRules.ForecastWeather(77, s, bucket, actual, c, Clock) == actual) matches[bucket]++;
                }
            }
            Assert.That(matches[0], Is.EqualTo(samples));
            Assert.That(matches[1] / (double)samples, Is.InRange(0.77, 0.83));
            Assert.That(matches[2] / (double)samples, Is.InRange(0.62, 0.68));
        }

        [Test]
        public void Alternative_NeverEqualsActual_UnlessNothingElseIsPossible()
        {
            ClimateDef summer = Set.ClimateFor(Season.Summer);
            int sun = Set.IndexOf("sun");
            for (long s = 0; s < 2000; s++)
            {
                int shown = WeatherRules.ForecastWeather(3, s, 2, sun, summer, Clock);
                if (shown != sun) Assert.That(summer.Weights[shown], Is.GreaterThan(0), "alternatives come from the season's weights");
            }

            // A climate with a single possible weather: the alternative falls back to the actual weather.
            var only = new ClimateDef(Season.Spring, new[] { 1, 0, 0, 0, 0, 0, 0 }, Sim.Core.Maths.Fixed.Zero);
            for (long s = 0; s < 200; s++)
                Assert.That(WeatherRules.ForecastWeather(3, s, 2, 0, only, Clock), Is.EqualTo(0));
        }

        // Spec 001 AC-20.
        [Test]
        public void Stable_RevisedOnlyOnBucketChange()
        {
            Simulation sim = New(seed: 21, localSeconds: 3 * Hour);
            Assert.That(sim.Forecast(), Is.EqualTo(sim.Forecast()), "asking twice gives the same answer");

            // Track what each spell has shown, and in which bucket, across 3 game years of spells.
            var shown = new Dictionary<long, ForecastEntry>(); // looked up by key only, never iterated
            foreach (ForecastEntry f in sim.Forecast()) shown[f.SpellIndex] = f;
            int revisions = 0, changes = 0;
            for (int step = 0; step < 3 * 64; step++)
            {
                long mid = sim.State.Clock.SimTime + 2 * Hour;
                sim.AdvanceTo(mid);
                Assert.That(sim.Forecast(), Is.EqualTo(sim.Forecast()));
                sim.AdvanceTo((mid / (6 * Hour) + 1) * 6 * Hour); // the next spell boundary

                List<ForecastRevised> revised = sim.DrainEvents().OfType<ForecastRevised>().ToList();
                var expectedRevised = new List<string>();
                foreach (ForecastEntry now in sim.Forecast())
                {
                    if (shown.TryGetValue(now.SpellIndex, out ForecastEntry before))
                    {
                        if (before.Bucket == now.Bucket)
                            Assert.That(now.Weather, Is.EqualTo(before.Weather), "no change within a bucket");
                        else
                        {
                            Assert.That(now.Bucket, Is.EqualTo(before.Bucket - 1), "spells only move to nearer buckets");
                            if (now.Weather != before.Weather)
                            {
                                changes++;
                                expectedRevised.Add(now.SpellIndex + ":" + Set.Types[before.Weather].Id + "->" + Set.Types[now.Weather].Id);
                            }
                        }
                    }
                    shown[now.SpellIndex] = now;
                }
                revisions += revised.Count;
                Assert.That(revised.Select(r => r.SpellIndex + ":" + r.OldWeatherId + "->" + r.NewWeatherId), Is.EqualTo(expectedRevised));
            }
            Assert.That(changes, Is.GreaterThan(0), "the run should include some revisions");
            Assert.That(revisions, Is.EqualTo(changes));
        }
    }
}
