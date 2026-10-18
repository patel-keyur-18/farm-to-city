using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class FounderTests
    {
        private const long S = FarmTest.S;

        private static FounderState F(Simulation sim) { return sim.State.Farm.Founder; }

        /// <summary>The default start without eating from the store, so cash moves only as each test says.</summary>
        private static GameDefs NoEating(System.Func<string, string, string> edit = null)
        {
            return RichGame((f, j) =>
            {
                if (f == "founder.json") j = j.Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false");
                return edit == null ? j : edit(f, j);
            });
        }

        private static long DayLabourPaid(Simulation sim)
        {
            return sim.State.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.dayLabour")
                .SelectMany(e => e.Postings).Where(p => p.Account == Account.Cash).Sum(p => p.Amount);
        }

        // Spec 003 AC-8.
        [Test]
        public void LabourHours_AndDayLabour()
        {
            Simulation sim = NewFarm(game: NoEating());
            FarmTest.AssertNear(FounderRules.LabourRate(sim), "7.2", 8);
            Assert.That(sim.Execute(new SetLabourSplit(0, 25)).Accepted, Is.True);
            LabourBreakdown b = FounderRules.Breakdown(sim);
            FarmTest.AssertNear(b.OwnFields, "5.4", 8);
            FarmTest.AssertNear(b.DayLabour, "1.8", 8);
            Assert.That(b.DayLabourPay, Is.EqualTo(2_025_00));
            Assert.That(b.Hours, Is.EqualTo(Fixed.FromInt(8)));

            long cash = sim.State.Ledger.Cash;
            sim.DrainEvents();
            sim.AdvanceTo(Day);
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(2_025_00));
            Assert.That(sim.State.Ledger.Balance(Account.DayLabourEarnings), Is.EqualTo(-2_025_00));
            Assert.That(sim.DrainEvents().OfType<DayLabourPaid>().Single().Amount, Is.EqualTo(2_025_00));

            ulong h = sim.State.ComputeHash();
            Assert.That(sim.Execute(new SetLabourSplit(Day, 30)).Reason, Is.EqualTo(RejectReason.InvalidShare));
            Assert.That(sim.Execute(new SetLabourSplit(Day, 125)).Reason, Is.EqualTo(RejectReason.InvalidShare));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(h));
        }

        // Spec 003 AC-18.
        [Test]
        public void Fatigue_Health_Dynamics()
        {
            FounderDef d = NewFarm().Config.Game.Farm.Founder;
            Fixed recovery = Fixed.Parse("0.8");

            // Long hours: fatigue +0.08 a day from the day after the first long day, capped at 0.5; health follows §3.11.
            Simulation sim = NewFarm(game: NoEating());
            Assert.That(sim.Execute(new SetWorkHours(0, "long")).Accepted, Is.True);
            Fixed fatigue = Fixed.Zero, health = Fixed.One;
            for (int day = 1; day <= 12; day++)
            {
                sim.AdvanceTo(day * Day);
                if (day >= 2) fatigue = Min(fatigue + Fixed.Parse("0.08"), d.MaxFatigue); // day 1 worked normal hours
                health = Clamp(health + d.RecoveryPerDay - d.NutritionPenalty * Pos(d.GoodNutrition - Fixed.Parse("0.9"))
                    - d.FatiguePenalty * Pos(fatigue - d.SafeFatigue), d.MinHealth, Fixed.One);
                Assert.That(F(sim).Fatigue, Is.EqualTo(fatigue), "fatigue, day " + day);
                FarmTest.AssertNear(F(sim).Health, health.ToString(), 64);
                Assert.That(F(sim).Fatigue, Is.InRange(Fixed.Zero, d.MaxFatigue));
                Assert.That(F(sim).Health, Is.InRange(d.MinHealth, Fixed.One));
            }
            Assert.That(F(sim).Fatigue, Is.EqualTo(d.MaxFatigue));
            Assert.That(F(sim).Health, Is.LessThan(Fixed.One));
            FarmTest.AssertNear(F(sim).TodayHours, (Fixed.FromInt(10) * (Fixed.One - F(sim).Fatigue) * F(sim).Health).ToString(), 4);

            // Rest recovers 0.15 × 0.8 (a hut) a day.
            Assert.That(sim.Execute(new SetWorkHours(12 * Day, "rest")).Accepted, Is.True);
            sim.AdvanceTo(13 * Day); // the long day just ended still adds fatigue (capped)
            sim.AdvanceTo(14 * Day);
            Assert.That(F(sim).Fatigue, Is.EqualTo(d.MaxFatigue - Fixed.Parse("0.15") * recovery)); // 0.50 − 0.12
            Assert.That(sim.Execute(new SetWorkHours(14 * Day, "sprint")).Reason, Is.EqualTo(RejectReason.UnknownOption));

            // Basic food and normal hours: +0.02 − 0.01 a day, so health stays at 1. Subsistence: −0.01 a day.
            sim = NewFarm(game: NoEating());
            sim.AdvanceTo(3 * Day);
            Assert.That(F(sim).Health, Is.EqualTo(Fixed.One));
            Assert.That(sim.Execute(new SetDietPlan(3 * Day, "subsistence")).Accepted, Is.True);
            sim.AdvanceTo(4 * Day - 1);
            sim.AdvanceTo(5 * Day);
            Fixed expected = Fixed.One + (d.RecoveryPerDay - d.NutritionPenalty * Pos(d.GoodNutrition - Fixed.Parse("0.8"))) * Fixed.FromInt(2);
            FarmTest.AssertNear(F(sim).Health, expected.ToString(), 16);
            FarmTest.AssertNear(F(sim).Health, "0.98", 64);
        }

        // Spec 003 AC-20.
        [Test]
        public void Away_NormalHours()
        {
            Simulation sim = NewFarm(game: NoEating());
            Assert.That(sim.Execute(new SetWorkHours(0, "long")).Accepted, Is.True);
            sim.AdvanceTo(Day - 60);
            Assert.That(sim.Execute(new AdvanceTo(Day + 60, AdvanceMode.Away)).Accepted, Is.True);
            Assert.That(sim.Config.Game.Farm.WorkHours[F(sim).TodayOption].Id, Is.EqualTo("normal"));
            Assert.That(F(sim).TodayHours, Is.EqualTo(Fixed.FromInt(8)));
            sim.AdvanceTo(2 * Day);
            Assert.That(sim.Config.Game.Farm.WorkHours[F(sim).TodayOption].Id, Is.EqualTo("long"));
            Assert.That(F(sim).TodayHours, Is.EqualTo(Fixed.FromInt(10)), "the away day was normal: no fatigue yet");
        }

        // Spec 003 AC-21.
        [Test]
        public void LivingCosts_UpfrontAndChange()
        {
            Simulation sim = NewFarm(localSeconds: 10 * Hour, game: NoEating());
            Prepayment p = sim.State.Finance.FindPrepayment(F(sim).LivingPrepaymentId);
            Assert.That(p.Total, Is.EqualTo(16_125_00)); // 18,000 × (345,600 − 36,000) ÷ 345,600
            Assert.That(sim.State.Ledger.Balance(Account.PrepaidLivingCosts), Is.EqualTo(16_125_00));

            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new SetDietPlan(2 * Day, "balanced")).Accepted, Is.True);
            // Basic expensed 16,125 × 136,800 ÷ 309,600 = 7,125; refund 9,000; balanced for half a season: 12,000.
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(9_000_00 - 12_000_00));
            Assert.That(sim.Config.Game.Farm.DietPlans[F(sim).DietPlan].Id, Is.EqualTo("balanced"));
            sim.AdvanceTo(S);
            SeasonReport r = sim.State.Finance.Reports.Single();
            Assert.That(r.ByAccount.Single(kv => kv.Key == Account.LivingCosts).Value, Is.EqualTo(-(7_125_00 + 12_000_00)));

            // Can't afford the new plan after the refund: rejected, nothing changes.
            Simulation poor = NewFarm(game: TestContent.Game((f, j) => f == "founder.json" ? j.Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false") : j));
            ulong h = poor.State.ComputeHash();
            Assert.That(poor.Execute(new SetDietPlan(0, "rich")).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(poor.State.ComputeHash(), Is.EqualTo(h));
            Assert.That(poor.Execute(new SetDietPlan(0, "feast")).Reason, Is.EqualTo(RejectReason.UnknownPlan));
        }

        // Spec 003 AC-22 and spec 005 AC-28.
        [Test]
        public void Subsistence_NeverStuck()
        {
            Simulation sim = NewFarm(game: TestContent.Game((f, j) =>
                f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 18000")
                : f == "founder.json" ? j.Replace("\"startEatFromStore\": true", "\"startEatFromStore\": false") : j));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(0));
            sim.DrainEvents();
            sim.AdvanceTo(S);
            Assert.That(sim.DrainEvents().OfType<ObligationUnpaid>().Single().Amount, Is.EqualTo(18_000_00));
            Assert.That(sim.Config.Game.Farm.DietPlans[F(sim).DietPlan].Id, Is.EqualTo("subsistence"));
            FarmTest.AssertNear(FounderRules.Nutrition(sim), "0.8", 4);
            Assert.That(sim.State.Ledger.TotalOf(AccountGroup.Liabilities), Is.EqualTo(0), "no debt is ever created");

            // Full-time day labour pays 8 × 0.8 × 1,125 = 7,200 a day, so the next close can pay: back to basic.
            Assert.That(sim.Execute(new SetLabourSplit(S, 100)).Accepted, Is.True);
            sim.AdvanceTo(2 * S);
            Assert.That(sim.Config.Game.Farm.DietPlans[F(sim).DietPlan].Id, Is.EqualTo("basic"));
            Assert.That(sim.State.Finance.FindPrepayment(F(sim).LivingPrepaymentId).Total, Is.EqualTo(18_000_00));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // Spec 003 AC-23 (daily eating, ADR 0032).
        [Test]
        public void EatFromStore_Daily()
        {
            Simulation sim = NewFarm(game: RichGame());
            int radish = FarmTest.Good(sim, "radish"), wheat = FarmTest.Good(sim, "wheat"), lentils = FarmTest.Good(sim, "lentils");
            sim.State.Market.Stock[radish] = 50_000;
            sim.State.Market.Stock[wheat] = 200_000;
            sim.State.Market.Stock[lentils] = 0;
            long cash = sim.State.Ledger.Cash, prepaid = sim.State.Ledger.Balance(Account.PrepaidLivingCosts);
            sim.AdvanceTo(Day);

            // Allowance 0.6 × 18,000 ÷ 4 = 2,700: radish first (50 kg × 5.50 = 275), then 108.988 kg of wheat at 22.25.
            Assert.That(sim.State.Market.InStore(radish), Is.EqualTo(0));
            Assert.That(sim.State.Market.InStore(wheat), Is.EqualTo(200_000 - 108_988));
            long eaten = 275_00 + Money.Value(108_988, MarketRules.Floor(sim, wheat), 1000);
            Assert.That(eaten, Is.EqualTo(2_699_98));
            JournalEntry e = sim.State.Ledger.Entries.Single(x => x.DescriptionKey == "ledger.homeUse");
            Assert.That(e.Postings.Single(p => p.Account == Account.Cash).Amount, Is.EqualTo(eaten));
            Assert.That(e.Postings.Single(p => p.Account == Account.CropSales).Amount, Is.EqualTo(-eaten));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(eaten));
            Assert.That(sim.State.Finance.FindPrepayment(F(sim).LivingPrepaymentId).Total, Is.EqualTo(18_000_00), "the prepayment is unchanged");
            Assert.That(sim.State.Ledger.Balance(Account.PrepaidLivingCosts), Is.EqualTo(prepaid - 4_500_00)); // a day expensed
            Assert.That(F(sim).HomeUseSeason, Is.EqualTo(eaten));

            // On subsistence nothing is eaten.
            Assert.That(sim.Execute(new SetDietPlan(Day, "subsistence")).Accepted, Is.True);
            long left = sim.State.Market.InStore(wheat);
            sim.AdvanceTo(2 * Day);
            Assert.That(sim.State.Market.InStore(wheat), Is.EqualTo(left));
        }

        // Spec 003 AC-36.
        [Test]
        public void Start_State()
        {
            Simulation sim = NewFarm();
            FarmDefs farm = sim.Config.Game.Farm;
            FounderState f = F(sim);
            Assert.That(sim.State.Ledger.Cash + sim.State.Ledger.Balance(Account.PrepaidLivingCosts), Is.EqualTo(20_000_00));
            Assert.That(farm.DietPlans[f.DietPlan].Id, Is.EqualTo("basic"));
            Assert.That(farm.WorkHours[f.WorkHours].Id, Is.EqualTo("normal"));
            Assert.That(f.EatFromStore, Is.True);
            Assert.That(f.Health, Is.EqualTo(Fixed.One));
            Assert.That(f.Fatigue, Is.EqualTo(Fixed.Zero));
            FarmTest.AssertNear(FounderRules.Nutrition(sim), "0.9", 4);
            Assert.That(sim.State.Farm.SavedSeedAgeOf(farm.IndexOfCrop("wheat")), Is.EqualTo(Fixed.FromInt(2)));
            Assert.That(sim.State.Farm.SavedSeedAgeOf(farm.IndexOfCrop("lentils")), Is.EqualTo(Fixed.FromInt(2)));

            // The first daily passes eat the inherited wheat, then the lentils.
            int wheat = FarmTest.Good(sim, "wheat"), lentils = FarmTest.Good(sim, "lentils");
            sim.AdvanceTo(Day);
            Assert.That(sim.State.Market.InStore(wheat), Is.EqualTo(0));
            Assert.That(sim.State.Market.InStore(lentils), Is.InRange(1, 75_000 - 1));
            sim.AdvanceTo(3 * Day);
            Assert.That(sim.State.Market.InStore(lentils), Is.EqualTo(0));
            Assert.That(sim.State.Market.InStore(FarmTest.Good(sim, "seed_potato")), Is.EqualTo(600_000), "seed is never eaten");
        }

        // Spec 003 AC-37 (ADR 0032).
        [Test]
        public void PlanChanges_TimeWeighted()
        {
            Simulation baseline = NewFarm(game: NoEating());
            Assert.That(baseline.Execute(new SetLabourSplit(0, 25)).Accepted, Is.True);
            baseline.AdvanceTo(Day);
            long basePay = DayLabourPaid(baseline);

            // Rich food and full day labour from one minute before the pass: only that minute counts.
            Simulation gamed = NewFarm(game: NoEating());
            Assert.That(gamed.Execute(new SetLabourSplit(0, 25)).Accepted, Is.True);
            Assert.That(gamed.Execute(new SetDietPlan(Day - 60, "rich")).Accepted, Is.True);
            Assert.That(gamed.Execute(new SetLabourSplit(Day - 60, 100)).Accepted, Is.True);
            gamed.AdvanceTo(Day);
            long gamedPay = DayLabourPaid(gamed);
            // The last minute: 8 × 1.05 × 100% instead of 8 × 0.90 × 25% hours a day.
            Fixed extraHours = (Fixed.FromInt(8) * Fixed.Parse("1.05") - (Fixed.FromInt(8) * Fixed.Parse("0.9")).MulRatio(25, 100)).MulRatio(60, Day);
            long extra = Money.MulRate(1_125_00, extraHours);
            Assert.That(gamedPay - basePay, Is.InRange(extra - 1, extra + 1));
            Assert.That(gamedPay - basePay, Is.LessThan(10_00), "about 5 coins: a minute's worth, not a day's (6,075 more)");

            // Health uses the day's mean nutrition: (0.9 × 86,340 + 1.05 × 60) ÷ 86,400, not 1.05.
            Fixed mean = (Fixed.Parse("0.9").MulRatio(Day - 60, 1) + Fixed.Parse("1.05").MulRatio(60, 1)).MulRatio(1, Day);
            FounderDef d = gamed.Config.Game.Farm.Founder;
            Fixed expected = Min(Fixed.One, Fixed.One + d.RecoveryPerDay - d.NutritionPenalty * Pos(d.GoodNutrition - mean));
            Assert.That(gamed.State.Farm.Founder.Health, Is.EqualTo(expected));
        }

        private static Fixed Min(Fixed a, Fixed b) { return a < b ? a : b; }
        private static Fixed Pos(Fixed v) { return v > Fixed.Zero ? v : Fixed.Zero; }
        private static Fixed Clamp(Fixed v, Fixed lo, Fixed hi) { return v < lo ? lo : v > hi ? hi : v; }
    }
}
