using System.Linq;
using NUnit.Framework;
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
                if (PolicyRules.CanEnact(sim, PolicyId.Subsidy) == RejectReason.None) Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
                Close(sim);
                paid += T(sim).Reports.Last().Rows[PolicyDept].Paid;
                if (PolicyRules.Of(sim, PolicyId.Subsidy).Status == PolicyStatus.Active) Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
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
    }
}
