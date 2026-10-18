using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Time;
using UnityEngine;

namespace Game.Presentation.Phase0
{
    /// <summary>
    /// The on-device determinism check (US-023, ADR 0027, 0034 §8): replays the golden files inside the player
    /// (IL2CPP on iOS) on the same frozen content as <c>dotnet test</c> and compares the state hashes. Also times a
    /// 72 h away advance of the real sim (the GDD's catch-up budget is 2 s).
    /// </summary>
    public static class GoldenCheck
    {
        public sealed class Row
        {
            public string Name, Expected, Actual;
            public bool Pass;
            public double Milliseconds;
        }

        /// <summary>tests/Sim.Core.Tests/golden in the Editor; the copy in StreamingAssets/golden in a player.</summary>
        public static string Folder
        {
            get
            {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "tests", "Sim.Core.Tests", "golden"));
#else
                return Path.Combine(Application.streamingAssetsPath, "golden");
#endif
            }
        }

        private static SimConfig Config(bool game, bool dev)
        {
            string dir = Path.Combine(Folder, "content");
            Func<string, string> read = f => File.ReadAllText(Path.Combine(dir, f));
            WeatherSet weather = ContentLoader.LoadWeather(read("weather.json"));
            return new SimConfig(ContentLoader.LoadClock(read("clock.json")), weather, new SimOptions { DevCommandsEnabled = dev },
                game: game ? ContentLoader.LoadGame(read, weather) : null);
        }

        public static List<Row> RunAll()
        {
            var rows = new List<Row>();
            foreach (string path in Directory.GetFiles(Folder, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                var row = new Row { Name = Path.GetFileName(path) };
                try
                {
                    ReplayFile file = ReplayJson.Load(File.ReadAllText(path));
                    var watch = Stopwatch.StartNew();
                    ulong hash = Simulation.Replay(Config(file.Game, file.DevCommandsEnabled), file.Commands).State.ComputeHash();
                    watch.Stop();
                    row.Milliseconds = watch.Elapsed.TotalMilliseconds;
                    row.Actual = "0x" + hash.ToString("X16");
                    row.Expected = file.ExpectedHash.HasValue ? "0x" + file.ExpectedHash.Value.ToString("X16") : "(none)";
                    row.Pass = file.ExpectedHash == hash;
                }
                catch (Exception e)
                {
                    row.Actual = "error: " + e.GetType().Name + ": " + e.Message;
                    row.Expected = "";
                }
                rows.Add(row);
            }
            return rows;
        }

        /// <summary>A 72 h away advance of the default farm, fully sown, with trees and the market (spec 003 AC-29, 004 AC-17).</summary>
        public static double Advance72hMilliseconds()
        {
            Simulation sim = Simulation.NewGame(Config(true, false), new NewGame(3, 1_800_000_000, 8 * 3600));
            long t = sim.State.Clock.SimTime;
            foreach (int tile in new[] { 18, 19, 20, 26, 27, 28 }) sim.Execute(new Sow(t, tile, "radish", Sim.Core.Farm.SeedSource.Certified, "none"));
            var watch = Stopwatch.StartNew();
            sim.AdvanceTo(t + 72 * 3600, AdvanceMode.Away);
            watch.Stop();
            return watch.Elapsed.TotalMilliseconds;
        }
    }
}
