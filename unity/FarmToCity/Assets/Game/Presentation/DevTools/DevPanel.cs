#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Text;
using Game.Presentation.Ui;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Time;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation.DevTools
{
    /// <summary>
    /// The developer panel (spec 008 §3.14, ADR 0017, 0038): hold the time strip for three seconds. Time travel through
    /// <c>DebugAdvance</c> (+1 hour, +1 day, +1 season, the away cap), an in-memory snapshot and restore, and the golden-replay
    /// check of ADR 0035. Number format and reduced motion are in Settings, loans in Money and loans (ADR 0043). Compiled out of
    /// release builds: it exists only in development builds and the Editor.
    /// </summary>
    public sealed class DevPanel
    {
        /// <summary>What the panel needs from the game.</summary>
        public sealed class Host
        {
            public Func<Simulation> Sim;
            public Action<long, AdvanceMode> Advance;
            public Action Snapshot, Restore;
            public Func<string> Status;
            public Action<bool> Visibility;
        }

        private readonly VisualElement _root;
        private readonly Host _host;
        private readonly Label _status, _result;
        private bool _open;

        public DevPanel(VisualElement parent, Host host)
        {
            _host = host;
            _root = new VisualElement();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = new Color(0.1f, 0.07f, 0.04f, 0.78f);
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.display = DisplayStyle.None;
            parent.Add(_root);

            VisualElement panel = UiKit.Panel(30);
            panel.style.width = Length.Percent(80);
            panel.style.maxWidth = 1500;
            panel.style.maxHeight = Length.Percent(90);
            UiKit.Padding(panel, 20, 30);
            _root.Add(panel);

            panel.Add(UiKit.Heading1("Developer panel", 56));
            _status = UiKit.Text("", 30, UiKit.Wood, UiKit.Body);
            panel.Add(_status);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            panel.Add(scroll);
            scroll.Add(Section("Time travel (DebugAdvance)"));
            scroll.Add(ButtonRow(
                Btn("+1 hour", () => host.Advance(3600, AdvanceMode.Live)),
                Btn("+1 day", () => host.Advance(86400, AdvanceMode.Live)),
                Btn("+1 season", () => host.Advance(host.Sim().Clock.SecondsPerSeason, AdvanceMode.Live))));
            scroll.Add(ButtonRow(
                Btn("Away +1 day", () => host.Advance(86400, AdvanceMode.Away)),
                Btn("Away cap (72 h)", () => host.Advance(host.Sim().Clock.AwayCapSeconds, AdvanceMode.Away))));
            scroll.Add(Section("Snapshot (in memory)"));
            scroll.Add(ButtonRow(Btn("Snapshot", () => host.Snapshot()), Btn("Restore", () => host.Restore())));
            scroll.Add(Section("Determinism (ADR 0035)"));
            scroll.Add(ButtonRow(Btn("Golden replays", RunGoldens)));
            _result = UiKit.Text("", 28, UiKit.Ink, UiKit.Body);
            scroll.Add(_result);

            panel.Add(ButtonRow(Btn("Close", Close, ActionLook.Secondary)));
        }

        private static Label Section(string text)
        {
            Label l = UiKit.Heading1(text, 38, UiKit.Wood);
            l.style.marginTop = 14;
            return l;
        }

        private Button Btn(string text, Action a, ActionLook look = ActionLook.Primary)
        {
            Button b = UiKit.MakeButton(text, look, () => { a(); Refresh(); });
            b.style.minHeight = 74;
            b.style.minWidth = 220;
            return b;
        }

        private static VisualElement ButtonRow(params Button[] buttons)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            foreach (Button b in buttons) row.Add(b);
            return row;
        }

        public bool IsOpen { get { return _open; } }

        public void Open()
        {
            _open = true;
            _root.style.display = DisplayStyle.Flex;
            if (_host.Visibility != null) _host.Visibility(false);
            Refresh();
        }

        public void Close()
        {
            _open = false;
            _root.style.display = DisplayStyle.None;
            if (_host.Visibility != null) _host.Visibility(true);
        }

        private void Refresh()
        {
            Simulation sim = _host.Sim();
            CalendarDate d = Calendar.From(sim.State.Clock.SimTime, sim.Clock);
            _status.text = "seed " + sim.State.Seed + " · sim time " + sim.State.Clock.SimTime + " s · " + d + " · isAway " + sim.State.Clock.IsAway
                + (_host.Status != null ? "\n" + _host.Status() : "");
        }

        private void RunGoldens()
        {
            var sb = new StringBuilder();
            bool all = true;
            try
            {
                foreach (GoldenCheck.Row r in GoldenCheck.RunAll())
                {
                    all &= r.Pass;
                    sb.Append(r.Name).Append(": ").Append(r.Pass ? "PASS" : "FAIL").Append("  ").Append(r.Actual)
                        .Append(r.Pass ? "" : " (expected " + r.Expected + ")").Append("  ").Append(r.Milliseconds.ToString("0")).Append(" ms\n");
                }
                sb.Append(all ? "ALL PASS" : "SOME FAILED").Append("\n72 h away advance: ").Append(GoldenCheck.Advance72hMilliseconds().ToString("0.0")).Append(" ms (budget 2,000)");
            }
            catch (Exception e) { sb.Append("The golden files are missing from this build: ").Append(e.Message); }
            _result.text = sb.ToString();
            Debug.Log(_result.text);
        }
    }
}
#endif
