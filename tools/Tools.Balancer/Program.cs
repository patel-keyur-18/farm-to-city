using System;
using System.Collections.Generic;
using System.IO;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Time;
using Sim.Core.Weather;

namespace Tools.Balancer
{
    /// <summary>
    /// Headless balancer on a fake clock (ADR 0016, 0018). Modes:
    /// <list type="bullet">
    /// <item><c>[weather] [years] [seed]</c>: spec 001's hooks (weather frequency, rainy Monsoon spells, forecast accuracy).</item>
    /// <item><c>moisture [years] [seed]</c>: spec 002's seasonal moisture hook per soil.</item>
    /// <item><c>economy [years] [seed]</c>: the seasonal farm cash-flow model (US-013), 15 years by default.</item>
    /// </list>
    /// Usage: dotnet run --project tools/Tools.Balancer -- [mode] [years] [seed]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string content = FindContentFolder();
            bool named = args.Length > 0 && !char.IsDigit(args[0][0]);
            string mode = named ? args[0] : "weather";
            if (named) args = args[1..];
            int years = args.Length > 0 ? int.Parse(args[0]) : mode == "economy" ? 15 : 100;
            ulong seed = args.Length > 1 ? ulong.Parse(args[1]) : 1;
            if (mode == "moisture")
            {
                Moisture.Print(Simulation.NewGame(Config(content), new NewGame(seed, 0, 0)), years);
                return 0;
            }
            if (mode == "economy") return Economy.Run(Config(content), years, seed);

            ClockDef clock = ContentLoader.LoadClock(File.ReadAllText(Path.Combine(content, "clock.json")));
            WeatherSet weather = ContentLoader.LoadWeather(File.ReadAllText(Path.Combine(content, "weather.json")));
            Simulation sim = Simulation.NewGame(new SimConfig(clock, weather), new NewGame(seed, 0, 0));

            int types = weather.Types.Count;
            var counts = new long[4, types];
            var spellsPerSeason = new long[4];
            // spell → the weather each bucket first showed for it (−1 = not shown yet); keyed lookups only.
            var firstShown = new Dictionary<long, int[]>();
            var hits = new long[clock.ForecastDays];
            var samples = new long[clock.ForecastDays];

            long end = clock.SecondsPerYear * years;
            for (long t = clock.SpellSeconds; t <= end; t += clock.SpellSeconds)
            {
                foreach (ForecastEntry f in sim.Forecast())
                {
                    if (!firstShown.TryGetValue(f.SpellIndex, out int[] byBucket))
                    {
                        byBucket = new int[clock.ForecastDays];
                        for (int b = 0; b < byBucket.Length; b++) byBucket[b] = -1;
                        firstShown[f.SpellIndex] = byBucket;
                    }
                    if (byBucket[f.Bucket] < 0) byBucket[f.Bucket] = f.Weather;
                }

                sim.AdvanceTo(t);
                foreach (SimEvent e in sim.DrainEvents())
                {
                    if (!(e is WeatherSpellStarted spell)) continue;
                    int season = (int)Calendar.SeasonAt(spell.SimTime, clock);
                    counts[season, weather.IndexOf(spell.WeatherId)]++;
                    spellsPerSeason[season]++;
                }

                // Score every forecast made for the spell that has just started.
                long current = Calendar.SpellAt(t, clock);
                if (firstShown.TryGetValue(current, out int[] made))
                {
                    int actual = sim.State.Weather.WeatherOf(current);
                    for (int b = 0; b < made.Length; b++)
                    {
                        if (made[b] < 0) continue;
                        samples[b]++;
                        if (made[b] == actual) hits[b]++;
                    }
                    firstShown.Remove(current);
                }
            }

            Console.WriteLine("Farm to City balancer, sim " + SimInfo.Version + ". " + years + " game years, seed " + seed + ".");
            Console.WriteLine();
            Console.WriteLine("Weather frequency per season, % of spells (target in brackets):");
            Console.Write("  " + "Season".PadRight(9));
            foreach (WeatherTypeDef t in weather.Types) Console.Write(t.Id.PadLeft(16));
            Console.WriteLine();
            for (int s = 0; s < 4; s++)
            {
                ClimateDef c = weather.ClimateFor((Season)s);
                long total = 0;
                for (int i = 0; i < types; i++) total += c.Weights[i];
                Console.Write("  " + ((Season)s).ToString().PadRight(9));
                for (int i = 0; i < types; i++)
                {
                    string cell = Pct(counts[s, i], spellsPerSeason[s]) + " (" + Pct(c.Weights[i], total) + ")";
                    Console.Write(cell.PadLeft(16));
                }
                Console.WriteLine();
            }

            int m = (int)Season.Monsoon;
            long rainy = counts[m, weather.IndexOf("rain")] + counts[m, weather.IndexOf("heavy_rain")];
            Console.WriteLine();
            Console.WriteLine("Rainy Monsoon spells (rain + heavy rain): " + Pct(rainy, spellsPerSeason[m]) + "% (target about 55%, ±5)");
            Console.WriteLine();
            Console.WriteLine("Measured forecast accuracy (target in brackets):");
            for (int b = 0; b < clock.ForecastDays; b++)
                Console.WriteLine("  Day " + (b + 1) + ": " + Pct(hits[b], samples[b]) + "% (" + (clock.ForecastAccuracy[b].Raw / 4294967296.0 * 100).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "%)");
            return 0;
        }

        /// <summary>The full game content from <paramref name="content"/>.</summary>
        public static SimConfig Config(string content)
        {
            Func<string, string> read = f => File.ReadAllText(Path.Combine(content, f));
            WeatherSet weather = ContentLoader.LoadWeather(read("weather.json"));
            return new SimConfig(ContentLoader.LoadClock(read("clock.json")), weather, game: ContentLoader.LoadGame(read, weather));
        }

        private static string Pct(long part, long whole)
        {
            return whole == 0 ? "-" : (100.0 * part / whole).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string FindContentFolder()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FarmToCity.sln"))) dir = dir.Parent;
            if (dir == null) throw new InvalidOperationException("Run from inside the repository: content/ not found.");
            return Path.Combine(dir.FullName, "content");
        }
    }
}
