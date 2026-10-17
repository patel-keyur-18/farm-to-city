using System.Collections.Generic;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class SchedulerTests
    {
        // Spec 001 AC-9.
        [Test]
        public void SameTimestamp_ProcessingOrder()
        {
            var log = new List<string>();
            var probe = new ProbeSystem(SystemOrder.Agriculture);
            var agri = new ProbeJobHandler("agri", SystemOrder.Agriculture, log);
            var market = new ProbeJobHandler("market", SystemOrder.Market, log);
            Simulation sim = New(systems: new[] { probe }, handlers: new IJobHandler[] { agri, market });

            long t = 4 * Day; // an hour, day and season boundary at once
            // Scheduled in an order that differs from the expected processing order.
            sim.ScheduleJob("market", 5, t);
            sim.ScheduleJob("agri", 9, t);
            sim.ScheduleJob("agri", 3, t);
            sim.AdvanceTo(t - 1);
            probe.Log.Clear();

            sim.AdvanceTo(t);
            var all = new List<string>(log);
            all.AddRange(probe.Log);
            Assert.That(log, Is.EqualTo(new[]
            {
                "Job(agri, entity=3)@" + t,
                "Job(agri, entity=9)@" + t,
                "Job(market, entity=5)@" + t,
            }));
            Assert.That(probe.Log, Is.EqualTo(new[] { "Hourly@" + t, "Daily@" + t, "Seasonal@" + t }));

            // Jobs come before passes, and passes come in class order, in the emitted events too.
            List<string> events = Describe(sim.DrainEvents());
            Assert.That(events, Has.Some.Contains("HourStarted"));
            int hour = events.FindIndex(e => e.Contains("HourStarted"));
            int day = events.FindIndex(e => e.Contains("DayStarted"));
            int season = events.FindIndex(e => e.Contains("SeasonStarted"));
            Assert.That(hour, Is.LessThan(day));
            Assert.That(day, Is.LessThan(season));
        }

        [Test]
        public void YearBoundary_RunsAllPassesInOrder()
        {
            var probe = new ProbeSystem();
            Simulation sim = New(systems: new[] { probe });
            long year = sim.Clock.SecondsPerYear;
            sim.AdvanceTo(year - 1);
            probe.Log.Clear();
            sim.AdvanceTo(year);
            Assert.That(probe.Log, Is.EqualTo(new[] { "Hourly@" + year, "Daily@" + year, "Seasonal@" + year, "Yearly@" + year }));
            Assert.That(Describe(sim.DrainEvents()), Has.Some.EqualTo(year + " YearStarted(year=2)"));
        }

        // Spec 001 AC-10.
        [Test]
        public void Job_FiresOnceAtDueTime()
        {
            var log = new List<string>();
            Simulation sim = New(handlers: new[] { new ProbeJobHandler("probe", SystemOrder.Agriculture, log) });
            long t = 5 * Hour + 17;
            Job job = sim.ScheduleJob("probe", 1, t);
            Assert.That(job.State, Is.EqualTo(JobState.Running));

            sim.AdvanceTo(t - 1);
            Assert.That(log, Is.Empty);
            Assert.That(sim.State.Jobs.Find(job.Id), Is.Not.Null);

            sim.AdvanceTo(t);
            Assert.That(log, Is.EqualTo(new[] { "Job(probe, entity=1)@" + t }));

            sim.AdvanceTo(t + 10 * Day);
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(job.State, Is.EqualTo(JobState.Done));
            Assert.That(sim.State.Jobs.Find(job.Id), Is.Null);
        }

        [Test]
        public void NewGame_StartsAtLocalTime_FirstPassesAtNextBoundary()
        {
            var probe = new ProbeSystem();
            Simulation sim = New(localSeconds: 14 * Hour + 5 * 60, systems: new[] { probe });
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(14 * Hour + 300));
            Assert.That(sim.State.Clock.LastWallUtc, Is.EqualTo(Wall0));
            sim.AdvanceTo(15 * Hour);
            Assert.That(probe.Log, Is.EqualTo(new[] { "Hourly@" + 15 * Hour }));
            Assert.That(sim.State.CommandLog.Entries[0].Command, Is.InstanceOf<Commands.NewGame>());
        }

        // Spec 001 AC-11.
        [Test]
        public void ChunkingInvariance()
        {
            Simulation Build()
            {
                Simulation s = New(seed: 7, localSeconds: 9 * Hour + 30, handlers: new[] { new DummyJobHandler() });
                s.ScheduleJob(DummyJobHandler.Type, 1, s.State.Clock.SimTime + 2 * Hour);
                s.ScheduleJob(DummyJobHandler.Type, 2, s.State.Clock.SimTime + 3 * Hour + 11);
                s.ScheduleJob(DummyJobHandler.Type, 3, 12 * Hour); // on an hour boundary
                return s;
            }

            Simulation one = Build();
            long end = one.State.Clock.SimTime + one.Clock.SecondsPerYear;
            Assert.That(one.AdvanceTo(end).Accepted, Is.True);
            List<string> oneEvents = Describe(one.DrainEvents());

            Simulation many = Build();
            long start = many.State.Clock.SimTime;
            var targets = new List<long>();
            for (ulong i = 0; i < 499; i++)
                targets.Add(start + (long)Rng.Below(Rng.Next(7, (RngStream)901, i, 0), (ulong)(end - start)));
            targets.Sort();
            targets.Add(end);
            var manyEvents = new List<string>();
            foreach (long target in targets)
            {
                Assert.That(many.AdvanceTo(target).Accepted, Is.True);
                manyEvents.AddRange(Describe(many.DrainEvents()));
            }

            Assert.That(targets.Count, Is.EqualTo(500));
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
            Assert.That(manyEvents, Is.EqualTo(oneEvents));
            Assert.That(oneEvents.Count, Is.GreaterThan(16 * 24), "a year of hourly events at least");
            Assert.That(one.State.Jobs.All.Count, Is.EqualTo(3), "each dummy job keeps one successor alive");
        }

        // Spec 001 AC-12.
        [Test]
        public void Commands_ApplyAfterDueItems_RejectPast()
        {
            var log = new List<string>();
            Simulation sim = New(handlers: new[] { new ProbeJobHandler("probe", SystemOrder.Agriculture, log) });
            long t = 2 * Hour + 30;
            sim.ScheduleJob("probe", 1, t);

            Assert.That(sim.Execute(new MarkCommand(t, "a", log)).Accepted, Is.True);
            Assert.That(sim.Execute(new MarkCommand(t, "b", log)).Accepted, Is.True);
            Assert.That(log, Is.EqualTo(new[] { "Job(probe, entity=1)@" + t, "Command(a)@" + t, "Command(b)@" + t }),
                "the due item first, then same-time commands in submission order");

            int logged = sim.State.CommandLog.Entries.Count;
            Assert.That(sim.Execute(new MarkCommand(t - 1, "late", log)).Reason, Is.EqualTo(RejectReason.TimeInPast));
            Assert.That(sim.AdvanceTo(t - 1).Reason, Is.EqualTo(RejectReason.TimeInPast));
            Assert.That(sim.State.CommandLog.Entries.Count, Is.EqualTo(logged), "rejected commands aren't logged");
            Assert.That(log.Count, Is.EqualTo(3));
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(t));
        }
    }
}
