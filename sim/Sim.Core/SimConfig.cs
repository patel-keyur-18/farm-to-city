using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Scheduling;

namespace Sim.Core
{
    /// <summary>Runtime options that aren't game data.</summary>
    public sealed class SimOptions
    {
        /// <summary>Allows DebugAdvance (spec 001 §3.8). Development builds only; release builds never set it.</summary>
        public bool DevCommandsEnabled { get; set; }

        /// <summary>
        /// A failed ledger reconciliation throws (tests, development). When false it is only reported with
        /// <c>LedgerReconciliationFailed</c>, for player builds to log (spec 005 §3.1). Default true.
        /// </summary>
        public bool StrictChecks { get; set; } = true;
    }

    /// <summary>
    /// Everything needed to build a simulation besides the state: content, options, and the systems and job
    /// handlers from later specs (or tests). A replay must use the same configuration.
    /// </summary>
    public sealed class SimConfig
    {
        public ClockDef Clock { get; }
        public WeatherSet Weather { get; }
        public SimOptions Options { get; }
        public IReadOnlyList<ISimSystem> ExtraSystems { get; }
        public IReadOnlyList<IJobHandler> JobHandlers { get; }

        /// <summary>Land, map, buildings, start and ledger content, or null for a time-and-weather-only sim.</summary>
        public GameDefs Game { get; }

        public SimConfig(ClockDef clock, WeatherSet weather, SimOptions options = null,
            IReadOnlyList<ISimSystem> extraSystems = null, IReadOnlyList<IJobHandler> jobHandlers = null,
            GameDefs game = null)
        {
            Game = game;
            Clock = clock;
            Weather = weather;
            Options = options ?? new SimOptions();
            ExtraSystems = extraSystems ?? new ISimSystem[0];
            JobHandlers = jobHandlers ?? new IJobHandler[0];
        }
    }
}
