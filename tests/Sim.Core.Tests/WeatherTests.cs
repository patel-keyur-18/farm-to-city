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
    /// The statistical criteria (AC-15 to AC-17) run the spell process for one season's climate over 10 000
    /// spells, which is the process the spec's formulas describe. In the running game a season is only 16
    /// spells and its first spells carry weather over from the previous season;
    /// <see cref="SimSpells_FollowTheGeneratorChain"/> checks that the sim uses exactly this process.
    /// </summary>
    public class WeatherTests
    {
        private const int Spells = 10000;
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
            Assert.That(spells.Count, Is.EqualTo(4));
            Assert.That(spells.Select(s => Calendar.From(s.SimTime, sim.Clock).Hour), Is.EqualTo(new[] { 6, 12, 18, 0 }));
            Assert.That(spells.Select(s => s.SpellIndex), Is.EqualTo(new long[] { 1, 2, 3, 4 }));
            Assert.That(spells.All(s => s.SimTime % (6 * Hour) == 0));
        }

        // Spec 001 AC-15.
        [Test]
        public void ZeroWeight_NeverDrawn()
        {
            Assert.That(Chain(Set.ClimateFor(Season.Summer)), Has.None.EqualTo(Index("frost")));
            Assert.That(Chain(Set.ClimateFor(Season.Monsoon)), Has.None.EqualTo(Index("heatwave")));
        }

        // Spec 001 AC-16.
        [Test]
        public void Frequencies_MatchWeights()
        {
            foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Monsoon, Season.Winter })
            {
                ClimateDef c = Set.ClimateFor(season);
                int[] chain = Chain(c);
                double total = c.Weights.Sum();
                for (int i = 0; i < Set.Types.Count; i++)
                {
                    double observed = chain.Count(w => w == i) / (double)Spells;
                    double expected = c.Weights[i] / total;
                    Assert.That(observed, Is.EqualTo(expected).Within(0.03), season + " " + Set.Types[i].Id);
                }
            }
        }

        // Spec 001 AC-17.
        [Test]
        public void Persistence_MatchesFormula()
        {
            foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Monsoon, Season.Winter })
            {
                ClimateDef c = Set.ClimateFor(season);
                int[] chain = Chain(c);
                int same = 0;
                for (int n = 1; n < Spells; n++) if (chain[n] == chain[n - 1]) same++;
                double observed = same / (double)(Spells - 1);

                double total = c.Weights.Sum(), sumSquares = c.Weights.Sum(w => (w / total) * (w / total));
                double p = double.Parse(c.Persistence.ToString(), System.Globalization.CultureInfo.InvariantCulture);
                double expected = p + (1 - p) * sumSquares;
                Assert.That(observed, Is.EqualTo(expected).Within(0.03), season.ToString());
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
            long lastSpring = 4 * 4 - 1;
            bool found = false;
            for (ulong seed = 1; seed < 3000 && !found; seed++)
            {
                Simulation sim = New(seed: seed);
                sim.AdvanceTo(lastSpring * 6 * Hour);
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
            for (long n = 0; n < 80; n++) // five seasons
            {
                ClimateDef c = Set.ClimateFor(Calendar.SeasonAt(n * 6 * Hour, sim.Clock));
                expected.Add(previous = WeatherRules.SpellWeather(11, n, previous, c));
            }

            var actual = new List<int> { sim.State.Weather.WeatherOf(2) };
            sim.AdvanceTo(79 * 6 * Hour);
            List<WeatherSpellStarted> started = sim.DrainEvents().OfType<WeatherSpellStarted>().ToList();
            actual.AddRange(started.Select(s => Set.IndexOf(s.WeatherId)));
            Assert.That(started.First().SpellIndex, Is.EqualTo(3));
            Assert.That(actual, Is.EqualTo(expected.Skip(2).ToList()));
            Assert.That(sim.CurrentWeather.Id, Is.EqualTo(Set.Types[expected[79]].Id));
        }

        [Test]
        public void Buffer_CoversTheForecast()
        {
            Simulation sim = New(localSeconds: 7 * Hour);
            for (int step = 0; step < 50; step++)
            {
                long current = Calendar.SpellAt(sim.State.Clock.SimTime, sim.Clock);
                Assert.That(sim.State.Weather.FirstSpell, Is.EqualTo(current));
                Assert.That(sim.State.Weather.LastGeneratedSpell, Is.GreaterThanOrEqualTo(current + 12));
                sim.AdvanceTo(sim.State.Clock.SimTime + 5 * Hour + 13);
            }
        }
    }
}
