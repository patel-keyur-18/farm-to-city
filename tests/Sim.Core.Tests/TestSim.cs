using System.Collections.Generic;
using System.Linq;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Scheduling;

namespace Sim.Core.Tests
{
    /// <summary>Builds simulations from the real content files, plus test-only systems and job handlers.</summary>
    public static class TestSim
    {
        public const long Day = 86400;
        public const long Hour = 3600;
        public const long Wall0 = 1_800_000_000; // an arbitrary Unix time

        public static SimConfig Config(SimOptions options = null, IEnumerable<ISimSystem> systems = null, IEnumerable<IJobHandler> handlers = null)
        {
            return new SimConfig(TestContent.Clock(), TestContent.Weather(), options,
                systems?.ToList(), handlers?.ToList());
        }

        public static Simulation New(ulong seed = 42, long localSeconds = 0, SimOptions options = null,
            IEnumerable<ISimSystem> systems = null, IEnumerable<IJobHandler> handlers = null)
        {
            return Simulation.NewGame(Config(options, systems, handlers), new NewGame(seed, Wall0, localSeconds));
        }

        public static List<string> Describe(IEnumerable<SimEvent> events) { return events.Select(e => e.ToString()).ToList(); }
    }

    /// <summary>Records every pass it sees, and whether the sim was away at the time.</summary>
    public sealed class ProbeSystem : ISimSystem
    {
        public readonly List<string> Log = new List<string>();
        public readonly List<bool> AwayFlags = new List<bool>();

        public ProbeSystem(SystemOrder order = SystemOrder.Agriculture) { Order = order; }

        public SystemOrder Order { get; }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            Log.Add(pass + "@" + sim.State.Clock.SimTime);
            AwayFlags.Add(sim.State.Clock.IsAway);
        }
    }

    /// <summary>A job type that records when it runs and then completes.</summary>
    public sealed class ProbeJobHandler : IJobHandler
    {
        private readonly List<string> _log;
        public readonly List<bool> AwayFlags = new List<bool>();

        public ProbeJobHandler(string type, SystemOrder system, List<string> log)
        {
            JobType = type;
            System = system;
            _log = log;
        }

        public string JobType { get; }
        public SystemOrder System { get; }

        public void OnDue(Simulation sim, Job job)
        {
            _log.Add("Job(" + JobType + ", entity=" + job.TargetId + ")@" + sim.State.Clock.SimTime);
            AwayFlags.Add(sim.State.Clock.IsAway);
            sim.CompleteJob(job);
        }
    }

    /// <summary>
    /// A job that, when due, starts its successor on the same target after a seeded random 1–12 h
    /// (plus a few odd seconds, so it rarely lands on a boundary). Used by the determinism tests.
    /// </summary>
    public sealed class DummyJobHandler : IJobHandler
    {
        public const string Type = "test.dummy";
        private const RngStream Stream = (RngStream)900;

        public string JobType { get { return Type; } }
        public SystemOrder System { get { return SystemOrder.Production; } }

        public void OnDue(Simulation sim, Job job)
        {
            ulong r = Rng.Next(sim.State.Seed, Stream, (ulong)job.Id, 0);
            long duration = (long)Rng.Below(r, 12 * 3600) + 3600 + 7;
            sim.ScheduleJob(Type, job.TargetId, sim.State.Clock.SimTime + duration);
            sim.CompleteJob(job);
        }
    }
}
