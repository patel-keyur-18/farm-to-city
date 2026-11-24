using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-246 (spec 028 §3.7, ADR 0163, Preview 18): the restaurant's research, plan, today, settings and customers; every number's Why?.</summary>
    public class RestaurantViewTests
    {
        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
                if (l.WhyKey == "meals" || l.WhyKey.StartsWith("plan") || l.WhyKey == "shopDemand") WhyTests.AssertWords(l.Label, why);
            }
        }

        private static int TypeOf(Simulation sim) { return sim.Config.Game.Businesses.IndexOfType("restaurant"); }

        private static Business Diner(Simulation sim) { return sim.State.Businesses.Businesses.Single(b => BusinessRules.TypeOf(sim, b).IsMeals); }

        // The Businesses sheet has its picture button from the Township.
        [Test]
        public void Menu_Button()
        {
            Simulation sim = Situations.Township();
            ActionVm start = BusinessesSheet.Build(ViewKit2.Context(sim)).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(a => a.Id == "start:restaurant");
            Assert.That(start.Label, Is.EqualTo("Start a restaurant"));
            Assert.That(start.Enabled, Is.True);
        }

        // Step 1: who eats out, what they want, the menu, the kitchen, the sittings, the rivals, your expected share, the rent and the wage: every row with a Why?.
        [Test]
        public void Research()
        {
            Simulation sim = Situations.Owned();
            SheetVm vm = BusinessResearchSheet.Build(ViewKit2.Context(sim), TypeOf(sim));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Households that eat out").And.Contain("Portions they'd eat out a day").And.Contain("Thali").And.Contain("Paratha plate")
                .And.Contain("The kitchen").And.Contain("Open for").And.Contain("The town dhaba").And.Contain("A cook's wage"));
            Assert.That(vm.Lines.First(l => l.Label == "Open for").Value, Is.EqualTo("Lunch 11:00–15:00, dinner 18:00–22:00"));
            Assert.That(vm.Lines.First(l => l.Label == "Thali").Value, Does.EndWith("coins"));
            AssertWhys(sim, vm);
        }

        // Step 2: the plan has sales, ingredients and gas; run by you there is no wage; both read in profit at the usual prices.
        [Test]
        public void Plan()
        {
            Simulation sim = Situations.Owned();
            SheetVm hired = BusinessPlanSheet.Build(ViewKit2.Context(sim), TypeOf(sim), false), mine = BusinessPlanSheet.Build(ViewKit2.Context(sim), TypeOf(sim), true);
            Assert.That(hired.Lines.Select(l => l.Label), Does.Contain("Thalis and paratha plates sold").And.Contain("Gas for the stove"));
            Assert.That(hired.Lines.Any(l => l.Label.Contains("wage")), Is.True, "hired, a cook's wage");
            Assert.That(mine.Lines.Any(l => l.Label.Contains("wage")), Is.False, "run by you there is none");
            AssertWhys(sim, hired);
            AssertWhys(sim, mine);
        }

        // Step 4: its card: portions today, stars, the menu with each dish's price and the diners' parts, the kitchen's rate and its sittings.
        [Test]
        public void Today()
        {
            Simulation sim = Situations.Owned();
            Business b = Diner(sim);
            SheetVm vm = BusinessSheet.Build(ViewKit2.Context(sim), b.Id);
            Assert.That(vm.Title, Is.EqualTo("Restaurant"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Sold today").And.Contain("Thali").And.Contain("Paratha plate").And.Contain("The kitchen"));
            Assert.That(vm.Lines.First(l => l.Label == "Sold today").Value, Does.EndWith("portions").Or.EndWith("portion"));
            Assert.That(vm.Lines.First(l => l.Label == "Thali").Note, Does.Contain("6 in 10 diners"));
            AssertWhys(sim, vm);
            AssertWhys(sim, BusinessSheet.Build(ViewKit2.Context(sim), b.Id, ShopSheet.Customers));
            AssertWhys(sim, BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Books));
        }

        // Settings: fixed sittings, no hours or market switch; a switch for each dish by the command; the last dish cannot go; its diners order the other.
        [Test]
        public void Dish_Switch()
        {
            Simulation sim = Situations.Owned();
            Business b = Diner(sim);
            SheetVm vm = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings);
            Assert.That(vm.Lines.Any(l => l.Label == "Open hours"), Is.False, "the sittings are fixed");
            Assert.That(vm.Lines.Any(l => l.Label != null && l.Label.StartsWith("Buy")), Is.False, "the kitchen buys by its own order: store, businesses, market");
            LineVm thali = vm.Lines.First(l => l.Label == "Thali");
            Assert.That(thali.Action.Create(sim.State.Clock.SimTime), Is.TypeOf<SetDish>());
            Assert.That(sim.Execute(thali.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(MealsRules.On(b, 0), Is.False);
            SheetVm after = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, BusinessSheet.Settings);
            Assert.That(after.Lines.First(l => l.Label == "Thali").Note, Does.Contain("Off the menu"));
            Assert.That(after.Lines.First(l => l.Label == "Paratha plate").Action, Is.Null, "the last dish stays on");
            Assert.That(after.Lines.First(l => l.Label == "Paratha plate").Note, Does.Contain("last dish"));
            AssertWhys(sim, after);
        }

        // The research of a business with a manager style, books and the plan line all read without a missing string.
        [Test]
        public void No_Missing_Strings()
        {
            Simulation sim = Situations.Owned();
            Business b = Diner(sim);
            foreach (string tab in new[] { BusinessSheet.Settings, BusinessSheet.Books, ShopSheet.Customers, InvoiceSheet.Tab, null })
            {
                SheetVm vm = BusinessSheet.Build(ViewKit2.Context(sim), b.Id, tab);
                foreach (LineVm l in vm.Lines) { Assert.That(l.Label ?? "", Does.Not.Contain("{"), tab + " " + l.Label); Assert.That(l.Value ?? "", Does.Not.Contain("{"), tab + " " + l.Label); }
            }
        }
    }
}
