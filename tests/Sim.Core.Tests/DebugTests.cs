using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class DebugTests
    {
        private static Simulation Dev() { return New(localSeconds: 10 * Hour + 17 * 60, options: new SimOptions { DevCommandsEnabled = true }); }

        // Spec 001 AC-27.
        [Test]
        public void TimeTravel_GatedAndExact()
        {
            // Disabled by default (release builds never enable them).
            Simulation release = New(localSeconds: 10 * Hour);
            long r0 = release.State.Clock.SimTime;
            Assert.That(release.Execute(new DebugAdvance(Day, AdvanceMode.Live)).Reason, Is.EqualTo(RejectReason.DevCommandsDisabled));
            Assert.That(release.State.Clock.SimTime, Is.EqualTo(r0));
            Assert.That(release.State.CommandLog.Entries.Count, Is.EqualTo(1), "only NewGame");

            // +1 day live: exactly 86 400 s, no away report, lastWallUtc unchanged.
            Simulation sim = Dev();
            long t0 = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new DebugAdvance(Day, AdvanceMode.Live)).Accepted, Is.True);
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0 + Day));
            Assert.That(sim.State.Clock.LastWallUtc, Is.EqualTo(Wall0));
            Assert.That(sim.DrainEvents().OfType<AwayAdvanceCompleted>(), Is.Empty);

            // Jump to the away cap: like AC-22 (under the cap, applied exactly).
            long t1 = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new DebugAdvance(sim.Clock.AwayCapSeconds, AdvanceMode.Away)).Accepted, Is.True);
            AwayAdvanceCompleted jump = sim.DrainEvents().OfType<AwayAdvanceCompleted>().Single();
            Assert.That((jump.AppliedSeconds, jump.Capped), Is.EqualTo((72 * Hour, false)));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t1 + 72 * Hour));

            // +1 season away (96 h): like AC-23 (capped to 72 h by dropping a whole day; same time of day).
            long t2 = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new DebugAdvance(sim.Clock.SecondsPerSeason, AdvanceMode.Away)).Accepted, Is.True);
            AwayAdvanceCompleted season = sim.DrainEvents().OfType<AwayAdvanceCompleted>().Single();
            Assert.That((season.RequestedSeconds, season.AppliedSeconds, season.Capped), Is.EqualTo((96 * Hour, 72 * Hour, true)));
            Assert.That(Calendar.From(sim.State.Clock.SimTime, sim.Clock).Hour, Is.EqualTo(Calendar.From(t2, sim.Clock).Hour));

            // All three are in the command log.
            Assert.That(sim.State.CommandLog.Entries.Skip(1).Select(e => e.Command), Has.All.InstanceOf<DebugAdvance>());
            Assert.That(sim.State.CommandLog.Entries.Count, Is.EqualTo(4));

            // The next Sync carries on normally from the new sim time.
            long t3 = sim.State.Clock.SimTime;
            sim.Execute(new Sync(Wall0 + 30));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t3 + 30));
        }

        [TestCase(3600L)]
        [TestCase(86400L)]
        [TestCase(4 * 86400L)]
        [TestCase(72 * 3600L)]
        public void AllowedSteps_InBothModes(long seconds)
        {
            Assert.That(Dev().Execute(new DebugAdvance(seconds, AdvanceMode.Live)).Accepted, Is.True);
            Assert.That(Dev().Execute(new DebugAdvance(seconds, AdvanceMode.Away)).Accepted, Is.True);
        }

        [TestCase(1L)]
        [TestCase(7200L)]
        [TestCase(-3600L)]
        [TestCase(0L)]
        public void OtherSteps_AreInvalid(long seconds)
        {
            Simulation sim = Dev();
            Assert.That(sim.Execute(new DebugAdvance(seconds, AdvanceMode.Live)).Reason, Is.EqualTo(RejectReason.InvalidStep));
            Assert.That(sim.Execute(new DebugAdvance(seconds, AdvanceMode.Away)).Reason, Is.EqualTo(RejectReason.InvalidStep));
        }

        // ADR 0076 (US-218): the developer panel's Next era moves the town on an era at a time, as a gate would; the Village (its
        // gate not built yet) never unlocks by itself; release builds refuse it; the command saves and replays.
        [Test]
        public void NextEra()
        {
            Simulation release = NewFarm();
            Assert.That(release.Execute(new DebugNextEra()).Reason, Is.EqualTo(RejectReason.DevCommandsDisabled));
            Assert.That(release.State.Era.Current, Is.Zero);

            Simulation sim = NewFarm(options: new SimOptions { DevCommandsEnabled = true });
            var eras = sim.Config.Game.Onboarding.Eras;
            Assert.That(sim.Execute(new DebugNextEra()).Accepted, Is.True);
            Assert.That(eras[sim.State.Era.Current].Id, Is.EqualTo("hamlet"));
            Assert.That(sim.DrainEvents().OfType<EraUnlocked>().Single().EraId, Is.EqualTo("hamlet"));

            // A Hamlet whose Village gate isn't built stays a Hamlet through closes (eras.json: manual).
            Assert.That(eras[2].Manual, Is.True);
            sim.Execute(new DebugAdvance(sim.Clock.SecondsPerSeason, AdvanceMode.Live));
            Assert.That(eras[sim.State.Era.Current].Id, Is.EqualTo("hamlet"));

            Assert.That(sim.Execute(new DebugNextEra()).Accepted, Is.True);
            Assert.That(eras[sim.State.Era.Current].Id, Is.EqualTo("village"));
            Assert.That(sim.Execute(new DebugNextEra()).Reason, Is.EqualTo(RejectReason.InvalidStep), "the last era");

            Saves.LoadResult r = Saves.SaveCodec.Read(Saves.SaveCodec.Write(sim.State, sim.Config, Wall0), sim.Config);
            Assert.That(r.Error, Is.EqualTo(Saves.SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
