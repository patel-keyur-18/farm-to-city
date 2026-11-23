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
    /// <summary>US-258 (spec 028 §3.8, ADR 0160): the night shift: eligibility, the grid, being offered, the second worker, the hours, tiredness, and ending with the day worker.</summary>
    public class NightShiftTests
    {
        private static Business.Business Mill(out Simulation sim)
        {
            sim = InvoiceTests.Township();
            Business.Business b = InvoiceTests.Found(sim, "flour_mill");
            BusinessTests.GiveWheat(sim, 500_000_000);
            b.OwnPerDay = 200_000_000; // a plan the day shift cannot grind
            return b;
        }

        private static void Grid(Simulation sim) { sim.State.Township.UtilitiesBuilt |= 1 << UtilityDef.Grid; }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-19: only the mill and what makes by recipe (bakery, dairy, sweet shop) may have a night shift.
        [Test]
        public void Eligible_Kinds()
        {
            Simulation sim = InvoiceTests.Township();
            var kinds = sim.Config.Game.Businesses.Types.Where(NightRules.Eligible).Select(t => t.Id).ToList();
            Assert.That(kinds, Is.EquivalentTo(new[] { "flour_mill", "bakery", "dairy", "sweet_shop" }));
        }

        // AC-19: offered after the day shift fell behind on 3 of the last 8 days; needs the grid and a day worker; a second worker at the role's wage plus a quarter.
        [Test]
        public void Rules()
        {
            Business.Business b = Mill(out Simulation sim);
            Assert.That(NightRules.CanStart(sim, b), Is.EqualTo(RejectReason.NoGrid));
            Grid(sim);
            Assert.That(NightRules.CanStart(sim, b), Is.EqualTo(RejectReason.NightNotOffered), "not before it has been too busy");
            b.ShortDays = 0b101;
            Assert.That(NightRules.CanStart(sim, b), Is.EqualTo(RejectReason.NightNotOffered), "two of eight is not three");
            b.ShortDays = 0b10101;
            Assert.That(NightRules.ShortCount(sim, b), Is.EqualTo(3));
            Assert.That(NightRules.CanStart(sim, b), Is.EqualTo(RejectReason.None));
            long wage = NightRules.Wage(sim, b);
            long market = PeopleRules.AskedWage(sim, HiringRules.MarketWage(sim, b.Id), Fixed.FromInt(50), false);
            Assert.That(wage, Is.EqualTo(Money.RoundToCoin(Money.MulRate(market, Fixed.FromRatio(125, 100)))).Within(Money.Coin), "a quarter more");
            long cash = b.Ledger.Cash + b.Ledger.Balance(Account.Overdraft);
            Ok(sim, new SetNightShift(sim.State.Clock.SimTime, b.Id, true));
            Worker night = P(sim).NightAt(b.Id);
            Assert.That((night.Night, night.Wage, night.Place), Is.EqualTo((true, wage, b.Id)));
            Assert.That(P(sim).WorkerAt(b.Id), Is.Not.SameAs(night), "the day worker is still the day worker");
            Assert.That(cash - (b.Ledger.Cash + b.Ledger.Balance(Account.Overdraft)), Is.EqualTo(PeopleRules.RestOfSeason(sim, wage)), "the rest of the season, from the business's cash");
            Assert.That(sim.Execute(new SetNightShift(sim.State.Clock.SimTime, b.Id, true)).Reason, Is.EqualTo(RejectReason.NightState));
            Reconciles(sim);
        }

        // AC-19: the night's hours are open with the grid, 20:00 to 04:00, and not otherwise; without the grid it does not work.
        [Test]
        public void Hours()
        {
            Business.Business b = Mill(out Simulation sim);
            Grid(sim);
            b.ShortDays = 0b111;
            Assert.That(BusinessRules.OpenAt(sim, b, 22), Is.False);
            Ok(sim, new SetNightShift(sim.State.Clock.SimTime, b.Id, true));
            foreach (int hour in new[] { 20, 22, 0, 3 }) Assert.That(BusinessRules.OpenAt(sim, b, hour), Is.True, "hour " + hour);
            foreach (int hour in new[] { 4, 5, 18, 19 }) Assert.That(BusinessRules.OpenAt(sim, b, hour), Is.EqualTo(hour >= 8 && hour < 18), "hour " + hour);
            sim.State.Township.UtilitiesBuilt &= ~(1 << UtilityDef.Grid);
            Assert.That(BusinessRules.OpenAt(sim, b, 22), Is.False, "no grid, no night shift");
        }

        // AC-19: the night adds hours: with a plan the day shift cannot grind, the night worker grinds more; and the day that fell behind is counted.
        [Test]
        public void Adds_Hours_Not_Demand()
        {
            Business.Business b = Mill(out Simulation sim);
            Grid(sim);
            InvoiceTests.ToHour(sim, 19);
            long day = b.OwnToday;
            Assert.That(day, Is.GreaterThan(0));
            Assert.That(b.ShortDays & 1, Is.EqualTo(1), "the day shift could not keep up with the plan, counted when it ended");
            b.ShortDays = 0b111;
            Ok(sim, new SetNightShift(sim.State.Clock.SimTime, b.Id, true));
            long before = b.OwnToday;
            InvoiceTests.ToHour(sim, 23);
            Assert.That(b.OwnToday, Is.GreaterThan(before), "the night worker grinds");
            // With no plan left to grind, the night worker makes nothing more: it adds hours, not customers.
            b.OwnPerDay = 0;
            long made = b.OwnToday;
            InvoiceTests.ToHour(sim, 2, 1);
            Assert.That(b.OwnToday, Is.LessThanOrEqualTo(made), "nothing to grind, nothing ground");
        }

        // AC-19: the night worker's tiredness rises 0.07 a day, and the shift ends with the day worker or the business.
        [Test]
        public void Tiredness_And_Ending()
        {
            Business.Business b = Mill(out Simulation sim);
            Grid(sim);
            b.ShortDays = 0b111;
            Ok(sim, new SetNightShift(sim.State.Clock.SimTime, b.Id, true));
            Worker night = P(sim).NightAt(b.Id);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86_400);
            Assert.That(night.Fatigue, Is.GreaterThanOrEqualTo(sim.Config.Game.Benefits.NightFatigue), "never rests by day");
            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, b.Id));
            Assert.That(P(sim).NightAt(b.Id), Is.Null, "no day worker, no night shift");
            Reconciles(sim);

            Business.Business c = Mill(out Simulation sim2);
            Grid(sim2);
            c.ShortDays = 0b111;
            Ok(sim2, new SetNightShift(sim2.State.Clock.SimTime, c.Id, true));
            Ok(sim2, new SetNightShift(sim2.State.Clock.SimTime, c.Id, false));
            Assert.That(P(sim2).NightAt(c.Id), Is.Null);
            Ok(sim2, new SetNightShift(sim2.State.Clock.SimTime, c.Id, true));
            Ok(sim2, new CloseBusiness(sim2.State.Clock.SimTime, c.Id));
            Assert.That(P(sim2).NightAt(c.Id), Is.Null, "a closed business keeps no night worker");
            Reconciles(sim2);
        }

        // A sale pays the night worker off with the day worker.
        [Test]
        public void Sale_Releases_Night_Worker()
        {
            Business.Business b = Mill(out Simulation sim);
            Grid(sim);
            b.ShortDays = 0b111;
            Ok(sim, new SetNightShift(sim.State.Clock.SimTime, b.Id, true));
            ExitTests.Seasons(b, 50_000, 50_000, 50_000, 50_000);
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, true));
            for (int i = 0; i < 40 && b.Status != BusinessStatus.Closed; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Closed));
            Assert.That(P(sim).NightAt(b.Id), Is.Null);
            Reconciles(sim);
        }
    }
}
