using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 013 §7 (US-127): the field card's irrigation row, its buttons by era, and the irrigation <i>Why?</i>.</summary>
    public class IrrigationViewTests
    {
        private static Simulation Game(string era)
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == era) sim.State.Era.Current = i;
            return sim;
        }

        private static string[] Buttons(SheetVm sheet) { return sheet.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Select(b => b.Id).ToArray(); }

        private static string Irrigation(SheetVm sheet) { return sheet.Lines.Single(l => l.Label == "Irrigation").Value; }

        [Test]
        public void FieldCard()
        {
            Simulation home = Game("homestead");
            SheetVm sheet = FieldSheets.ForTile(ViewKit2.Context(home), 86);
            Assert.That(Irrigation(sheet), Is.EqualTo("The well, by bucket"));
            Assert.That(Buttons(sheet), Is.Empty, "nothing to buy before the Hamlet");

            Simulation sim = Game("hamlet");
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(Buttons(FieldSheets.ForTile(ctx, 86)), Is.EqualTo(new[] { "installPump", "buildChannel" }), "the pump and the canal from the Hamlet");
            ActionVm channel = FieldSheets.ForTile(ctx, 100).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == "buildChannel");
            Assert.That(channel.Enabled, Is.False, "100 isn't on the canal");
            Assert.That(channel.DisabledReason, Is.EqualTo("Only beside the canal, or beside a field of yours with a channel"));
            Assert.That(channel.Icon, Is.EqualTo("channel"));

            Assert.That(sim.Execute(FieldSheets.ForTile(ctx, 86).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == "installPump").Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            sim.Execute(new BuildChannel(sim.State.Clock.SimTime, 85));
            ctx = ViewKit2.Context(sim);
            Assert.That(Irrigation(FieldSheets.ForTile(ctx, 86)), Is.EqualTo("The pump"));
            Assert.That(Buttons(FieldSheets.ForTile(ctx, 86)), Is.EqualTo(new[] { "pumpOff", "buildChannel" }));
            ActionVm on = FieldSheets.ForTile(ctx, 84).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == "pumpOn");
            Assert.That(on.Detail, Is.EqualTo("Waters 2 of 4 acres"));
            Assert.That(sim.Execute(on.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.Irrigation.PumpAcres, Is.EqualTo(new[] { 86, 102, 84 }));
            Assert.That(Irrigation(FieldSheets.ForTile(ViewKit2.Context(sim), 85)), Is.EqualTo("Canal channel"));

            // The Village adds the borewell and drip; drip is half paid by the government on the first 2 acres.
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            ctx = ViewKit2.Context(sim);
            SheetVm well = FieldSheets.ForTile(ctx, 86);
            Assert.That(Buttons(well), Is.EqualTo(new[] { "pumpOff", "drillBorewell", "buildChannel", "layDrip" }));
            Assert.That(well.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == "layDrip").Detail, Is.EqualTo("20,000 coins · half paid by the government"));
            ActionVm drip = FieldSheets.ForTile(ctx, 101).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == "layDrip");
            Assert.That(drip.Enabled, Is.False);
            Assert.That(drip.DisabledReason, Is.EqualTo("Drip needs the pump or the borewell to water this acre first."));
            sim.Execute(new LayDrip(sim.State.Clock.SimTime, 86));
            ctx = ViewKit2.Context(sim);
            Assert.That(Irrigation(FieldSheets.ForTile(ctx, 86)), Is.EqualTo("Drip from the pump"));
            ActionVm off = FieldSheets.ForTile(ctx, 86).Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == "pumpOff");
            Assert.That(off.DisabledReason, Is.EqualTo("Drip needs pumped water: this acre has to stay on."));

            WhyVm why = WhyView.Build(ctx, "irrigation", 86);
            Assert.That(why.Title, Is.EqualTo("Water for this acre"));
            Assert.That(why.Value, Is.EqualTo("Drip from the pump"));
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("The pump keeps it at least at").And.Contain("Care saved each day (no weeding between the lines)"));
            Assert.That(why.Rows.Select(r => r.Label), Does.Not.Contain("Water fee each season"), "86 has no channel");
        }

        // The map draws the canal dry in its dry season and a channel dry when it carries no water for you (owner, 2026-11-04).
        [Test]
        public void Map_DryCanal()
        {
            Simulation sim = Game("hamlet");
            sim.Execute(new BuildChannel(sim.State.Clock.SimTime, 85));
            MapVm spring = MapView.Build(ViewKit2.Context(sim));
            Assert.That(spring.CanalRow, Is.EqualTo(4));
            Assert.That((spring.CanalDry, spring.Tiles[85].ChannelDry, spring.Tiles[84].ChannelDry), Is.EqualTo((false, false, false)));
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason); // Summer: closed for repairs
            MapVm summer = MapView.Build(ViewKit2.Context(sim));
            Assert.That((summer.CanalDry, summer.Tiles[85].ChannelDry), Is.EqualTo((true, true)));
        }
    }
}