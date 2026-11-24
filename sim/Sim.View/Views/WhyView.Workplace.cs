using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Supervisors, accidents, conflicts and associations (spec 028 §3.9): the chance, its factors and what each answer does, in words.</summary>
    public static partial class WhyView
    {
        private static WhyVm SupervisorWhy(ViewContext ctx, long id)
        {
            WorkplaceDef d = WorkplaceSheet.Defs(ctx);
            WhyVm vm = New(ctx, "why.supervisor.title", ctx.Fmt.Integer(d.SupervisorMinWorkers) + "+");
            Row(vm, ctx, "why.supervisor.span", ctx.Fmt.Integer(d.SupervisorSpan));
            Row(vm, ctx, "why.supervisor.star", "+" + ctx.Fmt.Decimal1(d.OutputPerStar * Fixed.FromInt(100)) + "%");
            Row(vm, ctx, "why.supervisor.risk", ctx.Fmt.Percent(d.RiskHalf));
            Text(vm, ctx, "why.supervisor.quarrel");
            Text(vm, ctx, "why.supervisor.assoc");
            vm.Footnote = ctx.Strings.Get("why.supervisor.foot", "wage", ctx.Fmt.Coins(HiringRules.AskedWage(ctx.Sim, HiringRules.SupervisorPlace, Fixed.FromInt(50), false)));
            return vm;
        }

        /// <summary>"Does it pay?" in your own numbers; the id is his stars (3 before you hire).</summary>
        private static WhyVm SupervisorPaysWhy(ViewContext ctx, long id)
        {
            Supervisor sv = WorkplaceRules.Supervisor(ctx.Sim);
            int stars = sv != null ? WorkplaceRules.Stars(sv.Skill) : (int)id;
            long wage = sv != null ? sv.Wage : HiringRules.AskedWage(ctx.Sim, HiringRules.SupervisorPlace, Fixed.FromInt(50), false);
            WorkplaceSheet.Gains(ctx, stars, wage, out long output, out long accidents, out long conflicts, out long left);
            int covered = WorkplaceRules.Workers(ctx.Sim, 0).Count;
            WhyVm vm = New(ctx, "why.supervisorPays.title", (left >= 0 ? "+" : "-") + Money(ctx, System.Math.Abs(left)));
            Row(vm, ctx, "why.supervisorPays.cost", Money(ctx, wage));
            Row(vm, ctx, "why.supervisorPays.output", "+" + Money(ctx, output), "n", ctx.Fmt.Integer(System.Math.Min(WorkplaceSheet.Defs(ctx).SupervisorSpan, covered)), "stars", ctx.Fmt.Integer(stars), "out", Money(ctx, Sim.Core.Market.MarketRules.Indexed(ctx.Sim, WorkplaceSheet.Defs(ctx).HandOutput)));
            Row(vm, ctx, "why.supervisorPays.accidents", "+" + Money(ctx, accidents));
            Row(vm, ctx, "why.supervisorPays.conflicts", "+" + Money(ctx, conflicts));
            if (sv != null) Row(vm, ctx, "why.supervisorPays.spared", ctx.Fmt.Integer(sv.Spared + sv.Settled + sv.Kept));
            vm.Footnote = ctx.Strings.Get("why.supervisorPays.foot");
            return vm;
        }

        private static WhyVm AccidentWhy(ViewContext ctx, long place)
        {
            Simulation sim = ctx.Sim;
            WorkplaceDef d = WorkplaceSheet.Defs(ctx);
            List<IStaff> list = WorkplaceRules.Workers(sim, place);
            IStaff w = list.Count > 0 ? list[0] : null;
            WhyVm vm = New(ctx, "why.accident.title", w == null ? "" : WorkplaceSheet.Season(ctx, WorkplaceRules.AccidentChance(sim, w, true)));
            Row(vm, ctx, "why.accident.base", ctx.Fmt.Decimal2(d.AccidentChance * Fixed.FromInt(100)) + "%");
            Row(vm, ctx, "why.accident.night", ctx.Strings.Get("why.pctMore", "pct", Pct(ctx, d.NightFactor - Fixed.One)));
            Row(vm, ctx, "why.accident.overtime", ctx.Strings.Get("why.pctMore", "pct", Pct(ctx, d.OvertimeFactor - Fixed.One)));
            Row(vm, ctx, "why.accident.skipped", ctx.Strings.Get("why.pctMore", "pct", Pct(ctx, d.SkippedFactor - Fixed.One)));
            Row(vm, ctx, "why.accident.half", ctx.Fmt.Percent(d.RiskHalf));
            Row(vm, ctx, "why.accident.severe", ctx.Fmt.Percent(d.SevereShare), "hours", ctx.Fmt.Integer(d.SevereHours), "bill", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.SevereBill)));
            Text(vm, ctx, "why.accident.out", "hours", ctx.Fmt.Integer(d.OutHours), "clinic", ctx.Fmt.Integer(d.ClinicOutHours), "bill", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.Bill)));
            vm.Footnote = ctx.Strings.Get("why.accident.foot");
            return vm;
        }

        private static WhyVm ConflictWhy(ViewContext ctx, long place)
        {
            Simulation sim = ctx.Sim;
            WorkplaceDef d = WorkplaceSheet.Defs(ctx);
            List<IStaff> list = WorkplaceRules.Workers(sim, place);
            WhyVm vm = New(ctx, "why.conflict.title", list.Count < d.ConflictMinWorkers ? ctx.Fmt.Integer(d.ConflictMinWorkers) + "+" : WorkplaceSheet.Season(ctx, WorkplaceRules.ConflictChance(sim, place)));
            Row(vm, ctx, "why.conflict.base", ctx.Fmt.Decimal1(d.ConflictChance * Fixed.FromInt(100)) + "%", "n", ctx.Fmt.Integer(d.ConflictMinWorkers));
            Row(vm, ctx, "why.conflict.tired", ctx.Strings.Get("why.points", "n", ctx.Fmt.Decimal1(WorkplaceRules.AverageFatigue(list) * Fixed.FromInt(100))), "k", ctx.Fmt.Integer(d.ConflictFatigue.RoundToLong()));
            Text(vm, ctx, "why.conflict.supervisor");
            Text(vm, ctx, "why.conflict.choices", "hours", ctx.Fmt.Integer(d.TalkHours), "pct", ctx.Fmt.Percent(d.TalkResolves), "resign", ctx.Fmt.Percent(d.LetResigns));
            vm.Footnote = ctx.Strings.Get("why.conflict.foot");
            return vm;
        }

        private static WhyVm AssociationWhy(ViewContext ctx, long place)
        {
            Simulation sim = ctx.Sim;
            WorkplaceDef d = WorkplaceSheet.Defs(ctx);
            List<IStaff> list = WorkplaceRules.Workers(sim, place);
            PlaceRelations r = sim.State.People.Workplace.Relations(place);
            WhyVm vm = New(ctx, "why.association.title", ctx.Fmt.Integer(d.AssocMinWorkers) + "+");
            Text(vm, ctx, "why.association.when", "n", ctx.Fmt.Integer(d.AssocMinWorkers));
            Row(vm, ctx, "why.association.pay", ctx.Strings.Get(WorkplaceRules.PayLow(sim, list) ? "why.yes" : "why.no"), "pct", ctx.Fmt.Percent(d.AssocPayUnder));
            Row(vm, ctx, "why.association.tired", ctx.Strings.Get(r != null && r.TiredCloses >= d.AssocCloses ? "why.yes" : "why.no"), "level", ctx.Fmt.Decimal1(d.AssocFatigue * Fixed.FromInt(100)), "closes", ctx.Fmt.Integer(d.AssocCloses));
            Row(vm, ctx, "why.association.name", ctx.Strings.Get(sim.State.People.Reputation < d.AssocReputation ? "why.yes" : "why.no"), "score", ctx.Fmt.Integer(d.AssocReputation));
            Row(vm, ctx, "why.association.forms", ctx.Fmt.Percent(WorkplaceRules.FormChance(sim, true)));
            Text(vm, ctx, "why.association.asks", "raise", ctx.Fmt.Percent(d.AssocRaise), "quiet", ctx.Fmt.Integer(d.AssocQuietCloses));
            vm.Footnote = ctx.Strings.Get("why.association.foot");
            return vm;
        }
    }
}
