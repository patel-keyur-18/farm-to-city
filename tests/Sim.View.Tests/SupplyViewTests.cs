using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 030 §3.4, §3.5, §7.4 (US-268, Preview 24): the Supply tab, its amounts list, today's supply on the hub, and every number's Why?.</summary>
    public class SupplyViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // The tab is there for a stall; Automatic shows the mode row, a counted row for each good with "Takes all it needs" and no Change.
        [Test]
        public void TabAutomatic()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business veg = Of(sim, "veg_stall");
            SheetVm card = BusinessSheet.Build(ctx, veg.Id);
            Assert.That(card.Tabs.Select(t => t.Label), Does.Contain("Supply"));
            SheetVm tab = BusinessSheet.Build(ctx, veg.Id, SupplySheet.Tab);
            LineVm mode = tab.Lines.Single(l => l.WhyKey == "supplyMode");
            Assert.That((mode.Value, mode.Action.Label), Is.EqualTo(("Automatic", "Use Manual")));
            var goods = tab.Lines.Where(l => l.WhyKey == "supplyToday").ToList();
            Assert.That(goods, Is.Not.Empty);
            Assert.That(goods.All(l => l.Action == null && l.Note == "Takes all it needs" && l.Value.StartsWith("From you ")), Is.True);
            Assert.That(tab.Lines.Any(l => l.Label.Contains("market")), Is.True, "buying at the market moved here");
            Assert.That(BusinessSheet.Build(ctx, veg.Id, BusinessSheet.Settings).Lines.Any(l => l.Label.Contains("market")), Is.False, "and left Settings");
        }

        // Manual: each capped good has Change, opening the amounts list; the list marks the one in force and Choose sets the cap.
        [Test]
        public void ManualAndAmounts()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business veg = Of(sim, "veg_stall");
            int potato = sim.Config.Game.Market.IndexOf("potato");
            Ok(sim, new SetStallGoods(sim.State.Clock.SimTime, veg.Id, "potato", 16_000));
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, veg.Id, true));
            SheetVm tab = BusinessSheet.Build(ctx, veg.Id, SupplySheet.Tab);
            Assert.That(tab.Lines.Single(l => l.WhyKey == "supplyMode").Value, Is.EqualTo("Manual"));
            LineVm potatoRow = tab.Lines.Single(l => l.WhyKey == "supplyToday" && l.WhyId == BusinessWords.GoodId(veg.Id, potato));
            Assert.That(potatoRow.Action.Opens.Value.Kind, Is.EqualTo(SupplySheet.CapKind));

            SheetVm amounts = SupplySheet.Cap(ctx, veg.Id, potatoRow.Action.Opens.Value.Arg);
            Assert.That(amounts.Lines.Select(l => l.Label), Is.EqualTo(new[] { "All it needs", "Half of a day", "A quarter of a day", "5.0 kg a day", "10.0 kg a day", "None" }), "the steps under a day of 16 kg");
            Assert.That(amounts.Lines[0].Badge, Is.EqualTo("In force"));
            ActionVm half = amounts.Lines[1].Action;
            Assert.That(half.Enabled, Is.True);
            Ok(sim, half.Create(sim.State.Clock.SimTime));
            Assert.That(SupplySheet.Cap(ctx, veg.Id, potatoRow.Action.Opens.Value.Arg).Lines[1].Badge, Is.EqualTo("In force"));
            Assert.That(BusinessSheet.Build(ctx, veg.Id, SupplySheet.Tab).Lines.Single(l => l.WhyKey == "supplyToday" && l.WhyId == potatoRow.WhyId).Note, Is.EqualTo("Takes up to half of a day (8.0 kg)"));
        }

        // The restaurant has no day's amount: its list is All it needs, the kg steps and None, no shares.
        [Test]
        public void RestaurantAmounts()
        {
            Simulation sim = Situations.Owned();
            ViewContext ctx = ViewKit2.Context(sim);
            Business rest = sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).IsMeals);
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, rest.Id, true));
            int g = SupplyRules.CappedInputs(sim, BusinessRules.TypeOf(sim, rest))[0];
            SheetVm amounts = SupplySheet.Cap(ctx, rest.Id, BusinessWords.GoodId(rest.Id, g).ToString());
            Assert.That(amounts.Lines.Select(l => l.Label), Is.EqualTo(new[] { "All it needs", "5.0 kg a day", "10.0 kg a day", "20.0 kg a day", "50.0 kg a day", "None" }));
        }

        // The mill keeps its own wheat a day as a row on its Supply tab, the tea stall its milk a day, and neither has the mode row.
        [Test]
        public void MillAndTea()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business tea = Of(sim, "tea_stall");
            SheetVm tab = BusinessSheet.Build(ctx, tea.Id, SupplySheet.Tab);
            Assert.That(tab.Lines.Any(l => l.WhyKey == "supplyMode"), Is.False);
            Assert.That(tab.Lines.Any(l => l.Label == "Milk a day"), Is.True, "its milk a day");
            Assert.That(BusinessSheet.Build(ctx, tea.Id, BusinessSheet.Settings).Lines.Any(l => l.Label == "Milk a day"), Is.False, "left Settings");
        }

        // The hub's row line and every Why on the tab, the amounts list and the hub.
        [Test]
        public void HubLineAndWhys()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business veg = Of(sim, "veg_stall");
            Ok(sim, new SetSupplyMode(sim.State.Clock.SimTime, veg.Id, true));
            SheetVm hub = BusinessHub.Build(ctx);
            Assert.That(hub.Lines.Count(l => l.WhyKey == "supplyBusiness"), Is.GreaterThan(0));
            Assert.That(hub.Lines.First(l => l.WhyKey == "supplyBusiness").Label, Does.StartWith("Today: "));
            var sheets = new[] { BusinessSheet.Build(ctx, veg.Id, SupplySheet.Tab), hub, BusinessSheet.Build(ctx, Of(sim, "tea_stall").Id, SupplySheet.Tab) };
            foreach (LineVm l in sheets.SelectMany(x => x.Lines).Where(l => l.WhyKey != null && l.WhyKey.StartsWith("supply")))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.WhyKey);
                WhyTests.AssertWords(l.WhyKey + " " + l.WhyId + " / " + l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
            }
            int potato = sim.Config.Game.Market.IndexOf("potato");
            SheetVm amounts = SupplySheet.Cap(ctx, veg.Id, BusinessWords.GoodId(veg.Id, potato).ToString());
            foreach (LineVm l in amounts.Lines) WhyTests.AssertWords("cap " + l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
        }
    }
}
