using Sim.Core.Maths;

namespace Sim.Core.Time
{
    /// <summary>The clock part of the game state (spec 001 §4).</summary>
    public sealed class ClockState
    {
        /// <summary>Sim time in seconds. t = 0 is Spring, Day 1, Year 1, 00:00.</summary>
        public long SimTime { get; internal set; }

        /// <summary>The wall clock (Unix seconds, UTC) at the last <c>Sync</c>.</summary>
        public long LastWallUtc { get; internal set; }

        /// <summary>True only while an away advance is being processed.</summary>
        public bool IsAway { get; internal set; }

        internal ClockState Clone()
        {
            return new ClockState { SimTime = SimTime, LastWallUtc = LastWallUtc, IsAway = IsAway };
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(SimTime);
            h.WriteLong(LastWallUtc);
            h.WriteBool(IsAway);
        }
    }
}
