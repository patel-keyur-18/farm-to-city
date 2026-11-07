using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 013 §7 (US-128): Menu → Equipment, its buttons and refusals in words, yours with book and resale, the field's "Worked by".</summary>
    public class EquipmentViewTests
    {
        private static ActionVm Button(SheetVm sheet, string id) { return sheet.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).Single(b => b.Id == id); }

        private static void Era(Simulation sim, string era)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == era) sim.State.Era.Current = i;
        }

        [Test]
        public void Sheet()
        {
            Simulation sim = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
            Era(sim, "hamlet");
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(MenuSheet.Build(ctx).Lines.Any(l => l.Label == "Equipment"), Is.True);
            SheetVm sheet = EquipmentSheet.Build(ctx);
            Assert.That(sheet.Lines.Where(l => l.IsHeader).Select(l => l.Label), Is.EqualTo(new[] { "Ox plough", "Tractor" }));
            Assert.That(sheet.Lines.First(l => l.Label == "What it does").Value, Is.EqualTo("Works 6 fields · labour −15% · harvests 3% more · carts 500 kg"));
            ActionVm plough = Button(sheet, "buy:ox_plough");
            Assert.That((plough.Enabled, plough.Detail, plough.DisabledReason), Is.EqualTo((false, "70,000 coins with the pair", "Build a second shed for the bullocks, with 2 free places.")));
            Assert.That(Button(sheet, "hire:ox_plough").Enabled, Is.True, "hiring a day is the affordable way in");
            Assert.That(Button(sheet, "buy:tractor").DisabledReason, Is.EqualTo("Comes once you're a Village"));
            Assert.That(Button(sheet, "lease:tractor").DisabledReason, Is.EqualTo("Comes once you're a Village"));
            Assert.That(Button(sheet, "buyLoan:tractor").DisabledReason, Is.EqualTo("Comes once you're a Village"));
            Assert.That(sheet.Lines.Where(l => l.Buttons != null).Select(l => l.Buttons.Count), Is.EqualTo(new[] { 2, 2, 2 }), "two a row: the tractor's four on two rows");

            // In the Village with too little cash: the price stays in sight, and the loan's down payment is refused before its sheet.
            Era(sim, "village");
            Sim.Core.Finance.Ledger books = sim.State.Ledger;
            books.Post(sim.State.Clock.SimTime, sim.Period, "ledger.opening", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, -(books.Cash - 100_000 * Sim.Core.Finance.Money.Coin)),
                new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.OwnersCapital, books.Cash - 100_000 * Sim.Core.Finance.Money.Coin));
            ctx = ViewKit2.Context(sim);
            sheet = EquipmentSheet.Build(ctx);
            Assert.That(Button(sheet, "buy:tractor").DisabledReason, Does.Match("^[0-9,]+ coins: more than you have$"));
            Assert.That(Button(sheet, "buyLoan:tractor").DisabledReason, Does.Match("^You pay [0-9,]+ coins now: more than you have$"));
            Assert.That(Button(sheet, "lease:tractor").Enabled, Is.True);
            Era(sim, "hamlet");
            ctx = ViewKit2.Context(sim);
            sheet = EquipmentSheet.Build(ctx);
            Assert.That(FieldSheets.ForTile(ctx, 84).Lines.Single(l => l.Label == "Worked by").Value, Is.EqualTo("Hand tools"));

            Assert.That(sim.Execute(Button(sheet, "hire:ox_plough").Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            ctx = ViewKit2.Context(sim);
            Assert.That(FieldSheets.ForTile(ctx, 84).Lines.Single(l => l.Label == "Worked by").Value, Is.EqualTo("Ox plough"));
            ActionVm hired = Button(EquipmentSheet.Build(ctx), "hire:ox_plough");
            Assert.That((hired.Label, hired.Enabled, hired.DisabledReason), Is.EqualTo(("Hired today", false, "Working your fields until midnight")));
            WhyVm why = WhyView.Build(ctx, "fieldTools", 84);
            Assert.That(why.Value, Is.EqualTo("Ox plough"));
        }

        [Test]
        public void Yours()
        {
            Simulation sim = Situations.Equipped();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm sheet = EquipmentSheet.Build(ctx);
            LineVm[] yours = sheet.Lines.Where(l => l.Label == "Yours").ToArray();
            Assert.That(yours.Length, Is.EqualTo(2));
            Assert.That(yours.Select(l => l.Value), Has.All.Match("Working [0-9]+ fields?|Broken down: .*"));
            LineVm value = sheet.Lines.Last(l => l.Label == "Book value · if sold");
            Sim.Core.Equipment.Machine tractor = sim.State.Equipment.Machines[1];
            Assert.That(tractor.Depreciated, Is.EqualTo(tractor.Cost / 60), "a season's depreciation");
            Assert.That(value.Value, Is.EqualTo(ctx.Fmt.Coins(tractor.Cost - tractor.Depreciated) + " · " + ctx.Fmt.Coins(Sim.Core.Equipment.EquipmentRules.Resale(sim, tractor))));
            Assert.That(sheet.Lines.Single(l => l.Label == "Maintenance at each close").Value, Is.EqualTo("Skipped: it breaks down more often"));
            Assert.That(sheet.Lines.Where(l => l.Label == "Sell it").Select(l => l.Action.Enabled), Is.EqualTo(new[] { true, true }));
            Sim.Core.Equipment.Machine plough = sim.State.Equipment.Machines[0];
            long pair = sim.State.Livestock.Animals.Where(x => sim.Config.Game.Livestock.Animals[x.Kind].Draught).Sum(x => Sim.Core.Livestock.LivestockRules.SellPrice(sim, x));
            Assert.That(sheet.Lines.First(l => l.Label == "Sell it").Action.Detail, Is.EqualTo(ctx.Fmt.Coins(Sim.Core.Equipment.EquipmentRules.Resale(sim, plough) + pair) + " with the pair"),
                "selling the plough sells its bullocks too");
            WhyVm why = WhyView.Build(ctx, value.WhyKey, value.WhyId);
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("Depreciation so far (1 of 60 seasons)"));
        }
    
        // ADR 0088: machines at work on a growing field by day, a field an hour; at night the tractor parks on lane acre 167 with its
        // trolley and the bullocks rest in the second shed; out at the plough, they aren't in it.
        [Test]
        public void Map()
        {
            Simulation sim = Situations.Equipped();
            long day = sim.State.Clock.SimTime / 86400 * 86400 + 86400;
            sim.AdvanceTo(day + 22 * 3600);
            MapVm night = MapView.Build(ViewKit2.Context(sim));
            Assert.That(night.Machines.Select(m => (m.Sprite, m.TileId, m.Parked)), Is.EqualTo(new[] { ("tractor_trolley", 167, true) }));
            Assert.That(night.SecondCattle.Animals.Select(a => a.Kind), Is.EqualTo(new[] { "bullock", "bullock" }));

            // The tractor sold: the ox plough works the growing fields by day, and its pair leave the shed.
            Assert.That(sim.Execute(new SellMachine(sim.State.Clock.SimTime, sim.State.Equipment.Machines[1].Id)).Accepted, Is.True);
            sim.AdvanceTo(day + 86400 + 10 * 3600);
            foreach (int t in Sim.Core.Equipment.EquipmentRules.Fields(sim))
                if (sim.State.Farm.CropOn(t) == null) sim.Execute(new Sow(sim.State.Clock.SimTime, t, "radish", Sim.Core.Farm.SeedSource.Certified, "none"));
            Assert.That(sim.State.Farm.Crops.Any(c => c.State == Sim.Core.Farm.CropState.Growing), Is.True);
            MapVm work = MapView.Build(ViewKit2.Context(sim));
            MachineVm plough = work.Machines.Single();
            Assert.That((plough.Sprite, plough.Parked), Is.EqualTo(("plough_team", false)));
            Assert.That(sim.State.Farm.CropOn(plough.TileId).State, Is.EqualTo(Sim.Core.Farm.CropState.Growing));
            Assert.That(work.SecondCattle.Animals, Is.Empty);
        }
    }
}
