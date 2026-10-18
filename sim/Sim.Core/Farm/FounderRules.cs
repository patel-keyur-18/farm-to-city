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
            s.DayStart = sim.State.Clock.SimTime;
            return s;
        }

        /// <summary>
        /// The first season's living costs, paid at the new game pro rata for what's left of season 0:
        /// cost × (S − t₀) ÷ S, rounded down (spec 003 §3.11). Runs after the opening balance sheet.
        /// </summary>
        internal static void PayFirstSeason(Simulation sim)
        {
            FounderState s = sim.State.Farm.Founder;
            long t = sim.State.Clock.SimTime, S = sim.Clock.SecondsPerSeason;
            long end = (t / S + 1) * S;
            long cost = (long)((System.Numerics.BigInteger)Market.MarketRules.Indexed(sim, sim.Config.Game.Farm.DietPlans[s.DietPlan].CostPerSeason) * (end - t) / S);
            if (cost == 0) return;
            if (!sim.State.Ledger.CanAfford(cost))
            {
                s.PreviousPlan = s.DietPlan;
                s.AutoSubsistence = true;
                s.DietPlan = sim.Config.Game.Farm.IndexOfDietPlan(DietPlanDef.SubsistenceId);
                return;
            }
            s.LivingPrepaymentId = Finance.Accrual.Open(sim, Finance.Account.LivingCosts, Finance.Account.PrepaidLivingCosts, cost, t, end, "founder:living").Id;
        }

        /// <summary>Hours × (1 − Fatigue) × Health of today's option (nutrition is applied as a rate).</summary>
        internal static Fixed FixHours(Simulation sim)
        {
            FounderState s = sim.State.Farm.Founder;
            if (s.SickToday) return Fixed.Zero;
            return sim.Config.Game.Farm.WorkHours[s.TodayOption].Hours * (Fixed.One - s.Fatigue) * s.Health;
        }

        /// <summary>The home's rest recovery: the best of the founder's buildings (hut 0.80).</summary>
        internal static Fixed RestRecovery(Simulation sim)
        {
            Fixed best = Fixed.Zero;
            foreach (Land.Building b in sim.State.Land.Buildings)
            {
                Fixed r = sim.Config.Game.Buildings[b.Def].RestRecovery;
                if (r > best) best = r;
            }
            return best;
        }

        /// <summary>True for a good the kitchen garden grows (for the garden's share of home use).</summary>
        internal static bool IsGardenGood(Simulation sim, int good)
        {
            foreach (GardenCropDef g in sim.Config.Game.Farm.GardenCrops)
                if (g.Good == good) return true;
            return false;
        }

        /// <summary>Switches the plan with time-weighting: accrue first, change, refresh (the caller handles payment).</summary>
        internal static void ChangePlan(Simulation sim, int plan, bool automatic)
        {
            FounderState s = sim.State.Farm.Founder;
            FarmSystem.Accrue(sim);
            int old = s.DietPlan;
            s.DietPlan = plan;
            FarmSystem.Refresh(sim);
            FarmDefs farm = sim.Config.Game.Farm;
            sim.Emit(new Events.DietPlanChanged(sim.State.Clock.SimTime, farm.DietPlans[old].Id, farm.DietPlans[plan].Id, automatic));
        }

        /// <summary>The founder's labour Why? (spec 003 §7): today's terms and where the hours go.</summary>
        public static LabourBreakdown Breakdown(Simulation sim)
        {
            FounderState s = sim.State.Farm.Founder;
            FarmDefs farm = sim.Config.Game.Farm;
            Fixed rate = LabourRate(sim);
            Fixed dayLabour = rate.MulRatio(s.DayLabourShare, 100);
            return new LabourBreakdown
            {
                Hours = farm.WorkHours[s.TodayOption].Hours, Nutrition = Nutrition(sim), Fatigue = s.Fatigue, Health = s.Health,
                Sick = s.SickToday, MandiCut = MandiCut(sim), Total = rate, OwnFields = rate - dayLabour, DayLabour = dayLabour,
                DayLabourPay = Finance.Money.MulRate(Market.MarketRules.Indexed(sim, farm.Founder.DayLabourWagePerHour), dayLabour),
            };
        }

        /// <summary>The current diet plan's nutrition.</summary>
        public static Fixed Nutrition(Simulation sim)
        {
            return sim.Config.Game.Farm.DietPlans[sim.State.Farm.Founder.DietPlan].Nutrition;
        }

        /// <summary>
        /// FounderLabourHours = Hours × Nutrition × (1 − Fatigue) × Health, hours a day (0 on a sick day), less the mandi
        /// trip's cut while one is under way (never below 0; ADR 0034).
        /// </summary>
        public static Fixed LabourRate(Simulation sim)
        {
            Fixed rate = sim.State.Farm.Founder.TodayHours * Nutrition(sim) - MandiCut(sim);
            return rate > Fixed.Zero ? rate : Fixed.Zero;
        }

        /// <summary>founderHours × 24 ÷ tripHours while a mandi trip is under way, else 0: exactly founderHours over the trip.</summary>
        public static Fixed MandiCut(Simulation sim)
        {
            if (sim.State.Market.Trip == null) return Fixed.Zero;
            MandiDef m = sim.Config.Game.Market.Mandi;
            return m.FounderHours.MulRatio(24, m.TripHours);
        }

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
            // Accrual intervals never cross a season boundary (one falls on every daily pass), so the season at the
            // interval's start is its season.
            if (plan.Id == DietPlanDef.SubsistenceId && Time.Calendar.SeasonAt(sim.State.Farm.LastAccrual, sim.Clock) == Time.Season.Winter)
                s.ColdSeconds += dt;
            if (s.EatFromStore)
                s.AllowanceSum += (plan.FoodShare * Fixed.FromInt(plan.CostPerSeason) * sim.State.Market.PriceIndex).MulRatio(dt, sim.Clock.SecondsPerSeason);
        }
    }
}

namespace Sim.Core.Farm
{
    /// <summary>The founder panel's labour Why?: Hours × Nutrition × (1 − Fatigue) × Health, and the split.</summary>
    public sealed class LabourBreakdown
    {
        public Fixed Hours { get; internal set; }
        public Fixed Nutrition { get; internal set; }
        public Fixed Fatigue { get; internal set; }
        public Fixed Health { get; internal set; }
        public bool Sick { get; internal set; }

        /// <summary>Hours a day taken by a mandi trip under way ("Mandi trip −0.5 h" over the trip).</summary>
        public Fixed MandiCut { get; internal set; }

        /// <summary>Hours a day in all, on the founder's own fields, and as day labour.</summary>
        public Fixed Total { get; internal set; }
        public Fixed OwnFields { get; internal set; }
        public Fixed DayLabour { get; internal set; }

        /// <summary>Money a day from day labour at today's rate.</summary>
        public long DayLabourPay { get; internal set; }
    }
}
