using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Livestock;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-254 (spec 025 §3.8, Preview 8): the shed sheet's Calves block, a kept heifer's row and card, her Why? pages, and the away card.</summary>
    public class HeiferViewTests
    {
        private const long Hour = 3600;

        /// <summary>A Hamlet with a shed and one cow, every calf a heifer; at the day before she calves.</summary>
        private static Simulation Dairy()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "livestock.json" ? j.Replace("\"share\": 0.5", "\"share\": 1") : j);
            sim.State.Era.Current = sim.Config.Game.Livestock.UnlockEra;
            sim.State.Ledger.Post(0, sim.Period, "ledger.opening", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 10_00_000_00),
                new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.OpeningEquity, -10_00_000_00));
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildShed(t)).Accepted, Is.True);
            sim.AdvanceTo(t + 25 * Hour);
            Assert.That(sim.Execute(new BuyAnimal(sim.State.Clock.SimTime, "cow")).Accepted, Is.True);
            return sim;
        }

        private static void Mornings(Simulation sim, int n)
        {
            for (int i = 0; i < n; i++)
            {
                long t = sim.State.Clock.SimTime, six = 6 * Hour, day = t / (24 * Hour) * (24 * Hour);
                sim.AdvanceTo(t < day + six ? day + six : day + 24 * Hour + six);
            }
        }

        private static ActionVm Button(SheetVm vm, string id) { return vm.Lines.Where(l => l.Action != null).Select(l => l.Action).FirstOrDefault(a => a.Id == id); }

        private static void WhysBuild(ViewContext ctx, SheetVm vm)
        {
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.WhyKey);
                Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId).Title, Is.Not.Empty, l.WhyKey + " " + l.WhyId);
            }
        }

        [Test]
        public void ShedSheet_HasTheCalvesSwitch_AndTheHonestCost()
        {
            Simulation sim = Dairy();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = ShedSheet.Build(ctx);
            Assert.That(vm.Lines.Any(l => l.IsHeader && l.Label == "Calves"), Is.True);
            LineVm row = vm.Lines.Single(l => l.Label == "Heifer calves");
            Assert.That(row.Value, Is.EqualTo("Sold at the fair"));
            Assert.That(row.Action.Label, Is.EqualTo("Keep heifer calves"));
            LineVm cow = vm.Lines.Single(l => l.Label == "Raising a cow");
            Assert.That(cow.Value, Does.Contain("on your own fodder").And.Contain("bought"));
            Assert.That(cow.Note, Does.Contain("8 seasons").And.Contain("costs").And.Contain("at the fair"));
            Assert.That(vm.Lines.Single(l => l.Label == "Raising a buffalo").Note, Does.Contain("10 seasons"));
            WhysBuild(ctx, vm);

            Assert.That(sim.Execute(row.Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            vm = ShedSheet.Build(ViewKit2.Context(sim));
            row = vm.Lines.Single(l => l.Label == "Heifer calves");
            Assert.That((row.Value, row.Action.Label), Is.EqualTo(("Kept", "Sell calves at the fair")));
            Assert.That(row.Note, Does.Contain("40%").And.Contain("8 to 10 seasons").And.Contain("Bull calves are sold"));
        }

        [Test]
        public void AKeptHeifer_HasARow_AndACard_WithEveryWhy()
        {
            Simulation sim = Dairy();
            Assert.That(sim.Execute(new SetKeepHeifers(sim.State.Clock.SimTime, true)).Accepted, Is.True);
            Mornings(sim, 16);
            Animal h = sim.State.Livestock.Animals.Single(a => a.Stage == AnimalStage.Heifer);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm shed = ShedSheet.Build(ctx);
            LineVm row = shed.Lines.Single(l => l.Label != null && l.Label.EndsWith("'s heifer · " + h.Name));
            Assert.That(row.Value, Does.StartWith("Heifer, grown in"));
            Assert.That(row.Note, Does.StartWith("Worth "));
            Assert.That(row.Opens.Value.Kind, Is.EqualTo("Animal"));
            Assert.That(shed.Subtitle, Is.EqualTo("2 of 4 places").Or.EqualTo("2 of 4 stalls"));
            WhysBuild(ctx, shed);

            SheetVm card = AnimalSheet.Build(ctx, h.Id);
            Assert.That(card.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Growing", "Fed this morning", "Health", "Vaccinated", "Worth now", "At the fair today" }));
            Assert.That(card.Lines.Single(l => l.Label == "Worth now").Note, Does.Contain("when grown"));
            Assert.That(card.Actions.Single().Label, Is.EqualTo("Sell at the fair"));
            WhysBuild(ctx, card);
            Assert.That(WhyView.Build(ctx, "heiferWorth", h.Id).Rows.Count, Is.EqualTo(5));
            Assert.That(WhyView.Build(ctx, "heiferKeep", 0).Rows.Count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void ANotFedHeifer_SaysSheIsWaiting()
        {
            Simulation sim = Dairy();
            Assert.That(sim.Execute(new SetKeepHeifers(sim.State.Clock.SimTime, true)).Accepted, Is.True);
            Mornings(sim, 17); // no straw or feed in the store: hungry
            Animal h = sim.State.Livestock.Animals.Single(a => a.Stage == AnimalStage.Heifer);
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm growing = AnimalSheet.Build(ctx, h.Id).Lines.Single(l => l.Label == "Growing");
            Assert.That(growing.Value, Is.EqualTo("Waiting: not fed enough"));
            Assert.That(growing.Note, Does.Contain("is needed for her to grow"));
            Assert.That(WhyView.Build(ctx, "heiferGrowing", h.Id).Rows.Any(r => r.Label.Contains("tomorrow does not count")), Is.True);
        }

        [Test]
        public void TheMap_DrawsAHeiferYoung()
        {
            Simulation sim = Dairy();
            Assert.That(sim.Execute(new SetKeepHeifers(sim.State.Clock.SimTime, true)).Accepted, Is.True);
            Mornings(sim, 16);
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.Cattle.Animals.Select(a => a.Young).OrderBy(y => y), Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void TheSeasonPage_CallsHerdGrowthNotCash_NotAPurchase()
        {
            Simulation sim = Dairy();
            Assert.That(sim.Execute(new SetKeepHeifers(sim.State.Clock.SimTime, true)).Accepted, Is.True);
            Mornings(sim, 16);
            SeasonVm page = SeasonBook.Build(ViewKit2.Context(sim), false);
            SeasonLineVm earned = page.Earned.Single(l => l.Label == "Herd growth (not cash)");
            Assert.That(earned.Amount, Is.GreaterThan(0));
            SeasonLineVm moved = page.Moved.Single(l => l.Key == "herdGrowth");
            Assert.That(moved.Label, Is.EqualTo("Herd growth: not cash"));
            Assert.That(moved.Amount, Is.EqualTo(-earned.Amount), "what it adds to earnings, it takes off again as cash");
        }

        [Test]
        public void TheAwayCard_CountsKeptSoldAndGrown()
        {
            Simulation sim = Dairy();
            ViewContext ctx = ViewKit2.Context(sim);
            long now = sim.State.Clock.SimTime;
            var events = new List<SimEvent>
            {
                new CalfKept(now, 1, 2, 4068), new CalfKept(now, 1, 3, 4068), new CalfNoRoom(now, 1, 4068), new HeiferGrown(now, 2),
                new AwayAdvanceCompleted(now, now - 30 * Hour, now, 30 * Hour, 30 * Hour, false),
            };
            AwayCard.Vm vm = AwayCard.Build(ctx, events);
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("2 heifer calves kept").And.Contain("A heifer calf sold: the shed was full").And.Contain("A heifer grown into a milking cow"));
        }
    }
}
