using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class HutTests
    {
        // Spec 006 AC-5 (ADR 0039: hut places on the inherited acres).
        [Test]
        public void Build_CostTimeDepreciation()
        {
            Simulation sim = Start();
            long t = 2 * Hour;
            sim.AdvanceTo(t);
            long cash = sim.State.Ledger.Cash, buildings = sim.State.Ledger.Balance(Account.Buildings);
            Ok(sim, new BuildHandHut(t, QuartersTile));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(15_000 * Coin));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(15_000 * Coin));
            Assert.That(PeopleRules.EmptyHut(sim), Is.Null, "not finished yet");

            sim.DrainEvents();
            sim.AdvanceTo(t + 24 * Hour - 1);
            Assert.That(PeopleRules.EmptyHut(sim), Is.Null);
            sim.AdvanceTo(t + 24 * Hour);
            Assert.That(PeopleRules.EmptyHut(sim), Is.Not.Null);
            Assert.That(Last<HandHutBuilt>(sim).SimTime, Is.EqualTo(t + 24 * Hour));

            // 250 a season from the next close.
            long dep = sim.State.Ledger.Balance(Account.AccumulatedDepreciation);
            sim.AdvanceTo(S);
            long hutDep = -(sim.State.Ledger.Balance(Account.AccumulatedDepreciation) - dep) - (40_000 * Coin / 80 + 15_000 * Coin / 120);
            Assert.That(hutDep, Is.EqualTo(250 * Coin));

            // Rejections (ADR 0040): a farm acre, the yard and unowned land (NotQuarters); two places on the quarters, so a third hut
            // is NoRoom there and TooManyHuts in Phase 1 either way.
            long now = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildHandHut(now, HomeTile)).Reason, Is.EqualTo(RejectReason.NotQuarters));
            Assert.That(sim.Execute(new BuildHandHut(now, YardTile)).Reason, Is.EqualTo(RejectReason.NotQuarters));
            Assert.That(sim.Execute(new BuildHandHut(now, 50)).Reason, Is.EqualTo(RejectReason.NotQuarters));
            Ok(sim, new BuildHandHut(now, QuartersTile));
            Assert.That(sim.Execute(new BuildHandHut(now, QuartersTile)).Reason, Is.EqualTo(RejectReason.NoRoom));
        }

        [Test]
        public void Build_ShortOfCash()
        {
            Simulation sim = Start(game: Game((f, j) => f == "start.json" ? j.Replace("\"startingCash\": 2000000", "\"startingCash\": 10000") : j));
            Assert.That(sim.Execute(new BuildHandHut(0, QuartersTile)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
        }
    }
}
