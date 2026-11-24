using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-260 (spec 028 §3.10, Preview 21): the kirana's, marketing's and khata's sheets; every number's Why?.</summary>
    public class GrowthViewTests
    {
        private static Business Shop(Simulation sim) { return sim.State.Businesses.Businesses.Single(b => BusinessRules.TypeOf(sim, b).Kind == ShopKind.Grocery); }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
                if (l.WhyKey.StartsWith("growth") || l.WhyKey.StartsWith("plan") || l.WhyKey == "shopDemand") WhyTests.AssertWords(l.Label, why);
            }
        }

        // The Businesses sheet has its picture button from the Township; the research and plan read like the other shops'.
        [Test]
        public void Plan()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(BusinessesSheet.Build(ctx).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(a => a.Id == "start:kirana").Label, Is.EqualTo("Start a kirana"));
            int type = sim.Config.Game.Businesses.IndexOfType("kirana");
            SheetVm research = BusinessResearchSheet.Build(ctx, type);
            Assert.That(research.Lines.Select(l => l.Label), Does.Contain("Groceries they buy a day").And.Contain("The village grocer's staples").And.Contain("A shopkeeper's wage"));
            AssertWhys(sim, research);
            SheetVm hired = BusinessPlanSheet.Build(ctx, type, false);
            Assert.That(hired.Lines.Select(l => l.Label), Does.Contain("Atta, rice, lentils, ghee and the rest sold"));
            AssertWhys(sim, hired);
        }

        // Today: the shelf, the stars, marketing and khata a line each, each with a Why?.
        [Test]
        public void Today_Settings_Customers_Books()
        {
            Simulation sim = Situations.Kirana();
            Business shop = Shop(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm today = BusinessSheet.Build(ctx, shop.Id, BusinessSheet.Today);
            Assert.That(today.Lines.Select(l => l.Label), Does.Contain("Marketing").And.Contain("Khata").And.Contain("Atta").And.Contain("Stars"));
            Assert.That(today.Lines.First(l => l.Label == "Khata").Value, Is.EqualTo("on"));
            AssertWhys(sim, today);

            SheetVm settings = BusinessSheet.Build(ctx, shop.Id, BusinessSheet.Settings);
            var buttons = settings.Lines.Where(l => l.Buttons != null).Select(l => l.Buttons).ToList();
            Assert.That(buttons[0].Select(a => a.Id), Is.EqualTo(new[] { "marketing:0", "marketing:200000", "marketing:400000", "marketing:800000" }), "none, 2,000, 4,000 and 8,000");
            Assert.That(buttons[0].Single(a => a.Chosen).Id, Is.EqualTo("marketing:200000"), "the one you have is marked");
            Assert.That(buttons[1].Select(a => a.Id), Is.EqualTo(new[] { "limit:50000", "limit:100000", "limit:200000" }), "500, 1,000 and 2,000");
            Assert.That(buttons[0].Single(a => a.Id == "marketing:800000").Create(sim.State.Clock.SimTime), Is.TypeOf<SetMarketing>());
            Assert.That(settings.Lines.First(l => l.Action != null && l.Action.Id == "khata").Action.Create(sim.State.Clock.SimTime), Is.TypeOf<SetKhata>());
            Assert.That(settings.Lines.Any(l => l.Label == "Quality"), Is.False, "a kirana sells one grade");
            AssertWhys(sim, settings);

            SheetVm customers = BusinessSheet.Build(ctx, shop.Id, ShopSheet.Customers);
            Assert.That(customers.Lines.Select(l => l.Label), Does.Contain("of which marketing").And.Contain("of which khata").And.Contain("The village grocer's staples reacts"));
            AssertWhys(sim, customers);

            SheetVm books = BusinessSheet.Build(ctx, shop.Id, BusinessSheet.Books);
            Assert.That(books.Lines.Select(l => l.Label), Does.Contain("Owed to the shop").And.Contain("Bad debt, last close").And.Contain("Households that owe").And.Contain("Marketing"));
            LineVm owing = books.Lines.First(l => l.Action != null && l.Action.Id.StartsWith("cut:"));
            Assert.That(owing.Action.Create(sim.State.Clock.SimTime), Is.TypeOf<CutOffHousehold>());
            AssertWhys(sim, books);
        }

        // A rise in marketing spends the shop's money now: a tap card says what it adds and what is paid; the budget you already have, or none, asks nothing.
        [Test]
        public void Marketing_Card()
        {
            Simulation sim = Situations.Kirana();
            Business shop = Shop(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            long t = sim.State.Clock.SimTime;
            ConfirmCardVm card = ConfirmCards.For(ctx, new SetMarketing(t, shop.Id, 8_000 * 100));
            Assert.That(card, Is.Not.Null);
            Assert.That((card.Title, card.Hold), Is.EqualTo(("Spend on marketing?", false)));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Adds to your score", "Paid again at each close, a season", "The shop pays now (a rise pays the whole difference for this season, however late)" }));
            Assert.That(card.Rows[2].Total, Is.True);
            Assert.That(ConfirmCards.For(ctx, new SetMarketing(t, shop.Id, 2_000 * 100)), Is.Null, "the budget it already has is paid for this season");
            Assert.That(ConfirmCards.For(ctx, new SetMarketing(t, shop.Id, 0)), Is.Null, "none spends nothing");
        }

        // A household cut off says so and offers Allow again.
        [Test]
        public void Cut_Off_Row()
        {
            Simulation sim = Situations.Kirana();
            Business shop = Shop(sim);
            KhataAccount a = shop.Credit.First();
            Assert.That(sim.Execute(new CutOffHousehold(sim.State.Clock.SimTime, shop.Id, a.Household, true)).Accepted, Is.True);
            SheetVm books = BusinessSheet.Build(ViewKit2.Context(sim), shop.Id, BusinessSheet.Books);
            LineVm row = books.Lines.First(l => l.Action != null && l.Action.Id == "cut:" + a.Household);
            Assert.That((row.Note, row.Action.Label), Is.EqualTo(("Cut off by you", "Allow again")));
        }
    }
}
