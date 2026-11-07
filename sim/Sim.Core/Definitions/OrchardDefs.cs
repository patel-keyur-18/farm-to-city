using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Whole-acre orchards (spec 013 §3.3, ADR 0090): an acre of one tree at its real density, by spec 003's tree rules per tree; a short
    /// crop between the young trees at a share of its yield and labour until the first fruit.
    /// </summary>
    public sealed class OrchardDefs
    {
        public int UnlockEra { get; set; }

        /// <summary>An intercrop's share of the acre's yield and of its labour.</summary>
        public Fixed IntercropShare { get; set; }

        /// <summary>What a fruit contractor pays for a batch on the trees, as a share of the trader's price (ADR 0090 §4).</summary>
        public Fixed ContractorShare { get; set; }

        /// <summary>Per crop index: may it grow between the rows of a young orchard.</summary>
        public IReadOnlyList<bool> Intercrops { get; set; }

        /// <summary>Per tree index (<c>FarmDefs.Trees</c>): its orchard, or null if it isn't planted by the acre.</summary>
        public IReadOnlyList<OrchardTreeDef> Trees { get; set; }
    }

    public sealed class OrchardTreeDef
    {
        public int TreesPerAcre { get; set; }

        /// <summary>Hours a day an acre needs while the trees grow, and once they bear.</summary>
        public Fixed LabourYoung { get; set; }
        public Fixed LabourBearing { get; set; }

        /// <summary>The moisture band the trees do best in (spec 003's water factor, as a crop's).</summary>
        public Fixed MoistureMin { get; set; }
        public Fixed MoistureMax { get; set; }
    }
}
