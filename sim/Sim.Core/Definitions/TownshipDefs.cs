using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>The Township's content (spec 023 §4, <c>content/township.json</c>): the paved link road now; the clinic, the gate and the cohort join it by story.</summary>
    public sealed class TownshipDef
    {
        public RoadDef Road { get; }
        public ClinicDef Clinic { get; }
        public QuartersDef Quarters { get; }
        public DoctorDef Doctor { get; }
        public GateDef Gate { get; }
        public CohortDef Cohort { get; }

        /// <summary>The three civic buildings (spec 023 §3.7) in index order: the watchman's post, the community hall, the playground.</summary>
        public System.Collections.Generic.IReadOnlyList<CivicDef> Civic { get; }

        /// <summary>The safety a town has with a post built but nobody in it or out of reach (spec 023 §3.7: 0.5).</summary>
        public Fixed SafetyBase { get; }

        /// <summary>What makes an acre valuable (spec 024 §3.1) and how rent and a slow sale follow it.</summary>
        public LandValueDef LandValue { get; }

        /// <summary>Zoning and the builders (spec 024 §3.3).</summary>
        public ZoningDef Zoning { get; }

        /// <summary>What a new building takes from the Township: materials, masons and time (spec 025 §3.2).</summary>
        public ConstructionDef Construction { get; set; }

        /// <summary>What a smoking building does to the land and the homes near it (spec 025 §3.4, §3.3): the generator's and the kiln's.</summary>
        public SmokeDef Smoke { get; set; }
        public UtilitiesDef Utilities { get; set; }

        /// <summary>Making a family house pucca (spec 024 §3.7, US-152).</summary>
        public UpgradeDef Upgrade { get; set; }

        /// <summary>Cold-store space in the mandi town (spec 025 §3.6, US-242).</summary>
        public ColdStoreDef ColdStore { get; set; }

        /// <summary>Town life (spec 026): reputation, approval, crime and inequality. Null in content without it.</summary>
        public TownLifeDef TownLife { get; set; }

        public TownshipDef(RoadDef road, ClinicDef clinic, QuartersDef quarters, DoctorDef doctor, GateDef gate, CohortDef cohort,
            System.Collections.Generic.IReadOnlyList<CivicDef> civic, Fixed safetyBase, LandValueDef landValue, ZoningDef zoning)
        {
            Zoning = zoning;
            LandValue = landValue;
            Civic = civic;
            SafetyBase = safetyBase;
            Gate = gate;
            Cohort = cohort;
            Road = road;
            Clinic = clinic;
            Quarters = quarters;
            Doctor = doctor;
        }
    }

    /// <summary>The village clinic (spec 023 §3.1, §3.2, §3.6): the district builds it for your share; what it does for the sick and the old.</summary>
    public sealed class ClinicDef
    {
        /// <summary>Index of the era it can be built from (the Village), or 0 without eras.</summary>
        public int UnlockEra { get; internal set; }

        /// <summary>The clinic plot's tile (the map's <c>clinic_plot</c>).</summary>
        public int Tile { get; internal set; }

        /// <summary>The community share, a gift to the village (year-0 coins, indexed at the press), and the hours the district takes.</summary>
        public long Share { get; internal set; }
        public int BuildHours { get; internal set; }

        /// <summary>Hours from both standing to the doctor's arrival.</summary>
        public int DoctorArrivesHours { get; internal set; }

        /// <summary>The five effects at full strength (spec 023 §3.2): sick chance cut, health loss cut, recovery boost, elders' death cut.</summary>
        public Fixed SickCut { get; internal set; }
        public Fixed LossCut { get; internal set; }
        public Fixed GainBoost { get; internal set; }
        public Fixed DeathCut { get; internal set; }

        /// <summary>Metres of reach and people it serves at full strength (spec 023 §3.6).</summary>
        public int Reach { get; internal set; }
        public int Capacity { get; internal set; }
    }

    /// <summary>The doctor's quarters (spec 023 §3.1): a larger family house, yours, reserved for the doctor.</summary>
    public sealed class QuartersDef
    {
        /// <summary>The quarters plot's tile (the map's <c>quarters_plot</c>) and the building it is (an index into <see cref="GameDefs.Buildings"/>).</summary>
        public int Tile { get; internal set; }
        public int Building { get; internal set; }

        /// <summary>The rent a season the doctor pays (year-0 coins, indexed) and the hours to build.</summary>
        public long Rent { get; internal set; }
        public int BuildHours { get; internal set; }
    }

    /// <summary>The doctor (spec 023 §3.1, §3.9): his income, when he leaves and when a new one comes.</summary>
    public sealed class DoctorDef
    {
        /// <summary>What the district pays him a season (year-0 coins, indexed): the household's income, never your cost.</summary>
        public long Salary { get; internal set; }

        /// <summary>Season closes in a row with Health's condition under the treasury's <c>healthLeaveBelow</c> before he leaves (spec 023 §3.9).</summary>
        public int LeaveCloses { get; internal set; }

        /// <summary>A new doctor comes when Health's condition is at least this again and the cash covers the allowance.</summary>
        public Fixed ReturnCondition { get; internal set; }
    }

    /// <summary>The petition for the paved link road (spec 023 §3.3).</summary>
    public sealed class RoadDef
    {
        /// <summary>Households of yours at which the council hall's petition is open.</summary>
        public int PetitionHouseholds { get; }

        /// <summary>The community share, a gift to the village (year-0 coins, indexed at the press).</summary>
        public long Share { get; }

        /// <summary>Days the district takes to build it.</summary>
        public int Days { get; }

        /// <summary>The road tier it brings: a key of <c>region.json</c>'s <c>transportCostPerKg</c>.</summary>
        public string Tier { get; }

        public RoadDef(int petitionHouseholds, long share, int days, string tier)
        {
            PetitionHouseholds = petitionHouseholds;
            Share = share;
            Days = days;
            Tier = tier;
        }
    }

    /// <summary>The Township's gate (spec 023 §3.4): households of yours (the doctor's counts), open businesses with a profitable season, and literacy of the people aged 7 and over.</summary>
    public sealed class GateDef
    {
        public int Households { get; internal set; }
        public int Businesses { get; internal set; }

        /// <summary>The share of your households' people aged 7 and over who can read (0.60).</summary>
        public Fixed Literacy { get; internal set; }
    }

    /// <summary>The old village as one record (spec 023 §3.5): never citizens, no cards, no portraits.</summary>
    public sealed class CohortDef
    {
        public int Households { get; internal set; }
        public int PeoplePerHousehold { get; internal set; }

        /// <summary>The share of those aged 7 and over who can read (0.65), for the statistics.</summary>
        public Fixed Literacy { get; internal set; }

        /// <summary>A household's income a season (year-0 coins, indexed): the base of its budget.</summary>
        public long Income { get; internal set; }

        /// <summary>Happiness under this at <see cref="LeaveCloses"/> closes in a row sends one household away.</summary>
        public int LeaveBelow { get; internal set; }
        public int LeaveCloses { get; internal set; }

        /// <summary>Shares of the people: children, elders (the rest are adults), and the school-age children who can't read yet and join the school free.</summary>
        public Fixed ChildShare { get; internal set; }
        public Fixed ElderShare { get; internal set; }
        public Fixed PupilShare { get; internal set; }
    }

    /// <summary>
    /// A civic building (spec 023 §3.7): commissioned by the treasury on a fixed public plot, paid as <see cref="Work"/>, with an upkeep line and,
    /// if it has a post, a wage line (both in <c>treasury.json</c>'s lines, Safety). Its benefit is <c>gain × e</c> where <c>e = condition × covered × scale</c>.
    /// </summary>
    public sealed class CivicDef
    {
        public const int Watchman = 0, Hall = 1, Playground = 2, Count = 3;

        public int Index { get; internal set; }
        public string Id { get; internal set; }

        /// <summary>The plot's tile (the map's <c>watchman_plot</c>, <c>hall_plot</c> or <c>playground_plot</c>).</summary>
        public int Tile { get; internal set; }

        /// <summary>The capital work (<c>treasury.json</c> works) and the lines that pay its upkeep and its post (an index into the lines, or -1).</summary>
        public string Work { get; internal set; }
        public int UpkeepLine { get; internal set; } = -1;
        public int WageLine { get; internal set; } = -1;

        public int Reach { get; internal set; }
        public int Capacity { get; internal set; }

        /// <summary>What it adds at full strength: the watchman's safety, the hall's belonging, the playground's leisure for households with children.</summary>
        public Fixed Gain { get; internal set; }

        /// <summary>Hours from the post being advertised to a villager taking it, and the unpaid closes in a row before he leaves.</summary>
        public int StaffHours { get; internal set; }
        public int LeaveCloses { get; internal set; }

        public bool HasPost { get { return WageLine >= 0; } }
    }
}

