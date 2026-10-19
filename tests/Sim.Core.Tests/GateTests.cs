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

        // ADR 0034 §7: every mechanic is used by at least one bot. The debt bots really borrow, and the defaulter really defaults.
        [Test]
        public void DebtBots_UseCredit()
        {
            Gate.DebtActivity leverage = Gate.DebtBotActivity(Live(), () => new LeverageBot(), 50);
            Assert.That(leverage.BankLoans, Is.GreaterThan(0), "the leverage bot borrows from the bank");
            Gate.DebtActivity defaulter = Gate.DebtBotActivity(Live(), () => new StrategicDefaulterBot(), 50);
            Assert.That(defaulter.SeedsForeclosed, Is.GreaterThan(40), "the strategic defaulter is foreclosed");
            Assert.That(defaulter.MedianScoreAtEnd, Is.LessThanOrEqualTo(400));
        }
    }
}

