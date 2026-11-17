using Sim.Core;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>The old village's *Why?* ids on the Town sheet (spec 023 §3.5).</summary>
    public static class CohortWhy
    {
        public const int Village = 0, Happiness = 1, Pupils = 2, Literacy = 3, Tax = 4;
    }

    public static partial class WhyView
    {
        /// <summary>The old village's rows (spec 023 §3.5): who they are, their four layers in words, the school's children, who can read, the tax from leisure and savings.</summary>
        private static WhyVm CohortWhyView(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            CohortState c = CohortRules.State(sim);
            CohortRules.Budget b = CohortRules.BudgetOf(sim);
            WhyVm vm;
            switch ((int)id)
            {
                case CohortWhy.Happiness:
                    vm = New(ctx, "why.cohort.happiness.title", ctx.Fmt.Integer(c.Happiness.RoundToLong()));
                    Row(vm, ctx, "why.cohort.survival", Pct(ctx, c.Survival));
                    Row(vm, ctx, "why.cohort.security", Pct(ctx, c.Security));
                    Row(vm, ctx, "why.cohort.belonging", Pct(ctx, c.Belonging));
                    Row(vm, ctx, "why.cohort.growth", Pct(ctx, c.Growth));
                    Text(vm, ctx, "why.cohort.happiness.text", "below", ctx.Fmt.Integer(CohortRules.Def(sim).LeaveBelow), "closes", ctx.Fmt.Integer(CohortRules.Def(sim).LeaveCloses));
                    return vm;
                case CohortWhy.Pupils:
                    vm = New(ctx, "why.cohort.pupils.title", ctx.Fmt.Integer(CohortRules.Pupils(sim)));
                    Text(vm, ctx, "why.cohort.pupils.text");
                    return vm;
                case CohortWhy.Literacy:
                    vm = New(ctx, "why.cohort.literacy.title", Pct(ctx, CohortRules.Literacy(sim)));
                    Text(vm, ctx, "why.cohort.literacy.text");
                    return vm;
                case CohortWhy.Tax:
                    vm = New(ctx, "why.cohort.tax.title", Money(ctx, c.TaxThis));
                    Row(vm, ctx, "why.cohort.tax.last", Money(ctx, c.TaxLast));
                    Row(vm, ctx, "why.cohort.tax.leisure", Money(ctx, b.Leisure * c.Households));
                    Row(vm, ctx, "why.cohort.tax.savings", Money(ctx, c.Savings));
                    Text(vm, ctx, "why.cohort.tax.text");
                    return vm;
                default:
                    vm = New(ctx, "why.cohort.village.title", s.Get("cohort.villageValue", "households", ctx.Fmt.Integer(c.Households), "people", ctx.Fmt.Integer(CohortRules.People(sim))));
                    Row(vm, ctx, "why.cohort.village.left", ctx.Fmt.Integer(c.Left));
                    Row(vm, ctx, "why.cohort.village.income", Money(ctx, b.Income));
                    Text(vm, ctx, "why.cohort.village.text");
                    return vm;
            }
        }
    }
}
