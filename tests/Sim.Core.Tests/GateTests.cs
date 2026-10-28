using System;
using System.Linq;
using NUnit.Framework;
using Tools.Balancer;

namespace Sim.Core.Tests
{
    /// <summary>
    /// The Phase 0 gate checks on the live content (US-015, US-016, ADR 0034 §7). They fail the build when a rule or a
    /// content change opens a money loop or makes the start unfair. `dotnet run --project tools/Tools.Balancer -- gate`
    /// prints the same numbers.
    /// </summary>
    [Category("Balance")]
    public class GateTests
    {
        private static SimConfig Live() { return LiveWith(null); }

        private static SimConfig LiveWith(System.Func<string, string, string> edit)
        {
            return new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game(edit));
        }

        // US-015: no command buys produce or sells inputs.
        [Test]
        public void MoneyLoops_Structural()
        {
            Assert.That(Gate.Structural(Live()), Is.Empty);
        }

        // US-015: command sequences at one instant never raise net worth.
        [Test]
        public void MoneyLoops_ZeroTime()
        {
            Assert.That(Gate.ZeroTimeLoops(Live(), 200, 5, out long worst), Is.Empty);
            Assert.That(worst, Is.LessThanOrEqualTo(0));
        }

        // US-015: the stress bots never beat the competent bot by more than 20% (ADR 0016's rule).
        [Test]
        public void MoneyLoops_StressBots()
        {
            foreach (Gate.StressResult s in Gate.Stress(Live(), 100).Skip(1))
                Assert.That(s.Ratio, Is.LessThanOrEqualTo(1.2), s.Bot);
            Gate.StressResult flip = Gate.Stress(Live(), 100, 16, new System.Func<Bot>[] { () => new LandFlipperBot() })[1];
            Assert.That(flip.Ratio, Is.LessThanOrEqualTo(1.2), "land-flipper over 4 years");
        }

        // US-016: the fresh start (and bounded creation) across 1,000 seeds.
        [Test]
        public void Viability_FreshStart()
        {
            Gate.FreshStartResult r = Gate.FreshStart(Live(), 1000);
            Assert.That(r.SpringProfitable, Is.GreaterThanOrEqualTo(990), "farm profit > 0 in the first Spring");
            Assert.That(r.LivingAlwaysPaid, Is.EqualTo(1000), "living costs paid at every close");
            Assert.That(r.OverCapSeeds, Is.EqualTo(0), "no season's output above its realistic cap");
        }

        // US-016: the broke founder recovers through day labour across 1,000 seeds.
        [Test]
        public void Viability_BrokeFounder()
        {
            Gate.BrokeResult r = Gate.Broke(LiveWith, 1000);
            Assert.That(r.BasicWithinOneSeason, Is.EqualTo(1000));
            Assert.That(r.SeasonOfCostsWithinTwo, Is.GreaterThanOrEqualTo(990));
        }

        // Spec 005 AC-36, ADR 0016, 0038: leverage and strategic default never beat the competent bot by more than 20%.
        [Test]
        public void Stress_Debt()
        {
            var debt = new System.Func<Bot>[] { () => new LeverageBot(), () => new StrategicDefaulterBot() };
            foreach (Gate.StressResult s in Gate.Stress(Live(), 100, 8, debt).Skip(1))
                Assert.That(s.Ratio, Is.LessThanOrEqualTo(1.2), s.Bot);
        }

        // Spec 006 AC-23, ADR 0039 §10: people stress bots stay within 1.20 × competent; the absentee (orders) at least 0.80 ×.
        [Test]
        public void PeopleStress()
        {
            var people = new System.Func<Bot>[] { () => new DayLabourSpammerBot(), () => new HandChurnerBot(), () => new LapseGamerBot(), () => new AbsenteeBot() };
            var results = Gate.Stress(Live(), 100, 8, people);
            foreach (Gate.StressResult s in results.Skip(1))
            {
                TestContext.Out.WriteLine(s.Bot + " " + s.Ratio.ToString("0.000"));
                if (s.Bot == "absentee") Assert.That(s.Ratio, Is.GreaterThanOrEqualTo(0.8), s.Bot);
                else Assert.That(s.Ratio, Is.LessThanOrEqualTo(1.2), s.Bot);
            }
        }

