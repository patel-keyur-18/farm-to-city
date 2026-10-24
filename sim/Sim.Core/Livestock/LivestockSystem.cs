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
            Dung(sim);
            Collect(sim, sim.State.Livestock.Animals, true);
            AdvanceDays(sim);
            Vaccinate(sim);
            Disease(sim);
            Feed(sim, FodderRules.Cut(sim));
            LowCheck(sim);
        }

        /// <summary>
        /// The co-op's vet vaccinates every animal on the first morning of each season, if cash allows (5030); otherwise she goes
        /// unvaccinated this season and nothing is owed (§3.10). A bought animal is already vaccinated for her season.
        /// </summary>
        private static void Vaccinate(Simulation sim)
        {
            long t = sim.State.Clock.SimTime;
            if (t % sim.Clock.SecondsPerSeason >= ClockDef.SecondsPerDay) return; // only the season's first morning
            long cost = Market.MarketRules.Indexed(sim, LivestockRules.Defs(sim).Livestock.VaccineCost);
            foreach (Animal a in sim.State.Livestock.Animals)
            {
                if (a.VaccinatedPeriod == sim.Period) continue;
                if (!sim.State.Ledger.CanAfford(cost)) { sim.Emit(new VaccinationMissed(t, a.Id)); continue; }
                if (cost > 0) sim.State.Ledger.Post(t, sim.Period, "ledger.vaccine", "animal:" + a.Id, new Posting(Account.VetAndBreeding, cost), new Posting(Account.Cash, -cost));
                a.VaccinatedPeriod = sim.Period;
            }
        }

        /// <summary>
        /// A day of illness passes, then a seeded draw per animal (stream Livestock, key = animal, counter = day): below
        /// diseaseChance × (vaccinated ? 1 : unvaccinatedFactor) × (1 + sickHealthFactor × (1 − health)) she falls ill. The vet comes at
        /// once (5030) and her next day's milk is lost; with no cash for him she is ill 2 days and loses twice the health (§3.10).
        /// </summary>
        private static void Disease(Simulation sim)
        {
            LivestockDef ld = LivestockRules.Defs(sim).Livestock;
            long t = sim.State.Clock.SimTime;
            ulong day = (ulong)(t / ClockDef.SecondsPerDay);
            long vet = Market.MarketRules.Indexed(sim, ld.VetVisitCost);
            foreach (Animal a in sim.State.Livestock.Animals)
            {
                if (a.SickDays > 0) a.SickDays--;
                if (a.SickDays > 0) continue;
                Fixed chance = LivestockRules.DiseaseChance(sim, a);
                Fixed u = Fixed.FromRaw((long)(Rng.Next(sim.State.Seed, RngStream.Livestock, (ulong)a.Id, day) >> 32));
                if (u >= chance) continue;
                bool vetCame = sim.State.Ledger.CanAfford(vet);
                if (vetCame && vet > 0)
                    sim.State.Ledger.Post(t, sim.Period, "ledger.vet", "animal:" + a.Id, new Posting(Account.VetAndBreeding, vet), new Posting(Account.Cash, -vet));
                a.SickDays = vetCame ? 1 : 2;
                a.Health = a.Health - (vetCame ? ld.SickHealthLoss : ld.SickHealthLoss + ld.SickHealthLoss);
                if (a.Health < ld.MinHealth) a.Health = ld.MinHealth;
                sim.Emit(new AnimalSick(t, a.Id, vetCame ? vet : 0, vetCame));
            }
        }

        private static long Grams(Fixed kg) { return (kg * Fixed.FromInt(1000)).RoundToLong(); }

        /// <summary>Each animal's dung since her last milking goes to the compost pit at no cost; what doesn't fit is spread and lost (§3.8).</summary>
        private static void Dung(Simulation sim)
        {
            LivestockDefs defs = LivestockRules.Defs(sim);
            long t = sim.State.Clock.SimTime, grams = 0;
            foreach (Animal a in sim.State.Livestock.Animals)
                grams += (LivestockRules.PerGameDay(sim, defs.Animals[a.Kind].DungCompostKgPerDay) * Fixed.FromInt(1000)).MulRatio(t - a.Since, ClockDef.SecondsPerDay).RoundToLong();
            long q = System.Math.Min(grams, Market.Store.Fits(sim, defs.Compost));
            if (q > 0) sim.State.Market.Stock[defs.Compost] += q;
        }

        /// <summary>One animal's ration for the coming game day in grams: green, straw, feed (feed × dryFeedFactor while dry; §3.7).</summary>
        public static (long Green, long Straw, long Feed) NeedOf(Simulation sim, Animal a)
        {
            AnimalDef d = LivestockRules.Defs(sim).Animals[a.Kind];
            Fixed feed = a.Stage == AnimalStage.Dry ? d.Feed * d.DryFeedFactor : d.Feed;
            return (Grams(LivestockRules.PerGameDay(sim, d.Green)), Grams(LivestockRules.PerGameDay(sim, d.Straw)), Grams(LivestockRules.PerGameDay(sim, feed)));
        }

        /// <summary>
        /// The morning's feeding (§3.7): green from the commons' share, then the fodder fields' cut, then bought from neighbours (if on
        /// and cash allows); what the cut leaves over is sold standing. Straw from the stack and feed from the feed room at average
        /// cost (5020). Every animal gets the same share of each feed; the fed score, hunger and health follow.
        /// </summary>
        internal static void Feed(Simulation sim, long ownCut)
        {
            LivestockState s = sim.State.Livestock;
            LivestockDefs defs = LivestockRules.Defs(sim);
            LivestockDef ld = defs.Livestock;
            long t = sim.State.Clock.SimTime;
            var report = new FeedingReport();
            s.LastFeeding = report;
            if (s.Animals.Count == 0)
            {
                SellStanding(sim, report, ownCut);
                return;
            }
            foreach (Animal a in s.Animals)
            {
                var n = NeedOf(sim, a);
                report.GreenNeed += n.Green;
                report.StrawNeed += n.Straw;
                report.FeedNeed += n.Feed;
            }

            Season season = Calendar.SeasonAt(t, sim.Clock);
            report.GreenCommons = ld.CommonsShare[(int)season].MulRatio(report.GreenNeed, 1).RoundToLong();
            long left = report.GreenNeed - report.GreenCommons;
            report.GreenOwn = System.Math.Min(left, ownCut);
            left -= report.GreenOwn;
            SellStanding(sim, report, ownCut - report.GreenOwn);
            if (left > 0 && s.BuyGreenFodder)
            {
                Fixed price = ld.GreenFodderBuyPrice * sim.State.Market.PriceIndex;
                long q = price <= Fixed.Zero ? left : System.Math.Min(left, Money.QuantityFor(sim.State.Ledger.Cash, price, 1000));
                if (q > 0)
                {
                    long cost = Money.Value(q, price, 1000);
                    if (cost > 0) sim.State.Ledger.Post(t, sim.Period, "ledger.greenFodder", "shed", new Posting(Account.AnimalFeed, cost), new Posting(Account.Cash, -cost));
                    report.GreenBought = q;
                    report.GreenBoughtAmount = cost;
                }
            }
            report.StrawGiven = TakeFeed(sim, defs.Straw, report.StrawNeed);
            report.FeedGiven = TakeFeed(sim, defs.Feed, report.FeedNeed);

            long green = report.GreenCommons + report.GreenOwn + report.GreenBought;
            // 1 − Σ weight × shortfall: exactly 1 when every feed is met, whatever the weights' rounding.
            Fixed score = Fixed.One - ld.GreenWeight * (Fixed.One - Share(green, report.GreenNeed))
                - ld.StrawWeight * (Fixed.One - Share(report.StrawGiven, report.StrawNeed)) - ld.FeedWeight * (Fixed.One - Share(report.FeedGiven, report.FeedNeed));
            if (score < Fixed.Zero) score = Fixed.Zero;
            foreach (Animal a in s.Animals)
            {
                a.FedScore = score;
                if (score < ld.HungryBelow)
                {
                    a.HungryDays++;
                    if (a.HungryDays >= ld.HungerGraceDays) a.Health = a.Health - ld.HungerHealthLoss;
                }
                else
                {
                    a.HungryDays = 0;
                    a.Health = a.Health + ld.HealthRecoveryPerDay;
                }
                if (a.Health < ld.MinHealth) a.Health = ld.MinHealth;
                if (a.Health > Fixed.One) a.Health = Fixed.One;
            }
            sim.Emit(new AnimalsFed(t, report.GreenNeed, green, report.GreenBoughtAmount, report.GreenSold, report.GreenSoldAmount, report.StrawGiven,
                report.FeedGiven, score));
        }

        private static Fixed Share(long given, long need) { return need <= 0 || given >= need ? Fixed.One : Fixed.FromRatio(given, need); }

        /// <summary>Takes up to the need from a place's stock, expensed at average cost (Dr 5020, Cr 1200); returns what was given.</summary>
        private static long TakeFeed(Simulation sim, int good, long need)
        {
            long q = System.Math.Min(need, sim.State.Market.Stock[good]);
            if (q <= 0) return 0;
            long book = Market.Store.RemoveStock(sim, good, q);
            if (book > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.feedEaten", "shed", new Posting(Account.AnimalFeed, book), new Posting(Account.InputsInStore, -book));
            return q;
        }

        /// <summary>What the fodder fields cut beyond the animals' need is sold standing to neighbours (§3.7): Dr Cash, Cr 4000.</summary>
        private static void SellStanding(Simulation sim, FeedingReport report, long grams)
        {
            if (grams <= 0) return;
            Fixed price = LivestockRules.Defs(sim).Livestock.GreenFodderSellPrice * sim.State.Market.PriceIndex;
            long amount = Money.Value(grams, price, 1000);
            if (amount > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.fodderSold", "shed", new Posting(Account.Cash, amount), new Posting(Account.CropSales, -amount));
            report.GreenSold = grams;
            report.GreenSoldAmount = amount;
        }

        /// <summary>Warns once when straw or feed lasts less than lowDays (§3.7); again only after a refill above it.</summary>
        internal static void LowCheck(Simulation sim)
        {
            LivestockState s = sim.State.Livestock;
            LivestockDefs defs = LivestockRules.Defs(sim);
            if (s.Animals.Count == 0) return;
            long straw = LivestockRules.DaysLeft(sim, defs.Straw), feed = LivestockRules.DaysLeft(sim, defs.Feed), low = defs.Livestock.LowDays;
            long t = sim.State.Clock.SimTime;
            if (straw < low && !s.StrawLow) { s.StrawLow = true; sim.Emit(new FeedLow(t, sim.Config.Game.Market.Goods[defs.Straw].Id, straw)); }
            if (feed < low && !s.FeedLow) { s.FeedLow = true; sim.Emit(new FeedLow(t, sim.Config.Game.Market.Goods[defs.Feed].Id, feed)); }
        }

        /// <summary>A place was filled (bought, or straw from a harvest): its low warning resets once it lasts lowDays again.</summary>
        internal static void Refilled(Simulation sim, GoodPlace place)
        {
            LivestockState s = sim.State.Livestock;
            if (s == null) return;
            LivestockDefs defs = LivestockRules.Defs(sim);
            if (place == GoodPlace.StrawStack && LivestockRules.DaysLeft(sim, defs.Straw) >= defs.Livestock.LowDays) s.StrawLow = false;
            if (place == GoodPlace.FeedRoom && LivestockRules.DaysLeft(sim, defs.Feed) >= defs.Livestock.LowDays) s.FeedLow = false;
        }

        /// <summary>A wheat or rice harvest's straw goes to the stack once the shed stands; the rest is ploughed in (§3.8).</summary>
        internal static void AddStraw(Simulation sim, CropDef crop, long grams)
        {
            if (sim.State.Livestock == null || crop.StrawPerKg == Fixed.Zero || LivestockRules.Shed(sim) == null) return;
            int straw = LivestockRules.Defs(sim).Straw;
            long q = System.Math.Min(crop.StrawPerKg.MulRatio(grams, 1).RoundToLong(), Market.Store.Fits(sim, straw));
            if (q <= 0) return;
            sim.State.Market.Stock[straw] += q;
            Refilled(sim, GoodPlace.StrawStack);
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
                if (price <= Fixed.Zero) continue; // milk with no price isn't counted against the food allowance
                long q = System.Math.Min(System.Math.Min(want, ml[i]), Money.QuantityFor(allowance - value, price, 1000)); // ml the allowance left buys
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
