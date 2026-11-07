using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// The village's first school (spec 018 §3.1, §4; ADR 0077, 0095): its build (the <c>school</c> block of <c>buildings.json</c>) and
    /// its pupils, adult classes and literacy (the <c>school</c> block of <c>population.json</c>). Built by the founder as a gift to the
    /// district, so its cost is never an asset.
    /// </summary>
    public sealed class SchoolDef
    {
        /// <summary>Index of the era it can be built from (the Hamlet), or 0 without eras.</summary>
        public int UnlockEra { get; internal set; }

        /// <summary>The school plot's tile (its <see cref="PlaceDef"/> of kind <c>school_plot</c>).</summary>
        public int Tile { get; internal set; }

        /// <summary>The building and its benches, slates and books: year-0 coins, indexed when paid.</summary>
        public long BuildingCost { get; internal set; }
        public long EquipmentCost { get; internal set; }
        public int BuildHours { get; internal set; }

        /// <summary>Pupils a teacher (the Right to Education norm); the district posts another above it.</summary>
        public int SeatsPerTeacher { get; internal set; }
        public int PupilAgeMin { get; internal set; }
        public int PupilAgeMax { get; internal set; }

        /// <summary>Life-years at school to finish primary and read.</summary>
        public int PrimaryYears { get; internal set; }

        /// <summary>The founder's upkeep, paid at each close.</summary>
        public long UpkeepPerSeason { get; internal set; }
        public int ClassSize { get; internal set; }
        public long ClassCost { get; internal set; }
        public int ClassHours { get; internal set; }
        public int ClassMinAge { get; internal set; }

        /// <summary>A literate adult's outside work earns this much more (spec 018 §3.2).</summary>
        public Fixed LiteratePremium { get; internal set; }

        public long Cost { get { return BuildingCost + EquipmentCost; } }
    }
}
