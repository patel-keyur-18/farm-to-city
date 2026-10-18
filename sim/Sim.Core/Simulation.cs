using System;
using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Scheduling;
using Sim.Core.Time;
using Sim.Core.Weather;

namespace Sim.Core
{
    /// <summary>
    /// The simulation: the game state plus the rules that change it. State changes only through
    /// <see cref="Execute"/>; the UI reads <see cref="State"/> and the emitted events.
    /// Same seed + same timestamped commands = same state (spec 001, CLAUDE.md determinism rules).
    /// </summary>
    public sealed class Simulation
    {
        public GameState State { get; }
        public SimConfig Config { get; }
        public ClockDef Clock { get { return Config.Clock; } }
        public WeatherSet Weather { get { return Config.Weather; } }

        private readonly List<ISimSystem> _systems = new List<ISimSystem>();
        private readonly Dictionary<string, IJobHandler> _handlers = new Dictionary<string, IJobHandler>(StringComparer.Ordinal);
        private readonly List<SimEvent> _events = new List<SimEvent>();
        private readonly List<SimEvent> _awayReport = new List<SimEvent>();

        private Simulation(SimConfig config, GameState state)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            State = state;

            AddSystem(new TimeSystem());
            AddSystem(new WeatherSystem());
            if (config.Game != null)
            {
                AddSystem(new LandSystem());
                AddSystem(new FarmSystem());
                AddSystem(new MarketSystem());
                AddSystem(new FounderSystem());
                AddSystem(new FinanceSystem());
            }
            foreach (ISimSystem s in config.ExtraSystems) AddSystem(s);
            var handlers = new List<IJobHandler>();
            if (config.Game != null) handlers.AddRange(new IJobHandler[] { new FarmSystem.CropGrowth(), new FarmSystem.GardenGrowth(), new FarmSystem.TreeMaturity(), new FarmSystem.PestCheck(), new MarketSystem.MandiReturn() });
            handlers.AddRange(config.JobHandlers);
            foreach (IJobHandler h in handlers)
            {
                if (_handlers.ContainsKey(h.JobType)) throw new ArgumentException("Duplicate job handler for '" + h.JobType + "'.");
                _handlers.Add(h.JobType, h); // lookup only; never iterated
            }
        }

        /// <summary>Creates a new game at t = localSecondsSinceMidnight (spec 001 §3.2).</summary>
        public static Simulation NewGame(SimConfig config, NewGame command)
        {
            if (command.LocalSecondsSinceMidnight < 0 || command.LocalSecondsSinceMidnight >= ClockDef.SecondsPerDay)
                throw new ArgumentOutOfRangeException(nameof(command), "localSecondsSinceMidnight must be 0–86 399.");

            var state = new GameState { Seed = command.Seed };
            state.Clock.SimTime = command.LocalSecondsSinceMidnight;
            state.Clock.LastWallUtc = command.StartWallUtc;
            var sim = new Simulation(config, state);
            state.CommandLog.Add(state.Clock.SimTime, command);
            WeatherSystem.Initialise(sim);
            if (config.Game != null)
            {
                state.Land = new LandState();
                state.Ledger = new Ledger();
                state.Finance = new FinanceState();
                MarketSystem.Initialise(sim); // first: land prices and building costs read the price index
                LandSystem.Initialise(sim);
                FarmSystem.Initialise(sim);
                FinanceSystem.Initialise(sim);
                if (config.Game.Credit != null) state.Credit = new CreditState { Score = config.Game.Credit.Score.StartScore, NoticeAnnounced = true };
                FounderRules.PayFirstSeason(sim);
            }

            // The first passes are at the next boundary strictly after the start time.
            long t = state.Clock.SimTime;
            foreach (ItemClass pass in new[] { ItemClass.Hourly, ItemClass.Daily, ItemClass.Seasonal, ItemClass.Yearly })
            {
                long period = sim.PassPeriod(pass);
                sim.SchedulePass(pass, (t / period + 1) * period);
            }
            return sim;
        }

