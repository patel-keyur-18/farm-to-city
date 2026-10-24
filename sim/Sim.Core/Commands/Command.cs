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
        UnknownGood,
        NotProduce,
        InvalidQuantity,
        InsufficientStock,
        NotOffered,
        NotAnInput,
        OverCartCapacity,
        TripUnderway,
        AlreadyWentToday,
        FounderSick,

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
        UnknownSlot,
        SlotOccupied,
        UnknownTree,
        TerrainNotAllowed,
        NoTree,
        NoRipeFruit,
        UnknownPlan,
        UnknownOption,
        InvalidShare,
        NotGrowing,
        AlreadySprayed,
        NoPestThreat,
        NotSelectable,
        AlreadyTreatedToday,

        // Spec 005 (credit, ADR 0038)
        UnknownLender,
        InvalidAmount,
        BelowMinimum,
        InvalidTerm,
        ScoreTooLow,
        OverLimit,
        UnknownLoan,
        LoanClosed,
        LoanDefaulted,
        NoArrears,
        AlreadyRestructured,
        LenderDoesNotRestructure,
        LoanSecured,

        // Spec 009 (onboarding)
        UnknownChapter,
        UnknownStep,
        NotCurrentStep,

        // Spec 006 (people, ADR 0039)
        NoneAvailable,
        NotQuarters,
        NoRoom,
        TooManyHuts,
        NoHut,
        AlreadyPosted,
        UnknownApplicant,
        AlreadyInterviewed,
        NoEmptyHut,
        NoHand,
        NotLapsed,
        UnknownTarget,
        TooManyOrders,
        NoOrder,

        /// <summary>The village's land, or a neighbour's farm not for sale yet (spec 010 §3.1).</summary>
        NotForSale,

        // Spec 011 (the trader's truck and supermarket orders)
        /// <summary>The truck has left (full, or between midnight and 6:00): "back at 6:00".</summary>
        TraderAway,
        UnknownOrder,
        /// <summary>The order lapsed, or deliveries are closed (midnight to 6:00).</summary>
        OrderClosed,
        /// <summary>More than what's left of the order.</summary>
        OrderFilled,

        // Spec 012 (cattle)
        /// <summary>Before the era the shed unlocks in (the Hamlet).</summary>
        EraLocked,
        AlreadyBuilt,
        NoShed,
        ShedFull,
        UnknownAnimal,
        /// <summary>The feed room, the straw stack or the compost pit has no room.</summary>
        PlaceFull,
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

        /// <summary>
        /// Applies the effect at the current sim time from inside a pass, without advancing time or logging: a hand's
        /// standing orders reuse the player's commands this way (spec 006 §3.8). Deterministic from the state.
        /// </summary>
        internal CommandResult ApplyNow(Simulation sim) { return ApplyAt(sim); }
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

        /// <summary>
        /// The state at the last checkpoint, as a save file without a log (spec 007 §3.2): replay starts here, or from the new
        /// game while this is null. Not hashed: like the log, it's history, not state.
        /// </summary>
        public byte[] Base { get; internal set; }

        internal void Add(long simTime, Command command) { _entries.Add(new CommandLogEntry(simTime, command)); }

        /// <summary>A live Sync from <paramref name="fromWallUtc"/>: extends the last entry when it is the run just before it (spec 007 §3.2).</summary>
        internal void AddLive(long simTime, long fromWallUtc, long toWallUtc)
        {
            int last = _entries.Count - 1;
            if (last >= 0 && _entries[last].Command is LiveRun run && run.ToWallUtc == fromWallUtc)
            {
                // Entries are shared between clones: replace the run, never change it.
                _entries[last] = new CommandLogEntry(_entries[last].SimTime, new LiveRun(run.FromWallUtc, toWallUtc, run.SimSeconds + (toWallUtc - fromWallUtc)));
                return;
            }
            _entries.Add(new CommandLogEntry(simTime, new LiveRun(fromWallUtc, toWallUtc, toWallUtc - fromWallUtc)));
        }

        /// <summary>The checkpoint: the base becomes <paramref name="baseSave"/> and the log restarts empty.</summary>
        internal void Checkpoint(byte[] baseSave)
        {
            Base = baseSave;
            _entries.Clear();
        }

        internal CommandLog Clone()
        {
            var c = new CommandLog { Base = Base };
            c._entries.AddRange(_entries);
            return c;
        }
    }
}
