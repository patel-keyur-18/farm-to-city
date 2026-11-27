using System;
using System.IO;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;

namespace Game.Services.Tests
{
    /// <summary>Small towns from the real content files, for the slot tests.</summary>
    public static class TownKit
    {
        public const long Day = 86400;
        public const long Hour = 3600;
        public const long Wall0 = 1_800_000_000;

        private static string Root
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FarmToCity.sln"))) dir = dir.Parent;
                if (dir == null) throw new InvalidOperationException("FarmToCity.sln not found above " + AppContext.BaseDirectory);
                return dir.FullName;
            }
        }

        public static string ReadContent(string file) { return File.ReadAllText(Path.Combine(Root, "content", file)); }

        public static SaveDef SaveDef() { return ContentLoader.LoadSaves(ReadContent("saves.json")).Save; }

        public static Simulation NewTown(ulong seed)
        {
            WeatherSet weather = ContentLoader.LoadWeather(ReadContent("weather.json"));
            GameDefs game = ContentLoader.LoadGame(ReadContent, weather);
            var config = new SimConfig(ContentLoader.LoadClock(ReadContent("clock.json")), weather, null, null, null, game);
            return Simulation.NewGame(config, new NewGame(seed, Wall0, 0));
        }
    }
}