namespace Sim.Core.Definitions
{
    /// <summary>Land value (spec 024 §3.1, <c>township.json</c> <c>landValue</c>): <c>LV</c> starts at 1.00 and adds what is near an acre, held between the floor and the ceiling.</summary>
    public sealed class LandValueDef
    {
        /// <summary>The square's and the pond's tiles (map places) and the metres within which each counts.</summary>
        public int SquareTile { get; internal set; }
        public int WaterTile { get; internal set; }
        public int SquareReach { get; internal set; }
        public int WaterReach { get; internal set; }

        /// <summary>The road an acre touches: the district road and the village lane.</summary>
        public Fixed RoadDistrict { get; internal set; }
        public Fixed RoadLane { get; internal set; }

        /// <summary>The services within reach: the school (its own reach, spec 023), the clinic, the square and water.</summary>
        public Fixed School { get; internal set; }
        public Fixed Clinic { get; internal set; }
        public Fixed Square { get; internal set; }
        public Fixed Water { get; internal set; }

        /// <summary>Each open business with a hired worker within <see cref="JobReach"/> metres adds <see cref="JobEach"/>, at most <see cref="JobMax"/> of them.</summary>
        public Fixed JobEach { get; internal set; }
        public int JobMax { get; internal set; }
        public int JobReach { get; internal set; }

