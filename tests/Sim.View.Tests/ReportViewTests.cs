using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Finance;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-7: the season report's lines, labels and Why? keys in §3.8's order, with the report's amounts.</summary>
    public class ReportViewTests
    {
        [Test]
        public void Labels_AndWhy()
        {
            Simulation sim = Situations.Later(out _);
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport r = sim.State.Finance.Reports.Last();
            SheetVm sheet = ReportSheet.Build(ctx, r.Period);
            var keys = sheet.Lines.Select(l => l.WhyKey).ToList();
            Assert.That(keys.Take(8), Is.EqualTo(ReportSheet.Order.Select(k => "report." + k)));
            Assert.That(keys.Skip(8), Is.EqualTo(new[] { "report.cashFlow", "report.netWorth" }));
            for (int i = 0; i < 8; i++)
            {
                ReportLine l = r.Line(ReportSheet.Order[i]);
                Assert.That(sheet.Lines[i].Label, Is.EqualTo(ctx.Strings.Get(l.LabelKey)));
                Assert.That(sheet.Lines[i].Value, Is.EqualTo(ctx.Fmt.Coins(l.Amount)));
                Assert.That(sheet.Lines[i].WhyId, Is.EqualTo(r.Period));
            }
            Assert.That(sheet.Lines.Single(l => l.WhyKey == "report.netWorth").Value, Is.EqualTo(ctx.Fmt.Coins(r.NetWorth.Total)));
            if (r.HomeUse > 0)
                Assert.That(WhyView.Build(ctx, "report.farmIncome", r.Period).Rows.Any(x => x.Label.StartsWith("Home-grown food paid")), Is.True, "the home-use note");
        }
    }
}
