using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 015's hiring board: posts and who applies, reference checks, negotiation, a business's worker.</summary>
    public partial class HiringTests
    {
        private static long CeilHour(long t) { return (t + Hour - 1) / Hour * Hour; }

        /// <summary>A family house on the acre beside the mill's with two working adults: crafting 60 and 30, farming 40 and 55.</summary>
        private static Household Townsfolk(Simulation sim)
        {
            BusinessTests.Hamlet(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildFamilyHouse(t, HomeTile + 1));
            sim.AdvanceTo(t + 48 * Hour);
            Building house = sim.State.Land.Buildings.Last(b => HouseRules.IsHouse(sim, b));
            Household x = AddFamily(sim, house.Id, 40, 35);
            Citizen a = sim.State.Citizens.Citizen(x.Members[0]), b = sim.State.Citizens.Citizen(x.Members[1]);
            a.Crafting = Fixed.FromInt(60);
            a.Skill = Fixed.FromInt(40);
            b.Crafting = Fixed.FromInt(30);
            b.Skill = Fixed.FromInt(55);
            a.Trait1 = 0;
            a.Trait2 = 5;
            b.Trait1 = 2;
            b.Trait2 = 5;
            b.Name = 1;
            x.Happiness = Fixed.FromInt(80);
            return x;
        }

        // AC-1: a post brings applicants for its role every 12 ± 6 h; townspeople without a town job apply at once with skill and traits known.
        [Test]
        public void Board_Applicants()
        {
            Simulation sim = Start();
            Household x = Townsfolk(sim);
            Business.Business mill = BusinessTests.Mill(sim, hire: false);
            long t = sim.State.Clock.SimTime;
            sim.DrainEvents();
            Ok(sim, new PostJob(t, mill.Id, false));
            long due = P(sim).PostAt(mill.Id).NextApplicantAt;
            Assert.That(due, Is.EqualTo(t + PeopleRules.ApplicantInterval(sim, P(sim).NextApplicantId, false)), "the next migrant: 12 ± 6 h at the market wage");
            Assert.That(sim.Execute(new PostJob(t, mill.Id, true)).Reason, Is.EqualTo(RejectReason.AlreadyPosted));
            Assert.That(sim.Execute(new PostJob(t, 99, false)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));

            // Your townspeople, most skilled in the role (crafting for a miller) first, known exactly.
            var town = P(sim).ApplicantsFor(mill.Id);
            Assert.That(town.Select(a => a.CitizenId), Is.EqualTo(new[] { x.Members[0], x.Members[1] }));
            Assert.That(town.Select(a => a.Skill), Is.EqualTo(new[] { 60, 30 }), "a miller's skill is crafting");
            Assert.That(town.All(a => a.SkillKnown && a.SecondTraitKnown), Is.True);
            PeopleRules.SkillRange(sim, town[0], out int lo, out int hi);
            Assert.That((lo, hi), Is.EqualTo((60, 60)));

            // A farm hand's post reads farming: the same two, the other way round; no hut needed for townspeople.
            Ok(sim, new PostJob(t, 0, false));
            Assert.That(P(sim).ApplicantsFor(0).Select(a => a.Skill), Is.EqualTo(new[] { 55, 40 }));

            // A migrant, commuting from a neighbouring village, at the seeded pace (12 ± 6 h at the market wage), until 3 wait.
            long first = CeilHour(due);
            Assert.That(first - t, Is.InRange(6 * Hour, 19 * Hour));
            sim.AdvanceTo(first - Hour);
            Assert.That(P(sim).ApplicantsFor(mill.Id).Count, Is.EqualTo(2));
            sim.AdvanceTo(first);
            Applicant m = P(sim).ApplicantsFor(mill.Id).Last();
            Assert.That((m.CitizenId, m.SkillKnown, m.SecondTraitKnown), Is.EqualTo((0L, false, false)), "a migrant: skill ±15, one trait");
            PeopleRules.SkillRange(sim, m, out lo, out hi);
            Assert.That(hi - lo, Is.LessThanOrEqualTo(30));
            Assert.That(P(sim).PostAt(mill.Id).NextApplicantAt, Is.GreaterThan(first), "townspeople don't fill the migrants' places: more may come");
            Assert.That(P(sim).OutsidersFor(mill.Id), Is.EqualTo(1));

            // Hired elsewhere, a townsperson's other applications go; a hire closes the place's post and sends the rest away.
            Ok(sim, new OfferContract(sim.State.Clock.SimTime, town[0].Id));
            Assert.That(P(sim).Applicants.Any(a => a.CitizenId == x.Members[0]), Is.False);
            Assert.That(P(sim).ApplicantsFor(mill.Id), Is.Empty);
            Assert.That(P(sim).PostAt(mill.Id), Is.Null);
            Assert.That(sim.Execute(new PostJob(sim.State.Clock.SimTime, mill.Id, false)).Reason, Is.EqualTo(RejectReason.AlreadyStaffed));
        }

        // §3.1: strangers never crowd your own people out: with three migrants already asking for the hut, word sent still brings your
        // townspeople, and they need no hut.
        [Test]
        public void Townspeople_NotCrowdedOut()
        {
            Simulation sim = Start();
            Household x = Townsfolk(sim);
            BuildHut(sim);
            for (int i = 0; i < 24 * 8 && P(sim).OutsidersFor(0) < 3; i++) sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            Assert.That(P(sim).OutsidersFor(0), Is.EqualTo(3));
            Ok(sim, new PostJob(sim.State.Clock.SimTime, false));
            Assert.That(P(sim).ApplicantsFor(0).Count(a => a.CitizenId != 0), Is.EqualTo(2));
            Applicant town = P(sim).ApplicantsFor(0).First(a => a.CitizenId == x.Members[1]);
            Ok(sim, new OfferContract(sim.State.Clock.SimTime, town.Id));
            Hand h = P(sim).Hands.Single();
            Citizen c = sim.State.Citizens.Citizen(x.Members[1]);
            Assert.That((h.HutId, h.Skill, c.Job, c.HandId), Is.EqualTo((0L, Fixed.FromInt(55), CitizenJob.Hand, h.Id)), "stays in the family house, a hand like any other");
            Assert.That(PeopleRules.EmptyHut(sim), Is.Not.Null, "the hut still stands empty for someone from outside");
        }

        // AC-2: a reference check costs 300, comes back after 12 h, narrows the skill to ±5 and says how they left.
        [Test]
        public void Reference()
        {
            Simulation sim = Start();
            BuildHut(sim);
            Applicant a = WaitForApplicant(sim);
            long t = sim.State.Clock.SimTime, cash = sim.State.Ledger.Cash;
            Ok(sim, new CheckReference(t, a.Id));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(300 * Coin));
            Assert.That(sim.Execute(new CheckReference(t, a.Id)).Reason, Is.EqualTo(RejectReason.AlreadyChecked), "once");
            sim.AdvanceTo(t + 11 * Hour);
            PeopleRules.SkillRange(sim, a, out int lo, out int hi);
            Assert.That(hi - lo, Is.GreaterThan(10), "still ±15 while it's out");
            sim.DrainEvents();
            sim.AdvanceTo(t + 12 * Hour);
            ReferenceChecked e = sim.DrainEvents().OfType<ReferenceChecked>().Single();
            Assert.That(e.ApplicantId, Is.EqualTo(a.Id));
            PeopleRules.SkillRange(sim, a, out lo, out hi);
            Assert.That((lo, hi), Is.EqualTo((System.Math.Max(0, a.Skill - 5), System.Math.Min(100, a.Skill + 5))));
            var traits = sim.Config.Game.People.Traits;
            bool poor = new[] { a.Trait1, a.Trait2 }.Any(i => traits[i].Careless || traits[i].HoursFactor < Fixed.One);
            Assert.That(e.GoodTerms, Is.EqualTo(!poor), "\"was let go\" when a trait is lazy or careless");

            // Known people need none; short of cash, it's refused.
            Assert.That(HiringRules.LeftOnGoodTerms(sim, new Applicant { Trait1 = sim.Config.Game.People.IndexOfTrait("lazy"), Trait2 = 0 }), Is.False);
            Applicant b = WaitForAnother(sim, a.Id);
            CashTo(sim, 100 * Coin);
            Assert.That(sim.Execute(new CheckReference(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
        }

        private static Applicant WaitForAnother(Simulation sim, long not)
        {
            for (int i = 0; i < 24 * 8 && !P(sim).Applicants.Any(x => x.Id != not); i++) sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
            return P(sim).Applicants.First(x => x.Id != not);
        }

        // AC-3: a low offer is taken at 0.40, else countered at the asked wage once; a high one at 0.95; the wage remembered.
        [Test]
        public void Negotiation()
        {
            Simulation sim = Start();
            var low = new Applicant { Id = 1 };
            Assert.That(HiringRules.AcceptChance(sim, low, OfferLevel.Low), Is.EqualTo(Fixed.Parse("0.40")));
            Assert.That(HiringRules.AcceptChance(sim, low, OfferLevel.High), Is.EqualTo(Fixed.Parse("0.95")));
            Assert.That(HiringRules.AcceptChance(sim, low, OfferLevel.Asked), Is.EqualTo(Fixed.Parse("0.85")));
            var town = new Applicant { Id = 2, CitizenId = 7 };
            Assert.That(HiringRules.AcceptChance(sim, town, OfferLevel.Low), Is.EqualTo(Fixed.Parse("0.60")), "townspeople: 0.6 below the asked wage");
            Assert.That(HiringRules.AcceptChance(sim, town, OfferLevel.Asked), Is.EqualTo(Fixed.One), "always at it or above");
            Assert.That(HiringRules.AcceptChance(sim, new Applicant { FromRosterId = 3 }, OfferLevel.Low), Is.EqualTo(Fixed.One), "regulars always (ADR 0041)");
            Assert.That(HiringRules.AcceptChance(sim, new Applicant { Countered = true }, OfferLevel.Asked), Is.EqualTo(Fixed.One), "a counter taken");

            // Seeds where a low offer is refused and countered, then the asked wage taken; and where it's taken at once.
            bool countered = false, taken = false;
            for (ulong seed = 1; seed < 40 && !(countered && taken); seed++)
            {
                Simulation s = Start(seed: seed);
                BuildHut(s);
                Applicant a = WaitForApplicant(s);
                long asked = HiringRules.OfferWage(s, a, OfferLevel.Asked), t = s.State.Clock.SimTime;
                Assert.That(HiringRules.OfferWage(s, a, OfferLevel.Low), Is.EqualTo(Money.RoundToCoin(Money.MulRate(asked, Fixed.Parse("0.9")))));
                Assert.That(HiringRules.OfferWage(s, a, OfferLevel.High), Is.EqualTo(Money.RoundToCoin(Money.MulRate(asked, Fixed.Parse("1.1")))));
                bool yes = Rng.Chance(Rng.Next(s.State.Seed, RngStream.Applicants, (ulong)a.Id, 5), Fixed.Parse("0.40"));
                s.DrainEvents();
                Ok(s, new OfferContract(t, a.Id, OfferLevel.Low));
                if (yes)
                {
                    Hand h = P(s).Hands.Single();
                    Assert.That((h.Wage, h.Fairness), Is.EqualTo((HiringRules.OfferWage(s, a, OfferLevel.Low), -1)), "hired low, and remembered");
                    taken = true;
                    continue;
                }
                CounterOffer c = s.DrainEvents().OfType<CounterOffer>().Single();
                Assert.That((c.Wage, a.Countered, P(s).Applicants.Contains(a)), Is.EqualTo((asked, true, true)), "a counter at the asked wage; they wait");
                Ok(s, new OfferContract(t, a.Id, OfferLevel.Asked));
                Assert.That((P(s).Hands.Single().Wage, P(s).Hands.Single().Fairness), Is.EqualTo((asked, 0)));
                countered = true;
            }
            Assert.That((countered, taken), Is.EqualTo((true, true)));

            // A high offer: 10% more, fair pay remembered; refused, they leave.
            for (ulong seed = 1; seed < 40; seed++)
            {
                Simulation s = Start(seed: seed);
                BuildHut(s);
                Applicant a = WaitForApplicant(s);
                bool yes = Rng.Chance(Rng.Next(s.State.Seed, RngStream.Applicants, (ulong)a.Id, 5), Fixed.Parse("0.95"));
                Ok(s, new OfferContract(s.State.Clock.SimTime, a.Id, OfferLevel.High));
                Assert.That(P(s).Applicants.Contains(a), Is.False, "no counter above the asked wage");
                if (!yes) continue;
                Assert.That((P(s).Hands.Single().Wage, P(s).Hands.Single().Fairness), Is.EqualTo((HiringRules.OfferWage(s, a, OfferLevel.High), 1)));
                return;
            }
            Assert.Fail("no seed took a high offer");
        }

        // §3.1, §3.9: a business's worker from the board is paid upfront from its cash, renewed at the asked wage, and can be let go.
        [Test]
        public void BusinessWorker()
        {
            Simulation sim = Start();
            Household x = Townsfolk(sim);
            Business.Business mill = BusinessTests.Mill(sim, hire: false);
            sim.AdvanceTo(mill.ReadyAt);
            Assert.That((mill.Status, mill.Worker), Is.EqualTo((BusinessStatus.Open, WorkerKind.None)), "nobody hired: it waits (the stopgap is gone)");
            Ok(sim, new PostJob(sim.State.Clock.SimTime, mill.Id, false));
            Applicant usha = P(sim).ApplicantsFor(mill.Id)[0];
            long cash = mill.Ledger.Cash, wage = HiringRules.OfferWage(sim, usha, OfferLevel.Asked);
            Assert.That(wage, Is.EqualTo(PeopleRules.AskedWage(sim, 27_000_00, Fixed.FromInt(60), false)), "a miller's market wage at crafting 60");
            Ok(sim, new OfferContract(sim.State.Clock.SimTime, usha.Id));
            Worker w = P(sim).WorkerAt(mill.Id);
            Citizen c = sim.State.Citizens.Citizen(x.Members[0]);
            Assert.That((mill.Worker, mill.WorkerCitizen, c.Job, c.Employer), Is.EqualTo((WorkerKind.Citizen, c.Id, CitizenJob.Business, mill.Id)));
            Assert.That((w.Skill, w.Wage, w.Fairness), Is.EqualTo((Fixed.FromInt(60), wage, 0)));
            Assert.That(cash - mill.Ledger.Cash, Is.EqualTo(PeopleRules.RestOfSeason(sim, wage)), "the rest of the season, from the mill's cash");

            sim.AdvanceTo(BusinessTests.NextClose(sim) + Hour);
            Assert.That((w.SeasonsWorked, w.Skill), Is.EqualTo((1, Fixed.FromInt(63))), "a season on the job: +3");
            Assert.That(w.Wage, Is.EqualTo(PeopleRules.AskedWage(sim, 27_000_00, Fixed.FromInt(60), false)), "renewed at the asked wage for the skill she had");

            // Let go after probation: severance 10% from the mill; she's back to outside work with her skill; you can run it.
            long before = mill.Ledger.Balance(Account.HiredLabour);
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, mill.Id));
            Assert.That(mill.Ledger.Balance(Account.HiredLabour) - before, Is.EqualTo(Money.MulRate(w.Wage, Fixed.Parse("0.10"))));
            Assert.That((mill.Worker, P(sim).Workers.Count, c.Job, c.Crafting), Is.EqualTo((WorkerKind.None, 0, CitizenJob.None, Fixed.FromInt(63))));
            Assert.That(sim.Execute(new LetWorkerGo(sim.State.Clock.SimTime, mill.Id)).Reason, Is.EqualTo(RejectReason.NoWorker));
            Ok(sim, new RunBusinessYourself(sim.State.Clock.SimTime, mill.Id));
            Assert.That(mill.Worker, Is.EqualTo(WorkerKind.Founder));
            Assert.That(sim.Execute(new RunBusinessYourself(sim.State.Clock.SimTime, mill.Id)).Reason, Is.EqualTo(RejectReason.AlreadyStaffed));
            Assert.That((sim.State.Ledger.Reconcile(), mill.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
        }
    }
}
