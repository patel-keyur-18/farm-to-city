using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Farm
{
    /// <summary>The founder's labour (spec 003 §3.6, ADR 0010, ADR 0032): a rate in hours a day, time-weighted.</summary>
    public static class FounderRules
    {
        private const long Day = ClockDef.SecondsPerDay;

        /// <summary>The new-game founder (ADR 0030): the start plan and hours, no fatigue, full health.</summary>
        internal static FounderState Start(Simulation sim)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            FounderDef f = farm.Founder;
            var s = new FounderState
            {
                DietPlan = f.StartDietPlan, PreviousPlan = f.StartDietPlan, WorkHours = f.StartWorkHours, TodayOption = f.StartWorkHours,
                DayLabourShare = f.StartDayLabourShare, EatFromStore = f.StartEatFromStore,
                Fatigue = Fixed.Zero, Health = Fixed.One,
            };
            s.TodayHours = farm.WorkHours[s.TodayOption].Hours;
            return s;
        }

        /// <summary>The current diet plan's nutrition.</summary>
        public static Fixed Nutrition(Simulation sim)
        {
            return sim.Config.Game.Farm.DietPlans[sim.State.Farm.Founder.DietPlan].Nutrition;
        }

        /// <summary>FounderLabourHours = Hours × Nutrition × (1 − Fatigue) × Health, hours a day (0 on a sick day).</summary>
        public static Fixed LabourRate(Simulation sim) { return sim.State.Farm.Founder.TodayHours * Nutrition(sim); }

        /// <summary>The founder's hours a day on their own fields: rate × (1 − dayLabourShare). Spec 006 adds hired hours.</summary>
        public static Fixed FieldHours(Simulation sim)
        {
            return LabourRate(sim).MulRatio(100 - sim.State.Farm.Founder.DayLabourShare, 100);
        }

        /// <summary>Day-labour hours, the nutrition sum and the food allowance for dt seconds at the current rates.</summary>
        internal static void Accrue(Simulation sim, long dt)
        {
            FounderState s = sim.State.Farm.Founder;
            DietPlanDef plan = sim.Config.Game.Farm.DietPlans[s.DietPlan];
            s.DayLabourHours += LabourRate(sim).MulRatio(s.DayLabourShare * dt, 100 * Day);
            s.NutritionSum += plan.Nutrition.MulRatio(dt, 1);
            if (s.EatFromStore)
                s.AllowanceSum += (plan.FoodShare * Fixed.FromInt(plan.CostPerSeason)).MulRatio(dt, sim.Clock.SecondsPerSeason);
        }
    }
}
