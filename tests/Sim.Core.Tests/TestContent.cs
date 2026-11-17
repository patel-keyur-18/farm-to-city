using System;
using System.IO;
using Sim.Content;
using Sim.Core.Definitions;

namespace Sim.Core.Tests
{
    /// <summary>Reads the real files in <c>content/</c> and test fixtures from the repository.</summary>
    public static class TestContent
    {
        private static string _root;

        /// <summary>The repository root: the nearest folder above the test binaries that holds FarmToCity.sln.</summary>
        public static string RepoRoot
        {
            get
            {
                if (_root != null) return _root;
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FarmToCity.sln"))) dir = dir.Parent;
                if (dir == null) throw new InvalidOperationException("FarmToCity.sln not found above " + AppContext.BaseDirectory);
                return _root = dir.FullName;
            }
        }

        public static string ReadContent(string file) { return File.ReadAllText(Path.Combine(RepoRoot, "content", file)); }

        public static string TestFile(string relative) { return Path.Combine(RepoRoot, "tests", "Sim.Core.Tests", relative); }

        public static ClockDef Clock() { return ContentLoader.LoadClock(ReadContent("clock.json")); }

        public static WeatherSet Weather() { return ContentLoader.LoadWeather(ReadContent("weather.json")); }

        /// <summary>
        /// The game content (everything but the clock and weather). <paramref name="edit"/> (file name, JSON) → JSON
        /// lets a test change one file.
        /// </summary>
        public static GameDefs Game(Func<string, string, string> edit = null, WeatherSet weather = null)
        {
            // Tests of the treasury, taxes and policies count your own town only: the old village (30 households at the Township's unlock,
            // spec 023 §3.5) is left out unless the test asks for it with <see cref="WithCohort"/>.
            return ContentLoader.LoadGame(f => { string j = edit == null ? ReadContent(f) : edit(f, ReadContent(f)); return f == "township.json" && !WithCohort ? NoCohort(j) : j; }, weather ?? Weather());
        }

        /// <summary>True while a test wants the old village at the unlock (set and cleared by the cohort's tests).</summary>
        public static bool WithCohort;

        internal static string NoCohort(string json) { return json.Replace("\"households\": 30,", "\"households\": 0,"); }
    }
}
