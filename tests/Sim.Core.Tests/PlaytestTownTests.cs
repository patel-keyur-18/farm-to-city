using System.IO;
using NUnit.Framework;
using Sim.Core.Saves;
using Tools.Balancer;

namespace Sim.Core.Tests
{
    /// <summary>US-237: the playtest town the balancer writes imports and plays on.</summary>
    [Category("Balance")]
    public class PlaytestTownTests
    {
        [Test]
        public void Town_ImportsAndPlays()
        {
            SimConfig config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game());
            string path = Path.Combine(Path.GetTempPath(), "ftc-playtest-" + System.Guid.NewGuid().ToString("N") + ".ftc");
            try
            {
                Assert.That(Playtest.Save(config, 2, path, "test"), Is.EqualTo(0));
                LoadResult r = SaveCodec.Read(File.ReadAllBytes(path), config);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
                Assert.That(Sim.Core.Progress.EraRules.Households(Simulation.FromState(config, r.State)), Is.GreaterThanOrEqualTo(Playtest.ThirdHousehold));
                Assert.That(r.State.Finance.Reports.Count, Is.GreaterThanOrEqualTo(1), "a close has happened");
                Assert.That(Sim.Core.Time.ClockAlign.Offset(r.State.Clock.SimTime, (long)System.DateTime.Now.TimeOfDay.TotalSeconds), Is.EqualTo(0), "the game's time of day is the machine's");
                Assert.That(r.State.Businesses.Businesses, Is.Empty, "no business yet");
                Simulation sim = Simulation.FromState(config, r.State);
                ulong before = sim.State.ComputeHash();
                sim.AdvanceTo(sim.State.Clock.SimTime + 4 * 86400); // a season of play on
                Assert.That(sim.State.ComputeHash(), Is.Not.EqualTo(before));
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }

        // US-249: the Phase 3 playtest town is the close after the Village unlock; it imports, is in the Village era and plays on.
        [Test]
        public void VillageTown_ImportsAndPlays()
        {
            SimConfig config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game());
            string path = Path.Combine(Path.GetTempPath(), "ftc-playtest-" + System.Guid.NewGuid().ToString("N") + ".ftc");
            try
            {
                Assert.That(Playtest.Save(config, 2, path, "test", "village"), Is.EqualTo(0));
                LoadResult r = SaveCodec.Read(File.ReadAllBytes(path), config);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
                Assert.That(config.Game.Onboarding.Eras[r.State.Era.Current].Id, Is.EqualTo("village"));
                Simulation sim = Simulation.FromState(config, r.State);
                ulong before = sim.State.ComputeHash();
                sim.AdvanceTo(sim.State.Clock.SimTime + 4 * 86400);
                Assert.That(sim.State.ComputeHash(), Is.Not.EqualTo(before));
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
