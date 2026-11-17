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

        public TownshipDef(RoadDef road, ClinicDef clinic, QuartersDef quarters, DoctorDef doctor, GateDef gate, CohortDef cohort)
        {
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
}
