using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// The irrigation ladder (spec 013 §3.1 as amended by ADR 0086 and ADR 0087): the pump on the well and a borewell each water
    /// the acres you choose, a canal channel waters its acre from the minor canal except in its dry seasons, and drip holds a
    /// pumped acre's crop at its need. Each is a building on its acre (price and life in <see cref="GameDefs.Buildings"/>).
    /// </summary>
    public sealed class IrrigationDef
    {
        public PumpedDef Pump { get; set; }
        public PumpedDef Borewell { get; set; }
        public CanalDef Canal { get; set; }
        public DripDef Drip { get; set; }
    }

    /// <summary>A pumped source: the well's pump or a borewell.</summary>
    public sealed class PumpedDef
    {
        /// <summary>Index into <see cref="GameDefs.Buildings"/>.</summary>
        public int Building { get; set; }

        /// <summary>Index of the era it comes with (0 without eras).</summary>
        public int UnlockEra { get; set; }

        /// <summary>How many acres it waters, chosen by the player.</summary>
        public int Acres { get; set; }

        public Fixed MoistureFloor { get; set; }

        /// <summary>Money a season for each acre on its list, paid at the close.</summary>
        public long PowerPerAcreSeason { get; set; }
    }

    public sealed class CanalDef
    {
        public int Building { get; set; }
        public int UnlockEra { get; set; }

        /// <summary>The irrigation department's water fee, money a season for each channelled acre, paid at the close.</summary>
        public long FeePerAcreSeason { get; set; }

        public Fixed MoistureFloor { get; set; }

        /// <summary>Per season (spring, summer, monsoon, winter): the canal is closed for repairs and its channels are dry.</summary>
        public IReadOnlyList<bool> DrySeasons { get; set; }

        /// <summary>The minor canal runs along the edge below this map row, from <see cref="FromColumn"/> to <see cref="ToColumn"/> (ADR 0087 §2).</summary>
        public int Row { get; set; }
        public int FromColumn { get; set; }
        public int ToColumn { get; set; }
    }

    public sealed class DripDef
    {
        public int Building { get; set; }
        public int UnlockEra { get; set; }

        /// <summary>The government's share of the price (PMKSY) on your first <see cref="SubsidyAcres"/> drip acres.</summary>
        public Fixed SubsidyShare { get; set; }
        public int SubsidyAcres { get; set; }

        public long PowerPerAcreSeason { get; set; }

        /// <summary>Upkeep each season, a share of the acre's drip cost.</summary>
        public Fixed UpkeepShare { get; set; }

        /// <summary>Fertiliser given through the drip: its yield factor rises by this share (fertigation).</summary>
        public Fixed FertigationBonus { get; set; }

        /// <summary>Care hours a day an acre saved (no weeding between the lines).</summary>
        public Fixed CareCutHoursPerDay { get; set; }
    }
}
