using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>The events' <em>Why?</em>s (spec 027 §7): why a card came, what each choice costs, the odds in words, the fire's damage, what an answer cost or paid. Words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        /// <summary>Why an event came and what its choices cost; on a repair card, how the fire's damage was worked out.</summary>
        private static WhyVm EventWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            EventsState st = EventRules.State(sim);
            PendingEvent p = st == null ? null : st.Waiting.Find(x => x.Id == id);
            if (p == null) return New(ctx, "why.event.title", "");
            EventsDef d = sim.Config.Game.Events;
            EventDef ev = d.Events[p.Def];
            if (ev.Id == EventsDef.FireRepair) return FireDamage(ctx, p);
            var s = ctx.Strings;
            WhyVm vm = New(ctx, "why.event.title", EventSheet.Title(ctx, p.Def));
            Row(vm, ctx, "why.event.came", s.Get("event." + ev.Id + ".why." + p.Why));
            for (int c = 0; c < ev.Choices.Count; c++)
            {
                long cost = EventRules.ChoiceCost(sim, p.Def, c, p);
                Row(vm, ctx, "why.event.choice", cost > 0 ? ctx.Fmt.Coins(cost) : s.Get("why.event.free"), "name", EventSheet.ChoiceLabel(ctx, p, c));
            }
            Row(vm, ctx, "why.event.odds", s.Get(ev.Disaster ? "why.event.oddsDisaster" : ev.Major ? "why.event.oddsMajor" : "why.event.oddsMinor"));
            Text(vm, ctx, ev.Disaster ? "why.event.textDisaster" : "why.event.text");
            return vm;
        }

        /// <summary>The fire's damage: the building's worth, 35% of it, the tower's half and the preparation's half.</summary>
        private static WhyVm FireDamage(ViewContext ctx, PendingEvent p)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            FireDef f = sim.Config.Game.Events.FireDef;
            Sim.Core.Land.Building b = sim.State.Land.Buildings.Find(x => x.Id == p.Target);
            EventsState st = EventRules.State(sim);
            WhyVm vm = New(ctx, "why.fire.title", Money(ctx, p.Param));
            if (b != null)
            {
                bool tower = UtilityRules.Covers(sim, UtilityDef.Tower, b.TileId);
                Row(vm, ctx, "why.fire.worth", Money(ctx, b.BookValue));
                Row(vm, ctx, "why.fire.share", Money(ctx, Sim.Core.Finance.Money.MulRate(b.BookValue, f.DamageShare)), "pct", Pct(ctx, f.DamageShare));
                Row(vm, ctx, "why.fire.tower", s.Get(tower ? "why.fire.towerYes" : "why.fire.towerNo"));
                Row(vm, ctx, "why.fire.prepared", s.Get(st.DisasterChoice == 1 ? "why.fire.preparedYes" : "why.fire.preparedNo"));
            }
            Row(vm, ctx, "why.fire.atOnce", s.Get("why.fire.atOnceValue", "bill", Money(ctx, p.Param), "days", ctx.Fmt.Integer(f.DowntimeDays)));
            Row(vm, ctx, "why.fire.slowly", s.Get("why.fire.slowlyValue", "bill", Money(ctx, Sim.Core.Finance.Money.MulRate(p.Param, f.SlowRepairShare)), "days", ctx.Fmt.Integer(f.SlowRepairDays)));
            Text(vm, ctx, "why.fire.text");
            return vm;
        }

        /// <summary>A past answer: the choice, what it cost or paid, and whether it was taken for you.</summary>
        private static WhyVm EventHistWhy(ViewContext ctx, long index)
        {
            Simulation sim = ctx.Sim;
            EventsState st = EventRules.State(sim);
            var s = ctx.Strings;
            if (st == null || index < 0 || index >= st.History.Count) return New(ctx, "why.eventHist.title", "");
            EventRecord r = st.History[(int)index];
            EventDef ev = sim.Config.Game.Events.Events[r.Def];
            WhyVm vm = New(ctx, "why.eventHist.title", EventSheet.Title(ctx, r.Def));
            Row(vm, ctx, "why.eventHist.chose", s.Get("event." + ev.Id + ".choice." + ev.Choices[r.Choice].Id));
            Row(vm, ctx, "why.eventHist.paid", r.Cash < 0 ? Money(ctx, -r.Cash) : s.Get("why.event.free"));
            Row(vm, ctx, "why.eventHist.how", s.Get(r.Defaulted ? "why.eventHist.howDefault" : "why.eventHist.howYou"));
            Text(vm, ctx, "why.eventHist.text");
            return vm;
        }
    }
}