        // Spec 009 AC-15 (ADR 0045): the competent bot unlocks the Hamlet at a median of day 7 ± 2, on the sim's own rule.
        [Test]
        public void HamletPace()
        {
            var days = Gate.HamletDays(Live(), 100);
            var met = days.Where(d => !double.IsNaN(d)).OrderBy(d => d).ToList();
            TestContext.Out.WriteLine("unlocked in " + met.Count + " of 100; median day " + (met.Count == 0 ? "-" : met[met.Count / 2].ToString("0.0")));
            Assert.That(met.Count, Is.GreaterThanOrEqualTo(50), "unlocked in most seeds");
            double median = days.Select(d => double.IsNaN(d) ? double.MaxValue : d).OrderBy(d => d).ElementAt(50);
            Assert.That(median, Is.InRange(5.0, 9.0));
        }

        // Spec 012 §9 (US-203): a buffalo's and a cow's payback over a lactation cycle, all feed bought and home-grown.
        [Test]
        public void Dairy()
        {
            Dairy.Result b = Tools.Balancer.Dairy.Cycle(LiveWith, "buffalo"), c = Tools.Balancer.Dairy.Cycle(LiveWith, "cow");
            foreach (Dairy.Result r in new[] { b, c })
                TestContext.Out.WriteLine(r.Kind + ": net " + (r.NetPerDay / 100).ToString("0") + " a day, home-grown " + (r.HomeNetPerDay / 100).ToString("0")
                    + "; payback " + r.PaybackYears.ToString("0.00") + " years, home-grown " + r.HomePaybackYears.ToString("0.00") + "; milk " + r.Milk / 100 + ", feed " + r.Feed / 100 + ", vet " + r.Vet / 100);
            double acre = 120_000.0 / 7.2 * 100; // an owner-operated acre's net a season (ADR 0033: land pays back in 7.2 seasons at 1,20,000)
            TestContext.Out.WriteLine("buffalo a season vs an acre: " + (b.HomeNetPerDay * 4 / acre).ToString("0.00") + " (home-grown), " + (b.NetPerDay * 4 / acre).ToString("0.00") + " (bought)");
            Assert.That(b.PaybackYears, Is.InRange(2.0, 3.5), "buffalo, all feed bought");
            Assert.That(b.HomePaybackYears, Is.InRange(1.0, 2.0), "buffalo, home-grown green and straw");
            Assert.That(c.HomePaybackYears / b.HomePaybackYears, Is.InRange(0.70, 0.90), "a cow pays back 10–30% faster per coin");
            Assert.That(b.HomeNetPerDay * 4 / acre, Is.InRange(0.7, 1.5), "a buffalo nets about what an owner-operated acre does");
        }

        // Spec 012 §9 (US-203): the competent bot keeps buffalo after the Hamlet; the dairy stress bot (a full shed on the biggest loans,
        // all feed bought) stays within 1.20 × and is never foreclosed in most seeds.
        [Test]
        public void Dairy_Bots()
        {
            int herd = 0;
            Economy.RunBot(Live(), new FarmerBot(), 8, 1, sim => herd = sim.State.Livestock.Animals.Count);
            TestContext.Out.WriteLine("competent bot's herd after 2 years: " + herd);
            Assert.That(herd, Is.EqualTo(2), "two buffalo");
            var r = Gate.Stress(Live(), 100, 8, new System.Func<Bot>[] { () => new DairyBot() });
            TestContext.Out.WriteLine("dairy-max " + r[1].Ratio.ToString("0.000") + " × competent (" + r[0].MedianNetWorth.ToString("N0") + ")");
            Assert.That(r[1].Ratio, Is.LessThanOrEqualTo(1.2));
        }

