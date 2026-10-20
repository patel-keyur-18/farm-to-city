using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Weather
{
    /// <summary>
    /// Generates the weather spells and keeps the forecast (spec 001 §3.5–3.6). Runs in the hourly pass;
    /// a spell is 1 hour by default (ADR 0042).
    /// </summary>
    internal sealed class WeatherSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Weather; } }

        /// <summary>Sets up the buffer and the forecast for a new game.</summary>
        public static void Initialise(Simulation sim)
        {
            WeatherState w = sim.State.Weather;
            long current = Calendar.SpellAt(sim.State.Clock.SimTime, sim.Clock);
            w.FirstSpell = 0;
            Generate(sim, current + sim.Clock.ForecastSpells);
            w.CurrentRunStart = 0;
            TrackRunStart(w, current);
            w.DropBefore(current);
            w.SetForecast(ComputeForecast(sim, current));
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass != ItemClass.Hourly) return;
            long t = sim.State.Clock.SimTime;
            if (t % sim.Clock.SpellSeconds != 0) return;

            WeatherState w = sim.State.Weather;
            long spell = Calendar.SpellAt(t, sim.Clock);
            Generate(sim, spell + sim.Clock.ForecastSpells);
            TrackRunStart(w, spell);
            w.DropBefore(spell);
            sim.Emit(new WeatherSpellStarted(t, spell, sim.Weather.Types[w.WeatherOf(spell)].Id));

            List<ForecastEntry> next = ComputeForecast(sim, spell);
            foreach (ForecastEntry now in next)
            {
                foreach (ForecastEntry before in w.ForecastShown)
                {
                    if (before.SpellIndex != now.SpellIndex) continue;
                    if (before.Bucket != now.Bucket && before.Weather != now.Weather)
                        sim.Emit(new ForecastRevised(t, now.SpellIndex, sim.Weather.Types[before.Weather].Id, sim.Weather.Types[now.Weather].Id));
                    break;
                }
            }
            w.SetForecast(next);
        }

        /// <summary>Moves <see cref="WeatherState.CurrentRunStart"/> along the buffer up to <paramref name="spell"/>.</summary>
        private static void TrackRunStart(WeatherState w, long spell)
        {
            for (long n = System.Math.Max(w.FirstSpell + 1, w.CurrentRunStart + 1); n <= spell; n++)
                if (w.WeatherOf(n) != w.WeatherOf(n - 1)) w.CurrentRunStart = n;
        }

        /// <summary>The forecast for the spells after <paramref name="current"/>, rolled per actual run (ADR 0043); stable for a given state.</summary>
        public static List<ForecastEntry> ComputeForecast(Simulation sim, long current)
        {
            ClockDef clock = sim.Clock;
            WeatherState w = sim.State.Weather;
            var list = new List<ForecastEntry>(clock.ForecastSpells);
            long runStart = w.CurrentRunStart;
            int previous = w.WeatherOf(current);
            for (long k = 1; k <= clock.ForecastSpells; k++)
            {
                long spell = current + k;
                int actual = w.WeatherOf(spell);
                if (actual != previous) runStart = spell;
                previous = actual;
                int bucket = WeatherRules.Bucket(k, clock);
                ClimateDef climate = sim.Weather.ClimateFor(SeasonOfSpell(spell, clock));
                int shown = WeatherRules.ForecastWeather(sim.State.Seed, runStart, bucket, actual, climate, clock);
                list.Add(new ForecastEntry(spell, bucket, shown));
            }
            return list;
        }

        /// <summary>Extends the buffer, in spell order, up to and including <paramref name="lastSpell"/>.</summary>
        private static void Generate(Simulation sim, long lastSpell)
        {
            WeatherState w = sim.State.Weather;
            for (long n = w.LastGeneratedSpell + 1; n <= lastSpell; n++)
            {
                int previous = n == 0 ? -1 : w.WeatherOf(n - 1);
                ClimateDef climate = sim.Weather.ClimateFor(SeasonOfSpell(n, sim.Clock));
                w.Append(WeatherRules.SpellWeather(sim.State.Seed, n, previous, climate));
            }
        }

        /// <summary>The season that contains the start of the spell.</summary>
        private static Season SeasonOfSpell(long spell, ClockDef clock)
        {
            return Calendar.SeasonAt(spell * clock.SpellSeconds, clock);
        }
    }
}
