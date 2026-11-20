using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Land;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-257 (spec 025 §3.8, Preview 11): the solar panel button on a field its source waters, the field after, the Why?, the map and the close.</summary>
    public class SolarPanelViewTests
    {
        private static Simulation Village()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            Situations.SetCash(sim, 5_00_000_00);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new InstallPump(t)).Accepted, Is.True);
            Assert.That(sim.Execute(new SetPumpAcres(t, new[] { 86, 102, 84, 85 })).Accepted, Is.True);
            return sim;
        }

        private static ActionVm Button(SheetVm sheet, string id) { return sheet.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).SingleOrDefault(b => b.Id == id); }

        [Test]
        public void TheButton_IsOnAFieldTheSourceWaters_WithThePriceAndTheSaving()
        {
            Simulation sim = Village();
            ViewContext ctx = ViewKit2.Context(sim);
            ActionVm solar = Button(FieldSheets.ForTile(ctx, 84), "solarPump");
            Assert.That(solar.Label, Is.EqualTo("Solar panel for the pump"));
            long cost = IrrigationRules.PanelCost(sim, 1), saving = IrrigationRules.PanelSaving(sim, 1);
            Assert.That(solar.Detail, Is.EqualTo(ctx.Fmt.Coins(cost) + " · saves about " + ctx.Fmt.Coins(saving) + " a season"));
            Assert.That((solar.Icon, solar.Enabled), Is.EqualTo(("solar_panel", true)));
            Assert.That(Button(FieldSheets.ForTile(ctx, 100), "solarPump"), Is.Null, "an acre the pump doesn't water");

            Situations.SetCash(sim, 1000 * 100);
            ActionVm poor = Button(FieldSheets.ForTile(ViewKit2.Context(sim), 84), "solarPump");
            Assert.That(poor.Enabled, Is.False);
            Assert.That(poor.DisabledReason, Is.Not.Empty);
        }

        [Test]
        public void AfterFitting_TheFieldSaysSolar_TheButtonGoes_AndTheWhyShowsTheSum()
        {
            Simulation sim = Village();
            Assert.That(sim.Execute(new FitSolarPanel(sim.State.Clock.SimTime, 1)).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm sheet = FieldSheets.ForTile(ctx, 84);
            Assert.That(sheet.Lines.Single(l => l.Label == "Irrigation").Value, Is.EqualTo("The pump (solar)"));
            Assert.That(Button(sheet, "solarPump"), Is.Null, "fitted once");
            LineVm row = sheet.Lines.Single(l => l.Label == "Solar panel");
            Assert.That(row.Value, Is.EqualTo("Fitted · power 80% lower"));
            Assert.That(row.Note, Is.EqualTo("The pump's power is " + ctx.Fmt.Coins(IrrigationRules.PumpPower(sim, 1)) + " a season instead of " + ctx.Fmt.Coins(IrrigationRules.PowerWithout(sim, 1)) + "."));

            LineVm water = sheet.Lines.Single(l => l.Label == "Irrigation");
            WhyVm why = WhyView.Build(ctx, water.WhyKey, water.WhyId);
            var labels = why.Rows.Select(r => r.Label).ToList();
            Assert.That(labels, Does.Contain("Without the panel it was").And.Contain("A solar panel for the pump costs").And.Contain("Seasons to pay it back").And.Contain("The pump itself lasts"));
            Assert.That(why.Rows.Single(r => r.Label == "Seasons to pay it back").Value, Is.EqualTo(ctx.Strings.Plural("why.seasons", IrrigationRules.PaybackSeasons(sim, 1))));
            Assert.That(why.Rows.Any(r => r.Label.Contains("would not pay for itself before")), Is.True, "the honest verdict, in words");
        }

        [Test]
        public void TheMap_DrawsThePanel_AndTheCloseShowsTheLowerPower()
        {
            Simulation sim = Village();
            Assert.That(MapView.Build(ViewKit2.Context(sim)).SolarPump, Is.False);
            long plain = IrrigationRules.PumpPower(sim, 1);
            Assert.That(sim.Execute(new FitSolarPanel(sim.State.Clock.SimTime, 1)).Accepted, Is.True);
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That((map.SolarPump, map.SolarBorewell), Is.EqualTo((true, false)));
            long solar = IrrigationRules.PumpPower(sim, 1);
            Assert.That(solar, Is.LessThan(plain / 4).Within(100));
            Assert.That(Sim.Core.Finance.CloseDues.Next(sim, true).Any(d => d.Amount == solar), Is.True, "the next season's due line is the lower sum");
        }

        [Test]
        public void TheConfirmCard_IsATapCard()
        {
            Assert.That(ConfirmRules.Of(new FitSolarPanel(0, 1)), Is.EqualTo(ConfirmKind.Tap));
            Simulation sim = Village();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new FitSolarPanel(sim.State.Clock.SimTime, 1));
            Assert.That((card.Title, card.Subtitle), Is.EqualTo(("Fit a solar panel?", "The pump")), "which source it is for");
        }
    }
}
