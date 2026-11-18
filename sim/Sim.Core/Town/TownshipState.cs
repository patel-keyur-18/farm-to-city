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

        /// <summary>Each acre's land value multiplier as last read (spec 024 §3.1); empty until the Township's unlock reads it.</summary>
        public Fixed[] LandValues { get; internal set; } = new Fixed[0];

        /// <summary>Families the migration draw found no home for, remembered for the builders (spec 024 §3.3); when the next is due.</summary>
        public int TurnedAway { get; internal set; }
        public long NextTurnedAwayAt { get; internal set; }

        /// <summary>The season in which builders last bought a parcel (one a season across the town), or −1.</summary>
        public long BuiltSeason { get; internal set; } = -1;

        /// <summary>Parcels builders have bought so far and the homes they built, for the sheets.</summary>
        public int BuilderParcels { get; internal set; }
        public int BuilderHomes { get; internal set; }

        /// <summary>The ring's gravel lane sections the treasury has built, a bit for each <see cref="Definitions.LaneDef"/> in map order (spec 025 §3.4; schema 21).</summary>
        public int LanesBuilt { get; internal set; }

        private static CivicPost[] NewCivic() { return new[] { new CivicPost(), new CivicPost(), new CivicPost() }; }

        private bool CivicDefault { get { foreach (CivicPost p in Civic) if (!p.IsDefault) return false; return true; } }

        /// <summary>Nothing of the Township has started.</summary>
        internal bool IsDefault { get { return Road.Status == RoadStatus.None && Clinic.IsDefault && GateMet == 0 && !Cohort.Joined && CivicDefault && LandValues.Length == 0 && TurnedAway == 0 && NextTurnedAwayAt == 0 && BuiltSeason == -1 && BuilderParcels == 0 && LanesBuilt == 0; } }

        internal TownshipState Clone()
        {
            var civic = new CivicPost[Civic.Length];
            for (int i = 0; i < civic.Length; i++) civic[i] = Civic[i].Clone();
            return new TownshipState { Road = Road.Clone(), Clinic = Clinic.Clone(), GateMet = GateMet, Cohort = Cohort.Clone(), Civic = civic, LandValues = (Fixed[])LandValues.Clone(), TurnedAway = TurnedAway, NextTurnedAwayAt = NextTurnedAwayAt, BuiltSeason = BuiltSeason, BuilderParcels = BuilderParcels, BuilderHomes = BuilderHomes, LanesBuilt = LanesBuilt };
        }

        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && IsDefault) return;
            Road.WriteTo(h);
            Clinic.WriteTo(h);
            h.WriteInt(GateMet);
            Cohort.WriteTo(h);
            foreach (CivicPost p in Civic) p.WriteTo(h);
            h.WriteInt(LandValues.Length);
            foreach (Fixed f in LandValues) h.WriteFixed(f);
            h.WriteInt(TurnedAway);
            h.WriteLong(NextTurnedAwayAt);
            h.WriteLong(BuiltSeason);
            h.WriteInt(BuilderParcels);
            h.WriteInt(BuilderHomes);
            if (h.Hashing ? LanesBuilt != 0 : !(h is SaveWriter lw && lw.Schema < MapMigration.RingSchema)) h.WriteInt(LanesBuilt); // schema 21; hashed once a lane stands
        }

        internal static TownshipState ReadFrom(SaveReader r)
        {
            var s = new TownshipState { Road = RoadState.ReadFrom(r), Clinic = ClinicState.ReadFrom(r), GateMet = r.ReadInt(), Cohort = CohortState.ReadFrom(r) };
            for (int i = 0; i < s.Civic.Length; i++) s.Civic[i] = CivicPost.ReadFrom(r);
            s.LandValues = new Fixed[r.ReadInt()];
            for (int i = 0; i < s.LandValues.Length; i++) s.LandValues[i] = r.ReadFixed();
            s.TurnedAway = r.ReadInt();
            s.NextTurnedAwayAt = r.ReadLong();
            s.BuiltSeason = r.ReadLong();
            s.BuilderParcels = r.ReadInt();
            s.BuilderHomes = r.ReadInt();
            if (r.Schema >= MapMigration.RingSchema) s.LanesBuilt = r.ReadInt();
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