        /// <summary>The town's size: <see cref="SizePer10"/> for each 10 households of yours over <see cref="SizeOver"/>, at most <see cref="SizeMax"/>.</summary>
        public int SizeOver { get; internal set; }
        public Fixed SizePer10 { get; internal set; }
        public Fixed SizeMax { get; internal set; }

        public Fixed Floor { get; internal set; }
        public Fixed Ceiling { get; internal set; }

        /// <summary>Rent's place from the Township: <c>base + slope × (LV − 1)</c>, between min and max (replaces ADR 0110's three constants).</summary>
        public Fixed RentBase { get; internal set; }
        public Fixed RentSlope { get; internal set; }
        public Fixed RentMin { get; internal set; }
        public Fixed RentMax { get; internal set; }

        /// <summary>The rent of a leased-out parcel each season, as a part of its land value (spec 024 §3.6).</summary>
        public Fixed LeaseYield { get; internal set; }

        /// <summary>Seconds a slow sale waits (spec 024 §3.4).</summary>
        public long SlowSaleSeconds { get; internal set; }
    }
}

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Cold-store space in the mandi town (spec 025 §3.6, <c>township.json</c> <c>coldStore</c>): rented by the tonne for a season, from the Township. The goods it
    /// keeps spoil at <see cref="Factor"/> of their rate there (a tenth for potato, a quarter for fruit).
    /// </summary>
    public sealed class ColdStoreDef
    {
        /// <summary>Money a kg a season (year-0 coins, indexed when charged).</summary>
        public long RentPerKgSeason { get; internal set; }
        public int MaxRentTonnes { get; internal set; }

        /// <summary>By good index: the share of its spoilage rate kept in the cold store, or 0 for a good it does not keep.</summary>
        public System.Collections.Generic.IReadOnlyList<Fixed> Factor { get; internal set; }
    }

    /// <summary>Making a kutcha family house pucca (spec 024 §3.7, <c>township.json</c> <c>upgrade</c>): offered from this land value; a pucca house lets at this rent × the place.</summary>
    public sealed class UpgradeDef
    {
        public const string Kind = "pucca_upgrade";

        public Fixed PuccaMinLandValue { get; internal set; }

        /// <summary>A season's rent of a pucca house at the base index (1/100 coin), before the place and prices.</summary>
        public long PuccaRent { get; internal set; }
    }

    /// <summary>Zoning and builders (spec 024 §3.3, <c>township.json</c> <c>zoning</c>).</summary>
    public sealed class ZoningDef
    {
        /// <summary>Families that found no home that a builder needs for each acre it buys; and the homes it builds on each acre.</summary>
        public int FamiliesPerAcre { get; internal set; }
        public int HomesPerAcre { get; internal set; }

        /// <summary>Parcels builders buy across the whole town in a season.</summary>
        public int ParcelsPerSeason { get; internal set; }

        /// <summary>The permit fee, a share of the land's value, paid into the town's purse (income <c>Fees</c>).</summary>
        public Fixed PermitFee { get; internal set; }

        /// <summary>The most families the counter remembers.</summary>
        public int TurnedAwayMax { get; internal set; }
    }
}

namespace Sim.Core.Definitions
{
    /// <summary>One smoking building's reach (metres, an acre is 100), the land value it takes from each acre within it and the health it takes from each home.</summary>
    public sealed class SmokeSource
    {
        public string Id { get; internal set; }
        public int Reach { get; internal set; }
        public Fixed LandValue { get; internal set; }
        public Fixed Health { get; internal set; }
    }

    /// <summary>
    /// One utility (spec 025 §3.1, US-144): a plot on the map, the treasury work that builds it, its reach in metres and capacity in people, and the
    /// treasury lines it opens (upkeep, and a second line: the generator's fuel or the collector's wage).
    /// </summary>
    public sealed class UtilityDef
    {
        public const int Tower = 0, Generator = 1, Grid = 2, Drains = 3, Collection = 4, Count = 5;

