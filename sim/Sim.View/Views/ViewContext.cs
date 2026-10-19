using System;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Maths;
using Sim.View.Content;
using Sim.View.Text;

namespace Sim.View.Views
{
    /// <summary>
    /// What every view builder needs: the simulation (read only), the string table, the formatter and the look data. A view model
    /// is built from this and is a plain object: it keeps no reference into the game state (spec 008 §3.1).
    /// </summary>
    public sealed class ViewContext
    {
        public Simulation Sim { get; }
        public StringTable Strings { get; }
        public Formatter Fmt { get; }
        public UiDef Ui { get; }

        public ViewContext(Simulation sim, StringTable strings, Formatter fmt, UiDef ui)
        {
            Sim = sim;
            Strings = strings;
            Fmt = fmt;
            Ui = ui;
        }

        public long Now { get { return Sim.State.Clock.SimTime; } }

        /// <summary>The text of a rejection as a sentence (spec 008 AC-13).</summary>
        public string Reject(RejectReason r, params string[] args) { return Strings.Get("reject." + r, args); }

        /// <summary>A yield factor (Care, Water, Weather) as a word a player reads at a glance: good, fair or poor (thresholds in ui.json).</summary>
        public string Rating(Fixed factor)
        {
            if (factor >= Fixed.FromRatio(Ui.RatingGoodPercent, 100)) return Strings.Get("rating.good");
            return Strings.Get(factor >= Fixed.FromRatio(Ui.RatingFairPercent, 100) ? "rating.fair" : "rating.poor");
        }
    }

    public enum ActionKind
    {
        Primary = 0,
        Secondary = 1,
        Danger = 2,

        /// <summary>Opens another sheet (see <see cref="ActionVm.Opens"/>); sends no command.</summary>
        Open = 3,
    }

    /// <summary>A sheet to open: Sow (tile), Store, Garden (bed), Tree (slot), Land (tile).</summary>
    public readonly struct SheetRequest
    {
        public string Kind { get; }
        public long Id { get; }

        public SheetRequest(string kind, long id)
        {
            Kind = kind;
            Id = id;
        }
    }

    /// <summary>
    /// A button on a sheet: its label, whether it is enabled and, if not, why (a sentence), and the command it sends. The command
    /// is made when pressed, stamped with the sim time then (<see cref="Create"/>): there are no view-only shortcuts.
    /// </summary>
    public sealed class ActionVm
    {
        public string Id { get; }
        public string Label { get; }

        /// <summary>A second line, for example the cost: "Seed 1,400 coins".</summary>
        public string Detail { get; }
        public ActionKind Kind { get; }
        public bool Enabled { get; }
        public string DisabledReason { get; }
        public SheetRequest? Opens { get; }
        private readonly Func<long, Command> _make;

        internal ActionVm(string id, string label, string detail, ActionKind kind, bool enabled, string disabledReason, Func<long, Command> make, SheetRequest? opens = null)
        {
            Id = id;
            Label = label;
            Detail = detail;
            Kind = kind;
            Enabled = enabled;
            DisabledReason = disabledReason;
            _make = make;
            Opens = opens;
        }

        /// <summary>The command to execute now (null for an action that only opens a sheet).</summary>
        public Command Create(long at) { return _make == null ? null : _make(at); }

        /// <summary>
        /// Builds an action by asking the simulation what the command would say right now (a preview on a copy of the state), so a
        /// greyed button always shows the sim's own reason (spec 008 §3.6).
        /// </summary>
        internal static ActionVm Checked(ViewContext ctx, string id, string label, string detail, ActionKind kind, Func<long, Command> make, params string[] reasonArgs)
        {
            CommandResult r = ctx.Sim.Preview(make(ctx.Now));
            return new ActionVm(id, label, detail, kind, r.Accepted, r.Accepted ? null : ctx.Reject(r.Reason, reasonArgs), make);
        }

        internal static ActionVm Open(string id, string label, SheetRequest opens, string detail = null)
        {
            return new ActionVm(id, label, detail, ActionKind.Open, true, null, null, opens);
        }
    }

    /// <summary>One label and value row on a sheet.</summary>
    public sealed class LineVm
    {
        public string Label { get; }
        public string Value { get; }

        /// <summary>A string key for a Why? sheet behind the value, or null.</summary>
        public string WhyKey { get; }

        /// <summary>A button on the row itself (the store's Sell per good), or null.</summary>
        public ActionVm Action { get; internal set; }

        public LineVm(string label, string value, string whyKey = null)
        {
            Label = label;
            Value = value;
            WhyKey = whyKey;
        }
    }

    /// <summary>A bottom sheet: title, subtitle, rows and buttons.</summary>
    public sealed class SheetVm
    {
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }

        /// <summary>The picture beside the title: a sprite id the presentation knows (a crop, a tree, "hut", "land").</summary>
        public string Icon { get; internal set; }
        public System.Collections.Generic.List<LineVm> Lines { get; } = new System.Collections.Generic.List<LineVm>();
        public System.Collections.Generic.List<ActionVm> Actions { get; } = new System.Collections.Generic.List<ActionVm>();
    }
}
