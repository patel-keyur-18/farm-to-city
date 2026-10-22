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
        public void Books_FarmProfitCountsMilk()
        {
            Simulation sim = LivestockKit.WithShed();
            LivestockKit.Buy(sim);
            LivestockKit.ToMorning(sim);
            var lines = Reporting.LinesFrom(new[] { new System.Collections.Generic.KeyValuePair<Account, long>(Account.MilkSales, 100),
                new System.Collections.Generic.KeyValuePair<Account, long>(Account.AnimalFeed, -40), new System.Collections.Generic.KeyValuePair<Account, long>(Account.LivestockSales, 10),
                new System.Collections.Generic.KeyValuePair<Account, long>(Account.VetAndBreeding, -5) });
            Assert.That(lines.Single(l => l.Key == "farmProfit").Amount, Is.EqualTo(65));
        }
    }
}
