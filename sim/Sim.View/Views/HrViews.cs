using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// HR events (spec 015 §3.7, §7): what happened in a sentence and the two choices, as a card when it happens and as a sheet from
    /// the alerts. The first choice says what it costs now.
    /// </summary>
    public static class HrEventSheet
    {
        /// <summary>The <i>Why?</i> behind a timed card's chance.</summary>
        private static string WhyKey(HrEvent e) { return e.Kind == HrEventKind.Accident ? "accident" : e.Kind == HrEventKind.Conflict ? "conflict" : "association"; }

        /// <summary>The worker's name (a hand's or a business worker's), or "A former worker".</summary>
        public static string Name(ViewContext ctx, HrEvent e)
        {
            IStaff w = WorkRules.Find(ctx.Sim, e.Place, e.WorkerId);
            return w is Hand h ? PeopleWords.Name(ctx, h.Name) : w is Worker k ? PeopleWords.Name(ctx, k.Name) : ctx.Strings.Get("away.formerWorker");
        }

        public static string Title(ViewContext ctx, HrEvent e)
        {
            string key = "hr." + e.Kind + ".title";
            if (e.Kind == HrEventKind.Accident) return ctx.Strings.Get(e.Severe ? "hr.Accident.titleSevere" : key, "name", Name(ctx, e));
            if (e.Kind == HrEventKind.Conflict) return ctx.Strings.Get(key, "name", Name(ctx, e), "other", OtherName(ctx, e));
            if (e.Kind == HrEventKind.Association) return ctx.Strings.Get(key);
            if (e.Kind != HrEventKind.BadBatch) return ctx.Strings.Get(key, "name", Name(ctx, e));
            return ctx.Strings.Get(key, "business", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(e.Place))));
        }

        /// <summary>The second worker of a quarrel.</summary>
        public static string OtherName(ViewContext ctx, HrEvent e)
        {
            IStaff w = WorkRules.Find(ctx.Sim, e.Place, e.OtherId);
            return w is Hand h ? PeopleWords.Name(ctx, h.Name) : w is Worker k ? PeopleWords.Name(ctx, k.Name) : ctx.Strings.Get("away.formerWorker");
        }

        /// <summary>The wording of the three cards with a deadline (spec 028 §3.9).</summary>
        private static string TimedText(ViewContext ctx, HrEvent e)
        {
            var s = ctx.Strings;
            WorkplaceDef d = ctx.Sim.Config.Game.Workplace;
            string name = Name(ctx, e);
            switch (e.Kind)
            {
                case HrEventKind.Accident:
                    IStaff w = WorkRules.Find(ctx.Sim, e.Place, e.WorkerId);
                    string bill = e.Cost > 0 ? s.Get("hr.Accident.billPaid", "amount", ctx.Fmt.Coins(e.Cost))
                        : w != null && Sim.Core.People.BenefitRules.Has(ctx.Sim, w, Sim.Core.People.Benefit.Health) ? s.Get("hr.Accident.billCover") : s.Get("hr.Accident.billOwn");
                    return s.Get("hr.Accident.text", "name", name, "hours", ctx.Fmt.Duration((e.Hours) * 3600L), "bill", bill, "amount", ctx.Fmt.Coins(e.Amount));
                case HrEventKind.Conflict:
                    return s.Get("hr.Conflict.text", "name", name, "other", OtherName(ctx, e), "hours", ctx.Fmt.Duration((d.TalkHours) * 3600L), "pct", ctx.Fmt.Percent(d.TalkResolves));
                default:
                    var why = new System.Collections.Generic.List<string>();
                    System.Collections.Generic.List<IStaff> list = WorkplaceRules.Workers(ctx.Sim, e.Place);
                    if (WorkplaceRules.PayLow(ctx.Sim, list)) why.Add(s.Get("hr.Association.whyPay"));
                    PlaceRelations r = ctx.Sim.State.People.Workplace.Relations(e.Place);
                    if (r != null && r.TiredCloses >= d.AssocCloses) why.Add(s.Get("hr.Association.whyTired"));
                    if (ctx.Sim.State.People.Reputation < d.AssocReputation) why.Add(s.Get("hr.Association.whyName"));
                    return s.Get("hr.Association.text", "n", ctx.Fmt.Integer(list.Count), "why", why.Count == 0 ? s.Get("hr.Association.whyNone") : string.Join(" ", why),
                        "raise", ctx.Fmt.Percent(d.AssocRaise), "half", ctx.Fmt.Percent(d.AssocNegotiateRaise), "agree", ctx.Fmt.Percent(d.AssocAgrees),
                        "output", ctx.Fmt.Percent(d.RefuseOutput), "stop", ctx.Fmt.Duration((d.StoppageHours) * 3600L));
            }
        }

        public static string Text(ViewContext ctx, HrEvent e)
        {
            var s = ctx.Strings;
            if (e.Timed) return TimedText(ctx, e);
            IStaff w = WorkRules.Find(ctx.Sim, e.Place, e.WorkerId);
            string key = "hr." + e.Kind + ".text", name = Name(ctx, e);
            long paid = w == null ? 0 : HrRules.Paid(w);
            switch (e.Kind)
            {
                case HrEventKind.RaiseRequest:
                    return s.Get(key, "name", name, "asked", ctx.Fmt.Coins(e.Amount), "paid", ctx.Fmt.Coins(paid),
                        "why", s.Get(w != null && w.Fairness < 0 ? "hr.RaiseRequest.hiredLow" : "hr.RaiseRequest.grown")) + " " + PayNow(ctx, e, w);
                case HrEventKind.Poaching:
                    return s.Get(key, "name", name, "offer", ctx.Fmt.Coins(e.Amount), "paid", ctx.Fmt.Coins(paid)) + " " + PayNow(ctx, e, w);
                case HrEventKind.SickLeave:
                    return s.Get(key, "name", name, "days", s.Plural("why.days", ctx.Sim.Config.Game.People.Hr.SickDays), "amount", ctx.Fmt.Coins(e.Amount));
                case HrEventKind.FamilyEmergency:
                    return s.Get(key, "name", name, "amount", ctx.Fmt.Coins(e.Amount)) + " " + PayNow(ctx, e, w);
                default:
                    return s.Get(key, "qty", ctx.Fmt.Kg(e.Grams), "good", BusinessWords.Good(ctx, e.Good), "cost", ctx.Fmt.Coins(e.Cost), "amount", ctx.Fmt.Coins(e.Amount));
            }
        }

        /// <summary>"Saying yes costs 2,780 coins now, from your cash" (or the business's).</summary>
        private static string PayNow(ViewContext ctx, HrEvent e, IStaff w)
        {
            string from = w is Worker k ? ctx.Strings.Get("hr.fromBusiness", "business", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(k.Place))))
                : ctx.Strings.Get("hr.fromYou");
            return ctx.Strings.Get("hr.payNow", "amount", ctx.Fmt.Coins(HrRules.CostNow(ctx.Sim, e)), "from", from);
        }

        /// <summary>A choice's button: its label and what it costs or brings now.</summary>
        public static ActionVm Choice(ViewContext ctx, HrEvent e, int choice)
        {
            var s = ctx.Strings;
            long id = e.Id;
            string label = s.Get("hr." + e.Kind + ".choice" + choice, "asked", ctx.Fmt.Coins(e.Amount), "offer", ctx.Fmt.Coins(e.Amount));
            string detail = null;
            if (e.Timed) return TimedChoice(ctx, e, choice, label);
            long now = HrRules.CostNow(ctx.Sim, e);
            if (choice == 0 && now > 0) detail = s.Get("hr.costNow", "amount", ctx.Fmt.Coins(now));
            if (choice == 1 && (e.Kind == HrEventKind.SickLeave || e.Kind == HrEventKind.BadBatch)) detail = s.Get("hr.back", "amount", ctx.Fmt.Coins(e.Amount));
            return ActionVm.Checked(ctx, "hr:" + id + ":" + choice, label, detail, choice == 0 ? ActionKind.Primary : ActionKind.Secondary, at => new AnswerHrEvent(at, id, choice));
        }

        /// <summary>A choice of an accident, a quarrel or an association: what it costs you now and what it does.</summary>
        private static ActionVm TimedChoice(ViewContext ctx, HrEvent e, int choice, string label)
        {
            var s = ctx.Strings;
            WorkplaceDef d = ctx.Sim.Config.Game.Workplace;
            long id = e.Id;
            string detail = null;
            long now = WorkplaceRules.CostNow(ctx.Sim, e, choice);
            if (now > 0) detail = s.Get("hr.costNow", "amount", ctx.Fmt.Coins(now));
            else if (e.Kind == HrEventKind.Accident) detail = choice == 0 ? s.Get("hr.Accident.keep", "amount", ctx.Fmt.Coins(e.Amount)) : s.Get("hr.back", "amount", ctx.Fmt.Coins(e.Amount));
            else if (e.Kind == HrEventKind.Conflict) detail = s.Get(choice == 0 ? "hr.Conflict.detail0" : choice == 1 ? "hr.Conflict.detail1" : "hr.Conflict.detail2", "hours", ctx.Fmt.Duration((d.TalkHours) * 3600L), "other", OtherName(ctx, e));
            else if (e.Kind == HrEventKind.Association) detail = s.Get(choice == 0 ? "hr.Association.detail0" : choice == 1 ? "hr.Association.detail1" : "hr.Association.detail2");
            // The default after the deadline is the last answer of a quarrel and an association, the first of an accident.
            return ActionVm.Checked(ctx, "hr:" + id + ":" + choice, label, detail, choice == WorkplaceRules.DefaultChoice(e.Kind) ? ActionKind.Primary : ActionKind.Secondary, at => new AnswerHrEvent(at, id, choice));
        }

        public static SheetVm Build(ViewContext ctx, long eventId)
        {
            HrEvent e = ctx.Sim.State.People?.HrEvents.Find(x => x.Id == eventId);
            if (e == null) return new SheetVm { Icon = "people", Title = ctx.Reject(RejectReason.UnknownEvent) };
            var vm = new SheetVm { Icon = "people", Title = Title(ctx, e), Subtitle = ctx.Strings.Get("hr.subtitle", "time", ctx.Fmt.Duration(ctx.Now - e.At)) };
            vm.Lines.Add(new LineVm(Text(ctx, e), null));
            if (e.Timed)
            {
                long left = System.Math.Max(0, e.ExpiresAt - ctx.Now);
                vm.Subtitle = ctx.Strings.Get("hr.timeLeft", "time", ctx.Fmt.Duration(left));
                vm.Lines.Add(new LineVm(ctx.Strings.Get("hr.defaultIs"), ctx.Strings.Get("hr." + e.Kind + ".choice" + WorkplaceRules.DefaultChoice(e.Kind), "asked", "", "offer", ""), WhyKey(e), e.Place));
            }
            int n = e.Kind == HrEventKind.Accident || !e.Timed ? 2 : 3;
            for (int i = 0; i < n; i++) vm.Actions.Add(Choice(ctx, e, i));
            return vm;
        }

        /// <summary>The card when it happens: the same words, the first choice as Yes and the second as Not now.</summary>
        public static CardVm Card(ViewContext ctx, long eventId)
        {
            HrEvent e = ctx.Sim.State.People?.HrEvents.Find(x => x.Id == eventId);
            if (e == null) return null;
            var c = new CardVm { Title = Title(ctx, e), Text = Text(ctx, e) };
            if (e.Timed)
            {
                // Three answers don't fit a card: it opens the sheet with them, and its own default answers after a day.
                c.Text += " " + ctx.Strings.Get("hr.timeLeft", "time", ctx.Fmt.Duration(System.Math.Max(0, e.ExpiresAt - ctx.Now)));
                c.Buttons.Add(new CardButton(SettingsIds.Decide, ctx.Strings.Get("hr.decide"), ActionKind.Primary));
                c.Buttons.Add(new CardButton(SettingsIds.NotNow, ctx.Strings.Get("hr.later"), ActionKind.Secondary));
                return c;
            }
            c.Buttons.Add(new CardButton(SettingsIds.Yes, Choice(ctx, e, 0).Label, ActionKind.Primary));
            c.Buttons.Add(new CardButton(SettingsIds.NotNow, Choice(ctx, e, 1).Label, ActionKind.Secondary));
            return c;
        }
    }
}
