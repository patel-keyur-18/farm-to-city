using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Game.Presentation;
using Game.Presentation.Ui;
using Sim.View.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Editor
{
    /// <summary>
    /// Opens every sheet the open town can reach and lists anything that spills out of its sheet or text wider than its box (spec 030 §3.2, US-265).
    /// Start in play mode: <c>SheetLayoutCheck.Start()</c>, wait, then read <c>SheetLayoutCheck.Report()</c>. It starts from the Menu and the HUD's
    /// sheets and follows every "open another sheet" action (a few of each kind), so it shows what the owner's screenshots showed: tabs past the edge, wrapped badges.
    /// It only opens sheets (nothing is bought, hired or sold); placing modes and the Codex's popup card are skipped.
    /// </summary>
    public static class SheetLayoutCheck
    {
        private const int MaxSheets = 220, PerKind = 3;
        private const double Settle = 0.55; // seconds: the sheet slides in, then its tab row scrolls to the showing tab

        private static readonly Queue<SheetRequest> Queue = new Queue<SheetRequest>();
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static readonly Dictionary<string, int> Kinds = new Dictionary<string, int>();
        private static readonly List<string> Problems = new List<string>();
        private static readonly HashSet<string> ProblemKeys = new HashSet<string>();
        private static bool _running;
        private static int _opened;
        private static double _at;
        private static string _current;
        private static GameRoot _root;
        private static SheetPresenter _sheet;
        private static MethodInfo _open;

        public static string Start()
        {
            _root = UnityEngine.Object.FindFirstObjectByType<GameRoot>();
            if (_root == null) return "no GameRoot: enter play mode";
            const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Instance;
            _sheet = (SheetPresenter)typeof(GameRoot).GetField("_sheet", bf).GetValue(_root);
            _open = typeof(GameRoot).GetMethod("OpenSheet", bf);
            Queue.Clear(); Seen.Clear(); Kinds.Clear(); Problems.Clear(); ProblemKeys.Clear();
            _opened = 0; _current = null;
            foreach (string kind in new[] { "Menu", "Store", "People", "Orders", "Money", "Businesses", "Settings", "Equipment", "Land", "Hire" }) Enqueue(new SheetRequest(kind, 0));
            _running = true;
            _at = EditorApplication.timeSinceStartup + 0.5;
            EditorApplication.update += Step;
            return "started";
        }

        public static string Report()
        {
            var sb = new StringBuilder();
            sb.Append(_running ? "RUNNING" : "DONE").Append(": ").Append(_opened).Append(" sheets opened, ").Append(Problems.Count).Append(" problems\n");
            foreach (string p in Problems) sb.Append(p).Append('\n');
            return sb.ToString();
        }

        private static void Enqueue(SheetRequest r)
        {
            if (r.Kind.Contains("Placement") || r.Kind == CodexSheet.CardKind) return; // they put the map in placing mode, or open a popup card over the sheet
            string key = r.Kind + "|" + r.Id + "|" + r.Arg;
            if (!Seen.Add(key)) return;
            Kinds.TryGetValue(r.Kind, out int n);
            if (n >= PerKind) return;
            Kinds[r.Kind] = n + 1;
            Queue.Enqueue(r);
        }

        private static void Step()
        {
            if (EditorApplication.timeSinceStartup < _at) return;
            if (!Application.isPlaying || _root == null) { _running = false; EditorApplication.update -= Step; return; }
            try
            {
                if (_current != null && _sheet.Root != null)
                {
                    Check(_current, _sheet.Root);
                    Follow(_sheet.Last);
                }
                _current = null;
                if (Queue.Count == 0 || _opened >= MaxSheets) { _running = false; EditorApplication.update -= Step; return; }
                SheetRequest next = Queue.Dequeue();
                _current = next.Kind + (next.Id != 0 ? ":" + next.Id : "") + (next.Arg != null ? ":" + next.Arg : "");
                _opened++;
                _open.Invoke(_root, new object[] { next });
            }
            catch (Exception e)
            {
                Add("throws", (_current ?? "?") + ": " + (e.InnerException ?? e).Message);
                _current = null;
            }
            _at = EditorApplication.timeSinceStartup + Settle;
        }

        private static void Follow(SheetVm vm)
        {
            if (vm == null) return;
            void One(ActionVm a) { if (a != null && a.Opens.HasValue) Enqueue(a.Opens.Value); }
            void Many(List<ActionVm> l) { if (l != null) foreach (ActionVm a in l) One(a); }
            Many(vm.Tabs); Many(vm.Switch); Many(vm.Actions);
            foreach (LineVm l in vm.Lines)
            {
                One(l.Action); Many(l.Buttons); Many(l.Tabs);
                if (l.Opens.HasValue) Enqueue(l.Opens.Value);
            }
        }

        private static void Check(string name, VisualElement sheet)
        {
            Rect s = sheet.worldBound;
            sheet.Query<VisualElement>().ForEach(e =>
            {
                if (e == sheet || Skipped(e, sheet)) return;
                Rect b = e.worldBound;
                if (b.width <= 0 || b.height <= 0 || float.IsNaN(b.x) || float.IsNaN(b.width)) return;
                if (b.xMax > s.xMax + 3 || b.xMin < s.xMin - 3)
                    Add("spills sideways", name + ": " + Describe(e) + " by " + Mathf.Max(b.xMax - s.xMax, s.xMin - b.xMin).ToString("0") + " px");
                if (e is Label l && !string.IsNullOrEmpty(l.text) && l.resolvedStyle.display != DisplayStyle.None)
                {
                    Rect c = l.contentRect;
                    if (c.width <= 0) return;
                    // The text on one line first: when it fits, nothing is cut (measuring at the box's exact width wraps on a rounding hair).
                    Vector2 one = l.MeasureTextSize(l.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                    if (one.x <= c.width + 2f) return;
                    if (l.resolvedStyle.whiteSpace == WhiteSpace.NoWrap) { Add("text wider than its box", name + ": '" + Trim(l.text) + "' (" + one.x.ToString("0") + " > " + c.width.ToString("0") + ")"); return; }
                    Vector2 wrapped = l.MeasureTextSize(l.text, c.width, VisualElement.MeasureMode.AtMost, 0, VisualElement.MeasureMode.Undefined);
                    // the measure can differ from the layout by about half a line (Mandi's right-aligned hint measured 3 lines and shows 2): a cut line is a whole extra line
                    if (wrapped.y > c.height * 1.6f) Add("text taller than its box", name + ": '" + Trim(l.text) + "' (" + wrapped.y.ToString("0") + " > " + c.height.ToString("0") + ")");
                }
            });
        }

        /// <summary>Hidden elements, and anything inside a sideways-scrolling row (its tabs are clipped on purpose).</summary>
        private static bool Skipped(VisualElement e, VisualElement sheet)
        {
            for (VisualElement p = e; p != null && p != sheet; p = p.parent)
            {
                if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden) return true;
                if (p is ScrollView sv && sv.mode == ScrollViewMode.Horizontal && p != e) return true;
            }
            return false;
        }

        private static string Trim(string t) { t = t.Replace('\n', ' '); return t.Length > 40 ? t.Substring(0, 40) + "…" : t; }

        private static string Describe(VisualElement e)
        {
            string text = e is Label l ? " '" + Trim(l.text) + "'" : e is Button b ? " button '" + Trim(b.text) + "'" : "";
            string name = string.IsNullOrEmpty(e.name) ? "" : "#" + e.name;
            return e.GetType().Name + name + text;
        }

        private static void Add(string kind, string detail)
        {
            if (ProblemKeys.Add(kind + detail)) Problems.Add("- " + kind + ": " + detail);
        }
    }
}
