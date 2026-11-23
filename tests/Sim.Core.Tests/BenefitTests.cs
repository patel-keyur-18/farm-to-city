using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-258 (spec 028 §3.8, ADR 0160): the four benefits, their cost and effect, loyalty, the reputation step, health cover needing the clinic.</summary>
    public class BenefitTests
    {
        private static Business.Business Bakery(out Simulation sim)
        {
            sim = InvoiceTests.Township();
            return InvoiceTests.Found(sim, "bakery");
        }

        private static void Clinic(Simulation sim)
        {
            sim.State.Township.Clinic.Status = ClinicStatus.Open;
            sim.State.Township.Clinic.Doctor = DoctorStatus.InPost;
        }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-18: each benefit costs a worker a season, charged for the rest of the season when switched on, and adds its motivation; off stops it at once with no refund.
        [Test]
        public void Costs_Effects()
        {
            Business.Business b = Bakery(out Simulation sim);
            Worker w = P(sim).WorkerAt(b.Id);
            BenefitsDef d = sim.Config.Game.Benefits;
            Fixed m0 = WorkRules.Motivation(sim, w);
            long cash = b.Ledger.Cash + b.Ledger.Balance(Account.Overdraft);
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Meal, true));
            long paid = cash - (b.Ledger.Cash + b.Ledger.Balance(Account.Overdraft));
            Assert.That(paid, Is.InRange(1L, BenefitRules.Cost(sim, Benefit.Meal)), "the rest of the season, for its one worker");
            Assert.That(paid, Is.EqualTo(PeopleRules.RestOfSeason(sim, BenefitRules.Cost(sim, Benefit.Meal))));
            Assert.That(WorkRules.Motivation(sim, w) - m0, Is.EqualTo(d.MealMotivation));
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Leave, true));
            Assert.That(WorkRules.Motivation(sim, w) - m0, Is.EqualTo(d.MealMotivation + d.LeaveMotivation));
            Fixed before = WorkRules.Condition(sim, w);
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Leave, false));
            Assert.That(WorkRules.Condition(sim, w), Is.GreaterThan(before - Fixed.FromRatio(1, 20)), "paid leave costs its days' work: output a little under");
            long cash2 = b.Ledger.Cash;
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Meal, false));
            Assert.That(b.Ledger.Cash, Is.EqualTo(cash2), "no refund");
            Assert.That(WorkRules.Motivation(sim, w), Is.EqualTo(m0), "off stops it at once");
            Assert.That(sim.Execute(new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.None, true)).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            Reconciles(sim);
        }

        // AC-18: paid leave's output cost is its days of the season's.
        [Test]
        public void Leave_Costs_Its_Days()
        {
            Business.Business b = Bakery(out Simulation sim);
            Worker w = P(sim).WorkerAt(b.Id);
            Fixed before = BenefitRules.LeaveOutput(sim, w);
            Assert.That(before, Is.EqualTo(Fixed.One));
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Leave, true));
            BenefitsDef d = sim.Config.Game.Benefits;
            Assert.That(BenefitRules.LeaveOutput(sim, w), Is.EqualTo(Fixed.One - Fixed.FromRatio(d.LeaveDays, d.LeaveOf)));
            w.Fatigue = Fixed.FromRatio(3, 10);
            BenefitRules.Renew(sim, b.Id);
            Assert.That(w.Fatigue, Is.EqualTo(Fixed.FromRatio(3, 10) - d.LeaveFatigue), "tiredness falls once a season");
        }

        // AC-18: health cover needs the clinic; it halves sick days and pays them (no card).
        [Test]
        public void Health_Needs_Clinic()
        {
            Business.Business b = Bakery(out Simulation sim);
            Assert.That(sim.Execute(new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Health, true)).Reason, Is.EqualTo(RejectReason.NoClinic));
            Clinic(sim);
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Health, true));
            Worker w = P(sim).WorkerAt(b.Id);
            Assert.That(BenefitRules.SickFactor(sim, w), Is.EqualTo(sim.Config.Game.Benefits.HealthSick));
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Health, false));
            Assert.That(BenefitRules.SickFactor(sim, w), Is.EqualTo(Fixed.One));
        }

        // AC-18: each benefit multiplies the poaching chance by 0.85; all four leave about 2.6% of 5%.
        [Test]
        public void Loyalty()
        {
            Business.Business b = Bakery(out Simulation sim);
            Clinic(sim);
            Worker w = P(sim).WorkerAt(b.Id);
            Assert.That(BenefitRules.Loyalty(sim, w), Is.EqualTo(Fixed.One));
            foreach (Benefit x in BenefitRules.All) Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, x, true));
            Fixed four = BenefitRules.Loyalty(sim, w);
            Assert.That((four * Fixed.FromInt(10_000)).RoundToLong(), Is.EqualTo(5220), "0.85 four times");
            Fixed poach = sim.Config.Game.People.Hr.PoachChance * four;
            Assert.That(poach, Is.LessThan(sim.Config.Game.People.Hr.PoachChance * Fixed.FromRatio(3, 5)), "five percent falls to about two and a half");
            Reconciles(sim);
        }

        // AC-18: two or more kept through a close raise the employer's reputation by 1, once.
        [Test]
        public void Reputation_Step()
        {
            Business.Business b = Bakery(out Simulation sim);
            int rep = P(sim).Reputation;
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Meal, true));
            BenefitRules.AtClose(sim);
            Assert.That(P(sim).Reputation, Is.EqualTo(rep), "one is not enough");
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Leave, true));
            BenefitRules.AtClose(sim);
            Assert.That(P(sim).Reputation, Is.EqualTo(rep + 1));
        }

        // The festival bonus is paid on the festival's day, lifts motivation that season, and raises the name by 1; switched on mid-season it costs nothing until then.
        [Test]
        public void Festival_Bonus()
        {
            Business.Business b = Bakery(out Simulation sim);
            Worker w = P(sim).WorkerAt(b.Id);
            long cash = b.Ledger.Cash;
            Fixed m0 = WorkRules.Motivation(sim, w);
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Festival, true));
            Assert.That(b.Ledger.Cash, Is.EqualTo(cash), "nothing until the festival");
            Assert.That(WorkRules.Motivation(sim, w), Is.EqualTo(m0), "and no lift until it is paid");
            int rep = P(sim).Reputation;
            BenefitRules.Festival(sim);
            Assert.That(cash - b.Ledger.Cash, Is.EqualTo(BenefitRules.Cost(sim, Benefit.Festival)), "paid to its one worker");
            Assert.That(WorkRules.Motivation(sim, w) - m0, Is.EqualTo(sim.Config.Game.Benefits.FestivalMotivation));
            Assert.That(P(sim).Reputation, Is.EqualTo(rep + 1));
            Reconciles(sim);
        }

        // At a renewal the season's benefits are paid with the wage for everyone the business pays; a business that cannot pay loses them.
        [Test]
        public void Renewal_Pays_Or_Lapses()
        {
            Business.Business b = Bakery(out Simulation sim);
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, b.Id, Benefit.Meal, true));
            long cash = b.Ledger.Cash;
            BenefitRules.Renew(sim, b.Id);
            Assert.That(cash - b.Ledger.Cash, Is.EqualTo(BenefitRules.Cost(sim, Benefit.Meal)), "the whole season, upfront");
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Repairs, b.Ledger.Cash), new Posting(Account.Cash, -b.Ledger.Cash));
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Repairs, BusinessBooks.OverdraftLimit(sim, b) - BusinessBooks.Overdrawn(b)), new Posting(Account.Overdraft, -(BusinessBooks.OverdraftLimit(sim, b) - BusinessBooks.Overdrawn(b))));
            BenefitRules.Renew(sim, b.Id);
            Assert.That(b.Benefits & (int)Benefit.Meal, Is.EqualTo(0), "no cash, no meal");
        }

        // Your farm gives its hands benefits too: charged to you, shown on People.
        [Test]
        public void Farm_Hands()
        {
            Simulation sim = InvoiceTests.Township();
            Assert.That(BenefitRules.Staff(sim, 0), Is.Empty);
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, 0, Benefit.Meal, true));
            Assert.That(sim.State.Township.Exit.FarmBenefits, Is.EqualTo((int)Benefit.Meal));
            Reconciles(sim);
        }

        [Test]
        public void Before_The_Township_Nothing()
        {
            Simulation sim = Start();
            Assert.That(sim.Execute(new SetBenefit(sim.State.Clock.SimTime, 0, Benefit.Meal, true)).Reason, Is.EqualTo(RejectReason.NoTownship));
        }

        [Test]
        public void Content_Is_Checked()
        {
            string json = TestContent.ReadContent("benefits.json");
            BenefitsDef d = Sim.Content.ContentLoader.LoadBenefits(json);
            Assert.That((d.MealCost, d.LeaveDays, d.NightNeedDays), Is.EqualTo((1800L * 100, 4, 3)));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadBenefits(json.Replace("\"loyalty\": 0.85", "\"loyalty\": 0.2")));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadBenefits(json.Replace("\"nightNeedDays\": 3", "\"nightNeedDays\": 9")), "can't need more days than it looks back");
        }
    }
}
