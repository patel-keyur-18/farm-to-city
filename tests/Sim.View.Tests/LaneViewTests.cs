using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-253 (spec 025 §3.4, Preview 2): a lane acre's sheet before its section is built, the commission card and its guard, and every number's Why?.</summary>
    public class LaneViewTests
    {
        private static int Lane(Simulation sim) { return sim.Config.Game.Map.TileAt(2, 6); }

        private static void Fund(Simulation sim, long coins)
        {
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, coins * 100), new Posting(Account.GiftsFromFounder, -coins * 100));
        }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Title, Is.Not.Empty, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
            }
        }

        // Before the Township: the track's own words, and a grey button that says when it opens.
        [Test]
        public void Early()
        {
            Simulation sim = Situations.Town();
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), Lane(sim));
            Assert.That(vm.Title, Is.EqualTo("A track for a gravel lane"));
            ActionVm a = vm.Actions.Single(x => x.Id == "buildLane");
            Assert.That((a.Enabled, a.Detail), Is.EqualTo((false, "Opens with the Township")));
            Assert.That(vm.Lines.Any(l => l.WhyKey != null), Is.False, "no number before the town can build");
        }

        // The ready sheet: the cost, the town's money, how much of the lane is built; the button reads the cost.
        [Test]
        public void Ready()
        {
            Simulation sim = Situations.Township();
            Fund(sim, 500_000);
            ulong hash = sim.State.ComputeHash();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = FieldSheets.ForTile(ctx, Lane(sim));
            Assert.That(vm.Lines.Where(l => l.WhyKey != null).Select(l => l.Label), Is.EqualTo(new[] { "Cost, from the town's money", "The town's cash", "Built on this lane" }));
            Assert.That(vm.Lines.First(l => l.Label == "Built on this lane").Value, Is.EqualTo("0 of 4 sections"));
            long cost = Sim.Core.Market.MarketRules.Indexed(sim, 20_000 * 100);
            ActionVm go = vm.Actions.Single(a => a.Id == "buildLane");
            Assert.That((go.Enabled, go.Detail), Is.EqualTo((true, ctx.Fmt.Coins(cost) + ", from the town's money")));
            AssertWhys(sim, vm);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        // The town can't pay: grey, with the reason in the detail line, and a way to give.
        [Test]
        public void Poor()
        {
            Simulation sim = Situations.Township();
            long cash = sim.State.Treasury.Cash, need = Sim.Core.Market.MarketRules.Indexed(sim, 20_000 * 100);
            long drain = cash - need / 2; // down to half the cost
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.GiftsFromFounder, drain), new Posting(Account.Cash, -drain));
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), Lane(sim));
            ActionVm go = vm.Actions.Single(a => a.Id == "buildLane");
            Assert.That(go.Enabled, Is.False);
            Assert.That(go.Detail, Does.StartWith("The town has ").And.Contain(" and needs "));
            Assert.That(vm.Actions.Any(a => a.Id == "give"), Is.True);
        }

        // The card: the town's cash now, the section, the cash after; your own cash isn't touched; the guard speaks of the next close.
        [Test]
        public void Card()
        {
            Simulation sim = Situations.Township();
            Fund(sim, 500_000);
            ViewContext ctx = ViewKit2.Context(sim);
            int section = LaneRules.OfTile(sim, Lane(sim));
            ConfirmCardVm card = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, sim.Config.Game.Map.Lanes[section].Id));
            Assert.That(card.Title, Is.EqualTo("Build this lane section?"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "The town's cash now", "A section of gravel lane", "The town's cash after" }));
            Assert.That(card.CashChange, Is.EqualTo(0), "your own cash isn't touched");
            Assert.That(card.GoLabel, Is.EqualTo("Build for " + ctx.Fmt.Coins(LaneRules.Cost(sim, section))));
            Assert.That(card.Warning, Does.StartWith("Your own cash isn't touched.").And.Contain("at the next close"));
            Assert.That(card.Reassure, Is.EqualTo(card.Warning.Contains("can still pay")));
        }

        // The Why? of the cost shows the builder's price and today's prices; progress says what a built section does.
        [Test]
        public void Whys()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            int section = LaneRules.OfTile(sim, Lane(sim));
            WhyVm cost = WhyView.Build(ctx, "lane", section * 10 + LaneSheet.Cost);
            Assert.That(cost.Rows[0].Label, Is.EqualTo("The lane builder's price"));
            Assert.That(cost.Rows.Count, Is.GreaterThan(1));
            Assert.That(cost.Rows[0].Value, Is.EqualTo(ctx.Fmt.Coins(20_000 * 100)));
            WhyVm progress = WhyView.Build(ctx, "lane", section * 10 + LaneSheet.Progress);
            Assert.That(progress.Title, Is.EqualTo("Sections built on this lane"));
        }

        // A built lane acre is the plain "A gravel lane" sheet.
        [Test]
        public void Built()
        {
            Simulation sim = Situations.Township();
            Fund(sim, 500_000);
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "lane_w1")).Accepted, Is.True);
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), Lane(sim));
            Assert.That(vm.Title, Is.EqualTo("A gravel lane"));
            Assert.That(vm.Actions, Is.Empty);
        }
    }
}
