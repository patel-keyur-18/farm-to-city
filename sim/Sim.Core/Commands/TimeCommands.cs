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
