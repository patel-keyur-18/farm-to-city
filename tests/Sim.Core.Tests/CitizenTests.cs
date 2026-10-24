using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 014: citizens and households.</summary>
    public class CitizenTests
    {
        // §3.4, §3.11: a hired applicant becomes a citizen, aged as the applicant was, the gender of the name, a household of one in his
        // hut; dismissed, he goes home to his village and nobody keeps him (no living relative).
        [Test]
        public void HandBecomesCitizen_AndLeaves()
        {
            Simulation sim = Start();
            CitizensState s = sim.State.Citizens;
            Assert.That(s, Is.Not.Null);
            Assert.That(s.Population, Is.EqualTo(0));
            BuildHut(sim);
            Applicant a = WaitForApplicant(sim);
            int age = CitizenRules.ApplicantAge(sim, a);
            Assert.That(age, Is.InRange(20, 45));
            Hand h = HireHand(sim);
            Citizen c = s.OfHand(h.Id);
            Assert.That(c, Is.Not.Null);
            Assert.That((c.Name, c.Female, c.Job, c.Health), Is.EqualTo((h.Name, sim.Config.Game.People.Names[h.Name].Female, CitizenJob.Hand, Maths.Fixed.One)));
            Assert.That(CitizenRules.Age(sim, c), Is.InRange(20, 45));
            Assert.That(c.Education, Is.InRange(0, 1));
            Household x = s.Household(c.HouseholdId);
            Assert.That((x.Members.Single(), x.HomeId), Is.EqualTo((c.Id, h.HutId)));
            Assert.That(s.Population, Is.EqualTo(P(sim).Hands.Count));

            Ok(sim, new DismissHand(sim.State.Clock.SimTime, h.Id));
            Assert.That(s.Population, Is.EqualTo(0));
            Assert.That(s.Households, Is.Empty);
            Assert.That(s.Citizens, Is.Empty, "a lone hand who went home isn't kept: no living relative links to him");
        }

        // AC-10: a year with a hand, in one advance or in odd chunks, gives the same hash; the town round-trips through a save.
        [Test]
        public void ChunkingInvariance()
        {
            Simulation Setup()
            {
                Simulation sim = Start();
                HireHand(sim);
                return sim;
            }
            Simulation one = Setup(), many = Setup();
            Assert.That(one.State.ComputeHash(), Is.EqualTo(many.State.ComputeHash()));
            long end = one.State.Clock.SimTime + one.Clock.SecondsPerYear + 5 * Hour + 11;
            one.AdvanceTo(end);
            for (long x = many.State.Clock.SimTime; x < end; x += 7 * Hour + 23) many.AdvanceTo(System.Math.Min(end, x));
            many.AdvanceTo(end);
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
            Saves.LoadResult r = SaveTests.RoundTrip(one, one.Config);
            Assert.That(r.Error, Is.EqualTo(Saves.SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
            Assert.That(r.State.Citizens.Population, Is.EqualTo(one.State.People.Hands.Count));
        }
    }
}
