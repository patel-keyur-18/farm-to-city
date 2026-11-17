using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    public enum RoadStatus
    {
        None = 0,
        Building = 1,
        Built = 2,
    }

    /// <summary>
    /// The Township's own state (spec 023 §4, save schema 20): the link road now; the clinic, the gate, the cohort and the civic posts join it
    /// by story. A town that has started nothing hashes as before, so the golden replays and older towns are unchanged.
    /// </summary>
    public sealed class TownshipState
    {
        public RoadState Road { get; internal set; } = new RoadState();

        /// <summary>Nothing of the Township has started.</summary>
        internal bool IsDefault { get { return Road.Status == RoadStatus.None; } }

        internal TownshipState Clone() { return new TownshipState { Road = Road.Clone() }; }

        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && IsDefault) return;
            Road.WriteTo(h);
        }

        internal static TownshipState ReadFrom(SaveReader r) { return new TownshipState { Road = RoadState.ReadFrom(r) }; }
    }

    /// <summary>The paved link road (spec 023 §3.3): petitioned, built by the district in <c>days</c>, never the player's.</summary>
    public sealed class RoadState
    {
        public RoadStatus Status { get; internal set; }

        public long StartedAt { get; internal set; }
        public long ReadyAt { get; internal set; }
        public long JobId { get; internal set; }

        /// <summary>The share given, indexed at the press.</summary>
        public long Share { get; internal set; }

        internal RoadState Clone() { return (RoadState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt((int)Status);
            h.WriteLong(StartedAt);
            h.WriteLong(ReadyAt);
            h.WriteLong(JobId);
            h.WriteLong(Share);
        }

        internal static RoadState ReadFrom(SaveReader r)
        {
            return new RoadState { Status = (RoadStatus)r.ReadInt(), StartedAt = r.ReadLong(), ReadyAt = r.ReadLong(), JobId = r.ReadLong(), Share = r.ReadLong() };
        }
    }
}
