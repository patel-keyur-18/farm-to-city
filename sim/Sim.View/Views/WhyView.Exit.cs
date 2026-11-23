using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Selling and worth (spec 028 §3.3): why a business is worth what it is, what it holds, how a sale is priced and what a branch is.</summary>
    public static partial class WhyView
    {
        private static WhyVm BusinessWorthWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ExitDef d = ExitRules.Defs(sim);
            Business b = BusinessOf(ctx, id);
            if (b == null) return New(ctx, "why.businessWorth.title", "");
            Worth w = ExitRules.Valuation(sim, b);
            WhyVm vm = New(ctx, "why.businessWorth.title", Money(ctx, w.Value));
            Row(vm, ctx, "why.businessWorth.profit", Money(ctx, w.Profit), "n", ctx.Fmt.Integer(w.Seasons));
            Row(vm, ctx, "why.businessWorth.base", ctx.Strings.Get("exit.times", "n", ctx.Fmt.Decimal1(d.BaseMultiple)));
            if (w.Growth != 0) Row(vm, ctx, w.Growth > 0 ? "why.businessWorth.growth" : "why.businessWorth.fall", (w.Growth > 0 ? "+" : "−") + ctx.Fmt.Integer(d.GrowthStep));
            else Text(vm, ctx, w.Seasons >= 4 ? "why.businessWorth.steady" : "why.businessWorth.noTrend");
            if (w.Risk) Row(vm, ctx, "why.businessWorth.risk", "−" + ctx.Fmt.Integer(d.RiskStep));
            if (w.Manager) Row(vm, ctx, "why.businessWorth.manager", "+" + ctx.Fmt.Integer(d.ManagerBonus));
            Row(vm, ctx, "why.businessWorth.multiple", ctx.Strings.Get("exit.times", "n", ctx.Fmt.Decimal1(w.Multiple)), "min", ctx.Fmt.Integer(d.MinMultiple.RoundToLong()), "max", ctx.Fmt.Integer(d.MaxMultiple.RoundToLong()));
            Row(vm, ctx, "why.businessWorth.earns", Money(ctx, Sim.Core.Finance.Money.MulRate(w.Profit, w.Multiple)));
            Row(vm, ctx, "why.businessWorth.holds", Money(ctx, w.Book));
            vm.Footnote = ctx.Strings.Get("why.businessWorth.foot");
            return vm;
        }

        private static WhyVm BusinessHoldsWhy(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessHolds.title", b == null ? "" : Money(ctx, ExitRules.Book(b)));
            Text(vm, ctx, "why.businessHolds.assets");
            Text(vm, ctx, "why.businessHolds.debts");
            vm.Footnote = ctx.Strings.Get("why.businessHolds.foot");
            return vm;
        }

        private static WhyVm BusinessSaleWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.businessSale.title", "");
            Row(vm, ctx, "why.businessSale.buyer", ctx.Strings.Get("why.businessSale.buyerValue", "lo", Pct(ctx, d.BuyerLow), "hi", Pct(ctx, d.BuyerHigh)));
            Row(vm, ctx, "why.businessSale.days", ctx.Strings.Get("exit.days", "min", ctx.Fmt.Integer(d.SaleDaysMin), "max", ctx.Fmt.Integer(d.SaleDaysMax)));
            Row(vm, ctx, "why.businessSale.rushed", Pct(ctx, d.RushedShare), "n", ctx.Fmt.Integer(d.RushedDays));
            Text(vm, ctx, "why.businessSale.floor");
            Text(vm, ctx, "why.businessSale.goes");
            Text(vm, ctx, "why.businessSale.tax");
            vm.Footnote = ctx.Strings.Get("why.businessSale.foot");
            return vm;
        }

        private static WhyVm BusinessBranchWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.businessBranch.title", ctx.Fmt.Integer(d.MaxBranches));
            Text(vm, ctx, "why.businessBranch.what");
            Text(vm, ctx, "why.businessBranch.books");
            Text(vm, ctx, "why.businessBranch.limit", "max", ctx.Fmt.Integer(d.MaxBranches));
            vm.Footnote = ctx.Strings.Get("why.businessBranch.foot");
            return vm;
        }
    }
}
