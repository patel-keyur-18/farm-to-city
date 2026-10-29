using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 015 §3.7, §3.8 and ADR 0081: HR events with their choices, renewals at the current wage, employer reputation.</summary>
    public class HrEventTests
    {
        /// <summary>Rich content with only the named event possible, at <paramref name="chance"/> a season.</summary>
        private static Simulation Only(string chanceField, string chance)
        {
            return Start(Game((f, j) =>
            {
                if (f != "people.json") return j;
                foreach (string k in new[] { "raiseChance", "poachChance", "sickChance", "emergencyChance", "badBatchChance" })
                {
                    int at = j.IndexOf("\"" + k + "\":");
                    int end = j.IndexOf(',', at);
                    j = j.Substring(0, at) + "\"" + k + "\": " + (k == chanceField ? chance : "0") + j.Substring(end);
                }
                return j;
            }));
        }

        private static Hand SteadyHand(Simulation sim)
        {
            Hand h = HireHand(sim);
            h.Trait1 = h.Trait2 = sim.Config.Game.People.IndexOfTrait("steady");
            return h;
        }

        private static void ToClose(Simulation sim) { sim.AdvanceTo(BusinessTests.NextClose(sim)); }

        private static HrEvent Single(Simulation sim, HrEventKind kind)
        {
            HrEvent e = P(sim).HrEvents.SingleOrDefault();
            Assert.That(e, Is.Not.Null, "no " + kind + " waiting");
            Assert.That(e.Kind, Is.EqualTo(kind));
            return e;
        }

        // AC-8 and ADR 0081: a contract renews at its current wage; a worker paid below what they'd ask asks for a raise at a close (50%);
        // it waits, away or not, the worker carrying on; paid, the wage goes up from now; refused, they work less keenly.
        [Test]
        public void Choices()
        {
            // A raise request, paid.
            Simulation sim = Only("raiseChance", "1");
            Hand h = SteadyHand(sim);
            long wage = h.Wage;
            h.Skill = h.Skill + Fixed.FromInt(20); // grown since: they'd ask more
            ToClose(sim);
            Assert.That(h.Wage, Is.EqualTo(wage), "renewed at the current wage");
            HrEvent raise = Single(sim, HrEventKind.RaiseRequest);
            Assert.That(raise.Amount, Is.EqualTo(HrRules.Asked(sim, h)));
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Day);
            Assert.That(P(sim).HrEvents, Has.Count.EqualTo(1), "it waits; nothing else this season");
            Assert.That(PeopleRules.HandHours(sim, h), Is.GreaterThan(Fixed.Zero), "carrying on meanwhile");
            long cash = sim.State.Ledger.Cash, now = HrRules.CostNow(sim, raise);
            Assert.That(now, Is.EqualTo(PeopleRules.RestOfSeason(sim, raise.Amount - wage)));
            Ok(sim, new AnswerHrEvent(sim.State.Clock.SimTime, raise.Id, 0));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - now), "the rest of the season's raise, now");
            Assert.That((h.Wage, HrRules.Underpaid(sim, h), WorkRules.Motivation(sim, h)), Is.EqualTo((raise.Amount, false, Fixed.One)));
            Assert.That(sim.Execute(new AnswerHrEvent(sim.State.Clock.SimTime, raise.Id, 0)).Reason, Is.EqualTo(RejectReason.UnknownEvent));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            // Refused twice: −0.1 motivation while still paid less, −2 reputation each time.
            sim = Only("raiseChance", "1");
            h = SteadyHand(sim);
            h.Skill = h.Skill + Fixed.FromInt(20);
            int rep = P(sim).Reputation;
            for (int i = 0; i < 2; i++)
            {
                ToClose(sim);
                Ok(sim, new AnswerHrEvent(sim.State.Clock.SimTime, Single(sim, HrEventKind.RaiseRequest).Id, 1));
            }
            Assert.That(h.Hr.RaiseRefusals, Is.EqualTo(2));
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One + sim.Config.Game.People.Hr.RaiseRefusedMood));
            Assert.That(P(sim).Reputation, Is.EqualTo(rep - 4), "no fair renewal while they're underpaid, −2 a refusal");

            // An offer from elsewhere: let go, they leave at the next close without severance; matched, +15% and a keener worker.
            sim = Only("poachChance", "1");
            h = SteadyHand(sim);
            h.Skill = Fixed.FromInt(70);
            h.Wage = HrRules.Asked(sim, h); // paid fairly, still poached
            ToClose(sim);
            HrEvent offer = Single(sim, HrEventKind.Poaching);
            Assert.That(offer.Amount, Is.EqualTo(Money.RoundToCoin(Money.MulRate(HrRules.Asked(sim, h), Fixed.FromRatio(115, 100)))), "15% over what their skill is worth");
            Ok(sim, new AnswerHrEvent(sim.State.Clock.SimTime, offer.Id, 1));
            Assert.That(h.Hr.Leaving, Is.True);
            ToClose(sim);
            Assert.That(P(sim).Hands, Is.Empty, "gone at the close");

            sim = Only("poachChance", "1");
            h = SteadyHand(sim);
            h.Skill = Fixed.FromInt(70);
            h.Wage = HrRules.Asked(sim, h);
            ToClose(sim);
            offer = Single(sim, HrEventKind.Poaching);
            Ok(sim, new AnswerHrEvent(sim.State.Clock.SimTime, offer.Id, 0));
            Assert.That(h.Wage, Is.EqualTo(offer.Amount));
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One + sim.Config.Game.People.Hr.MatchedMood), "matched: keener");

            // Sick leave: off at once for 2 days; paid, +2 reputation; unpaid, the days' wage back.
            foreach (int choice in new[] { 0, 1 })
            {
                sim = Only("sickChance", "1"); // a quarter a day
                h = SteadyHand(sim);
                for (int i = 0; i < 40 && P(sim).HrEvents.Count == 0; i++) sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day);
                HrEvent sick = Single(sim, HrEventKind.SickLeave);
                Assert.That(PeopleRules.HandHours(sim, h), Is.EqualTo(Fixed.Zero), "off sick");
                Assert.That(sim.Execute(new SendOnCourse(sim.State.Clock.SimTime, 0, h.Id)).Reason, Is.EqualTo(RejectReason.OffSick));
                Assert.That(sim.Execute(new SetLongDays(sim.State.Clock.SimTime, h.Id, true)).Reason, Is.EqualTo(RejectReason.OffSick));
                Assert.That(sick.Amount, Is.EqualTo(h.Wage * 2 / 4), "two of the season's four days");
                long before = sim.State.Ledger.Cash;
                rep = P(sim).Reputation;
                Ok(sim, new AnswerHrEvent(sim.State.Clock.SimTime, sick.Id, choice));
                if (choice == 0) Assert.That((P(sim).Reputation, sim.State.Ledger.Cash), Is.EqualTo((rep + 2, before)));
                else Assert.That(sim.State.Ledger.Cash, Is.EqualTo(before + sick.Amount), "unpaid: the days' wage back");
                sim.AdvanceTo(sick.At + 2 * Day + Hour);
                Assert.That(PeopleRules.HandHours(sim, h), Is.GreaterThan(Fixed.Zero), "back at work");
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            }

            // A family emergency: a quarter's wage advanced, repaid from the next renewal.
            sim = Only("emergencyChance", "1");
            h = SteadyHand(sim);
            for (int i = 0; i < 40 && P(sim).HrEvents.Count == 0; i++) sim.AdvanceTo((sim.State.Clock.SimTime / Day + 1) * Day);
            HrEvent need = Single(sim, HrEventKind.FamilyEmergency);
            Assert.That(need.Amount, Is.EqualTo(Money.RoundToCoin(h.Wage / 4)));
            long c0 = sim.State.Ledger.Cash;
            rep = P(sim).Reputation;
            Ok(sim, new AnswerHrEvent(sim.State.Clock.SimTime, need.Id, 0));
            Assert.That((sim.State.Ledger.Cash, h.Hr.Advance, P(sim).Reputation), Is.EqualTo((c0 - need.Amount, need.Amount, rep + 3)));
            long c1 = sim.State.Ledger.Cash;
            ToClose(sim);
            Assert.That(h.Hr.Advance, Is.EqualTo(0L));
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey == "ledger.advanceRepaid"), Is.True);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            // A bad batch at the mill: its atta set aside; thrown away at its cost, or sold cheap for half (−2 reputation).
            foreach (int choice in new[] { 0, 1 })
            {
                Simulation m = Only("badBatchChance", "1");
                Business.Business b = BusinessTests.Mill(m);
                m.AdvanceTo(b.ReadyAt);
                BusinessTests.GiveWheat(m, 2_000_000);
                for (int i = 0; i < 40 && P(m).HrEvents.Count == 0; i++) m.AdvanceTo((m.State.Clock.SimTime / Day + 1) * Day + 20 * Hour);
                HrEvent batch = Single(m, HrEventKind.BadBatch);
                Assert.That(batch.Grams, Is.GreaterThan(0L));
                rep = P(m).Reputation;
                long sales = b.Ledger.Balance(Account.Sales);
                Ok(m, new AnswerHrEvent(m.State.Clock.SimTime, batch.Id, choice));
                if (choice == 1) Assert.That((b.Ledger.Balance(Account.Sales), P(m).Reputation), Is.EqualTo((sales - batch.Amount, rep - 2)));
                else Assert.That(b.Ledger.Entries.Last(e => e.DescriptionKey == "ledger.badBatch").Postings.Single(p => p.Account == Account.CostOfGoodsSold).Amount, Is.EqualTo(batch.Cost));
                Assert.That(b.Ledger.Reconcile(), Is.Null);
            }

            // Never a bad batch with a perfectionist.
            Simulation q = Only("badBatchChance", "1");
            Business.Business mill = BusinessTests.Mill(q);
            q.AdvanceTo(mill.ReadyAt);
            P(q).WorkerAt(mill.Id).Trait1 = q.Config.Game.People.IndexOfTrait("perfectionist");
            BusinessTests.GiveWheat(q, 2_000_000);
            q.AdvanceTo(q.State.Clock.SimTime + 20 * Day);
            Assert.That(P(q).HrEvents, Is.Empty);
        }

        // AC-8: the chances. A raise request at half the closes for an underpaid worker; sick leave a season's 2% spread over its days,
        // doubled when tired.
        [Test]
        public void Chances()
        {
            Simulation sim = Start();
            Hand h = SteadyHand(sim);
            h.Skill = h.Skill + Fixed.FromInt(20);
            int raises = 0, n = 2000;
            for (int i = 0; i < n; i++)
            {
                h.Id = 1000 + i;
                h.Hr.EventSeason = 0;
                P(sim).HrEvents.Clear();
                HrRules.Draw(sim, h);
                raises += P(sim).HrEvents.Count(e => e.Kind == HrEventKind.RaiseRequest);
            }
            Assert.That(raises, Is.InRange(n * 0.45, n * 0.55), "50% a close");

            int sick = 0, tired = 0;
            for (int i = 0; i < 20_000; i++)
            {
                h.Id = 10_000 + i;
                h.Hr.EventSeason = 0;
                h.Hr.SickUntil = 0;
                P(sim).HrEvents.Clear();
                h.Fatigue = i % 2 == 0 ? Fixed.Zero : Fixed.FromRatio(4, 10);
                HrRules.Daily(sim);
                int k = P(sim).HrEvents.Count(e => e.Kind == HrEventKind.SickLeave);
                if (i % 2 == 0) sick += k; else tired += k;
            }
            Assert.That(sick, Is.InRange(25, 75), "0.5% a day rested (about 50 in 10,000)");
            Assert.That(tired, Is.InRange(65, 135), "1% a day over 0.3 tired (about 100)");
        }

        // AC-9: reputation's steps and effects.
        [Test]
        public void Reputation()
        {
            Simulation sim = Only("raiseChance", "0");
            Hand h = SteadyHand(sim);
            Assert.That(P(sim).Reputation, Is.EqualTo(50));
            ToClose(sim);
            Assert.That(P(sim).Reputation, Is.EqualTo(51), "renewed at what they ask: +1");
            h.Skill = h.Skill + Fixed.FromInt(20);
            ToClose(sim);
            Assert.That(P(sim).Reputation, Is.EqualTo(51), "renewed below what they ask: nothing");

            // Letting go: −1 in the first season, +1 with severance after it.
            Simulation a = Start();
            Hand first = HireHand(a);
            Ok(a, new DismissHand(a.State.Clock.SimTime, first.Id));
            Assert.That(P(a).Reputation, Is.EqualTo(49));
            Ok(sim, new DismissHand(sim.State.Clock.SimTime, h.Id));
            Assert.That(P(sim).Reputation, Is.EqualTo(52));

            // A lapse: −3.
            Simulation l = Only("raiseChance", "0");
            SteadyHand(l);
            CashTo(l, 0);
            ToClose(l);
            Assert.That(P(l).Reputation, Is.EqualTo(47));

            // Effects: at 80 or more offers are taken 10% likelier and above-usual word brings 5 more skill; at 20 or less 10% less likely
            // and applicants a third slower.
            Simulation e = Start();
            var app = new Applicant { Id = 900, Skill = 50, Place = 0 };
            Fixed asked = HiringRules.AcceptChance(e, app, OfferLevel.Asked);
            P(e).Reputation = 85;
            Assert.That(HiringRules.AcceptChance(e, app, OfferLevel.Asked), Is.EqualTo(asked + Fixed.FromRatio(1, 10)));
            P(e).Reputation = 15;
            Assert.That(HiringRules.AcceptChance(e, app, OfferLevel.Asked), Is.EqualTo(asked - Fixed.FromRatio(1, 10)));
            long slow = PeopleRules.ApplicantInterval(e, 5, false);
            P(e).Reputation = 50;
            long usual = PeopleRules.ApplicantInterval(e, 5, false);
            Assert.That(slow, Is.EqualTo((Fixed.FromInt(usual) / Fixed.FromRatio(3, 4)).RoundToLong()));

            // A hire below the asked wage: −2.
            Simulation low = Start();
            BuildHut(low);
            int before = P(low).Reputation;
            for (int tries = 0; tries < 30 && P(low).Hands.Count == 0; tries++)
            {
                Applicant x = WaitForApplicant(low);
                low.Execute(new OfferContract(low.State.Clock.SimTime, x.Id, OfferLevel.Low));
            }
            Assert.That(P(low).Hands.Single().Fairness, Is.EqualTo(-1));
            Assert.That(P(low).Reputation, Is.EqualTo(before - 2));
        }
    }
}
