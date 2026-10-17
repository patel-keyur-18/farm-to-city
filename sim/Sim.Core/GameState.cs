using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core
{
    /// <summary>
    /// Everything the simulation persists (spec 001 §4). The UI only reads it; it changes only through commands.
    /// </summary>
    public sealed class GameState
    {
        /// <summary>Bumped whenever the canonical write order changes, so stale golden hashes fail loudly.</summary>
        public const int HashFormatVersion = 1;

        /// <summary>The world seed, set at new game.</summary>
        public ulong Seed { get; internal set; }

        public ClockState Clock { get; internal set; } = new ClockState();

        /// <summary>
        /// Writes the canonical byte stream: a fixed field order, entities in id order.
        /// Each later spec appends its own section. The command log is input, not state, and is never hashed.
        /// </summary>
        public void WriteTo(StateHasher h)
        {
            h.WriteInt(HashFormatVersion);
            h.WriteULong(Seed);
            Clock.WriteTo(h);
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
            return new GameState { Seed = Seed, Clock = Clock.Clone() };
        }
    }
}
