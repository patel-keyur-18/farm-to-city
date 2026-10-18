using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class FounderTests
    {
        // Spec 003 AC-19: at health 0.5 the seeded sick-day rate is 25% ± 1 point; a sick day gives 0 hours and FounderSick.
        [Test]
        public void SickDays()
        {
            FounderDef d = TestContent.Game().Farm.Founder;
            Fixed chance = d.SickChanceAtMinHealth * (Fixed.One - d.MinHealth) / (Fixed.One - d.MinHealth);
            Assert.That(chance, Is.EqualTo(Fixed.Parse("0.25")));
            int sick = 0;
            for (ulong day = 0; day < 10_000; day++) if (Rng.Chance(Rng.Next(42, RngStream.FounderSick, 0, day), chance)) sick++;
            Assert.That(sick / 10_000.0, Is.EqualTo(0.25).Within(0.01));

            // In the sim: health held at 0.5 before a pass whose draw is sick.
            Simulation sim = NewFarm(game: NoEating());
            long dayIndex = 1;
            while (!Rng.Chance(Rng.Next(42, RngStream.FounderSick, 0, (ulong)dayIndex), chance)) dayIndex++;
            sim.AdvanceTo(dayIndex * Day - 1);
            F(sim).Health = Fixed.Parse("0.49"); // basic food: +0.02 − 0.01 at the pass gives 0.50
            sim.DrainEvents();
            sim.AdvanceTo(dayIndex * Day);
            FarmTest.AssertNear(F(sim).Health, "0.5", 64);
            Assert.That(F(sim).SickToday, Is.True);
            Assert.That(F(sim).TodayHours, Is.EqualTo(Fixed.Zero));
            Assert.That(FounderRules.LabourRate(sim), Is.EqualTo(Fixed.Zero));
            Assert.That(sim.DrainEvents().OfType<FounderSick>().Single().Day, Is.EqualTo(dayIndex));
            Assert.That(sim.Execute(new GoToMandi(sim.State.Clock.SimTime, new[] { new Market.MandiLoad("wheat", 1_000) })).Reason,
                Is.EqualTo(RejectReason.FounderSick));
        }

        // Spec 003 AC-34 (ADR 0031, 0034): on subsistence a Winter day costs noWarmClothingPenalty more health; time-weighted.
        [Test]
        public void WarmClothing_Winter()
        {
            FounderDef d = TestContent.Game().Farm.Founder;
            Fixed DayChange(long dayStart, bool subsistence, long switchToBasicAt = -1)
            {
                Simulation sim = NewFarm(game: NoEating((f, j) => subsistence ? StartSubsistence(f, NoSickness(f, j)) : NoSickness(f, j)));
                sim.AdvanceTo(dayStart);
                F(sim).Health = Fixed.Parse("0.9"); // away from the cap
                if (switchToBasicAt >= 0) Assert.That(sim.Execute(new SetDietPlan(switchToBasicAt, "basic")).Accepted, Is.True);
                sim.AdvanceTo(dayStart + Day);
                return F(sim).Health - Fixed.Parse("0.9");
            }
            long summer = 1 * FarmTest.S + Day, winter = 3 * FarmTest.S + Day;
            FarmTest.AssertNear(DayChange(summer, true) - DayChange(winter, true), d.NoWarmClothingPenalty.ToString(), 64);
            FarmTest.AssertNear(DayChange(winter, true), "-0.03", 64);
            Assert.That(DayChange(winter, false), Is.EqualTo(DayChange(summer, false)), "on basic, no penalty");
            // Half a Winter day on subsistence: nutrition 0.85 on average and half the clothing penalty: 0.02 − 0.02 − 0.01.
            FarmTest.AssertNear(DayChange(winter, true, winter + Day / 2), "-0.01", 64);
        }

        // Spec 003 AC-35.
        [Test]
        public void Medicine()
        {
            Simulation sim = NewFarm(game: NoEating());
            F(sim).Health = Fixed.Parse("0.7");
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new BuyMedicine(0)).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(500_00));
            Assert.That(sim.State.Ledger.Balance(Account.HealthCosts), Is.EqualTo(500_00));
            FarmTest.AssertNear(F(sim).Health, "0.8", 4);
            Assert.That(sim.Execute(new BuyMedicine(Hour)).Reason, Is.EqualTo(RejectReason.AlreadyTreatedToday));
            F(sim).Health = Fixed.Parse("0.95");
            Assert.That(sim.Execute(new BuyMedicine(Day)).Accepted, Is.True);
            Assert.That(F(sim).Health, Is.EqualTo(Fixed.One), "capped at 1");
        }
    }
}
