using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-103 (spec 023 §3.5, spec 022 AC-8, ADR 0116): the old village as one record: joining, residents, pupils, its tax, its happiness and leaving.</summary>
    public class CohortTests
    {
        private static Simulation Township(System.Func<string, string, string> edit = null)
        {
            Simulation sim = TownshipTests.Start(cohort: true, edit);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            return sim;
        }

        // Review fix: a town that reached the Township before the old village could join (a schema-19 save) is joined at its next close.
        [Test]
        public void JoinsAtTheCloseIfMissed()
        {
            Simulation sim = Township();
            CohortRules.State(sim).Joined = false;
            CohortRules.State(sim).Households = 0;
            Assert.That(LandValueRules.On(sim), Is.False);
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + Hour);
            Assert.That((CohortRules.Joined(sim), CohortRules.State(sim).Households), Is.EqualTo((true, 30)));
            Assert.That(LandValueRules.On(sim), Is.True, "land value, zoning and leasing switch on");
        }

        // AC-6: 30 households and 150 people join at the unlock; they are residents, and the school's pupils.
        [Test]
        public void Join()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            Assert.That(CohortRules.Joined(sim), Is.False, "nothing before the unlock");
            Assert.That(TreasuryRules.Residents(sim), Is.EqualTo(sim.State.Citizens.Population));
            TownshipTests.Families(sim, 2);
            int own = sim.State.Citizens.Population;
            Ok(sim, new BuildSchool(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 49 * Hour);
            TreasuryTests.Township(sim);
            CohortState c = CohortRules.State(sim);
            Assert.That((c.Joined, c.Households, CohortRules.People(sim)), Is.EqualTo((true, 30, 150)));
            Assert.That(TreasuryRules.Residents(sim), Is.EqualTo(own + 150), "residents for the grants");
            Assert.That(CohortRules.Pupils(sim), Is.EqualTo(10), "about 10 children who can't read yet join the school free");
            Assert.That(SchoolRules.Teachers(sim), Is.EqualTo(1));
            Assert.That(ClinicRules.Served(sim), Is.EqualTo(own + 150), "the clinic serves them too");
            Assert.That(c.Happiness, Is.GreaterThan(Fixed.FromInt(60)), "a town left alone keeps them happy");
            Assert.That(sim.PendingEvents.OfType<CohortJoined>().Single().Households, Is.EqualTo(30));
        }

        // AC-6: the grants count them: untied 100 a resident.
        [Test]
        public void Grants()
        {
            Simulation sim = Township();
            TreasuryRules.GrantsNow(sim, out long untied, out _, out _);
            Assert.That(untied, Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 100 * Coin * TreasuryRules.Residents(sim))));
            Assert.That(TreasuryRules.Residents(sim), Is.GreaterThan(150));
        }

        // spec 022 AC-8: the old village's tax comes from leisure and savings before food; food stays met; the treasury receives what they pay.
        [Test]
        public void Burden()
        {
            Simulation sim = Township(delegate(string f, string j) { return f == "treasury.json" ? j.Replace("\"rate\": 0.016", "\"rate\": 0.024") : j; });
            CohortState c = CohortRules.State(sim);
            CohortRules.Budget before = CohortRules.BudgetOf(sim);
            Assert.That(before.FoodMet, Is.EqualTo(Fixed.One), "fed before the tax");
            long cash = TreasuryRules.State(sim).Ledger.Cash;
            long bill = CohortRules.TaxCollected(sim, TaxRules.Rate(sim, TaxKind.House));
            Assert.That(bill, Is.GreaterThan(0));
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(c.TaxThis, Is.EqualTo(bill), "the office collects its share, the village pays it");
            Assert.That(c.TaxThis, Is.LessThanOrEqualTo(CohortRules.LeisureTotal(sim)), "from leisure first");
            CohortRules.Budget after = CohortRules.BudgetOf(sim);
            Assert.That(after.Food, Is.EqualTo(before.Food), "never from food");
            Assert.That(after.FoodMet, Is.EqualTo(Fixed.One));
            Assert.That(-TreasuryRules.State(sim).Reports.Last().Balance(Account.HouseTaxIncome), Is.GreaterThanOrEqualTo(c.TaxThis), "the treasury received what they paid (and yours)");
            Assert.That(c.Happiness, Is.GreaterThan(Fixed.FromInt(60)), "the cap costs them little");
        }

        // The burden past leisure comes from savings; with neither, they pay what they can and the rest is not carried.
        [Test]
        public void Burden_FromSavings()
        {
            Simulation sim = Township(delegate(string f, string j) { return f == "township.json" ? j.Replace("\"income\": 36000", "\"income\": 1000") : f == "treasury.json" ? j.Replace("\"rate\": 0.016", "\"rate\": 0.024") : j; });
            CohortState c = CohortRules.State(sim);
            CohortRules.Budget b = CohortRules.BudgetOf(sim);
            long bill = CohortRules.TaxCollected(sim, TaxRules.Rate(sim, TaxKind.House));
            Assert.That(b.Leisure * 30, Is.LessThan(bill), "leisure on 1,000 is less than the tax");
            c.Savings = 100_000 * Coin;
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(c.TaxThis, Is.EqualTo(bill), "paid: the leisure first, then savings");
            Assert.That(c.Savings, Is.EqualTo(100_000 * Coin - (bill - b.Leisure * 30) + b.Saved * 30));
            c.Savings = 0;
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(c.TaxThis, Is.LessThanOrEqualTo(bill), "what they can");
        }

        // AC-6: under 35 for two closes running, one household (5 people) leaves.
        [Test]
        public void Leaves()
        {
            Simulation sim = Township(delegate(string f, string j) { return f == "township.json" ? j.Replace("\"income\": 36000", "\"income\": 2500") : j; });
            CohortState c = CohortRules.State(sim);
            Assert.That(c.Households, Is.EqualTo(30));
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(c.Happiness, Is.LessThan(Fixed.FromInt(35)), "hungry");
            Assert.That(c.Households, Is.EqualTo(30), "one close is not enough");
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(c.Households, Is.EqualTo(29));
            Assert.That(CohortRules.People(sim), Is.EqualTo(145));
            Assert.That(sim.PendingEvents.OfType<CohortHouseholdLeft>().Count(), Is.EqualTo(1));
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(c.Households, Is.EqualTo(29), "the count starts again after one leaves");
        }

        // A town left alone: at default taxes with the clinic and the school open they stay and stay happy.
        [Test]
        public void LeftAlone()
        {
            Simulation sim = Township();
            for (int i = 0; i < 8; i++) sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(CohortRules.State(sim).Households, Is.EqualTo(30));
            Assert.That(CohortRules.State(sim).Happiness, Is.GreaterThan(Fixed.FromInt(60)));
        }
    }
}
