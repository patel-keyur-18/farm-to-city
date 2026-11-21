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

        /// <summary>The utilities the treasury has built, a bit for each <see cref="Definitions.UtilityDef"/> in order (spec 025 §3.1; schema 21).</summary>
        public int UtilitiesBuilt { get; internal set; }

        /// <summary>The buildings being made from the Township (spec 025 §3.2), in the order they began; the trader's day they draw from, and what it has sold today.</summary>
        public System.Collections.Generic.List<BuildProject> Projects { get; internal set; } = new System.Collections.Generic.List<BuildProject>();
        public long NextProjectId { get; internal set; }
        public long StockDay { get; internal set; } = -1;
        public long TakenBricks { get; internal set; }
        public long TakenCement { get; internal set; }
        public long TakenTimber { get; internal set; }

        /// <summary>Bricks your kiln sold the trader this trader's day: they add to his stock for the whole town (US-243).</summary>
        public long KilnBricks { get; internal set; }

        /// <summary>The trader's day those bricks were sold on (−1 none yet).</summary>
        public long KilnDay { get; internal set; } = -1;

        /// <summary>Approval, reputation, crime and inequality as read at the last close (spec 026, schema 22); hashed once a close has read the town.</summary>
        public TownLifeState TownLife { get; internal set; } = new TownLifeState();

        /// <summary>The event engine (spec 027, save schema 22); default until the first 6:00 roll sees the Township.</summary>
        public EventsState Events { get; internal set; } = new EventsState();

        /// <summary>Crop, property and business insurance (spec 027 §3.5–§3.6, save schema 22); default until a policy is switched on.</summary>
        public InsuranceState Insurance { get; internal set; } = new InsuranceState();

        /// <summary>The trader's forward contracts (spec 027 §3.7, save schema 22); default until a staple is harvested at the Township.</summary>
        public ContractsState Contracts { get; internal set; } = new ContractsState();

        /// <summary>Sales between businesses on terms (spec 028 §3.1, save schema 23); default until a sale is made on terms or a term is set.</summary>
        public Business.InvoicesState Invoices { get; internal set; } = new Business.InvoicesState();

        private bool ConstructionDefault { get { return Projects.Count == 0 && NextProjectId == 0 && StockDay == -1 && TakenBricks == 0 && TakenCement == 0 && TakenTimber == 0 && KilnBricks == 0 && KilnDay == -1; } }

        private static CivicPost[] NewCivic() { return new[] { new CivicPost(), new CivicPost(), new CivicPost() }; }

        private bool CivicDefault { get { foreach (CivicPost p in Civic) if (!p.IsDefault) return false; return true; } }

        /// <summary>Nothing of the Township has started.</summary>
        internal bool IsDefault { get { return Road.Status == RoadStatus.None && Clinic.IsDefault && GateMet == 0 && !Cohort.Joined && CivicDefault && LandValues.Length == 0 && TurnedAway == 0 && NextTurnedAwayAt == 0 && BuiltSeason == -1 && BuilderParcels == 0 && LanesBuilt == 0 && UtilitiesBuilt == 0 && ConstructionDefault && TownLife.IsDefault && Events.IsDefault && Insurance.IsDefault && Contracts.IsDefault && Invoices.IsDefault; } }

        internal TownshipState Clone()
        {
            var civic = new CivicPost[Civic.Length];
            for (int i = 0; i < civic.Length; i++) civic[i] = Civic[i].Clone();
            return new TownshipState { Road = Road.Clone(), Clinic = Clinic.Clone(), GateMet = GateMet, Cohort = Cohort.Clone(), Civic = civic, LandValues = (Fixed[])LandValues.Clone(), TurnedAway = TurnedAway, NextTurnedAwayAt = NextTurnedAwayAt, BuiltSeason = BuiltSeason, BuilderParcels = BuilderParcels, BuilderHomes = BuilderHomes, LanesBuilt = LanesBuilt, UtilitiesBuilt = UtilitiesBuilt, Projects = Projects.ConvertAll(x => x.Clone()), NextProjectId = NextProjectId, StockDay = StockDay, TakenBricks = TakenBricks, TakenCement = TakenCement, TakenTimber = TakenTimber, KilnBricks = KilnBricks, KilnDay = KilnDay, TownLife = TownLife.Clone(), Events = Events.Clone(), Insurance = Insurance.Clone(), Contracts = Contracts.Clone(), Invoices = Invoices.Clone() };
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
            if (h.Hashing ? UtilitiesBuilt != 0 : !(h is SaveWriter uw && uw.Schema < MapMigration.RingSchema)) h.WriteInt(UtilitiesBuilt); // schema 21; hashed once a utility stands
            if (h.Hashing ? !ConstructionDefault : !(h is SaveWriter kw && kw.Schema < MapMigration.RingSchema)) // schema 21; hashed once a building has been made
            {
                h.WriteInt(Projects.Count);
                foreach (BuildProject p in Projects) p.WriteTo(h);
                h.WriteLong(NextProjectId);
                h.WriteLong(StockDay);
                h.WriteLong(TakenBricks);
                h.WriteLong(TakenCement);
                h.WriteLong(TakenTimber);
                h.WriteLong(KilnBricks);
                h.WriteLong(KilnDay);
            }
            if (h.Hashing ? !TownLife.IsDefault : !(h is SaveWriter tw && tw.Schema < TownLifeSchema)) TownLife.WriteTo(h); // schema 22; hashed once a close has read the town
            if (h.Hashing ? !Events.IsDefault : !(h is SaveWriter ew && ew.Schema < TownLifeSchema)) Events.WriteTo(h); // schema 22; the engine, once it has seen the Township
            if (h.Hashing ? !Insurance.IsDefault : !(h is SaveWriter iw && iw.Schema < TownLifeSchema)) Insurance.WriteTo(h); // schema 22; once a policy was ever switched on
            if (h.Hashing ? !Contracts.IsDefault : !(h is SaveWriter xw && xw.Schema < TownLifeSchema)) Contracts.WriteTo(h); // schema 22; once a staple was harvested at the Township
            if (h.Hashing ? !Invoices.IsDefault : !(h is SaveWriter nw && nw.Schema < GrowthSchema)) Invoices.WriteTo(h); // schema 23; once a sale was made on terms
        }

        /// <summary>The save schema that added town life (spec 026, Sprint 24).</summary>
        public const int TownLifeSchema = 22;

        /// <summary>The save schema that added business growth (spec 028, Sprint 25): invoices first.</summary>
        public const int GrowthSchema = 23;

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
            if (r.Schema >= MapMigration.RingSchema)
            {
                s.LanesBuilt = r.ReadInt();
                s.UtilitiesBuilt = r.ReadInt();
                int n = r.ReadCount();
                for (int i = 0; i < n; i++) s.Projects.Add(BuildProject.ReadFrom(r));
                s.NextProjectId = r.ReadLong(); s.StockDay = r.ReadLong(); s.TakenBricks = r.ReadLong(); s.TakenCement = r.ReadLong(); s.TakenTimber = r.ReadLong(); s.KilnBricks = r.ReadLong(); s.KilnDay = r.ReadLong();
            }
            if (r.Schema >= TownLifeSchema) { s.TownLife = TownLifeState.ReadFrom(r); s.Events = EventsState.ReadFrom(r); s.Insurance = InsuranceState.ReadFrom(r); s.Contracts = ContractsState.ReadFrom(r); }
            if (r.Schema >= GrowthSchema) s.Invoices = Business.InvoicesState.ReadFrom(r);
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
