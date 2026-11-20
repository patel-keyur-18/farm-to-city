using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Town life (spec 026, <c>township.json</c> <c>townLife</c>): the five reputation scores' weights, the pull on migration and the grants' bonus,
    /// approval, crime and inequality. Festivals and petitions join it by story.
    /// </summary>
    public sealed class TownLifeDef
    {
        public const int Livability = 0, Business = 1, Education = 2, Health = 3, Environment = 4, Scores = 5, PartsEach = 3;

        /// <summary>The weights (summing to 100) of each score's three parts, in the order spec 026 §3.4 names them.</summary>
        public System.Collections.Generic.IReadOnlyList<int[]> Weights { get; internal set; }

        /// <summary>The pull on a family's wait: <c>100 + (approval − 50) ÷ divisor + (mean − 50) ÷ divisor</c>, between <c>PullMin</c> and <c>PullMax</c> (§3.7).</summary>
        public int PullDivisor { get; internal set; }
        public int PullMin { get; internal set; }
        public int PullMax { get; internal set; }

        /// <summary>The grants rise by <c>(mean − 50) ÷ divisor</c> percent above a mean of 50, never lowered (§3.4).</summary>
        public int GrantBonusDivisor { get; internal set; }

        /// <summary>The business-climate part for the road: a track, and the paved link road (§3.4).</summary>
        public int RoadTrack { get; internal set; }

        public int ApprovalStart { get; internal set; }

        /// <summary>Crime (§3.5): <c>base + perUnemployed × unemployed% + inequalityShare × the inequality score</c>.</summary>
        public int CrimeBase { get; internal set; }
        public Fixed CrimePerUnemployed { get; internal set; }
        public Fixed CrimeInequalityShare { get; internal set; }

        /// <summary>The land-value term for crime: <c>step</c> for every 10 points of crime under (or over) 50, at most <c>max</c>.</summary>
        public Fixed CrimeLandValueStep { get; internal set; }
        public Fixed CrimeLandValueMax { get; internal set; }

        /// <summary>Inequality (§3.6): needs this many of your households; each whole point of the ratio above <c>HappinessAbove</c> costs a household below the median a point, at most <c>MaxPoints</c>; from <c>PushOutAt</c> the poorest tenth leave a season sooner; the ratio is capped.</summary>
        public int NeedsHouseholds { get; internal set; }
        public int HappinessAbove { get; internal set; }
        public int MaxPoints { get; internal set; }
        public int PushOutAt { get; internal set; }
        public int RatioCap { get; internal set; }

        /// <summary>Petitions and approval (§3.2, §3.3).</summary>
        public PetitionDef Petition { get; internal set; }

        /// <summary>Festivals (§3.1).</summary>
        public FestivalDef Festival { get; internal set; }
    }

    /// <summary>One festival a season (spec 026 §3.1): its content id (a string <c>festival.&lt;id&gt;</c>), the season it falls in and its cost a person.</summary>
    public sealed class FestivalKindDef
    {
        public string Id { get; internal set; }
        public int Season { get; internal set; }
        public int PerPerson { get; internal set; }
    }

    /// <summary>Festivals (spec 026 §3.1): the four kinds, the venues' reach, the happiness lift, the shops' rise and how far ahead a day can be chosen.</summary>
    public sealed class FestivalDef
    {
        public const int Hall = 0, Square = 1, Town = 0, You = 1;

        /// <summary>The four festivals, in season order (Spring, Summer, Monsoon, Winter).</summary>
        public System.Collections.Generic.IReadOnlyList<FestivalKindDef> Kinds { get; internal set; }

        /// <summary>Happiness points on the day: inside the venue's reach, and elsewhere in the town.</summary>
        public int InReach { get; internal set; }
        public int Outside { get; internal set; }

        /// <summary>The square's reach in metres (the hall's is its own).</summary>
        public int SquareReach { get; internal set; }

        /// <summary>The day can be 1 to this many days ahead.</summary>
        public int DaysAhead { get; internal set; }

        /// <summary>Festivals kept for the hall's sheet.</summary>
        public int Keep { get; internal set; }

        /// <summary>The shops' demand rise on the day.</summary>
        public Fixed ShopChai { get; internal set; }
        public Fixed ShopSweets { get; internal set; }
        public Fixed ShopVeg { get; internal set; }
    }

    /// <summary>
    /// Petitions and approval (spec 026 §3.2, §3.3): when families raise one, how it can end and what each ending does to approval, and how approval moves a close.
    /// </summary>
    public sealed class PetitionDef
    {
        /// <summary>The eight topics: the clinic, the school, the community hall, the watchman, the playground, water, rent too high, no work.</summary>
        public const int Clinic = 0, School = 1, Hall = 2, Watchman = 3, Playground = 4, Water = 5, Rent = 6, Work = 7, Topics = 8;

        /// <summary>A petition is raised when this many of your households (or <c>MinShare</c> of them, whichever is more) share the need.</summary>
        public int MinHouseholds { get; internal set; }
        public Fixed MinShare { get; internal set; }

        /// <summary>A household's rent over this share of its income raises the rent petition.</summary>
        public Fixed RentShare { get; internal set; }

        /// <summary>The closes a promise can run: 1, 2 or 4.</summary>
        public System.Collections.Generic.IReadOnlyList<int> PromiseCloses { get; internal set; }

        /// <summary>Approval points for each ending.</summary>
        public int Done { get; internal set; }
        public int Kept { get; internal set; }
        public int Declined { get; internal set; }
        public int Ignored { get; internal set; }
        public int Broken { get; internal set; }

        /// <summary>Closes a topic is quiet after a petition ends, and after one is declined; closes an unanswered petition waits before it is ignored.</summary>
        public int QuietCloses { get; internal set; }
        public int DeclinedQuietCloses { get; internal set; }
        public int IgnoredAfter { get; internal set; }

        /// <summary>Approval moves <c>(happiness − neutral) ÷ step</c> a close, at most <c>StepMax</c> either way.</summary>
        public int NeutralHappiness { get; internal set; }
        public int Step { get; internal set; }
        public int StepMax { get; internal set; }

        /// <summary>Settled petitions kept for the sheet, and the closes of approval shown.</summary>
        public int KeepEnded { get; internal set; }
        public int KeepHistory { get; internal set; }
    }
}
