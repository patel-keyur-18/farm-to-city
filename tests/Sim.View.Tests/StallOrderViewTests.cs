using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 030 §3.6, §7.5 (US-269, Preview 25): the crop list's row, the sell step, the hand's line, and the pick's Why?.</summary>
    public class StallOrderViewTests
    {
        /// <summary>The one farm field in the shops' town: a crop growing and Raju's order on it, which these tests replace.</summary>
        private static int FieldOf(Simulation sim) { return 86; }

        private static Business Veg(Simulation sim) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == "veg_stall"); }

        private static Hand HandOf(Simulation sim) { return sim.State.People.Hands[0]; }

        // The crop list: a row For your vegetable stall under Let him choose, with the vegetable he would sow, only while a stall is open.
        [Test]
        public void CropListRow()
        {
            Simulation sim = Situations.Shops();
            int Field = FieldOf(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            Business veg = Veg(sim);
            SowSheetVm crops = OrderSheet.Crops(ctx, Field, null);
            int at = crops.Lines.FindIndex(l => l.Action?.Id == "crop:auto");
            LineVm row = crops.Lines[at + 1];
            Assert.That((row.Label, row.Action.Id, row.Action.Label), Is.EqualTo(("For your vegetable stall", "crop:stall:" + veg.Id, "Choose")), "one stall: no place named");
            Assert.That(row.Value, Does.StartWith("Grows what the stall will lack tomorrow: "));
            Assert.That((row.WhyKey, row.WhyId), Is.EqualTo(("stallPick", veg.Id * 1000 + Field)));
            Assert.That(row.Action.Opens.Value.Arg, Is.EqualTo("stall:" + veg.Id + "+none"));

            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, veg.Id));
            Assert.That(OrderSheet.Crops(ViewKit2.Context(sim), Field, null).Lines.Any(l => l.Action != null && l.Action.Id.StartsWith("crop:stall:")), Is.False, "no open stall, no row");
        }

        // The sell step has one way, Keep in store, and sets an order that grows for the stall.
        [Test]
        public void SellStepKeepsInStore()
        {
            Simulation sim = Situations.Shops();
            int Field = FieldOf(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            Business veg = Veg(sim);
            string steps = "stall:" + veg.Id + "+none|every";
            SheetVm sheet = OrderSheet.Build(ctx, OrderTargetKind.Field, Field, steps);
            Assert.That(sheet.Subtitle, Does.StartWith("For your vegetable stall"));
            var set = sheet.Lines.Where(l => l.Action != null && l.Action.Id.StartsWith("set:")).ToList();
            Assert.That(set.Select(l => l.Action.Id), Is.EqualTo(new[] { "set:store" }), "no offer, no trader");
            Assert.That(set[0].Value, Does.Contain("6:00"));
            Ok(sim, set[0].Action.Create(sim.State.Clock.SimTime));
            StandingOrder o = sim.State.People.OrderFor(OrderTargetKind.Field, Field);
            Assert.That((o.ForBusiness, o.Sell, o.Once), Is.EqualTo((veg.Id, SellRule.Store, false)));
        }

        // The hand's sheet: the order reads For your vegetable stall, with a Stall preorder line and the stall's own ?; when the stall closes it says he chooses.
        [Test]
        public void HandSheetLine()
        {
            Simulation sim = Situations.Shops();
            int Field = FieldOf(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            Business veg = Veg(sim);
            Hand h = HandOf(sim);
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.ForStall(Field, veg.Id, "none")));
            LineVm line = HandSheet.Build(ctx, h.Id).Lines.Single(l => l.Label == PeopleWords.Target(ctx, OrderTargetKind.Field, Field));
            Assert.That(line.Value, Does.Contain("For your vegetable stall").And.Contain("Keep in store").And.Contain("Stall preorder: "));
            Assert.That((line.WhyKey, line.WhyId), Is.EqualTo(("stallPick", veg.Id * 1000 + Field)));
            WhyTests.AssertWords("stall pick", WhyView.Build(ctx, line.WhyKey, line.WhyId));
            Assert.That(FieldSheets.ForTile(ctx, Field).Lines.Single(l => l.WhyKey == "stallPick").Value, Does.Contain("Stall preorder: "), "the field's sheet too");

            Ok(sim, new CloseBusiness(sim.State.Clock.SimTime, veg.Id));
            ViewContext after = ViewKit2.Context(sim);
            LineVm gone = HandSheet.Build(after, h.Id).Lines.Single(l => l.Label == PeopleWords.Target(after, OrderTargetKind.Field, Field));
            Assert.That(gone.Value, Does.Contain("Let ").And.Contain("is gone, so ").And.Not.Contain("For your vegetable stall ·"));
            Assert.That(gone.WhyKey, Is.EqualTo("order"));
        }

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }
    }
}