        // Spec 011 §9 (US-194): the truck rarely fills and the supermarket's orders stay a bonus.
        [Test]
        public void TruckAndOrders()
        {
            Gate.TradeResult r = Gate.Trade(Live(), 100);
            TestContext.Out.WriteLine("truck full " + r.FullShare.ToString("0.000") + " of " + r.TruckDays + " days; orders " + r.OrderShare.ToString("0.000") + " of sales");
            Assert.That(r.TruckDays, Is.GreaterThan(0));
            Assert.That(r.FullShare, Is.LessThan(0.1), "the truck fills under 1 day in 10");
            Assert.That(r.Supermarket, Is.GreaterThan(0), "the bot fills orders");
            Assert.That(r.OrderShare, Is.LessThan(0.2), "orders under 20% of sales");
        }

        // ---- US-098 (ADR 0045 §4): the Phase 1 health checks, thresholds set from the measured runs with a margin ----

        // ADR 0016's stress set: the cautious saver at most 1.20 × competent, random below competent (leverage is Stress_Debt).
        [Test]
        public void Phase1_StressBots()
        {
            var results = Gate.Stress(Live(), 100, 8, new System.Func<Bot>[] { () => new CautiousSaverBot(), () => new RandomBot() });
            foreach (Gate.StressResult s in results.Skip(1)) TestContext.Out.WriteLine(s.Bot + " " + s.Ratio.ToString("0.000"));
            Assert.That(results[1].Ratio, Is.LessThanOrEqualTo(1.2), "cautious saver");
            Assert.That(results[2].Ratio, Is.LessThan(1.0), "random");
        }

        // Never stuck: no cash, an empty store and no credit; farm profit above 0 within 2 seasons (measured: every seed by season 2).
        [Test]
        public void NeverStuck()
        {
            var first = Gate.NeverStuck(LiveWith, 1000);
            int within = first.Count(k => k >= 1 && k <= 2);
            TestContext.Out.WriteLine("profitable within 2 seasons: " + within + " of 1000");
            Assert.That(within, Is.GreaterThanOrEqualTo(950));
        }

        // Recoverable bankruptcy: after a foreclosure, farm profit within 2 seasons and the Hamlet's conditions again within
        // 3 game years (the GDD's window; measured: 1 season, and a median of 8 days).
        [Test]
        public void RecoverableBankruptcy()
        {
            Gate.RecoveryResult r = Gate.Recovery(Live(), 100);
            int profit = r.SeasonsToProfit.Count(k => k >= 1 && k <= 2);
            int hamlet = r.DaysToHamlet.Count(d => !double.IsNaN(d) && d <= 48);
            TestContext.Out.WriteLine(r.Foreclosed + " foreclosed; profit within 2 seasons " + profit + "; Hamlet within 48 days " + hamlet);
            Assert.That(r.Foreclosed, Is.GreaterThanOrEqualTo(50), "the defaulter is foreclosed");
            Assert.That(profit, Is.GreaterThanOrEqualTo(r.Foreclosed * 95 / 100));
            Assert.That(hamlet, Is.GreaterThanOrEqualTo(r.Foreclosed * 95 / 100));
        }

        // Exact ledgers: every Phase 1 bot closes every season reconciled to the coin.
        [Test]
        public void ExactLedger()
        {
            Assert.That(Gate.ExactLedger(Live(), 50), Is.Empty);
        }

        // Bounded prices: the 0.5–2× rail clamps under 0.1% of good-hours.
        [Test]
        public void BoundedPrices()
        {
            double share = Gate.RailShare(Live(), 100);
            TestContext.Out.WriteLine("rail share " + share.ToString("0.00000"));
            Assert.That(share, Is.LessThan(0.001));
        }

        // ADR 0034 §7: every mechanic is used by at least one bot. The debt bots really borrow, and the defaulter really defaults.
        [Test]
        public void DebtBots_UseCredit()
        {
            Gate.DebtActivity leverage = Gate.DebtBotActivity(Live(), () => new LeverageBot(), 50);
            Assert.That(leverage.BankLoans, Is.GreaterThan(0), "the leverage bot borrows from the bank");
            Gate.DebtActivity defaulter = Gate.DebtBotActivity(Live(), () => new StrategicDefaulterBot(), 50);
            // Every default ends in foreclosure. Where the farm earns enough to pay on time, the defaulter repays instead (ADR 0051).
            Assert.That(defaulter.SeedsDefaulted, Is.GreaterThanOrEqualTo(25), "the strategic defaulter defaults on at least half the seeds");
            Assert.That(defaulter.DefaultedNotForeclosed, Is.Zero, "and every default is foreclosed");
            Assert.That(defaulter.MedianScoreAtEnd, Is.LessThanOrEqualTo(400));
        }
    
