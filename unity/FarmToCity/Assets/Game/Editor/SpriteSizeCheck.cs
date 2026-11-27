using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Spec 032 AC-1 (as amended by ADR 0182): every rendered sprite other than a shadow keeps its world size and its anchor. <c>docs/art/sprite-world-sizes.json</c>
    /// records each sprite's frame (width, height) and its pivot's distance from the frame's left and bottom edges in world units, as of Sprint 29 (528 px a unit).
    /// The anchor is judged by where it stands against the frame's middle (across) and its bottom edge (up): within <see cref="AnchorUnits"/> world units. The frame is
    /// judged within <see cref="FrameTolerance"/> (<see cref="PeopleFrameTolerance"/> for people: new, slimmer bodies). <see cref="Report"/>
    /// reads <c>sprites.json</c> (each sprite's own <c>ppu</c>, else the manifest's) and lists the sprites that moved more: it must be empty. Run from the Editor with <c>unity command eval "return Game.Editor.SpriteSizeCheck.Report();"</c>.
    /// </summary>
    public static class SpriteSizeCheck
    {
        /// <summary>How far an anchor may move, in world units (about 5 px at 1x).</summary>
        public const double AnchorUnits = 0.01;

        /// <summary>The share a frame's width or height may differ, and the share for people.</summary>
        public const double FrameTolerance = 0.05, PeopleFrameTolerance = 0.15;

        private static string Root { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..")); } }

        [Serializable] private class Row { public string name; public double w, h, px, py; }
        [Serializable] private class Table { public Row[] sprites; }
        [Serializable] private class Info { public string name; public int w, h; public double pivotX, pivotY, ppu; }
        [Serializable] private class Manifest { public double pixelsPerUnit; public Info[] sprites; }

        /// <summary>Each sprite that moved: its name and what changed. Empty when every sprite is within the tolerance.</summary>
        public static List<string> Check()
        {
            var problems = new List<string>();
            Table table = JsonUtility.FromJson<Table>(File.ReadAllText(Path.Combine(Root, "docs", "art", "sprite-world-sizes.json")));
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Path.Combine(Root, "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender", "sprites.json")));
            var recorded = new Dictionary<string, Row>();
            foreach (Row r in table.sprites) recorded[r.name] = r;
            var seen = new HashSet<string>();
            foreach (Info s in manifest.sprites)
            {
                if (s.name.EndsWith("_shadow", StringComparison.Ordinal)) continue; // a shadow has its own frame (spec 032 §3.3)
                seen.Add(s.name);
                if (!recorded.TryGetValue(s.name, out Row rec)) { problems.Add(s.name + ": not in the recorded table"); continue; }
                double ppu = s.ppu > 0 ? s.ppu : manifest.pixelsPerUnit;
                double w = s.w / ppu, h = s.h / ppu, px = s.pivotX * w, py = s.pivotY * h;
                double tol = s.name.StartsWith("person_", StringComparison.Ordinal) ? PeopleFrameTolerance : FrameTolerance;
                string d = Differ("width", w, rec.w, tol * rec.w) + Differ("height", h, rec.h, tol * rec.h)
                    + Differ("anchor across", px - w / 2, rec.px - rec.w / 2, AnchorUnits) + Differ("anchor up", py, rec.py, AnchorUnits);
                if (d.Length > 0) problems.Add(s.name + ":" + d);
            }
            foreach (Row r in table.sprites) if (!seen.Contains(r.name)) problems.Add(r.name + ": in the table but not rendered");
            return problems;
        }

        private static string Differ(string what, double now, double was, double allowed)
        {
            return Math.Abs(now - was) > allowed ? " " + what + " " + was.ToString("0.000") + " → " + now.ToString("0.000") : "";
        }

        public static string Report()
        {
            List<string> p = Check();
            var sb = new StringBuilder();
            sb.Append(p.Count == 0 ? "SpriteSizeCheck: 0 problems" : "SpriteSizeCheck: " + p.Count + " problems");
            foreach (string s in p) sb.Append('\n').Append(s);
            return sb.ToString();
        }
    }
}
