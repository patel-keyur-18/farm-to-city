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
        public void AccuracyByBucket_RolledPerRun()
        {
            // In the running sim, hours 1–24 ahead always show the actual weather, and every hour of one actual run in one
            // bucket shows the same weather (rolled per run, ADR 0043).
            Simulation sim = New(seed: 5);
            for (int step = 0; step < 200; step++)
            {
                IReadOnlyList<ForecastEntry> f = sim.Forecast();
                Assert.That(f.Count, Is.EqualTo(72));
                for (int k = 0; k < f.Count; k++)
                {
                    if (f[k].Bucket == 0) Assert.That(f[k].Weather, Is.EqualTo(sim.State.Weather.WeatherOf(f[k].SpellIndex)));
                    if (k > 0 && f[k].Bucket == f[k - 1].Bucket && sim.State.Weather.WeatherOf(f[k].SpellIndex) == sim.State.Weather.WeatherOf(f[k - 1].SpellIndex)
                        && Calendar.SeasonAt(f[k].SpellIndex * Hour, sim.Clock) == Calendar.SeasonAt(f[k - 1].SpellIndex * Hour, sim.Clock))
                        Assert.That(f[k].Weather, Is.EqualTo(f[k - 1].Weather), "one run, one bucket, one forecast");
                }
                sim.AdvanceTo(sim.State.Clock.SimTime + 7 * Hour);
            }

            // Per hour over 2 000 seeds: hours 25–48 match 77–83% of the time, hours 49–72 match 62–68%.
            var hits = new int[3];
            var hours = new int[3];
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                Simulation s = New(seed: seed, localSeconds: (long)(seed % 24) * Hour);
                foreach (ForecastEntry e in s.Forecast())
                {
                    hours[e.Bucket]++;
                    if (e.Weather == s.State.Weather.WeatherOf(e.SpellIndex)) hits[e.Bucket]++;
                }
            }
            Assert.That(hits[0], Is.EqualTo(hours[0]));
            Assert.That(hits[1] / (double)hours[1], Is.InRange(0.77, 0.83));
            Assert.That(hits[2] / (double)hours[2], Is.InRange(0.62, 0.68));

            // 10 000 samples per bucket: spells with every season's climate and every actual weather it allows.
            var matches = new int[3];
            const int samples = 10000;
            for (int bucket = 0; bucket < 3; bucket++)
            {
                for (long s = 0; s < samples; s++)
                {
                    Season season = (Season)(s % 4);
                    ClimateDef c = Set.ClimateFor(season);
                    int actual = WeatherRules.SpellWeather(77, s, -1, c); // s is the run's first hour here
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
            var only = new ClimateDef(Season.Spring, new[] { 1, 0, 0, 0, 0, 0, 0 }, new Sim.Core.Maths.Fixed[7]);
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
            for (int step = 0; step < 3 * 384; step++) // three game years of hourly spells
            {
                long mid = sim.State.Clock.SimTime + Hour / 2;
                sim.AdvanceTo(mid);
                Assert.That(sim.Forecast(), Is.EqualTo(sim.Forecast()));
                sim.AdvanceTo((mid / Hour + 1) * Hour); // the next spell boundary

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
