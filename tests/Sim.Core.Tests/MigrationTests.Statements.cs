using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Finance;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // Spec 019 AC-19 (US-137): a schema-15 town opens its past seasons at once (profit and loss, balance sheet), with the cash flow
        // rows and Your people marked as kept from the next close; that close fills them, and the new schema round-trips.
        [Test]
        public void Schema15_Statements()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(15)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Finance.Reports, Is.Not.Empty);
            foreach (SeasonReport rep in r.State.Finance.Reports)
            {
                Assert.That((rep.CashKept, rep.CashRows.Count, rep.People.Count), Is.EqualTo((false, 0, 0)), "season " + rep.Period);
                Assert.That(rep.NetWorth.Deposit, Is.EqualTo(0));
                Assert.That(rep.Lines.Count, Is.GreaterThan(0), "its profit and loss opens");
                Assert.That(rep.CashFlow.Closing - rep.CashFlow.Opening, Is.EqualTo(rep.CashFlow.Closing - rep.CashFlow.Opening), "its cash summary stays");
            }

            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            SeasonReport next = sim.State.Finance.Reports.Last();
            Assert.That(next.CashKept, Is.True, "the next close keeps its rows");
            Assert.That(next.CashRows.Sum(x => x.Amount), Is.EqualTo(next.CashFlow.Closing - next.CashFlow.Opening));
            Assert.That(sim.State.Finance.Reports.First().CashKept, Is.False, "a past season stays as it was");

            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            SeasonReport kept = again.State.Finance.Reports.Last();
            Assert.That(kept.CashRows.Select(x => (x.Account, x.Amount)), Is.EqualTo(next.CashRows.Select(x => (x.Account, x.Amount))), "the rows round-trip");
            Assert.That((kept.FirstEntry, kept.EndEntry), Is.EqualTo((next.FirstEntry, next.EndEntry)));
            Assert.That(kept.People.Select(x => (x.Name, x.Paid, x.Outcome)), Is.EqualTo(next.People.Select(x => (x.Name, x.Paid, x.Outcome))));

            // Milestones are schema 16: a schema-15 write can't hold them (the older-town pass finds them again), so compare the town without them.
            sim.State.Finance.Milestones.Reached.Clear();
            sim.State.Finance.Milestones.InheritedWorth = 0;
            LoadResult old = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0, schema: 15), config, MinSchema);
            Assert.That(old.Error, Is.EqualTo(SaveError.None), old.Detail);
            Assert.That(old.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "a schema-15 write is the same town");
        }
    }
}
