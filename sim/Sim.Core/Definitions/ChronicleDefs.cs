using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// The moments the Chronicle keeps (spec 029 §3.2, ADR 0165). The numbers are saved, so a kind is only ever added at the end. The first
    /// fourteen are written once; <see cref="RecordHarvest"/> to <see cref="PetitionKept"/> repeat at most once a season; a story's step and
    /// the line a town's Chronicle begins with are written whenever they come.
    /// </summary>
    public enum ChronicleKind
    {
        Founding = 0,
        Hamlet = 1,
        Village = 2,
        Township = 3,
        FirstSettler = 4,
        FirstChild = 5,
        FirstLiterateChild = 6,
        FirstAdultReader = 7,
        FirstFestival = 8,
        SchoolOpened = 9,
        ClinicBuilt = 10,
        RoadBuilt = 11,
        FirstOrchard = 12,
        FirstProfit = 13,
        RecordHarvest = 14,
        BusinessFounded = 15,
        BusinessSold = 16,
        BusinessClosed = 17,
        FireSurvived = 18,
        EpidemicSurvived = 19,
        LoanCleared = 20,
        PetitionKept = 21,
        Story = 22,
        Begins = 23,
    }

    /// <summary>One kind of moment: its id (the string table's key is <c>chronicle.&lt;id&gt;</c>) and the icon its line shows.</summary>
    public sealed class ChronicleKindDef
    {
        public string Id { get; internal set; }
        public string Icon { get; internal set; }
    }

    /// <summary><c>chronicle.json</c> (spec 029 §4): how many entries are kept, the record harvest's floor and rise, and each kind's id and icon.</summary>
    public sealed class ChronicleDefs
    {
        public static readonly string[] KindIds =
        {
            "founding", "hamlet", "village", "township", "firstSettler", "firstChild", "firstLiterateChild", "firstAdultReader", "firstFestival",
            "schoolOpened", "clinicBuilt", "roadBuilt", "firstOrchard", "firstProfit", "recordHarvest", "businessFounded", "businessSold",
            "businessClosed", "fireSurvived", "epidemicSurvived", "loanCleared", "petitionKept", "story", "begins",
        };

        /// <summary>Entries kept; the oldest repeating one goes first when full (the once-only kinds, the eras and the founding never go).</summary>
        public int Keep { get; internal set; }

        /// <summary>A crop's season total of at least this many grams, and at least <see cref="RecordRise"/> above its best, is a record (§3.3).</summary>
        public long RecordMinGrams { get; internal set; }
        public Fixed RecordRise { get; internal set; }

        /// <summary>The Almanacs kept (§3.4), and the profit a best return must reach (1/100 coin): a smaller one is "a quiet year".</summary>
        public int AlmanacKeep { get; internal set; }
        public long MinProfit { get; internal set; }

        public IReadOnlyList<ChronicleKindDef> Kinds { get; internal set; }

        /// <summary>True for the fourteen kinds written once (§3.2): they are pinned.</summary>
        public static bool Once(ChronicleKind k) { return k <= ChronicleKind.FirstProfit; }

        /// <summary>True for the eight kinds that write at most one a season (§3.2).</summary>
        public static bool Repeats(ChronicleKind k) { return k >= ChronicleKind.RecordHarvest && k <= ChronicleKind.PetitionKept; }

        /// <summary>The kinds the cap never drops: the once-only ones and the line a Chronicle begins with.</summary>
        public static bool Pinned(ChronicleKind k) { return Once(k) || k == ChronicleKind.Begins; }
    }

    /// <summary>The four citizen arcs (spec 029 §3.6): the order is their priority when several steps fall on one close.</summary>
    public enum StoryArc { Stall = 0, Reader = 1, Migrant = 2, LongLife = 3 }

    /// <summary><c>stories.json</c> (spec 029 §3.6, §4): every number of the four arcs.</summary>
    public sealed class StoriesDefs
    {
        /// <summary>A stall of his own: seasons worked, household savings (1/100 coin, year-0), motivation, the chance each season, and the seasons after leaving the stall is judged.</summary>
        public int StallSeasons { get; internal set; }
        public long StallSavings { get; internal set; }
        public Fixed StallMotivation { get; internal set; }
        public Fixed StallChance { get; internal set; }
        public int StallJudgedAfter { get; internal set; }

        /// <summary>The first reader in the family: the age at which a paid job or a hire is the third step.</summary>
        public int ReaderAdultAge { get; internal set; }

        /// <summary>A migrant family that stays: the seasons of the first and second step.</summary>
        public int MigrantFirst { get; internal set; }
        public int MigrantSecond { get; internal set; }

        /// <summary>A long life: the three ages, and the age from which a death is remembered.</summary>
        public IReadOnlyList<int> LifeAges { get; internal set; }
        public int LifeDiedFrom { get; internal set; }
    }
}
