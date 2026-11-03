using System;
using System.Collections.Generic;
using System.Linq;

using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Time;

namespace Tools.Balancer
{
    /// <summary>
    /// Spec 002 §9's moisture hook: the mean moisture of an unwatered acre by season, per soil (loam is the target:
    /// Spring 30–50, Summer 15–35, Monsoon 65–85, Winter 40–60). Samples every hour on unowned, rain-fed tiles. Also the
    /// dry-season hook (ADR 0085): a crop's water factor averaged over each season, its mean and its worst season in 10
    /// (okra in Spring, Summer and the Monsoon; wheat in Winter).
    /// </summary>
    public static class Moisture
    {
        public static readonly (string Season, double Min, double Max)[] Targets =
            { ("Spring", 30, 50), ("Summer", 15, 35), ("Monsoon", 65, 85), ("Winter", 40, 60) };

        /// <summary>Mean moisture [soil, season] over the years (the first year is warm-up and not counted).</summary>
        public static double[,] Measure(Simulation sim, int years)
        {
            return Measure(sim, years, out _);
        }

        /// <summary>
        /// As <see cref="Measure(Simulation, int)"/>, and each season's mean water factor per soil, in order: water[soil, season]
        /// lists one value per season played (the crop of <see cref="WaterCrop"/>).
        /// </summary>
        public static double[,] Measure(Simulation sim, int years, out List<double>[,] water)
        {
            GameDefs defs = sim.Config.Game;
            var sum = new double[defs.Soils.Count, 4];
            var n = new long[defs.Soils.Count, 4];
            water = new List<double>[defs.Soils.Count, 4];
            for (int s = 0; s < defs.Soils.Count; s++)
                for (int q = 0; q < 4; q++) water[s, q] = new List<double>();
            CropDef[] crops = Enumerable.Range(0, 4).Select(q => WaterCrop(defs, q)).ToArray();
            var wSum = new double[defs.Soils.Count];
            var wN = new long[defs.Soils.Count];
            int current = -1;
            long start = sim.Clock.SecondsPerYear, end = start + years * sim.Clock.SecondsPerYear;
            for (long t = 3600; t <= end; t += 3600)
            {
                sim.AdvanceTo(t);
                if (t <= start) continue;
                int season = (int)Calendar.SeasonAt(t - 1, sim.Clock);
                if (season != current && current >= 0) Flush(water, current, wSum, wN);
                current = season;
                foreach (Tile tile in sim.State.Land.Tiles)
                {
                    if (tile.Terrain != Terrain.Farm || sim.State.Land.Parcels[tile.ParcelId].Owned) continue;
                    sum[tile.Soil, season] += tile.Moisture.Raw / 4294967296.0;
                    n[tile.Soil, season]++;
                    wSum[tile.Soil] += FarmRules.WaterFactor(defs.Farm.Farm, crops[season], tile.Moisture).Raw / 4294967296.0;
                    wN[tile.Soil]++;
                }
            }
            if (current >= 0) Flush(water, current, wSum, wN);
            for (int s = 0; s < defs.Soils.Count; s++)
                for (int q = 0; q < 4; q++)
                    sum[s, q] = n[s, q] == 0 ? double.NaN : sum[s, q] / n[s, q];
            return sum;
        }

        /// <summary>The crop whose water factor the dry-season hook reports: okra, or wheat in Winter.</summary>
        public static CropDef WaterCrop(GameDefs defs, int season)
        {
            string id = season == (int)Season.Winter ? "wheat" : "okra";
            return defs.Farm.Crops.First(c => c.Id == id);
        }

        private static void Flush(List<double>[,] water, int season, double[] wSum, long[] wN)
        {
            for (int s = 0; s < wSum.Length; s++)
            {
                if (wN[s] > 0) water[s, season].Add(wSum[s] / wN[s]);
                wSum[s] = 0;
                wN[s] = 0;
            }
        }

        /// <summary>The worst season in 10 (the 10th percentile, rounded down).</summary>
        public static double WorstInTen(List<double> seasons)
        {
            return seasons.Count == 0 ? double.NaN : seasons.OrderBy(x => x).ElementAt(seasons.Count / 10);
        }

        public static void Print(Simulation sim, int years)
        {
            double[,] m = Measure(sim, years, out List<double>[,] water);
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
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Console.WriteLine("Water factor of a season's crop on unwatered acres: mean / worst season in 10 (okra; wheat in Winter; ADR 0085):");
            Console.Write("  " + "Soil".PadRight(8));
            foreach (var t in Targets) Console.Write(t.Season.PadLeft(20));
            Console.WriteLine();
            for (int s = 0; s < sim.Config.Game.Soils.Count; s++)
            {
                Console.Write("  " + sim.Config.Game.Soils[s].Id.PadRight(8));
                for (int q = 0; q < 4; q++)
                {
                    List<double> w = water[s, q];
                    string cell = w.Count == 0 ? "–" : w.Average().ToString("0.00", inv) + " / " + WorstInTen(w).ToString("0.00", inv);
                    Console.Write(cell.PadLeft(20));
                }
                Console.WriteLine();
            }
        }
    }
}
