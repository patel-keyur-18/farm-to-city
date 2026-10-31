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

        /// <summary>Where the job is (spec 015 §3.1, schema 11): 0 your fields (a farm hand), else a business's id.</summary>
        public long Place { get; internal set; }

        /// <summary>One of your townspeople (known exactly, spec 015 §3.1), else 0.</summary>
        public long CitizenId { get; internal set; }

        /// <summary>When the reference check comes back (spec 015 §3.2), 0 if none was asked; done once <see cref="ReferenceDone"/>.</summary>
        public long ReferenceAt { get; internal set; }
        public bool ReferenceDone { get; internal set; }

        /// <summary>Refused a low offer and asked for the asked wage instead (spec 015 §3.3): the next offer at it is accepted.</summary>
        public bool Countered { get; internal set; }

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
            if (h is SaveWriter sw && sw.Schema < 11) return;
            h.WriteLong(Place);
            h.WriteLong(CitizenId);
            h.WriteLong(ReferenceAt);
            h.WriteBool(ReferenceDone);
            h.WriteBool(Countered);
        }

        internal static Applicant ReadFrom(SaveReader r)
        {
            var a = new Applicant
            {
                Id = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Skill = r.ReadInt(), Trait1 = r.ReadRef(ContentTable.Trait),
                Trait2 = r.ReadRef(ContentTable.Trait), SecondTraitKnown = r.ReadBool(), SkillKnown = r.ReadBool(), AboveMarket = r.ReadBool(),
                ArrivedAt = r.ReadLong(), WaitedLiveSeconds = r.ReadLong(), FromRosterId = r.ReadLong(),
            };
            if (r.Schema < 11) return a;
            a.Place = r.ReadLong();
            a.CitizenId = r.ReadLong();
            a.ReferenceAt = r.ReadLong();
            a.ReferenceDone = r.ReadBool();
            a.Countered = r.ReadBool();
            return a;
        }
    }

    public enum ContractState
    {
        Active = 0,
        Lapsed = 1,
    }

    /// <summary>A resident hand and their season contract (spec 006 §3.6). One contract per hand while they stay.</summary>
    public sealed class Hand : IStaff
    {
        public long Id { get; internal set; }
        public int Name { get; internal set; }

        /// <summary>Farming skill 0–100; grows each season (fractional: an ambitious hand gains 4.5).</summary>
        public Fixed Skill { get; internal set; }
        public int Trait1 { get; internal set; }
        public int Trait2 { get; internal set; }

        /// <summary>The <see cref="Land.Building"/> id of the hut they live in; 0 once their family lives in a family house (spec 014 §3.10).</summary>
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

        /// <summary>
        /// The family house's rent taken off this season's wage as housing in kind (spec 014 §3.6, schema 10): <see cref="Wage"/> is
        /// the cash; a loyal hand keeps Wage + this.
        /// </summary>
        public long HousingInKind { get; internal set; }

        /// <summary>The wage memory (spec 015 §3.3, schema 11): +1 hired 10% above the asked wage, −1 below it until a renewal pays it, else 0.</summary>
        public int Fairness { get; internal set; }

        /// <summary>Fatigue 0–fatigueMax (spec 015 §3.4, schema 11): up after a long day, down after a normal one.</summary>
        public Fixed Fatigue { get; internal set; }

        /// <summary>Works long days: 2 hours more, paid at 1.5× (spec 015 §3.4).</summary>
        public bool LongDays { get; internal set; }

        /// <summary>When their course ends, or 0 when not on one (spec 015 §3.5): away, no work, wage still paid.</summary>
        public long CourseUntil { get; internal set; }

        /// <summary>What HR events left behind (spec 015 §3.7).</summary>
        public HrState Hr { get; internal set; } = new HrState();

        internal Hand Clone()
        {
            var c = (Hand)MemberwiseClone();
            c.Hr = Hr.Clone();
            return c;
        }

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
            if (!(h is Saves.SaveWriter sw && sw.Schema < 10) && (!h.Hashing || HousingInKind != 0)) h.WriteLong(HousingInKind); // hashed only once used
            if (!(h is Saves.SaveWriter sw11 && sw11.Schema < 11) && (!h.Hashing || Fairness != 0)) h.WriteInt(Fairness);
            if (!(h is Saves.SaveWriter sw12 && sw12.Schema < 11) && (!h.Hashing || Fatigue != Fixed.Zero || LongDays || CourseUntil != 0))
            {
                h.WriteFixed(Fatigue);
                h.WriteBool(LongDays);
                h.WriteLong(CourseUntil);
            }
            if (!(h is Saves.SaveWriter sw13 && sw13.Schema < 11) && (!h.Hashing || Hr.Used)) Hr.WriteTo(h);
        }

        internal static Hand ReadFrom(SaveReader r)
        {
            var hand = new Hand
            {
                Id = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Skill = r.ReadFixed(), Trait1 = r.ReadRef(ContentTable.Trait),
                Trait2 = r.ReadRef(ContentTable.Trait), HutId = r.ReadLong(), HiredAt = r.ReadLong(), SeasonsWorked = r.ReadInt(), Wage = r.ReadLong(),
                Contract = (ContractState)r.ReadInt(), PrepaymentId = r.ReadLong(), LapsedAt = r.ReadLong(), LapsedLiveSeconds = r.ReadLong(),
                ActionHour = r.ReadLong(), ActionsInHour = r.ReadInt(),
            };
            if (r.Schema >= 10) hand.HousingInKind = r.ReadLong();
            if (r.Schema >= 11)
            {
                hand.Fairness = r.ReadInt();
                hand.Fatigue = r.ReadFixed();
                hand.LongDays = r.ReadBool();
                hand.CourseUntil = r.ReadLong();
                hand.Hr = HrState.ReadFrom(r);
            }
            return hand;
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

    /// <summary>A posted job (spec 006 §3.5, spec 015 §3.1): at most one a place.</summary>
    public sealed class JobPost
    {
        /// <summary>0 your fields (a farm hand), else a business's id.</summary>
        public long Place { get; internal set; }
        public bool AboveMarket { get; internal set; }
        public long PostedAt { get; internal set; }
        public long ExpiresAt { get; internal set; }

        /// <summary>A business post's next applicant (a farm hand's is <see cref="PeopleState.NextApplicantAt"/>), 0 when none is due.</summary>
        public long NextApplicantAt { get; internal set; }

        internal JobPost Clone() { return (JobPost)MemberwiseClone(); }
    }

    /// <summary>
    /// Someone you hired to work at a business (spec 015 §3.1, schema 11): one of your townspeople, or a worker who commutes from a
    /// neighbouring village (not a citizen). Paid a season upfront from the business's cash.
    /// </summary>
    public sealed class Worker : IStaff
    {
        public long Id { get; internal set; }

        /// <summary>The business's id.</summary>
        public long Place { get; internal set; }

        /// <summary>The townsperson, or 0 for a commuter.</summary>
        public long CitizenId { get; internal set; }
        public int Name { get; internal set; }

        /// <summary>The role's skill (crafting, or farming for a dairyman) 0–100, the live record while they work here.</summary>
        public Fixed Skill { get; internal set; }
        public int Trait1 { get; internal set; }
        public int Trait2 { get; internal set; }
        public long Wage { get; internal set; }

        /// <summary>The wage memory, as <see cref="Hand.Fairness"/>.</summary>
        public int Fairness { get; internal set; }
        public long HiredAt { get; internal set; }

        /// <summary>Season closes passed; 0 is the probation season.</summary>
        public int SeasonsWorked { get; internal set; }

        /// <summary>As <see cref="Hand.Fatigue"/> and <see cref="Hand.CourseUntil"/>; their long days are the business's hours past its usual day.</summary>
        public Fixed Fatigue { get; internal set; }
        public long CourseUntil { get; internal set; }
        public HrState Hr { get; internal set; } = new HrState();

        internal Worker Clone()
        {
            var c = (Worker)MemberwiseClone();
            c.Hr = Hr.Clone();
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteLong(Place);
            h.WriteLong(CitizenId);
            h.WriteRef(ContentTable.Name, Name);
            h.WriteFixed(Skill);
            h.WriteRef(ContentTable.Trait, Trait1);
            h.WriteRef(ContentTable.Trait, Trait2);
            h.WriteLong(Wage);
            h.WriteInt(Fairness);
            h.WriteLong(HiredAt);
            h.WriteInt(SeasonsWorked);
            h.WriteFixed(Fatigue);
            h.WriteLong(CourseUntil);
            Hr.WriteTo(h);
        }

        internal static Worker ReadFrom(SaveReader r)
        {
            var w = new Worker
            {
                Id = r.ReadLong(), Place = r.ReadLong(), CitizenId = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Skill = r.ReadFixed(),
                Trait1 = r.ReadRef(ContentTable.Trait), Trait2 = r.ReadRef(ContentTable.Trait), Wage = r.ReadLong(), Fairness = r.ReadInt(),
                HiredAt = r.ReadLong(), SeasonsWorked = r.ReadInt(), Fatigue = r.ReadFixed(), CourseUntil = r.ReadLong(),
            };
            w.Hr = HrState.ReadFrom(r);
            return w;
        }
    }

    /// <summary>
    /// A worker's HR record (spec 015 §3.7, schema 11), the same for a farm hand and a business's worker: a mood an event left for a
    /// season, raises refused, the season of their last event, sick leave, an advance to repay, and an offer from elsewhere taken.
    /// </summary>
    public sealed class HrState
    {
        /// <summary>Added to motivation through <see cref="MoodSeason"/> (a season index).</summary>
        public Fixed Mood { get; internal set; }
        public long MoodSeason { get; internal set; }

        /// <summary>Raise requests refused since they were last paid what they ask.</summary>
        public int RaiseRefusals { get; internal set; }

        /// <summary>The season index + 1 of their last event (0: none yet): at most one a season.</summary>
        public long EventSeason { get; internal set; }

        /// <summary>When their sick leave ends, or 0.</summary>
        public long SickUntil { get; internal set; }

        /// <summary>An advance on their wage, repaid from the next renewal.</summary>
        public long Advance { get; internal set; }

        /// <summary>They took an offer from elsewhere and leave at the season's close.</summary>
        public bool Leaving { get; internal set; }

        internal bool Used { get { return Mood != Fixed.Zero || MoodSeason != 0 || RaiseRefusals != 0 || EventSeason != 0 || SickUntil != 0 || Advance != 0 || Leaving; } }

        internal HrState Clone() { return (HrState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteFixed(Mood);
            h.WriteLong(MoodSeason);
            h.WriteInt(RaiseRefusals);
            h.WriteLong(EventSeason);
            h.WriteLong(SickUntil);
            h.WriteLong(Advance);
            h.WriteBool(Leaving);
        }

        internal static HrState ReadFrom(SaveReader r)
        {
            return new HrState
            {
                Mood = r.ReadFixed(), MoodSeason = r.ReadLong(), RaiseRefusals = r.ReadInt(), EventSeason = r.ReadLong(), SickUntil = r.ReadLong(),
                Advance = r.ReadLong(), Leaving = r.ReadBool(),
            };
        }
    }

    public enum HrEventKind
    {
        RaiseRequest = 0,
        Poaching = 1,
        SickLeave = 2,
        FamilyEmergency = 3,
        BadBatch = 4,
    }

    /// <summary>
    /// An HR event waiting for the player (spec 015 §3.7): no deadline, the worker carrying on meanwhile. Amount is the wage asked, the
    /// offer from elsewhere, the sick days' wage, the advance, or what the bad batch would fetch sold cheap; a bad batch holds its goods.
    /// </summary>
    public sealed class HrEvent
    {
        public long Id { get; internal set; }
        public HrEventKind Kind { get; internal set; }

        /// <summary>0 for a farm hand, else the business's id.</summary>
        public long Place { get; internal set; }

        /// <summary>The hand's or the business worker's id.</summary>
        public long WorkerId { get; internal set; }
        public long At { get; internal set; }
        public long Amount { get; internal set; }

        /// <summary>A bad batch's good (by index), grams and cost, set aside from the business's stock.</summary>
        public int Good { get; internal set; } = -1;
        public long Grams { get; internal set; }
        public long Cost { get; internal set; }

        internal HrEvent Clone() { return (HrEvent)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt((int)Kind);
            h.WriteLong(Place);
            h.WriteLong(WorkerId);
            h.WriteLong(At);
            h.WriteLong(Amount);
            h.WriteBool(Good >= 0);
            if (Good < 0) return;
            h.WriteRef(ContentTable.Good, Good);
            h.WriteLong(Grams);
            h.WriteLong(Cost);
        }

        internal static HrEvent ReadFrom(SaveReader r)
        {
            var e = new HrEvent
            {
                Id = r.ReadLong(), Kind = (HrEventKind)r.ReadInt(), Place = r.ReadLong(), WorkerId = r.ReadLong(), At = r.ReadLong(), Amount = r.ReadLong(),
            };
            if (!r.ReadBool()) return e;
            e.Good = r.ReadRef(ContentTable.Good);
            e.Grams = r.ReadLong();
            e.Cost = r.ReadLong();
            return e;
        }
    }

    /// <summary>The people part of the game state (spec 006 §4). Every list is kept in id order.</summary>
    public sealed class PeopleState
    {
        public List<Neighbour> Roster { get; } = new List<Neighbour>();
        public List<Applicant> Applicants { get; } = new List<Applicant>();
        public List<Hand> Hands { get; } = new List<Hand>();
        public List<StandingOrder> Orders { get; } = new List<StandingOrder>();
        public List<Construction> Constructions { get; } = new List<Construction>();

        /// <summary>Open posts in place order (spec 015 §3.1).</summary>
        public List<JobPost> Posts { get; } = new List<JobPost>();

        /// <summary>Your businesses' hired workers, in id order (schema 11).</summary>
        public List<Worker> Workers { get; } = new List<Worker>();
        public long NextWorkerId { get; internal set; } = 1;

        /// <summary>Employer reputation 0–100 (spec 015 §3.8).</summary>
        public int Reputation { get; internal set; } = 50;

        /// <summary>HR events waiting for the player, in id order (spec 015 §3.7).</summary>
        public List<HrEvent> HrEvents { get; } = new List<HrEvent>();
        public long NextHrEventId { get; internal set; } = 1;

        /// <summary>The farm hand's post, or null.</summary>
        public JobPost Post { get { return PostAt(0); } }

        public JobPost PostAt(long place) { return Posts.Find(x => x.Place == place); }

        public Worker WorkerAt(long place) { return Workers.Find(x => x.Place == place); }

        /// <summary>Applicants for a place, in id order.</summary>
        public List<Applicant> ApplicantsFor(long place) { return Applicants.FindAll(x => x.Place == place); }

        /// <summary>
        /// Those from outside waiting for a place (migrants and regulars): at most maxApplicants of them, and as many of your
        /// townspeople besides, so strangers never crowd your own people out (spec 015 §3.1).
        /// </summary>
        public int OutsidersFor(long place) { return Applicants.FindAll(x => x.Place == place && x.CitizenId == 0).Count; }

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
                NextApplicantAt = NextApplicantAt, NextApplicantId = NextApplicantId, NextHandId = NextHandId,
                NextConstructionId = NextConstructionId, NextWorkerId = NextWorkerId, Reputation = Reputation, NextHrEventId = NextHrEventId,
            };
            foreach (HrEvent e in HrEvents) c.HrEvents.Add(e.Clone());
            foreach (JobPost x in Posts) c.Posts.Add(x.Clone());
            foreach (Worker w in Workers) c.Workers.Add(w.Clone());
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
            bool v11 = !(h is SaveWriter sw && sw.Schema < 11);
            if (!v11 || (h.Hashing && Posts.TrueForAll(x => x.Place == 0)))
            {
                // Schema 10's single post (and the hash before business posts, so golden replays are unchanged).
                JobPost post = Post;
                h.WriteBool(post != null);
                if (post != null)
                {
                    h.WriteBool(post.AboveMarket);
                    h.WriteLong(post.PostedAt);
                    h.WriteLong(post.ExpiresAt);
                }
            }
            else
            {
                h.WriteInt(Posts.Count);
                foreach (JobPost x in Posts)
                {
                    h.WriteLong(x.Place);
                    h.WriteBool(x.AboveMarket);
                    h.WriteLong(x.PostedAt);
                    h.WriteLong(x.ExpiresAt);
                    h.WriteLong(x.NextApplicantAt);
                }
            }
            h.WriteLong(NextApplicantAt);
            h.WriteLong(NextApplicantId);
            h.WriteLong(NextHandId);
            h.WriteLong(NextConstructionId);
            if (!v11 || (h.Hashing && Workers.Count == 0 && NextWorkerId == 1 && Reputation == 50 && NextHrEventId == 1)) return; // hashed only once used
            h.WriteInt(Workers.Count);
            foreach (Worker w in Workers) w.WriteTo(h);
            h.WriteLong(NextWorkerId);
            h.WriteInt(Reputation);
            h.WriteInt(HrEvents.Count);
            foreach (HrEvent e in HrEvents) e.WriteTo(h);
            h.WriteLong(NextHrEventId);
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
            if (r.Schema < 11)
            {
                if (r.ReadBool()) s.Posts.Add(new JobPost { AboveMarket = r.ReadBool(), PostedAt = r.ReadLong(), ExpiresAt = r.ReadLong() });
            }
            else
            {
                n = r.ReadCount();
                for (int i = 0; i < n; i++)
                    s.Posts.Add(new JobPost { Place = r.ReadLong(), AboveMarket = r.ReadBool(), PostedAt = r.ReadLong(), ExpiresAt = r.ReadLong(), NextApplicantAt = r.ReadLong() });
            }
            s.NextApplicantAt = r.ReadLong();
            s.NextApplicantId = r.ReadLong();
            s.NextHandId = r.ReadLong();
            s.NextConstructionId = r.ReadLong();
            if (r.Schema < 11) return s; // workers come from the businesses' old ones (CitizenRules.MigrateWorkers)
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Workers.Add(Worker.ReadFrom(r));
            s.NextWorkerId = r.ReadLong();
            s.Reputation = r.ReadInt();
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.HrEvents.Add(HrEvent.ReadFrom(r));
            s.NextHrEventId = r.ReadLong();
            return s;
        }
    }
}
