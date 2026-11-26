using Sim.Core.Business;

namespace Sim.View.Views
{
    /// <summary>The Businesses hub's numbers (spec 030 §3.3): a total adds its rows; a row's profit is its Books line, season so far.</summary>
    public static partial class WhyView
    {
        /// <summary>Profit this season: id 0 the total with a row for each business, otherwise one business's lines.</summary>
        private static WhyVm HubProfit(ViewContext ctx, long id)
        {
            var sim = ctx.Sim;
            if (id == 0)
            {
                WhyVm all = New(ctx, "why.hubProfit.title", Money(ctx, BusinessHub.TotalProfit(sim)));
                foreach (Business b in BusinessHub.Listed(sim))
                    all.Rows.Add(new WhyRowVm(BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, b)), Money(ctx, BusinessHub.Profit(sim, b))));
                Text(all, ctx, "why.hubProfit.each");
                all.Footnote = ctx.Strings.Get("why.hubProfit.lesson");
                return all;
            }
            Business one = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.hubProfit.rowTitle", Money(ctx, BusinessHub.Profit(sim, one)), "name", BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, one)));
            BusinessReport r = BusinessBooks.Report(one, sim.Period);
            long Line(int l) { return BusinessBooksSheet.Amount(r, l); }
            var type = BusinessRules.TypeOf(sim, one);
            Row(vm, ctx, BusinessWords.Key(ctx, "why.books.rowIncome", type), Money(ctx, Line(BusinessBooksSheet.Fees) + Line(BusinessBooksSheet.Sales)));
            Row(vm, ctx, BusinessWords.Key(ctx, "why.books.rowGoods", type), "−" + Money(ctx, Line(BusinessBooksSheet.Goods)));
            Row(vm, ctx, "why.books.rowRunning", "−" + Money(ctx, Line(BusinessBooksSheet.Wages) + Line(BusinessBooksSheet.Power) + Line(BusinessBooksSheet.Repairs)
                + Line(BusinessBooksSheet.Depreciation) + Line(BusinessBooksSheet.Interest) + Line(BusinessBooksSheet.Rent)));
            Text(vm, ctx, "why.hubProfit.row");
            vm.Footnote = ctx.Strings.Get("why.hubProfit.lesson");
            return vm;
        }

        /// <summary>Cash in your businesses: the total, a row for each business's own cash.</summary>
        private static WhyVm HubCash(ViewContext ctx, long id)
        {
            var sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.hubCash.title", Money(ctx, BusinessHub.TotalCash(sim)));
            foreach (Business b in BusinessHub.Listed(sim))
                vm.Rows.Add(new WhyRowVm(BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, b)), Money(ctx, b.Ledger.Cash)));
            Text(vm, ctx, "why.hubCash.own");
            vm.Footnote = ctx.Strings.Get("why.hubCash.lesson");
            return vm;
        }
    }
}
