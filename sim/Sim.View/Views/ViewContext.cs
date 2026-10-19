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

    /// <summary>
    /// A sheet to open: Sow (tile), Store, Garden (bed), Tree (slot), Land (tile), People, Hand (hand id), Order (tile, or a tree
    /// slot with Arg "tree"; Arg carries the step chosen so far), HutPlacement.
    /// </summary>
    public readonly struct SheetRequest
    {
        public string Kind { get; }
        public long Id { get; }
        public string Arg { get; }

        public SheetRequest(string kind, long id, string arg = null)
        {
            Kind = kind;
            Id = id;
            Arg = arg;
        }
    }

    /// <summary>
    /// A question before an action (ADR 0039 §6): "Change Ramesh's order to potato?". Yes sends <see cref="Yes"/>'s commands;
    /// No sends the action's own command.
    /// </summary>
    public sealed class ConfirmVm
    {
        public string Question { get; }
        public string YesLabel { get; }
        public string NoLabel { get; }

        /// <summary>True: No still sends the action's own command ("Just this once"). False: No only cancels ("Keep them").</summary>
        public bool NoSends { get; }

        /// <summary>Yes is the destructive choice (red), as in "Let go".</summary>
        public bool YesIsDanger { get; internal set; }
        private readonly Func<long, Command[]> _yes;

        internal ConfirmVm(string question, string yesLabel, string noLabel, Func<long, Command[]> yes, bool noSends = true)
        {
            Question = question;
            YesLabel = yesLabel;
            NoLabel = noLabel;
            _yes = yes;
            NoSends = noSends;
        }

        public Command[] Yes(long at) { return _yes(at); }
    }

    /// <summary>Skill as stars (ADR 0039 §7): solid up to the sure part, faded up to the possible part, out of 5.</summary>
    public readonly struct StarsVm
    {
        public int Solid { get; }
        public int Faded { get; }

        public StarsVm(int solid, int faded)
        {
            Solid = solid;
            Faded = faded;
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

        /// <summary>Asked before the command is sent, or null.</summary>
        public ConfirmVm Confirm { get; internal set; }
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

        /// <summary>Skill stars shown before the value, or null.</summary>
        public StarsVm? Stars { get; internal set; }

        /// <summary>A section heading ("Day labourers"): drawn as a title row, no value or button.</summary>
        public bool IsHeader { get; internal set; }

        internal static LineVm Header(string text) { return new LineVm(text, null) { IsHeader = true }; }

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
