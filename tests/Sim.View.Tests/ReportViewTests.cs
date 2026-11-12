using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Finance;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-7 and spec 019 §3.1: the season page's profit and loss, its lines and Why? keys in §3.8's order, with the report's amounts.</summary>
    public class ReportViewTests
    {
        [Test]
        public void Labels_AndWhy()
        {
            Simulation sim = Situations.Later(out _);
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport r = sim.State.Finance.Reports.Last();
            SheetVm sheet = ReportSheet.Build(ctx, r.Period);
            string[] order = ReportSheet.Order.Where(k => k != "gifts").ToArray(); // no gifts this season: no line (spec 018, ADR 0095 §5)
            foreach (string key in new[] { "farmProfit", "netResult" })
            {
                ReportLine l = r.Line(key);
                LineVm line = sheet.Lines.Single(x => x.WhyKey == "report." + key);
                Assert.That(line.Label, Is.EqualTo(ctx.Strings.Get(l.LabelKey)));
                Assert.That(line.Value, Is.EqualTo(StatementSheet.Amount(ctx, l.Amount)));
                Assert.That(line.WhyId, Is.EqualTo(r.Period));
            }
            foreach (string key in order.Where(k => r.Line(k).Accounts.Count > 0 && k != "farmProfit"))
            {
                string total = ctx.Fmt.Coins(r.Line(key).Amount);
                Assert.That(sheet.Lines.Any(x => x.IsHeader && x.Label.Contains(ctx.Strings.Get(r.Line(key).LabelKey)) || x.IsHeader && x.Label.Contains(total)), Is.True, key);
            }
            Assert.That(sheet.Tabs.Select(t => t.Id), Is.EqualTo(new[] { "tab:pl", "tab:cash", "tab:balance" }));
            Assert.That(ReportSheet.Build(ctx, r.Period, StatementSheet.BalanceTab).Lines.Single(l => l.WhyKey == "report.netWorth").Value, Is.EqualTo(ctx.Fmt.Coins(r.NetWorth.Total)));
            if (r.HomeUse > 0)
                Assert.That(WhyView.Build(ctx, "report.farmIncome", r.Period).Rows.Any(x => x.Label.StartsWith("Home-grown food paid")), Is.True, "the home-use note");
        }
    }
}
