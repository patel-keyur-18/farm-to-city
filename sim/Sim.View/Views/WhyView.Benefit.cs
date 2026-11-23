using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Benefits and the night shift (spec 028 §3.8): what each benefit costs and does, with its "Does it pay?" in your own numbers; loyalty; why a night shift is offered.</summary>
    public static partial class WhyView
    {
        private static WhyVm BenefitWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BenefitsDef d = BenefitRules.Defs(sim);
            long employer = id / 16;
            var b = (Benefit)(id % 16);
            string k = BenefitSheet.Key(b);
            WhyVm vm = New(ctx, "why.benefit." + k + ".title", Money(ctx, BenefitRules.Cost(sim, b)));
            Row(vm, ctx, "why.benefit.cost", Money(ctx, BenefitRules.Cost(sim, b)), "n", ctx.Fmt.Integer(BenefitRules.Staff(sim, employer).Count));
            Text(vm, ctx, "why.benefit." + k + ".effect");
            long wage = BenefitSheet.WageOf(ctx, employer);
            Fixed lift = b == Benefit.Meal ? d.MealMotivation : b == Benefit.Health ? d.HealthMotivation : b == Benefit.Festival ? d.FestivalMotivation : d.LeaveMotivation;
            Fixed net = b == Benefit.Leave ? lift - Fixed.FromRatio(d.LeaveDays, d.LeaveOf) : lift; // paid leave's days are not worked: it can cost output
            Row(vm, ctx, "why.benefit.output", Money(ctx, Sim.Core.Finance.Money.MulRate(wage, net)), "pct", ctx.Fmt.Decimal1(net * Fixed.FromInt(100)), "wage", Money(ctx, wage));
            Row(vm, ctx, "why.benefit.keep", ctx.Fmt.Decimal1(d.Loyalty * Fixed.FromInt(100)) + "%", "poach", ctx.Fmt.Decimal1(sim.Config.Game.People.Hr.PoachChance * Fixed.FromInt(100)));
            Text(vm, ctx, "why.benefit.rep", "n", ctx.Fmt.Integer(d.KeptForReputation));
            vm.Footnote = ctx.Strings.Get("why.benefit.foot");
            return vm;
        }

        private static WhyVm LoyaltyWhy(ViewContext ctx, long employer)
        {
            BenefitsDef d = BenefitRules.Defs(ctx.Sim);
            int flags = BenefitRules.FlagsOfEmployer(ctx.Sim, employer);
            Fixed poach = ctx.Sim.Config.Game.People.Hr.PoachChance;
            WhyVm vm = New(ctx, "why.loyalty.title", ctx.Fmt.Decimal1(poach * BenefitRules.LoyaltyOf(ctx.Sim, flags) * Fixed.FromInt(100)) + "%");
            Row(vm, ctx, "why.loyalty.base", ctx.Fmt.Decimal1(poach * Fixed.FromInt(100)) + "%");
            Row(vm, ctx, "why.loyalty.each", ctx.Fmt.Percent(d.Loyalty));
            Row(vm, ctx, "why.loyalty.kept", ctx.Fmt.Integer(BenefitRules.Count(flags)));
            Text(vm, ctx, "why.loyalty.leave");
            Text(vm, ctx, "why.loyalty.rep", "n", ctx.Fmt.Integer(d.KeptForReputation));
            vm.Footnote = ctx.Strings.Get("why.loyalty.foot");
            return vm;
        }

        private static WhyVm NightWhy(ViewContext ctx, long id)
        {
            BenefitsDef d = BenefitRules.Defs(ctx.Sim);
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.night.title", b == null ? "" : ctx.Fmt.Integer(NightRules.ShortCount(ctx.Sim, b)) + " / " + ctx.Fmt.Integer(d.NightLookBack));
            Text(vm, ctx, "why.night.what", "from", ctx.Fmt.Integer(d.NightStart), "to", ctx.Fmt.Integer(d.NightEnd));
            Row(vm, ctx, "why.night.offered", ctx.Fmt.Integer(d.NightNeedDays), "of", ctx.Fmt.Integer(d.NightLookBack));
            Row(vm, ctx, "why.night.wage", "+" + ctx.Fmt.Percent(d.NightWageUp));
            Row(vm, ctx, "why.night.tired", ctx.Fmt.Integer((d.NightFatigue * Fixed.FromInt(100)).RoundToLong()) + " points");
            Text(vm, ctx, "why.night.grid");
            vm.Footnote = ctx.Strings.Get("why.night.foot");
            return vm;
        }
    }
}
