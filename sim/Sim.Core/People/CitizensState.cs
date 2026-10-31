using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.People
{
    /// <summary>A citizen's work (spec 014 §3.6).</summary>
    public enum CitizenJob
    {
        None = 0,

        /// <summary>The founder's resident hand: <see cref="Citizen.HandId"/>.</summary>
        Hand = 1,

        /// <summary>A day labourer in the region on about half the days (§3.6).</summary>
        Outside = 2,

        Retired = 3,

        /// <summary>Works at one of the founder's businesses: <see cref="Citizen.Employer"/> (spec 016 §3.6). A town job.</summary>
        Business = 4,
    }

    /// <summary>Living in town, or kept in the family tree after leaving it (§3.3).</summary>
    public enum CitizenGone
    {
        Here = 0,
        Died = 1,
        Left = 2,

        /// <summary>A daughter who married into her husband's village (ADR 0070).</summary>
        MarriedAway = 3,
    }

    /// <summary>What a story line on the citizen card tells (§3.3).</summary>
    public enum StoryKind
    {
        Arrived = 0,
        Hired = 1,
        Married = 2,
        ChildBorn = 3,
        Retired = 4,
        Died = 5,
        Left = 6,
        Born = 7,
        MarriedAway = 8,
    }

    /// <summary>One line of a citizen's story: what, when, and who else (a spouse, a child), or 0.</summary>
    public readonly struct StoryLine
    {
        public StoryKind Kind { get; }
        public long At { get; }
        public long Other { get; }

        public StoryLine(StoryKind kind, long at, long other)
        {
            Kind = kind;
            At = at;
            Other = other;
        }
    }

    /// <summary>A person living on the founder's land (spec 014 §3.3), or one kept in the family tree after they went.</summary>
    public sealed class Citizen
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="Definitions.PeopleDefs.Names"/>.</summary>
        public int Name { get; internal set; }
        public bool Female { get; internal set; }

        /// <summary>The season index (t ÷ S) at which they were 0: age = (season − born) × lifeYearsPerSeason (§3.2). Negative for adults who arrived.</summary>
        public long BornSeason { get; internal set; }
        public long HouseholdId { get; internal set; }

        /// <summary>0 none, 1 primary (§3.3).</summary>
        public int Education { get; internal set; }

        /// <summary>Farming 0–100. A hand's own record (<see cref="Hand.Skill"/>) is the live one while he works for you.</summary>
        public Fixed Skill { get; internal set; }

        /// <summary>Crafting 0–100 (spec 015 §3.4, schema 11): the miller's and the baker's skill. A worker's own record is the live one while they work.</summary>
        public Fixed Crafting { get; internal set; }
        public int Trait1 { get; internal set; }
        public int Trait2 { get; internal set; }

        /// <summary>0.5–1.0 (§3.3).</summary>
        public Fixed Health { get; internal set; }
        public CitizenJob Job { get; internal set; }

        /// <summary>The <see cref="Hand"/> id while <see cref="Job"/> is Hand, else 0.</summary>
        public long HandId { get; internal set; }

        // Family by id (0 = unknown or none, §3.3).
        public long Father { get; internal set; }
        public long Mother { get; internal set; }
        public long Spouse { get; internal set; }

        /// <summary>The business they work at while <see cref="Job"/> is Business, else 0 (schema 10).</summary>
        public long Employer { get; internal set; }

        /// <summary>The home village they came from or went to (index into the villages), or −1.</summary>
        public int Village { get; internal set; } = -1;

        /// <summary>Hired as a day labourer by the founder until this time (§3.6), or 0.</summary>
        public long LabourUntil { get; internal set; }

        /// <summary>When the last labour hire ended (0 never): that day's outside work isn't paid as well.</summary>
        public long LabouredUntil { get; internal set; }

        public bool Labouring(long t) { return LabourUntil > t; }

        public CitizenGone Gone { get; internal set; }
        public long GoneAt { get; internal set; }

        public List<StoryLine> Story { get; internal set; } = new List<StoryLine>();

        public bool Living { get { return Gone == CitizenGone.Here; } }

        internal Citizen Clone()
        {
            var c = (Citizen)MemberwiseClone();
            c.Story = new List<StoryLine>(Story);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Name, Name);
            h.WriteBool(Female);
            h.WriteLong(BornSeason);
            h.WriteLong(HouseholdId);
            h.WriteInt(Education);
            h.WriteFixed(Skill);
            h.WriteRef(ContentTable.Trait, Trait1);
            h.WriteRef(ContentTable.Trait, Trait2);
            h.WriteFixed(Health);
            h.WriteInt((int)Job);
            h.WriteLong(HandId);
            h.WriteLong(Father);
            h.WriteLong(Mother);
            h.WriteLong(Spouse);
            h.WriteRef(ContentTable.Village, Village);
            h.WriteLong(LabourUntil);
            h.WriteLong(LabouredUntil);
            h.WriteInt((int)Gone);
            h.WriteLong(GoneAt);
            h.WriteInt(Story.Count);
            foreach (StoryLine s in Story)
            {
                h.WriteInt((int)s.Kind);
                h.WriteLong(s.At);
                h.WriteLong(s.Other);
            }
            if (!(h is SaveWriter sw && sw.Schema < 10) && (!h.Hashing || Employer != 0)) h.WriteLong(Employer); // hashed only once used
            if (!(h is SaveWriter sw11 && sw11.Schema < 11)) h.WriteFixed(Crafting);
        }

        internal static Citizen ReadFrom(SaveReader r)
        {
            var c = new Citizen
            {
                Id = r.ReadLong(), Name = r.ReadRef(ContentTable.Name), Female = r.ReadBool(), BornSeason = r.ReadLong(), HouseholdId = r.ReadLong(),
                Education = r.ReadInt(), Skill = r.ReadFixed(), Trait1 = r.ReadRef(ContentTable.Trait), Trait2 = r.ReadRef(ContentTable.Trait),
                Health = r.ReadFixed(), Job = (CitizenJob)r.ReadInt(), HandId = r.ReadLong(), Father = r.ReadLong(), Mother = r.ReadLong(),
                Spouse = r.ReadLong(), Village = r.ReadRef(ContentTable.Village), LabourUntil = r.ReadLong(), LabouredUntil = r.ReadLong(), Gone = (CitizenGone)r.ReadInt(), GoneAt = r.ReadLong(),
            };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) c.Story.Add(new StoryLine((StoryKind)r.ReadInt(), r.ReadLong(), r.ReadLong()));
            if (r.Schema >= 10) c.Employer = r.ReadLong();
            if (r.Schema >= 11) c.Crafting = r.ReadFixed();
            return c;
        }
    }

    /// <summary>A family sharing a home (spec 014 §3.4): its season budget, needs and happiness.</summary>
    public sealed class Household
    {
        public long Id { get; internal set; }

        /// <summary>Living members' citizen ids, ascending.</summary>
        public List<long> Members { get; internal set; } = new List<long>();

        /// <summary>The <see cref="Land.Building"/> id of the hut or family house they live in.</summary>
        public long HomeId { get; internal set; }

        public long Savings { get; internal set; }

        // The season's budget (§3.5), set at each close.
        public long Income { get; internal set; }
        public long Food { get; internal set; }
        public long FoodNeed { get; internal set; }
        public Fixed FoodMet { get; internal set; } = Fixed.One;
        public long Rent { get; internal set; }
        public long Clothing { get; internal set; }
        public long HealthCare { get; internal set; }
        public long Schooling { get; internal set; }
        public long Leisure { get; internal set; }
        public long Saved { get; internal set; }

        /// <summary>The outside pay the budget expects this season, and what outside work and day labour have paid so far (§3.6).</summary>
        public long OutsideExpected { get; internal set; }
        public long Earned { get; internal set; }

        /// <summary>Seasons of rent not paid (§3.4).</summary>
        public int RentBehind { get; internal set; }

        // Needs met 0–1 and happiness 0–100 (§3.8), updated at each daily pass.
        public Fixed Survival { get; internal set; } = Fixed.One;
        public Fixed Security { get; internal set; }
        public Fixed Belonging { get; internal set; }
        public Fixed Growth { get; internal set; }
        public Fixed Happiness { get; internal set; }

        /// <summary>Season closes in a row below <c>leaveBelow</c> (§3.10).</summary>
        public int LowSeasons { get; internal set; }

        /// <summary>The season index through which the household mourns (§3.9), or −1.</summary>
        public long MourningSeason { get; internal set; } = -1;

        internal Household Clone()
        {
            var c = (Household)MemberwiseClone();
            c.Members = new List<long>(Members);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt(Members.Count);
            foreach (long m in Members) h.WriteLong(m);
            h.WriteLong(HomeId);
            h.WriteLong(Savings);
            h.WriteLong(Income);
            h.WriteLong(Food);
            h.WriteLong(FoodNeed);
            h.WriteFixed(FoodMet);
            h.WriteLong(Rent);
            h.WriteLong(Clothing);
            h.WriteLong(HealthCare);
            h.WriteLong(Schooling);
            h.WriteLong(Leisure);
            h.WriteLong(Saved);
            h.WriteLong(OutsideExpected);
            h.WriteLong(Earned);
            h.WriteInt(RentBehind);
            h.WriteFixed(Survival);
            h.WriteFixed(Security);
            h.WriteFixed(Belonging);
            h.WriteFixed(Growth);
            h.WriteFixed(Happiness);
            h.WriteInt(LowSeasons);
            h.WriteLong(MourningSeason);
        }

        internal static Household ReadFrom(SaveReader r)
        {
            var x = new Household { Id = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) x.Members.Add(r.ReadLong());
            x.HomeId = r.ReadLong();
            x.Savings = r.ReadLong();
            x.Income = r.ReadLong();
            x.Food = r.ReadLong();
            x.FoodNeed = r.ReadLong();
            x.FoodMet = r.ReadFixed();
            x.Rent = r.ReadLong();
            x.Clothing = r.ReadLong();
            x.HealthCare = r.ReadLong();
            x.Schooling = r.ReadLong();
            x.Leisure = r.ReadLong();
            x.Saved = r.ReadLong();
            x.OutsideExpected = r.ReadLong();
            x.Earned = r.ReadLong();
            x.RentBehind = r.ReadInt();
            x.Survival = r.ReadFixed();
            x.Security = r.ReadFixed();
            x.Belonging = r.ReadFixed();
            x.Growth = r.ReadFixed();
            x.Happiness = r.ReadFixed();
            x.LowSeasons = r.ReadInt();
            x.MourningSeason = r.ReadLong();
            return x;
        }
    }

    /// <summary>Citizens and households (spec 014 §4; save schema 9). Null when the content has no family house.</summary>
    public sealed class CitizensState
    {
        /// <summary>In id order: the living and the departed still linked to them (§3.3).</summary>
        public List<Citizen> Citizens { get; internal set; } = new List<Citizen>();

        /// <summary>In id order.</summary>
        public List<Household> Households { get; internal set; } = new List<Household>();
        public long NextCitizenId { get; internal set; } = 1;
        public long NextHouseholdId { get; internal set; } = 1;

        /// <summary>When the next family may arrive at an empty house (§3.10), or 0 when none is due.</summary>
        public long NextFamilyAt { get; internal set; }

        /// <summary>Family houses being built (§3.4), in id order; each becomes a building when its job ends.</summary>
        public List<Construction> Constructions { get; internal set; } = new List<Construction>();
        public long NextConstructionId { get; internal set; } = 1;

        /// <summary>A free family house offered to a hand living alone (§3.13): the hand, the house, when; 0 when none is open.</summary>
        public long OfferHandId { get; internal set; }
        public long OfferHouseId { get; internal set; }
        public long OfferAt { get; internal set; }

        public Citizen Citizen(long id) { return Citizens.Find(c => c.Id == id); }

        public Household Household(long id) { return Households.Find(x => x.Id == id); }

        /// <summary>The citizen who is this hand, or null.</summary>
        public Citizen OfHand(long handId) { return Citizens.Find(c => c.Living && c.Job == CitizenJob.Hand && c.HandId == handId); }

        /// <summary>People living in the town (§3.10).</summary>
        public int Population
        {
            get
            {
                int n = 0;
                foreach (Citizen c in Citizens) if (c.Living) n++;
                return n;
            }
        }

        internal CitizensState Clone()
        {
            var c = (CitizensState)MemberwiseClone();
            c.Citizens = Citizens.ConvertAll(x => x.Clone());
            c.Households = Households.ConvertAll(x => x.Clone());
            c.Constructions = Constructions.ConvertAll(x => x.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Citizens.Count);
            foreach (Citizen c in Citizens) c.WriteTo(h);
            h.WriteInt(Households.Count);
            foreach (Household x in Households) x.WriteTo(h);
            h.WriteLong(NextCitizenId);
            h.WriteLong(NextHouseholdId);
            h.WriteInt(Constructions.Count);
            foreach (Construction x in Constructions) x.WriteTo(h);
            h.WriteLong(NextConstructionId);
            h.WriteLong(NextFamilyAt);
            h.WriteLong(OfferHandId);
            h.WriteLong(OfferHouseId);
            h.WriteLong(OfferAt);
        }

        internal static CitizensState ReadFrom(SaveReader r)
        {
            var s = new CitizensState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Citizens.Add(People.Citizen.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Households.Add(People.Household.ReadFrom(r));
            s.NextCitizenId = r.ReadLong();
            s.NextHouseholdId = r.ReadLong();
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Constructions.Add(Construction.ReadFrom(r));
            s.NextConstructionId = r.ReadLong();
            s.NextFamilyAt = r.ReadLong();
            s.OfferHandId = r.ReadLong();
            s.OfferHouseId = r.ReadLong();
            s.OfferAt = r.ReadLong();
            return s;
        }
    }
}
