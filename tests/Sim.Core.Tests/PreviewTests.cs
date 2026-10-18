using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Farm;

namespace Sim.Core.Tests
{
    /// <summary>Simulation.Preview: what a command would say, on a copy of the state (spec 008 §3.6).</summary>
    public class PreviewTests
    {
        [Test]
        public void Preview_SaysWhatExecuteWould_AndChangesNothing()
        {
            Simulation sim = TestSim.NewFarm(seed: 7, localSeconds: 8 * 3600);
            sim.AdvanceTo(20_000);
            long t = sim.State.Clock.SimTime;
            ulong hash = sim.State.ComputeHash();
            int logged = sim.State.CommandLog.Entries.Count;

            Command ok = new Sow(t, 20, "potato", SeedSource.Certified, "none");
            Command wrongSeason = new Sow(t, 20, "wheat", SeedSource.Certified, "none");
            Command unowned = new Sow(t, 1, "potato", SeedSource.Certified, "none");
            Assert.That(sim.Preview(ok).Accepted, Is.True);
            Assert.That(sim.Preview(wrongSeason).Reason, Is.EqualTo(RejectReason.OutOfSeason));
            Assert.That(sim.Preview(unowned).Reason, Is.EqualTo(RejectReason.NotOwned));
            Assert.That(sim.Preview(new Sow(t - 1, 20, "potato", SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.TimeInPast));

            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a preview changes nothing");
            Assert.That(sim.State.CommandLog.Entries.Count, Is.EqualTo(logged), "and is never logged");
            Assert.That(sim.State.Farm.CropOn(20), Is.Null);

            Assert.That(sim.Execute(ok).Accepted, Is.True, "the real thing then behaves as previewed");
            Assert.That(sim.Preview(ok).Reason, Is.EqualTo(RejectReason.TileOccupied));
        }

        [Test]
        public void Clone_WithoutLog_HashesLikeTheOriginal()
        {
            Simulation sim = TestSim.NewFarm(seed: 9);
            sim.AdvanceTo(3 * 86400);
            Assert.That(sim.State.Clone(withCommandLog: false).ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