        private static double Median(System.Collections.Generic.IEnumerable<double> v)
        {
            var o = v.OrderBy(x => double.IsNaN(x) ? double.MaxValue : x).ToList();
            return o[o.Count / 2];
        }

        // Spec 014 §9 (US-211, ADR 0071): after the Hamlet the competent bot builds family houses on the bank's home loan and has 5
        // households by the Village pacing (day 21, ADR 0028; measured day 15–20), 12–25 people (10–20, median 16), town happiness
        // 65–85 (a town of renters with outside work scores about 68 by §3.8 until businesses bring town jobs; measured 68–70),
        // under 1 in 10 households leaving a year (0), rent under 15% of income (9–14%). The cautious saver, which never borrows,
        // gets there by day 48 (measured 29–43). The random bot never reaches the Hamlet, so it isn't measured (ADR 0071).
        [Test]
        public void Town()
        {
            Gate.TownResult c = Gate.Town(Live(), () => new FarmerBot(), 20);
            Gate.TownResult s = Gate.Town(Live(), () => new CautiousSaverBot(), 20, 12);
            TestContext.Out.WriteLine("competent: 5 households day " + Median(c.Day5).ToString("0.0") + " (" + c.Day5.Min().ToString("0.0") + "–" + c.Day5.Max().ToString("0.0")
                + "); people at day 21 " + Median(c.People21.Select(x => (double)x)) + " (" + c.People21.Min() + "–" + c.People21.Max() + "); happiness " + Median(c.Happiness).ToString("0")
                + "; leaving a year " + c.LeavingPerYear.Max().ToString("0.00") + "; rent share " + Median(c.RentShare).ToString("0.000") + " (max " + c.RentShare.Max().ToString("0.000") + ")");
            TestContext.Out.WriteLine("cautious: 5 households day " + Median(s.Day5).ToString("0.0") + " (latest " + s.Day5.Max().ToString("0.0") + ")");
            Assert.That(c.HamletRuns, Is.EqualTo(20));
            Assert.That(c.Day5.Count(d => !double.IsNaN(d) && d <= 21), Is.GreaterThanOrEqualTo(18), "5 households by the Village pacing (day 21)");
            Assert.That(Median(c.People21.Select(x => (double)x)), Is.InRange(12, 25));
            Assert.That(Median(c.Happiness), Is.InRange(65, 85));
            Assert.That(c.LeavingPerYear.Max(), Is.LessThan(0.1));
            Assert.That(c.RentShare.Max(), Is.LessThan(0.15));
            Assert.That(s.Day5.Count(d => !double.IsNaN(d) && d <= 48), Is.GreaterThanOrEqualTo(18), "the cautious saver within 48 days");
        }

