using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-123 (spec 028 §3.1, §7, Preview 16): the Orders tab, its terms buttons, the stop rule's warning and the books' why-cash-moved rows.</summary>
    public class InvoiceViewTests
    {
        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // Preview 16: the tab shows from the Township; it lists who buys on what term, what is owed and what is owed by the business, with a Why? on each number.
        [Test]
        public void Orders_Tab()
        {
            Simulation sim = Situations.Invoicing();
            ViewContext ctx = ViewKit2.Context(sim);
            Business mill = Of(sim, "flour_mill"), bakery = Of(sim, "bakery");
            SheetVm m = BusinessSheet.Build(ctx, mill.Id, InvoiceSheet.Tab);
            Assert.That(m.Tabs.Any(t => t.Id == "tab:" + InvoiceSheet.Tab), Is.True, "an Orders tab");
            Assert.That(m.Lines.Any(l => l.Action != null && l.Action.Id == "terms:" + bakery.Id + ":"), Is.True, "the bakery's terms button on the mill");
            Assert.That(m.Lines.Any(l => l.WhyKey == "invoiceOwed"), Is.True, "what is owed to the mill");

            SheetVm b = BusinessSheet.Build(ctx, bakery.Id, InvoiceSheet.Tab);
            Assert.That(b.Lines.Any(l => l.Action != null && l.Action.Id.StartsWith("terms:0:grocer")), Is.True, "the village grocer buys pav on terms");
            Assert.That(b.Lines.Any(l => l.WhyKey == "invoiceLine" && l.Note != null && l.Note.StartsWith(ctx.Strings.Get("invoices.stage.overdue"))), Is.True, "an overdue invoice is told so");
            Assert.That(b.Lines[0].WhyKey, Is.EqualTo("invoiceStop"), "the warning leads: the mill stopped delivering");
            Assert.That(b.Lines[0].Action.Id, Is.EqualTo("putCash"));
            Assert.That(b.Lines[0].NoteTone, Is.EqualTo(Tone.Warn));
        }

        // The terms button cycles Cash, Half-season, Season, Cash; it sets the term for later sales.
        [Test]
        public void Terms_Button_Cycles()
        {
            Simulation sim = Situations.Invoicing();
            Business mill = Of(sim, "flour_mill"), bakery = Of(sim, "bakery");
            string[] expect = { "season", "cash", "half" };
            foreach (string term in expect)
            {
                ViewContext ctx = ViewKit2.Context(sim);
                LineVm line = BusinessSheet.Build(ctx, mill.Id, InvoiceSheet.Tab).Lines.First(l => l.Action != null && l.Action.Id == "terms:" + bakery.Id + ":");
                Assert.That(line.Action.Enabled, Is.True);
                Assert.That(sim.Execute(line.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
                Assert.That(sim.Config.Game.Invoices.Terms[InvoiceRules.TermOf(sim, mill.Id, bakery.Id, null)].Id, Is.EqualTo(term));
            }
        }

        // Before the Township there is no tab.
        [Test]
        public void No_Tab_Before_The_Township()
        {
            Simulation sim = Situations.Mill();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(BusinessSheet.Build(ctx, sim.State.Businesses.Businesses.Single().Id).Tabs.Any(t => t.Id == "tab:" + InvoiceSheet.Tab), Is.False);
        }

        // AC-4: the books show why cash is not profit: customers who have not paid, suppliers not paid, and the rest; the rows add up to the cash that moved.
        [Test]
        public void Books_Bridge()
        {
            Simulation sim = Situations.Invoicing();
            ViewContext ctx = ViewKit2.Context(sim);
            Business mill = Of(sim, "flour_mill");
            SheetVm books = BusinessSheet.Build(ctx, mill.Id, BusinessSheet.Books);
            Assert.That(books.Lines.Any(l => l.IsHeader && l.Label == ctx.Strings.Get("books.bridgeNow")), Is.True, "the bridge shows once a business trades on terms");
            long profit = BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeProfit);
            long sum = profit + BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeWear) + BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeCustomers)
                + BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeSuppliers) + BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeStock)
                + BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeTax);
            Assert.That(BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeRunning), Is.EqualTo(sum));
            BusinessReport r = BusinessBooks.Report(mill, sim.Period);
            Assert.That(BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeRunning) + BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeOther), Is.EqualTo(r.Closing - r.Opening), "running plus the rest is all the cash that moved");
            Assert.That(BusinessBooksSheet.Amount(ctx, mill, 0, BusinessBooksSheet.BridgeCustomers), Is.EqualTo(-mill.Ledger.Balance(Sim.Core.Finance.Account.OwedByBuyers)), "the mill is owed what it is owed");
            // A business that never traded on terms shows no bridge.
            Simulation plain = Situations.Mill();
            ViewContext pc = ViewKit2.Context(plain);
            Assert.That(BusinessSheet.Build(pc, plain.State.Businesses.Businesses.Single().Id, BusinessSheet.Books).Lines.Any(l => l.IsHeader && l.Label == pc.Strings.Get("books.bridgeNow")), Is.False);
        }
    }
}
