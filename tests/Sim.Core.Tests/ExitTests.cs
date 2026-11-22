using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-125 (spec 028 §3.3, ADR 0158): the worth and its multiple, a listing and its buyer, a rushed sale, cancelling, the gain taxed, the closed policy and cards, branches.</summary>
    public class ExitTests
    {
        private const long Day = 86_400;

        private static Simulation Township() { return InvoiceTests.Township(); }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "business " + b.Id);
        }

        /// <summary>The kept reports with these season profits, oldest first (coins).</summary>
        private static void Seasons(Business.Business b, params long[] coins)
        {
            b.Reports.Clear();
            long p = 0;
            foreach (long c in coins)
            {
                long amount = c * Coin;
                var balances = new List<KeyValuePair<Account, long>> { new KeyValuePair<Account, long>(amount >= 0 ? Account.Sales : Account.Repairs, -amount) };
                b.Reports.Add(new BusinessReport { Period = p++, Balances = balances.AsReadOnly() });
            }
        }

        // AC-8: the worth is the larger of what it holds and a multiple of a year's profit; 4.5 to start, +1 growth, -1 a fall, -1 risk (a loss or under four seasons), +1 a manager with four profitable seasons, within 3 to 6.
        [Test]
        public void Valuation()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Seasons(b, 10_000, 10_000, 10_000, 10_000);
            Worth w = ExitRules.Valuation(sim, b);
            Assert.That((w.Profit, w.Multiple, w.Growth, w.Risk, w.Manager), Is.EqualTo((40_000 * Coin, Fixed.FromRatio(9, 2), 0, false, false)), "a steady year at the base multiple");
            Assert.That(w.Value, Is.EqualTo(System.Math.Max(w.Book, 180_000 * Coin)));

            Seasons(b, 10_000, 10_000, 12_000, 12_000);
            Assert.That(ExitRules.Valuation(sim, b).Multiple, Is.EqualTo(Fixed.FromRatio(11, 2)), "profit 10% or more up on the two before: +1");
            Seasons(b, 12_000, 12_000, 10_000, 10_000);
            Assert.That(ExitRules.Valuation(sim, b).Multiple, Is.EqualTo(Fixed.FromRatio(7, 2)), "10% or more down: -1");
            Seasons(b, 10_000, 10_000, 10_500, 10_500);
            Assert.That(ExitRules.Valuation(sim, b).Growth, Is.EqualTo(0), "under 10% is no growth");

            Seasons(b, 10_000, 10_000, 10_000);
            w = ExitRules.Valuation(sim, b);
            Assert.That((w.Risk, w.Multiple, w.Profit), Is.EqualTo((true, Fixed.FromInt(3) + Fixed.FromRatio(1, 2), 40_000 * Coin)), "under four seasons old: -1; three seasons annualised");
            Seasons(b, 10_000, -1_000, 10_000, 10_000);
            Assert.That(ExitRules.Valuation(sim, b).Risk, Is.True, "a season that lost money: -1");

            ManagerTests.Manage(sim, b);
            Seasons(b, 10_000, 10_000, 10_000, 10_000);
            w = ExitRules.Valuation(sim, b);
            Assert.That((w.Manager, w.Multiple), Is.EqualTo((true, Fixed.FromRatio(11, 2))), "a manager and four profitable seasons: +1");
            Seasons(b, 10_000, 10_000, 12_000, 12_000);
            Assert.That(ExitRules.Valuation(sim, b).Multiple, Is.EqualTo(Fixed.FromInt(6)), "clamped at 6");
            Seasons(b, 12_000, 12_000, 10_000, -10_000);
            Assert.That(ExitRules.Valuation(sim, b).Multiple, Is.EqualTo(Fixed.FromInt(3)), "clamped at 3");

            Seasons(b, 1, 1, 1, 1);
            w = ExitRules.Valuation(sim, b);
            Assert.That(w.Value, Is.EqualTo(w.Book), "a sale never ends in a paper loss: never under what it holds");
            Seasons(b);
            Assert.That(ExitRules.Valuation(sim, b).Profit, Is.GreaterThanOrEqualTo(1), "at least 1 coin");
        }

        private static void RunUntilSold(Simulation sim, Business.Business b, int maxDays = 8)
        {
            for (int i = 0; i < maxDays * 24 && b.Status != BusinessStatus.Closed; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Closed), "sold");
        }

        // AC-9: a listing completes in 3 to 6 days at 90 to 105% of the worth, never under what it holds; the business runs until then; what goes and what you receive add up.
        [Test]
        public void Sale()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Seasons(b, 50_000, 50_000, 50_000, 50_000);
            P(sim).WorkerAt(b.Id).SeasonsWorked = 1; // past the probation season: severance is due
            long cash0 = sim.State.Ledger.Cash, now = sim.State.Clock.SimTime;
            Ok(sim, new ListBusiness(now, b.Id, false));
            Assert.That(b.Listed, Is.True);
            long days = (b.CompletesAt - now) / Day;
            Assert.That(days, Is.InRange(3, 6));
            Assert.That(b.ListShare, Is.InRange(Fixed.FromRatio(90, 100), Fixed.FromRatio(105, 100)));
            Assert.That(sim.Execute(new ListBusiness(now, b.Id, false)).Reason, Is.EqualTo(RejectReason.AlreadyListed));
            sim.AdvanceTo(now + 3600);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open), "it runs until the day it closes");
            Worth before = ExitRules.Valuation(sim, b);
            RunUntilSold(sim, b);
            SaleRecord rec = sim.State.Township.Exit.Sales.Single();
            Assert.That(rec.Price, Is.GreaterThanOrEqualTo(rec.Held), "never under what it holds");
            Assert.That(rec.Price, Is.InRange(Finance.Money.MulRate(rec.Worth, Fixed.FromRatio(90, 100)), Finance.Money.MulRate(rec.Worth, Fixed.FromRatio(105, 100)) + 1));
            Assert.That(rec.TypeId, Is.EqualTo("bakery"));
            Assert.That(rec.Severance, Is.GreaterThan(0), "its worker is paid off");
            Assert.That(rec.Gain, Is.GreaterThan(rec.Severance), "the buyer pays for what it holds after its people are paid off: the gain is the premium, not the severance back");
            Assert.That(sim.State.Ledger.Cash - cash0, Is.GreaterThanOrEqualTo(rec.ToYou - 50_000 * Coin), "what came to you reached your cash (less a few days' living costs)");
            Assert.That(P(sim).WorkerAt(b.Id), Is.Null, "its worker is released");
            Assert.That(BusinessRules.Place(sim, b.TileId), Is.EqualTo(RejectReason.None), "the acre is yours again");
            Assert.That(sim.PendingEvents.OfType<BusinessSold>().Count(), Is.EqualTo(1));
            Reconciles(sim);
            Assert.That(before.Value, Is.GreaterThan(0));
        }

        [Test]
        public void Rushed()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Seasons(b, 50_000, 50_000, 50_000, 50_000);
            long now = sim.State.Clock.SimTime;
            Ok(sim, new ListBusiness(now, b.Id, true));
            Assert.That((b.CompletesAt - now, b.ListShare), Is.EqualTo((Day, Fixed.FromRatio(85, 100))), "one day at 85%");
            RunUntilSold(sim, b, 2);
            SaleRecord rec = sim.State.Township.Exit.Sales.Single();
            Assert.That(rec.Share, Is.EqualTo(Fixed.FromRatio(85, 100)));
            Assert.That(rec.Price, Is.GreaterThanOrEqualTo(rec.Held));
            Reconciles(sim);
        }

        [Test]
        public void Cancel()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Assert.That(sim.Execute(new CancelBusinessSale(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.NotListedForSale));
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, false));
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 3600);
            Ok(sim, new CancelBusinessSale(sim.State.Clock.SimTime, b.Id));
            Assert.That(b.Listed, Is.False);
            sim.AdvanceTo(sim.State.Clock.SimTime + 8 * Day);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open), "cancelled before a buyer came");
            Assert.That(sim.State.Township.Exit.Sales, Is.Empty);
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, false));
        }

        // AC-9: the gain over what it holds is business income, taxed at the business tax with the season's profit; the treasury is paid what is taxed.
        [Test]
        public void Gain_Taxed()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Seasons(b, 500_000, 500_000, 500_000, 500_000);
            long treasury = sim.State.Treasury.Ledger.Balance(Account.BusinessTaxIncome);
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, false));
            RunUntilSold(sim, b);
            SaleRecord rec = sim.State.Township.Exit.Sales.Single();
            Assert.That(rec.Gain, Is.GreaterThan(0), "a worth above what it holds");
            Assert.That(rec.Gain, Is.EqualTo(rec.Price - rec.Held));
            Assert.That(rec.Tax, Is.GreaterThanOrEqualTo(Finance.Money.MulRate(rec.Gain, TaxRules.Rate(sim, TaxKind.Business)) - 1), "tax on the gain at the business rate");
            Assert.That(-(sim.State.Treasury.Ledger.Balance(Account.BusinessTaxIncome) - treasury), Is.EqualTo(rec.Tax), "the treasury is paid what is taxed");
            Assert.That(rec.ToYou, Is.EqualTo(rec.Price - rec.Tax).Within(2_000_000), "the price less the tax (and its small cash and severance)");
            Reconciles(sim);
        }

        // ADR 0154's flagged items: a sold or closed business keeps no policy, and a card aimed at it ends quietly.
        [Test]
        public void Gone_Business_Policy_And_Cards()
        {
            Simulation sim = Township();
            Business.Business b = InvoiceTests.Found(sim, "bakery");
            Business.Business other = InvoiceTests.Found(sim, "flour_mill", b.TileId);
            InsuranceRules.State(sim).BusinessPolicyOrNew(b.Id).On = true;
            InsuranceRules.State(sim).BusinessPolicyOrNew(other.Id).On = true;
            EventsState es = EventRules.State(sim);
            int strike = sim.Config.Game.Events.IndexOf("supplier_strike");
            es.Waiting.Add(new PendingEvent { Id = es.NextId++, Def = strike, RaisedAt = sim.State.Clock.SimTime, ExpiresAt = sim.State.Clock.SimTime + 48 * 3600, Target = b.Id });
            es.Waiting.Add(new PendingEvent { Id = es.NextId++, Def = strike, RaisedAt = sim.State.Clock.SimTime, ExpiresAt = sim.State.Clock.SimTime + 48 * 3600, Target = other.Id });
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            Assert.That(InsuranceRules.State(sim).BusinessPolicy(b.Id), Is.Null, "its policy is dropped");
            Assert.That(InsuranceRules.State(sim).BusinessPolicy(other.Id), Is.Not.Null);
            Assert.That(es.Waiting.Select(c => c.Target), Is.EqualTo(new[] { other.Id }), "the card aimed at it ends quietly");
            Reconciles(sim);
        }

        // AC-10: a second business of a kind is a branch of the first, starting as its settings, with its own books and manager; at most 2 branches a kind.
        [Test]
        public void Branch()
        {
            Simulation sim = Township();
            Business.Business first = InvoiceTests.Found(sim, "bakery");
            first.OpenHour = 7;
            first.Fee = Fixed.FromInt(5);
            Business.Business b2 = InvoiceTests.Found(sim, "bakery", first.TileId);
            Assert.That((b2.BranchOf, first.BranchOf), Is.EqualTo((first.Id, 0L)));
            Assert.That(b2.OpenHour, Is.EqualTo(7), "its plan starts as the first one's");
            Assert.That(b2.Ledger, Is.Not.SameAs(first.Ledger), "its own books");
            Worker m = ManagerTests.Manage(sim, b2);
            Assert.That(P(sim).ManagerAt(first.Id), Is.Null, "its own manager");
            Assert.That(m.Place, Is.EqualTo(b2.Id));
            Business.Business b3 = InvoiceTests.Found(sim, "bakery", first.TileId, b2.TileId);
            Assert.That(ExitRules.Branches(sim, first.Type), Is.EqualTo(2));
            int tile = InvoiceTests.FreeTile(sim, first.TileId, b2.TileId, b3.TileId);
            long give = 3_000_000 * Coin;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            Assert.That(sim.Execute(new FoundBusiness(sim.State.Clock.SimTime, "bakery", tile, 400_000 * Coin, false)).Reason, Is.EqualTo(RejectReason.TooManyBranches));
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b3.Id));
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "bakery", tile, 400_000 * Coin, false));
            Reconciles(sim);
        }

        [Test]
        public void Before_The_Township_Nothing()
        {
            Simulation sim = Start();
            Business.Business mill = BusinessTests.Mill(sim);
            sim.AdvanceTo(mill.ReadyAt + 1);
            Assert.That(sim.Execute(new ListBusiness(sim.State.Clock.SimTime, mill.Id, false)).Reason, Is.EqualTo(RejectReason.NoTownship));
        }

        [Test]
        public void Content_Is_Checked()
        {
            string json = TestContent.ReadContent("exit.json");
            Sim.Core.Definitions.ExitDef d = Sim.Content.ContentLoader.LoadExit(json);
            Assert.That((d.MaxBranches, d.Buyers.Count, d.SaleDaysMin, d.SaleDaysMax), Is.EqualTo((2, 3, 3, 6)));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadExit(json.Replace("\"maxMultiple\": 6", "\"maxMultiple\": 2")));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadExit(json.Replace("\"rushedShare\": 0.85", "\"rushedShare\": 0.95")), "a rushed sale pays less than a buyer's lowest");
        }
    }
}
