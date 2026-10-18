using System.Numerics;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Scheduling;

namespace Sim.Core.Farm
{
    /// <summary>
    /// The founder's household day (spec 003 §3.6, §3.11; ADR 0010, 0032): at each daily pass, day-labour pay, eating
    /// from the store, fatigue and health for the day that ended, then the hours for the day ahead.
    /// </summary>
    internal sealed class FounderSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Labour; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass != ItemClass.Daily) return;
            FarmSystem.Accrue(sim);
            FounderState s = sim.State.Farm.Founder;
            FarmDefs farm = sim.Config.Game.Farm;
            FounderDef def = farm.Founder;
            long t = sim.State.Clock.SimTime;

            PayDayLabour(sim);
            EatFromStore(sim);

            // Fatigue from the option worked in the day that ended (ADR 0032); a rest is less restful in a hut.
            Fixed change = farm.WorkHours[s.TodayOption].FatigueChange;
            if (change < Fixed.Zero) change = change * FounderRules.RestRecovery(sim);
            s.Fatigue = Clamp(s.Fatigue + change, Fixed.Zero, def.MaxFatigue);

            // Health with the day's time-weighted nutrition (spec 003 §3.11). Sick days, warm clothing and medicine: US-052.
            long elapsed = t - s.DayStart;
            Fixed nutrition = elapsed > 0 ? s.NutritionSum.MulRatio(1, elapsed) : FounderRules.Nutrition(sim);
            s.Health = Clamp(s.Health + def.RecoveryPerDay
                - def.NutritionPenalty * Max0(def.GoodNutrition - nutrition)
                - def.FatiguePenalty * Max0(s.Fatigue - def.SafeFatigue), def.MinHealth, Fixed.One);

            // The day ahead: the chosen hours live, normal hours while away (ADR 0010).
            s.TodayOption = sim.State.Clock.IsAway ? farm.IndexOfWorkHours(WorkHoursDef.NormalId) : s.WorkHours;
            s.TodayHours = FounderRules.FixHours(sim);
            s.NutritionSum = Fixed.Zero;
            s.DayStart = t;
            FarmSystem.Refresh(sim);
        }

        /// <summary>Pays the day-labour hours accrued since the last daily pass (Dr Cash, Cr 4100).</summary>
        private static void PayDayLabour(Simulation sim)
        {
            FounderState s = sim.State.Farm.Founder;
            Fixed hours = s.DayLabourHours;
            s.DayLabourHours = Fixed.Zero;
            long amount = Money.MulRate(sim.Config.Game.Farm.Founder.DayLabourWagePerHour, hours);
            if (amount <= 0) return;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.AccrualPeriod, "ledger.dayLabour", "founder",
                new Posting(Account.Cash, amount), new Posting(Account.DayLabourEarnings, -amount));
            sim.Emit(new DayLabourPaid(sim.State.Clock.SimTime, hours, amount));
        }

        /// <summary>
        /// The household eats from the store up to the day's food allowance, valued at the trader's floor, most perishable
        /// first (highest spoilage, then good id). The value is grocery money not spent: Dr Cash, Cr the sales account as
        /// home use (ADR 0032).
        /// </summary>
        private static void EatFromStore(Simulation sim)
        {
            FounderState s = sim.State.Farm.Founder;
            long allowance = s.AllowanceSum.RoundToLong();
            s.AllowanceSum = Fixed.Zero;
            if (allowance <= 0) return;
            var goods = sim.Config.Game.Market.Goods;
            bool[] done = new bool[goods.Count];
            long crop = 0, fruit = 0, garden = 0;
            while (allowance > 0)
            {
                int g = -1;
                for (int i = 0; i < goods.Count; i++)
                {
                    if (done[i] || !goods[i].Edible || sim.State.Market.Stock[i] == 0) continue;
                    if (g < 0 || goods[i].SpoilagePerDay > goods[g].SpoilagePerDay) g = i;
                }
                if (g < 0) break;
                done[g] = true;
                Fixed floor = MarketRules.Floor(sim, g);
                if (floor <= Fixed.Zero) continue;
                long q = sim.State.Market.Stock[g];
                long value = MarketRules.Value(sim, g, q, floor);
                if (value > allowance)
                {
                    // The most grams worth no more than the allowance left.
                    q = (long)(new BigInteger(allowance) * goods[g].UnitSize * (BigInteger.One << Fixed.FractionBits) / (new BigInteger(floor.Raw) * Money.Coin));
                    while (q > 0 && MarketRules.Value(sim, g, q, floor) > allowance) q--;
                    value = MarketRules.Value(sim, g, q, floor);
                }
                if (q == 0) continue;
                Store.Take(sim, g, q);
                allowance -= value;
                if (goods[g].Sales == SalesKind.Fruit) fruit += value; else crop += value;
                if (FounderRules.IsGardenGood(sim, g)) garden += value;
            }
            long total = crop + fruit;
            if (total == 0) return;
            var postings = new System.Collections.Generic.List<Posting> { new Posting(Account.Cash, total) };
            if (crop > 0) postings.Add(new Posting(Account.CropSales, -crop));
            if (fruit > 0) postings.Add(new Posting(Account.FruitSales, -fruit));
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.AccrualPeriod, "ledger.homeUse", "founder", postings.ToArray());
            s.HomeUseSeason += total;
            s.GardenHomeUseSeason += garden;
            sim.Emit(new HomeUseEaten(sim.State.Clock.SimTime, total, garden));
        }

        /// <summary>
        /// Settlement step 5 (spec 005 §3.4): pays the new season's living costs upfront, or drops the household to
        /// subsistence when it can't (no debt, ADR 0009); returns to the previous plan at the first close that can pay.
        /// </summary>
        internal static void SettleLiving(Simulation sim)
        {
            FarmDefs farm = sim.Config.Game.Farm;
            FounderState s = sim.State.Farm.Founder;
            long t = sim.State.Clock.SimTime;
            s.HomeUseSeason = 0;
            s.GardenHomeUseSeason = 0;
            FarmSystem.Accrue(sim);

            int plan = s.AutoSubsistence ? s.PreviousPlan : s.DietPlan;
            long cost = farm.DietPlans[plan].CostPerSeason;
            if (cost == 0) return; // subsistence chosen by the player: nothing to pay
            if (sim.State.Ledger.CanAfford(cost))
            {
                s.LivingPrepaymentId = Accrual.Open(sim, Account.LivingCosts, Account.PrepaidLivingCosts, cost, t, t + sim.Clock.SecondsPerSeason, "founder:living").Id;
                sim.Emit(new ObligationPaid(t, "Living", "founder", cost));
                if (s.AutoSubsistence) FounderRules.ChangePlan(sim, plan, true);
                s.AutoSubsistence = false;
                return;
            }
            sim.Emit(new ObligationUnpaid(t, "Living", "founder", cost));
            s.LivingPrepaymentId = 0;
            if (s.AutoSubsistence) return;
            s.PreviousPlan = s.DietPlan;
            s.AutoSubsistence = true;
            FounderRules.ChangePlan(sim, farm.IndexOfDietPlan(DietPlanDef.SubsistenceId), true);
        }

        private static Fixed Clamp(Fixed v, Fixed lo, Fixed hi) { return v < lo ? lo : v > hi ? hi : v; }

        private static Fixed Max0(Fixed v) { return v > Fixed.Zero ? v : Fixed.Zero; }
    }
}
