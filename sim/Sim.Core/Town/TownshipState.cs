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
    /// The Township's own state (spec 023 §4, save schema 20): the link road and the clinic with its doctor; the gate, the cohort and the civic posts join it
    /// by story. A town that has started nothing hashes as before, so the golden replays and older towns are unchanged.
    /// </summary>
    public sealed class TownshipState
    {
        public RoadState Road { get; internal set; } = new RoadState();

        public ClinicState Clinic { get; internal set; } = new ClinicState();

        /// <summary>The gate's goals met so far, a <see cref="TownshipGoal"/> mask (spec 023 §3.4): a goal never un-meets.</summary>
        public int GateMet { get; internal set; }

        /// <summary>The old village (spec 023 §3.5), from the unlock.</summary>
        public CohortState Cohort { get; internal set; } = new CohortState();

        /// <summary>The watchman's post, the community hall and the playground (spec 023 §3.7), in <see cref="Definitions.CivicDef"/> order.</summary>
        public CivicPost[] Civic { get; internal set; } = NewCivic();

        private static CivicPost[] NewCivic() { return new[] { new CivicPost(), new CivicPost(), new CivicPost() }; }

        private bool CivicDefault { get { foreach (CivicPost p in Civic) if (!p.IsDefault) return false; return true; } }

        /// <summary>Nothing of the Township has started.</summary>
        internal bool IsDefault { get { return Road.Status == RoadStatus.None && Clinic.IsDefault && GateMet == 0 && !Cohort.Joined && CivicDefault; } }

        internal TownshipState Clone()
        {
            var civic = new CivicPost[Civic.Length];
            for (int i = 0; i < civic.Length; i++) civic[i] = Civic[i].Clone();
            return new TownshipState { Road = Road.Clone(), Clinic = Clinic.Clone(), GateMet = GateMet, Cohort = Cohort.Clone(), Civic = civic };
        }

        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && IsDefault) return;
            Road.WriteTo(h);
            Clinic.WriteTo(h);
            h.WriteInt(GateMet);
            Cohort.WriteTo(h);
            foreach (CivicPost p in Civic) p.WriteTo(h);
        }

        internal static TownshipState ReadFrom(SaveReader r)
        {
            var s = new TownshipState { Road = RoadState.ReadFrom(r), Clinic = ClinicState.ReadFrom(r), GateMet = r.ReadInt(), Cohort = CohortState.ReadFrom(r) };
            for (int i = 0; i < s.Civic.Length; i++) s.Civic[i] = CivicPost.ReadFrom(r);
            return s;
        }
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

        /// <summary>What the road earned you at the trader, this season and last (spec 023 §3.3): each sale's extra, kilos × the saving a kg × the price index.</summary>
        public long EarnedThis { get; internal set; }
        public long EarnedLast { get; internal set; }

        internal RoadState Clone() { return (RoadState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt((int)Status);
            h.WriteLong(StartedAt);
            h.WriteLong(ReadyAt);
            h.WriteLong(JobId);
            h.WriteLong(Share);
            h.WriteLong(EarnedThis);
            h.WriteLong(EarnedLast);
        }

        internal static RoadState ReadFrom(SaveReader r)
        {
            return new RoadState { Status = (RoadStatus)r.ReadInt(), StartedAt = r.ReadLong(), ReadyAt = r.ReadLong(), JobId = r.ReadLong(), Share = r.ReadLong(), EarnedThis = r.ReadLong(), EarnedLast = r.ReadLong() };
        }
    }
}
