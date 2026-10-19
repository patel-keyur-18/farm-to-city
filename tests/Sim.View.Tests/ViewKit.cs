using System.IO;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.View.Text;

namespace Sim.View.Tests
{
    /// <summary>Shared setup: the real content, the English string table and a new game.</summary>
    public static class ViewKit
    {
        private static string _root;

        public static string RepoRoot
        {
            get
            {
                if (_root != null) return _root;
                var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FarmToCity.sln"))) dir = dir.Parent;
                if (dir == null) throw new System.InvalidOperationException("FarmToCity.sln not found above " + System.AppContext.BaseDirectory);
                return _root = dir.FullName;
            }
        }

        public static string ReadContent(string file) { return File.ReadAllText(Path.Combine(RepoRoot, "content", file)); }

        public static StringTable Strings(bool throwOnMissing = true)
        {
            StringTable t = StringTable.Load(ReadContent("strings/en.json"));
            t.ThrowOnMissing = throwOnMissing;
            return t;
        }

        public static Formatter Indian() { return new Formatter(Strings(), NumberFormat.Indian); }

        public static Formatter Western() { return new Formatter(Strings(), NumberFormat.Western); }

        public static SimConfig Config(System.Func<string, string, string> edit = null, SimOptions options = null)
        {
            WeatherSet weather = ContentLoader.LoadWeather(ReadContent("weather.json"));
            GameDefs game = ContentLoader.LoadGame(f => edit == null ? ReadContent(f) : edit(f, ReadContent(f)), weather);
            return new SimConfig(ContentLoader.LoadClock(ReadContent("clock.json")), weather, options, game: game);
        }

        public static Simulation NewGame(ulong seed = 42, long localSeconds = 8 * 3600, System.Func<string, string, string> edit = null, SimOptions options = null)
        {
            return Simulation.NewGame(Config(edit, options), new NewGame(seed, 1_800_000_000, localSeconds));
        }
    }
}

namespace Sim.View.Tests
{
    using Sim.View.Content;
    using Sim.View.Views;

    public static class ViewKit2
    {
        public static UiDef Ui() { return UiLoader.Load(ViewKit.ReadContent("ui.json")); }

        public static ViewContext Context(Simulation sim, NumberFormat format = NumberFormat.Indian)
        {
            return new ViewContext(sim, ViewKit.Strings(), new Formatter(ViewKit.Strings(), format), Ui());
        }
    }
}
