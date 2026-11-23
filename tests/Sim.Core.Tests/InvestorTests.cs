using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-247 (spec 028 §3.4, ADR 0159): investors, partners and a rival's buy-out; their shares, the profit paid at the close, buying back, the sale settling them first.</summary>
    public class InvestorTests
    {
        private static Business.Business Profitable(out Simulation sim, string type = "bakery")
        {
            sim = InvoiceTests.Township();
            Business.Business b = InvoiceTests.Found(sim, type);
            ExitTests.Seasons(b, 50_000, 50_000, 50_000, 50_000);
            return b;
        }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "business " + b.Id);
        }

        private static long _period;

        /// <summary>Closes of later and later seasons (the roll's period moved, the clock not) until the business is offered a deal of this kind.</summary>
        private static DealOffer OfferOf(Simulation sim, Business.Business b, DealKind kind, int tries = 200)
        {
            ExitState x = sim.State.Township.Exit;
            for (int i = 0; i < tries; i++)
            {
                x.Offers.Clear();
                b.BoughtOutOfferedAt = 0;
                OwnerRules.AtClose(sim, ++_period);
                DealOffer o = x.Offers.Find(q => q.Kind == kind && q.BusinessId == b.Id);
                if (o != null) return o;
            }
            Assert.Fail("never offered a " + kind);
            return null;
        }

        private static void Savings(Simulation sim, long coins) { foreach (Household h in sim.State.Citizens.Households) h.Savings = coins * Coin; }

        private static DealOffer Add(Simulation sim, DealKind kind, Business.Business b, int pct, long coins, long who = 0)
        {
            ExitState x = sim.State.Township.Exit;
            var o = new DealOffer { Id = x.NextOfferId++, Kind = kind, BusinessId = b.Id, Who = who, Share = Fixed.FromRatio(pct, 100), Cash = coins * Coin, RaisedAt = sim.State.Clock.SimTime, ExpiresAt = sim.State.Clock.SimTime + 3600 };
            x.Offers.Add(o);
            return o;
        }

        private static void Earn(Simulation sim, Business.Business b, long coins) { b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, coins * Coin), new Posting(Account.Sales, -coins * Coin)); }

        // AC-11: an investor offers 10 to 30% of the worth in cash for the same share; the card is open 48 h.
        [Test]
        public void Offer()
        {
            Business.Business b = Profitable(out Simulation sim);
            ExitDef d = sim.Config.Game.Exit;
            Assert.That(ExitRules.Valuation(sim, b).Value, Is.GreaterThanOrEqualTo(d.InvestorValueMin), "worth enough for an investor to look");
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            Assert.That(d.InvestorShares, Does.Contain(o.Share), "10, 15, 20, 25 or 30%");
            Assert.That(o.Cash, Is.EqualTo(Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, o.Share))), "cash = the worth × the share");
            Assert.That(o.ExpiresAt - o.RaisedAt, Is.EqualTo(48 * 3600), "open 48 hours");
            Assert.That(sim.PendingEvents.OfType<DealOffered>().Any(), Is.True);
        }

        // Spec 028 §3.4: nothing is raised for a business that lost money or while you are away (gap 10: live only); one offer open at a time.
        [Test]
        public void Offer_Conditions()
        {
            Business.Business b = Profitable(out Simulation sim);
            ExitState x = sim.State.Township.Exit;
            ExitTests.Seasons(b, 50_000, 50_000, 50_000, -1_000);
            for (int i = 0; i < 60; i++) OwnerRules.AtClose(sim, ++_period);
            Assert.That(x.Offers, Is.Empty, "a season that lost money draws nothing");
            ExitTests.Seasons(b, 50_000, 50_000, 50_000, 50_000);
            sim.State.Clock.IsAway = true;
            for (int i = 0; i < 60; i++) OwnerRules.AtClose(sim, ++_period);
            Assert.That(x.Offers, Is.Empty, "nothing lands while you are away");
            sim.State.Clock.IsAway = false;
            DealOffer first = OfferOf(sim, b, DealKind.Investor);
            x.Offers.Add(first);
            int count = x.Offers.Count;
            for (int i = 0; i < 40; i++) OwnerRules.AtClose(sim, ++_period);
            Assert.That(x.Offers.Count, Is.EqualTo(count), "one offer open at a time for a business");
        }

        // AC-11: the cash goes into the business as a liability: your equity in it and your net worth are what they were; the share is held.
        [Test]
        public void Accept_Investor()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            long equity = BusinessBooks.Equity(b), worth = Reporting.NetWorth(sim).Total;
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            Stake k = b.Stakes.Single();
            Assert.That((k.Kind, k.Share, k.Put), Is.EqualTo((StakeKind.Investor, o.Share, o.Cash)));
            Assert.That(OwnerRules.Liability(b), Is.EqualTo(o.Cash), "owed to him as his share");
            Assert.That(BusinessBooks.Equity(b), Is.EqualTo(equity), "your equity in it is what it was");
            Assert.That(Reporting.NetWorth(sim).Total, Is.EqualTo(worth), "a share is a liability, not income");
            Assert.That(sim.State.Township.Exit.Offers, Is.Empty);
            Assert.That(sim.Execute(new AcceptDeal(sim.State.Clock.SimTime, o.Id)).Reason, Is.EqualTo(RejectReason.UnknownOffer), "answered once");
            Assert.That(ExitRules.Valuation(sim, b).Book, Is.EqualTo(equity + o.Cash), "what it holds counts his money");
            Reconciles(sim);
        }

        // AC-11: a share is paid its part of the season's profit at the close, from the business's cash, and nothing when it lost.
        [Test]
        public void Share_Profit()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            Earn(sim, b, 90_000);
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 3600);
            BusinessReport r = b.Reports[b.Reports.Count - 1];
            long profit = BusinessBooks.NetProfit(r), paid = b.Stakes.Single().Paid;
            Assert.That(profit, Is.GreaterThan(0));
            Assert.That(paid, Is.InRange(Money.MulRate(profit, o.Share) - Coin, Money.MulRate(profit, o.Share) + Coin), "the share × the season's profit");
            Assert.That(b.Ledger.Balance(Account.OutsideDrawings), Is.EqualTo(paid), "a distribution, kept out of the profit");
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Repairs, 500_000 * Coin), new Posting(Account.Overdraft, -500_000 * Coin));
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 3600);
            Assert.That(b.Stakes.Single().Paid, Is.EqualTo(0), "a season that lost pays nothing");
            Reconciles(sim);
        }

        // AC-11: outside shares (investor plus partner) never go above 40%; one investor a business.
        [Test]
        public void OutsideCap()
        {
            Business.Business b = Profitable(out Simulation sim);
            Savings(sim, 1_000_000);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, Add(sim, DealKind.Investor, b, 30, 100_000).Id));
            DealOffer partner = Add(sim, DealKind.Partner, b, 20, 50_000, sim.State.Citizens.Households[0].Id);
            Assert.That(sim.Execute(new AcceptDeal(sim.State.Clock.SimTime, partner.Id)).Reason, Is.EqualTo(RejectReason.OutsideCap), "30% and 20% would be 50%");
            Assert.That(OwnerRules.Outside(b), Is.EqualTo(Fixed.FromRatio(30, 100)));
            DealOffer second = Add(sim, DealKind.Investor, b, 10, 30_000);
            Assert.That(sim.Execute(new AcceptDeal(sim.State.Clock.SimTime, second.Id)).Reason, Is.EqualTo(RejectReason.HasStake), "one investor a business");
            Reconciles(sim);
        }

        // AC-12: a partner puts in 20% of the worth from her own savings for 20% of the profit; one a business.
        [Test]
        public void Partner()
        {
            Business.Business b = Profitable(out Simulation sim);
            Savings(sim, 1_000_000);
            DealOffer o = OfferOf(sim, b, DealKind.Partner);
            Household home = sim.State.Citizens.Household(o.Who);
            long saved = home.Savings;
            Assert.That(o.Share, Is.EqualTo(Fixed.FromRatio(20, 100)));
            Assert.That(o.Cash, Is.EqualTo(Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, Fixed.FromRatio(20, 100)))), "20% of the worth");
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            Assert.That(home.Savings, Is.EqualTo(saved - o.Cash), "from her own savings");
            Assert.That(b.Stakes.Single().Kind, Is.EqualTo(StakeKind.Partner));
            Earn(sim, b, 90_000);
            long before = home.Savings;
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 3600);
            Assert.That(b.Stakes.Single().Paid, Is.GreaterThan(0));
            Assert.That(home.Savings - before, Is.GreaterThanOrEqualTo(b.Stakes.Single().Paid), "her share reaches her household");
            Reconciles(sim);
        }

        // Gap 11 (ADR 0159): a partner never blocks a sale: any sale or closing pays outside shares first, a part of what is left.
        [Test]
        public void Partner_Settled_On_Sale()
        {
            Business.Business b = Profitable(out Simulation sim);
            Savings(sim, 1_000_000);
            DealOffer o = OfferOf(sim, b, DealKind.Partner);
            Household home = sim.State.Citizens.Household(o.Who);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            long saved = home.Savings;
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, b.Id, true));
            for (int i = 0; i < 30 && b.Status != BusinessStatus.Closed; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            SaleRecord rec = sim.State.Township.Exit.Sales.Single();
            Assert.That(rec.Outside, Is.GreaterThan(0), "paid first");
            Assert.That(rec.Outside, Is.InRange(Money.MulRate(rec.Price, Fixed.FromRatio(20, 100)) - 20 * Coin, Money.MulRate(rec.Price, Fixed.FromRatio(20, 100)) + 20 * Coin), "about 20% of the price");
            Assert.That(home.Savings - saved, Is.EqualTo(rec.Outside), "she is paid");
            Assert.That(b.Stakes, Is.Empty);
            Assert.That(rec.ToYou, Is.LessThanOrEqualTo(rec.Price - rec.Outside), "what you receive is what is left");
            Reconciles(sim);
        }

        // AC-11: after four seasons an investor's share is bought back at the worth now × the share; before, it is refused; it becomes yours.
        [Test]
        public void BuyBack()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            Assert.That(sim.Execute(new BuyBackShare(sim.State.Clock.SimTime, b.Id, StakeKind.Investor)).Reason, Is.EqualTo(RejectReason.BuyBackTooEarly));
            Assert.That(sim.Execute(new BuyBackShare(sim.State.Clock.SimTime, b.Id, StakeKind.Partner)).Reason, Is.EqualTo(RejectReason.NoStake));
            Stake k = b.Stakes.Single();
            k.Period -= 4; // four seasons on
            long price = OwnerRules.BuyBackPrice(sim, b, k), cash = sim.State.Ledger.Cash, worth = Reporting.NetWorth(sim).Total;
            Assert.That(price, Is.EqualTo(System.Math.Max(k.Put, Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, k.Share)))), "the worth × the share, never under what he put in");
            Ok(sim, new BuyBackShare(sim.State.Clock.SimTime, b.Id, StakeKind.Investor));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(price), "paid from your cash");
            Assert.That(b.Stakes, Is.Empty);
            Assert.That(OwnerRules.Liability(b), Is.EqualTo(0));
            Assert.That(worth - Reporting.NetWorth(sim).Total, Is.EqualTo(price - o.Cash).Within(Coin), "what it cost beyond what he put in");
            Reconciles(sim);
        }

        // Sprint 25 balance (ADR 0164): a business whose profit fell buys its investor back for what he put in, not for less.
        [Test]
        public void BuyBack_Floor()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            Stake k = b.Stakes.Single();
            k.Period -= 4;
            ExitTests.Seasons(b, 0, 0, 0, 0);
            Assert.That(Money.RoundToCoin(Money.MulRate(ExitRules.Valuation(sim, b).Value, k.Share)), Is.LessThan(k.Put), "the worth fell below what he put in");
            Assert.That(OwnerRules.BuyBackPrice(sim, b, k), Is.EqualTo(k.Put), "the price holds at his cash");
            Ok(sim, new BuyBackShare(sim.State.Clock.SimTime, b.Id, StakeKind.Investor));
            Reconciles(sim);
        }

        // AC-13: a rival (a regional buyer for a business that is not a shop) offers 90 to 110% of the worth, never under what it holds.
        [Test]
        public void BuyOut_Offer()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.BuyOut);
            ExitDef d = sim.Config.Game.Exit;
            Worth w = ExitRules.Valuation(sim, b);
            Assert.That(o.Share, Is.InRange(d.BuyOutLow, d.BuyOutHigh));
            Assert.That(o.Cash, Is.GreaterThanOrEqualTo(w.Book), "never under what it holds");
            Assert.That(o.Cash, Is.InRange(ExitRules.PriceAt(w, d.BuyOutLow) - 1, ExitRules.PriceAt(w, d.BuyOutHigh) + 1));
            Assert.That(d.Buyers[(int)o.Who], Is.EqualTo(BusinessRules.TypeOf(sim, b).IsShop ? "rival" : "regional"), "a rival for a shop, a regional buyer for any other business");
            Assert.That(o.ExpiresAt - o.RaisedAt, Is.EqualTo(24 * 3600).Or.EqualTo(48 * 3600));
            Assert.That(b.BoughtOutOfferedAt, Is.GreaterThan(0));
            long id = o.Id;
            Ok(sim, new DeclineDeal(sim.State.Clock.SimTime, id));
            Assert.That(sim.State.Township.Exit.Offers.Any(q => q.Id == id), Is.False, "refusing costs nothing");
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open));
            Assert.That(b.Stakes, Is.Empty);
        }

        // Spec 028 §3.4: at most one buy-out offer a business each 2 seasons; an offer not answered lapses to no.
        [Test]
        public void BuyOut_Gap_And_Lapse()
        {
            Business.Business b = Profitable(out Simulation sim);
            ExitState x = sim.State.Township.Exit;
            DealOffer o = OfferOf(sim, b, DealKind.BuyOut);
            x.Offers.Clear();
            for (int i = 0; i < 80; i++)
            {
                long period = ++_period;
                if (period + 1 - b.BoughtOutOfferedAt < sim.Config.Game.Exit.BuyOutGapSeasons)
                {
                    OwnerRules.AtClose(sim, period);
                    Assert.That(x.Offers.Any(q => q.Kind == DealKind.BuyOut), Is.False, "not twice within 2 seasons");
                }
                x.Offers.Clear();
            }
            x.Offers.Add(o);
            sim.AdvanceTo(o.ExpiresAt + 3600);
            Assert.That(x.Offers.Any(q => q.Id == o.Id), Is.False, "lapsed to no");
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open));
        }

        // AC-13: accepting is a sale (a hold), the outside share is paid first, and the business is gone with its offers.
        [Test]
        public void BuyOut_Accept_Settles()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer inv = OfferOf(sim, b, DealKind.Investor);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, inv.Id));
            DealOffer o = OfferOf(sim, b, DealKind.BuyOut);
            Ok(sim, new AcceptBuyOut(sim.State.Clock.SimTime, o.Id));
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Closed));
            SaleRecord rec = sim.State.Township.Exit.Sales.Single();
            Assert.That(rec.Price, Is.GreaterThanOrEqualTo(System.Math.Min(o.Cash, rec.Held)));
            Assert.That(rec.Outside, Is.GreaterThan(0), "his share is paid first");
            Assert.That(sim.State.Township.Exit.Offers.Any(q => q.BusinessId == b.Id), Is.False);
            Assert.That(sim.PendingEvents.OfType<BusinessSold>().Count(), Is.EqualTo(1));
            Reconciles(sim);
        }

        // A business that is gone takes its offers with it, quietly.
        [Test]
        public void Gone_Ends_Offers()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            Assert.That(sim.State.Township.Exit.Offers.Any(q => q.Id == o.Id), Is.False);
            Reconciles(sim);
        }

        // A plain closing pays an outside owner his part of what is left, so nothing of his is wiped.
        [Test]
        public void Closing_Pays_Outside()
        {
            Business.Business b = Profitable(out Simulation sim);
            DealOffer o = OfferOf(sim, b, DealKind.Investor);
            Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, b.Id));
            Assert.That(b.Stakes, Is.Empty);
            Assert.That(OwnerRules.Liability(b), Is.EqualTo(0));
            Reconciles(sim);
        }

        [Test]
        public void Content_Is_Checked()
        {
            string json = TestContent.ReadContent("exit.json");
            ExitDef d = Sim.Content.ContentLoader.LoadExit(json);
            Assert.That((d.MaxOutside, d.BuyBackSeasons, d.InvestorShares.Count, d.PartnerStake), Is.EqualTo((Fixed.FromRatio(40, 100), 4, 5, Fixed.FromRatio(20, 100))));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadExit(json.Replace("\"maxOutside\": 0.40", "\"maxOutside\": 1.50")));
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadExit(json.Replace("[0.10, 0.15, 0.20, 0.25, 0.30]", "[0.30, 0.20]")), "shares ascend");
            Assert.Throws<Sim.Content.ContentException>(() => Sim.Content.ContentLoader.LoadExit(json.Replace("\"buyOutHigh\": 1.10", "\"buyOutHigh\": 0.80")));
        }
    }
}
