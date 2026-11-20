using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Equipment;
using Sim.Core.Events;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-256 (spec 025 §3.8, Preview 10): the tractor's Hiring out block, its refusals in words, the Why? pages, the season's line and the away card.</summary>
    public class HireOutViewTests
    {
        private const long Hour = 3600, Day = 86400;

        private static Simulation Farm()
        {
            Simulation sim = Situations.Equipped(); // Ready() already has a hired hand
            Assert.That(sim.State.People.Hands.Count, Is.GreaterThan(0));
            return sim;
        }

        private static Machine Tractor(Simulation sim) { return sim.State.Equipment.Machines.Single(m => sim.Config.Game.Equipment.Machines[m.Kind].Id == "tractor"); }

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
        public void Off_ShowsTheSwitch_TheFee_AndAnHonestPayback()
        {
            Simulation sim = Farm();
            Machine kept = Tractor(sim);
            Assert.That(sim.Execute(new SetMaintenance(sim.State.Clock.SimTime, kept.Id, true)).Accepted, Is.True);
            kept.Maintained = true; // the situation skips maintenance, which makes hiring out lose (below)
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = EquipmentSheet.Build(ctx);
            Assert.That(vm.Lines.Count(l => l.IsHeader && l.Label == "Hiring out"), Is.EqualTo(1), "the tractor only: the ox plough isn't let out");
            LineVm sw = Row(vm, "Hire out to neighbours");
            Assert.That(sw.Value, Is.EqualTo("Off"));
            Assert.That(sw.Note, Does.Contain("up to 8 acres a day at " + ctx.Fmt.Paise(EquipmentRules.HireFeePerAcre(sim, sim.Config.Game.Equipment.Machines[Tractor(sim).Kind])) + " an acre").And.Contain("The diesel and wear are yours"));
            Assert.That((sw.Action.Label, sw.Action.Enabled), Is.EqualTo(("Switch on", true)));
            Assert.That(sw.Action.Detail, Does.StartWith("about ").And.Contain("a season"));
            LineVm pays = Row(vm, "Does it pay?");
            Assert.That(pays.Value, Is.EqualTo("Not for the tractor"));
            Assert.That(pays.Note, Does.Contain("depreciation and upkeep"));
            Assert.That(vm.Lines.Any(l => l.Label == "This season so far"), Is.False, "nothing yet");
            WhysBuild(ctx, vm);
        }

        [Test]
        public void On_SaysTheSeason_TheSum_AndNeverPaysForTheTractor()
        {
            Simulation sim = Farm();
            Machine m = Tractor(sim);
            Assert.That(sim.Execute(new SetHireOut(sim.State.Clock.SimTime, m.Id, true)).Accepted, Is.True);
            Assert.That(sim.Execute(new SetMaintenance(sim.State.Clock.SimTime, m.Id, true)).Accepted, Is.True);
            m.Maintained = true; // the tractor is kept up (the situation skips it): breakdowns at 0.5% a day
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Day);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = EquipmentSheet.Build(ctx);
            LineVm sw = Row(vm, "Hire out to neighbours");
            Assert.That(sw.Value, Is.EqualTo("On · 8 acres a day"));
            Assert.That(sw.Action.Label, Is.EqualTo("Switch off"));
            EquipmentRules.HiredOutSeason(sim, m, out int acres, out long fees, out long diesel);
            Assert.That(acres, Is.GreaterThan(0));
            LineVm season = Row(vm, "This season so far");
            Assert.That(season.Value, Is.EqualTo(acres + " acres · earned " + ctx.Fmt.Coins(fees)));
            Assert.That(season.Note, Is.EqualTo("Diesel for them " + ctx.Fmt.Coins(diesel)));
            LineVm pays = Row(vm, "Does it pay?");
            Assert.That(pays.Value, Does.StartWith("About ").And.EndWith("a season"));
            Assert.That(pays.Note, Does.Contain("it never pays for it").And.Contain("expected repairs"));
            WhysBuild(ctx, vm);
            WhyVm fee = WhyView.Build(ctx, sw.WhyKey, sw.WhyId);
            Assert.That(fee.Rows.Select(r => r.Label), Does.Contain("Most acres they want a day"));
            Assert.That(fee.Rows.First(r => r.Label.StartsWith("Neighbours pay")).Label, Does.Contain("80%"));
            WhyVm cost = WhyView.Build(ctx, season.WhyKey, season.WhyId);
            Assert.That(cost.Rows.Select(r => r.Label), Does.Contain("Expected extra repairs a day"));
            WhyVm sum = WhyView.Build(ctx, pays.WhyKey, pays.WhyId);
            Assert.That(sum.Rows.Select(r => r.Label), Does.Contain("Left over a season").And.Some.StartsWith("Its depreciation a season"));
            Assert.That(sum.Footnote, Is.Not.Empty);
        }

        // Skipping maintenance (4% a day, doubled to 8% on the days it works) costs more in repairs than the fees leave: the sheet says it loses.
        [Test]
        public void WithMaintenanceSkipped_HiringOut_LosesMoney_AndSaysSo()
        {
            Simulation sim = Farm();
            Machine m = Tractor(sim);
            Assert.That(m.Maintained, Is.False);
            Assert.That(sim.Execute(new SetHireOut(sim.State.Clock.SimTime, m.Id, true)).Accepted, Is.True);
            LineVm pays = Row(EquipmentSheet.Build(ViewKit2.Context(sim)), "Does it pay?");
            Assert.That(pays.Value, Does.StartWith("Loses ").And.EndWith("a season"));
        }

        [Test]
        public void Refusals_AreInWords()
        {
            Simulation sim = Farm();
            Machine m = Tractor(sim);
            foreach (var h in sim.State.People.Hands) h.Hr.SickUntil = sim.State.Clock.SimTime + 30 * Day;
            SheetVm vm = EquipmentSheet.Build(ViewKit2.Context(sim));
            LineVm sw = Row(vm, "Hire out to neighbours");
            Assert.That(sw.Action.Enabled, Is.False);
            Assert.That(sw.Action.DisabledReason, Is.EqualTo("Needs a hired hand to drive it."));

            foreach (var h in sim.State.People.Hands) h.Hr.SickUntil = 0;
            Assert.That(sim.Execute(new SetHireOut(sim.State.Clock.SimTime, m.Id, true)).Accepted, Is.True);
            foreach (var h in sim.State.People.Hands) h.Hr.SickUntil = sim.State.Clock.SimTime + 30 * Day;
            Assert.That(Row(EquipmentSheet.Build(ViewKit2.Context(sim)), "Hire out to neighbours").Value, Is.EqualTo("On · no hand to drive it"));
            foreach (var h in sim.State.People.Hands) h.Hr.SickUntil = 0;

            m.BrokenUntil = sim.State.Clock.SimTime + 5 * Hour;
            LineVm resting = Row(EquipmentSheet.Build(ViewKit2.Context(sim)), "Hire out to neighbours");
            Assert.That(resting.Value, Is.EqualTo("On · resting"));
            Assert.That(resting.Note, Does.Contain("works for neighbours again when it is mended"));
        }

        [Test]
        public void ALeasedTractor_CantBeLetOn()
        {
            Simulation sim = Farm();
            Machine m = Tractor(sim);
            m.Leased = true;
            LineVm sw = Row(EquipmentSheet.Build(ViewKit2.Context(sim)), "Hire out to neighbours");
            Assert.That(sw.Value, Is.EqualTo("Not for a leased tractor"));
            Assert.That(sw.Note, Does.Contain("can't be let on"));
            Assert.That(sw.Action, Is.Null);
        }

        [Test]
        public void TheSeasonPage_ShowsMachineHireReceived_AsFarmIncome()
        {
            Simulation sim = Farm();
            Assert.That(sim.Execute(new SetHireOut(sim.State.Clock.SimTime, Tractor(sim).Id, true)).Accepted, Is.True);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Day);
            SeasonVm page = SeasonBook.Build(ViewKit2.Context(sim), false);
            SeasonLineVm line = page.Earned.Single(l => l.Label == "Machine hire received");
            Assert.That(line.Amount, Is.GreaterThan(0));
            Assert.That(line.Icon, Is.EqualTo("tractor"));
            Assert.That(line.WhyText, Does.Contain("Neighbours paid you"));
            Assert.That(page.Spent.Any(l => l.Label == "Power, water and diesel" && l.Amount > 0), Is.True, "the diesel is in Power");
        }

        [Test]
        public void TheAwayCard_SaysWhatTheTractorDid()
        {
            Simulation sim = Farm();
            ViewContext ctx = ViewKit2.Context(sim);
            long now = sim.State.Clock.SimTime;
            var events = new List<SimEvent>
            {
                new MachineHiredOut(now, 1, "tractor", 8, 65_100, 24_400),
                new MachineHiredOut(now, 1, "tractor", 8, 65_100, 24_400),
                new MachineHiredOut(now, 1, "tractor", 8, 65_100, 24_400),
                new AwayAdvanceCompleted(now, now - 72 * Hour, now, 72 * Hour, 72 * Hour, false),
            };
            AwayCard.Vm vm = AwayCard.Build(ctx, events);
            LineVm row = vm.Lines.Single(l => l.Label == "Your tractor");
            Assert.That(row.Value, Is.EqualTo("Ploughed 24 acres for neighbours: " + ctx.Fmt.Coins(195_300)));
            Assert.That(row.Note, Is.EqualTo("Diesel " + ctx.Fmt.Coins(73_200) + ", paid"));
            Assert.That(WhyView.Build(ctx, row.WhyKey, row.WhyId).Title, Is.Not.Empty);
        }
    }
}
