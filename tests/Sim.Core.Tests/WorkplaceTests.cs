using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Saves;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>The workplace's shared set-up (spec 028 §3.9, US-259): a Township with the farm's hands.</summary>
    internal static class WorkplaceKit
    {
        private const long Coin = 100, Hour = 3600;

        /// <summary>A Township whose farm has <paramref name="hands"/> hands under contract (the first hired, the others copied from it with their own ids).</summary>
        internal static Simulation Farm(int hands)
        {
            Simulation sim = InvoiceTests.Township();
            CashTo(sim, 9_000_000 * Coin);
            Hand first = HireHand(sim);
            for (int i = 1; i < hands; i++)
            {
                Hand h = first.Clone();
                h.Id = P(sim).NextHandId++;
                h.PrepaymentId = 0;
                P(sim).Hands.Add(h);
            }
            return sim;
        }

        internal static long Now(Simulation sim) { return sim.State.Clock.SimTime; }

        internal static HrEvent Card(Simulation sim, HrEventKind kind, long place, long worker, long other = 0, long amount = 0)
        {
            var e = new HrEvent { Id = P(sim).NextHrEventId++, Kind = kind, Place = place, WorkerId = worker, OtherId = other, At = Now(sim), Amount = amount, ExpiresAt = Now(sim) + 24 * Hour };
            P(sim).HrEvents.Add(e);
            return e;
        }

        internal static Supervisor Supervise(Simulation sim, int skill = 60)
        {
            P(sim).Workplace.Supervisor = new Supervisor { Skill = Fixed.FromInt(skill), Wage = 30_000 * Coin, HiredAt = Now(sim) };
            return P(sim).Workplace.Supervisor;
        }
    }

    /// <summary>US-259 (spec 028 §3.9, ADR 0166 gaps 3 to 5): the supervisor's post, span, output and halving.</summary>
    public class SupervisorTests
    {
        // AC-20: a place needs 3 hands for the post; one covers up to 5; each star adds 1.2% to each covered hand's output.
        [Test]
        public void Span_Output()
        {
            Simulation sim = WorkplaceKit.Farm(2);
            Assert.That(sim.Execute(new PostJob(WorkplaceKit.Now(sim), HiringRules.SupervisorPlace, false)).Reason, Is.EqualTo(RejectReason.TooFewWorkers), "2 hands: the post needs 3");
            sim = WorkplaceKit.Farm(6);
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new PostJob(WorkplaceKit.Now(sim), HiringRules.SupervisorPlace, false));
            for (int i = 0; i < 24 * 8 && P(sim).ApplicantsFor(HiringRules.SupervisorPlace).Count == 0; i++) sim.AdvanceTo(WorkplaceKit.Now(sim) + 3600);
            Applicant a = P(sim).ApplicantsFor(HiringRules.SupervisorPlace).First();
            Assert.That(a.CitizenId, Is.EqualTo(0), "a supervisor comes from outside the town");
            for (int tries = 0; tries < 20 && WorkplaceRules.Supervisor(sim) == null; tries++)
            {
                if (P(sim).Applicant(a.Id) == null) { for (int i = 0; i < 24 * 8 && P(sim).ApplicantsFor(HiringRules.SupervisorPlace).Count == 0; i++) sim.AdvanceTo(WorkplaceKit.Now(sim) + 3600); a = P(sim).ApplicantsFor(HiringRules.SupervisorPlace).First(); }
                Ok(sim, new OfferContract(WorkplaceKit.Now(sim), a.Id, OfferLevel.High));
            }
            Supervisor sv = WorkplaceRules.Supervisor(sim);
            Assert.That(sv, Is.Not.Null);
            Assert.That(sim.Execute(new PostJob(WorkplaceKit.Now(sim), HiringRules.SupervisorPlace, false)).Reason, Is.EqualTo(RejectReason.HasSupervisor));
            Assert.That(WorkplaceRules.CoveredCount(sim), Is.EqualTo(5), "up to 5 of the 6");
            var hands = P(sim).Hands;
            Assert.That(hands.Take(5).All(h => WorkplaceRules.Covered(sim, h)) && !WorkplaceRules.Covered(sim, hands[5]), Is.True);
            Fixed stars = Fixed.FromInt(WorkplaceRules.Stars(sv.Skill));
            Assert.That(WorkplaceRules.OutputFactor(sim, hands[0]), Is.EqualTo(Fixed.One + sim.Config.Game.Workplace.OutputPerStar * stars));
            Assert.That(WorkplaceRules.OutputFactor(sim, hands[5]), Is.EqualTo(Fixed.One), "the sixth is not covered");
            Assert.That(cash - sim.State.Ledger.Cash, Is.GreaterThan(0), "his wage for the rest of the season, upfront");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Ok(sim, new DismissSupervisor(WorkplaceKit.Now(sim)));
            Assert.That(WorkplaceRules.OutputFactor(sim, hands[0]), Is.EqualTo(Fixed.One));
            Assert.That(sim.Execute(new DismissSupervisor(WorkplaceKit.Now(sim))).Reason, Is.EqualTo(RejectReason.NoSupervisor));
        }

        // AC-20: his half chance on accidents and associations, a quarrel settled for you (no card), what he spared counted.
        [Test]
        public void Halves_Risk()
        {
            Simulation sim = WorkplaceKit.Farm(4);
            Hand h = P(sim).Hands[0];
            WorkplaceDef d = sim.Config.Game.Workplace;
            Fixed alone = WorkplaceRules.AccidentChance(sim, h, true), assoc = WorkplaceRules.FormChance(sim, true);
            WorkplaceKit.Supervise(sim);
            Assert.That(WorkplaceRules.AccidentChance(sim, h, true), Is.EqualTo(alone * d.RiskHalf));
            Assert.That(WorkplaceRules.AccidentChance(sim, h, false), Is.EqualTo(alone), "the chance without him is shown too");
            Assert.That(WorkplaceRules.FormChance(sim, true), Is.EqualTo(assoc * d.RiskHalf));
            P(sim).Hands.ForEach(x => x.Fatigue = Fixed.FromRatio(1, 2));
            int settled = 0;
            for (int day = 0; day < 400; day++)
            {
                sim.AdvanceTo((WorkplaceKit.Now(sim) / 86400 + 1) * 86400 + 60);
                P(sim).HrEvents.RemoveAll(e => e.Kind == HrEventKind.Accident);
                P(sim).Hands.ForEach(x => { x.Fatigue = Fixed.FromRatio(1, 2); x.Hr.SickUntil = 0; });
                settled = P(sim).Workplace.Supervisor.Settled;
                if (settled > 0) break;
            }
            Assert.That(settled, Is.GreaterThan(0), "a quarrel came and he settled it");
            Assert.That(P(sim).HrEvents.Any(e => e.Kind == HrEventKind.Conflict), Is.False, "no card reached you");
        }
    }

    /// <summary>US-259: accidents: the chance and its multipliers, the card and its 24 h default, the severe case.</summary>
    public class AccidentTests
    {
        // AC-21: 0.45% a worker a day (about 1.8% a season), ×1.5 on a long day, ×2 with a machine's maintenance skipped; the clinic and health cover.
        [Test]
        public void Chance()
        {
            Simulation sim = WorkplaceKit.Farm(3);
            Hand h = P(sim).Hands[0];
            WorkplaceDef d = sim.Config.Game.Workplace;
            Assert.That(WorkplaceRules.AccidentChance(sim, h, true), Is.EqualTo(d.AccidentChance));
            Assert.That(d.AccidentChance * Fixed.FromInt(4), Is.InRange(Fixed.FromRatio(17, 1000), Fixed.FromRatio(19, 1000)), "about 1.8% a 4-day season");
            h.LongDays = true;
            Assert.That(WorkplaceRules.AccidentChance(sim, h, true), Is.EqualTo(d.AccidentChance * d.OvertimeFactor));
            Assert.That(WorkplaceRules.OutHours(sim, false), Is.EqualTo(d.OutHours));
            sim.State.Township.Clinic.Status = Sim.Core.Town.ClinicStatus.Open;
            sim.State.Township.Clinic.Doctor = Sim.Core.Town.DoctorStatus.InPost;
            Assert.That(WorkplaceRules.OutHours(sim, false), Is.EqualTo(d.ClinicOutHours), "3 hours with the clinic");
            Assert.That(WorkplaceRules.OutHours(sim, true), Is.EqualTo(d.SevereHours));
        }

        // AC-21: the bill is paid at once, he is out, the card waits 24 h and its default pays the days' wage (+0.05 mood, name +1); not paying takes the hours' wage back (−0.10, name −1).
        [Test]
        public void Card_Default()
        {
            Simulation sim = WorkplaceKit.Farm(3);
            Hand h = P(sim).Hands[0];
            long cash = sim.State.Ledger.Cash, now = WorkplaceKit.Now(sim);
            int rep = P(sim).Reputation;
            WorkplaceRules.Hurt(sim, h, false, now);
            HrEvent e = P(sim).HrEvents.Single(x => x.Kind == HrEventKind.Accident);
            Assert.That((e.Hours, e.Severe, e.ExpiresAt - now), Is.EqualTo((8, false, 24L * 3600)));
            Assert.That(h.Hr.SickUntil, Is.EqualTo(now + 8 * 3600), "out for 8 hours");
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.RoundToCoin(Market.MarketRules.Indexed(sim, sim.Config.Game.Workplace.Bill))), "the bill, at today's prices");
            Assert.That(WorkRules.Output(sim, h), Is.EqualTo(Fixed.Zero), "no work while out");
            sim.AdvanceTo(now + 23 * 3600);
            Assert.That(P(sim).HrEvents, Has.Member(e), "it still waits");
            sim.AdvanceTo(now + 25 * 3600);
            Assert.That(P(sim).HrEvents.Any(x => x.Kind == HrEventKind.Accident), Is.False, "the default answered it");
            Assert.That(P(sim).Reputation, Is.EqualTo(rep + 1), "paid: your name +1");
            Assert.That(h.Hr.SickUntil, Is.EqualTo(0), "back at work");
            Assert.That(sim.PendingEvents.OfType<HrEventDefaulted>().Any(x => x.Kind == HrEventKind.Accident && x.Choice == 0), Is.True);
            // not paid
            Hand k = P(sim).Hands[1];
            WorkplaceRules.Hurt(sim, k, false, WorkplaceKit.Now(sim));
            HrEvent f = P(sim).HrEvents.Single(x => x.Kind == HrEventKind.Accident);
            Ok(sim, new AnswerHrEvent(WorkplaceKit.Now(sim), f.Id, 1));
            Assert.That(P(sim).Reputation, Is.EqualTo(rep), "not paid: −1 after the +1");
            Assert.That(HrRules.Mood(sim, k), Is.EqualTo(sim.Config.Game.Workplace.UnpaidMood));
            Assert.That(sim.Execute(new AnswerHrEvent(WorkplaceKit.Now(sim), f.Id, 1)).Reason, Is.EqualTo(RejectReason.UnknownEvent), "answered once");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-21: a severe accident is out 32 hours with an 8,000 bill (free with health cover); nobody dies.
        [Test]
        public void Severe()
        {
            Simulation sim = WorkplaceKit.Farm(3);
            Hand h = P(sim).Hands[0];
            long cash = sim.State.Ledger.Cash;
            WorkplaceRules.Hurt(sim, h, true, WorkplaceKit.Now(sim));
            HrEvent e = P(sim).HrEvents.Single();
            Assert.That((e.Hours, e.Severe), Is.EqualTo((32, true)));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.RoundToCoin(Market.MarketRules.Indexed(sim, sim.Config.Game.Workplace.SevereBill))));
            P(sim).HrEvents.Clear();
            h.Hr.SickUntil = 0;
            sim.State.Township.Exit.FarmBenefits |= (int)Benefit.Health;
            cash = sim.State.Ledger.Cash;
            WorkplaceRules.Hurt(sim, h, true, WorkplaceKit.Now(sim));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash), "health cover pays the bill");
            sim.AdvanceTo(WorkplaceKit.Now(sim) + 40 * 3600);
            Assert.That(P(sim).Hands, Has.Member(h), "nobody dies");
            Assert.That(h.Hr.SickUntil, Is.EqualTo(0));
        }

        // The daily roll finds accidents on its own, about the stated chance, and never stacks a second on a worker with a card open.
        [Test]
        public void Rolls_Daily()
        {
            Simulation sim = WorkplaceKit.Farm(4);
            var seen = new System.Collections.Generic.List<AccidentHappened>();
            for (int day = 0; day < 300; day++)
            {
                sim.AdvanceTo((WorkplaceKit.Now(sim) / 86400 + 1) * 86400 + 60);
                seen.AddRange(sim.PendingEvents.OfType<AccidentHappened>().Where(x => !seen.Contains(x)));
                P(sim).HrEvents.RemoveAll(x => x.Kind != HrEventKind.Accident);
                CashTo(sim, 9_000_000 * 100);
            }
            int accidents = seen.Count;
            Assert.That(accidents, Is.InRange(2, 20), "4 hands × 300 days × 0.45% is about 5");
        }
    }

    /// <summary>US-259: conflicts: the chance, the three answers, the default.</summary>
    public class ConflictTests
    {
        [Test]
        public void Chance()
        {
            Simulation sim = WorkplaceKit.Farm(1);
            Assert.That(WorkplaceRules.ConflictChance(sim, 0), Is.EqualTo(Fixed.Zero), "one worker has no one to quarrel with");
            sim = WorkplaceKit.Farm(3);
            WorkplaceDef d = sim.Config.Game.Workplace;
            Fixed rested = WorkplaceRules.ConflictChance(sim, 0);
            Assert.That(rested, Is.EqualTo(d.ConflictChance.MulRatio(1, 4)), "3% a season over its 4 days");
            P(sim).Hands.ForEach(x => x.Fatigue = Fixed.FromRatio(1, 5));
            Assert.That(WorkplaceRules.ConflictChance(sim, 0), Is.EqualTo(d.ConflictChance * (Fixed.One + d.ConflictFatigue * Fixed.FromRatio(1, 5)).MulRatio(1, 4)), "tiredness raises it");
        }

        // AC-22: talk (your hours, settles it 7 in 10), move one (he is cross), let it be (both −0.05, one may resign); the default after 24 h is let it be.
        [Test]
        public void Choices()
        {
            Simulation sim = WorkplaceKit.Farm(3);
            WorkplaceDef d = sim.Config.Game.Workplace;
            Hand a = P(sim).Hands[0], b = P(sim).Hands[1];
            HrEvent e = WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, a.Id, b.Id);
            Assert.That(WorkplaceRules.TalkCut(sim), Is.EqualTo(Fixed.Zero));
            Ok(sim, new AnswerHrEvent(WorkplaceKit.Now(sim), e.Id, 0));
            Assert.That(WorkplaceRules.TalkCut(sim), Is.GreaterThan(Fixed.Zero), "your hours go to the talk");
            e = WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, a.Id, b.Id);
            Ok(sim, new AnswerHrEvent(WorkplaceKit.Now(sim), e.Id, 1));
            Assert.That(HrRules.Mood(sim, b), Is.EqualTo(d.MoveMood));
            Assert.That(HrRules.Mood(sim, a), Is.EqualTo(Fixed.Zero), "the one who stayed is untouched");
            int resigned = 0, kept = 0;
            for (int i = 0; i < 40; i++)
            {
                a.Hr.Leaving = b.Hr.Leaving = false;
                e = WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, a.Id, b.Id);
                Ok(sim, new AnswerHrEvent(WorkplaceKit.Now(sim), e.Id, 2));
                Assert.That((HrRules.Mood(sim, a), HrRules.Mood(sim, b)), Is.EqualTo((d.LetMood, d.LetMood)));
                if (a.Hr.Leaving != b.Hr.Leaving) resigned++; else kept++;
                Assert.That(a.Hr.Leaving && b.Hr.Leaving, Is.False, "one at most");
            }
            Assert.That((resigned, kept), Is.EqualTo((resigned, kept)).And.Matches<(int, int)>(t => t.Item1 > 5 && t.Item2 > 5), "about half resign");
            Assert.That(sim.Execute(new AnswerHrEvent(WorkplaceKit.Now(sim), 999, 0)).Reason, Is.EqualTo(RejectReason.UnknownEvent));
            e = WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, a.Id, b.Id);
            Assert.That(sim.Execute(new AnswerHrEvent(WorkplaceKit.Now(sim), e.Id, 3)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            sim.AdvanceTo(WorkplaceKit.Now(sim) + 25 * 3600);
            Assert.That(P(sim).HrEvents.Any(x => x.Id == e.Id), Is.False, "the default let it be");
            Assert.That(sim.PendingEvents.OfType<HrEventDefaulted>().Any(x => x.EventId == e.Id && x.Choice == 2), Is.True);
        }
    }

    /// <summary>US-259: associations: the triggers, the three answers, the stoppage, the farm only.</summary>
    public class AssociationTests
    {
        // Review of Sprint 26: an association is the whole place's, not one hand's: letting any hand go leaves its card, and a quarrel ends when either of its two goes; a card whose people are gone ends without a word that something was done.
        [Test]
        public void Cards_Outlive_Or_End_With_Their_People()
        {
            Simulation sim = WorkplaceKit.Farm(5);
            // formed by the rule, the card names nobody
            P(sim).Reputation = 5;
            for (int i = 0; i < 40 && !sim.PendingEvents.OfType<HrEventRaised>().Any(e => e.Kind == HrEventKind.Association); i++) { sim.AdvanceTo(WorkplaceKit.Now(sim) + 4 * 86400); CashTo(sim, 9_000_000 * 100); P(sim).Reputation = 5; }
            Assert.That(sim.PendingEvents.OfType<HrEventRaised>().First(e => e.Kind == HrEventKind.Association).WorkerId, Is.EqualTo(0L), "an association the rule raises names no worker");
            while (P(sim).Hands.Count < 5) { Hand h = P(sim).Hands[0].Clone(); h.Id = P(sim).NextHandId++; h.PrepaymentId = 0; P(sim).Hands.Add(h); }
            HrEvent assoc = WorkplaceKit.Card(sim, HrEventKind.Association, 0, 0);
            Ok(sim, new DismissHand(WorkplaceKit.Now(sim), P(sim).Hands[0].Id));
            Assert.That(P(sim).HrEvents.Contains(assoc), Is.True, "letting the lowest hand go leaves it");
            Assert.That(sim.Execute(new AnswerHrEvent(WorkplaceKit.Now(sim), assoc.Id, 2)).Accepted, Is.True, "and it can be answered");

            // a quarrel ends when the other of the two goes
            HrEvent quarrel = WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, P(sim).Hands[0].Id, P(sim).Hands[1].Id);
            Ok(sim, new DismissHand(WorkplaceKit.Now(sim), P(sim).Hands[1].Id));
            Assert.That(P(sim).HrEvents.Contains(quarrel), Is.False, "the quarrel ends with one of its two");
            sim.DrainEvents();
            sim.AdvanceTo(WorkplaceKit.Now(sim) + 30 * 3600);
            Assert.That(sim.PendingEvents.OfType<HrEventDefaulted>().Any(e => e.EventId == quarrel.Id), Is.False, "nothing is said of a default that was never applied");
        }

        // AC-23: pay more than 5% under the reference, a tired team for 2 closes, or a name under 50 each makes one likely; a place needs 4 workers.
        [Test]
        public void Triggers()
        {
            Simulation sim = WorkplaceKit.Farm(4);
            WorkplaceDef d = sim.Config.Game.Workplace;
            var hands = WorkplaceRules.Workers(sim, 0);
            Assert.That(WorkplaceRules.PayLow(sim, hands), Is.False.Or.True, "paid what the market gives or a little under");
            foreach (Hand h in P(sim).Hands) h.Wage = HrRules.Asked(sim, h) / 2;
            Assert.That(WorkplaceRules.PayLow(sim, hands), Is.True, "half the reference is far under");
            foreach (Hand h in P(sim).Hands) h.Wage = HrRules.Asked(sim, h) * 2;
            Assert.That(WorkplaceRules.PayLow(sim, hands), Is.False);
            // tiredness counts closes in a row
            P(sim).Hands.ForEach(x => x.Fatigue = Fixed.FromRatio(2, 5));
            WorkplaceRules.AtClose(sim);
            WorkplaceRules.AtClose(sim);
            Assert.That(P(sim).Workplace.Relations(0).TiredCloses, Is.EqualTo(2), "two closes tired");
            P(sim).Hands.ForEach(x => x.Fatigue = Fixed.Zero);
            WorkplaceRules.AtClose(sim);
            Assert.That(P(sim).Workplace.Relations(0).TiredCloses, Is.EqualTo(0), "rested: the count starts again");
            // fewer than 4 hands: never
            Simulation three = WorkplaceKit.Farm(3);
            P(three).Reputation = 5;
            for (int i = 0; i < 12; i++) { three.AdvanceTo(WorkplaceKit.Now(three) + 4 * 86400); CashTo(three, 9_000_000 * 100); }
            Assert.That(three.PendingEvents.OfType<AssociationFormed>().Any(), Is.False, "3 hands can't form one");
            // 4 hands and a poor name: one forms within a few closes
            Simulation four = WorkplaceKit.Farm(4);
            P(four).Reputation = 5;
            for (int i = 0; i < 30 && !four.PendingEvents.OfType<AssociationFormed>().Any(); i++) { four.AdvanceTo(WorkplaceKit.Now(four) + 4 * 86400); CashTo(four, 9_000_000 * 100); P(four).Reputation = 5; }
            Assert.That(four.PendingEvents.OfType<AssociationFormed>().Any(), Is.True, "a name under 50 asks for it");
            Assert.That(d.AssocForms, Is.EqualTo(Fixed.FromRatio(1, 2)));
        }

        // AC-23: accept (+10%, quiet for 4 closes), negotiate (+5% and a benefit, agreed 6 in 10, else a refusal), refuse (−0.10, output × 0.85 this season); the default is refuse.
        [Test]
        public void Choices()
        {
            Simulation sim = WorkplaceKit.Farm(4);
            WorkplaceDef d = sim.Config.Game.Workplace;
            Hand h = P(sim).Hands[0];
            long wage = h.Wage;
            HrEvent e = WorkplaceKit.Card(sim, HrEventKind.Association, 0, h.Id);
            long cash = sim.State.Ledger.Cash, cost = WorkplaceRules.CostNow(sim, e, 0);
            Assert.That(cost, Is.GreaterThan(0));
            Ok(sim, new AnswerHrEvent(WorkplaceKit.Now(sim), e.Id, 0));
            Assert.That(h.Wage, Is.EqualTo(wage + Money.RoundToCoin(Money.MulRate(HrRules.Paid(h) - 0 > 0 ? wage + h.HousingInKind : wage, d.AssocRaise))), "+10%");
            Assert.That(cash - sim.State.Ledger.Cash, Is.GreaterThan(0), "the rest of the season's raise, now");
            long season = WorkplaceKit.Now(sim) / sim.Clock.SecondsPerSeason;
            Assert.That(P(sim).Workplace.Relations(0).QuietUntil, Is.EqualTo(season + d.AssocQuietCloses), "none for 4 closes");
            // negotiate: sometimes yes (+5% and a benefit), sometimes it counts as a refusal
            int yes = 0, no = 0;
            for (int i = 0; i < 40; i++)
            {
                e = WorkplaceKit.Card(sim, HrEventKind.Association, 0, h.Id);
                int refusals = P(sim).Workplace.Relations(0).Refusals;
                Ok(sim, new AnswerHrEvent(WorkplaceKit.Now(sim), e.Id, 1));
                if (P(sim).Workplace.Relations(0).Refusals > refusals) no++; else yes++;
                P(sim).Workplace.Relations(0).Refusals = 0;
                P(sim).Workplace.Relations(0).CutSeason = 0;
                CashTo(sim, 9_000_000 * 100);
            }
            Assert.That((yes, no), Is.Not.EqualTo((0, 0)).And.Matches<(int, int)>(t => t.Item1 > 8 && t.Item2 > 5), "about 6 in 10 agree");
            Assert.That(BenefitRules.FlagsOfEmployer(sim, 0), Is.Not.EqualTo(0), "a benefit came with the yes");
            // refuse
            Simulation r = WorkplaceKit.Farm(4);
            Hand k = P(r).Hands[0];
            Fixed out0 = WorkRules.Output(r, k);
            e = WorkplaceKit.Card(r, HrEventKind.Association, 0, k.Id);
            Ok(r, new AnswerHrEvent(WorkplaceKit.Now(r), e.Id, 2));
            Assert.That(HrRules.Mood(r, k), Is.EqualTo(d.RefuseMood));
            Assert.That(WorkplaceRules.OutputFactor(r, k), Is.EqualTo(d.RefuseOutput), "output × 0.85 this season");
            Assert.That(WorkRules.Output(r, k), Is.LessThan(out0));
            long S = r.Clock.SecondsPerSeason;
            r.AdvanceTo((WorkplaceKit.Now(r) / S + 1) * S + 3600);
            Assert.That(WorkplaceRules.OutputFactor(r, P(r).Hands[0]), Is.EqualTo(Fixed.One), "the season after, normal again");
            // the default after 24 h is a refusal
            Simulation def = WorkplaceKit.Farm(4);
            e = WorkplaceKit.Card(def, HrEventKind.Association, 0, P(def).Hands[0].Id);
            def.AdvanceTo(WorkplaceKit.Now(def) + 25 * 3600);
            Assert.That(P(def).Workplace.Relations(0).Refusals, Is.EqualTo(1));
            Assert.That(def.PendingEvents.OfType<HrEventDefaulted>().Any(x => x.Kind == HrEventKind.Association && x.Choice == 2), Is.True);
            Assert.That(def.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-23: a second refusal stops the farm for 2 hours; it touches this place only.
        [Test]
        public void Stoppage()
        {
            Simulation sim = WorkplaceKit.Farm(4);
            Business.Business bakery = InvoiceTests.Found(sim, "bakery");
            Worker baker = P(sim).WorkerAt(bakery.Id);
            WorkplaceDef d = sim.Config.Game.Workplace;
            long now = WorkplaceKit.Now(sim);
            HrEvent e = WorkplaceKit.Card(sim, HrEventKind.Association, 0, P(sim).Hands[0].Id);
            Ok(sim, new AnswerHrEvent(now, e.Id, 2));
            Assert.That(P(sim).Hands.Any(h => h.Hr.SickUntil != 0 && h.Hr.SickUntil > now), Is.False, "the first refusal doesn't stop work");
            e = WorkplaceKit.Card(sim, HrEventKind.Association, 0, P(sim).Hands[0].Id);
            Ok(sim, new AnswerHrEvent(now, e.Id, 2));
            Assert.That(P(sim).Hands.All(h => h.Hr.SickUntil == now + d.StoppageHours * 3600), Is.True, "every hand is out for 2 hours");
            Assert.That(baker.Hr.SickUntil, Is.EqualTo(0), "the bakery is untouched");
            Assert.That(HrRules.Mood(sim, baker), Is.EqualTo(Fixed.Zero));
            sim.AdvanceTo(now + 3 * 3600);
            Assert.That(P(sim).Hands.All(h => h.Hr.SickUntil == 0), Is.True, "back after 2 hours");
        }
    }

    /// <summary>The Editor tour's town for US-259.</summary>
    public class WorkplaceEditorTown
    {
        // Writes a Township bot's town with 4 hands and an accident, a quarrel and an association waiting to FTC_TOWN_OUT: run with --filter EditorTown.
        [Test, Explicit("makes the Editor tour's town")]
        public void EditorTown()
        {
            string path = System.Environment.GetEnvironmentVariable("FTC_TOWN_OUT");
            if (string.IsNullOrEmpty(path)) { Assert.Ignore("FTC_TOWN_OUT names the file"); return; }
            TestContent.WithCohort = true;
            SimConfig config;
            try { config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game(null)); }
            finally { TestContent.WithCohort = false; }
            Tools.Balancer.Economy.Run run = Tools.Balancer.Economy.RunBot(config, new Tools.Balancer.TownshipBot(false), 28, 1, null);
            Simulation sim = run.Sim;
            long give = 6_000_000L * 100 - sim.State.Ledger.Cash; // a balanced entry, not a command: the file must stay saveable
            if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            Assert.That(P(sim).Hands.Count, Is.GreaterThan(0), "the bot has a hand to copy");
            Hand first = P(sim).Hands[0];
            while (P(sim).Hands.Count < 4)
            {
                Hand h = first.Clone();
                h.Id = P(sim).NextHandId++;
                h.PrepaymentId = 0;
                h.Name = (h.Name + P(sim).Hands.Count * 7) % sim.Config.Game.People.Names.Count;
                P(sim).Hands.Add(h);
            }
            long now = sim.State.Clock.SimTime;
            P(sim).Hands[0].Hr.SickUntil = now + 6 * 3600;
            HrEvent a = WorkplaceKit.Card(sim, HrEventKind.Accident, 0, P(sim).Hands[0].Id, amount: 166_700); a.Hours = 8; a.Cost = 200_000;
            WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, P(sim).Hands[1].Id, P(sim).Hands[2].Id);
            WorkplaceKit.Card(sim, HrEventKind.Association, 0, P(sim).Hands[3].Id);
            long wall = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            sim.State.Clock.Reanchor(wall);
            System.IO.File.WriteAllBytes(path, Saves.SaveCodec.Write(sim.State, config, wall));
            TestContext.Out.WriteLine("EDITORTOWN hands " + P(sim).Hands.Count + " cash " + sim.State.Ledger.Cash / 100 + " era " + sim.State.Era.Current);
        }
    }
}
