using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Helpers for the spec 012 tests.</summary>
    public static class LivestockKit
    {
        public static LivestockState L(Simulation sim) { return sim.State.Livestock; }

        /// <summary>Marks the town a Hamlet, the era the shed unlocks in.</summary>
        public static void Hamlet(Simulation sim) { sim.State.Era.Current = sim.Config.Game.Livestock.UnlockEra; }

        /// <summary>The next 6:00 pass after now (spec 012 §3.15).</summary>
        public static long NextMorning(Simulation sim)
        {
            long t = sim.State.Clock.SimTime, six = 6 * Hour;
            long day = t / Day * Day;
            return t < day + six ? day + six : day + Day + six;
        }

        public static void ToMorning(Simulation sim) { sim.AdvanceTo(NextMorning(sim)); }

        /// <summary>A Hamlet with a finished shed, at the next 6:00.</summary>
        public static Simulation WithShed(GameDefs game = null)
        {
            Simulation sim = Start(game);
            Hamlet(sim);
            Ok(sim, new BuildShed(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            ToMorning(sim);
            return sim;
        }

        public static Animal Buy(Simulation sim, string kind = "buffalo")
        {
            Ok(sim, new BuyAnimal(sim.State.Clock.SimTime, kind));
            return L(sim).Animals.Last();
        }
    }

    public class LivestockTests
    {
        private static long Money(long coins) { return coins * Coin; }

        [Test]
        public void Shed_BuildAndLimits()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildShed(t)).Reason, Is.EqualTo(RejectReason.EraLocked));
            LivestockKit.Hamlet(sim);
            long buildings = sim.State.Ledger.Balance(Account.Buildings), cash = sim.State.Ledger.Cash;
            Ok(sim, new BuildShed(t));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings) - buildings, Is.EqualTo(Money(40000)));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money(40000)));
            Assert.That(sim.Execute(new BuildShed(t)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
            Assert.That(sim.Execute(new BuyAnimal(t, "buffalo")).Reason, Is.EqualTo(RejectReason.NoShed));
            sim.AdvanceTo(t + 24 * Hour - 1);
            Assert.That(LivestockRules.Shed(sim), Is.Null);
            sim.AdvanceTo(t + 24 * Hour);
            Assert.That(LivestockRules.Shed(sim), Is.Not.Null);
            Assert.That(LivestockRules.Shed(sim).TileId, Is.EqualTo(103));
            Assert.That(sim.PendingEvents.OfType<ShedBuilt>().Count(), Is.EqualTo(1));
            Assert.That(sim.Execute(new BuildShed(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt));
        }

        [Test]
        public void BuyAnimal()
        {
            Simulation sim = LivestockKit.WithShed();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuyAnimal(t, "goat")).Reason, Is.EqualTo(RejectReason.UnknownAnimal));
            long cash = sim.State.Ledger.Cash;
            Animal a = LivestockKit.Buy(sim);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money(100000)));
            Assert.That(sim.State.Ledger.Balance(Account.Livestock), Is.EqualTo(Money(100000)));
            Assert.That(a.Stage, Is.EqualTo(AnimalStage.Milking));
            Assert.That(a.StageDay, Is.EqualTo(0));
            Assert.That(a.Health, Is.EqualTo(Fixed.One));
            Assert.That(a.VaccinatedPeriod, Is.EqualTo(sim.Period));
            Assert.That(a.Name, Is.Not.Empty);
            LivestockKit.Buy(sim, "cow");
            LivestockKit.Buy(sim);
            LivestockKit.Buy(sim, "cow");
            Assert.That(LivestockKit.L(sim).Animals.Select(x => x.Name).Distinct().Count(), Is.EqualTo(4), "four different names");
            Assert.That(sim.Execute(new BuyAnimal(t, "buffalo")).Reason, Is.EqualTo(RejectReason.ShedFull));
            Assert.That(Reporting.NetWorth(sim).LivestockAtBook, Is.EqualTo(Money(340000)));
        }

        [Test]
        public void Milk_CoopAndHome()
        {
            Simulation sim = LivestockKit.WithShed();
            Animal a = LivestockKit.Buy(sim);
            long milkSales = sim.State.Ledger.Balance(Account.MilkSales), cash = sim.State.Ledger.Cash;
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);

            // 8 L × 22.8 × curve(0) 1.10 = 200.64 L; the household keeps 1.5 × 22.8 = 34.2 L; all at (12 + 6.5 × 7) = 57.50 a litre.
            MilkCollected m = sim.DrainEvents().OfType<MilkCollected>().Single();
            Assert.That(m.Millilitres, Is.EqualTo(200_640));
            Assert.That(m.HomeMillilitres, Is.EqualTo(34_200));
            Assert.That(m.HomeAmount, Is.EqualTo(196_650));
            Assert.That(m.Amount, Is.EqualTo(Finance.Money.Value(200_640, Fixed.Parse("57.5"), 1000) - 196_650));
            Assert.That(milkSales - sim.State.Ledger.Balance(Account.MilkSales), Is.EqualTo(m.Amount + m.HomeAmount));
            Assert.That(a.LastMilkMl, Is.EqualTo(200_640));
            Assert.That(sim.State.Farm.Founder.HomeUseSeason, Is.GreaterThanOrEqualTo(196_650));
            Assert.That(LivestockKit.L(sim).HomeMilkCredit, Is.EqualTo(196_650), "part of the day's food allowance until midnight");
            sim.AdvanceTo(sim.State.Clock.SimTime + 18 * Hour);
            Assert.That(LivestockKit.L(sim).HomeMilkCredit, Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Cash, Is.GreaterThan(cash));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        [Test]
        public void Milk_NoHouseholdShare_WhenNotEatingFromStore()
        {
            Simulation sim = LivestockKit.WithShed();
            Ok(sim, new SetEatFromStore(sim.State.Clock.SimTime, false));
            LivestockKit.Buy(sim);
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            MilkCollected m = sim.DrainEvents().OfType<MilkCollected>().Single();
            Assert.That(m.HomeMillilitres, Is.EqualTo(0));
            Assert.That(m.Amount, Is.EqualTo(Finance.Money.Value(200_640, Fixed.Parse("57.5"), 1000)));
        }

        [Test]
        public void Milk_HalfDay_ProRata()
        {
            Simulation sim = LivestockKit.WithShed();
            Ok(sim, new SetEatFromStore(sim.State.Clock.SimTime, false));
            sim.AdvanceTo(sim.State.Clock.SimTime + 12 * Hour);
            LivestockKit.Buy(sim);
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            Assert.That(sim.DrainEvents().OfType<MilkCollected>().Single().Millilitres, Is.EqualTo(100_320));
        }

        [Test]
        public void Lactation_Cycle()
        {
            Simulation sim = LivestockKit.WithShed();
            Animal a = LivestockKit.Buy(sim);
            for (int d = 0; d < 12; d++) LivestockKit.ToMorning(sim);
            Assert.That(a.Stage, Is.EqualTo(AnimalStage.Milking));
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            Assert.That(a.Stage, Is.EqualTo(AnimalStage.Dry));
            Assert.That(sim.PendingEvents.OfType<AnimalDry>().Count(), Is.EqualTo(1));
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            Assert.That(sim.PendingEvents.OfType<MilkCollected>(), Is.Empty, "a dry buffalo gives no milk");
            for (int d = 0; d < 3; d++) LivestockKit.ToMorning(sim);
            long sales = sim.State.Ledger.Balance(Account.LivestockSales);
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            CalfBorn calf = sim.PendingEvents.OfType<CalfBorn>().Single();
            Assert.That(calf.Amount, Is.EqualTo(Money(5000)));
            Assert.That(sales - sim.State.Ledger.Balance(Account.LivestockSales), Is.EqualTo(Money(5000)));
            Assert.That(a.Stage, Is.EqualTo(AnimalStage.Milking));
            Assert.That(a.StageDay, Is.EqualTo(0));
        }

        [Test]
        public void Cow_LessMilkInSummer()
        {
            Simulation sim = LivestockKit.WithShed();
            Animal cow = LivestockKit.Buy(sim, "cow");
            Fixed spring = LivestockRules.BaseMilkMl(sim, cow, Season.Spring), summer = LivestockRules.BaseMilkMl(sim, cow, Season.Summer);
            Assert.That(summer, Is.EqualTo(spring * Fixed.Parse("0.85")));
            Assert.That(LivestockRules.MilkPrice(sim, cow.Kind), Is.EqualTo(Fixed.FromInt(38)));
        }

        [Test]
        public void Labour_JoinsCareDemand()
        {
            Simulation sim = LivestockKit.WithShed();
            Fixed before = Farm.FarmSystem.LabourDemand(sim);
            LivestockKit.Buy(sim);
            LivestockKit.Buy(sim);
            Assert.That(Farm.FarmSystem.LabourDemand(sim) - before, Is.EqualTo(Fixed.FromInt(3)));
        }

        [Test]
        public void SellAnimal_AtTheFair()
        {
            Simulation sim = LivestockKit.WithShed();
            Animal a = LivestockKit.Buy(sim);
            sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            long cash = sim.State.Ledger.Cash, loss = sim.State.Ledger.Balance(Account.LossOnAssetSales);
            sim.DrainEvents();
            Ok(sim, new SellAnimal(sim.State.Clock.SimTime, a.Id));
            Assert.That(sim.PendingEvents.OfType<MilkCollected>().Single().Millilitres, Is.EqualTo(50_160), "her milk since 6:00 goes to the co-op first");
            Assert.That(sim.PendingEvents.OfType<AnimalSold>().Single().Amount, Is.EqualTo(Money(80000)));
            Assert.That(sim.State.Ledger.Balance(Account.LossOnAssetSales) - loss, Is.EqualTo(Money(20000)));
            Assert.That(sim.State.Ledger.Balance(Account.Livestock), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Cash - cash, Is.GreaterThan(Money(80000)));
            Assert.That(LivestockKit.L(sim).Animals, Is.Empty);
            Assert.That(sim.Execute(new SellAnimal(sim.State.Clock.SimTime, a.Id)).Reason, Is.EqualTo(RejectReason.UnknownAnimal));
        }

        [Test]
        public void Books()
        {
            Simulation sim = LivestockKit.WithShed();
            LivestockKit.Buy(sim);
            LivestockKit.ToMorning(sim);
            var lines = Reporting.LinesFrom(new[] { new System.Collections.Generic.KeyValuePair<Account, long>(Account.MilkSales, 100),
                new System.Collections.Generic.KeyValuePair<Account, long>(Account.AnimalFeed, -40), new System.Collections.Generic.KeyValuePair<Account, long>(Account.LivestockSales, 10),
                new System.Collections.Generic.KeyValuePair<Account, long>(Account.VetAndBreeding, -5) });
            Assert.That(lines.Single(l => l.Key == "farmProfit").Amount, Is.EqualTo(65));
        }
        // ---- US-199: feeding (spec 012 §3.7, §3.8) ----

        private static void Fill(Simulation sim, string good, long grams) { Ok(sim, new Buy(sim.State.Clock.SimTime, good, grams)); }

        [Test]
        public void Feeding_Order()
        {
            Simulation sim = LivestockKit.WithShed(Game(NoSpoilage));
            Fill(sim, "straw", 500_000);
            Fill(sim, "cattle_feed", 500_000);
            LivestockKit.Buy(sim);
            long feedAccount = sim.State.Ledger.Balance(Account.AnimalFeed);
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            // Need a game day: green 27 × 22.8 = 615.6 kg (Spring: the commons give 10%), straw 136.8 kg, feed 114 kg.
            AnimalsFed f = sim.PendingEvents.OfType<AnimalsFed>().Single();
            FeedingReport r = LivestockKit.L(sim).LastFeeding;
            Assert.That(r.GreenNeed, Is.EqualTo(615_600));
            Assert.That(r.GreenCommons, Is.EqualTo(61_560));
            Assert.That(r.GreenBought, Is.EqualTo(554_040));
            Assert.That(r.GreenBoughtAmount, Is.EqualTo(Finance.Money.Value(554_040, Fixed.FromInt(3), 1000)));
            Assert.That(r.StrawGiven, Is.EqualTo(136_800));
            Assert.That(r.FeedGiven, Is.EqualTo(114_000));
            Assert.That(f.FedScore, Is.EqualTo(Fixed.One));
            long strawCost = Finance.Money.Value(136_800, Fixed.FromInt(6) * Fixed.Parse("0.85"), 1000); // Spring's straw price
            Assert.That(sim.State.Ledger.Balance(Account.AnimalFeed) - feedAccount, Is.EqualTo(r.GreenBoughtAmount + strawCost + Finance.Money.Value(114_000, Fixed.FromInt(28), 1000)).Within(2));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            // Nothing bought, no straw left: only the feed's weight counts.
            Ok(sim, new SetBuyGreenFodder(sim.State.Clock.SimTime, false));
            sim.State.Market.Stock[sim.Config.Game.Livestock.Straw] = 0;
            sim.State.Market.StockCost[sim.Config.Game.Livestock.Straw] = 0;
            LivestockKit.ToMorning(sim);
            FeedingReport r2 = LivestockKit.L(sim).LastFeeding;
            Assert.That(r2.GreenBought, Is.EqualTo(0));
            Assert.That(System.Math.Abs((LivestockKit.L(sim).Animals[0].FedScore - Fixed.Parse("0.44")).Raw), Is.LessThan(100), "commons 10% × 0.4 + feed 0.4");
        }

        [Test]
        public void Hunger_NeverDies()
        {
            Simulation sim = LivestockKit.WithShed();
            Animal a = LivestockKit.Buy(sim);
            Ok(sim, new SetBuyGreenFodder(sim.State.Clock.SimTime, false));
            LivestockKit.ToMorning(sim);
            Assert.That(a.HungryDays, Is.EqualTo(1));
            Assert.That(a.Health, Is.EqualTo(Fixed.One), "one hungry morning does no harm");
            LivestockKit.ToMorning(sim);
            Assert.That(a.Health, Is.EqualTo(Fixed.Parse("0.9")));
            for (int d = 0; d < 10; d++) LivestockKit.ToMorning(sim);
            Assert.That(a.Health, Is.EqualTo(Fixed.Parse("0.5")), "never below the floor");
            Assert.That(LivestockKit.L(sim).Animals, Has.Count.EqualTo(1), "she never dies");
            Ok(sim, new SetBuyGreenFodder(sim.State.Clock.SimTime, true));
            Fill(sim, "straw", 500_000);
            Fill(sim, "cattle_feed", 500_000);
            LivestockKit.ToMorning(sim);
            Assert.That(a.HungryDays, Is.EqualTo(0));
            Assert.That(a.Health, Is.EqualTo(Fixed.Parse("0.55")));
        }

        [Test]
        public void FeedLow()
        {
            Simulation sim = LivestockKit.WithShed();
            sim.DrainEvents();
            LivestockKit.Buy(sim);
            Assert.That(sim.PendingEvents.OfType<FeedLow>().Select(e => e.Good), Is.EquivalentTo(new[] { "straw", "cattle_feed" }), "warned at purchase");
            sim.DrainEvents();
            LivestockKit.ToMorning(sim);
            Assert.That(sim.PendingEvents.OfType<FeedLow>(), Is.Empty, "once until refilled");
            Fill(sim, "cattle_feed", 400_000);
            LivestockKit.ToMorning(sim); // 2.5 days left: not yet low
            Assert.That(sim.PendingEvents.OfType<FeedLow>().Where(e => e.Good == "cattle_feed"), Is.Empty);
            sim.DrainEvents();
            LivestockKit.ToMorning(sim); // 1.5 days left
            Assert.That(sim.PendingEvents.OfType<FeedLow>().Single(e => e.Good == "cattle_feed").DaysLeft, Is.EqualTo(1));
        }

        [Test]
        public void Places_OutsideTheHutStore()
        {
            Simulation sim = LivestockKit.WithShed();
            long used = Market.Store.UsedGrams(sim);
            Fill(sim, "cattle_feed", 2_400_000);
            Fill(sim, "compost", 1_000_000);
            Assert.That(Market.Store.UsedGrams(sim), Is.EqualTo(used), "feed and compost don't take the hut store's room");
            Assert.That(sim.Execute(new Buy(sim.State.Clock.SimTime, "cattle_feed", 1000)).Reason, Is.EqualTo(RejectReason.PlaceFull));
            Simulation bare = Start();
            Assert.That(bare.Execute(new Buy(0, "straw", 1000)).Reason, Is.EqualTo(RejectReason.NoShed));
        }

        [Test]
        public void Straw_FromHarvest()
        {
            foreach (bool shed in new[] { true, false })
            {
                Simulation sim = shed ? LivestockKit.WithShed() : Start();
                int straw = sim.Config.Game.Livestock.Straw;
                long t = sim.State.Clock.SimTime;
                Ok(sim, new Sow(t, HomeTile, "wheat", Farm.SeedSource.Certified, "none"));
                sim.AdvanceTo(t + 6 * Hour);
                Ok(sim, new Harvest(sim.State.Clock.SimTime, HomeTile, Market.Overflow.SellRestToTrader));
                long grams = sim.PendingEvents.OfType<CropHarvested>().Last().Grams;
                Assert.That(sim.State.Market.Stock[straw], Is.EqualTo(shed ? (Fixed.Parse("1.5").MulRatio(grams, 1)).RoundToLong() : 0), shed ? "1.5 kg a kg of wheat" : "no stack: ploughed in");
            }
        }

        [Test]
        public void Dung_ToCompostPit()
        {
            Simulation sim = LivestockKit.WithShed();
            int compost = sim.Config.Game.Livestock.Compost;
            long before = sim.State.Market.Stock[compost];
            LivestockKit.Buy(sim);
            LivestockKit.ToMorning(sim);
            Assert.That(sim.State.Market.Stock[compost] - before, Is.EqualTo(216_600), "9.5 kg × 22.8 a game day");
        }
        // ---- US-201: health (spec 012 §3.10) ----

        /// <summary>The co-op pays nothing, so a town with no cash stays without it (the vet and vaccine tests).</summary>
        private static string NoMilkMoney(string file, string json)
        {
            return file == "livestock.json" ? json.Replace("\"milkBasePerLitre\": 12", "\"milkBasePerLitre\": 0").Replace("\"milkPerFatPoint\": 6.5", "\"milkPerFatPoint\": 0") : json;
        }

        [Test]
        public void Health_VaccineDisease()
        {
            Simulation sim = LivestockKit.WithShed(Game(NoMilkMoney)); // the morning's milk money would pay the vet
            Animal a = LivestockKit.Buy(sim);
            Assert.That(LivestockRules.DiseaseChance(sim, a), Is.EqualTo(Fixed.Parse("0.01")));
            a.Health = Fixed.Parse("0.5");
            Assert.That(LivestockRules.DiseaseChance(sim, a), Is.EqualTo(Fixed.Parse("0.02")), "× (1 + 2 × 0.5)");
            a.Health = Fixed.One;

            // The first morning of the next season: the co-op's vet vaccinates her (300).
            long vet = sim.State.Ledger.Balance(Account.VetAndBreeding);
            sim.AdvanceTo(sim.Clock.SecondsPerSeason + 6 * Hour);
            Assert.That(a.VaccinatedPeriod, Is.EqualTo(1));
            Assert.That(sim.State.Ledger.Balance(Account.VetAndBreeding) - vet, Is.GreaterThanOrEqualTo(Money(300)));

            // With no cash at the next season start she goes unvaccinated and her risk is × 3; nothing is owed.
            CashTo(sim, 0);
            sim.DrainEvents();
            sim.AdvanceTo(2 * sim.Clock.SecondsPerSeason + 6 * Hour);
            Assert.That(sim.PendingEvents.OfType<VaccinationMissed>().Count(), Is.EqualTo(1));
            Assert.That(a.VaccinatedPeriod, Is.EqualTo(1));
            Assert.That(LivestockRules.DiseaseChance(sim, a), Is.EqualTo(Fixed.Parse("0.03") * (Fixed.One + Fixed.FromInt(2) * (Fixed.One - a.Health))));
            Assert.That(sim.State.Ledger.Cash, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void Disease_VetAndNoVet()
        {
            foreach (bool cash in new[] { true, false })
            {
                Simulation sim = LivestockKit.WithShed(Game((f, j) => NoMilkMoney(f, NoSpoilage(f, j))));
                Animal a = LivestockKit.Buy(sim);
                if (!cash) CashTo(sim, 0);
                AnimalSick sick = null;
                for (int d = 0; d < 400 && sick == null; d++)
                {
                    sim.DrainEvents();
                    if (cash) { Fill(sim, "straw", 136_800); Fill(sim, "cattle_feed", 114_000); }
                    LivestockKit.ToMorning(sim);
                    sick = sim.PendingEvents.OfType<AnimalSick>().FirstOrDefault();
                }
                Assert.That(sick, Is.Not.Null, "a seeded illness within 400 days");
                Assert.That(sick.VetCame, Is.EqualTo(cash));
                Assert.That(sick.Cost, Is.EqualTo(cash ? Money(1500) : 0));
                Assert.That(a.SickDays, Is.EqualTo(cash ? 1 : 2));
                sim.DrainEvents();
                LivestockKit.ToMorning(sim);
                Assert.That(a.LastMilkMl, Is.EqualTo(0), "her milk on a sick day is thrown away");
                Assert.That(LivestockKit.L(sim).Animals, Has.Count.EqualTo(1), "illness never kills");
            }
        }

        [Test]
        public void Disease_SameSeedSameDays()
        {
            string Run()
            {
                Simulation sim = LivestockKit.WithShed();
                LivestockKit.Buy(sim);
                var days = new System.Collections.Generic.List<long>();
                for (int d = 0; d < 120; d++)
                {
                    sim.DrainEvents();
                    LivestockKit.ToMorning(sim);
                    if (sim.PendingEvents.OfType<AnimalSick>().Any()) days.Add(sim.State.Clock.SimTime / Day);
                }
                return string.Join(",", days);
            }
            Assert.That(Run(), Is.EqualTo(Run()));
        }
        // ---- US-202: the dairy loan and foreclosure (spec 012 §3.12) ----

        [Test]
        public void DairyLoan_Foreclosure()
        {
            Simulation sim = LivestockKit.WithShed();
            long t = sim.State.Clock.SimTime;
            int bank = sim.Config.Game.Credit.IndexOfLender("coop_bank");
            Assert.That(Finance.CreditRules.Limit(sim, bank).Limit, Is.EqualTo(0), "no land, no income yet: nothing to lend against");
            // With her as collateral: 1,00,000 × 0.75 × the score factor at 650 (0.75) = 56,250, rounded down to 56,200.
            Assert.That(Finance.CreditRules.Limit(sim, bank, 0, Money(100000)).Limit, Is.EqualTo(Money(56200)));
            Assert.That(sim.Execute(new BuyAnimal(t, "buffalo", new Financing("coop_bank", Money(60000), 8))).Reason, Is.EqualTo(RejectReason.OverLimit));
            Assert.That(sim.Execute(new BuyAnimal(t, "buffalo", new Financing("coop_bank", Money(100001), 8))).Reason, Is.EqualTo(RejectReason.InvalidAmount));
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new BuyAnimal(t, "buffalo", new Financing("coop_bank", Money(50000), 8)));
            Animal a = LivestockKit.L(sim).Animals.Single();
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money(50000)), "half down, half the bank's");
            Assert.That(a.LoanId, Is.GreaterThan(0));
            Assert.That(sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(-Money(50000)));

            // Selling her repays the bank first: nothing else supports the loan.
            sim.AdvanceTo(t + Hour);
            Ok(sim, new SellAnimal(sim.State.Clock.SimTime, a.Id));
            Assert.That(sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(0), "the bank was paid from the sale");

            // Foreclosure takes animals after bought land, at their fair price, only until the debt is covered; the shed stays.
            Ok(sim, new BuyAnimal(sim.State.Clock.SimTime, "buffalo", new Financing("coop_bank", Money(50000), 8)));
            LivestockKit.Buy(sim, "cow");
            sim.DrainEvents();
            Finance.CreditSystem.Foreclose(sim);
            var seized = sim.PendingEvents.OfType<AnimalSeized>().ToList();
            Assert.That(seized.Count, Is.EqualTo(1), "one buffalo covers the 50,000");
            Assert.That(seized[0].Amount, Is.EqualTo(Money(80000)));
            Assert.That(LivestockKit.L(sim).Animals.Single().Kind, Is.EqualTo(sim.Config.Game.Livestock.IndexOfAnimal("cow")));
            Assert.That(LivestockRules.Shed(sim), Is.Not.Null, "the shed is on the protected yard");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-16: a dairy year gives the same hash in one advance or in hours.
        [Test]
        public void ChunkingInvariance()
        {
            Simulation Setup()
            {
                Simulation sim = LivestockKit.WithShed();
                long t = sim.State.Clock.SimTime;
                Ok(sim, new Sow(t, HomeTile, "berseem", Farm.SeedSource.Certified, "compost"));
                LivestockKit.Buy(sim);
                LivestockKit.Buy(sim, "cow");
                Fill(sim, "straw", 5_000_000);
                Fill(sim, "cattle_feed", 2_000_000);
                return sim;
            }
            Simulation one = Setup(), many = Setup();
            long end = one.State.Clock.SimTime + one.Clock.SecondsPerSeason * 4 + 7 * Hour + 13;
            one.AdvanceTo(end);
            for (long x = many.State.Clock.SimTime; x < end; x += 3 * Hour + 17) many.AdvanceTo(System.Math.Min(end, x));
            many.AdvanceTo(end);
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
        }
    }
}
