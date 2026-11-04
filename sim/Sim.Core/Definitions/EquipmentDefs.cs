using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// The equipment ladder (spec 013 §3.2, ADR 0088): machines bought, leased a season or hired a day, each covering a number of your
    /// fields with a labour cut and a Tools factor. The combine harvester waits for Phase 3 (ADR 0089).
    /// </summary>
    public sealed class EquipmentDefs
    {
        public IReadOnlyList<MachineDef> Machines { get; set; }

        /// <summary>Index into <c>LivestockDefs.Animals</c> of the draught animal a machine's <see cref="MachineDef.Bullocks"/> are.</summary>
        public int Bullock { get; set; }

        /// <summary>A bought machine's chance of breaking each day, with its maintenance paid this season or skipped.</summary>
        public Fixed BreakdownMaintained { get; set; }
        public Fixed BreakdownSkipped { get; set; }

        /// <summary>A repair costs this share of the machine's price.</summary>
        public Fixed RepairShare { get; set; }

        /// <summary>A machine sells at this share of its book value.</summary>
        public Fixed ResaleShare { get; set; }

        /// <summary>Machines are drawn at work from and to these hours; the tractor parks on <see cref="ParkTile"/> otherwise (ADR 0088).</summary>
        public int WorkFromHour { get; set; }
        public int WorkToHour { get; set; }
        public int ParkTile { get; set; }

        public int IndexOf(string id)
        {
            for (int i = 0; i < Machines.Count; i++) if (Machines[i].Id == id) return i;
            return -1;
        }
    }

    public sealed class MachineDef
    {
        public string Id { get; set; }
        public string NameKey { get; set; }
        public int UnlockEra { get; set; }

        /// <summary>Money (× PriceIndex): the machine itself; its bullocks are bought as livestock beside it.</summary>
        public long Price { get; set; }
        public int LifeSeasons { get; set; }

        /// <summary>Draught animals that come with it and pull it (2 for the ox plough), or 0.</summary>
        public int Bullocks { get; set; }

        /// <summary>Money a season to lease it, or 0 when it can't be leased.</summary>
        public long LeasePerSeason { get; set; }
        public long HirePerDay { get; set; }

        /// <summary>A bought machine's maintenance a season, as a share of its price.</summary>
        public Fixed MaintenanceShare { get; set; }

        /// <summary>Money a season for each field it covers, paid upfront at the close.</summary>
        public long DieselPerAcreSeason { get; set; }

        /// <summary>How many of your fields it covers.</summary>
        public int Acres { get; set; }

        /// <summary>The share of a covered field's labour it saves, and its Tools factor for a crop sown under it.</summary>
        public Fixed LabourCut { get; set; }
        public Fixed Tools { get; set; }

        /// <summary>The most it takes to the mandi without hiring the trolley, kg (0 none).</summary>
        public long CartKg { get; set; }

        /// <summary>The bank lends against it (its book value at the lender's <c>equipmentLtv</c>).</summary>
        public bool Loan { get; set; }
    }

}
