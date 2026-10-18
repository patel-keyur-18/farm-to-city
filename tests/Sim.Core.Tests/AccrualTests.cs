using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class AccrualTests
    {
        private const long S = 4 * Day;

        // Spec 005 AC-6.
        [Test]
        public void Prepaid_ExpensedDaily_Exact()
        {
            Simulation sim = NewFarm(game: Quiet());
            Ledger l = sim.State.Ledger;
            Prepayment p = Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, 36_000_00, 0, S, "test:wages");
            Assert.That(l.Balance(Account.PrepaidWages), Is.EqualTo(36_000_00));
            for (int d = 1; d <= 4; d++)
            {
                sim.AdvanceTo(d * Day - 1);
                long before = ExpensedSoFar(sim, Account.HiredLabour);
                sim.AdvanceTo(d * Day);
                Assert.That(ExpensedSoFar(sim, Account.HiredLabour) - before, Is.EqualTo(9_000_00), "day " + d);
            }
            Assert.That(l.Balance(Account.PrepaidWages), Is.EqualTo(0));
            Assert.That(sim.State.Finance.Prepayments, Is.Empty);

            sim = NewFarm(game: Quiet());
            Accrual.Open(sim, Account.LivingCosts, Account.PrepaidLivingCosts, 10_000_01, 0, S, "test:odd");
            var steps = new long[4];
            for (int d = 1; d <= 4; d++)
            {
                long before = ExpensedSoFar(sim, Account.LivingCosts);
                sim.AdvanceTo(d * Day);
                steps[d - 1] = ExpensedSoFar(sim, Account.LivingCosts) - before;
            }
            Assert.That(steps, Is.EqualTo(new long[] { 2_500_00, 2_500_00, 2_500_00, 2_500_01 }));
        }

        // Spec 005 AC-7.
        [Test]
        public void Prepaid_SpansSeasonClose()
        {
            Simulation sim = NewFarm(game: Quiet());
            sim.AdvanceTo(2 * Day);
            Accrual.Open(sim, Account.Rent, Account.PrepaidRent, 36_000_00, 2 * Day, 6 * Day, "test:rent");
            sim.AdvanceTo(S);
            SeasonReport r = sim.State.Finance.Reports.Single();
            Assert.That(r.ByAccount.Single(kv => kv.Key == Account.Rent).Value, Is.EqualTo(-18_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.PrepaidRent), Is.EqualTo(18_000_00));
        }

        // Spec 005 AC-8.
        [Test]
        public void Prepaid_EarlyClose_RefundsUnearned()
        {
            Simulation sim = NewFarm(game: Quiet());
            Prepayment p = Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, 36_000_00, 0, S, "test:wages");
            sim.AdvanceTo(Day + Day / 2);
            long cash = sim.State.Ledger.Cash;
            Assert.That(Accrual.RefundIfClosed(sim, p.Id), Is.EqualTo(22_500_00));
            Assert.That(Accrual.Close(sim, p.Id), Is.EqualTo(22_500_00));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(22_500_00));
            Assert.That(ExpensedSoFar(sim, Account.HiredLabour), Is.EqualTo(13_500_00));
            Assert.That(sim.State.Ledger.Balance(Account.PrepaidWages), Is.EqualTo(0));
            Assert.That(sim.State.Finance.Prepayments, Is.Empty);
        }

        /// <summary>Everything expensed to the account so far (the close moves balances to retained earnings, so sum the entries).</summary>
        private static long ExpensedSoFar(Simulation sim, Account a)
        {
            return sim.State.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.expensed" || e.DescriptionKey == "ledger.prepaidRefund")
                .SelectMany(e => e.Postings).Where(p => p.Account == a).Sum(p => p.Amount);
        }
    }

    public class SeasonCloseTests
    {
        private const long S = 4 * Day;

        // Spec 005 AC-9.
        [Test]
        public void CutOff()
        {
            Simulation sim = NewFarm(game: Quiet());
            Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, 36_000_00, 0, S, "test:before");
            sim.AdvanceTo(S);
            // A payment made at the boundary, after the close: the new season.
            Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, 36_000_00, S, 2 * S, "test:after");

            JournalEntry[] atBoundary = sim.State.Ledger.Entries.Where(e => e.SimTime == S).ToArray();
            Assert.That(atBoundary.Where(e => e.DescriptionKey == "ledger.expensed").Select(e => e.Period), Is.All.EqualTo(0L));
            Assert.That(atBoundary.Where(e => e.DescriptionKey == "ledger.depreciation").Select(e => e.Period), Is.All.EqualTo(0L));
            Assert.That(atBoundary.Single(e => e.DescriptionKey == "ledger.closeSeason").Period, Is.EqualTo(0));
            Assert.That(atBoundary.Single(e => e.Reference == "test:after").Period, Is.EqualTo(1));
            Assert.That(sim.State.Finance.Reports[0].ByAccount.Single(kv => kv.Key == Account.HiredLabour).Value, Is.EqualTo(-36_000_00));
        }

        // Spec 005 AC-10.
        [Test]
        public void PnL_ClosesToRetainedEarnings()
        {
            Simulation sim = NewFarm(game: Quiet());
            Ledger l = sim.State.Ledger;
            l.Post(10, 0, "test.sale", null, new Posting(Account.Cash, 18_400_00), new Posting(Account.CropSales, -18_400_00));
            l.Post(11, 0, "test.seed", null, new Posting(Account.SeedsAndPlanting, 3_200_00), new Posting(Account.Cash, -3_200_00));
            l.Post(12, 0, "test.dayLabour", null, new Posting(Account.Cash, 1_600_00), new Posting(Account.DayLabourEarnings, -1_600_00));
            l.Post(13, 0, "test.living", null, new Posting(Account.LivingCosts, 15_000_00), new Posting(Account.Cash, -15_000_00));
            long retained = l.Balance(Account.RetainedEarnings);
            sim.AdvanceTo(S);

            SeasonReport r = sim.State.Finance.Reports.Single();
            Assert.That(r.Period, Is.EqualTo(0));
            Assert.That(r.Line("farmIncome").Amount, Is.EqualTo(18_400_00));
            Assert.That(r.Line("farmCosts").Amount, Is.EqualTo(-3_200_00 - 625_00)); // seed, depreciation (hut 500, well 125)
            Assert.That(r.FarmProfit, Is.EqualTo(18_400_00 - 3_825_00));
            Assert.That(r.Line("otherIncome").Amount, Is.EqualTo(1_600_00));
            Assert.That(r.Line("household").Amount, Is.EqualTo(-15_000_00));
            Assert.That(r.Line("finance").Amount, Is.EqualTo(0));
            Assert.That(r.NetResult, Is.EqualTo(18_400_00 - 3_825_00 + 1_600_00 - 15_000_00));
            Assert.That(r.Line("farmProfit").LabelKey, Is.EqualTo("report.farmProfit"));
            Assert.That(r.Line("farmProfit").WhyKey, Is.EqualTo("report.farmProfit.why"));
            Assert.That(r.Line("farmCosts").Accounts, Does.Contain(Account.Depreciation));

            Assert.That(-(l.Balance(Account.RetainedEarnings) - retained), Is.EqualTo(r.NetResult));
            foreach (Account a in Accounts.All)
            {
                AccountGroup g = Accounts.GroupOf(a);
                if (g == AccountGroup.Income || g == AccountGroup.Expenses) Assert.That(l.Balance(a), Is.EqualTo(0), a.ToString());
            }
            Assert.That(sim.DrainEvents().OfType<SeasonClosed>().Single().Report, Is.SameAs(r));
        }

        // Spec 005 AC-11.
        [Test]
        public void CashFlow_ProfitIsNotCash()
        {
            Simulation sim = NewFarm(game: Quiet());
            long opening = sim.State.Ledger.Cash;
            sim.AdvanceTo(Day);
            sim.State.Ledger.Post(Day, 0, "test.sale", null, new Posting(Account.Cash, 5_000_00), new Posting(Account.CropSales, -5_000_00));
            Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, 36_000_00, S, 2 * S, "test:nextSeason");
            sim.AdvanceTo(S);

            CashFlowSummary c = sim.State.Finance.Reports.Single().CashFlow;
            Assert.That(c.Opening, Is.EqualTo(opening));
            Assert.That(c.Opening + c.Inflows - c.Outflows, Is.EqualTo(c.Closing));
            Assert.That(c.Closing, Is.EqualTo(sim.State.Ledger.Cash));
            Assert.That(c.Inflows, Is.EqualTo(5_000_00));
            Assert.That(c.Outflows, Is.EqualTo(36_000_00));
            Assert.That(c.PaidUpfront, Is.EqualTo(36_000_00));
            Assert.That(sim.State.Finance.Reports[0].ByAccount.Any(kv => kv.Key == Account.HiredLabour), Is.False);
        }

        // Spec 005 AC-12.
        [Test]
        public void Depreciation_StraightLine()
        {
            Simulation sim = NewFarm(game: Quiet());
            sim.AdvanceTo(S);
            Assert.That(sim.State.Finance.Reports[0].ByAccount.Single(kv => kv.Key == Account.Depreciation).Value, Is.EqualTo(-625_00));
            Assert.That(sim.State.Land.Buildings[0].Depreciated, Is.EqualTo(500_00));
            Assert.That(sim.State.Land.Buildings[1].Depreciated, Is.EqualTo(125_00));

            // A cost that doesn't divide evenly: 1,000.01 over 3 seasons is 333.33, 333.33, then 333.35, then nothing.
            sim = NewFarm(game: Quiet((f, j) => f == "buildings.json"
                ? j.Replace("\"value\": 15000, \"usefulLifeSeasons\": 120", "\"value\": 1000.01, \"usefulLifeSeasons\": 3") : j));
            var steps = new long[5];
            for (int k = 1; k <= 5; k++)
            {
                long before = sim.State.Land.Buildings[1].Depreciated;
                sim.AdvanceTo(k * S);
                steps[k - 1] = sim.State.Land.Buildings[1].Depreciated - before;
            }
            Assert.That(steps, Is.EqualTo(new long[] { 333_33, 333_33, 333_35, 0, 0 }));
            Assert.That(sim.State.Land.Buildings[1].BookValue, Is.EqualTo(0));
        }
    }

    public class NetWorthTests
    {
        // Spec 005 AC-14 (trees join with US-047 and loans with F05.2).
        [Test]
        public void Composition()
        {
            Simulation sim = NewFarm(game: Quiet());
            Accrual.Open(sim, Account.LivingCosts, Account.PrepaidLivingCosts, 18_000_00, 0, 4 * Day, "test:living");
            Assert.That(sim.Execute(new BuyParcel(0, "n3")).Accepted, Is.True);
            sim.AdvanceTo(4 * Day);

            NetWorthSnapshot n = sim.State.Finance.Reports.Single().NetWorth;
            Ledger l = sim.State.Ledger;
            long land = sim.State.Land.Parcels.Where(p => p.Owned).Sum(p => Land.LandRules.ParcelPrice(sim, p));
            Assert.That(n.Cash, Is.EqualTo(l.Cash));
            Assert.That(n.Prepaid, Is.EqualTo(0)); // fully expensed by the close
            Assert.That(n.LandAtMarket, Is.EqualTo(land));
            Assert.That(n.BuildingsAtBook, Is.EqualTo(55_000_00 - 625_00));
            Assert.That(n.StoredGoods, Is.EqualTo(Market.Store.Value(sim)));
            Assert.That(n.Total, Is.EqualTo(n.Cash + n.Prepaid + n.LandAtMarket + n.BuildingsAtBook + n.TreesAtBook + n.StoredGoods
                - n.LoanPrincipal - n.InterestPayable));

            sim.AdvanceTo(4 * Day + 1);
            Accrual.Open(sim, Account.LivingCosts, Account.PrepaidLivingCosts, 9_000_00, 4 * Day + 1, 8 * Day, "test:prepaid");
            NetWorthSnapshot now = Reporting.NetWorth(sim);
            Assert.That(now.Prepaid, Is.EqualTo(9_000_00));
            Assert.That(now.BookEquity, Is.EqualTo(l.TotalOf(AccountGroup.Assets) - l.TotalOf(AccountGroup.Liabilities)));
        }

        // Spec 005 AC-15.
        [Test]
        public void BuyLand_IsAnAssetSwap()
        {
            Simulation sim = NewFarm(game: Quiet());
            long before = Reporting.NetWorth(sim).Total;
            Assert.That(sim.Execute(new BuyParcel(0, "n3")).Accepted, Is.True);
            Assert.That(Reporting.NetWorth(sim).Total, Is.EqualTo(before));
        }
    }
}
