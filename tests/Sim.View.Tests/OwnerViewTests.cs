using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-247 (spec 028 §3.4, Preview 17 screens 1 and 4 to 6): the owners row, the owners' sheet, an offer's card, the bell.</summary>
    public class OwnerViewTests
    {
        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // Screen 1: Settings has an Owners row (You 85% · the investor 15%) that opens the owners' sheet; a waiting offer is flagged.
        [Test]
        public void Settings_Has_The_Owners()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            Business bakery = Of(sim, "bakery");
            LineVm row = BusinessSheet.Build(ctx, bakery.Id, BusinessSheet.Settings).Lines.Single(l => l.WhyKey == "ownerRow");
            Assert.That(row.Value, Is.EqualTo("You 85% · Ramesh Seth 15%"));
            Assert.That(row.Action.Opens.Value, Is.EqualTo(new SheetRequest(OwnerSheet.OwnersKind, bakery.Id)));
            Assert.That(row.Note, Does.Contain("offer waits"), "a rival's offer for the bakery is waiting");
            LineVm plain = BusinessSheet.Build(ctx, Of(sim, "flour_mill").Id, BusinessSheet.Settings).Lines.Single(l => l.WhyKey == "ownerRow");
            Assert.That(plain.Value, Is.EqualTo("You 100%"));
        }

        // Screen 5: one row for you and one for each outside owner with what was put in and paid; Buy back asks a tap with the price; the limit bar.
        [Test]
        public void Owners_Sheet_And_Buy_Back()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            Business bakery = Of(sim, "bakery");
            SheetVm vm = OwnerSheet.Owners(ctx, bakery.Id);
            Assert.That(vm.Lines.Any(l => l.Label == "You" && l.Value == "85%"), Is.True);
            LineVm his = vm.Lines.Single(l => l.Label.StartsWith("Ramesh Seth"));
            Assert.That((his.Value, his.WhyKey), Is.EqualTo(("15%", "ownerPaid")));
            Assert.That(his.Note, Does.Contain("Put in").And.Contain("paid at the last close"));
            Assert.That(his.Action.Enabled, Is.True, "five seasons on");
            Assert.That(ConfirmRules.Of(his.Action.Create(sim.State.Clock.SimTime)), Is.EqualTo(ConfirmKind.Tap), "buying back spends: a tap with the price");
            Assert.That(ConfirmCards.For(ctx, his.Action.Create(sim.State.Clock.SimTime)).CashChange, Is.LessThan(0));
            Assert.That(vm.Lines.Any(l => l.WhyKey == "partnerOffer" && l.Value.Contains("20%")), Is.True, "none yet: one can join");
            LineVm limit = vm.Lines.Single(l => l.WhyKey == "outsideLimit");
            Assert.That((limit.Value, limit.Meter), Is.EqualTo(("15% of 40%", 37)));
            Assert.That(sim.Execute(his.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(OwnerSheet.Owners(ViewKit2.Context(sim), bakery.Id).Lines.Any(l => l.Label.StartsWith("Ramesh Seth")), Is.False, "bought back");

            bakery.Stakes.Clear();
            Assert.That(OwnerSheet.Owners(ViewKit2.Context(sim), bakery.Id).Lines.Any(l => l.WhyKey == "investorOffer"), Is.True);
        }

        // Too early: an investor's share says how long, greyed with the sim's own reason.
        [Test]
        public void Buy_Back_Too_Early()
        {
            Simulation sim = Situations.Owned();
            Business bakery = Of(sim, "bakery");
            bakery.Stakes[0].Period = sim.Period;
            LineVm his = OwnerSheet.Owners(ViewKit2.Context(sim), bakery.Id).Lines.Single(l => l.Label.StartsWith("Ramesh Seth"));
            Assert.That(his.Action.Enabled, Is.False);
            Assert.That(his.Action.DisabledReason, Does.Contain("four seasons"));
            Assert.That(his.Action.Detail, Does.Contain("more seasons"));
        }

        // Screens 4 and 6: an investor's card, a partner's and a rival's, each with its numbers, the answer that happens if you wait and a timer; selling is a hold.
        [Test]
        public void Cards()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            var offers = sim.State.Township.Exit.Offers;
            DealOffer inv = offers.Single(o => o.Kind == DealKind.Investor), partner = offers.Single(o => o.Kind == DealKind.Partner), buy = offers.Single(o => o.Kind == DealKind.BuyOut);

            SheetVm card = OwnerSheet.Card(ctx, inv.Id);
            Assert.That(card.Title, Is.EqualTo("An investor"));
            Assert.That(card.Lines[0].Label, Does.Contain("Imran Khan").And.Contain("20%"));
            Assert.That(card.Lines.Any(l => l.Label.StartsWith("Cash into") && l.Value.StartsWith("+71,550")), Is.True, "the cash that comes in");
            Assert.That(card.Lines.Any(l => l.Tag == "If you wait" && l.Action.Id == "deal:decline"), Is.True, "declining is the free default");
            LineVm take = card.Lines.Single(l => l.Action != null && l.Action.Id == "deal:accept");
            Assert.That(take.Action.Create(0), Is.TypeOf<AcceptDeal>());
            Assert.That(card.Lines.Last().Label, Does.Contain("no, thank you in"));

            SheetVm pc = OwnerSheet.Card(ctx, partner.Id);
            Assert.That(pc.Title, Is.EqualTo("A partner"));
            Assert.That(pc.Lines.Any(l => l.Label.StartsWith("Their stake")), Is.True);

            SheetVm bc = OwnerSheet.Card(ctx, buy.Id);
            Assert.That(bc.Title, Does.StartWith("An offer for your"));
            Assert.That(bc.Lines.Any(l => l.Label == "You would receive"), Is.True, "every coin before you decide");
            Assert.That(bc.Lines.Any(l => l.Label == "Outside owners are paid first" && l.Value.StartsWith("−")), Is.True, "the investor is paid first");
            Assert.That(ConfirmRules.Of(bc.Lines.Single(l => l.Action != null && l.Action.Id == "deal:accept").Action.Create(0)), Is.EqualTo(ConfirmKind.Hold), "accepting is a sale");
        }

        // The bell: one alert for each offer with its time to answer, opening its card; answering it clears it.
        [Test]
        public void Bell_And_Answer()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            var alerts = Alerts.Build(ctx).Where(a => a.Action != null && a.Action.Opens.HasValue && a.Action.Opens.Value.Kind == OwnerSheet.CardKind).ToList();
            Assert.That(alerts.Count, Is.EqualTo(3));
            Assert.That(alerts.Any(a => a.Text.StartsWith("An investor offers money for your flour mill")), Is.True);
            DealOffer partner = sim.State.Township.Exit.Offers.Single(o => o.Kind == DealKind.Partner);
            Assert.That(sim.Execute(OwnerSheet.Card(ctx, partner.Id).Lines.Single(l => l.Action != null && l.Action.Id == "deal:decline").Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).Count(a => a.Action != null && a.Action.Opens.HasValue && a.Action.Opens.Value.Kind == OwnerSheet.CardKind), Is.EqualTo(2));
            Assert.That(OwnerSheet.Card(ViewKit2.Context(sim), partner.Id).Title, Is.EqualTo("That offer is gone."), "answered");
        }

        // The sale receipt lists what the outside owners were paid first.
        [Test]
        public void Receipt_Lists_Outside()
        {
            Simulation sim = Situations.Owned();
            Business bakery = Of(sim, "bakery");
            Assert.That(sim.Execute(new AcceptBuyOut(sim.State.Clock.SimTime, sim.State.Township.Exit.Offers.Single(o => o.Kind == DealKind.BuyOut).Id)).Accepted, Is.True);
            SaleRecord rec = sim.State.Township.Exit.Sales.Single();
            SheetVm card = ExitSheet.Receipt(ViewKit2.Context(sim), rec.Id);
            Assert.That(card.Lines.Any(l => l.Label == "Paid first to outside owners" && l.Value.StartsWith("−")), Is.True);
            Assert.That(bakery.Status, Is.EqualTo(BusinessStatus.Closed));
        }
    }
}
