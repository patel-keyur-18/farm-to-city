using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 §3.5: the sheets a tap opens, their buttons, and why a button is greyed.</summary>
    public class SheetTests
    {
        private const long Hour = 3600;

        private static Simulation Rich(ulong seed = 42)
        {
            return ViewKit.NewGame(seed: seed, edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
        }

        private static ActionVm Action(SheetVm sheet, string id) { return sheet.Actions.Single(a => a.Id == id); }

        private static ActionVm RowAction(SheetVm sheet, string id) { return sheet.Lines.Single(l => l.Action != null && l.Action.Id == id).Action; }

        [Test]
        public void VacantField_OffersSow_AndShowsTheSoil()
        {
            Simulation sim = ViewKit.NewGame();
            SheetVm sheet = FieldSheets.ForTile(ViewKit2.Context(sim), 20);
            Assert.That(sheet.Title, Is.EqualTo("Loam field"));
            Assert.That(sheet.Subtitle, Is.EqualTo("Your family's land"));
            Assert.That(sheet.Lines.Select(l => l.Label), Does.Contain("Fertility").And.Contain("Moisture").And.Contain("Soil factor").And.Contain("Well acre"));
            Assert.That(sheet.Lines.Single(l => l.Label == "Fertility").Value, Is.EqualTo("45 / 100"));
            Assert.That(Action(sheet, "sow").Opens.Value.Kind, Is.EqualTo("Sow"));
            Assert.That(Action(sheet, "sow").Opens.Value.Id, Is.EqualTo(20));
            Assert.That(sheet.Actions.Any(a => a.Id == "sellParcel"), Is.False, "the inherited land can never be sold");
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), 18).Lines.Any(l => l.Label == "Well acre"), Is.False);
        }

        [Test]
        public void GrowingField_ShowsCareAndSprays()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            sim.Execute(new Sow(sim.State.Clock.SimTime, 20, "potato", SeedSource.Certified, "none"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Hour);
            SheetVm sheet = FieldSheets.ForTile(ctx, 20);
            Assert.That(sheet.Title, Is.EqualTo("Potato, growing"));
            Assert.That(sheet.Lines.Single(l => l.Label == "Time left").Value, Is.EqualTo("21 h"));
            Assert.That(sheet.Lines.Select(l => l.Label), Does.Contain("Care").And.Contain("Water").And.Contain("Weather"));
            // Words, not factors (owner feedback, device run 2).
            foreach (string label in new[] { "Care", "Water", "Weather" })
                Assert.That(sheet.Lines.Single(l => l.Label == label).Value, Is.AnyOf("Good", "Fair", "Poor"), label);
            Assert.That(Action(sheet, "spray").Kind, Is.EqualTo(ActionKind.Primary));
            Assert.That(Action(sheet, "clear").Kind, Is.EqualTo(ActionKind.Danger));
            Assert.That(Action(sheet, "clear").Enabled, Is.True);
            Assert.That(sheet.Actions.Any(a => a.Id == "harvest"), Is.False);
            Command spray = Action(sheet, "spray").Create(sim.State.Clock.SimTime);
            Assert.That(spray, Is.TypeOf<Spray>());
        }

        [Test]
        public void ReadyField_Harvests_AndOffersToSellTheRestWhenTheStoreIsFull()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            sim.Execute(new Sow(sim.State.Clock.SimTime, 20, "radish", SeedSource.Certified, "none"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Hour);
            SheetVm sheet = FieldSheets.ForTile(ctx, 20);
            Assert.That(sheet.Title, Is.EqualTo("Radish, ready"));
            Assert.That(sheet.Lines.Single(l => l.Label == "Waiting").Value, Does.StartWith("spoils in"));
            Assert.That(Action(sheet, "harvest").Enabled, Is.True);
            Assert.That(sheet.Actions.Any(a => a.Id == "harvestSell"), Is.False, "only when the harvest doesn't fit");

            // A nearly full store: Harvest is greyed with the sim's own reason, and the second button sells the rest.
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("wheat")] += Sim.Core.Market.Store.CapacityGrams(sim) - Sim.Core.Market.Store.UsedGrams(sim) - 10_000;
            sheet = FieldSheets.ForTile(ctx, 20);
            Assert.That(Action(sheet, "harvest").Enabled, Is.False);
            Assert.That(Action(sheet, "harvest").DisabledReason, Is.EqualTo("The store is full."));
            Assert.That(Action(sheet, "harvestSell").Enabled, Is.True);
            Command c = Action(sheet, "harvestSell").Create(sim.State.Clock.SimTime);
            Assert.That(((Harvest)c).Overflow, Is.EqualTo(Sim.Core.Market.Overflow.SellRestToTrader));
            Assert.That(sim.Execute(c).Accepted, Is.True);

            // Waiting past the grace.
            Simulation late = ViewKit.NewGame();
            late.Execute(new Sow(late.State.Clock.SimTime, 20, "radish", SeedSource.Certified, "none"));
            late.AdvanceTo(late.State.Clock.SimTime + 80 * Hour);
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(late), 20).Lines.Single(l => l.Label == "Waiting").Value, Is.EqualTo("starting to spoil").Or.Contain("spoils in"));
        }

        [Test]
        public void UnownedLand_BuyButtonSaysWhyNot()
        {
            Simulation poor = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(poor);
            SheetVm adjacent = FieldSheets.ForTile(ctx, 16); // w1, next to the home acres
            Assert.That(adjacent.Title, Is.EqualTo("Land for sale"));
            Assert.That(adjacent.Subtitle, Is.EqualTo("2 acres"));
            Assert.That(adjacent.Lines.Select(l => l.Label), Is.EqualTo(new[] { "Price", "Your cash", "Quick resale" }));
            Assert.That(Action(adjacent, "buy").Enabled, Is.False);
            Assert.That(Action(adjacent, "buy").DisabledReason, Is.EqualTo("You don't have enough coins for that."));
            SheetVm far = FieldSheets.ForTile(ctx, 47);
            Assert.That(Action(far, "buy").DisabledReason, Is.EqualTo("You can only buy land next to your own."));

            Simulation rich = Rich();
            SheetVm ok = FieldSheets.ForTile(ViewKit2.Context(rich), 16);
            Assert.That(Action(ok, "buy").Enabled, Is.True);
            Command buy = Action(ok, "buy").Create(rich.State.Clock.SimTime);
            Assert.That(((BuyParcel)buy).ParcelId, Is.EqualTo("w1"));
            Assert.That(rich.Execute(buy).Accepted, Is.True);
            SheetVm bought = FieldSheets.ForTile(ViewKit2.Context(rich), 16);
            Assert.That(bought.Subtitle, Is.EqualTo("Land you bought"));
            Assert.That(Action(bought, "sellParcel").Enabled, Is.True);
            Assert.That(Action(bought, "sellParcel").Kind, Is.EqualTo(ActionKind.Danger));
        }

        [Test]
        public void GardenBed_SowThenPick()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm empty = FieldSheets.ForBed(ctx, 1);
            Assert.That(empty.Title, Is.EqualTo("Garden bed 1"));
            Assert.That(empty.Lines.Where(l => l.Action != null).Select(l => l.Action.Id), Is.EqualTo(new[] { "sow:garden_radish", "sow:garden_spinach", "sow:garden_coriander" }));
            Assert.That(empty.Lines.Where(l => l.Action != null).All(l => l.Action.Enabled), Is.True);
            Assert.That(empty.Lines.Single(l => l.Label == "Radish").Value, Does.StartWith("ready in"));
            Assert.That(sim.Execute(RowAction(empty, "sow:garden_radish").Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(FieldSheets.ForBed(ctx, 1).Subtitle, Is.EqualTo("Radish, growing"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 11 * 60);
            SheetVm ready = FieldSheets.ForBed(ctx, 1);
            Assert.That(Action(ready, "pick").Enabled, Is.True);
            Assert.That(Action(ready, "pick").Detail, Is.EqualTo("5.0 kg"));
            Assert.That(sim.Execute(Action(ready, "pick").Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(FieldSheets.ForBed(ctx, 1).Lines.Single(l => l.Label == "This season").Value, Is.EqualTo("5.0 kg of 100 kg picked"));
        }

        [Test]
        public void TreeSlot_PlantPickRemove()
        {
            Simulation sim = Rich();
            ViewContext ctx = ViewKit2.Context(sim);
            long yard = sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard).Id;
            SheetVm empty = FieldSheets.ForSlot(ctx, yard);
            Assert.That(empty.Title, Is.EqualTo("Yard tree"));
            Assert.That(empty.Lines.Count(l => l.Action != null), Is.EqualTo(4), "one row per tree");
            Assert.That(RowAction(empty, "plant:mango").Label, Is.EqualTo("Plant"));
            Assert.That(RowAction(empty, "plant:mango").Detail, Is.EqualTo("350 coins"));
            Assert.That(empty.Lines.Single(l => l.Label == "Mango").Value, Does.StartWith("fruits in").And.Contain("grows in"));
            Assert.That(sim.Execute(RowAction(empty, "plant:banana").Create(sim.State.Clock.SimTime)).Accepted, Is.True);

            SheetVm young = FieldSheets.ForSlot(ctx, yard);
            Assert.That(young.Title, Is.EqualTo("Banana"));
            Assert.That(young.Subtitle, Is.EqualTo("Growing"));
            Assert.That(young.Lines.Select(l => l.Label), Does.Contain("Cost").And.Contain("Earned so far").And.Contain("Pays for itself"));
            Assert.That(Action(young, "removeTree").Kind, Is.EqualTo(ActionKind.Danger));
            Assert.That(young.Actions.Any(a => a.Id == "pickFruit"), Is.False);

            long edge = sim.State.Land.Slots.First(s => s.Kind == SlotKind.FieldEdge).Id;
            Assert.That(FieldSheets.ForSlot(ctx, edge).Lines.Any(l => l.Label == "Shade"), Is.True);
            Assert.That(sim.Execute(Action(young, "removeTree").Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(FieldSheets.ForSlot(ctx, yard).Subtitle, Is.EqualTo("No tree yet"));
        }

        [Test]
        public void StoreSheet_SellsToTheTrader()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm store = StoreSheet.Build(ctx);
            Assert.That(store.Title, Is.EqualTo("Store"));
            Assert.That(store.Lines.Select(l => l.Label), Does.Contain("Wheat").And.Contain("Lentils").And.Contain("Seed potato"));
            Assert.That(store.Subtitle, Does.StartWith("675").Or.Contain("of 2,000 kg"));
            Assert.That(store.Actions, Is.Empty, "each Sell sits on its good's row");
            ActionVm sellWheat = store.Lines.Single(l => l.Label == "Wheat").Action;
            Assert.That(sellWheat.Id, Is.EqualTo("sell:wheat"));
            Assert.That(sellWheat.Enabled, Is.True);
            Assert.That(sellWheat.Label, Is.EqualTo("Sell"));
            Assert.That(store.Lines.Single(l => l.Label == "Seed potato").Action, Is.Null, "inputs aren't sold to the trader here");
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(sellWheat.Create(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash, Is.GreaterThan(cash));
            Assert.That(StoreSheet.Build(ctx).Lines.Any(l => l.Label == "Wheat"), Is.False, "sold out");


            // A good on offer can't be sold again.
            Simulation m = ViewKit.NewGame();
            m.Execute(new Offer(m.State.Clock.SimTime, "lentils", 75_000));
            ActionVm lentils = StoreSheet.Build(ViewKit2.Context(m)).Lines.Single(l => l.Label == "Lentils").Action;
            Assert.That(lentils.Enabled, Is.False);
            Assert.That(lentils.DisabledReason, Is.Not.Empty);
        }

        [Test]
        public void Rating_WordsForAYieldFactor()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame()); // ui.json: good from 95%, fair from 80%
            Assert.That(ctx.Rating(Fixed.FromRatio(110, 100)), Is.EqualTo("Good"));
            Assert.That(ctx.Rating(Fixed.FromRatio(95, 100)), Is.EqualTo("Good"));
            Assert.That(ctx.Rating(Fixed.FromRatio(94, 100)), Is.EqualTo("Fair"));
            Assert.That(ctx.Rating(Fixed.FromRatio(80, 100)), Is.EqualTo("Fair"));
            Assert.That(ctx.Rating(Fixed.FromRatio(79, 100)), Is.EqualTo("Poor"));
            Assert.That(ctx.Rating(Fixed.Zero), Is.EqualTo("Poor"));
        }

        // AC-3 (views): every string key written in Sim.View's code exists in en.json (plurals by their forms).
        [Test]
        public void ViewKeys_AreAllInTheTable()
        {
            var table = ViewKit.Strings();
            string dir = Path.Combine(ViewKit.RepoRoot, "sim", "Sim.View");
            var missing = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains("/obj/") || file.Contains("/bin/")) continue;
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"([a-z][A-Za-z]*(?:\\.[A-Za-z0-9_]+)+)\""))
                {
                    string key = m.Groups[1].Value;
                    if (!table.Has(key) && !table.Has(key + ".one")) missing.Add(Path.GetFileName(file) + ": " + key);
                }
            }
            Assert.That(missing, Is.Empty);
        }
    }
}
