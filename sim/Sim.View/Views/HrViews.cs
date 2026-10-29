using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// HR events (spec 015 §3.7, §7): what happened in a sentence and the two choices, as a card when it happens and as a sheet from
    /// the alerts. The first choice says what it costs now.
    /// </summary>
    public static class HrEventSheet
    {
        /// <summary>The worker's name (a hand's or a business worker's), or "A former worker".</summary>
        public static string Name(ViewContext ctx, HrEvent e)
        {
            IStaff w = WorkRules.Find(ctx.Sim, e.Place, e.WorkerId);
            return w is Hand h ? PeopleWords.Name(ctx, h.Name) : w is Worker k ? PeopleWords.Name(ctx, k.Name) : ctx.Strings.Get("away.formerWorker");
        }

        public static string Title(ViewContext ctx, HrEvent e)
        {
            string key = "hr." + e.Kind + ".title";
            if (e.Kind != HrEventKind.BadBatch) return ctx.Strings.Get(key, "name", Name(ctx, e));
            return ctx.Strings.Get(key, "business", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(e.Place))));
        }

        public static string Text(ViewContext ctx, HrEvent e)
        {
            var s = ctx.Strings;
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
            long now = HrRules.CostNow(ctx.Sim, e);
            if (choice == 0 && now > 0) detail = s.Get("hr.costNow", "amount", ctx.Fmt.Coins(now));
            if (choice == 1 && (e.Kind == HrEventKind.SickLeave || e.Kind == HrEventKind.BadBatch)) detail = s.Get("hr.back", "amount", ctx.Fmt.Coins(e.Amount));
            return ActionVm.Checked(ctx, "hr:" + id + ":" + choice, label, detail, choice == 0 ? ActionKind.Primary : ActionKind.Secondary, at => new AnswerHrEvent(at, id, choice));
        }

        public static SheetVm Build(ViewContext ctx, long eventId)
        {
            HrEvent e = ctx.Sim.State.People?.HrEvents.Find(x => x.Id == eventId);
            if (e == null) return new SheetVm { Icon = "people", Title = ctx.Reject(RejectReason.UnknownEvent) };
            var vm = new SheetVm { Icon = "people", Title = Title(ctx, e), Subtitle = ctx.Strings.Get("hr.subtitle", "time", ctx.Fmt.Duration(ctx.Now - e.At)) };
            vm.Lines.Add(new LineVm(Text(ctx, e), null));
            vm.Actions.Add(Choice(ctx, e, 0));
            vm.Actions.Add(Choice(ctx, e, 1));
            return vm;
        }

        /// <summary>The card when it happens: the same words, the first choice as Yes and the second as Not now.</summary>
        public static CardVm Card(ViewContext ctx, long eventId)
        {
            HrEvent e = ctx.Sim.State.People?.HrEvents.Find(x => x.Id == eventId);
            if (e == null) return null;
            var c = new CardVm { Title = Title(ctx, e), Text = Text(ctx, e) };
            c.Buttons.Add(new CardButton(SettingsIds.Yes, Choice(ctx, e, 0).Label, ActionKind.Primary));
            c.Buttons.Add(new CardButton(SettingsIds.NotNow, Choice(ctx, e, 1).Label, ActionKind.Secondary));
            return c;
        }
    }
}
