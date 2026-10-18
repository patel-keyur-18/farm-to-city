using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using Sim.Core.Time;
using Sim.Core.Weather;

namespace Sim.Core
{
    /// <summary>
    /// Everything the simulation persists (spec 001 §4). The UI only reads it; it changes only through commands.
    /// </summary>
    public sealed class GameState
    {
        /// <summary>Bumped whenever the canonical write order changes, so stale golden hashes fail loudly.</summary>
        public const int HashFormatVersion = 2;

        /// <summary>The world seed, set at new game.</summary>
        public ulong Seed { get; internal set; }

        public ClockState Clock { get; internal set; } = new ClockState();

        public Scheduler Scheduler { get; internal set; } = new Scheduler();

        public JobTable Jobs { get; internal set; } = new JobTable();

        public WeatherState Weather { get; internal set; } = new WeatherState();

        /// <summary>Land and soil (spec 002); null in a time-and-weather-only sim.</summary>
        public LandState Land { get; internal set; }

        /// <summary>The founder's ledger (spec 005); null in a time-and-weather-only sim.</summary>
        public Ledger Ledger { get; internal set; }

        /// <summary>Accepted commands, for replay. Not hashed: it's input, not state.</summary>
        public CommandLog CommandLog { get; internal set; } = new CommandLog();

        /// <summary>
        /// Writes the canonical byte stream: a fixed field order, entities in id order.
        /// Each later spec appends its own section at the end. The command log is never hashed.
        /// </summary>
        public void WriteTo(StateHasher h)
        {
            h.WriteInt(HashFormatVersion);
            h.WriteULong(Seed);
            Clock.WriteTo(h);
            Scheduler.WriteTo(h);
            Jobs.WriteTo(h);
            Weather.WriteTo(h);
            // Spec 002 and 005 sections, present only when the sim has game content, so a
            // time-and-weather-only state (golden 001) hashes as before.
            if (Land != null) Land.WriteTo(h);
            if (Ledger != null) Ledger.WriteTo(h);
        }

        /// <summary>The 64-bit FNV-1a hash of the canonical state.</summary>
        public ulong ComputeHash()
        {
            var h = new StateHasher();
            WriteTo(h);
            return h.Value;
        }

        /// <summary>A deep copy (in-memory round-trip; saving to disk is spec 007).</summary>
        public GameState Clone()
        {
            return new GameState
            {
                Seed = Seed,
                Clock = Clock.Clone(),
                Scheduler = Scheduler.Clone(),
                Jobs = Jobs.Clone(),
                Weather = Weather.Clone(),
                Land = Land?.Clone(),
                Ledger = Ledger?.Clone(),
                CommandLog = CommandLog.Clone(),
            };
        }
    }
}
