using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public class ContractTests
    {
        private static int Trait(Simulation sim, string id) { return sim.Config.Game.People.IndexOfTrait(id); }

        // Spec 006 AC-8.
        [Test]
        public void Offer_Accept_Prepay()
        {
            for (ulong seed = 1; seed < 50; seed++)
            {
                Simulation sim = Start(seed: seed);
                BuildHut(sim);
                Applicant a = WaitForApplicant(sim);
                if (a.ArrivedAt > S / 2) continue;
                sim.AdvanceTo(S / 2);
                if (P(sim).Applicant(a.Id) == null) continue;
                long wage = PeopleRules.AskedWage(sim, Fixed.FromInt(a.Skill), false);
                long prepaid = sim.State.Ledger.Balance(Account.PrepaidWages);
                Ok(sim, new OfferContract(S / 2, a.Id));
                if (P(sim).Hands.Count == 0) continue; // declined: try another seed
                Assert.That(sim.State.Ledger.Balance(Account.PrepaidWages) - prepaid, Is.EqualTo(wage / 2));
                Assert.That(P(sim).Population, Is.EqualTo(1));
                return;
            }
            Assert.Fail("no seed hired at mid-season");
        }

        // Spec 006 AC-9.
        [Test]
        public void HandHours()
        {
            Simulation sim = Start();
            int steady = Trait(sim, "steady"), hard = Trait(sim, "hardworking"), lazy = Trait(sim, "lazy"), loyal = Trait(sim, "loyal");
            Hand Make(int t1) { return new Hand { Skill = Fixed.FromInt(50), Trait1 = t1, Trait2 = loyal }; }
            FarmTest.AssertNear(PeopleRules.HandHours(sim, Make(steady)), "7.6", 8);
            FarmTest.AssertNear(PeopleRules.HandHours(sim, Make(hard)), "8.36", 8);
            FarmTest.AssertNear(PeopleRules.HandHours(sim, Make(lazy)), "6.46", 8);

            Hand h = HireHand(sim);
            Assert.That(PeopleRules.HiredHours(sim), Is.EqualTo(PeopleRules.HandHours(sim, h)));
        }

        // Spec 006 AC-10 (ADR 0039: back pay on renewal after a lapse).
        [Test]
        public void Renewal_Lapse_Grace()
        {
            // Renewal after living costs, at the re-asked wage.
            Simulation sim = Start();
            Hand h = HireHand(sim);
            h.Trait1 = Trait(sim, "steady");
            h.Trait2 = Trait(sim, "hardworking");
            sim.DrainEvents();
            sim.AdvanceTo(S);
            var paid = sim.PendingEvents.OfType<ObligationPaid>().Select(e => e.Kind).ToList();
            Assert.That(paid.IndexOf("Living"), Is.LessThan(paid.IndexOf("Wages")));
            Assert.That(h.Wage, Is.EqualTo(PeopleRules.AskedWage(sim, h.Skill - Fixed.FromInt(3), false)), "the wage for the skill at the close");

            // A loyal hand keeps the old wage, even with more skill.
            Simulation l = Start(seed: 3);
            Hand loyal = HireHand(l);
            loyal.Trait1 = Trait(l, "loyal");
            loyal.Trait2 = Trait(l, "steady");
            loyal.Wage = 30_000 * Coin;
            l.AdvanceTo(S);
            Assert.That(loyal.Wage, Is.EqualTo(30_000 * Coin));

            // Short of cash at the close: the contract lapses, the hours leave the pool, the hand leaves after 2 live days.
            Simulation x = Start(seed: 5);
            Hand lap = HireHand(x);
            x.AdvanceTo(S - Hour);
            CashTo(x, 10_000 * Coin);
            x.AdvanceTo(S);
            Assert.That(lap.Contract, Is.EqualTo(ContractState.Lapsed));
            Assert.That(PeopleRules.HiredHours(x), Is.EqualTo(Fixed.Zero));
            x.AdvanceTo(S + 2 * Day - Hour);
            Assert.That(P(x).Hand(lap.Id), Is.Not.Null);
            x.AdvanceTo(S + 2 * Day);
            Assert.That(P(x).Hand(lap.Id), Is.Null, "left after the grace");

            // Renewing within the grace pays the whole season, the waiting day expensed at once, and resumes work.
            Simulation r = Start(seed: 5);
            Hand back = HireHand(r);
            r.AdvanceTo(S - Hour);
            CashTo(r, 10_000 * Coin);
            r.AdvanceTo(S + Day);
            CashTo(r, 200_000 * Coin);
            long cash = r.State.Ledger.Cash, labour = r.State.Ledger.Balance(Account.HiredLabour);
            Ok(r, new RenewContract(S + Day, back.Id));
            Assert.That(cash - r.State.Ledger.Cash, Is.EqualTo(back.Wage));
            Assert.That(r.State.Ledger.Balance(Account.HiredLabour) - labour, Is.EqualTo(back.Wage / 4));
            Assert.That(back.Contract, Is.EqualTo(ContractState.Active));
            Assert.That(PeopleRules.HiredHours(r), Is.EqualTo(PeopleRules.HandHours(r, back)));

            // Still lapsed at the next close: the contract ends.
            Simulation n = Start(seed: 5);
            Hand gone = HireHand(n);
            n.AdvanceTo(S - Hour);
            CashTo(n, 10_000 * Coin);
            Ok(n, new AdvanceTo(2 * S - Hour, Time.AdvanceMode.Away)); // the grace doesn't run while away
            Assert.That(P(n).Hand(gone.Id), Is.Not.Null);
            n.AdvanceTo(2 * S);
            Assert.That(P(n).Hand(gone.Id), Is.Null);
        }

        // Spec 006 AC-11.
        [Test]
        public void Dismiss_RefundAndSeverance()
        {
            // First season (probation): the whole unearned wage back.
            Simulation sim = Start();
            Hand h = HireHand(sim);
            long t = sim.State.Clock.SimTime;
            long refund = Accrual.RefundIfClosed(sim, h.PrepaymentId);
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new DismissHand(t, h.Id));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(refund));
            Assert.That(Last<HandDismissed>(sim).Severance, Is.EqualTo(0));
            Assert.That(P(sim).Population, Is.EqualTo(0));

            // After the first close: 10% of the wage, out of the refund.
            Simulation s2 = Start();
            Hand h2 = HireHand(s2);
            s2.AdvanceTo(S + Day);
            refund = Accrual.RefundIfClosed(s2, h2.PrepaymentId);
            cash = s2.State.Ledger.Cash;
            Ok(s2, new DismissHand(S + Day, h2.Id));
            Assert.That(Last<HandDismissed>(s2).Severance, Is.EqualTo(h2.Wage / 10));
            Assert.That(s2.State.Ledger.Cash - cash, Is.EqualTo(refund - h2.Wage / 10));

            // Severance never exceeds the refund (late in the season).
            Simulation s3 = Start();
            Hand h3 = HireHand(s3);
            s3.AdvanceTo(2 * S - Hour);
            refund = Accrual.RefundIfClosed(s3, h3.PrepaymentId);
            Assert.That(refund, Is.LessThan(h3.Wage / 10));
            cash = s3.State.Ledger.Cash;
            Ok(s3, new DismissHand(2 * S - Hour, h3.Id));
            Assert.That(Last<HandDismissed>(s3).Severance, Is.EqualTo(refund));
            Assert.That(s3.State.Ledger.Cash, Is.EqualTo(cash));
        }

        // Spec 006 AC-12.
        [Test]
        public void SkillGrowth_AndWage()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            h.Trait1 = Trait(sim, "steady");
            h.Trait2 = Trait(sim, "hardworking");
            Fixed skill = h.Skill;
            sim.AdvanceTo(S);
            Assert.That(h.Skill, Is.EqualTo(skill + Fixed.FromInt(3)));
            sim.AdvanceTo(2 * S);
            Assert.That(h.Wage, Is.EqualTo(PeopleRules.AskedWage(sim, skill + Fixed.FromInt(3), false)), "the next renewal asks for the new skill");

            Simulation a = Start(seed: 2);
            Hand amb = HireHand(a);
            amb.Trait1 = Trait(a, "ambitious");
            amb.Trait2 = Trait(a, "steady");
            amb.Skill = Fixed.FromInt(98);
            Fixed before = amb.Skill;
            a.AdvanceTo(S);
            Assert.That(amb.Skill, Is.EqualTo(Fixed.FromInt(100)), "capped");
            amb.Skill = Fixed.FromInt(40);
            a.AdvanceTo(2 * S);
            Assert.That(amb.Skill, Is.EqualTo(Fixed.Parse("44.5")));
        }
    }
}
