using System;

using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Time;

namespace Tools.Balancer
{
    /// <summary>
    /// Spec 002 §9's moisture hook: the mean moisture of an unwatered acre by season, per soil (loam is the target:
    /// Spring 30–50, Summer 15–35, Monsoon 65–85, Winter 40–60). Samples every hour on unowned, rain-fed tiles.
    /// </summary>
    public static class Moisture
    {
        public static readonly (string Season, double Min, double Max)[] Targets =
            { ("Spring", 30, 50), ("Summer", 15, 35), ("Monsoon", 65, 85), ("Winter", 40, 60) };

        /// <summary>Mean moisture [soil, season] over the years (the first year is warm-up and not counted).</summary>
        public static double[,] Measure(Simulation sim, int years)
        {
            GameDefs defs = sim.Config.Game;
            var sum = new double[defs.Soils.Count, 4];
            var n = new long[defs.Soils.Count, 4];
            long start = sim.Clock.SecondsPerYear, end = start + years * sim.Clock.SecondsPerYear;
            for (long t = 3600; t <= end; t += 3600)
            {
                sim.AdvanceTo(t);
                if (t <= start) continue;
                int season = (int)Calendar.SeasonAt(t - 1, sim.Clock);
                foreach (Tile tile in sim.State.Land.Tiles)
                {
                    if (tile.Terrain != Terrain.Farm || sim.State.Land.Parcels[tile.ParcelId].Owned) continue;
                    sum[tile.Soil, season] += tile.Moisture.Raw / 4294967296.0;
                    n[tile.Soil, season]++;
                }
            }
            for (int s = 0; s < defs.Soils.Count; s++)
                for (int q = 0; q < 4; q++)
                    sum[s, q] = n[s, q] == 0 ? double.NaN : sum[s, q] / n[s, q];
            return sum;
        }

        public static void Print(Simulation sim, int years)
        {
            double[,] m = Measure(sim, years);
            Console.WriteLine("Mean moisture of unwatered acres by season (" + years + " years after a warm-up year; loam target in brackets):");
            Console.Write("  " + "Soil".PadRight(8));
            foreach (var t in Targets) Console.Write((t.Season + " (" + t.Min + "–" + t.Max + ")").PadLeft(20));
            Console.WriteLine();
            for (int s = 0; s < sim.Config.Game.Soils.Count; s++)
            {
                Console.Write("  " + sim.Config.Game.Soils[s].Id.PadRight(8));
                for (int q = 0; q < 4; q++) Console.Write(m[s, q].ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).PadLeft(20));
                Console.WriteLine();
            }
        }
    }
}
