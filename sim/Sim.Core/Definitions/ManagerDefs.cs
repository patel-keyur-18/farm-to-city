using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>One style of manager (spec 028 §3.2): the price, quality and days of stock he sets.</summary>
    public sealed class ManagerStyleDef
    {
        public string Id { get; internal set; }
        public string NameKey { get; internal set; }

        /// <summary>A shop's price, or a mill's fee, as a share of the reference (1 is the reference).</summary>
        public Fixed PriceFactor { get; internal set; }

        /// <summary>1 standard, 2 good (shops only).</summary>
        public int Quality { get; internal set; }

        /// <summary>Days of stock he puts out (a stall) or of inputs he buys ahead (a maker).</summary>
        public int StockDays { get; internal set; }
    }

    /// <summary>Managers (spec 028 §3.2, <c>content/managers.json</c>), from the Township's content, or null.</summary>
    public sealed class ManagersDef
    {
        /// <summary>The share of the business's own worker's market wage that is his base (a season at skill 50, year-0): he works it himself and takes a tenth of the profit besides. The asked wage follows his skill and the price index, as any role's.</summary>
        public Fixed WageOfRole { get; internal set; }

        /// <summary>The share of the season's profit (before his share) he is paid when it is positive.</summary>
        public Fixed WageProfitShare { get; internal set; }

        /// <summary>Skill points to a star (as the UI's <c>skillStarPoints</c>): his stars are his skill ÷ this, rounded, 1 to 5.</summary>
        public int StarPoints { get; internal set; }

        /// <summary>The share of days he mis-sets, by stars 1 to 5.</summary>
        public IReadOnlyList<Fixed> MissDay { get; internal set; }

        /// <summary>The share of that day's output or stock that is made or put out on a mis-set day.</summary>
        public Fixed MissOutput { get; internal set; }

        /// <summary>A profit target is last season's profit times this; a growth target is last season's sales times that.</summary>
        public Fixed ProfitGoalRise { get; internal set; }
        public Fixed GrowthGoalRise { get; internal set; }

        /// <summary>Added to his motivation for the season after two misses in a row (spec 015 §3.4: a mood lasts a season).</summary>
        public Fixed MissedMood { get; internal set; }

        public IReadOnlyList<ManagerStyleDef> Styles { get; internal set; }

        public int StyleIndex(string id)
        {
            for (int i = 0; i < Styles.Count; i++) if (Styles[i].Id == id) return i;
            return -1;
        }

        /// <summary>His stars from his skill, 1 to 5.</summary>
        public int Stars(Fixed skill)
        {
            long s = (skill.RoundToLong() + StarPoints / 2) / StarPoints;
            return (int)(s < 1 ? 1 : s > 5 ? 5 : s);
        }

        /// <summary>The chance he mis-sets a day at these stars.</summary>
        public Fixed MissChance(int stars) { return MissDay[stars - 1]; }
    }
}
