using Sim.Core.Time;

namespace Sim.Core.Commands
{
    /// <summary>Starts a new game (spec 001 §5). Always the first entry of a command log.</summary>
    public sealed class NewGame : Command
    {
        public ulong Seed { get; }
        public long StartWallUtc { get; }
        public long LocalSecondsSinceMidnight { get; }

        public NewGame(ulong seed, long startWallUtc, long localSecondsSinceMidnight)
        {
            Seed = seed;
            StartWallUtc = startWallUtc;
            LocalSecondsSinceMidnight = localSecondsSinceMidnight;
        }

        internal override CommandResult Apply(Simulation sim)
        {
            // A simulation is created by Simulation.NewGame; this command can't be applied twice.
            return CommandResult.Reject(RejectReason.InvalidArgument);
        }
    }

    /// <summary>
    /// Processes every due item up to <see cref="Target"/> (spec 001 §3.3). Used by Sync, tests and tools.
    /// In Away mode the advance is reported with <c>AwayAdvanceCompleted</c> and no cap is applied.
    /// </summary>
    public sealed class AdvanceTo : Command
    {
        public long Target { get; }
        public AdvanceMode Mode { get; }

        public AdvanceTo(long target, AdvanceMode mode = AdvanceMode.Live)
        {
            Target = target;
            Mode = mode;
        }

        internal override CommandResult Apply(Simulation sim)
        {
            long now = sim.State.Clock.SimTime;
            if (Target < now) return CommandResult.Reject(RejectReason.TimeInPast);
            if (Mode == AdvanceMode.Away) sim.AdvanceAway(Target - now, Target - now, false);
            else sim.AdvanceLive(Target);
            return CommandResult.Ok;
        }
    }
}

namespace Sim.Core.Commands
{
    using Sim.Core.Definitions;
    using Sim.Core.Events;

    /// <summary>
    /// Advances the sim by the wall-clock time since the last Sync (spec 001 §3.4). The platform layer
    /// calls it every frame (or at least once a second) and on every resume.
    /// <list type="bullet">
    /// <item>0 ≤ Δ ≤ awayThresholdSeconds: live, Δ seconds.</item>
    /// <item>Δ above the threshold: away, Δ seconds, capped to the away cap by dropping whole days.</item>
    /// <item>Δ &lt; 0 (the clock went backwards): nothing is granted and ClockAnomalyDetected is emitted.</item>
    /// </list>
    /// Afterwards lastWallUtc = nowWallUtc in every case (re-anchoring).
    /// </summary>
    public sealed class Sync : Command
    {
        public long NowWallUtc { get; }

        public Sync(long nowWallUtc) { NowWallUtc = nowWallUtc; }

        internal override CommandResult Apply(Simulation sim)
        {
            Time.ClockState clock = sim.State.Clock;
            long delta = NowWallUtc - clock.LastWallUtc;
            if (delta < 0)
            {
                sim.Emit(new ClockAnomalyDetected(clock.SimTime, clock.LastWallUtc, NowWallUtc));
            }
            else if (delta <= sim.Clock.AwayThresholdSeconds)
            {
                sim.AdvanceLive(clock.SimTime + delta);
            }
            else
            {
                long applied = AwayCap.Apply(delta, sim.Clock);
                sim.AdvanceAway(delta, applied, applied != delta);
            }
            clock.LastWallUtc = NowWallUtc;
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// A run of consecutive live <see cref="Sync"/>s, compacted into one command-log entry (spec 007 §3.2): advances live by
    /// <see cref="SimSeconds"/> and re-anchors the wall clock to <see cref="ToWallUtc"/>. Live advances are chunking-invariant,
    /// so a run replays exactly like the Syncs it stands for.
    /// </summary>
    public sealed class LiveRun : Command
    {
        public long FromWallUtc { get; }
        public long ToWallUtc { get; }
        public long SimSeconds { get; }

        public LiveRun(long fromWallUtc, long toWallUtc, long simSeconds)
        {
            FromWallUtc = fromWallUtc;
            ToWallUtc = toWallUtc;
            SimSeconds = simSeconds;
        }

        internal override CommandResult Apply(Simulation sim)
        {
            Time.ClockState clock = sim.State.Clock;
            if (clock.LastWallUtc != FromWallUtc || SimSeconds != ToWallUtc - FromWallUtc || SimSeconds < 0)
                return CommandResult.Reject(RejectReason.InvalidArgument);
            sim.AdvanceLive(clock.SimTime + SimSeconds);
            clock.LastWallUtc = ToWallUtc;
            return CommandResult.Ok;
        }
    }

    /// <summary>The away cap (spec 001 §3.4).</summary>
    public static class AwayCap
    {
        /// <summary>
        /// Seconds actually applied for <paramref name="requested"/> seconds away: unchanged up to the cap C;
        /// above it, requested − k × 86 400 with the smallest whole k that brings it to ≤ C. Dropping whole days
        /// keeps the sim's time of day aligned with the device's. The result lies in (C − 24 h, C].
        /// </summary>
        public static long Apply(long requested, ClockDef clock)
        {
            long cap = clock.AwayCapSeconds;
            if (requested <= cap) return requested;
            long days = (requested - cap + ClockDef.SecondsPerDay - 1) / ClockDef.SecondsPerDay;
            return requested - days * ClockDef.SecondsPerDay;
        }
    }
}

namespace Sim.Core.Commands
{
    using Sim.Core.Definitions;
    using Sim.Core.Time;

    /// <summary>
    /// Developer time travel (spec 001 §3.8). Rejected with DevCommandsDisabled unless
    /// <c>SimOptions.DevCommandsEnabled</c>. Allowed steps, in either mode: +1 hour, +1 day, +1 season and the
    /// away cap (owner decision, 2026-10-17). Live advances exactly; Away applies the away cap like Sync.
    /// lastWallUtc is unchanged, so the next Sync carries on from the new sim time.
    /// </summary>
    public sealed class DebugAdvance : Command
    {
        public long Seconds { get; }
        public AdvanceMode Mode { get; }

        public DebugAdvance(long seconds, AdvanceMode mode)
        {
            Seconds = seconds;
            Mode = mode;
        }

        internal override CommandResult Apply(Simulation sim)
        {
            if (!sim.Config.Options.DevCommandsEnabled) return CommandResult.Reject(RejectReason.DevCommandsDisabled);
            ClockDef clock = sim.Clock;
            if (Seconds != 3600 && Seconds != ClockDef.SecondsPerDay && Seconds != clock.SecondsPerSeason && Seconds != clock.AwayCapSeconds)
                return CommandResult.Reject(RejectReason.InvalidStep);

            if (Mode == AdvanceMode.Away)
            {
                long applied = AwayCap.Apply(Seconds, clock);
                sim.AdvanceAway(Seconds, applied, applied != Seconds);
            }
            else
            {
                sim.AdvanceLive(sim.State.Clock.SimTime + Seconds);
            }
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Developer: moves the town to the next era now (ADR 0076), as its gate would, so a later era's buildings can be tested on
    /// the device before that gate is built. Rejected with DevCommandsDisabled unless dev commands are on, and InvalidStep at the
    /// last era.
    /// </summary>
    public sealed class DebugNextEra : Command
    {
        internal override CommandResult Apply(Simulation sim)
        {
            if (!sim.Config.Options.DevCommandsEnabled) return CommandResult.Reject(RejectReason.DevCommandsDisabled);
            return Progress.EraRules.Unlock(sim) ? CommandResult.Ok : CommandResult.Reject(RejectReason.InvalidStep);
        }
    }
}
