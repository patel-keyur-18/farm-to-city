using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>Valuing, selling and branching a business (spec 028 §3.3, <c>content/exit.json</c>), from the Township's content, or null.</summary>
    public sealed class ExitDef
    {
        /// <summary>The multiple of a year's profit a business is worth: the base, moved by growth, risk and a manager, within the clamp.</summary>
        public Fixed BaseMultiple { get; internal set; }
        public int GrowthStep { get; internal set; }
        public int RiskStep { get; internal set; }
        public int ManagerBonus { get; internal set; }
        public Fixed MinMultiple { get; internal set; }
        public Fixed MaxMultiple { get; internal set; }

        /// <summary>The last two seasons' profit this much above (or below) the two before is growth (or a fall).</summary>
        public Fixed GrowthShare { get; internal set; }

        /// <summary>Seasons of profit a manager's bonus (and no risk) needs: the valuation reads this many.</summary>
        public int ProfitableSeasons { get; internal set; }

        /// <summary>A buyer pays this share of the worth, between low and high, in 3 to 6 days; a rushed sale one day at a lower share.</summary>
        public Fixed BuyerLow { get; internal set; }
        public Fixed BuyerHigh { get; internal set; }
        public int SaleDaysMin { get; internal set; }
        public int SaleDaysMax { get; internal set; }
        public int RushedDays { get; internal set; }
        public Fixed RushedShare { get; internal set; }

        /// <summary>The most branches of one kind besides the first.</summary>
        public int MaxBranches { get; internal set; }

        /// <summary>The kinds of buyer, by id (a townsperson, a rival, a regional buyer).</summary>
        public IReadOnlyList<string> Buyers { get; internal set; }
    }
}