        /// <summary>
        /// Rebuilds a simulation from a command log: the first command must be <see cref="Commands.NewGame"/>.
        /// Every command must be accepted again, or the replay has diverged and this throws.
        /// </summary>
        public static Simulation Replay(SimConfig config, IReadOnlyList<Command> commands)
        {
            if (commands == null || commands.Count == 0 || !(commands[0] is NewGame newGame))
                throw new ArgumentException("A replay starts with a NewGame command.", nameof(commands));
            Simulation sim = NewGame(config, newGame);
            for (int i = 1; i < commands.Count; i++)
            {
                CommandResult r = sim.Execute(commands[i]);
                if (!r.Accepted)
                    throw new InvalidOperationException("Replay diverged: command " + i + " (" + commands[i].GetType().Name + ") was " + r + ".");
            }
            return sim;
        }

        /// <summary>Validates and applies a command. Accepted commands are recorded in the command log.</summary>
        public CommandResult Execute(Command command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            long at = State.Clock.SimTime;
            CommandResult result = command.Apply(this);
            if (result.Accepted) State.CommandLog.Add(at, command);
            return result;
        }

        /// <summary>Shorthand for <c>Execute(new AdvanceTo(target, mode))</c>.</summary>
        public CommandResult AdvanceTo(long target, AdvanceMode mode = AdvanceMode.Live)
        {
            return Execute(new AdvanceTo(target, mode));
        }

        /// <summary>Events emitted since the last <see cref="DrainEvents"/>, in order.</summary>
        public IReadOnlyList<SimEvent> PendingEvents { get { return _events; } }

        /// <summary>Returns and clears the pending events.</summary>
        public List<SimEvent> DrainEvents()
        {
            var copy = new List<SimEvent>(_events);
            _events.Clear();
            return copy;
        }

        /// <summary>The events of the most recent away advance, ending with AwayAdvanceCompleted (the While-you-were-away report).</summary>
        public IReadOnlyList<SimEvent> LastAwayReport { get { return _awayReport; } }

        /// <summary>The season index since t = 0: the ledger period of an entry posted now (spec 005 §3.1).</summary>
        public long Period { get { return State.Clock.SimTime / Clock.SecondsPerSeason; } }

        /// <summary>
        /// The period of the time just ended: at a season boundary, the closing season. Daily-pass accruals for the
        /// day that ended post here (prepaid expensing, day labour, home use), so they belong to the season they
        /// were earned or used in (spec 005 AC-9).
        /// </summary>
        internal long AccrualPeriod { get { return State.Clock.SimTime == 0 ? 0 : (State.Clock.SimTime - 1) / Clock.SecondsPerSeason; } }

        /// <summary>The weather of the current spell.</summary>
        public WeatherTypeDef CurrentWeather
        {
            get { return Weather.Types[State.Weather.WeatherOf(Calendar.SpellAt(State.Clock.SimTime, Clock))]; }
        }

        /// <summary>
        /// The forecast for the next forecastSpells spells (spec 001 §3.6). Deterministic: asking twice at the
        /// same sim time gives the same answer.
        /// </summary>
        public IReadOnlyList<ForecastEntry> Forecast()
        {
            return WeatherSystem.ComputeForecast(this, Calendar.SpellAt(State.Clock.SimTime, Clock));
        }

        /// <summary>
        /// Jobs due at or before <paramref name="untilSimTime"/>, in processing order, with their wall-clock due
        /// times (spec 001 §3.7). The platform layer asks for <c>simTime + awayCap</c>: later jobs can't complete
        /// during a single absence.
        /// </summary>
        public List<UpcomingJob> UpcomingJobs(long untilSimTime)
        {
            var list = new List<UpcomingJob>();
            foreach (ScheduledItem item in State.Scheduler.InOrder())
            {
                if (item.DueTime > untilSimTime) break;
                if (item.Class != ItemClass.Job) continue;
                Job job = State.Jobs.Find(item.JobId);
                if (job == null || job.State != JobState.Running) continue;
                list.Add(new UpcomingJob(job.Id, job.Type, job.TargetId, item.DueTime, WallTimeOf(item.DueTime)));
            }
            return list;
        }

