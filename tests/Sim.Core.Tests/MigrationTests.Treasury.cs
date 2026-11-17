using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // AC-16 (US-138, schema 19): a schema-18 town loads with a treasury that hasn't opened, hashing as it did; the Township then opens it
        // once, and the open treasury round-trips at 19 to the same hash.
        [Test]
        public void Schema18_Treasury()
        {
            Simulation sim = TreasuryTests.Start();
            Ok(sim, new BuildSchool(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 48 * Hour);
            Assert.That(sim.State.Treasury.Opened, Is.False);

            byte[] old = SaveCodec.Write(sim.State, sim.Config, Wall0, schema: 18);
            LoadResult r = SaveCodec.Read(old, sim.Config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Treasury, Is.Not.Null, "the content has a treasury");
            Assert.That(r.State.Treasury.Opened, Is.False, "but nothing opened it in a schema-18 town");
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "an unopened treasury adds nothing to the hash");

            Simulation loaded = Simulation.FromState(sim.Config, r.State);
            Ok(loaded, new DebugNextEra());
            Assert.That(loaded.State.Treasury.Opened, Is.True);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(loaded.State, sim.Config, Wall0), sim.Config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(loaded.State.ComputeHash()), "round-trips at schema 19");
            Assert.That(again.State.Treasury.Cash, Is.EqualTo(loaded.State.Treasury.Cash));
        }
    }
}
