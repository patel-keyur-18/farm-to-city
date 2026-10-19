using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class ApplicantTests
    {
        private static long CeilHour(long t) { return (t + Hour - 1) / Hour * Hour; }

        // Spec 006 AC-6.
        [Test]
        public void Arrival_Queue_Away()
        {
            Simulation sim = Start();
            BuildHut(sim);
            long built = sim.State.Clock.SimTime;
            sim.DrainEvents();

            // Arrivals at the seeded times, at most 3 waiting.
            var expected = new List<long>();
            long next = CeilHour(built + PeopleRules.ApplicantInterval(sim, 1, false));
            for (long id = 1; id <= 3; id++)
            {
                expected.Add(next);
                next = CeilHour(next + PeopleRules.ApplicantInterval(sim, id + 1, false));
            }
            sim.AdvanceTo(expected[2]);
            var arrived = sim.DrainEvents().OfType<ApplicantArrived>().Select(e => e.SimTime).ToList();
            Assert.That(arrived, Is.EqualTo(expected));
            Assert.That(P(sim).NextApplicantAt, Is.EqualTo(0), "the queue is full");
            sim.AdvanceTo(expected[0] + 2 * Day - Hour);
            Assert.That(sim.DrainEvents().OfType<ApplicantArrived>(), Is.Empty);

            // Each leaves 2 days after arriving.
            sim.AdvanceTo(expected[0] + 2 * Day);
            var left = sim.DrainEvents().OfType<ApplicantLeft>().ToList();
            Assert.That(left.Select(e => e.ApplicantId), Is.EqualTo(new long[] { 1 }));

            // Nothing arrives or leaves while away; the queue waits.
            Simulation away = Start();
            BuildHut(away);
            away.AdvanceTo(away.State.Clock.SimTime + Hour);
            int waiting = P(away).Applicants.Count;
            away.DrainEvents();
            Ok(away, new AdvanceTo(away.State.Clock.SimTime + 72 * Hour, AdvanceMode.Away));
            var events = away.DrainEvents();
            Assert.That(events.OfType<ApplicantArrived>().Concat<SimEvent>(events.OfType<ApplicantLeft>()), Is.Empty);
            Assert.That(P(away).Applicants.Count, Is.EqualTo(waiting));
        }

        // Spec 006 AC-7.
        [Test]
        public void Profile_Interview_Wage()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Simulation sim = Start(seed: seed);
                BuildHut(sim);
                Applicant a = WaitForApplicant(sim);
                PeopleRules.SkillRange(sim, a, out int lo, out int hi);
                Assert.That(a.Skill, Is.InRange(lo, hi));
                Assert.That(hi - lo, Is.EqualTo(System.Math.Min(100, a.Skill + 15) - System.Math.Max(0, a.Skill - 15)));
                Assert.That(a.Skill, Is.InRange(20, 70));
                Assert.That(a.Trait1, Is.Not.EqualTo(a.Trait2));
                Assert.That(a.SecondTraitKnown, Is.False);
                Ok(sim, new Interview(sim.State.Clock.SimTime, a.Id));
                Assert.That(a.SecondTraitKnown, Is.True);
                Assert.That(Last<ApplicantInterviewed>(sim).Trait, Is.EqualTo(sim.Config.Game.People.Traits[a.Trait2].Id));
                Assert.That(sim.Execute(new Interview(sim.State.Clock.SimTime, a.Id)).Reason, Is.EqualTo(RejectReason.AlreadyInterviewed));
            }
            Simulation s = Start();
            Assert.That(PeopleRules.AskedWage(s, Fixed.FromInt(50), false), Is.EqualTo(30_000 * Coin));
            Assert.That(PeopleRules.AskedWage(s, Fixed.FromInt(30), false), Is.EqualTo(27_600 * Coin));
            Assert.That(PeopleRules.AskedWage(s, Fixed.FromInt(70), false), Is.EqualTo(32_400 * Coin));
        }
    }

    public class HiringTests
    {
        // Spec 006 AC-18.
        [Test]
        public void PostJob_RateAndQuality()
        {
            Simulation sim = Start();
            double walk = 0, posted = 0;
            for (long id = 1; id <= 2000; id++)
            {
                walk += PeopleRules.ApplicantInterval(sim, id, false);
                posted += PeopleRules.ApplicantInterval(sim, id, true);
            }
            Assert.That(posted / walk, Is.EqualTo(0.5).Within(0.01), "twice as often");

            BuildHut(sim);
            long t = sim.State.Clock.SimTime, cash = sim.State.Ledger.Cash, selling = sim.State.Ledger.Balance(Finance.Account.TransportAndSelling);
            Ok(sim, new PostJob(t, true));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(200 * Coin));
            Assert.That(sim.State.Ledger.Balance(Finance.Account.TransportAndSelling) - selling, Is.EqualTo(200 * Coin));
            Assert.That(sim.Execute(new PostJob(t, false)).Reason, Is.EqualTo(RejectReason.AlreadyPosted));

            // Above market: skill 30–80, wage 1.10 × the asked wage; hiring ends the post.
            Applicant a = WaitForApplicant(sim);
            Assert.That(a.AboveMarket, Is.True);
            Assert.That(a.Skill, Is.InRange(30, 80));
            Hand h = HireHand(sim);
            Assert.That(h.Wage, Is.EqualTo(PeopleRules.AskedWage(sim, h.Skill, true)));
            Assert.That(h.Wage, Is.EqualTo(PeopleRules.AskedWage(sim, h.Skill, false) * 110 / 100).Within(Coin));
            Assert.That(P(sim).Post, Is.Null, "a hire ends the post");

            // Acceptance over 10,000 seeded offers: 85% at the market wage, 95% above it.
            int market = 0, above = 0;
            for (long id = 1; id <= 10_000; id++)
            {
                ulong r = Rng.Next(42, RngStream.Applicants, (ulong)id, 5);
                if (Rng.Chance(r, Fixed.Parse("0.85"))) market++;
                if (Rng.Chance(r, Fixed.Parse("0.95"))) above++;
            }
            Assert.That(market / 100.0, Is.EqualTo(85).Within(1));
            Assert.That(above / 100.0, Is.EqualTo(95).Within(1));

            // The post expires after 4 days.
            Simulation e = Start();
            BuildHut(e);
            Ok(e, new PostJob(e.State.Clock.SimTime, false));
            long expires = P(e).Post.ExpiresAt;
            Assert.That(expires - e.State.Clock.SimTime, Is.EqualTo(4 * Day));
            e.AdvanceTo(expires);
            Assert.That(P(e).Post, Is.Null);
        }

        // Spec 006 AC-19.
        [Test]
        public void PostJob_WhileBuilding()
        {
            Simulation none = Start();
            ulong hash = none.State.ComputeHash();
            Assert.That(none.Execute(new PostJob(0, false)).Reason, Is.EqualTo(RejectReason.NoHut));
            Assert.That(none.State.ComputeHash(), Is.EqualTo(hash));

            Simulation posted = Start(), walk = Start();
            Ok(posted, new BuildHandHut(0, QuartersTile));
            Ok(walk, new BuildHandHut(0, QuartersTile));
            Ok(posted, new PostJob(0, false));
            posted.AdvanceTo(24 * Hour - 1);
            Assert.That(P(posted).Applicants, Is.Empty, "no applicant before the hut is finished");
            Applicant a = WaitForApplicant(posted), b = WaitForApplicant(walk);
            Assert.That(a.ArrivedAt, Is.GreaterThanOrEqualTo(24 * Hour));
            Assert.That(a.ArrivedAt, Is.LessThan(b.ArrivedAt), "sooner than a walk-in");
        }
    }
}
