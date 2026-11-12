using System.Linq;
using NUnit.Framework;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 019 §3.2 (US-135): *Your wealth*'s hub as a sheet.</summary>
    public class HubViewTests
    {
        [Test]
        public void Hub_Rows()
        {
            int towns = 0;
            foreach (var sim in Situations.All())
            {
                ViewContext ctx = ViewKit2.Context(sim);
                SheetVm wealth = MoneySheet.Build(ctx, MoneySheet.Wealth);
                LineVm made = wealth.Lines.Single(l => l.Pie != null);
                Assert.That(made.Pie.Sum(p => p.Percent), Is.InRange(90, 100), "shares are rounded down, never over a whole");
                LineVm trend = wealth.Lines.Single(l => l.Label == "Net worth, in today's coins");
                int closes = System.Math.Min(sim.State.Finance.Reports.Count, HubSheet.TrendRows);
                Assert.That(trend.Trend?.Values.Count ?? 0, Is.EqualTo(closes == 0 ? 0 : closes + 1), "the closes, then now");
                foreach (LineVm l in wealth.Lines.Where(l => l.WhyKey == StatementSheet.HubWhy)) Assert.That(WhyView.Has(l.WhyKey), Is.True);
                towns++;
            }
            Assert.That(towns, Is.GreaterThan(0));
        }
    }
}
