using System.Collections.Generic;
using Sim.Core.Finance;

namespace Sim.View.Views
{
    /// <summary>The hub's <em>Why?</em>s (spec 019 §3.2): each number with the figures it is made of.</summary>
    public static partial class WhyView
    {
        private static string SignedPct(ViewContext ctx, long now, long then)
        {
            if (then <= 0) return ctx.Fmt.Integer(0) + "%";
            long pct = (now - then) * 100 / then; // towards zero: a rise never looks bigger than it is
            return (pct > 0 ? "+" : pct < 0 ? "−" : "") + ctx.Fmt.Integer(System.Math.Abs(pct)) + "%";
        }

        private static WhyVm HubWhy(ViewContext ctx, int row)
        {
            var sim = ctx.Sim;
            var s = ctx.Strings;
            NetWorthSnapshot worth = Reporting.NetWorth(sim);
            WhyVm vm;
            switch (row)
            {
                case HubSheet.Trend:
                {
                    vm = New(ctx, "hub.why.trend", Money(ctx, worth.Total));
                    List<TrendPoint> points = Hub.Trend(sim, HubSheet.TrendRows);
                    foreach (TrendPoint p in points)
                        vm.Rows.Add(new WhyRowVm(ReportSheet.PeriodName(ctx, p.Period), Money(ctx, p.InTodaysCoins) + " · " + s.Get("hub.why.trendRow", "counted", Money(ctx, p.AsCounted))));
                    Row(vm, ctx, "hub.why.trendNow", Money(ctx, worth.Total));
                    if (points.Count > 0)
                    {
                        TrendPoint first = points[0];
                        Sim.Core.Maths.Fixed prices = first.PricesSince;
                        Text(vm, ctx, "hub.why.trendChange", "coins", SignedPct(ctx, worth.Total, first.InTodaysCoins), "counted", SignedPct(ctx, worth.Total, first.AsCounted),
                            "prices", SignedPct(ctx, prices.Raw, Sim.Core.Maths.Fixed.One.Raw));
                    }
                    Text(vm, ctx, "hub.why.trendText");
                    return vm;
                }
                case HubSheet.Made:
                    vm = New(ctx, "hub.why.made", Money(ctx, Hub.Owned(worth)));
                    foreach (KeyValuePair<string, long> c in HubSheet.Classes(worth)) vm.Rows.Add(new WhyRowVm(s.Get("hub.class." + c.Key), Money(ctx, c.Value)));
                    Text(vm, ctx, "hub.why.madeText");
                    return vm;
                case HubSheet.Debt:
                {
                    vm = New(ctx, "hub.why.debt", HubSheet.Pct(ctx, Hub.DebtPercent(worth)));
                    Row(vm, ctx, "hub.why.debtOwe", Money(ctx, Hub.Owed(worth)));
                    Row(vm, ctx, "hub.why.debtOwn", Money(ctx, Hub.Owned(worth)));
                    Sim.Core.Definitions.LedgerDef d = sim.Config.Game.Ledger;
                    Text(vm, ctx, "hub.why.debtText", "comfortable", ctx.Fmt.Percent(d.DebtComfortableUnder), "risky", ctx.Fmt.Percent(d.DebtRiskyOver));
                    return vm;
                }
                case HubSheet.Emergency:
                {
                    long tenths = Hub.EmergencyTenths(sim);
                    vm = New(ctx, "hub.why.emergency", tenths < 0 ? s.Get("hub.emergencyNothing") : s.Get("hub.emergencyValue", "n", HubSheet.Tenths(ctx, tenths)));
                    Row(vm, ctx, "hub.why.emergencyCash", Money(ctx, worth.Cash));
                    if (worth.Deposit > 0) Row(vm, ctx, "hub.why.emergencySavings", Money(ctx, worth.Deposit));
                    Row(vm, ctx, "hub.why.emergencyDue", Money(ctx, Hub.NextCloseTotal(sim)));
                    Text(vm, ctx, "hub.why.emergencyText");
                    return vm;
                }
                case HubSheet.Savings:
                {
                    vm = New(ctx, "hub.why.savings", Money(ctx, SavingsRules.Balance(sim)));
                    Row(vm, ctx, "hub.why.savingsRate", ctx.Fmt.Percent(sim.Config.Game.Ledger.SavingsInterest));
                    Row(vm, ctx, "hub.why.savingsNext", Money(ctx, SavingsRules.NextInterest(sim)));
                    Text(vm, ctx, "hub.why.savingsText");
                    return vm;
                }
                case HubSheet.Prices:
                {
                    int rose = Hub.PricesRose(sim, out int seasons);
                    vm = New(ctx, "hub.why.prices", seasons == 0 ? s.Get("savings.pricesNone") : s.Get("savings.pricesValue", "pct", ctx.Fmt.Integer(rose), "seasons", s.Plural("why.seasons", seasons)));
                    Row(vm, ctx, "hub.why.pricesDeposit", ctx.Fmt.Percent(sim.Config.Game.Ledger.SavingsInterest));
                    Text(vm, ctx, "hub.why.pricesText");
                    return vm;
                }
                case HubSheet.Standing:
                    vm = New(ctx, "hub.why.standing", SavingsRules.Always(sim) ? s.Get("savings.standing.always") : s.Get("savings.standing.ask"));
                    Text(vm, ctx, "hub.why.standingText");
                    return vm;
                default:
                {
                    vm = New(ctx, "hub.why.passive", s.Get("hub.passiveValue", "pct", ctx.Fmt.Integer(Hub.PassivePercent(sim))));
                    if (sim.State.Finance.Reports.Count == 0) { Text(vm, ctx, "hub.why.passiveNone"); return vm; }
                    long total = Hub.Passive(sim, out long rent, out long interest, out long business);
                    Row(vm, ctx, "hub.why.passiveRent", Money(ctx, rent));
                    if (interest != 0) Row(vm, ctx, "hub.why.passiveInterest", Money(ctx, interest));
                    if (business != 0) Row(vm, ctx, "hub.why.passiveBusiness", MoneySheet.Signed(ctx, business));
                    Row(vm, ctx, "hub.why.passiveLiving", Money(ctx, Hub.LivingPerSeason(sim)));
                    Text(vm, ctx, "hub.why.passiveText");
                    return vm;
                }
            }
        }
    }
}
