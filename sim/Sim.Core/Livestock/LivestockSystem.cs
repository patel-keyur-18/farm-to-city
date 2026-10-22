using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Livestock
{
    /// <summary>
    /// Cattle (spec 012). The morning pass at <c>feedHour</c> (§3.15), system Agriculture after the crops' accrual and before the
    /// Market's truck and orders: milking and the co-op, then the lactation days.
    /// </summary>
    internal sealed class LivestockSystem : ISimSystem
    {
        public const string ShedJob = "shed.build";

        public SystemOrder Order { get { return SystemOrder.Agriculture; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Hourly && Market.MarketRules.HourOfDay(sim) == LivestockRules.Defs(sim).Livestock.FeedHour) Morning(sim);
        }

        private static void Morning(Simulation sim)
        {
            Collect(sim, sim.State.Livestock.Animals, true);
            AdvanceDays(sim);
        }

        /// <summary>The Care ratio accrues with the crops' factors (§3.9): called from <see cref="FarmSystem.Accrue"/>.</summary>
        internal static void AccrueCare(Simulation sim, long dt)
        {
            LivestockState s = sim.State.Livestock;
            if (s == null) return;
            s.CareSum += sim.State.Farm.CareRatio.MulRatio(dt, 1);
            s.CareSeconds += dt;
        }

        /// <summary>
        /// Milks <paramref name="animals"/> for the time since each was last milked (§3.5). With <paramref name="household"/> the
        /// household keeps its milk first (buffalo first, inside its food allowance); the co-op buys the rest. Dr Cash, Cr 4020.
        /// </summary>
        internal static void Collect(Simulation sim, List<Animal> animals, bool household)
        {
            LivestockState s = sim.State.Livestock;
            LivestockDefs defs = LivestockRules.Defs(sim);
            long t = sim.State.Clock.SimTime;
            if (animals.Count == 0) { ResetCare(s); return; }
            FarmSystem.Accrue(sim);
            Season season = Calendar.SeasonAt(t - 1, sim.Clock);
            Fixed care = LivestockRules.CareFactor(sim);

            var ml = new long[animals.Count];
            long since = t;
            for (int i = 0; i < animals.Count; i++)
            {
                ml[i] = LivestockRules.MilkMl(sim, animals[i], season, care, t - animals[i].Since);
                if (animals[i].Since < since) since = animals[i].Since;
            }

            // The household's milk (§3.5): buffalo first (the dearest milk), then id; never more than its food allowance.
            var home = new long[animals.Count];
            long homeValue = 0, homeMl = 0;
            if (household) homeValue = HouseholdMilk(sim, animals, ml, home, System.Math.Min(ClockDef.SecondsPerDay, t - since), out homeMl);

            long total = 0, totalMl = 0;
            for (int i = 0; i < animals.Count; i++)
            {
                Animal a = animals[i];
                long v = Money.Value(ml[i], LivestockRules.MilkPrice(sim, a.Kind), 1000);
                a.Earned += v;
                a.LastMilkMl = ml[i];
                a.Since = t;
                total += v;
                totalMl += ml[i];
            }
            if (total > 0)
                sim.State.Ledger.Post(t, sim.AccrualPeriod, "ledger.milk", "shed", new Posting(Account.Cash, total), new Posting(Account.MilkSales, -total));
            if (homeValue > 0)
            {
                sim.State.Farm.Founder.HomeUseSeason += homeValue;
                s.HomeMilkCredit += homeValue;
            }
            if (household)
            {
                s.LastMilkMl = totalMl;
                s.LastMilkAmount = total - homeValue;
                s.LastHomeMl = homeMl;
                ResetCare(s);
            }
            if (totalMl > 0) sim.Emit(new MilkCollected(t, totalMl, total - homeValue, homeMl, homeValue));
        }

        private static void ResetCare(LivestockState s)
        {
            s.CareSum = Fixed.Zero;
            s.CareSeconds = 0;
        }

        /// <summary>Fills <paramref name="home"/> with the millilitres the household keeps from each animal; returns their value.</summary>
        private static long HouseholdMilk(Simulation sim, List<Animal> animals, long[] ml, long[] home, long seconds, out long homeMl)
        {
            homeMl = 0;
            FounderState f = sim.State.Farm.Founder;
            DietPlanDef plan = sim.Config.Game.Farm.DietPlans[f.DietPlan];
            if (!f.EatFromStore || plan.FoodShare == Fixed.Zero || seconds <= 0) return 0;
            LivestockDefs defs = LivestockRules.Defs(sim);
            long allowance = (plan.FoodShare * Fixed.FromInt(plan.CostPerSeason) * sim.State.Market.PriceIndex)
                .MulRatio(seconds, sim.Clock.SecondsPerSeason).RoundToLong();
            long want = (LivestockRules.PerGameDay(sim, defs.Livestock.HouseholdLitresPerDay) * Fixed.FromInt(1000))
                .MulRatio(seconds, ClockDef.SecondsPerDay).RoundToLong();

            var order = new List<int>();
            for (int i = 0; i < animals.Count; i++) order.Add(i);
            order.Sort((x, y) =>
            {
                int c = LivestockRules.MilkPrice(sim, animals[y].Kind).CompareTo(LivestockRules.MilkPrice(sim, animals[x].Kind));
                return c != 0 ? c : animals[x].Id.CompareTo(animals[y].Id);
            });
            long value = 0;
            foreach (int i in order)
            {
                if (want <= 0 || allowance <= value) break;
                Fixed price = LivestockRules.MilkPrice(sim, animals[i].Kind);
                long fit = (Fixed.FromInt((allowance - value) * 10) / price).FloorToLong(); // ml worth the allowance left: money × 1000 ÷ (price × 100)
                long q = System.Math.Min(System.Math.Min(want, ml[i]), fit);
                while (q > 0 && value + Money.Value(q, price, 1000) > allowance) q--;
                if (q <= 0) continue;
                home[i] = q;
                value += Money.Value(q, price, 1000);
                want -= q;
                homeMl += q;
            }
            return value;
        }

        /// <summary>The lactation days (§3.6): a day older; dry after the milk days; a calf after the dry days, sold at the fair (4030).</summary>
        private static void AdvanceDays(Simulation sim)
        {
            LivestockDefs defs = LivestockRules.Defs(sim);
            long t = sim.State.Clock.SimTime;
            foreach (Animal a in sim.State.Livestock.Animals)
            {
                AnimalDef def = defs.Animals[a.Kind];
                a.StageDay++;
                if (a.Stage == AnimalStage.Milking && a.StageDay >= def.MilkDays)
                {
                    a.Stage = AnimalStage.Dry;
                    a.StageDay = 0;
                    sim.Emit(new AnimalDry(t, a.Id));
                }
                if (a.Stage == AnimalStage.Dry && a.StageDay >= def.DryDays)
                {
                    a.Stage = AnimalStage.Milking;
                    a.StageDay = 0;
                    long calf = Market.MarketRules.Indexed(sim, def.CalfPrice);
                    if (calf > 0)
                    {
                        sim.State.Ledger.Post(t, sim.Period, "ledger.calf", "animal:" + a.Id, new Posting(Account.Cash, calf), new Posting(Account.LivestockSales, -calf));
                        a.Earned += calf;
                    }
                    sim.Emit(new CalfBorn(t, a.Id, calf));
                }
            }
        }

        /// <summary>The shed is finished (§3.3): a building on the yard's shed acre.</summary>
        internal sealed class ShedBuild : IJobHandler
        {
            public string JobType { get { return ShedJob; } }
            public SystemOrder System { get { return SystemOrder.Agriculture; } }

            public void OnDue(Simulation sim, Job job)
            {
                LivestockState s = sim.State.Livestock;
                LandState land = sim.State.Land;
                LivestockDefs defs = LivestockRules.Defs(sim);
                var b = new Building { Id = land.NextBuildingId++, Def = defs.Shed, TileId = defs.Livestock.ShedTile, Cost = s.ShedCost };
                land.Buildings.Add(b);
                s.ShedReadyAt = 0;
                s.ShedCost = 0;
                sim.CompleteJob(job);
                sim.Emit(new ShedBuilt(sim.State.Clock.SimTime, b.Id));
            }
        }
    }
}
