using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using static Sim.Core.Tests.CreditKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 019 §3.6, ADR 0101 §7 (US-136): the five wealth milestones, each once, from the report just built and from the reports kept before.</summary>
    public class MilestoneTests
    {
        private static string[] Ids(Simulation sim) { return sim.DrainEvents().OfType<MilestoneReached>().Select(m => m.Id).ToArray(); }

        // AC-7: each fires once, by its condition, saved and replayed.
        [Test]
        public void Once()
        {
            Simulation sim = NewSim(cash: 20_000);
            sim.AdvanceTo(S);
            Assert.That(Ids(sim), Is.Empty, "20,000 coins is not a lakh");
            AddCash(sim, 100_000);
            sim.AdvanceTo(2 * S);
            var reached = sim.DrainEvents().OfType<MilestoneReached>().ToList();
            Assert.That(reached.Select(m => m.Id), Does.Contain(MilestoneRules.FirstLakh));
            MilestoneReached lakh = reached.Single(m => m.Id == MilestoneRules.FirstLakh);
            Assert.That(lakh.Period, Is.EqualTo(1), "the season it happened in");
            Assert.That(lakh.Past, Is.False);
            Assert.That(MilestoneRules.State(sim).Find(MilestoneRules.FirstLakh).Period, Is.EqualTo(1));
            sim.AdvanceTo(3 * S);
            Assert.That(Ids(sim), Does.Not.Contain(MilestoneRules.FirstLakh), "once");

            // Saved and read back, the same state and the same hash; a replay of the commands gives it too.
            byte[] bytes = Sim.Core.Saves.SaveCodec.Write(sim.State, sim.Config, TestSim.Wall0);
            var loaded = Sim.Core.Saves.SaveCodec.Read(bytes, sim.Config);
            Assert.That(MilestoneRules.State(Simulation.FromState(sim.Config, loaded.State)).Reached.Select(m => m.Id), Is.EqualTo(MilestoneRules.State(sim).Reached.Select(m => m.Id)));
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // Borrowed cash is not savings: a lakh in hand and a lakh owed is nothing saved.
        [Test]
        public void BorrowedIsNotSaved()
        {
            Simulation sim = NewSim(cash: 20_000);
            Assert.That(Take(sim, "coop_bank", 100_000, 3).Accepted, Is.True);
            Assert.That(Cash(sim), Is.GreaterThan(Coins(100_000)));
            sim.AdvanceTo(S);
            Assert.That(Ids(sim), Does.Not.Contain(MilestoneRules.FirstLakh));
        }

        // Twice the inheritance, in the coins of that close: the inheritance is found once from a fresh game of the seed and kept.
        [Test]
        public void TwiceInherited()
        {
            Simulation sim = NewSim(cash: 20_000);
            long start = Reporting.NetWorth(sim).Total;
            sim.AdvanceTo(S);
            sim.DrainEvents();
            Assert.That(MilestoneRules.State(sim).InheritedWorth, Is.EqualTo(start), "a fresh game's net worth, kept at the first close");
            Assert.That(MilestoneRules.Inherited(sim, 0), Is.GreaterThanOrEqualTo(start));
            AddCash(sim, (2 * start) / Coins(1) + 10);
            sim.AdvanceTo(2 * S);
            Assert.That(Ids(sim), Does.Contain(MilestoneRules.Twice));
        }

        // First rent: the first close after a rent was received.
        [Test]
        public void FirstRent()
        {
            Simulation sim = NewSim(cash: 20_000);
            sim.AdvanceTo(S);
            Assert.That(Ids(sim), Does.Not.Contain(MilestoneRules.FirstRent));
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.rent", null, new Posting(Account.Cash, Coins(1_000)), new Posting(Account.RentReceived, -Coins(1_000)));
            sim.AdvanceTo(2 * S);
            Assert.That(Ids(sim), Does.Contain(MilestoneRules.FirstRent));
        }

        // Debt-free: only by repaying your own last loan; a write-off doesn't count; nothing before a first loan.
        [Test]
        public void DebtFree()
        {
            Simulation sim = NewSim(cash: 20_000);
            sim.AdvanceTo(S);
            Assert.That(Ids(sim), Does.Not.Contain(MilestoneRules.DebtFree), "never had a loan");
            Assert.That(Take(sim, "coop_bank", 10_000, 3).Accepted, Is.True);
            sim.AdvanceTo(2 * S);
            Assert.That(Ids(sim), Does.Not.Contain(MilestoneRules.DebtFree), "still owing");
            Assert.That(sim.Execute(new RepayLoan(sim.State.Clock.SimTime, Loan(sim).Id, long.MaxValue / 4)).Accepted, Is.True);
            Assert.That(MilestoneRules.DebtFreeNow(sim), Is.True);
            sim.AdvanceTo(3 * S);
            Assert.That(Ids(sim), Does.Contain(MilestoneRules.DebtFree));

            Simulation seized = NewSim(cash: 20_000);
            Assert.That(Take(seized, "coop_bank", 10_000, 3).Accepted, Is.True);
            Loan(seized).State = LoanState.WrittenOff;
            Assert.That(MilestoneRules.DebtFreeNow(seized), Is.False, "a foreclosure's write-off is not repaying");
        }

        // AC-22: an older town's first close after the update records what its kept reports show, with their real seasons, in one go.
        [Test]
        public void FromReports()
        {
            string without = ", \"milestones\": { \"savings\": 100000, \"netWorthTimes\": 2, \"passivePercent\": 100 }";
            Simulation old = NewSim(cash: 150_000, edit: (f, j) => f == "finance.json" ? j.Replace(without, "") : j);
            Assert.That(old.Config.Game.Ledger.Milestones, Is.Null);
            Assert.That(Take(old, "coop_bank", 10_000, 3).Accepted, Is.True);
            Assert.That(old.Execute(new RepayLoan(old.State.Clock.SimTime, Loan(old).Id, long.MaxValue / 4)).Accepted, Is.True);
            old.AdvanceTo(S);
            old.State.Ledger.Post(old.State.Clock.SimTime, old.Period, "test.rent", null, new Posting(Account.Cash, Coins(1_000)), new Posting(Account.RentReceived, -Coins(1_000)));
            old.AdvanceTo(2 * S);
            old.AdvanceTo(3 * S - 1);
            Assert.That(old.State.Finance.Reports.Count, Is.EqualTo(2));
            Assert.That(old.State.Finance.Milestones.IsDefault, Is.True, "content without milestones checks none and keeps nothing");

            // The update: the same town, now with milestones.
            Simulation now = NewSim(cash: 150_000);
            byte[] bytes = Sim.Core.Saves.SaveCodec.Write(old.State, old.Config, TestSim.Wall0);
            Simulation updated = Simulation.FromState(now.Config, Sim.Core.Saves.SaveCodec.Read(bytes, now.Config).State);
            updated.AdvanceTo(3 * S);
            var reached = updated.DrainEvents().OfType<MilestoneReached>().ToList();
            Assert.That(reached.Single(m => m.Id == MilestoneRules.FirstLakh).Period, Is.EqualTo(0), "the first report already showed a lakh");
            Assert.That(reached.Single(m => m.Id == MilestoneRules.FirstLakh).Past, Is.True);
            Assert.That(reached.Single(m => m.Id == MilestoneRules.FirstRent).Period, Is.EqualTo(1), "the rent's season, not this one");
            Assert.That(reached.All(m => m.Past || m.Period == 2), Is.True);
            Assert.That(reached.Select(m => m.Id), Does.Not.Contain(MilestoneRules.DebtFree), "repaid before the kept reports: its season is unknown, so it waits for the next close");
            updated.AdvanceTo(4 * S);
            MilestoneReached later = updated.DrainEvents().OfType<MilestoneReached>().Single(m => m.Id == MilestoneRules.DebtFree);
            Assert.That(later.Period, Is.EqualTo(3));
            Assert.That(later.Past, Is.False);
            Assert.That(reached.Select(m => m.Id).Distinct().Count(), Is.EqualTo(reached.Count), "each once");
        }
    }
}
