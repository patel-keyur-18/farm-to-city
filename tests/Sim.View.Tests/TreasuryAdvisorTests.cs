using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Progress;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-139 (spec 021 §3.12, AC-15): the town planner's four notes, on the treasury sheet, one a close.</summary>
    public class TreasuryAdvisorTests
    {
        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static AdvisorNote Said(Simulation sim)
        {
            if (Guide.FirstSessionActive(sim)) Assert.That(sim.Execute(new SkipFirstSession(T(sim))).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonClosed close = new SeasonClosed(T(sim), sim.State.Treasury.Reports.Last().Period, new Sim.Core.Finance.SeasonReport
            {
                Period = sim.State.Treasury.Reports.Last().Period, ByAccount = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Sim.Core.Finance.Account, long>>(),
                Lines = new System.Collections.Generic.List<Sim.Core.Finance.ReportLine> { new Sim.Core.Finance.ReportLine("farmProfit", 0, new Sim.Core.Finance.Account[0]), new Sim.Core.Finance.ReportLine("netResult", 0, new Sim.Core.Finance.Account[0]) }, CashFlow = new Sim.Core.Finance.CashFlowSummary(), NetWorth = new Sim.Core.Finance.NetWorthSnapshot(),
            });
            return AdvisorWatch.Observe(ctx, new SimEvent[] { close }).SingleOrDefault(a => a.Report && sim.Config.Game.Advisors.IsPlanner(sim.Config.Game.Advisors.Notes[sim.Config.Game.Advisors.IndexOf(a.Id)]));
        }

        // AC-15: each note holds at its exact condition, with the town's numbers, and reads whole.
        [Test]
        public void Notes()
        {
            Simulation sim = Situations.Township(); // its first close had the founder's gift in it, which makes up a thin balance
            sim.AdvanceTo((T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + 2 * 3600);
            sim.DrainEvents();
            TreasuryState t = sim.State.Treasury;
            Assert.That(sim.Config.Game.Advisors.Advisors, Does.Contain("planner"));
            for (int d = 0; d < TreasuryState.Departments; d++) { t.OverBudget[d] = 0; t.Unpaid[d] = 0; t.Condition[d] = Fixed.One; }
            t.Ledger.Post(T(sim), sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 9_000_000_00), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.GrantUntied, -9_000_000_00));
            Assert.That(Said(sim), Is.Null, "a town that's fine hears nothing");

            // 1: a department over its budget two closes running.
            t.OverBudget[0] = 1;
            Assert.That(Said(sim), Is.Null, "once is not twice");
            t.OverBudget[0] = 2;
            AdvisorNote a = Said(sim);
            Assert.That((a.Id, a.Numbers[0]), Is.EqualTo(("deptOver", 0L)));
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(AdvisorWords.Text(ctx, a), Does.Contain("Council's office").And.Contain("two seasons running"));
            Assert.That(AdvisorWords.Planner(ctx), Is.Null, "the line shows only once it is said");
            Assert.That(sim.Execute(new HearAdvisors(T(sim), new[] { a })).Accepted, Is.True);
            Assert.That(AdvisorWords.Planner(ctx).Text, Does.Contain("Council's office"));
            Assert.That(AdvisorWords.Report(ctx, a.Key), Is.Null, "never on the founder's own season report");
            t.OverBudget[0] = 0;

            // 2: a service under 70%.
            sim.State.Guidance.Advice.Clear();
            t.Condition[0] = Fixed.Parse("0.69");
            a = Said(sim);
            Assert.That((a.Id, a.Numbers[0], a.Numbers[1]), Is.EqualTo(("serviceLow", 0L, Fixed.Parse("0.69").Raw)));
            Assert.That(AdvisorWords.Text(ctx, a), Does.Contain("69%"));
            t.Condition[0] = Fixed.Parse("0.70");
            Assert.That(Said(sim), Is.Null, "70% is not under 70%");

            // 3: the tied grant at risk: Health has a line and is unpaid or under 50% (no clinic yet, so it can't hold here).
            Assert.That(sim.State.Treasury.Reports.Last().Rows[1].Needs, Is.EqualTo(0), "no clinic until spec 023");
            t.Condition[1] = Fixed.Parse("0.2");
            Assert.That(Said(sim), Is.Null, "no doctor's pay, nothing at risk");

            // 4: the balance under a season's spending, with no tax or gift to make it up.
            long due = TreasuryRules.Forecast(sim).Due;
            t.Condition[1] = Fixed.One;
            Assert.That(due, Is.GreaterThan(0));
            Assert.That(t.Cash, Is.GreaterThan(due));
            t.Ledger.Post(T(sim), sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, -(t.Cash - due / 2)), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.DeptOffice, t.Cash - due / 2));
            sim.State.Guidance.Advice.Clear();
            a = Said(sim);
            Assert.That((a.Id, a.Numbers), Is.EqualTo(("thinBalance", new[] { t.Cash, due })));
            Assert.That(AdvisorWords.Text(ctx, a), Does.Not.Contain("{"));
            AdvisorVm line = AdvisorWords.Line(ctx, a, false);
            Assert.That((line.Portrait, line.Who), Is.EqualTo(("portrait_advisor_planner", "Meena Rao, the district's town planner")));
        }
    }
}
