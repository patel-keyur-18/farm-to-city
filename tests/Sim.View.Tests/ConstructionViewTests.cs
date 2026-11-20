using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-145 (spec 025 §3.2, Preview 2): the cards' rows and timing, a site's sheet, Building materials, the masons' tab, the alert, and every number's Why?.</summary>
    public class ConstructionViewTests
    {
        private static Simulation Township()
        {
            Simulation sim = Situations.Township();
            Situations.SetCash(sim, 600_000_00);
            return sim;
        }

        /// <summary>The trader's day begins at his hour: the stock is full and what waits for it draws its bricks.</summary>
        private static void ToRestock(Simulation sim) { sim.AdvanceTo(sim.State.Clock.SimTime + Sim.Core.Town.ConstructionRules.SecondsToRestock(sim) + 60); }

        private static int FreeTile(Simulation sim)
        {
            foreach (Tile t in sim.State.Land.Tiles) if (HouseRules.Place(sim, t.Id) == RejectReason.None) return t.Id;
            Assert.Fail("no tile for a house");
            return -1;
        }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
            }
        }

        // The house card: cash now, materials, masons (or the gang), cash after; and when it will be ready, in words.
        [Test]
        public void House_Card()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            int tile = FreeTile(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new BuildFamilyHouse(ctx.Now, tile));
            Assert.That(card, Is.Not.Null);
            BuildQuote q = ConstructionRules.Quote(sim, "family_house", BuildProject.Yours, 1);
            Assert.That(card.Rows.Select(r => r.Label).First(), Is.EqualTo("Your cash now"));
            Assert.That(card.Rows.Select(r => r.Label), Does.Contain("Materials: bricks, cement, timber"));
            Assert.That(card.Rows.Select(r => r.Label).Any(l => l.StartsWith(q.Gang ? "The gang: 20 days" : "Masons: 20 days")), Is.True);
            Assert.That(card.Rows.Last().Label, Is.EqualTo("Your cash after"));
            Assert.That(-card.CashChange, Is.EqualTo(q.Total));
            Assert.That(card.GoLabel, Does.Contain("Build"));
            Assert.That(card.Warning, Does.Contain("ready in about").And.Contain("Your money is in the building from today."));
            Assert.That(card.Warning, Does.Contain("bricks"), "2,400 bricks against the day's 2,000: it waits for them");
        }

        // Tap the acre while it waits: what it waits for, what it has, its masons and the date; the sheet's button opens Building materials.
        [Test]
        public void Site_Sheet_Then_Materials()
        {
            Simulation sim = Township();
            int tile = FreeTile(sim);
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, tile)).Accepted, Is.True);
            ToRestock(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = FieldSheets.ForTile(ctx, tile);
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Status").And.Contain("Bricks").And.Contain("Cement").And.Contain("Timber").And.Contain("Ready in"));
            Assert.That(vm.Lines.First(l => l.Label == "Status").Value, Is.EqualTo("Waiting for bricks"));
            Assert.That(vm.Lines.First(l => l.Label == "Bricks").Value, Is.EqualTo("2,000 of 2,400 bricks"));
            Assert.That(vm.Actions.Any(a => a.Id == "materials" && a.Opens.Value.Kind == BuildingSheet.Kind), Is.True);
            AssertWhys(sim, vm);

            SheetVm mats = BuildingSheet.Build(ctx);
            Assert.That(mats.Title, Is.EqualTo("Building materials"));
            Assert.That(mats.Lines.First(l => l.Label == "Bricks").Value, Is.EqualTo("0 of 2,000 bricks left"));
            Assert.That(mats.Lines.Any(l => l.Label.Contains("Your family house") && l.Value == "Waiting for bricks"), Is.True);
            AssertWhys(sim, mats);
        }

        // A waiting build raises a note on the bell, with a button to the materials.
        [Test]
        public void Alert_While_Waiting()
        {
            Simulation sim = Township();
            Assert.That(sim.Execute(new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim))).Accepted, Is.True);
            AlertVm a = Alerts.Build(ViewKit2.Context(sim)).Single(x => x.Text.Contains("waiting for"));
            Assert.That(a.Text, Is.EqualTo("Your family house is waiting for bricks."));
            Assert.That(a.Action.Opens.Value.Kind, Is.EqualTo(BuildingSheet.Kind));
        }

        // The civic plot while its building is made: the town's own words and the same site rows.
        [Test]
        public void Civic_Site()
        {
            Simulation sim = Township();
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "community_hall")).Accepted, Is.True);
            ToRestock(sim);
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), CivicRules.Def(sim, CivicDef.Hall).Tile);
            Assert.That(vm.Subtitle, Is.EqualTo("The town is building it from its own money."));
            Assert.That(vm.Lines.First(l => l.Label == "Bricks").Value, Is.EqualTo("2,000 of 4,000 bricks"));
            AssertWhys(sim, vm);
            WhyVm cost = WhyView.Build(ViewKit2.Context(sim), "civic", CivicDef.Hall * 100 + CivicSheet.Cost);
            Assert.That(cost.Rows.Select(r => r.Label).First(), Is.EqualTo("Materials at the trader's prices"));
        }

        // The hiring board has a Masons tab: who they are and whether they are free, and what a mason's pay is.
        [Test]
        public void Board_Masons()
        {
            Simulation sim = Township();
            Assert.That(HiringBoard.Places(sim), Does.Contain(-1L));
            SheetVm vm = HiringBoard.Build(ViewKit2.Context(sim), -1);
            Assert.That(vm.Tabs.Select(t => t.Label), Does.Contain("Masons"));
            Assert.That(vm.Lines.Any(l => l.Label.StartsWith("A mason is a grown-up")), Is.True);
            int masons = ConstructionRules.AllMasons(sim).Count;
            Assert.That(vm.Lines.Count(l => l.Value == "free" || (l.Value != null && l.Value.StartsWith("kept for"))), Is.EqualTo(masons));
        }

        // Before the Township there is no building sheet to open and no Menu row; from it the Menu has Building.
        [Test]
        public void Menu_And_Before()
        {
            Simulation before = Situations.Town();
            Assert.That(MenuSheet.Build(ViewKit2.Context(before)).Lines.Any(l => l.Label == "Building"), Is.False);
            Assert.That(BuildingSheet.Build(ViewKit2.Context(before)).Subtitle, Does.Contain("from the Township"));
            Assert.That(MenuSheet.Build(ViewKit2.Context(Township())).Lines.Any(l => l.Label == "Building"), Is.True);
        }
    }
}
