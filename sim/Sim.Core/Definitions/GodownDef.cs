using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Spec 013 §3.4 as amended by ADR 0087: your own godown in the mandi town (off the map), space to rent in the warehouse there,
    /// and letting your godown to a trader. Goods in either spoil at <see cref="SpoilageFactor"/> of their rate.
    /// </summary>
    public sealed class GodownDef
    {
        public const string BuildingId = "godown";

        /// <summary>Index of the godown in <see cref="GameDefs.Buildings"/> (its price and life).</summary>
        public int Building { get; set; }

        /// <summary>Index of the era it comes with in the onboarding eras (the Hamlet), or 0 without eras.</summary>
        public int UnlockEra { get; set; }

        public long CapacityKg { get; set; }
        public Fixed SpoilageFactor { get; set; }

        /// <summary>Per good: kept in the godown and the warehouse (the dry goods).</summary>
        public IReadOnlyList<bool> Holds { get; set; }

        /// <summary>Money a kg a season (year-0 coins, indexed when charged).</summary>
        public long WarehouseRentPerKgSeason { get; set; }
        public int MaxRentTonnes { get; set; }

        /// <summary>The chance a trader takes your godown at a close, and the rent he pays (money a kg a season).</summary>
        public Fixed TenantChance { get; set; }
        public long LetRentPerKgSeason { get; set; }
    }
}
