namespace Sim.Core.Definitions
{
    /// <summary>The Township's content (spec 023 §4, <c>content/township.json</c>): the paved link road now; the clinic, the gate and the cohort join it by story.</summary>
    public sealed class TownshipDef
    {
        public RoadDef Road { get; }

        public TownshipDef(RoadDef road) { Road = road; }
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
}
