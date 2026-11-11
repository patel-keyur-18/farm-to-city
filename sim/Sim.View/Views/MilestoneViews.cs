using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Finance;

namespace Sim.View.Views
{
    /// <summary>
    /// <em>Milestones</em> on Your wealth (spec 019 §3.6, ADR 0101 §7): all five, the ones reached first with their season, the rest dimmed with
    /// today's number beside the target. Never a reward, so nothing here pushes.
    /// </summary>
    public static class MilestonesSection
    {
        /// <summary>The milestone rows' <em>Why?</em> ids: this plus the milestone's place in <see cref="MilestoneRules.Ids"/>.</summary>
        public const int WhyBase = 10;

        public static void Add(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!MilestoneRules.Enabled(sim)) return;
            var s = ctx.Strings;
            MilestonesState ms = MilestoneRules.State(sim);
            vm.Lines.Add(LineVm.Header(s.Get("milestone.title", "n", ctx.Fmt.Integer(ms.Reached.Count), "of", ctx.Fmt.Integer(MilestoneRules.Ids.Length))));
            var order = new List<int>();
            foreach (MilestoneEntry m in ms.Reached) order.Add(System.Array.IndexOf(MilestoneRules.Ids, m.Id)); // reached first, in the order they came
            for (int i = 0; i < MilestoneRules.Ids.Length; i++) if (!order.Contains(i)) order.Add(i);
            foreach (int i in order) vm.Lines.Add(Row(ctx, i));
            vm.Lines.Add(new LineVm(s.Get("milestone.note"), null));
        }

        private static LineVm Row(ViewContext ctx, int index)
        {
            var s = ctx.Strings;
            string id = MilestoneRules.Ids[index];
            MilestoneEntry got = MilestoneRules.State(ctx.Sim).Find(id);
            var row = new LineVm(s.Get("milestone." + id), got != null ? ReportSheet.PeriodName(ctx, got.Period) : s.Get("milestone.notYet"), StatementSheet.HubWhy, MilestonesSection.WhyBase + index) { Icon = "star" };
            if (got != null) { row.Note = s.Get("milestone." + id + ".done"); row.NoteTone = Tone.Good; }
            else { row.Note = Todo(ctx, id); row.Dim = true; }
            return row;
        }

        /// <summary>What an unreached milestone needs, with where you stand now.</summary>
        public static string Todo(ViewContext ctx, string id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Sim.Core.Definitions.MilestonesDef d = sim.Config.Game.Ledger.Milestones;
            NetWorthSnapshot worth = Reporting.NetWorth(sim);
            switch (id)
            {
                case MilestoneRules.FirstLakh:
                    return s.Get("milestone.firstLakh.todo", "have", ctx.Fmt.Coins(MilestoneRules.Saved(worth)), "target", ctx.Fmt.Coins(d.Savings));
                case MilestoneRules.Twice:
                    return s.Get("milestone.twiceInherited.todo", "have", ctx.Fmt.Coins(worth.Total), "target", ctx.Fmt.Coins(d.NetWorthTimes * MilestoneRules.InheritedNow(sim)),
                        "times", ctx.Fmt.Integer(d.NetWorthTimes), "inherited", ctx.Fmt.Coins(MilestoneRules.InheritedNow(sim)));
                case MilestoneRules.Passive:
                    return s.Get("milestone.passiveCoversLiving.todo", "pct", ctx.Fmt.Integer(Hub.PassivePercent(sim)));
                default:
                    return s.Get("milestone." + id + ".todo");
            }
        }
    }

    /// <summary>The toast after a close (spec 019 §3.6): one to three name themselves, more say how many; an older town's first close says they're from past seasons.</summary>
    public sealed class MilestoneToastVm
    {
        public string Kicker { get; internal set; }
        public string Text { get; internal set; }

        /// <summary>Opens Money and loans, Your wealth, where the list is.</summary>
        public SheetRequest Opens { get; internal set; }

        public static MilestoneToastVm Build(ViewContext ctx, IReadOnlyList<Sim.Core.Events.MilestoneReached> reached)
        {
            if (reached == null || reached.Count == 0) return null;
            var s = ctx.Strings;
            bool past = false;
            foreach (Sim.Core.Events.MilestoneReached m in reached) past |= m.Past;
            var names = new List<string>();
            foreach (Sim.Core.Events.MilestoneReached m in reached) names.Add(s.Get("milestone." + m.Id));
            var vm = new MilestoneToastVm { Kicker = s.Get(reached.Count == 1 ? "milestone.toast.single" : "milestone.toast.several"), Opens = new SheetRequest("Money", 0, MoneySheet.Wealth) };
            if (past) vm.Text = s.Get("milestone.toast.past", "n", ctx.Fmt.Integer(reached.Count));
            else if (reached.Count <= 3) vm.Text = string.Join(", ", names);
            else vm.Text = s.Get("milestone.toast.count", "n", ctx.Fmt.Integer(reached.Count));
            return vm;
        }
    }
}
