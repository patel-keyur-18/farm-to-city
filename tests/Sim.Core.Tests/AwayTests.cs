using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Scheduling;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class AwayTests
    {
        // Spec 001 AC-21.
        [Test]
        public void SmallDelta_IsLive()
        {
            Simulation sim = New(localSeconds: 10 * Hour);
            long t0 = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new Sync(Wall0 + 30)).Accepted, Is.True);
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0 + 30));
            Assert.That(sim.State.Clock.LastWallUtc, Is.EqualTo(Wall0 + 30));
            Assert.That(sim.DrainEvents().OfType<AwayAdvanceCompleted>(), Is.Empty);

            // Exactly at the threshold (60 s) is still live.
            sim.Execute(new Sync(Wall0 + 90));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0 + 90));
            Assert.That(sim.DrainEvents().OfType<AwayAdvanceCompleted>(), Is.Empty);
        }

        // Spec 001 AC-22.
        [Test]
        public void Away_UnderCap()
        {
            Simulation sim = New(localSeconds: 10 * Hour);
            long t0 = sim.State.Clock.SimTime;
            sim.Execute(new Sync(Wall0 + 5 * Hour));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0 + 5 * Hour));

            AwayAdvanceCompleted done = sim.DrainEvents().OfType<AwayAdvanceCompleted>().Single();
            Assert.That(done.AppliedSeconds, Is.EqualTo(5 * Hour));
            Assert.That(done.RequestedSeconds, Is.EqualTo(5 * Hour));
            Assert.That(done.Capped, Is.False);
            Assert.That((done.FromSimTime, done.ToSimTime), Is.EqualTo((t0, t0 + 5 * Hour)));

            // Just over the threshold is away.
            sim.Execute(new Sync(Wall0 + 5 * Hour + 61));
            Assert.That(sim.DrainEvents().OfType<AwayAdvanceCompleted>().Single().AppliedSeconds, Is.EqualTo(61));
        }

        // Spec 001 AC-23.
        [Test]
        public void Away_OverCap_DropsWholeDays()
        {
            Simulation sim = New(localSeconds: 10 * Hour + 17 * 60);
            long t0 = sim.State.Clock.SimTime;
            sim.Execute(new Sync(Wall0 + 100 * Hour));

            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0 + 52 * Hour));
            AwayAdvanceCompleted done = sim.DrainEvents().OfType<AwayAdvanceCompleted>().Single();
            Assert.That(done.AppliedSeconds, Is.EqualTo(52 * Hour));
            Assert.That(done.RequestedSeconds, Is.EqualTo(100 * Hour));
            Assert.That(done.Capped, Is.True);

            CalendarDate after = Calendar.From(sim.State.Clock.SimTime, sim.Clock);
            CalendarDate uncapped = Calendar.From(t0 + 100 * Hour, sim.Clock);
            Assert.That((after.Hour, after.Minute, after.Second), Is.EqualTo((uncapped.Hour, uncapped.Minute, uncapped.Second)));
            Assert.That(sim.State.Clock.LastWallUtc, Is.EqualTo(Wall0 + 100 * Hour));
        }

        [TestCase(72 * Hour, 72 * Hour)]
        [TestCase(72 * Hour + 1, 48 * Hour + 1)]
        [TestCase(96 * Hour, 72 * Hour)]
        [TestCase(96 * Hour + 1, 72 * Hour + 1 - Day)]
        [TestCase(30 * Day + 5, 2 * Day + 5)]
        public void Cap_ResultLiesWithinLastDayBeforeTheCap(long requested, long expected)
        {
            long applied = AwayCap.Apply(requested, TestContent.Clock());
            Assert.That(applied, Is.EqualTo(expected));
            Assert.That(applied, Is.GreaterThan(48 * Hour).And.LessThanOrEqualTo(72 * Hour));
            Assert.That((requested - applied) % Day, Is.EqualTo(0));
        }

        // Spec 001 AC-24.
        [Test]
        public void BackwardJump_GrantsNothing()
        {
            Simulation sim = New(localSeconds: 10 * Hour);
            long t0 = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new Sync(Wall0 - 2 * Hour)).Accepted, Is.True);
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0));
            ClockAnomalyDetected anomaly = sim.DrainEvents().OfType<ClockAnomalyDetected>().Single();
            Assert.That((anomaly.LastWallUtc, anomaly.NowWallUtc), Is.EqualTo((Wall0, Wall0 - 2 * Hour)));
            Assert.That(sim.State.Clock.LastWallUtc, Is.EqualTo(Wall0 - 2 * Hour));

            sim.Execute(new Sync(Wall0 - 2 * Hour + 30));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t0 + 30));
            Assert.That(sim.DrainEvents().OfType<ClockAnomalyDetected>(), Is.Empty);
        }

        // Spec 001 AC-25.
        [Test]
        public void IsAway_VisibleToSystems()
        {
            var probe = new ProbeSystem();
            var jobLog = new List<string>();
            var jobs = new ProbeJobHandler("probe", SystemOrder.Agriculture, jobLog);
            Simulation sim = New(localSeconds: 10 * Hour, systems: new[] { probe }, handlers: new[] { jobs });

            sim.ScheduleJob("probe", 1, sim.State.Clock.SimTime + 20 * 60);
            sim.Execute(new Sync(Wall0 + 59)); // live
            sim.Execute(new Sync(Wall0 + 59 + 3 * Hour)); // away: crosses hour boundaries and the job
            Assert.That(probe.AwayFlags.Count, Is.EqualTo(3));
            Assert.That(probe.AwayFlags, Has.All.True);
            Assert.That(jobs.AwayFlags, Is.EqualTo(new[] { true }));
            Assert.That(sim.State.Clock.IsAway, Is.False, "cleared after the away advance");

            probe.AwayFlags.Clear();
            sim.ScheduleJob("probe", 2, sim.State.Clock.SimTime + 10);
            for (int i = 1; i <= 120; i++) sim.Execute(new Sync(Wall0 + 59 + 3 * Hour + 30 * i)); // an hour live
            Assert.That(probe.AwayFlags, Is.Not.Empty);
            Assert.That(probe.AwayFlags, Has.All.False);
            Assert.That(jobs.AwayFlags, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void AwayReport_CollectsTheAdvanceEvents()
        {
            Simulation sim = New(localSeconds: 10 * Hour);
            sim.Execute(new Sync(Wall0 + 10));
            sim.Execute(new Sync(Wall0 + 10 + 30 * Hour));
            IReadOnlyList<SimEvent> report = sim.LastAwayReport;
            Assert.That(report.Last(), Is.InstanceOf<AwayAdvanceCompleted>());
            Assert.That(report.OfType<HourStarted>().Count(), Is.EqualTo(30));
            Assert.That(report.OfType<DayStarted>().Count(), Is.EqualTo(1));
            Assert.That(report.OfType<WeatherSpellStarted>().Count(), Is.EqualTo(5));
        }
    }
}
