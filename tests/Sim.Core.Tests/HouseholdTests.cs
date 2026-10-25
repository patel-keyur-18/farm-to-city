using System.Linq;
using NUnit.Framework;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 014 §3.5: household budgets by Engel's law, food met, savings and health.</summary>
    public class HouseholdTests
    {
        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        private static Household HouseholdOf(Simulation sim, Hand h)
        {
            return sim.State.Citizens.Household(sim.State.Citizens.OfHand(h.Id).HouseholdId);
        }

        // AC-4: a hand alone in his hut keeps half his wage (the rest goes home); the food share follows the Engel curve; food met;
        // clothing 8%, health 5%, education 5%; the rest half leisure, half saved.
        [Test]
        public void Budget_Engel()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            h.Wage = 30_000_00;
            Household x = HouseholdOf(sim, h);
            HouseholdRules.SetBudget(sim, x);
            Assert.That(x.Income, Is.EqualTo(15_000_00), "30,000 less half sent home");
            // 15,000 a person lies between 8,000 (55%) and 20,000 (40%): 55% − 15% × 7,000 ÷ 12,000 = 46.25%.
            Assert.That(HouseholdRules.FoodShare(sim, 15_000_00), Is.EqualTo(Fixed.Parse("0.4625")));
            Assert.That(x.Food, Is.EqualTo(6_937_50));
            Assert.That((x.FoodNeed, x.FoodMet), Is.EqualTo((4_500_00L, Fixed.One)));
            Assert.That((x.Clothing, x.HealthCare, x.Schooling, x.Rent), Is.EqualTo((1_200_00L, 750_00L, 750_00L, 0L)));
            Assert.That(x.Leisure + x.Saved, Is.EqualTo(15_000_00 - 6_937_50 - 1_200_00 - 750_00 - 750_00));
            Assert.That(x.Leisure, Is.EqualTo(2_681_25));
            Assert.That(x.Savings, Is.EqualTo(0), "banked at the close, when it's been earned");
            sim.AdvanceTo(NextClose(sim));
            Assert.That(x.Savings, Is.GreaterThanOrEqualTo(2_681_25), "the season's saving banked (at least: the renewed wage may be higher)");

            // The curve's ends: everything to food below the first point's share, flat past the last.
            Assert.That(HouseholdRules.FoodShare(sim, 0), Is.EqualTo(Fixed.Parse("0.65")));
            Assert.That((HouseholdRules.FoodShare(sim, 4_000_00) - Fixed.Parse("0.6")).Raw, Is.InRange(-4, 4), "halfway to 8,000: 60%");
            Assert.That(HouseholdRules.FoodShare(sim, 1_00_000_00), Is.EqualTo(Fixed.Parse("0.25")));
            // Poorer households spend a larger share on food (Engel's law).
            Fixed last = Fixed.One;
            for (long income = 0; income <= 80_000_00; income += 2_000_00)
            {
                Fixed share = HouseholdRules.FoodShare(sim, income);
                Assert.That(share, Is.LessThanOrEqualTo(last));
                last = share;
            }
        }

        // AC-4: a short season draws on savings before food is cut; health falls by the shortfall and recovers once fed again.
        [Test]
        public void Savings_ShortSeason()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            Household x = HouseholdOf(sim, h);
            Citizen c = sim.State.Citizens.OfHand(h.Id);

            // 6,000 a season, half sent home: 3,000 at 61.25% buys 1,837.50 of the 4,500 they need.
            h.Wage = 6_000_00;
            x.Savings = 2_000_00;
            HouseholdRules.SetBudget(sim, x);
            Assert.That(x.Income, Is.EqualTo(3_000_00));
            Assert.That(x.Food, Is.EqualTo(1_837_50 + 2_000_00), "every rupee saved goes to food first");
            Assert.That(x.FoodMet, Is.EqualTo(Fixed.FromRatio(3_837_50, 4_500_00)));
            Assert.That(x.Savings, Is.EqualTo(0), "savings used up");

            // No savings left: food met falls further.
            x.Savings = 0;
            HouseholdRules.SetBudget(sim, x);
            Fixed met = x.FoodMet;
            Assert.That(met, Is.EqualTo(Fixed.FromRatio(1_837_50, 4_500_00)));

            // At the close, health falls by 0.20 × the shortfall; the new season's budget is set on the renewed wage.
            h.Wage = 30_000_00; // a loyal hand keeps his wage at renewal
            Fixed before = c.Health;
            sim.AdvanceTo(NextClose(sim));
            Assert.That(c.Health, Is.EqualTo(before - Fixed.Parse("0.20") * (Fixed.One - met)));
            Assert.That(x.Income, Is.EqualTo(h.Wage - Finance.Money.MulRate(h.Wage, Fixed.Parse("0.5"))), "the renewed wage, half sent home");
            Assert.That(x.FoodMet, Is.EqualTo(Fixed.One));
            Fixed fell = c.Health;
            sim.AdvanceTo(NextClose(sim));
            Assert.That(c.Health, Is.EqualTo(fell + Fixed.Parse("0.10") > Fixed.One ? Fixed.One : fell + Fixed.Parse("0.10")), "fed again, health recovers");
        }

    
        // AC-12: an adult without a town job earns the day-labour rate × 8 × 0.5 each daily pass; children and elders earn nothing;
        // hired labourers are the town's outside workers first, their household paid the labourer's wage in place of that day's
        // outside work; what was earned beyond the budget's expectation is saved at the close.
        [Test]
        public void OutsideWork_LabourersFirst()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            Household x = AddFamily(sim, h.HutId, 30);
            Household y = AddFamily(sim, h.HutId, 12, 66); // a child and an elder
            Citizen adult = sim.State.Citizens.Citizen(x.Members[0]);
            Assert.That(HouseholdRules.OutsideDayPay(sim), Is.EqualTo(4_500_00), "1,125 an hour × 8 × 0.5");
            Assert.That(HouseholdRules.OutsideSeasonPay(sim), Is.EqualTo(18_000_00), "4 days");
            Assert.That(HouseholdRules.EmploymentRate(sim), Is.EqualTo(Fixed.FromRatio(1, 2)), "the hand of 2 working-age adults");

            long t0 = (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(t0);
            Assert.That((x.Income, x.OutsideExpected, y.Income), Is.EqualTo((18_000_00L, 18_000_00L, 0L)), "the adult only");
            long savings = x.Savings, saved = x.Saved;
            long day1 = t0 + TestSim.Day;
            sim.AdvanceTo(day1);
            Assert.That((x.Earned, y.Earned), Is.EqualTo((4_500_00L, 0L)), "one adult's day; children and elders don't work");

            // Sow, then hire one labourer: the town's adult comes, not a neighbour; the founder pays as always.
            Assert.That(PeopleRules.FreeNow(sim), Is.EqualTo(P(sim).Roster.Count(n => n.FreeToday && !n.Hired) + 1), "the town's adult is free too");
            Ok(sim, new Commands.Sow(day1, HomeTile, "radish", Farm.SeedSource.Certified, "none"));
            long until = PeopleRules.LabourUntil(sim);
            long cash = sim.State.Ledger.Cash, cost = PeopleRules.LabourCost(sim, 1, until);
            Ok(sim, new Commands.HireLabourers(day1, 1));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - cost));
            Assert.That(adult.LabourUntil, Is.EqualTo(until));
            Assert.That(P(sim).Roster.Any(n => n.Hired), Is.False, "no neighbour needed");
            Assert.That(x.Earned, Is.EqualTo(4_500_00 + cost), "the labourer's wage to his household");
            Assert.That(PeopleRules.DayLabourHours(sim), Is.EqualTo(Fixed.FromInt(8)));

            // He goes home when the crops are in; that day's outside work isn't paid as well; the next day it is.
            sim.AdvanceTo(until);
            Assert.That(adult.LabourUntil, Is.EqualTo(0));
            sim.AdvanceTo(day1 + TestSim.Day);
            Assert.That(x.Earned, Is.EqualTo(4_500_00 + cost));
            sim.AdvanceTo(day1 + 2 * TestSim.Day);
            Assert.That(x.Earned, Is.EqualTo(9_000_00 + cost));

            // At the close (after day 4's pay): 3 days outside + the wage, against the 4 days expected; the difference is saved.
            sim.AdvanceTo(t0 + sim.Clock.SecondsPerSeason);
            Assert.That(x.Earned, Is.EqualTo(0), "a new season");
            Assert.That(x.Savings, Is.EqualTo(savings + saved + 13_500_00 + cost - 18_000_00));
        }
    }
}
