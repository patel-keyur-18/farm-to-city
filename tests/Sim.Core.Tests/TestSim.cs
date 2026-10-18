using System.Collections.Generic;
using System.Linq;
using Sim.Core.Commands;
using Sim.Core.Definitions;
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

        public static SimConfig Config(SimOptions options = null, IEnumerable<ISimSystem> systems = null,
            IEnumerable<IJobHandler> handlers = null, GameDefs game = null)
        {
            return new SimConfig(TestContent.Clock(), TestContent.Weather(), options,
                systems?.ToList(), handlers?.ToList(), game);
        }

        /// <summary>A new game with land and the ledger (the default content unless <paramref name="game"/> is given).</summary>
        public static Simulation NewFarm(ulong seed = 42, long localSeconds = 0, GameDefs game = null, SimOptions options = null)
        {
            return Simulation.NewGame(Config(options, game: game ?? TestContent.Game()), new NewGame(seed, Wall0, localSeconds));
        }

        /// <summary>The default content with 20,00,000 coins of starting cash, for tests that spend freely.</summary>
        public static GameDefs RichGame(System.Func<string, string, string> edit = null)
        {
            return TestContent.Game((f, j) =>
            {
                if (f == "start.json") j = j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000");
                return edit == null ? j : edit(f, j);
            });
        }

        /// <summary>
        /// A content edit that turns spoilage off (every spoilagePerDay 0), for tests that count exact grams of
        /// something else; spoilage has its own tests.
        /// </summary>
        public static string NoSpoilage(string file, string json)
        {
            return file == "goods.json" ? System.Text.RegularExpressions.Regex.Replace(json, "\"spoilagePerDay\": [0-9.]+", "\"spoilagePerDay\": 0") : json;
        }

        /// <summary>Content with no inflation (the price index stays at its start), for tests of year-0 examples.</summary>
        public static GameDefs NoInflation(System.Func<string, string, string> edit = null)
        {
            return TestContent.Game((f, j) =>
            {
                if (f == "market.json") j = j.Replace("\"inflationMinPerYear\": 0.02, \"inflationMaxPerYear\": 0.06", "\"inflationMinPerYear\": 0, \"inflationMaxPerYear\": 0");
                return edit == null ? j : edit(f, j);
            });
        }

        /// <summary>
        /// Rich content without the household's money flows: the founder starts on subsistence (no living costs) and
        /// doesn't eat from the store. For ledger and store tests that count every coin.
        /// </summary>
        public static GameDefs Quiet(System.Func<string, string, string> edit = null)
        {
            return RichGame((f, j) =>
            {
                if (f == "founder.json")
                    j = j.Replace("\"startDietPlan\": \"basic\"", "\"startDietPlan\": \"subsistence\"")
                        .Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false");
                return edit == null ? j : edit(f, j);
            });
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

    /// <summary>A test-only stamped command that records when it was applied.</summary>
    public sealed class MarkCommand : TimedCommand
    {
        private readonly List<string> _log;
        public string Name { get; }

        public MarkCommand(long at, string name, List<string> log) : base(at)
        {
            Name = name;
            _log = log;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            _log.Add("Command(" + Name + ")@" + sim.State.Clock.SimTime);
            return CommandResult.Ok;
        }
    }
}
