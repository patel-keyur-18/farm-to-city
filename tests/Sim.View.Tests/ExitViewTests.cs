using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-125 (spec 028 §3.3, §7, Preview 17): the worth in Settings, the Sell or expand sheet, the sale's card and its alert.</summary>
    public class ExitViewTests
    {
        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // Preview 17, screen 1: Settings has a Worth section with its ? and the way to Sell or expand.
        [Test]
        public void Settings_Has_The_Worth()
        {
            Simulation sim = Situations.Managed();
            ViewContext ctx = ViewKit2.Context(sim);
            Business bakery = Of(sim, "bakery");
            SheetVm set = BusinessSheet.Build(ctx, bakery.Id, BusinessSheet.Settings);
            Assert.That(set.Lines.Any(l => l.WhyKey == "businessWorth"), Is.True);
            LineVm sell = set.Lines.Single(l => l.Action != null && l.Action.Id == "sellExpand");
            Assert.That(sell.Action.Opens.Value, Is.EqualTo(new SheetRequest(ExitSheet.Kind, bakery.Id)));
        }

        // Preview 17, screen 2: worth and what it holds, List it and Rush it (each a hold), a branch with its count; listed, the days left and Cancel the sale.
        [Test]
        public void Sell_Or_Expand()
        {
            Simulation sim = Situations.Managed();
            ViewContext ctx = ViewKit2.Context(sim);
            Business mill = Of(sim, "flour_mill");
            SheetVm vm = ExitSheet.Build(ctx, mill.Id);
            Assert.That(vm.Lines.Any(l => l.WhyKey == "businessWorth") && vm.Lines.Any(l => l.WhyKey == "businessHolds"), Is.True);
            LineVm list = vm.Lines.Single(l => l.Action != null && l.Action.Id == "list"), rush = vm.Lines.Single(l => l.Action != null && l.Action.Id == "rush");
            Assert.That(ConfirmRules.Of(list.Action.Create(0)), Is.EqualTo(ConfirmKind.Hold), "selling is a hold: a tap can never sell your business");
            LineVm branch = vm.Lines.Single(l => l.WhyKey == "businessBranch");
            Assert.That(branch.Action.Opens.Value.Kind, Is.EqualTo("BusinessResearch"));
            Assert.That(branch.Value, Does.Contain("0 of 2 branches"));
            ConfirmCardVm card = ConfirmCards.For(ctx, list.Action.Create(sim.State.Clock.SimTime));
            Assert.That(card.Hold && card.Rows.Count == 3, Is.True);

            Assert.That(sim.Execute(list.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            SheetVm listed = ExitSheet.Build(ViewKit2.Context(sim), mill.Id);
            Assert.That(listed.Lines.Any(l => l.Action != null && l.Action.Id == "cancelSale"), Is.True);
            Assert.That(listed.Lines.Any(l => l.Action != null && l.Action.Id == "list"), Is.False);
            Assert.That(sim.Execute(listed.Lines.Single(l => l.Action != null && l.Action.Id == "cancelSale").Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(rush.Action.Enabled, Is.True);
        }

        // Preview 17, screen 3: the day it sells, a bell alert opens the card with the price, the tax and what you receive.
        [Test]
        public void Sale_Card_And_Alert()
        {
            Simulation sim = Situations.Managed();
            Business mill = Of(sim, "flour_mill");
            Assert.That(sim.Execute(new ListBusiness(sim.State.Clock.SimTime, mill.Id, true)).Accepted, Is.True);
            sim.AdvanceTo(sim.State.Clock.SimTime + 26 * 3600);
            Assert.That(mill.Status, Is.EqualTo(BusinessStatus.Closed));
            ViewContext ctx = ViewKit2.Context(sim);
            AlertVm alert = Alerts.Build(ctx).First(a => a.Action != null && a.Action.Opens.HasValue && a.Action.Opens.Value.Kind == ExitSheet.ReceiptKind);
            Assert.That(alert.Text, Does.Contain("flour mill").And.Contain("sold for"));
            SheetVm card = ExitSheet.Receipt(ctx, alert.Action.Opens.Value.Id);
            Assert.That(card.Title, Is.EqualTo("Your flour mill is sold"));
            Assert.That(card.Lines.Any(l => l.Label == "You receive"), Is.True);
            Assert.That(card.Lines.Count(l => l.WhyKey == "businessSale"), Is.GreaterThanOrEqualTo(3));
        }
    }
}
