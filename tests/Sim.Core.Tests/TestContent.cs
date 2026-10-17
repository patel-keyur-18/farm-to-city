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
        /// The land, map, buildings, start and finance content. <paramref name="edit"/> (file name, JSON) → JSON
        /// lets a test change one file.
        /// </summary>
        public static GameDefs Game(Func<string, string, string> edit = null)
        {
            Func<string, string> read = f => edit == null ? ReadContent(f) : edit(f, ReadContent(f));
            return ContentLoader.LoadGame(read("land.json"), read("map.json"), read("buildings.json"),
                read("start.json"), read("finance.json"), Weather());
        }
    }
}
