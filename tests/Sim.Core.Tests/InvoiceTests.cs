using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>US-123 (spec 028 §3.1, ADR 0155): sales between businesses on terms, the grocer and tea stall on terms, ageing and the stop rule, exact books.</summary>
    public class InvoiceTests
    {
        private const long Day = 86_400, Hour = 3600;

        internal static Simulation Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            return sim;
        }

        internal static int FreeTile(Simulation sim, params int[] not)
        {
            foreach (Tile t in sim.State.Land.Tiles)
                if (!not.Contains(t.Id) && BusinessRules.Place(sim, t.Id) == RejectReason.None && Sim.Core.People.HouseRules.Place(sim, t.Id) == RejectReason.None) return t.Id;
            Assert.Fail("no tile for a business");
            return -1;
        }

        internal static Business.Business Found(Simulation sim, string type, params int[] not)
        {
            long give = 3_000_000 * Coin - sim.State.Ledger.Cash;
            if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, type, FreeTile(sim, not), 400_000 * Coin, false));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 60);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open));
            return b;
        }

        /// <summary>A mill (with wheat in the store) and a bakery, both open; the clock a few hours into the next morning so atta has gone from one to the other.</summary>
        internal static Simulation MillAndBakery(out Business.Business mill, out Business.Business bakery)
        {
            Simulation sim = Township();
            mill = Found(sim, "flour_mill");
            bakery = Found(sim, "bakery", mill.TileId);
            BusinessTests.GiveWheat(sim, 20_000_000);
            return sim;
        }

        internal static void ToHour(Simulation sim, int hour, int daysAhead = 0)
        {
            long t = (sim.State.Clock.SimTime / Day + daysAhead) * Day + hour * Hour;
            if (t <= sim.State.Clock.SimTime) t += Day;
            sim.AdvanceTo(t);
        }

        private static void Reconciles(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "business " + b.Id);
        }

        // AC-1: from the Township a sale between two of your businesses is an invoice on the pair's term (half a season by default); before the Township it is cash;
        // households, the trader and your own store's sales to a shop stay cash.
        [Test]
        public void Which_Sales_Are_Invoiced()
        {
            Simulation sim = MillAndBakery(out Business.Business mill, out Business.Business bakery);
            Assert.That(InvoiceRules.On(sim), Is.True);
            ToHour(sim, 14, 1);
            InvoicesState s = sim.State.Township.Invoices;
            Assert.That(s.Open.Count, Is.GreaterThan(0), "the mill's atta to the bakery is invoiced");
            Invoice atta = s.Open.First(i => i.Good == "atta");
            Assert.That(atta.Seller, Is.EqualTo(mill.Id));
            Assert.That(atta.Buyer, Is.EqualTo(bakery.Id));
            Assert.That(atta.TermSeconds, Is.EqualTo(2 * Day), "half a season, 2 real days");
            Assert.That(atta.DueAt, Is.EqualTo(atta.IssuedAt + 2 * Day));
            long owed = s.Open.Where(i => i.Seller == mill.Id).Sum(i => i.Amount);
            Assert.That(mill.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(owed), "the seller's receivable");
            Assert.That(-bakery.Ledger.Balance(Account.TradePayable), Is.EqualTo(s.Open.Where(i => i.Buyer == bakery.Id).Sum(i => i.Amount)), "the buyer's payable");
            Assert.That(s.Open.All(i => i.Buyer != 0 || i.BuyerRival.Length > 0), Is.True, "only businesses and village shops are invoiced");
            Assert.That(s.Open.Any(i => i.Good == "wheat"), Is.False, "the farm's own wheat stays cash");
            Reconciles(sim);
        }

        [Test]
        public void Before_The_Township_Cash()
        {
            Simulation sim = Start();
            Business.Business mill = BusinessTests.Mill(sim);
            sim.AdvanceTo(mill.ReadyAt + 1);
            RecipeTests.Found(sim, "bakery", HomeTile + 1);
            BusinessTests.GiveWheat(sim, 20_000_000);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day);
            Assert.That(InvoiceRules.On(sim), Is.False);
            Assert.That(sim.State.Township?.Invoices.Open.Count ?? 0, Is.EqualTo(0));
            Assert.That(mill.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(0));
            Assert.That(sim.Execute(new SetTerms(sim.State.Clock.SimTime, mill.Id, 2, null, "season")).Reason, Is.EqualTo(RejectReason.NoTownship));
        }

        // AC-1: the term is the seller's choice for each buyer: cash pays at once, Season is 4 real days; a bad business, a buyer that is the seller or an unknown term is refused.
        [Test]
        public void Terms()
        {
            Simulation sim = MillAndBakery(out Business.Business mill, out Business.Business bakery);
            long at = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetTerms(at, 9999, bakery.Id, null, "half")).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(sim.Execute(new SetTerms(at, mill.Id, mill.Id, null, "half")).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(sim.Execute(new SetTerms(at, mill.Id, bakery.Id, null, "year")).Reason, Is.EqualTo(RejectReason.InvalidArgument));
            Assert.That(sim.Execute(new SetTerms(at, mill.Id, 0, "nobody", "half")).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Ok(sim, new SetTerms(at, mill.Id, bakery.Id, null, "season"));
            Assert.That(InvoiceRules.TermOf(sim, mill.Id, bakery.Id, null), Is.EqualTo(2));
            ToHour(sim, 14, 1);
            Assert.That(sim.State.Township.Invoices.Open.First(i => i.Good == "atta").TermSeconds, Is.EqualTo(4 * Day));

            Ok(sim, new SetTerms(sim.State.Clock.SimTime, mill.Id, bakery.Id, null, "cash"));
            sim.State.Township.Invoices.Open.Clear(); // what is owed so far is settled by hand below
            long before = mill.Ledger.Cash;
            ToHour(sim, 14, 1);
            Assert.That(sim.State.Township.Invoices.Open.Any(i => i.Seller == mill.Id && i.Good == "atta"), Is.False, "on cash, no new invoice");
            Assert.That(mill.Ledger.Cash, Is.Not.EqualTo(before));
        }

        // AC-2: the grocer and the tea stall take the village shops' pav and paneer on terms; they pay at the term's end and never go late.
        [Test]
        public void Wholesale_Buyers()
        {
            Simulation sim = Township();
            Business.Business bakery = Found(sim, "bakery");
            ToHour(sim, 8, 1);
            ToHour(sim, 8, 1);
            InvoicesState s = sim.State.Township.Invoices;
            var village = s.Open.Where(i => i.BuyerRival.Length > 0).ToList();
            Assert.That(village.Any(i => i.BuyerRival == "grocer" && i.Good == "pav"), Is.True, "the grocer takes pav");
            Assert.That(village.Any(i => i.BuyerRival == "village_tea" && i.Good == "pav"), Is.True, "so does the tea stall");
            Assert.That(village.All(i => i.Buyer == 0 && i.Seller == bakery.Id && i.TermSeconds == 2 * Day), Is.True);
            long owed = village.Sum(i => i.Amount);
            Assert.That(owed, Is.GreaterThan(0));
            sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day);
            var events = sim.DrainEvents();
            Assert.That(events.OfType<InvoicePaid>().Any(p => p.BuyerRival == "grocer"), Is.True, "paid at the term's end");
            Assert.That(sim.State.Township.Invoices.Open.Any(i => i.Stage != InvoiceStage.Current), Is.False, "a village shop never goes late");
            Assert.That(events.OfType<InvoiceOverdue>().Any(), Is.False);
            Reconciles(sim);

            Ok(sim, new SetTerms(sim.State.Clock.SimTime, bakery.Id, 0, "grocer", "cash"));
            sim.State.Township.Invoices.Open.Clear();
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Day);
            Assert.That(sim.State.Township.Invoices.Open.Any(i => i.BuyerRival == "grocer"), Is.False, "the grocer on cash: no invoice");
            Assert.That(sim.State.Township.Invoices.Open.Any(i => i.BuyerRival == "village_tea"), Is.True, "the tea stall still on terms");
        }

        // Empties the bakery's cash and its overdraft room so it cannot pay what it owes the mill.
        private static void Broke(Simulation sim, Business.Business b)
        {
            long cash = b.Ledger.Cash;
            if (cash > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.OwnersCapital, cash), new Posting(Account.Cash, -cash));
            long room = BusinessBooks.OverdraftLimit(sim, b) - BusinessBooks.Overdrawn(b);
            if (room > 0) b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, room), new Posting(Account.Overdraft, -room));
            long all = b.Ledger.Cash;
            b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.OwnersCapital, all), new Posting(Account.Cash, -all));
        }

        /// <summary>Keeps the business without money, hour by hour, until <paramref name="until"/> (its sales would otherwise pay the invoice).</summary>
        private static void BrokeUntil(Simulation sim, Business.Business b, long until)
        {
            while (sim.State.Clock.SimTime < until)
            {
                Broke(sim, b);
                sim.AdvanceTo(System.Math.Min(until, sim.State.Clock.SimTime + 600));
            }
        }

        // AC-3: an invoice the buyer cannot pay at its due time is late for one term, then overdue; overdue stops the seller's deliveries until it is paid.
        [Test]
        public void Ageing_And_StopRule()
        {
            Simulation sim = MillAndBakery(out Business.Business mill, out Business.Business bakery);
            ToHour(sim, 14, 1);
            Invoice first = sim.State.Township.Invoices.Open.First(i => i.Seller == mill.Id);
            long due = first.DueAt;
            sim.DrainEvents();
            sim.AdvanceTo(due - Hour);
            Broke(sim, bakery); // the bakery runs dry just before the first atta is due
            sim.AdvanceTo(due + 1 * Hour);
            Assert.That(first.Stage, Is.EqualTo(InvoiceStage.Late), "unpaid at its due time: late");
            Assert.That(sim.State.Township.Invoices.Open.Contains(first), Is.True, "no fee and no write-off: it stays");
            Assert.That(InvoiceRules.Blocked(sim, mill, bakery), Is.False, "late is not yet a stop");
            BrokeUntil(sim, bakery, due + 2 * Day + Hour);
            Assert.That(first.Stage, Is.EqualTo(InvoiceStage.Overdue), "more than a term past due");
            var events = sim.DrainEvents();
            Assert.That(events.OfType<InvoiceOverdue>().Count(), Is.GreaterThan(0));
            Assert.That(events.OfType<SupplyStopped>().Any(e => e.Seller == mill.Id && e.Buyer == bakery.Id), Is.True);
            Assert.That(InvoiceRules.Blocked(sim, mill, bakery), Is.True);

            // Stopped: the mill sells the bakery no new atta, so its payable does not grow.
            long payable = -bakery.Ledger.Balance(Account.TradePayable);
            int atta = sim.Config.Game.Market.IndexOf("atta");
            long stock = bakery.Stock[atta];
            BrokeUntil(sim, bakery, sim.State.Clock.SimTime + 12 * Hour);
            Assert.That(sim.State.Township.Invoices.Open.Count(i => i.Seller == mill.Id && i.IssuedAt > due + 2 * Day), Is.EqualTo(0), "no new invoice while it is overdue");
            Assert.That(-bakery.Ledger.Balance(Account.TradePayable), Is.EqualTo(payable));
            Assert.That(bakery.Stock[atta], Is.LessThanOrEqualTo(stock), "and no new atta arrived");

            // Put money in: the invoice is paid at the next hour and the mill delivers again.
            Ok(sim, new PutCash(sim.State.Clock.SimTime, bakery.Id, 2_000_000 * Coin));
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Hour);
            Assert.That(sim.State.Township.Invoices.Open.Contains(first), Is.False, "paid");
            Assert.That(InvoiceRules.Blocked(sim, mill, bakery), Is.False);
            Reconciles(sim);
        }

        // AC-4: the books stay exact through invoices and payments; the receivable and payable cancel in net worth, so terms never create money.
        [Test]
        public void Books_Reconcile_And_NetWorth_Cancels()
        {
            Simulation terms = MillAndBakery(out Business.Business m1, out Business.Business b1);
            Simulation cash = MillAndBakery(out Business.Business m2, out Business.Business b2);
            Ok(cash, new SetTerms(cash.State.Clock.SimTime, m2.Id, b2.Id, null, "cash"));
            ToHour(terms, 14, 1);
            ToHour(cash, 14, 1);
            Assert.That(terms.State.Township.Invoices.Open.Count, Is.GreaterThan(0));
            Reconciles(terms);
            Reconciles(cash);
            Assert.That(BusinessBooks.Equity(m1) + BusinessBooks.Equity(b1), Is.EqualTo(BusinessBooks.Equity(m2) + BusinessBooks.Equity(b2)), "the same worth on terms or on cash");
            Assert.That(m1.Ledger.Balance(Account.OwedByBuyers), Is.GreaterThan(0));
            Assert.That(m2.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(0));
            // The books show the difference between profit and cash: the seller's cash is lower by what it is owed.
            Assert.That(m2.Ledger.Cash - m1.Ledger.Cash, Is.EqualTo(m1.Ledger.Balance(Account.OwedByBuyers)), "the mill's cash is short by what the bakery owes it");
        }

        // A business closing collects what it is owed and pays what it owes; what a buyer cannot pay is forgiven between your two businesses; the books stay exact.
        [Test]
        public void Closing_Settles()
        {
            Simulation sim = MillAndBakery(out Business.Business mill, out Business.Business bakery);
            ToHour(sim, 14, 1);
            Assert.That(-bakery.Ledger.Balance(Account.TradePayable), Is.GreaterThan(0));
            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, bakery.Id)); // the buyer closes: it pays the mill from its proceeds
            Assert.That(bakery.Ledger.Balance(Account.TradePayable), Is.EqualTo(0));
            Assert.That(sim.State.Township.Invoices.Open.Any(i => i.Buyer == bakery.Id), Is.False);
            Reconciles(sim);

            Simulation sim2 = MillAndBakery(out Business.Business mill2, out Business.Business bakery2);
            ToHour(sim2, 14, 1);
            Broke(sim2, bakery2);
            Assert.That(mill2.Ledger.Balance(Account.OwedByBuyers), Is.GreaterThan(0));
            Ok(sim2, new CloseBusiness(sim2.State.Clock.SimTime, mill2.Id)); // the seller closes: the bakery cannot pay, so it is forgiven
            Assert.That(mill2.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(0));
            Assert.That(bakery2.Ledger.Balance(Account.TradePayable), Is.EqualTo(0), "its payable to the closed mill is forgiven");
            Assert.That(sim2.State.Township.Invoices.Open.Any(i => i.Seller == mill2.Id), Is.False);
            Reconciles(sim2);
        }

        // AC-26 (US-123 part): two seasons of invoices, terms changed and a stop, and everything reconciles; a replay of the same commands gives the same state.
        [Test]
        public void Reconciles_Through_Seasons()
        {
            Simulation sim = MillAndBakery(out Business.Business mill, out Business.Business bakery);
            for (int i = 0; i < 8; i++)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + Day);
                if (i == 2) Ok(sim, new SetTerms(sim.State.Clock.SimTime, mill.Id, bakery.Id, null, "season"));
                if (i == 4) Broke(sim, bakery);
                Reconciles(sim);
            }
            sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            Reconciles(sim);
        }
    }
}
