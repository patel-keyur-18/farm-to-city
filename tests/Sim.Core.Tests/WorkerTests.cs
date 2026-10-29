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
    /// <summary>Spec 015 §3.4–3.6: a worker's output (skill × motivation × (1 − fatigue)), long days, courses and the four new traits.</summary>
    public class WorkerTests
    {
        private static int Trait(Simulation sim, string id) { return sim.Config.Game.People.IndexOfTrait(id); }

        /// <summary>A hand under contract with two steady traits (no effect of their own).</summary>
        private static Hand SteadyHand(Simulation sim)
        {
            Hand h = HireHand(sim);
            h.Trait1 = h.Trait2 = Trait(sim, "steady");
            return h;
        }

        /// <summary>A mill, built and open, with a commuter hired at <paramref name="skill"/> with the two traits.</summary>
        private static (Business.Business, Worker) MillWith(Simulation sim, int skill, string t1 = "steady", string t2 = "steady")
        {
            Business.Business b = BusinessTests.Mill(sim, hire: false);
            var a = new Applicant { Id = P(sim).NextApplicantId++, Skill = skill, Trait1 = Trait(sim, t1), Trait2 = Trait(sim, t2), Place = b.Id };
            HiringRules.HireWorker(sim, a, HiringRules.OfferWage(sim, a, OfferLevel.Asked), 0);
            sim.AdvanceTo(b.ReadyAt);
            return (b, P(sim).WorkerAt(b.Id));
        }

        private static bool Near(Fixed a, Fixed b) { return System.Math.Abs(a.Raw - b.Raw) < Fixed.FromRatio(1, 1000).Raw; }

        private static long Memo(Ledger l, string key)
        {
            return l.Entries.Where(e => e.DescriptionKey == key).Sum(e => e.Postings.Where(p => p.Account == Account.HiredLabour).Sum(p => p.Amount));
        }

        // AC-4: output = skill factor × motivation × (1 − fatigue); a fair hire in a content home has motivation exactly 1, so a hand
        // works spec 006's hours exactly and a typical (skill-50) business worker the business's rate.
        [Test]
        public void Output_Neutral()
        {
            Simulation sim = Start();
            Hand h = SteadyHand(sim);
            var p = sim.Config.Game.People;
            Assert.That(WorkRules.HomeOf(sim, h).Happiness, Is.GreaterThanOrEqualTo(Fixed.FromInt(60)), "alone in his hut, content");
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One));
            Fixed spec006 = p.Hand.HoursPerDay * (p.Hand.ProductivityBase + p.Hand.ProductivityPerSkill * h.Skill.MulRatio(1, 100));
            Assert.That(PeopleRules.HandHours(sim, h), Is.EqualTo(spec006), "spec 006's hours, exactly");

            // Motivation's parts: a low hire −0.10; a home at 40 −0.06; a sociable colleague +0.05; within 0.7–1.2.
            h.Fairness = -1;
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One - p.Work.FairBelow));
            WorkRules.HomeOf(sim, h).Happiness = Fixed.FromInt(40);
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One - p.Work.FairBelow - p.Work.HomePerPoint * Fixed.FromInt(20)));
            Assert.That(PeopleRules.HandHours(sim, h), Is.EqualTo(spec006 * WorkRules.Motivation(sim, h)), "motivation scales the hours");
            WorkRules.HomeOf(sim, h).Happiness = Fixed.Zero;
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One - p.Work.FairBelow - p.Work.HomePerPoint * Fixed.FromInt(60)), "the lowest without HR events (US-114)");
            h.Fairness = 1;
            WorkRules.HomeOf(sim, h).Happiness = Fixed.FromInt(80);
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One + p.Work.FairAbove), "hired above the asked wage");

            // A business: a skill-50 worker grinds at the rate, a skill-70 one faster, a skill-30 one slower; you run it as rated.
            Simulation m = Start();
            (Business.Business b, Worker w) = MillWith(m, 50);
            long rate = BusinessRules.TypeOf(m, b).GramsPerHour;
            Assert.That(BusinessRules.Capacity(m, b), Is.EqualTo(rate));
            w.Skill = Fixed.FromInt(70);
            Assert.That(BusinessRules.Capacity(m, b), Is.GreaterThan(rate));
            w.Skill = Fixed.FromInt(30);
            Assert.That(BusinessRules.Capacity(m, b), Is.LessThan(rate));
            w.Fatigue = Fixed.FromRatio(1, 2);
            Assert.That(BusinessRules.Capacity(m, b), Is.LessThan(rate / 2), "tired and unskilled");
        }

        // AC-5: long days raise output and fatigue; fatigue falls at normal hours; the extra hours pay 1.5×.
        [Test]
        public void Fatigue_Overtime()
        {
            Simulation sim = Start();
            Hand h = SteadyHand(sim);
            var work = sim.Config.Game.People.Work;
            Fixed normal = PeopleRules.HandHours(sim, h);
            long hourly = h.Wage * 100 / (8 * 4 * 100); // a season's wage over its 32 normal hours
            long overtime = WorkRules.OvertimeDay(sim, h);
            Assert.That(overtime, Is.EqualTo(Money.RoundToCoin(h.Wage * 3 / 32)), "2 hours at 1.5× the hourly wage (" + hourly + ")");

            long cash = sim.State.Ledger.Cash, t = sim.State.Clock.SimTime;
            Ok(sim, new SetLongDays(t, h.Id, true));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - overtime), "today's paid now");
            Assert.That(Near(PeopleRules.HandHours(sim, h), normal * Fixed.FromRatio(10, 8)), Is.True, "10 hours, not 8");

            long day = (t / Day + 1) * Day;
            sim.AdvanceTo(day);
            Assert.That(h.Fatigue, Is.EqualTo(work.FatigueUp), "a long day tires");
            Assert.That(PeopleRules.HandHours(sim, h), Is.LessThan(normal * Fixed.FromRatio(10, 8)), "tired, less");
            for (int i = 0; i < 12; i++) sim.AdvanceTo(day + (i + 1) * Day);
            Assert.That(h.Fatigue, Is.EqualTo(work.FatigueMax), "never above 0.5");
            Assert.That(PeopleRules.HandHours(sim, h), Is.LessThan(normal), "worn out, long days do less than normal ones");
            Assert.That(sim.State.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.overtime"), Is.EqualTo(14), "paid each morning, upfront (at the wage renewed each season)");

            Ok(sim, new SetLongDays(sim.State.Clock.SimTime, h.Id, false));
            sim.AdvanceTo(day + 13 * Day);
            Assert.That(h.Fatigue, Is.EqualTo(work.FatigueMax - work.FatigueDown), "a normal day rests");

            // Short of cash, long days stop at the morning's payment.
            Ok(sim, new SetLongDays(sim.State.Clock.SimTime, h.Id, true));
            CashTo(sim, 0);
            Assert.That(sim.Execute(new SetLongDays(sim.State.Clock.SimTime, h.Id, true)).Accepted, Is.True, "already on: nothing to pay");
            var stopped = Events<LongDaysStopped>(sim, () => sim.AdvanceTo(day + 14 * Day));
            Assert.That(stopped, Has.Count.EqualTo(1));
            Assert.That(h.LongDays, Is.False);

            // A business: its open hours past the usual day are the long day, each hour paid at 1.5× as it's worked.
            Simulation m = Start();
            (Business.Business b, Worker w) = MillWith(m, 50);
            Assert.That(m.Execute(new SetBusiness(m.State.Clock.SimTime, b.Id, 6, 20, b.Fee, b.OwnPerDay, false)).Reason, Is.EqualTo(RejectReason.InvalidArgument), "12 hours at most");
            Ok(m, new SetBusiness(m.State.Clock.SimTime, b.Id, 7, 19, b.Fee, b.OwnPerDay, false));
            long start = (m.State.Clock.SimTime / Day + 1) * Day;
            m.AdvanceTo(start);
            long before = Memo(b.Ledger, "ledger.overtime");
            m.AdvanceTo(start + Day);
            Assert.That(Memo(b.Ledger, "ledger.overtime") - before, Is.EqualTo(2 * WorkRules.OvertimeHour(m, w)), "7–8 and 18–19");
            Assert.That(WorkRules.OvertimeHour(m, w), Is.EqualTo(Money.RoundToCoin(w.Wage * 3 / (2 * 10 * 4))), "1.5× a season's wage over its 40 usual hours");
            Assert.That(w.Fatigue, Is.EqualTo(work.FatigueUp + work.FatigueUp), "two long days");
        }

        private static System.Collections.Generic.List<T> Events<T>(Simulation sim, System.Action run) where T : SimEvent
        {
            sim.DrainEvents();
            run();
            return sim.DrainEvents().OfType<T>().ToList();
        }

        // AC-6: a course adds 10 skill after courseDays (1) away, paid; while away they don't work; the next renewal asks more.
        [Test]
        public void Course()
        {
            Simulation sim = Start();
            Hand h = SteadyHand(sim);
            Fixed skill = h.Skill;
            long asked = PeopleRules.AskedWage(sim, skill, false), cash = sim.State.Ledger.Cash, t = sim.State.Clock.SimTime;
            Ok(sim, new SendOnCourse(t, 0, h.Id));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - 6_000 * Coin));
            Assert.That(PeopleRules.HandHours(sim, h), Is.EqualTo(Fixed.Zero), "away");
            Assert.That(sim.Execute(new SendOnCourse(t, 0, h.Id)).Reason, Is.EqualTo(RejectReason.OnCourse));
            var done = Events<CourseFinished>(sim, () => sim.AdvanceTo(t + sim.Config.Game.People.Work.CourseDays * Day));
            Assert.That(done, Has.Count.EqualTo(1));
            Assert.That(h.Skill, Is.EqualTo(skill + Fixed.FromInt(10)));
            Assert.That(h.CourseUntil, Is.EqualTo(0L));
            Assert.That(PeopleRules.AskedWage(sim, h.Skill, false), Is.GreaterThan(asked), "worth more: asks more at renewal");
            CashTo(sim, 1_000 * Coin);
            Assert.That(sim.Execute(new SendOnCourse(sim.State.Clock.SimTime, 0, h.Id)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));

            // A business's worker: paid from the business, the mill stands idle while they're away.
            Simulation m = Start();
            (Business.Business b, Worker w) = MillWith(m, 40);
            long bcash = b.Ledger.Cash;
            Ok(m, new SendOnCourse(m.State.Clock.SimTime, b.Id, w.Id));
            Assert.That(Memo(b.Ledger, "ledger.course"), Is.EqualTo(6_000 * Coin));
            Assert.That(b.Ledger.Cash, Is.EqualTo(bcash - 6_000 * Coin));
            var worked = Events<BusinessWorked>(m, () => m.AdvanceTo(m.State.Clock.SimTime + Day));
            Assert.That(worked, Is.Empty, "nobody grinds while they're away");
            m.AdvanceTo(w.CourseUntil);
            Assert.That(w.Skill, Is.EqualTo(Fixed.FromInt(50)));
            Assert.That(m.Execute(new SendOnCourse(m.State.Clock.SimTime, b.Id + 7, 1)).Reason, Is.EqualTo(RejectReason.NoWorker));
        }

        // AC-7: the four traits act as §3.6; no contradictory pairs are drawn; all ten are drawn.
        [Test]
        public void Traits()
        {
            Simulation sim = Start();
            var p = sim.Config.Game.People;
            int hard = Trait(sim, "hardworking"), lazy = Trait(sim, "lazy"), careless = Trait(sim, "careless"), perfect = Trait(sim, "perfectionist");
            var seen = new System.Collections.Generic.HashSet<int>();
            for (ulong i = 0; i < 2_000; i++)
            {
                ulong roll = Rng.Next(42, RngStream.Applicants, i, 3);
                int first = (int)(i % (ulong)p.Traits.Count), second = PeopleRules.SecondTrait(sim, roll, first);
                seen.Add(second);
                Assert.That(second, Is.Not.EqualTo(first));
                Assert.That(p.Clash(first, second), Is.False, p.Traits[first].Id + " with " + p.Traits[second].Id);
            }
            Assert.That(seen, Has.Count.EqualTo(10), "all ten drawn");
            Assert.That(p.Clash(hard, lazy) && p.Clash(lazy, hard) && p.Clash(careless, perfect) && p.Clash(perfect, careless), Is.True);

            // Perfectionist: 5% slower. Sociable: each colleague +0.05.
            Hand h = SteadyHand(sim);
            Fixed hours = PeopleRules.HandHours(sim, h);
            h.Trait2 = perfect;
            Assert.That(Near(PeopleRules.HandHours(sim, h), hours * Fixed.FromRatio(95, 100)), Is.True, "perfectionist: 5% slower");
            h.Trait2 = Trait(sim, "steady");
            BuildHut(sim); // the quarters' second place
            Hand other = null;
            for (int tries = 0; tries < 20 && other == null; tries++)
            {
                Applicant a = WaitForApplicant(sim);
                Ok(sim, new OfferContract(sim.State.Clock.SimTime, a.Id));
                other = P(sim).Hands.FirstOrDefault(x => x != h);
            }
            other.Trait1 = Trait(sim, "sociable");
            other.Trait2 = Trait(sim, "steady");
            other.Fairness = 0;
            Assert.That(WorkRules.Motivation(sim, h), Is.EqualTo(Fixed.One + Fixed.FromRatio(5, 100)), "a sociable colleague");

            // Creative sells 5% higher; frugal pays 10% less for power. Same seed, same day, only the worker's trait differs.
            long Sales(string trait, out long power)
            {
                Simulation m = Start();
                (Business.Business b, Worker _) = MillWith(m, 50, trait, "steady");
                BusinessTests.GiveWheat(m, 2_000_000);
                m.AdvanceTo((m.State.Clock.SimTime / Day + 1) * Day + Day);
                power = b.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.power").Sum(e => e.Postings.Where(x => x.Account == Account.Power).Sum(x => x.Amount));
                return b.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.sales").Sum(e => e.Postings.Where(x => x.Account == Account.Sales).Sum(x => -x.Amount));
            }
            long steady = Sales("steady", out long steadyPower), creative = Sales("creative", out _);
            Sales("frugal", out long frugalPower);
            Assert.That(steady, Is.GreaterThan(0));
            Assert.That(creative, Is.EqualTo(steady * 105 / 100).Within(steady / 1000 + 100), "creative: 5% more");
            Assert.That(frugalPower, Is.EqualTo(steadyPower * 90 / 100).Within(steadyPower / 1000 + 100), "frugal: 10% less power");

            // Frugal at home: half the leisure saved.
            Household x = WorkRules.HomeOf(sim, h);
            HouseholdRules.SetBudget(sim, x);
            long leisure = x.Leisure, saved = x.Saved;
            sim.State.Citizens.Citizen(x.Members[0]).Trait1 = Trait(sim, "frugal");
            HouseholdRules.SetBudget(sim, x);
            Assert.That(x.Leisure, Is.EqualTo(leisure / 2));
            Assert.That(x.Saved, Is.EqualTo(saved + leisure - leisure / 2));
        }
    }
}