        /// <summary>The wall-clock time (Unix seconds, UTC) of a sim time: lastWallUtc + (t − simTime).</summary>
        public long WallTimeOf(long simTime)
        {
            return State.Clock.LastWallUtc + (simTime - State.Clock.SimTime);
        }

        // ---- Used by systems, job handlers and commands ----

        internal void Emit(SimEvent e) { _events.Add(e); }

        /// <summary>Starts a job of a registered type, due at <paramref name="dueTime"/> (≥ the current sim time).</summary>
        internal Job ScheduleJob(string type, long targetId, long dueTime)
        {
            if (!_handlers.TryGetValue(type, out IJobHandler handler))
                throw new ArgumentException("No job handler for '" + type + "'.", nameof(type));
            if (dueTime < State.Clock.SimTime)
                throw new ArgumentOutOfRangeException(nameof(dueTime), "A job can't be due in the past.");
            Job job = State.Jobs.Add(type, targetId, State.Clock.SimTime, dueTime);
            State.Scheduler.Schedule(dueTime, ItemClass.Job, handler.System, targetId, job.Id);
            return job;
        }

        /// <summary>Marks a job Done and removes it from the job table.</summary>
        internal void CompleteJob(Job job)
        {
            job.State = JobState.Done;
            State.Jobs.Remove(job.Id);
        }

        /// <summary>Processes every item due up to <paramref name="target"/> in live mode.</summary>
        internal void AdvanceLive(long target)
        {
            ProcessUntil(target);
        }

        /// <summary>An away advance of <paramref name="applied"/> seconds, reported with AwayAdvanceCompleted.</summary>
        internal void AdvanceAway(long requested, long applied, bool capped)
        {
            long from = State.Clock.SimTime;
            int firstEvent = _events.Count;
            State.Clock.IsAway = true;
            try
            {
                ProcessUntil(from + applied);
            }
            finally
            {
                State.Clock.IsAway = false;
            }
            CreditSystem.AnnounceAfterAway(this); // a default reached while away is shown again on return (ADR 0038)
            Emit(new AwayAdvanceCompleted(State.Clock.SimTime, from, State.Clock.SimTime, requested, applied, capped));
            _awayReport.Clear();
            for (int i = firstEvent; i < _events.Count; i++) _awayReport.Add(_events[i]);
        }

        // ---- Processing ----

        private void ProcessUntil(long target)
        {
            if (target < State.Clock.SimTime) throw new InvalidOperationException("Time never moves backwards.");
            Scheduler scheduler = State.Scheduler;
            while (scheduler.Count > 0 && scheduler.Peek().DueTime <= target)
            {
                ScheduledItem item = scheduler.Pop();
                State.Clock.SimTime = item.DueTime;
                if (item.Class == ItemClass.Job) RunJob(item);
                else RunPass(item);
            }
            State.Clock.SimTime = target;
        }

        private void RunJob(ScheduledItem item)
        {
            Job job = State.Jobs.Find(item.JobId);
            if (job == null || job.State != JobState.Running) return;
            job.State = JobState.Ready;
            _handlers[job.Type].OnDue(this, job);
        }

        private void RunPass(ScheduledItem item)
        {
            foreach (ISimSystem system in _systems) system.OnPass(this, item.Class);
            SchedulePass(item.Class, item.DueTime + PassPeriod(item.Class));
        }

        private void SchedulePass(ItemClass pass, long dueTime)
        {
            State.Scheduler.Schedule(dueTime, pass, SystemOrder.Time, 0, 0);
        }

        private long PassPeriod(ItemClass pass)
        {
            switch (pass)
            {
                case ItemClass.Hourly: return 3600;
                case ItemClass.Daily: return ClockDef.SecondsPerDay;
                case ItemClass.Seasonal: return Clock.SecondsPerSeason;
                case ItemClass.Yearly: return Clock.SecondsPerYear;
                default: throw new ArgumentOutOfRangeException(nameof(pass));
            }
        }

        /// <summary>Inserts a system, keeping the list in system order (stable for equal orders).</summary>
        private void AddSystem(ISimSystem system)
        {
            int i = _systems.Count;
            while (i > 0 && _systems[i - 1].Order > system.Order) i--;
            _systems.Insert(i, system);
        }
    }
}
