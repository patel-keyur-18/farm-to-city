using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Finance;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 019 AC-3, AC-4, AC-8, AC-10 (US-137, ADR 0101 §4–§5): the season page's three statements, last year beside each line, Your books, Your year, Your people.</summary>
    public class StatementViewTests
    {
        /// <summary>The Hamlet town (a hand, a family house) left to run for ten seasons.</summary>
        private static Simulation Years()
        {
            Simulation sim = Situations.Town();
            sim.AdvanceTo(sim.State.Clock.SimTime + 10 * sim.Clock.SecondsPerSeason, Sim.Core.Time.AdvanceMode.Away);
            Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThanOrEqualTo(9));
            return sim;
        }

        // AC-3: Your books lists the last 8 closed seasons, newest first, never this season so far, each opening its season page's three statements.
        [Test]
        public void PastSeasons()
        {
            Simulation sim = Years();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm books = BooksSheet.Build(ctx);
            var reports = sim.State.Finance.Reports;
            Assert.That(books.Lines.Count, Is.EqualTo(BooksSheet.Shown));
            Assert.That(books.Lines.Select(l => l.Action.Opens.Value.Id), Is.EqualTo(reports.AsEnumerable().Reverse().Take(BooksSheet.Shown).Select(r => r.Period)));
            Assert.That(books.Lines.Select(l => l.Label), Has.None.Contains("so far"));
            foreach (LineVm l in books.Lines)
                foreach (string tab in new[] { StatementSheet.ProfitTab, StatementSheet.CashTab, StatementSheet.BalanceTab })
                {
                    SheetVm page = ReportSheet.Build(ctx, l.Action.Opens.Value.Id, tab);
                    Assert.That(page.Lines.Count, Is.GreaterThan(2), l.Label + " " + tab);
                    Assert.That(page.Tab, Is.EqualTo("tab:" + tab));
                }
            Assert.That(MoneySheet.Build(ctx, MoneySheet.Wealth).Lines.Any(l => l.Buttons != null && l.Buttons.Any(b => b.Opens.Value.Kind == "Books")), Is.True, "Your wealth opens Your books");
        }

        // The cash flow tab: opening cash, every row by group, and closing cash, adding up to the coin; each row's Why? lists its movements.
        [Test]
        public void CashFlow_Page()
        {
            Simulation sim = Years();
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport r = sim.State.Finance.Reports.Last(x => x.CashKept);
            SheetVm page = ReportSheet.Build(ctx, r.Period, StatementSheet.CashTab);
            Assert.That(page.Lines.First().Value, Is.EqualTo(ctx.Fmt.Coins(r.CashFlow.Opening)));
            Assert.That(page.Lines.Last().Value, Is.EqualTo(ctx.Fmt.Coins(r.CashFlow.Closing)));
            Assert.That(page.Lines.Count(l => l.WhyKey == "report.cashrow"), Is.EqualTo(r.CashRows.Count));
            for (int i = 0; i < r.CashRows.Count; i++)
            {
                WhyVm why = WhyView.Build(ctx, "report.cashrow", StatementSheet.WhyId(r.Period, i));
                Assert.That(why.Rows, Is.Not.Empty);
                Assert.That(why.Value, Is.EqualTo(StatementSheet.Amount(ctx, r.CashRows[i].Amount)));
            }
            Assert.That(page.Lines.Where(l => l.IsHeader).Select(l => l.Label), Has.None.Contain(" 0 coins"), "no heading over nothing");
            SeasonReport old = sim.State.Finance.Reports.First();
            SheetVm oldPage = ReportSheet.Build(ctx, old.Period, StatementSheet.CashTab);
            Assert.That(old.CashKept, Is.True, "a town made on this schema keeps every season's rows");
            Assert.That(oldPage.Lines.Count, Is.GreaterThan(2));
        }

        // The balance sheet: what you own less what you owe is net worth.
        [Test]
        public void BalanceSheet()
        {
            Simulation sim = Years();
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (SeasonReport r in sim.State.Finance.Reports)
            {
                NetWorthSnapshot n = r.NetWorth;
                Assert.That(StatementSheet.Item(n, "own") - StatementSheet.Item(n, "owe"), Is.EqualTo(n.Total), "season " + r.Period);
                SheetVm page = ReportSheet.Build(ctx, r.Period, StatementSheet.BalanceTab);
                Assert.That(page.Lines.Single(l => l.WhyKey == "report.netWorth").Value, Is.EqualTo(ctx.Fmt.Coins(n.Total)));
            }
        }

        // AC-4: each line sits beside the same season last year, "First <season>" until a year has passed.
        [Test]
        public void LastYear()
        {
            Simulation sim = Years();
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport first = sim.State.Finance.Reports.First(), later = sim.State.Finance.Reports.First(x => StatementSheet.LastYear(sim, x.Period) != null);
            SheetVm a = ReportSheet.Build(ctx, first.Period);
            Assert.That(a.Lines.Where(l => l.WhyKey == "report.acct").Select(l => l.Note), Has.All.StartWith("First"));
            SheetVm b = ReportSheet.Build(ctx, later.Period);
            SeasonReport year = StatementSheet.LastYear(sim, later.Period);
            LineVm net = b.Lines.Single(l => l.WhyKey == "report.netResult");
            Assert.That(net.Note, Is.EqualTo(ctx.Strings.Get("stmt.last", "season", StatementSheet.SeasonName(ctx, later.Period), "amount", StatementSheet.Amount(ctx, year.NetResult))));
            WhyVm why = WhyView.Build(ctx, "report.netResult", later.Period);
            Assert.That(why.Rows.Any(x => x.Label.StartsWith("The biggest change") || x.Label.StartsWith("Nothing moved")), Is.True, "its Why? names the biggest change");
            Assert.That(ReportSheet.Build(ctx, later.Period, StatementSheet.BalanceTab).Lines.Single(l => l.WhyKey == "report.netWorth").Note, Does.StartWith("Last"));
        }

        // AC-8: after each Winter close Your year sets the year beside last year's.
        [Test]
        public void YearReview()
        {
            Simulation sim = Years();
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport second = sim.State.Finance.Reports.Where(r => YearSheet.EndsYear(r.Period)).First(r => YearSheet.Seasons(sim, r.Period - 4).Count == 4);
            SheetVm vm = YearSheet.Build(ctx, second.Period);
            long profit = YearSheet.Seasons(sim, second.Period).Sum(r => r.FarmProfit), before = YearSheet.Seasons(sim, second.Period - 4).Sum(r => r.FarmProfit);
            LineVm line = vm.Lines.First();
            Assert.That(line.Value, Is.EqualTo(StatementSheet.Amount(ctx, profit)));
            Assert.That(line.Note, Is.EqualTo(ctx.Strings.Get("year.lastYear", "amount", StatementSheet.Amount(ctx, before))));
            Assert.That(vm.Lines.Select(l => l.Label), Has.Some.EqualTo(ctx.Strings.Get("year.netWorth")));
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null)) Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId).Rows, Is.Not.Empty, l.Label);
            Assert.That(YearSheet.EndsYear(second.Period), Is.True);
            Assert.That(YearSheet.EndsYear(second.Period + 1), Is.False);
            SeasonReport firstWinter = sim.State.Finance.Reports.First(r => YearSheet.EndsYear(r.Period));
            Assert.That(YearSheet.Build(ctx, firstWinter.Period).Lines.First().Note, Is.EqualTo(ctx.Strings.Get("year.firstYear")));
        }

        // AC-10: Your people lists wages against what they'd ask, who left and why, and the employer's name.
        [Test]
        public void YourPeople()
        {
            Simulation sim = Years();
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport r = sim.State.Finance.Reports.First(x => x.People.Count > 0);
            PeopleRow p = r.People[0];
            SheetVm page = ReportSheet.Build(ctx, r.Period);
            int head = page.Lines.FindIndex(l => l.IsHeader && l.Label == ctx.Strings.Get("stmt.people"));
            Assert.That(head, Is.GreaterThan(0), "Your people ends the profit and loss");
            LineVm row = page.Lines[head + 1];
            Assert.That(row.Label, Does.StartWith(PeopleWords.Name(ctx, p.Name)));
            WhyVm why = WhyView.Build(ctx, row.WhyKey, row.WhyId);
            Assert.That(why.Rows.Any(x => x.Value == ctx.Fmt.Coins(p.Asked)) || p.Outcome >= StaffOutcome.LeftForElsewhere, Is.True);
            Assert.That(page.Lines.Last().WhyKey, Is.EqualTo("report.people"), "the employer's name last");
            Assert.That(WhyView.Build(ctx, "report.people", StatementSheet.WhyId(r.Period, 999)).Rows, Is.Not.Empty);
        }

        // Every cash movement can be worded: a label for each account in each direction, and a P&L name for every income and expense account.
        [Test]
        public void EveryRowHasWords()
        {
            var table = ViewKit.Strings();
            foreach (Account a in Accounts.All)
            {
                if (a == Account.Cash) continue;
                foreach (string dir in new[] { "in", "out" }) Assert.That(table.Has("cash.row." + (int)a + "." + dir), Is.True, a + " " + dir);
                AccountGroup g = Accounts.GroupOf(a);
                if (g == AccountGroup.Income || g == AccountGroup.Expenses) Assert.That(table.Has("account." + a), Is.True, a.ToString());
            }
        }

        // AC-9 (ADR 0101 §4): a schema-15 town's past seasons open at once; their cash flow says it is kept from the next season on, and
        // the season after a kept one says last year's wasn't itemised.
        [Test]
        public void OlderTown_CashFlowKeptFromNext()
        {
            string file = System.IO.Path.Combine(ViewKit.RepoRoot, "tests", "Sim.Core.Tests", "saves", "schema15.ftc");
            var read = Sim.Core.Saves.SaveCodec.Read(System.IO.File.ReadAllBytes(file), ViewKit.Config(), 3);
            Assert.That(read.Error, Is.EqualTo(Sim.Core.Saves.SaveError.None), read.Detail);
            Simulation sim = Simulation.FromState(ViewKit.Config(), read.State);
            ViewContext ctx = ViewKit2.Context(sim);
            SeasonReport old = sim.State.Finance.Reports.Last();
            SheetVm page = ReportSheet.Build(ctx, old.Period, StatementSheet.CashTab);
            Assert.That(page.Lines.Select(l => l.Label), Has.Some.EqualTo(ctx.Strings.Get("stmt.cash.notKept")));
            Assert.That(page.Lines.Last().Value, Is.EqualTo(ctx.Fmt.Coins(old.CashFlow.Closing)), "its cash summary stays");
            Assert.That(ReportSheet.Build(ctx, old.Period, StatementSheet.BalanceTab).Lines.Count, Is.GreaterThan(3), "its balance sheet opens");
            Assert.That(ReportSheet.Build(ctx, old.Period).Lines.Count, Is.GreaterThan(3), "its profit and loss opens");
            sim.AdvanceTo(sim.State.Clock.SimTime + 5 * sim.Clock.SecondsPerSeason, Sim.Core.Time.AdvanceMode.Away);
            SeasonReport kept = sim.State.Finance.Reports.First(x => x.CashKept);
            SeasonReport yearOn = sim.State.Finance.Reports.FirstOrDefault(x => x.CashKept && StatementSheet.LastYear(sim, x.Period) != null && !StatementSheet.LastYear(sim, x.Period).CashKept);
            if (yearOn != null)
                Assert.That(ReportSheet.Build(ctx, yearOn.Period, StatementSheet.CashTab).Lines.Where(l => l.Note != null).Select(l => l.Note), Has.Some.Contain("not itemised"));
            Assert.That(kept.CashRows.Count, Is.GreaterThan(0));
        }
    }
}
