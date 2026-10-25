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
            Assert.That(s.Citizens.Single().Living, Is.False, "kept until the close (AC-13)");
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Citizens.Citizens, Is.Empty, "a lone hand who went home isn't kept: no living relative links to him");
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
    
        /// <summary>A hand's citizen made <paramref name="age"/> years old now.</summary>
        private static Citizen AgedHand(Simulation sim, Hand h, int age)
        {
            Citizen c = sim.State.Citizens.OfHand(h.Id);
            c.BornSeason = CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime) - age;
            return c;
        }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        // AC-1: everyone ages a year at each season close; a hand turning 60 finishes his season and isn't renewed (CitizenRetired).
        [Test]
        public void Ageing_Retirement()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            Citizen c = AgedHand(sim, h, 58);
            sim.AdvanceTo(NextClose(sim));
            Assert.That(CitizenRules.Age(sim, c), Is.EqualTo(59), "a year at the close");
            Assert.That(P(sim).Hands.Single().Contract, Is.EqualTo(ContractState.Active), "renewed at 59");
            sim.AdvanceTo(NextClose(sim) - 1);
            Assert.That(CitizenRules.Age(sim, c), Is.EqualTo(59), "no birthday mid-season");
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            Events.CitizenRetired e = sim.PendingEvents.OfType<Events.CitizenRetired>().Single();
            Assert.That((e.HandId, e.Age, e.Name, e.WentHome), Is.EqualTo((h.Id, 60, h.Name, true)));
            Assert.That(sim.PendingEvents.OfType<Events.ContractRenewed>(), Is.Empty, "not renewed");
            Assert.That(P(sim).Hands, Is.Empty);
            Assert.That(P(sim).Orders.Where(o => o.HandId == h.Id), Is.Empty);
        }

        // §3.13: a retired hand living alone goes home; his hut is free and a new hand can be hired into it.
        [Test]
        public void RetiredHandFreesHut()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            long hut = h.HutId;
            AgedHand(sim, h, 59);
            sim.AdvanceTo(NextClose(sim));
            Assert.That(sim.State.Citizens.Population, Is.EqualTo(0), "gone home to his village");
            Assert.That(sim.State.Citizens.Citizens, Is.Empty, "nobody links to him");
            Assert.That(PeopleRules.EmptyHut(sim).Id, Is.EqualTo(hut));
            Hand next = HireHand(sim);
            Assert.That(next.HutId, Is.EqualTo(hut));
            Assert.That(sim.State.Citizens.OfHand(next.Id), Is.Not.Null);

            // With his household in another home (his family's house, US-208), he stays on as its elder, retired.
            Citizen c = AgedHand(sim, next, 59);
            sim.State.Citizens.Household(c.HouseholdId).HomeId = 9_999;
            sim.AdvanceTo(NextClose(sim));
            Assert.That((c.Living, c.Job, c.HandId), Is.EqualTo((true, CitizenJob.Retired, 0L)));
            Assert.That(sim.State.Citizens.Population, Is.EqualTo(1));
            Assert.That(PeopleRules.EmptyHut(sim).Id, Is.EqualTo(hut), "his hut is free");
        }
    }
}
