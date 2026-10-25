using NUnit.Framework;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 014 §3.8: needs layers, weights and the gate.</summary>
    public class HappinessTests
    {
        private static int Score(Household x) { return (int)x.Happiness.RoundToLong(); }

        // AC-5 (owner, 2026-10-24: the rule kept, the outside-work example corrected to 68).
        [Test]
        public void Layers()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            Citizen c = sim.State.Citizens.OfHand(h.Id);
            Household x = sim.State.Citizens.Household(c.HouseholdId);
            x.FoodMet = Fixed.One;
            x.Leisure = 1_000_00;

            // Enough food, a home, a hand's job, rent paid: 40 + 30 × (1 + 0.5 + 1) ÷ 3 + 20 × 0.8 + 10 × 0.3 = 84.
            HappinessRules.Update(sim, x);
            Assert.That(Score(x), Is.EqualTo(84));
            Assert.That((x.Survival, x.Belonging, x.Growth), Is.EqualTo((Fixed.One, Fixed.Parse("0.8"), Fixed.Parse("0.3"))));

            // A hand's job but short of food (0.6): security gated at 0.75: 24 + 18.75 + 16 + 3 = 61.75.
            x.FoodMet = Fixed.Parse("0.6");
            HappinessRules.Update(sim, x);
            Assert.That(Score(x), Is.EqualTo(62));

            // Outside work only: security 0.5 gates belonging at 0.625: 40 + 15 + 10 + 3 = 68.
            x.FoodMet = Fixed.One;
            c.Job = CitizenJob.Outside;
            c.HandId = 0;
            HappinessRules.Update(sim, x);
            Assert.That(Score(x), Is.EqualTo(68));

            // Crowded (2 in a hut for 1) halves survival; nothing left for leisure drops growth; mourning takes 10 this season.
            x.Members.Add(999);
            Assert.That(HappinessRules.Shelter(sim, x), Is.EqualTo(Fixed.FromRatio(1, 2)));
            x.Members.Remove(999);
            x.Leisure = 0;
            HappinessRules.Update(sim, x);
            Assert.That(Score(x), Is.EqualTo(65));
            x.MourningSeason = CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime);
            HappinessRules.Update(sim, x);
            Assert.That(Score(x), Is.EqualTo(55));
            Assert.That(HappinessRules.Town(sim), Is.EqualTo(x.Happiness), "one household: the town's is its");
        }

        // §3.12: needs are updated at each daily pass.
        [Test]
        public void DailyPass()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            Household x = sim.State.Citizens.Household(sim.State.Citizens.OfHand(h.Id).HouseholdId);
            x.Happiness = Fixed.Zero;
            sim.AdvanceTo((sim.State.Clock.SimTime / TestSim.Day + 1) * TestSim.Day);
            Assert.That(x.Happiness, Is.GreaterThan(Fixed.Zero));
        }
    }
}
