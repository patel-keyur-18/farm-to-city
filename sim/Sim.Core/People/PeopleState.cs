using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.People
{
    /// <summary>A neighbour from a nearby village who can be hired by the day (spec 006 §3.2, §3.3).</summary>
    public sealed class Neighbour
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="Definitions.PeopleDefs.Names"/>.</summary>
        public int Name { get; internal set; }

        /// <summary>Index into <see cref="Definitions.PeopleDefs.Traits"/>.</summary>
        public int Trait { get; internal set; }

        /// <summary>Seconds hired in all (ADR 0039): six full day-equivalents make a regular.</summary>
        public long SecondsWorked { get; internal set; }
        public bool AskedToMoveIn { get; internal set; }
        public bool FreeToday { get; internal set; }

        /// <summary>Kept for the schema-4 layout: −1 since ADR 0049 (labourers tend every field); a field id only in Sprint 10's first build.</summary>
        public int Tile { get; internal set; } = -1;

        /// <summary>When they go home: the crop's readyAt rounded up to the hour (ADR 0048 §3); 0 when not hired.</summary>
        public long Until { get; internal set; }

        public bool Hired { get { return Until != 0; } }

        internal Neighbour Clone() { return (Neighbour)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Name, Name);
            h.WriteRef(ContentTable.Trait, Trait);
            h.WriteLong(SecondsWorked);
            h.WriteBool(AskedToMoveIn);
            h.WriteBool(FreeToday);
            h.WriteInt(Tile);
            h.WriteLong(Until);
        }

        internal static Neighbour ReadFrom(SaveReader r, long now)
        {
            var n = new Neighbour
            {
                Id = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Trait = r.ReadRef(ContentTable.Trait), SecondsWorked = r.ReadLong(),
                AskedToMoveIn = r.ReadBool(), FreeToday = r.ReadBool(),
            };
            if (r.Schema >= 4)
            {
                n.Tile = r.ReadTile();
                n.Until = r.ReadLong();
            }
            else if (r.ReadBool()) n.Until = PeopleRules.NextDailyPass(now); // schema 3: hired today, paid to the daily pass (ADR 0048 §4)
            return n;
        }
    }

    /// <summary>Someone asking to be the resident hand (spec 006 §3.5).</summary>
    public sealed class Applicant
    {
        public long Id { get; internal set; }
        public int Name { get; internal set; }
        public int Skill { get; internal set; }
        public int Trait1 { get; internal set; }
        public int Trait2 { get; internal set; }
        public bool SecondTraitKnown { get; internal set; }

        /// <summary>A regular's skill is known exactly (spec 006 §3.3).</summary>
        public bool SkillKnown { get; internal set; }

        /// <summary>Came through an above-market job post: 1.10 × the wage, more likely to accept.</summary>
        public bool AboveMarket { get; internal set; }
        public long ArrivedAt { get; internal set; }

        /// <summary>Live seconds waited: applicants never leave while the player is away (ADR 0002).</summary>
        public long WaitedLiveSeconds { get; internal set; }

        /// <summary>The roster id of a regular, else 0.</summary>
        public long FromRosterId { get; internal set; }

        internal Applicant Clone() { return (Applicant)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Name, Name);
            h.WriteInt(Skill);
            h.WriteRef(ContentTable.Trait, Trait1);
            h.WriteRef(ContentTable.Trait, Trait2);
            h.WriteBool(SecondTraitKnown);
            h.WriteBool(SkillKnown);
            h.WriteBool(AboveMarket);
            h.WriteLong(ArrivedAt);
            h.WriteLong(WaitedLiveSeconds);
            h.WriteLong(FromRosterId);
        }

        internal static Applicant ReadFrom(SaveReader r)
        {
            return new Applicant
            {
                Id = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Skill = r.ReadInt(), Trait1 = r.ReadRef(ContentTable.Trait),
                Trait2 = r.ReadRef(ContentTable.Trait), SecondTraitKnown = r.ReadBool(), SkillKnown = r.ReadBool(), AboveMarket = r.ReadBool(),
                ArrivedAt = r.ReadLong(), WaitedLiveSeconds = r.ReadLong(), FromRosterId = r.ReadLong(),
            };
        }
    }

    public enum ContractState
    {
        Active = 0,
        Lapsed = 1,
    }

    /// <summary>A resident hand and their season contract (spec 006 §3.6). One contract per hand while they stay.</summary>
    public sealed class Hand
    {
        public long Id { get; internal set; }
        public int Name { get; internal set; }

        /// <summary>Farming skill 0–100; grows each season (fractional: an ambitious hand gains 4.5).</summary>
        public Fixed Skill { get; internal set; }
        public int Trait1 { get; internal set; }
        public int Trait2 { get; internal set; }

        /// <summary>The <see cref="Land.Building"/> id of the hut they live in.</summary>
        public long HutId { get; internal set; }
        public long HiredAt { get; internal set; }

        /// <summary>Season closes passed under contract; 0 is the probation season (no severance).</summary>
        public int SeasonsWorked { get; internal set; }

        // The contract.
        public long Wage { get; internal set; }
        public ContractState Contract { get; internal set; }
        public long PrepaymentId { get; internal set; }
        public long LapsedAt { get; internal set; }

        /// <summary>Live seconds since the lapse: the grace never runs out while the player is away.</summary>
        public long LapsedLiveSeconds { get; internal set; }

        /// <summary>The sim hour (t ÷ 3600) of the hand's order actions so far and how many (ADR 0040: an order starts at once).</summary>
        public long ActionHour { get; internal set; }
        public int ActionsInHour { get; internal set; }

        internal Hand Clone() { return (Hand)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Name, Name);
            h.WriteFixed(Skill);
            h.WriteRef(ContentTable.Trait, Trait1);
            h.WriteRef(ContentTable.Trait, Trait2);
            h.WriteLong(HutId);
            h.WriteLong(HiredAt);
            h.WriteInt(SeasonsWorked);
            h.WriteLong(Wage);
            h.WriteInt((int)Contract);
            h.WriteLong(PrepaymentId);
            h.WriteLong(LapsedAt);
            h.WriteLong(LapsedLiveSeconds);
            h.WriteLong(ActionHour);
            h.WriteInt(ActionsInHour);
        }

        internal static Hand ReadFrom(SaveReader r)
        {
            return new Hand
            {
                Id = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Skill = r.ReadFixed(), Trait1 = r.ReadRef(ContentTable.Trait),
                Trait2 = r.ReadRef(ContentTable.Trait), HutId = r.ReadLong(), HiredAt = r.ReadLong(), SeasonsWorked = r.ReadInt(), Wage = r.ReadLong(),
                Contract = (ContractState)r.ReadInt(), PrepaymentId = r.ReadLong(), LapsedAt = r.ReadLong(), LapsedLiveSeconds = r.ReadLong(),
                ActionHour = r.ReadLong(), ActionsInHour = r.ReadInt(),
            };
        }
    }

    public enum SellRule
    {
        Store = 0,
        Offer = 1,
        Trader = 2,
    }

    public enum OrderTargetKind
    {
        Field = 0,
        Tree = 1,

        /// <summary>The hand looks after the shed: keeps straw and feed stocked (spec 012 §3.11). Target 0; one per farm.</summary>
        Animals = 2,
    }

    public enum OrderAction
    {
        None = 0,
        Harvested = 1,
        Sowed = 2,
        Picked = 3,

        /// <summary>Bought straw or cattle feed for the shed (spec 012 §3.11).</summary>
        Bought = 4,
    }

    /// <summary>Why an order is waiting (spec 006 §3.8), emitted once per change.</summary>
    public enum OrderWait
    {
        None = 0,
        OutOfSeason = 1,
        InsufficientFunds = 2,
        StoreFull = 3,
        NoSavedSeed = 4,
        Paused = 5,

        /// <summary>The store is full and the trader's truck is away (spec 011 §3.6b).</summary>
        TraderAway = 6,
    }

    /// <summary>A standing order: one hand, one target (spec 006 §3.8). Target: a tile id or a tree slot id.</summary>
    public sealed class StandingOrder
    {
        public long HandId { get; internal set; }
        public OrderTargetKind Kind { get; internal set; }
        public long Target { get; internal set; }

        /// <summary>Field orders: indices into the crop and fertiliser lists, and the seed source.</summary>
        public int Crop { get; internal set; }
        public Farm.SeedSource Seed { get; internal set; }
        public int Fertiliser { get; internal set; }

        public SellRule Sell { get; internal set; }
        public Fixed MinPrice { get; internal set; }

        /// <summary>An animals order's days of straw and feed to keep (spec 012 §3.11), held in <see cref="MinPrice"/>.</summary>
        public int Days { get { return (int)MinPrice.FloorToLong(); } }

        /// <summary>This season only: cleared after its harvest or pick; otherwise it repeats every season (a standing order).</summary>
        public bool Once { get; internal set; }

        // The last action, for the hand sheet's "Last done" (ADR 0039).
        public OrderAction LastAction { get; internal set; }
        public long LastAt { get; internal set; }
        public long LastQuantity { get; internal set; }
        public long LastAmount { get; internal set; }
        public OrderWait Waiting { get; internal set; }

        internal StandingOrder Clone() { return (StandingOrder)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(HandId);
            h.WriteInt((int)Kind);
            h.WriteLong(Target);
            h.WriteRef(ContentTable.Crop, Crop);
            h.WriteInt((int)Seed);
            h.WriteRef(ContentTable.Fertiliser, Fertiliser);
            h.WriteInt((int)Sell);
            h.WriteFixed(MinPrice);
            h.WriteBool(Once);
            h.WriteInt((int)LastAction);
            h.WriteLong(LastAt);
            h.WriteLong(LastQuantity);
            h.WriteLong(LastAmount);
            h.WriteInt((int)Waiting);
        }

        internal static StandingOrder ReadFrom(SaveReader r)
        {
            var o = new StandingOrder
            {
                HandId = r.ReadLong(), Kind = (OrderTargetKind)r.ReadInt(), Target = r.ReadLong(), Crop = r.ReadRef(ContentTable.Crop),
                Seed = (Farm.SeedSource)r.ReadInt(), Fertiliser = r.ReadRef(ContentTable.Fertiliser), Sell = (SellRule)r.ReadInt(), MinPrice = r.ReadFixed(),
                Once = r.ReadBool(), LastAction = (OrderAction)r.ReadInt(), LastAt = r.ReadLong(), LastQuantity = r.ReadLong(), LastAmount = r.ReadLong(),
                Waiting = (OrderWait)r.ReadInt(),
            };
            if (r.OldMap && o.Kind == OrderTargetKind.Field) o.Target = Saves.MapMigration.Remap((int)o.Target); // spec 010 §3.4
            return o;
        }

        /// <summary>Orders are kept sorted by (hand, kind, target).</summary>
        internal int CompareKey(StandingOrder o)
        {
            int c = HandId.CompareTo(o.HandId);
            if (c != 0) return c;
            c = ((int)Kind).CompareTo((int)o.Kind);
            return c != 0 ? c : Target.CompareTo(o.Target);
        }
    }

    /// <summary>A hand's hut being built (spec 006 §3.4); the building joins the land when the job ends.</summary>
    public sealed class Construction
    {
        public long Id { get; internal set; }
        public int TileId { get; internal set; }
        public long Cost { get; internal set; }
        public long JobId { get; internal set; }
        public long ReadyAt { get; internal set; }

        internal Construction Clone() { return (Construction)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt(TileId);
            h.WriteLong(Cost);
            h.WriteLong(JobId);
            h.WriteLong(ReadyAt);
        }

        internal static Construction ReadFrom(SaveReader r)
        {
            return new Construction { Id = r.ReadLong(), TileId = r.ReadTile(), Cost = r.ReadLong(), JobId = r.ReadLong(), ReadyAt = r.ReadLong() };
        }
    }

    /// <summary>A posted job (spec 006 §3.5); at most one.</summary>
    public sealed class JobPost
    {
        public bool AboveMarket { get; internal set; }
        public long PostedAt { get; internal set; }
        public long ExpiresAt { get; internal set; }

        internal JobPost Clone() { return (JobPost)MemberwiseClone(); }
    }

    /// <summary>The people part of the game state (spec 006 §4). Every list is kept in id order.</summary>
    public sealed class PeopleState
    {
        public List<Neighbour> Roster { get; } = new List<Neighbour>();
        public List<Applicant> Applicants { get; } = new List<Applicant>();
        public List<Hand> Hands { get; } = new List<Hand>();
        public List<StandingOrder> Orders { get; } = new List<StandingOrder>();
        public List<Construction> Constructions { get; } = new List<Construction>();
        public JobPost Post { get; internal set; }

        /// <summary>When the next applicant arrives, or 0 when none is due (no empty hut, or the queue is full).</summary>
        public long NextApplicantAt { get; internal set; }
        public long NextApplicantId { get; internal set; } = 1;
        public long NextHandId { get; internal set; } = 1;
        public long NextConstructionId { get; internal set; } = 1;

        /// <summary>The town's population: resident hands (the founder household is counted by spec 009).</summary>
        public int Population { get { return Hands.Count; } }

        public Hand Hand(long id) { return Hands.Find(x => x.Id == id); }

        public Applicant Applicant(long id) { return Applicants.Find(x => x.Id == id); }

        public StandingOrder OrderFor(OrderTargetKind kind, long target) { return Orders.Find(o => o.Kind == kind && o.Target == target); }

        internal PeopleState Clone()
        {
            var c = new PeopleState
            {
                Post = Post?.Clone(), NextApplicantAt = NextApplicantAt, NextApplicantId = NextApplicantId, NextHandId = NextHandId,
                NextConstructionId = NextConstructionId,
            };
            foreach (Neighbour n in Roster) c.Roster.Add(n.Clone());
            foreach (Applicant a in Applicants) c.Applicants.Add(a.Clone());
            foreach (Hand h in Hands) c.Hands.Add(h.Clone());
            foreach (StandingOrder o in Orders) c.Orders.Add(o.Clone());
            foreach (Construction k in Constructions) c.Constructions.Add(k.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Roster.Count);
            foreach (Neighbour n in Roster) n.WriteTo(h);
            h.WriteInt(Applicants.Count);
            foreach (Applicant a in Applicants) a.WriteTo(h);
            h.WriteInt(Hands.Count);
            foreach (Hand x in Hands) x.WriteTo(h);
            h.WriteInt(Orders.Count);
            foreach (StandingOrder o in Orders) o.WriteTo(h);
            h.WriteInt(Constructions.Count);
            foreach (Construction k in Constructions) k.WriteTo(h);
            h.WriteBool(Post != null);
            if (Post != null)
            {
                h.WriteBool(Post.AboveMarket);
                h.WriteLong(Post.PostedAt);
                h.WriteLong(Post.ExpiresAt);
            }
            h.WriteLong(NextApplicantAt);
            h.WriteLong(NextApplicantId);
            h.WriteLong(NextHandId);
            h.WriteLong(NextConstructionId);
        }

        internal static PeopleState ReadFrom(SaveReader r, long now)
        {
            var s = new PeopleState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Roster.Add(Neighbour.ReadFrom(r, now));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Applicants.Add(People.Applicant.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Hands.Add(People.Hand.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Orders.Add(StandingOrder.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Constructions.Add(Construction.ReadFrom(r));
            if (r.ReadBool()) s.Post = new JobPost { AboveMarket = r.ReadBool(), PostedAt = r.ReadLong(), ExpiresAt = r.ReadLong() };
            s.NextApplicantAt = r.ReadLong();
            s.NextApplicantId = r.ReadLong();
            s.NextHandId = r.ReadLong();
            s.NextConstructionId = r.ReadLong();
            return s;
        }
    }
}
