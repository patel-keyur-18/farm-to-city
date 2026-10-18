using System.Collections.Generic;

namespace Sim.Core.Commands
{
    /// <summary>Why a command was rejected.</summary>
    public enum RejectReason
    {
        None = 0,
        TimeInPast,
        DevCommandsDisabled,
        InvalidStep,
        InvalidArgument,

        // Spec 002 (land)
        UnknownParcel,
        AlreadyOwned,
        NotAdjacent,
        InsufficientFunds,
        NotOwned,
        Protected,
        ParcelInUse,
        WouldStrandLand,

        // Spec 004 (goods and store)
        StoreFull,
        TraderFull,

        // Spec 003 (crops, garden, trees, founder)
        UnknownTile,
        NotFarmTile,
        TileOccupied,
        UnknownCrop,
        OutOfSeason,
        UnknownFertiliser,
        SeedCannotBeSaved,
        NoSavedSeed,
        NoCrop,
        NotReady,
        UnknownBed,
        BedBusy,
        BedRested,
    }

    /// <summary>The outcome of <c>Simulation.Execute</c>.</summary>
    public readonly struct CommandResult
    {
        public RejectReason Reason { get; }
        public bool Accepted { get { return Reason == RejectReason.None; } }

        private CommandResult(RejectReason reason) { Reason = reason; }

        public static readonly CommandResult Ok = new CommandResult(RejectReason.None);

        public static CommandResult Reject(RejectReason reason) { return new CommandResult(reason); }

        public override string ToString() { return Accepted ? "Ok" : "Rejected(" + Reason + ")"; }
    }

    /// <summary>
    /// A request to change the state (spec 001 §5). Commands are immutable, so the command log can be
    /// replayed exactly. Like a command object in CQRS: the only way state changes.
    /// </summary>
    public abstract class Command
    {
        internal abstract CommandResult Apply(Simulation sim);
    }

    /// <summary>
    /// A command stamped with the sim time it applies at (spec 001 §3.3). It runs after every item due at
    /// or before <see cref="At"/>; a stamp earlier than the current sim time is rejected with TimeInPast.
    /// Game commands from later specs derive from this.
    /// </summary>
    public abstract class TimedCommand : Command
    {
        public long At { get; }

        protected TimedCommand(long at) { At = at; }

        /// <summary>Validates against the state at <see cref="At"/> and applies the effect.</summary>
        protected abstract CommandResult ApplyAt(Simulation sim);

        internal sealed override CommandResult Apply(Simulation sim)
        {
            if (At < sim.State.Clock.SimTime) return CommandResult.Reject(RejectReason.TimeInPast);
            sim.AdvanceLive(At);
            return ApplyAt(sim);
        }
    }

    /// <summary>One accepted command and the sim time it was executed at.</summary>
    public readonly struct CommandLogEntry
    {
        public long SimTime { get; }
        public Command Command { get; }

        public CommandLogEntry(long simTime, Command command)
        {
            SimTime = simTime;
            Command = command;
        }
    }

    /// <summary>Every accepted command, in order (spec 001 §4). Input for replay; not part of the state hash.</summary>
    public sealed class CommandLog
    {
        private readonly List<CommandLogEntry> _entries = new List<CommandLogEntry>();

        public IReadOnlyList<CommandLogEntry> Entries { get { return _entries; } }

        internal void Add(long simTime, Command command) { _entries.Add(new CommandLogEntry(simTime, command)); }

        internal CommandLog Clone()
        {
            var c = new CommandLog();
            c._entries.AddRange(_entries);
            return c;
        }
    }
}
