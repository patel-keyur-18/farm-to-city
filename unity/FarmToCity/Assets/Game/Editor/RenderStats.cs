using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Game.Presentation;
using Game.Presentation.Map;
using Sim.View.Views;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// The Editor's rendering statistics (US-101, ADR 0107 §12). Every visible story frames the most crowded view and reads the draw calls, batches and
    /// sprites; a reading that grows more than 10% over the last one for the same view is flagged. Driven from the terminal with
    /// <c>unity command eval</c> while playing (Editor only, never in a build):
    /// <code>
    /// Game.Editor.RenderStats.Frame("all");              // zoom out to the whole map (also "village", "farm")
    /// // wait a second so a frame is drawn at that view, then:
    /// Game.Editor.RenderStats.Read();                    // the numbers now, as one line
    /// Game.Editor.RenderStats.Record("US-101 first reading", "all"); // read, compare with the last "all" reading, append to the log
    /// </code>
    /// The log is <c>docs/planning/render-stats.json</c> (kept in git). The numbers are the Editor's (a Mac Game view at about 1280 × 720), not the
    /// phone's: they show growth between stories, and the phone's frame time comes from the Development build (US-101.T2).
    /// </summary>
    public static class RenderStats
    {
        /// <summary>Growth that is flagged, in percent (ADR 0107 §12).</summary>
        public const double FlagPercent = 10.0;

        private static readonly string[] Metrics = { "drawCalls", "batches", "setPassCalls", "visibleSprites", "triangles" };

        private static string LogPath { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "docs", "planning", "render-stats.json")); } }

        private static GameRoot Root()
        {
            GameRoot g = UnityEngine.Object.FindFirstObjectByType<GameRoot>();
            if (g == null) throw new InvalidOperationException("Not playing: no GameRoot");
            return g;
        }

        /// <summary>Frames a view: "all" (the whole map, the most crowded), "village" (the centre east of the farm at the zoom the phone opens the village at) or "farm" (the opening view).</summary>
        public static string Frame(string view)
        {
            GameRoot g = Root();
            MapInput input = (MapInput)typeof(GameRoot).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);
            MapCamera cam = input.View;
            switch (view)
            {
                case "all":
                    input.SetView(cam.CentreX, cam.CentreY, cam.MaxHalfHeight);
                    break;
                case "farm":
                {
                    MapVm home = (MapVm)typeof(GameRoot).GetField("_mapVm", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);
                    input.Focus(home.HomeX, home.HomeY); // the yard, at the zoom the game opens at
                    break;
                }
                case "village":
                {
                    // Centred on the square (the middle of the village), at the zoom that fits the whole village.
                    MapPresenter map = (MapPresenter)typeof(GameRoot).GetField("_map", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);
                    MapVm vm = (MapVm)typeof(GameRoot).GetField("_mapVm", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(g);
                    (float x, float y) = vm.SquareTile >= 0 ? map.Grid.Centre(vm.SquareTile % map.Grid.Width, vm.SquareTile / map.Grid.Width) : (cam.CentreX, cam.CentreY);
                    input.SetView(x, y, Mathf.Min(cam.MaxHalfHeight, 2.2f));
                    break;
                }
                default:
                    return "unknown view '" + view + "' (all, village, farm)";
            }
            return "framed " + view + " at half height " + cam.HalfHeight.ToString("0.00", CultureInfo.InvariantCulture) + " (centre " + cam.CentreX.ToString("0.0", CultureInfo.InvariantCulture) + ", " + cam.CentreY.ToString("0.0", CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>One reading of the last frame drawn: the Editor's draw calls, batches and set-pass calls, the triangles, the sprites on screen and in the scene.</summary>
        public static Dictionary<string, double> Measure()
        {
            int visible = 0, total = 0;
            foreach (SpriteRenderer s in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
            {
                if (!s.enabled || !s.gameObject.activeInHierarchy) continue;
                total++;
                if (s.isVisible) visible++;
            }
            return new Dictionary<string, double>
            {
                { "drawCalls", UnityStats.drawCalls },
                { "batches", UnityStats.batches },
                { "setPassCalls", UnityStats.setPassCalls },
                { "triangles", UnityStats.triangles },
                { "visibleSprites", visible },
                { "sprites", total },
            };
        }

        public static string Read()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<string, double> kv in Measure()) sb.Append(kv.Key).Append('=').Append(kv.Value.ToString("0", CultureInfo.InvariantCulture)).Append(' ');
            return sb.ToString().TrimEnd();
        }

        /// <summary>Reads, compares with the last reading of the same view, and appends to the log. Returns the numbers and every metric that grew over 10%.</summary>
        public static string Record(string label, string view)
        {
            Dictionary<string, double> now = Measure();
            string path = LogPath;
            List<string> lines = File.Exists(path) ? new List<string>(File.ReadAllLines(path)) : new List<string>();
            // One reading a line: {"label":"…","view":"…","date":"…","drawCalls":n,…}; the file is a JSON array written by hand so a diff stays readable.
            string last = null;
            for (int i = lines.Count - 1; i >= 0; i--) if (lines[i].Contains("\"view\":\"" + view + "\"")) { last = lines[i]; break; }
            var flags = new StringBuilder();
            if (last != null)
                foreach (string m in Metrics)
                {
                    double before = Field(last, m);
                    if (before > 0 && now[m] > before * (1 + FlagPercent / 100.0))
                        flags.Append(m).Append(' ').Append(before.ToString("0", CultureInfo.InvariantCulture)).Append(" → ").Append(now[m].ToString("0", CultureInfo.InvariantCulture))
                            .Append(" (+").Append(((now[m] / before - 1) * 100).ToString("0", CultureInfo.InvariantCulture)).Append("%); ");
                }
            var entry = new StringBuilder("  {\"label\":\"").Append(label.Replace("\"", "'")).Append("\",\"view\":\"").Append(view).Append("\",\"date\":\"").Append(DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('"');
            foreach (KeyValuePair<string, double> kv in now) entry.Append(",\"").Append(kv.Key).Append("\":").Append(kv.Value.ToString("0", CultureInfo.InvariantCulture));
            entry.Append('}');
            lines.RemoveAll(l => l.Trim() == "[" || l.Trim() == "]" || l.Trim().Length == 0);
            for (int i = 0; i < lines.Count; i++) lines[i] = lines[i].TrimEnd(',');
            lines.Add(entry.ToString());
            var o = new StringBuilder("[\n");
            for (int i = 0; i < lines.Count; i++) o.Append(lines[i]).Append(i < lines.Count - 1 ? ",\n" : "\n");
            o.Append("]\n");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, o.ToString());
            return Read() + (last == null ? " | first reading of this view" : flags.Length == 0 ? " | no metric grew over 10% against the last reading" : " | FLAG: " + flags.ToString().TrimEnd());
        }

        private static double Field(string line, string name)
        {
            string key = "\"" + name + "\":";
            int at = line.IndexOf(key, StringComparison.Ordinal);
            if (at < 0) return 0;
            int from = at + key.Length, to = from;
            while (to < line.Length && (char.IsDigit(line[to]) || line[to] == '.')) to++;
            return double.TryParse(line.Substring(from, to - from), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0;
        }
    }
}
