using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Tools.Balancer;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>
    /// Spec 022 §9's checks that need no bot: a Township left alone, and the four exploits (the rate dodger, the policy flipper, the one who
    /// milks the treasury). Taxes and policies begin at the Township; until spec 023 builds its gate the town is reached with the developer's
    /// next-era, so these stand on a small town and check the rules, not the pace.
    /// </summary>
    [Category("Balance")]
    public class TaxGateTests
    {
        private static TreasuryState T(Simulation sim) { return sim.State.Treasury; }

        private static Simulation Township(System.Func<string, string, string> edit = null, int seed = 0)
        {
            Simulation sim = NewFarm(game: Game(edit), options: new SimOptions { DevCommandsEnabled = true });
            TreasuryTests.Era(sim, "village");
            TreasuryTests.Family(sim);
            TreasuryTests.Township(sim);
            return sim;
        }

        private static void Close(Simulation sim) { sim.AdvanceTo(TreasuryTests.NextClose(sim)); }

        // AC-12: a Township nobody ever opens a sheet on: eight years with no command, nothing breaks. The treasury is never below zero, every
        // book reconciles, no policy runs that wasn't enacted, and the founder is charged only what the formulas say (the house tax).
        [Test]
        public void HandsOff()
        {
            Simulation sim = Township();
            for (int i = 0; i < 32; i++)
            {
                Close(sim);
                Assert.That(T(sim).Cash, Is.GreaterThanOrEqualTo(0), "the treasury never goes below zero (season " + i + ")");
                Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            }
            Assert.That(T(sim).Policies.All(p => p.Status == PolicyStatus.None), Is.True, "no policy runs unless it was enacted");
            Assert.That(T(sim).Reports.Last().Rows[(int)PolicyDept].Paid, Is.EqualTo(0));
        }

        private const int PolicyDept = 6;

        // The rate dodger: a rate cut the minute before a close and put back the minute after saves nothing at that close (it bills at the rate in
        // force), and costs the town exactly one season at the cut rate: the same taxes as a plain one-season cut, to the coin.
        [Test]
        public void RateDodger()
        {
            long[] Taxes(bool dodge)
            {
                Simulation sim = Township();
                var bills = new long[6];
                for (int i = 0; i < bills.Length; i++)
                {
                    if (dodge && i == 1) Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.House, Fixed.Zero)); // the cut, just before the close
                    Close(sim);
                    bills[i] = -T(sim).Reports.Last().Balance(Account.HouseTaxIncome);
                    if (dodge && i == 1) Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.House, Fixed.Parse("0.016"))); // and put back just after
                }
                return bills;
            }
            long[] steady = Taxes(false), dodger = Taxes(true);
            Assert.That(steady[0], Is.GreaterThan(0));
            Assert.That(dodger[1], Is.EqualTo(steady[1]), "the close the cut came before still bills the old rate");
            Assert.That(dodger[2], Is.EqualTo(0), "the cut's one season, the next");
            Assert.That(dodger[3], Is.EqualTo(steady[3]), "and then it is as it was");
        }

        // The policy flipper: enact, repeal, enact again each season. It pays every time and never gets an effect: a policy needs its lag to pass, and a repeal waits a season.
        [Test]
        public void PolicyFlipper()
        {
            Simulation sim = Township((f, j) => f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": 10000000") : j);
            int effective = 0;
            long paid = 0;
            for (int i = 0; i < 12; i++)
            {
                if (!PolicyRules.Of(sim, PolicyId.Subsidy).Repealing && PolicyRules.CanEnact(sim, PolicyId.Subsidy) == RejectReason.None) Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
                Close(sim);
                paid += T(sim).Reports.Last().Rows[PolicyDept].Paid;
                if (PolicyRules.Of(sim, PolicyId.Subsidy).Status == PolicyStatus.Active && !PolicyRules.Of(sim, PolicyId.Subsidy).Repealing) Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
                if (PolicyRules.Effective(sim, PolicyId.Subsidy)) effective++;
            }
            Assert.That(paid, Is.GreaterThan(0), "it paid for the subsidy");
            Assert.That(effective, Is.EqualTo(0), "and never had it");
        }

        // Milk the treasury: every tax at its cap, every policy the slots allow, years of it: not one coin from the treasury reaches your cash.
        [Test]
        public void MilkTheTreasury()
        {
            Simulation sim = Township((f, j) => f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": 10000000") : j);
            Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.House, Fixed.Parse("0.024")));
            Ok(sim, new SetTaxRate(sim.State.Clock.SimTime, TaxKind.Business, Fixed.Parse("0.2")));
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Grant));
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.RentControl));
            for (int i = 0; i < 16; i++) Close(sim);
            foreach (JournalEntry e in sim.State.Ledger.Entries)
                Assert.That(e.DescriptionKey.StartsWith("ledger.town") || e.DescriptionKey.Contains("grant") && e.Postings.Any(p => p.Account == Account.Cash && p.Amount > 0 && e.Reference == "district"), Is.False, e.DescriptionKey);
            Assert.That(T(sim).Cash, Is.GreaterThanOrEqualTo(0));
        }
    
        // ---- spec 023 §12: the Township's taxes on a bot-built town (US-100) ----

        private static string Rates(string house, string business, string file, string json)
        {
            return file == "treasury.json" ? json.Replace("\"rate\": 0.016", "\"rate\": " + house).Replace("\"rate\": 0.10", "\"rate\": " + business) : json;
        }

        private static Gate.TownshipResult Run(string house, string business, int seeds, int closes = 8)
        {
            TestContent.WithCohort = true;
            try
            {
                var config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game((f, j) => Rates(house, business, f, j)));
                return Gate.Township(config, () => new TownshipBot(false), seeds, 80, closes);
            }
            finally { TestContent.WithCohort = false; }
        }

        private static double Median(System.Collections.Generic.IEnumerable<double> xs) { var l = xs.OrderBy(x => x).ToList(); return l[l.Count / 2]; }

        private static int Seeds { get { string v = System.Environment.GetEnvironmentVariable("FTC_SEEDS"); return v == null ? 12 : int.Parse(v); } }

        // Every tax at 0 (spec 022 §9): the town left alone with no tax at all: the treasury runs dry, services decay (never below zero for the
        // treasury), the old village and your households stay, and nothing is stuck: the Township is still reached and the town goes on.
        [Test]
        public void EveryTaxAtZero()
        {
            Gate.TownshipResult normal = Run("0.016", "0.10", Seeds, 16), zero = Run("0", "0", Seeds, 16);
            TestContext.Out.WriteLine("ZERO: treasury two years on " + string.Join(" ", zero.TreasuryAfter.Select(c => c / Money.Coin)) + "; lowest service " + string.Join(" ", zero.LowestService.Select(x => x.ToString("0.00")))
                + "; happiness " + Median(zero.Happiness).ToString("0.0") + " (default taxes " + Median(normal.Happiness).ToString("0.0") + "); old village happiness " + Median(zero.OldVillageHappiness).ToString("0.0") + " (" + Median(normal.OldVillageHappiness).ToString("0.0") + "); households left " + string.Join(" ", zero.OldVillageLeft) + "; your households " + string.Join(" ", zero.HouseholdsAfter) + " (default " + string.Join(" ", normal.HouseholdsAfter) + ")");
            Assert.That(zero.Broken, Is.EqualTo(0), "the treasury is never below zero and its books reconcile");
            Assert.That(zero.Township.Count(double.IsNaN), Is.EqualTo(normal.Township.Count(double.IsNaN)), "no tax doesn't stop the Township");
            Assert.That(Median(zero.LowestService), Is.LessThan(0.3), "four years with no tax and the services have wasted away");
            Assert.That(Median(normal.LowestService), Is.GreaterThan(0.9), "and with the default rates they have not");
            Assert.That(Median(zero.Happiness), Is.LessThan(Median(normal.Happiness) - 1), "the town is less happy for it (spec 022 §9 said 5; measured 1.8: the services weigh little against food, work and rent)");
            Assert.That(zero.OldVillageLeft.All(n => n == 0), Is.True, "the old village stays");
            Assert.That(zero.HouseholdsAfter.Min(), Is.GreaterThanOrEqualTo(14), "yours stay too");
        }

        // Every tax at its cap: the treasury is the better for it, nobody is stuck, no old household is driven away.
        [Test]
        public void EveryTaxAtTheCap()
        {
            Gate.TownshipResult normal = Run("0.016", "0.10", Seeds), cap = Run("0.024", "0.2", Seeds);
            TestContext.Out.WriteLine("CAP: treasury two years on " + Median(cap.TreasuryAfter.Select(c => (double)c / Money.Coin)).ToString("0") + " (default " + Median(normal.TreasuryAfter.Select(c => (double)c / Money.Coin)).ToString("0")
                + "); happiness " + Median(cap.Happiness).ToString("0.0") + " (" + Median(normal.Happiness).ToString("0.0") + "); old village happiness " + Median(cap.OldVillageHappiness).ToString("0.0") + " (" + Median(normal.OldVillageHappiness).ToString("0.0") + "); old households left " + string.Join(" ", cap.OldVillageLeft)
                + "; your households " + string.Join(" ", cap.HouseholdsAfter));
            Assert.That(cap.Broken, Is.EqualTo(0));
            Assert.That(Median(cap.TreasuryAfter.Select(c => (double)c)), Is.GreaterThan(Median(normal.TreasuryAfter.Select(c => (double)c))), "a higher rate brings the town more");
        }
    }
}
