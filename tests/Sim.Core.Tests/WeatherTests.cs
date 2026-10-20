using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Time;
using Sim.Core.Weather;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>
    /// The statistical criteria (AC-15 to AC-17) run the spell process for one season's climate over 200 000
    /// hourly spells, which is the process the spec's formulas describe. In the running game a season is only 96
    /// spells and its first spells carry weather over from the previous season;
    /// <see cref="SimSpells_FollowTheGeneratorChain"/> checks that the sim uses exactly this process.
    /// </summary>
    public class WeatherTests
    {
        private const int Spells = 200000;
        private static readonly WeatherSet Set = TestContent.Weather();

        private static int[] Chain(ClimateDef climate, ulong seed = 2026)
        {
            var chain = new int[Spells];
            int previous = -1;
            for (int n = 0; n < Spells; n++)
                chain[n] = previous = WeatherRules.SpellWeather(seed, n, previous, climate);
            return chain;
        }

        private static int Index(string id) { return Set.IndexOf(id); }

        // Spec 001 AC-14.
        [Test]
        public void SpellBoundaries()
        {
            Simulation sim = New(localSeconds: 0);
            sim.AdvanceTo(Day);
            List<WeatherSpellStarted> spells = sim.DrainEvents().OfType<WeatherSpellStarted>().ToList();
            Assert.That(spells.Count, Is.EqualTo(24));
            Assert.That(spells.Select(s => Calendar.From(s.SimTime, sim.Clock).Hour), Is.EqualTo(Enumerable.Range(1, 23).Append(0)));
            Assert.That(spells.Select(s => s.SpellIndex), Is.EqualTo(Enumerable.Range(1, 24).Select(i => (long)i)));
            Assert.That(spells.All(s => s.SimTime % Hour == 0));
        }

        // Spec 001 AC-15.
        [Test]
        public void ZeroWeight_NeverDrawn()
        {
            Assert.That(Chain(Set.ClimateFor(Season.Summer)), Has.None.EqualTo(Index("frost")));
            Assert.That(Chain(Set.ClimateFor(Season.Monsoon)), Has.None.EqualTo(Index("heatwave")));
        }

        private static readonly Season[] Seasons = { Season.Spring, Season.Summer, Season.Monsoon, Season.Winter };

        // Spec 001 AC-16: the long-run share of every weather equals its share, with draw weights derived from it.
        [Test]
        public void Frequencies_MatchShares()
        {
            foreach (Season season in Seasons)
            {
                ClimateDef c = Set.ClimateFor(season);
                int[] chain = Chain(c);
                double total = c.Weights.Sum();
                for (int i = 0; i < Set.Types.Count; i++)
                {
                    double observed = chain.Count(w => w == i) / (double)Spells;
                    double expected = c.Weights[i] / total;
                    Assert.That(observed, Is.EqualTo(expected).Within(0.02), season + " " + Set.Types[i].Id);
                }
            }
        }

        /// <summary>Spec 001 §4: the mean runs, in hours, that the persistence table gives (0 = the weather has no share).</summary>
        private static readonly double[][] TableRuns =
        {
            new[] { 29, 10, 6, 1.5, 6.0, 3, 0 },
            new[] { 37, 6, 4, 0, 8, 0, 1.8 },
            new[] { 6, 8, 18, 2.2, 0, 0, 1.5 },
            new[] { 37, 12, 6.0, 0, 0, 4, 0 },
        };

        // Spec 001 AC-17: each weather's mean run is 1 / ((1 − pᵢ)(1 − w̄ᵢ)) and matches the table.
        [Test]
        public void RunLengths_MatchFormula()
        {
            foreach (Season season in Seasons)
            {
                ClimateDef c = Set.ClimateFor(season);
                int[] chain = Chain(c);
                var runs = new long[Set.Types.Count];
                var hours = new long[Set.Types.Count];
                for (int n = 0; n < Spells; n++)
                {
                    hours[chain[n]]++;
                    if (n == 0 || chain[n] != chain[n - 1]) runs[chain[n]]++;
                }
                double drawTotal = c.DrawWeights.Sum();
                for (int i = 0; i < Set.Types.Count; i++)
                {
                    if (c.Weights[i] == 0) continue;
                    double p = double.Parse(c.Persistence[i].ToString(), System.Globalization.CultureInfo.InvariantCulture);
                    double formula = 1 / ((1 - p) * (1 - c.DrawWeights[i] / drawTotal));
                    double observed = hours[i] / (double)runs[i];
                    string what = season + " " + Set.Types[i].Id;
                    Assert.That(observed, Is.EqualTo(formula).Within(10).Percent, what);
                    Assert.That(formula, Is.EqualTo(TableRuns[(int)season][i]).Within(5).Percent, what + " (the spec's table)");
                }
            }
        }

        // Spec 001 AC-18.
        [Test]
        public void SeasonChange_DropsInvalidCarryOver()
        {
            ClimateDef summer = Set.ClimateFor(Season.Summer);
            int frost = Index("frost");
            for (long n = 0; n < Spells; n++)
            {
                int w = WeatherRules.SpellWeather(7, n, frost, summer);
                Assert.That(w, Is.Not.EqualTo(frost));
                // Drawn from Summer's weights exactly as a fresh spell would be (counter 1, no persistence roll).
                Assert.That(w, Is.EqualTo(WeatherRules.SpellWeather(7, n, -1, summer)));
            }

            // In the sim: find a world whose last Spring spell is Frost, and check Summer's first spell.
            long lastSpring = 4 * 24 - 1;
            bool found = false;
            for (ulong seed = 1; seed < 3000 && !found; seed++)
            {
                Simulation sim = New(seed: seed);
                sim.AdvanceTo(lastSpring * Hour);
                if (sim.State.Weather.WeatherOf(lastSpring) != frost) continue;
                found = true;
                Assert.That(sim.State.Weather.WeatherOf(lastSpring + 1), Is.Not.EqualTo(frost), "seed " + seed);
            }
            Assert.That(found, Is.True, "no seed with a frosty last Spring spell");
        }

        [Test]
        public void SimSpells_FollowTheGeneratorChain()
        {
            Simulation sim = New(seed: 11, localSeconds: 13 * Hour);
            int previous = -1;
            var expected = new List<int>();
            for (long n = 0; n < 480; n++) // five seasons
            {
                ClimateDef c = Set.ClimateFor(Calendar.SeasonAt(n * Hour, sim.Clock));
                expected.Add(previous = WeatherRules.SpellWeather(11, n, previous, c));
            }

            var actual = new List<int> { sim.State.Weather.WeatherOf(13) };
            sim.AdvanceTo(479 * Hour);
            List<WeatherSpellStarted> started = sim.DrainEvents().OfType<WeatherSpellStarted>().ToList();
            actual.AddRange(started.Select(s => Set.IndexOf(s.WeatherId)));
            Assert.That(started.First().SpellIndex, Is.EqualTo(14));
            Assert.That(actual, Is.EqualTo(expected.Skip(13).ToList()));
            Assert.That(sim.CurrentWeather.Id, Is.EqualTo(Set.Types[expected[479]].Id));

            // The current run's first hour (spec 001 §3.6) follows the chain.
            long start = 479;
            while (start > 0 && expected[(int)start - 1] == expected[479]) start--;
            Assert.That(sim.State.Weather.CurrentRunStart, Is.EqualTo(start));
        }

        [Test]
        public void Buffer_CoversTheForecast()
        {
            Simulation sim = New(localSeconds: 7 * Hour);
            for (int step = 0; step < 50; step++)
            {
                long current = Calendar.SpellAt(sim.State.Clock.SimTime, sim.Clock);
                Assert.That(sim.State.Weather.FirstSpell, Is.EqualTo(current));
                Assert.That(sim.State.Weather.LastGeneratedSpell, Is.GreaterThanOrEqualTo(current + 72));
                sim.AdvanceTo(sim.State.Clock.SimTime + 5 * Hour + 13);
            }
        }
    }
}