        // Spec 016 §9 (US-119, ADR 0079): a bot founds the bakery (measured from the Hamlet: the Village gate is spec 018's) and runs it,
        // hired, by itself, and on a business loan, selling the shops' pav and rusk and the trader's rusk; never stuck, the overdraft
        // never permanent, and its season profit inside ADR 0079's targets.
        [Test]
        public void Bakery()
        {
            var config = LiveWith(Gate.BakeryInHamlet);
            var results = new[] { () => new BusinessBot(false, false, "bakery"), () => new BusinessBot(true, false, "bakery"), (Func<Bot>)(() => new BusinessBot(false, true, "bakery")) }
                .Select(make => Gate.Business(config, make, 8, "bakery", 16)).ToList();
            foreach (Gate.MillResult m in results)
                TestContext.Out.WriteLine(m.Bot + ": founded " + m.Founded + "/" + m.Seeds + ", profit a season " + string.Join(", ", m.Profit.OrderBy(x => x).Select(x => x.ToString("0")))
                    + "; payback " + string.Join(", ", m.Payback.OrderBy(x => x).Select(x => x.ToString("0.0"))) + "; overdrawn runs " + string.Join(",", m.OverdrawnRun)
                    + "; closed " + m.Closed + ", foreclosed " + m.Foreclosed + "; end cash " + string.Join(", ", m.EndCash.Select(c => (c / 100).ToString())));
            foreach (Gate.MillResult m in results)
            {
                Assert.That(m.Founded, Is.EqualTo(m.Seeds), m.Bot + ": founded in every seed");
                Assert.That(m.EndCash.All(c => c > 0), Is.True, m.Bot + ": never stuck: every run plays on with cash");
                Assert.That(m.OverdrawnRun.Max(), Is.LessThanOrEqualTo(3), m.Bot + ": the overdraft never permanent");
            }
            Assert.That(results[0].Foreclosed + results[2].Foreclosed, Is.Zero, "hired, the farm keeps its founder");
            Assert.That(results[1].Foreclosed, Is.LessThanOrEqualTo(results[1].Seeds / 4), "run by you: foreclosed in at most a quarter of runs (the farm loses your hours)");
            double Mid(System.Collections.Generic.List<double> v) { return v.OrderBy(x => x).ElementAt(v.Count / 2); }
            Assert.That(Mid(results[0].Profit), Is.InRange(15_000, 25_000), "hired");
            Assert.That(Mid(results[0].Payback), Is.InRange(5.0, 8.0), "hired, payback");
            Assert.That(Mid(results[1].Profit), Is.InRange(45_000, 60_000), "run by you");
            Assert.That(Mid(results[1].Payback), Is.InRange(2.0, 3.0), "run by you, payback");
            Assert.That(Mid(results[2].Profit), Is.InRange(15_000, 25_000), "hired, on a business loan");
        }

        // Spec 016 §9 (US-117, ADR 0072): a bot founds the flour mill in the Hamlet and runs it, hired, by itself, and on a business loan;
        // never stuck, the overdraft never permanent, and its season profit inside ADR 0073's targets.
        [Test]
        public void Mill()
        {
            var results = new[] { () => new MillBot(false), () => new MillBot(true), (Func<Bot>)(() => new MillBot(false, true)) }.Select(make => Gate.Mill(Live(), make, 8)).ToList();
            foreach (Gate.MillResult m in results)
                TestContext.Out.WriteLine(m.Bot + ": founded " + m.Founded + "/" + m.Seeds + ", profit a season " + string.Join(", ", m.Profit.OrderBy(x => x).Select(x => x.ToString("0")))
                    + "; payback " + string.Join(", ", m.Payback.OrderBy(x => x).Select(x => x.ToString("0.0"))) + "; overdrawn runs " + string.Join(",", m.OverdrawnRun)
                    + "; closed " + m.Closed + ", foreclosed " + m.Foreclosed + "; end cash " + string.Join(", ", m.EndCash.Select(c => (c / 100).ToString())));
            foreach (Gate.MillResult m in results)
            {
                Assert.That(m.Founded, Is.EqualTo(m.Seeds), m.Bot + ": founded in every seed");
                Assert.That(m.EndCash.All(c => c > 0), Is.True, m.Bot + ": never stuck: every run plays on with cash");
                Assert.That(m.Foreclosed, Is.LessThanOrEqualTo(m.Seeds / 4), m.Bot + ": foreclosed in at most a quarter of runs (running it yourself costs the farm)");
                Assert.That(m.OverdrawnRun.Max(), Is.LessThanOrEqualTo(2), m.Bot + ": the overdraft never permanent");
            }
            // ADR 0073's targets, on the median run.
            double Mid(System.Collections.Generic.List<double> v) { return v.OrderBy(x => x).ElementAt(v.Count / 2); }
            Assert.That(Mid(results[0].Profit), Is.InRange(5_000, 10_000), "hired");
            Assert.That(Mid(results[1].Profit), Is.InRange(30_000, 40_000), "run by you");
            Assert.That(Mid(results[1].Payback), Is.InRange(3.0, 4.0), "run by you, payback");
            Assert.That(Mid(results[2].Profit), Is.InRange(5_000, 10_000), "hired, on a business loan");
        }
    }
}
