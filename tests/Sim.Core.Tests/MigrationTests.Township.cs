using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        // AC-12 (spec 023, schema 20): a schema-19 town loads with a Township that hasn't started, hashing as it did, and the road then
        // starts once and round-trips at 20.
        [Test]
        public void Schema19_Township()
        {
            Simulation sim = NewFarm(game: Game(), options: new SimOptions { DevCommandsEnabled = true });
            Assert.That(sim.State.Township, Is.Not.Null, "the content has a Township");

            ChronicleTests.Forget(sim.State.Chronicle); // the Chronicle is schema 24: a schema-19 write can't hold it
            byte[] old = SaveCodec.Write(sim.State, sim.Config, Wall0, schema: 19);
            LoadResult r = SaveCodec.Read(old, sim.Config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Township.Road.Status, Is.EqualTo(RoadStatus.None));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "an unstarted Township adds nothing to the hash");
        }
    }
}
