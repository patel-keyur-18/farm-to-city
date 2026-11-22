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

        // Investors, partners and buy-outs (spec 028 §3.4).

        /// <summary>The shares of its worth an investor may offer cash for, ascending; the most outside owners (investor and partner) hold together.</summary>
        public IReadOnlyList<Fixed> InvestorShares { get; internal set; }
        public Fixed MaxOutside { get; internal set; }

        /// <summary>Seasons an investor's share must stand before you may buy it back; the worth (in coins) a business needs before an investor looks at it.</summary>
        public int BuyBackSeasons { get; internal set; }
        public long InvestorValueMin { get; internal set; }

        /// <summary>A partner puts in this share of the worth and takes this share of the profit.</summary>
        public Fixed PartnerStake { get; internal set; }
        public Fixed PartnerProfit { get; internal set; }

        /// <summary>A buy-out offer is this share of the worth, between low and high (never under what it holds); open this many hours, more when the business is a big part of your net worth; seasons between offers for a business.</summary>
        public Fixed BuyOutLow { get; internal set; }
        public Fixed BuyOutHigh { get; internal set; }
        public int BuyOutHours { get; internal set; }
        public int BuyOutBigHours { get; internal set; }
        public Fixed BuyOutBigShare { get; internal set; }
        public int BuyOutGapSeasons { get; internal set; }

        /// <summary>Hours an investor's or a partner's offer stays open; the chance, at a close, that a business is offered something.</summary>
        public int OfferHours { get; internal set; }
        public Fixed OfferChance { get; internal set; }

        /// <summary>The investors' names, by id (the strings give each one).</summary>
        public IReadOnlyList<string> Investors { get; internal set; }
    }
}
