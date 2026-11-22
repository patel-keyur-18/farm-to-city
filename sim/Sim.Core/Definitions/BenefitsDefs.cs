using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Benefits and the night shift (spec 028 §3.8, <c>content/benefits.json</c>). Costs are coins a worker a season at year-0 prices; effects are added to motivation (still capped by the work rules).
    /// </summary>
    public sealed class BenefitsDef
    {
        public long MealCost { get; internal set; }
        public Fixed MealMotivation { get; internal set; }

        /// <summary>Health cover: its cost, its motivation, and the factor the chance of a sick day is multiplied by (2% to 1% is 0.5).</summary>
        public long HealthCost { get; internal set; }
        public Fixed HealthMotivation { get; internal set; }
        public Fixed HealthSick { get; internal set; }

        /// <summary>The festival bonus: paid to each worker on a festival's day; motivation that season; employer reputation.</summary>
        public long FestivalCost { get; internal set; }
        public Fixed FestivalMotivation { get; internal set; }
        public int FestivalReputation { get; internal set; }

        /// <summary>Paid leave: its cost, its motivation, the days of a season's <see cref="LeaveOf"/> worked days lost, and the fatigue it takes off once a season.</summary>
        public long LeaveCost { get; internal set; }
        public Fixed LeaveMotivation { get; internal set; }
        public int LeaveDays { get; internal set; }
        public int LeaveOf { get; internal set; }
        public Fixed LeaveFatigue { get; internal set; }

        /// <summary>Each benefit multiplies the poaching chance and the chance of leaving unhappy by this; this many kept through a close raise the employer's reputation by 1.</summary>
        public Fixed Loyalty { get; internal set; }
        public int KeptForReputation { get; internal set; }

        /// <summary>The night shift: the wage a second worker asks above the role's, the fatigue a day it adds, the hours it works (start to end of the next day), and when it is offered: this many of the last days the day shift could not keep up.</summary>
        public Fixed NightWageUp { get; internal set; }
        public Fixed NightFatigue { get; internal set; }
        public int NightStart { get; internal set; }
        public int NightEnd { get; internal set; }
        public int NightNeedDays { get; internal set; }
        public int NightLookBack { get; internal set; }
    }
}