        public int Index { get; internal set; }
        public string Id { get; internal set; }
        public int Tile { get; internal set; }
        public string Work { get; internal set; }
        public int UpkeepLine { get; internal set; } = -1;
        public int ExtraLine { get; internal set; } = -1;
        public int Reach { get; internal set; }
        public int Capacity { get; internal set; }

        /// <summary>The reach or capacity is the whole town's (past this, the sheets say so).</summary>
        public const int Whole = 5000;
    }

    /// <summary>The utilities (<c>township.json</c> <c>utilities</c>) and what going without each costs.</summary>
    public sealed class UtilitiesDef
    {
        public System.Collections.Generic.IReadOnlyList<UtilityDef> Steps { get; internal set; }

        /// <summary>Under this condition of the Utilities department a utility covers nobody.</summary>
        public Fixed MinCondition { get; internal set; }

        /// <summary>Residents from which drains and collection are missed.</summary>
        public int TownSize { get; internal set; }

        /// <summary>A top-up gift is the shortfall rounded up to this (1/100 coin).</summary>
        public long GiftRound { get; internal set; }

        /// <summary>Health lost a season in a home beyond clean water; beyond drains; the cap on Health's condition without drains.</summary>
        public Fixed WaterHealth { get; internal set; }
        public Fixed DrainsHealth { get; internal set; }
        public Fixed DrainsHealthCap { get; internal set; }

        /// <summary>Land value lost on a built-up acre beyond drains.</summary>
        public Fixed DrainsLandValue { get; internal set; }

        /// <summary>Belonging lost without collection; the leisure share power adds; shop hours power gives at the usual wage.</summary>
        public Fixed WasteBelonging { get; internal set; }
        public Fixed PowerLeisure { get; internal set; }
        public int EveningHours { get; internal set; }
    }

    /// <summary>Smoke (spec 025): the sources by id and the most land value smoke takes from one acre in all.</summary>
    public sealed class SmokeDef
    {
        public System.Collections.Generic.IReadOnlyList<SmokeSource> Sources { get; internal set; }
        public Fixed LandValueCap { get; internal set; }
        /// <summary>The most health smoke takes from one home in all (ADR 0143): kilns stack, the total does not.</summary>
        public Fixed HealthCap { get; internal set; }

        public SmokeSource Source(string id)
        {
            foreach (SmokeSource s in Sources) if (s.Id == id) return s;
            return null;
        }
    }

    /// <summary>
    /// Construction from the Township (spec 025 §3.2, US-145): bricks, cement and timber from the trader up to a daily limit for the whole town,
    /// masons at a price by the mason-day (or the district's gang at <see cref="GangPrice"/> and <see cref="GangHours"/>), and build time.
    /// Timber is in tenths of a cubic metre.
    /// </summary>
    public sealed class ConstructionDef
    {
        public long DailyBricks { get; set; }
        public long DailyCement { get; set; }
        public long DailyTimber { get; set; }

        /// <summary>The price of one brick, one bag of cement and a tenth of a cubic metre of timber, at the base index (1/100 coin).</summary>
        public long BrickPrice { get; set; }
        public long CementPrice { get; set; }
        public long TimberPrice { get; set; }

        /// <summary>A mason's day, at the base index.</summary>
        public long MasonDay { get; set; }
        public Fixed GangPrice { get; set; }
        public Fixed GangHours { get; set; }
        public int MaxMasons { get; set; }

        /// <summary>The hour the trader's stock fills again.</summary>
        public int RestockHour { get; set; }

        /// <summary>Out of a hundred, the share of grown-ups with no town job who have the mason's trade (a fixed draw for each person).</summary>
        public int MasonShare { get; set; }

        public System.Collections.Generic.IReadOnlyList<BuildKindDef> Kinds { get; set; }

        public BuildKindDef Kind(string id)
        {
            foreach (BuildKindDef k in Kinds) if (k.Id == id) return k;
            return null;
        }

        public BuildKindDef KindOfWork(string work)
        {
            foreach (BuildKindDef k in Kinds) if (k.Work == work) return k;
            return null;
        }

        public int IndexOf(string id)
        {
            for (int i = 0; i < Kinds.Count; i++) if (Kinds[i].Id == id) return i;
            return -1;
        }
    }

    /// <summary>One kind of building: its materials, mason-days and hours; <see cref="Work"/> is the treasury's work that builds it, or null (a family house).</summary>
    public sealed class BuildKindDef
    {
        public string Id { get; set; }
        public string Work { get; set; }
        public long Bricks { get; set; }
        public long Cement { get; set; }
        public long Timber { get; set; }
        public int MasonDays { get; set; }
        public int Hours { get; set; }
    }
}
