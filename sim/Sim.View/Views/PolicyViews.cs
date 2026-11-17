using System;
using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// <em>Taxes and policies</em>, the Policies tab (spec 022 §7, US-141): the five policies, each with what it costs a season, where it stands,
    /// who it helps and who pays, and <em>Enact</em> or <em>Repeal</em>. At most three at a time. Nothing here is needed to keep the town running.
    /// </summary>
    public static class PolicySheet
    {
        public const string Kind = "Policies";

        private static readonly string[] Keys = { "meals", "subsidy", "grant", "minWage", "rentControl" };
        private static readonly string[] Icons = { "basket", "fertiliser", "savings", "population", "deed" };

        public static string Key(int policy) { return Keys[policy]; }
        public static string Name(ViewContext ctx, int policy) { return ctx.Strings.Get("policy." + Keys[policy] + ".name"); }

        /// <summary>Taxes and Policies, over both sheets (the Policies tab says how many of the slots are taken).</summary>
        internal static void Tabs(ViewContext ctx, SheetVm vm, string tab)
        {
            var s = ctx.Strings;
            vm.Tab = tab;
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:taxes", s.Get("policy.tabTaxes"), new SheetRequest(TaxSheet.Kind, 0)),
                ActionVm.Open("tab:policies", s.Get("policy.tabPolicies", "used", ctx.Fmt.Integer(PolicyRules.Used(ctx.Sim)), "max", ctx.Fmt.Integer(PolicyRules.Def(ctx.Sim).MaxActive)), new SheetRequest(Kind, 0)),
            };
        }

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "place_council_hall", Title = s.Get("tax.title"), Subtitle = ReportSheet.PeriodName(ctx, sim.Period) };
            if (!TreasuryRules.IsOpen(sim)) return vm;
            TreasuryState st = TreasuryRules.State(sim);
            Tabs(ctx, vm, "tab:policies");
            vm.Lines.Add(new LineVm(s.Get("tax.notYours"), null) { Banner = true });
            if (!st.PolicyTourSeen) // the first-visit note (spec 022 §3.7): once
                vm.Lines.Add(new LineVm(s.Get("policy.tourTitle"), null) { Banner = true, Note = s.Get("policy.tour"), Action = ActionVm.Checked(ctx, "tour", s.Get("tax.tourGotIt"), null, ActionKind.Primary, at => new SeePolicyTour(at)) });

            TreasuryRules.CloseForecast f = TreasuryRules.Forecast(sim);
            long income = TreasuryRules.ExpectedIncome(sim);
            vm.Lines.Add(new LineVm(s.Get("tax.cash"), ctx.Fmt.Coins(st.Cash), "treasuryBalance", 0));
            vm.Lines.Add(new LineVm(s.Get("treasury.atClose"), TreasurySheet.CloseStatus(ctx, income, f.Due, st.Cash, out Tone tone), "treasuryDue", 0) { NoteTone = tone });

            for (int i = 0; i < PolicyState.Count; i++) vm.Lines.Add(Row(ctx, i));
            vm.Lines.Add(new LineVm(s.Get("policy.leftAlone"), null) { Banner = true });
            return vm;
        }

        private static LineVm Row(ViewContext ctx, int i)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var p = (PolicyId)i;
            PolicyState st = PolicyRules.Of(sim, p);
            long cost = PolicyRules.Cost(sim, p);
            string state = State(ctx, p, st, out Tone tone);
            string note = (state == null ? "" : state + "\n") + s.Get("policy.helps", "text", s.Get("policy." + Keys[i] + ".helps")) + "\n" + s.Get("policy.costs", "text", s.Get("policy." + Keys[i] + ".costs"));
            ActionVm action = PolicyRules.Holds(st)
                ? ActionVm.Checked(ctx, "repeal:" + Keys[i], s.Get("policy.repeal"), null, ActionKind.Secondary, at => new RepealPolicy(at, p))
                : ActionVm.Checked(ctx, "enact:" + Keys[i], s.Get("policy.enact"), null, ActionKind.Primary, at => new EnactPolicy(at, p));
            return new LineVm(Name(ctx, i), CostText(ctx, p, cost), "policy", i) { Icon = Icons[i], Note = note, NoteTone = tone, Action = action };
        }

        /// <summary>"28,000 a season", or what a zero cost means.</summary>
        public static string CostText(ViewContext ctx, PolicyId p, long cost)
        {
            var s = ctx.Strings;
            if (cost > 0) return s.Get("policy.cost", "amount", ctx.Fmt.Coins(cost));
            return s.Get(p == PolicyId.Meals ? "policy.noPupils" : "policy.noResidents");
        }

        /// <summary>Where a policy stands, in one line: setting up (and from when), working, paused, ending, waiting to be enacted again.</summary>
        private static string State(ViewContext ctx, PolicyId p, PolicyState st, out Tone tone)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            tone = Tone.Plain;
            if (st.Repealing) return s.Get("policy.ending");
            switch (st.Status)
            {
                case PolicyStatus.Enacted:
                case PolicyStatus.Active:
                    int left = PolicyRules.SettingUp(sim, p);
                    if (st.Status == PolicyStatus.Enacted) return s.Get("policy.firstPaid", "from", FieldSheets.SeasonName(ctx, sim.Period + left));
                    if (left > 0) return s.Get("policy.settingUp", "from", FieldSheets.SeasonName(ctx, sim.Period + left));
                    return s.Get("policy.working");
                case PolicyStatus.Paused:
                    tone = Tone.Warn;
                    return s.Get("policy.paused");
            }
            long wait = PolicyRules.Wait(sim, p);
            if (wait > 0) return s.Get("policy.waiting", "from", FieldSheets.SeasonName(ctx, sim.Period + wait));
            if (p == PolicyId.Subsidy && st.Hangover > 0) return s.Plural("policy.hangover", st.Hangover);
            return null;
        }
    }
}
