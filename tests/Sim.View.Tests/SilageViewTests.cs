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
    /// <summary>US-255 (spec 025 §3.8, Preview 9): the Feed stores block, the green fodder row with silage, the Why? pages, the map and the away card.</summary>
    public class SilageViewTests
    {
        private static Simulation Shed(int animals)
        {
            Simulation sim = CattleViewTests.Hamlet(out ViewContext _);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildShed(t)).Accepted, Is.True);
            sim.AdvanceTo(t + 25 * 3600);
            for (int i = 0; i < animals; i++) Assert.That(sim.Execute(new BuyAnimal(sim.State.Clock.SimTime, "buffalo")).Accepted, Is.True);
            return sim;
        }

        private static LineVm Row(SheetVm vm, string label) { return vm.Lines.Single(l => l.Label == label); }

        private static void WhysBuild(ViewContext ctx, SheetVm vm)
        {
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.WhyKey);
                Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId).Title, Is.Not.Empty, l.WhyKey + " " + l.WhyId);
            }
        }

        [Test]
        public void FeedStores_BeforeBuying_SayWhatEachPays()
        {
            Simulation sim = Shed(4);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = ShedSheet.Build(ctx);
            Assert.That(vm.Lines.Any(l => l.IsHeader && l.Label == "Feed stores"), Is.True);
            LineVm pit = Row(vm, "Silage pit");
            Assert.That(pit.Value, Is.EqualTo("Not built"));
            Assert.That(pit.Note, Does.Contain("holds 20 t").And.Contain("15% is lost").And.Contain("gains about"));
            Assert.That((pit.Action.Label, pit.Action.Enabled), Is.EqualTo(("Build a silage pit", true)));
            LineVm cutter = Row(vm, "Chaff cutter");
            Assert.That(cutter.Value, Is.EqualTo("Not bought"));
            Assert.That(cutter.Note, Does.Contain("20%").And.Contain("herd of 4").And.Contain("to pay back").And.Contain("a small herd should not buy it").Or.Contain("A small herd should not buy it"));
            Assert.That(cutter.Action.Label, Is.EqualTo("Buy a chaff cutter"));
            Assert.That(Row(vm, "Sheds").Value, Is.EqualTo("1 of 2"));
            Assert.That(Row(vm, "Sheds").Note, Does.Contain("dairy farm business"));
            WhysBuild(ctx, vm);
        }

        [Test]
        public void Buying_ChangesTheRows_AndTheMap()
        {
            Simulation sim = Shed(4);
            SheetVm vm = ShedSheet.Build(ViewKit2.Context(sim));
            Assert.That(sim.Execute(Row(vm, "Silage pit").Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.Execute(Row(vm, "Chaff cutter").Action.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            vm = ShedSheet.Build(ctx);
            Assert.That(Row(vm, "Silage pit").Value, Is.EqualTo("Empty · holds 20,000 kg"));
            Assert.That(Row(vm, "Silage pit").Action, Is.Null);
            Assert.That(Row(vm, "Chaff cutter").Value, Is.EqualTo("Working: green fodder and straw 20% less"));
            Assert.That(Row(vm, "Chaff cutter").Note, Does.StartWith("Saves about"));
            WhysBuild(ctx, vm);
            Assert.That(MapView.Build(ctx).Cattle.Silage, Is.True);
        }

        [Test]
        public void TheGreenRow_SaysWhereTheSilageWent_AndCameFrom()
        {
            Simulation sim = Shed(4);
            Assert.That(sim.Execute(new BuildSilagePit(sim.State.Clock.SimTime)).Accepted, Is.True);
            LivestockSystem.Feed(sim, 8_000_000); // a flush: far more than four buffalo eat
            ViewContext ctx = ViewKit2.Context(sim);
            LineVm green = Row(ShedSheet.Build(ctx), "Green fodder");
            Assert.That(green.Value, Does.Contain("spare went to the pit").And.Contain("lost"));
            Assert.That(Row(ShedSheet.Build(ctx), "Silage pit").Value, Does.Contain("of 20,000 kg · lasts"));
            LivestockSystem.Feed(sim, 0);
            green = Row(ShedSheet.Build(ViewKit2.Context(sim)), "Green fodder");
            Assert.That(green.Value, Does.Contain("silage "));
        }

        [Test]
        public void TheAwayCard_SaysSilagePutByAndFed()
        {
            Simulation sim = Shed(1);
            ViewContext ctx = ViewKit2.Context(sim);
            long now = sim.State.Clock.SimTime;
            var events = new List<SimEvent>
            {
                new AnimalsFed(now, 616_000, 616_000, 0, 0, 0, 137_000, 114_000, Sim.Core.Maths.Fixed.One, 410_000, 0),
                new AnimalsFed(now, 616_000, 616_000, 0, 0, 0, 137_000, 114_000, Sim.Core.Maths.Fixed.One, 0, 1_893_000),
                new AwayAdvanceCompleted(now, now - 30 * 3600, now, 30 * 3600, 30 * 3600, false),
            };
            AwayCard.Vm vm = AwayCard.Build(ctx, events);
            Assert.That(vm.Lines.Single(l => l.Label == "Silage put by").Value, Is.EqualTo("1,893 kg"));
            Assert.That(vm.Lines.Single(l => l.Label == "Silage fed instead of bought fodder").Value, Is.EqualTo("410 kg"));
        }
    }
}
