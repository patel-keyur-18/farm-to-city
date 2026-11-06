using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 013 §7 (US-129, ADR 0090): plant an orchard from the field sheet, sow only between young rows, pick a batch in parts and sell the rest on the trees.</summary>
    public class OrchardViewTests
    {
        private const int Field = 84;

        private static void Era(Simulation sim, string era)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == era) sim.State.Era.Current = i;
        }

        private static ActionVm Action(SheetVm sheet, string id) { return sheet.Actions.Single(a => a.Id == id); }

        [Test]
        public void FieldCard()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
            ViewContext ctx = ViewKit2.Context(sim);
            ActionVm plant = Action(FieldSheets.ForTile(ctx, Field), "orchard");
            Assert.That((plant.Enabled, plant.DisabledReason), Is.EqualTo((false, "Comes once you're a Hamlet")));

            Era(sim, "hamlet");
            ctx = ViewKit2.Context(sim);
            SheetVm sheet = OrchardSheet.Build(ctx, Field);
            Assert.That(sheet.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Banana", "Papaya", "Guava", "Mango" }));
            Assert.That(sheet.Lines.All(l => WhyView.Has(l.WhyKey) && WhyView.Build(ctx, l.WhyKey, l.WhyId) != null), Is.True);
            LineVm banana = sheet.Lines.First();
            Assert.That(banana.Value, Does.StartWith("400 trees · fruits in"));
            Assert.That(sim.Execute(banana.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);

            // Young trees: the field sheet is the orchard's, and the sow sheet lists the short crops only.
            ctx = ViewKit2.Context(sim);
            sheet = FieldSheets.ForTile(ctx, Field);
            Assert.That((sheet.Title, sheet.Subtitle), Is.EqualTo(("Banana orchard", "400 young trees")));
            Assert.That(sheet.Actions.Select(a => a.Id), Is.EqualTo(new[] { "sow", "clearOrchard" }));
            Assert.That(Action(sheet, "sow").Label, Is.EqualTo("Sow between the rows"));
            Assert.That(Action(sheet, "clearOrchard").Confirm, Is.Not.Null, "its cost is lost: asked first");
            SowSheetVm sow = SowSheet.Build(ctx, Field);
            Assert.That(sow.Rows.Select(r => r.CropId), Is.SubsetOf(new[] { "radish", "methi", "amaranth", "lentils", "peas" }).And.Not.Empty);
            Assert.That(sow.Rows.Single(r => r.CropId == "radish").LabourText, Does.Contain("1.5 h"), "half the work between the rows");
            Assert.That(sow.NoteText, Is.EqualTo("Between young banana trees: 50% of the yield and of the work."));

            // Between hourly passes the next batch so far is the rates since the last accrual, not zero (a fresh orchard isn't "Poor").
            sim.AdvanceTo(sim.State.Clock.SimTime / 3600 * 3600 + 1800);
            WhyVm soFar = WhyView.Build(ViewKit2.Context(sim), "orchardBatch", Field);
            Assert.That(soFar.Rows.Where(r => r.Value != null && r.Value.EndsWith("less")).Select(r => r.Value), Has.None.EqualTo("100% less"));

            // Ripe: more than a day can move. Pick what can go today, then sell the rest on the trees.
            Orchard o = OrchardRules.Of(sim, Field);
            long t = sim.State.Clock.SimTime + sim.Config.Game.Farm.Trees[o.Def].GrowthSeconds + 1;
            while (o.RipeGrams == 0) sim.AdvanceTo(t += sim.Clock.SecondsPerSeason / 4);
            sim.AdvanceTo((sim.State.Clock.SimTime / 86400 + 1) * 86400 + 10 * 3600); // the truck is in
            ctx = ViewKit2.Context(sim);
            sheet = FieldSheets.ForTile(ctx, Field);
            Assert.That(sheet.Subtitle, Is.EqualTo("400 trees, fruit is ripe"));
            foreach (LineVm l in sheet.Lines.Where(l => l.WhyKey != null)) Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId), Is.Not.Null, l.WhyKey);
            Assert.That(sheet.Actions.Select(a => a.Id), Is.EqualTo(new[] { "pickOrchard", "sellOnTrees", "clearOrchard" }));
            Assert.That(Action(sheet, "sellOnTrees").Label, Is.EqualTo("Sell it all on the trees"));
            Assert.That(sim.Execute(Action(sheet, "pickOrchard").Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            ctx = ViewKit2.Context(sim);
            sheet = FieldSheets.ForTile(ctx, Field);
            Assert.That(o.RipeGrams, Is.GreaterThan(0), "the rest waits on the trees");
            ActionVm pick = Action(sheet, "pickOrchard");
            Assert.That((pick.Enabled, pick.DisabledReason), Is.EqualTo((false, "The trader can't take any more today.")));
            ActionVm sell = Action(sheet, "sellOnTrees");
            Assert.That(sell.Label, Is.EqualTo("Sell the rest on the trees"), "some picked: the contractor takes the rest");
            Assert.That(sell.Detail, Is.EqualTo(ctx.Fmt.Coins(OrchardRules.ContractorPrice(sim, o))));
            WhyVm why = WhyView.Build(ctx, "orchardContractor", Field);
            Assert.That(why.Rows.Select(r => r.Value), Does.Contain("70%"));
            Assert.That(sim.Execute(sell.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(o.RipeGrams, Is.Zero);
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), Field).Actions.Select(a => a.Id), Is.EqualTo(new[] { "clearOrchard" }), "bearing: nothing to sow");
        }
    }
}
