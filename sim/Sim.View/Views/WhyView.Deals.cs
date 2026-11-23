using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Investors, partners and buy-outs (spec 028 §3.4): why an offer is what it is, who owns a business, what a share is paid and what buying it back costs.</summary>
    public static partial class WhyView
    {
        private static WhyVm InvestorOfferWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.investorOffer.title", "");
            Text(vm, ctx, "why.investorOffer.what");
            Row(vm, ctx, "why.investorOffer.sizes", Pct(ctx, d.InvestorShares[0]) + " – " + Pct(ctx, d.InvestorShares[d.InvestorShares.Count - 1]));
            Row(vm, ctx, "why.investorOffer.min", Money(ctx, d.InvestorValueMin));
            Text(vm, ctx, "why.investorOffer.when");
            Row(vm, ctx, "why.investorOffer.cap", Pct(ctx, d.MaxOutside));
            vm.Footnote = ctx.Strings.Get("why.investorOffer.foot", "hours", ctx.Fmt.Integer(d.OfferHours));
            return vm;
        }

        private static WhyVm PartnerOfferWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.partnerOffer.title", Pct(ctx, d.PartnerProfit));
            Text(vm, ctx, "why.partnerOffer.what");
            Row(vm, ctx, "why.partnerOffer.stake", Pct(ctx, d.PartnerStake));
            Row(vm, ctx, "why.partnerOffer.profit", Pct(ctx, d.PartnerProfit));
            Text(vm, ctx, "why.partnerOffer.noPrices");
            Row(vm, ctx, "why.partnerOffer.cap", Pct(ctx, d.MaxOutside));
            vm.Footnote = ctx.Strings.Get("why.partnerOffer.foot");
            return vm;
        }

        private static WhyVm DealBuyoutWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.dealBuyout.title", "");
            Row(vm, ctx, "why.dealBuyout.range", Pct(ctx, d.BuyOutLow) + " – " + Pct(ctx, d.BuyOutHigh));
            Text(vm, ctx, "why.dealBuyout.floor");
            Row(vm, ctx, "why.dealBuyout.open", ctx.Fmt.Integer(d.BuyOutHours) + " / " + ctx.Fmt.Integer(d.BuyOutBigHours), "big", Pct(ctx, d.BuyOutBigShare));
            Row(vm, ctx, "why.dealBuyout.gap", ctx.Fmt.Integer(d.BuyOutGapSeasons));
            Text(vm, ctx, "why.dealBuyout.sale");
            vm.Footnote = ctx.Strings.Get("why.dealBuyout.foot");
            return vm;
        }

        private static WhyVm OwnerRowWhy(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.ownerRow.title", b == null ? "" : OwnerSheet.Split(ctx, b));
            Text(vm, ctx, "why.ownerRow.what");
            Text(vm, ctx, "why.ownerRow.books");
            vm.Footnote = ctx.Strings.Get("why.ownerRow.foot");
            return vm;
        }

        private static WhyVm OwnerPaidWhy(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.ownerPaid.title", "");
            Text(vm, ctx, "why.ownerPaid.what");
            Text(vm, ctx, "why.ownerPaid.after");
            Text(vm, ctx, "why.ownerPaid.loss");
            vm.Footnote = ctx.Strings.Get("why.ownerPaid.foot");
            return vm;
        }

        private static WhyVm OutsideLimitWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.outsideLimit.title", Pct(ctx, d.MaxOutside));
            Text(vm, ctx, "why.outsideLimit.what");
            Text(vm, ctx, "why.outsideLimit.each");
            vm.Footnote = ctx.Strings.Get("why.outsideLimit.foot");
            return vm;
        }

        private static WhyVm ShareBackWhy(ViewContext ctx, long id)
        {
            ExitDef d = ExitRules.Defs(ctx.Sim);
            WhyVm vm = New(ctx, "why.shareBack.title", "");
            Text(vm, ctx, "why.shareBack.price");
            Row(vm, ctx, "why.shareBack.wait", ctx.Fmt.Integer(d.BuyBackSeasons));
            Text(vm, ctx, "why.shareBack.partner");
            Text(vm, ctx, "why.shareBack.sale");
            vm.Footnote = ctx.Strings.Get("why.shareBack.foot");
            return vm;
        }

        private static WhyVm DealOfferWhy(ViewContext ctx, long id)
        {
            WhyVm vm = New(ctx, "why.dealOffer.title", "");
            Text(vm, ctx, "why.dealOffer.what");
            Text(vm, ctx, "why.dealOffer.wait");
            vm.Footnote = ctx.Strings.Get("why.dealOffer.foot");
            return vm;
        }
    }
}
