using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-144 (spec 025 §3.1, Preview 4): the five utilities' plot sheets in every state, the commission card with its guard and top-up, the Town tab's Infrastructure section, and every number's Why?.</summary>
    public class UtilityViewTests
    {
        private const int Tower = UtilityDef.Tower, Generator = UtilityDef.Generator, Grid = UtilityDef.Grid, Drains = UtilityDef.Drains, Collection = UtilityDef.Collection;
        private const long Hour = 3600;

        private static void Fund(Simulation sim, long coins)
        {
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, coins * 100), new Posting(Account.GiftsFromFounder, -coins * 100));
        }

        private static Simulation Township(long coins = 3_000_000)
        {
            Simulation sim = Situations.Township();
            Fund(sim, coins);
            Situations.SetCash(sim, 5_000_000_00);
            return sim;
        }

        private static void Build(Simulation sim, int u)
        {
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, UtilityRules.Def(sim, u).Work)).Accepted, Is.True);
            for (int i = 0; i < 400 && !UtilityRules.Built(sim, u); i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(UtilityRules.Built(sim, u), Is.True);
        }

        private static SheetVm Plot(Simulation sim, int u) { return FieldSheets.ForTile(ViewKit2.Context(sim), UtilityRules.Def(sim, u).Tile); }

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

        // Before the Township the plot says why the button is grey.
        [Test]
        public void Locked()
        {
            Simulation sim = Situations.Town();
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), sim.Config.Game.Map.Places.First(p => p.Kind == "tower_plot").Tile);
            Assert.That(vm.Title, Is.EqualTo("Water tower"));
            Assert.That(vm.Lines.Select(l => l.Value), Does.Contain("Not yet"));
            Assert.That(vm.Actions.Single(a => a.Id == "commission").Enabled, Is.False);
        }

        // Each plot before it is built: its cost, upkeep, reach, capacity, what it does and the town's cash, every row with a Why?.
        [Test]
        public void Plot_Sheets()
        {
            Simulation sim = Township();
            foreach (int u in new[] { Tower, Generator, Grid, Drains, Collection })
            {
                SheetVm vm = Plot(sim, u);
                Assert.That(vm.Title, Is.EqualTo(new[] { "Water tower", "Diesel generator", "Grid connection", "Drains", "Collection post" }[u]));
                var labels = vm.Lines.Select(l => l.Label).ToList();
                Assert.That(labels, Does.Contain("Reach"));
                Assert.That(labels, Does.Contain("Capacity"));
                Assert.That(labels, Does.Contain("Upkeep a season, from Utilities"));
                Assert.That(vm.Lines.Single(l => l.Label == "Cost, paid from the town's cash").Value, Is.EqualTo(ViewKit2.Context(sim).Fmt.Coins(UtilityRules.Cost(sim, u))));
                Assert.That(vm.Actions.Single(a => a.Id == "commission").Enabled, Is.True);
                AssertWhys(sim, vm);
            }
            Assert.That(Plot(sim, Grid).Lines.Single(l => l.Label == "Capacity").Value, Is.EqualTo("No limit"));
            Assert.That(Plot(sim, Grid).Lines.Single(l => l.Label == "Reach").Value, Is.EqualTo("The whole town"));
            Assert.That(Plot(sim, Tower).Lines.Single(l => l.Label == "Reach").Value, Is.EqualTo("400 m"));
            Assert.That(Plot(sim, Generator).Lines.Select(l => l.Label), Does.Contain("Its smoke"), "the generator says what its smoke reaches");
            Assert.That(Plot(sim, Collection).Lines.Select(l => l.Label), Does.Contain("The collector's wage a season, from Utilities"));
        }

        // A short treasury: the main button is the one-tap top-up (the gift rounded up), and the plain gift is one tap away.
        [Test]
        public void Poor_Plot_Offers_The_TopUp()
        {
            Simulation sim = Township(coins: 0);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -sim.State.Treasury.Cash), new Posting(Account.GiftsFromFounder, sim.State.Treasury.Cash));
            Fund(sim, 105_000);
            SheetVm vm = Plot(sim, Tower);
            Assert.That(vm.Lines.Select(l => l.Value), Does.Contain(ViewKit2.Context(sim).Fmt.Coins(sim.State.Treasury.Cash)));
            ActionVm build = vm.Actions.Single(a => a.Id == "commission");
            long gift = UtilityRules.TopUp(sim, UtilityRules.Cost(sim, Tower));
            Assert.That(build.Detail, Is.EqualTo("Give " + ViewKit2.Context(sim).Fmt.Coins(gift) + " and build"));
            Assert.That(build.Enabled, Is.True);
            Assert.That(vm.Actions.Select(a => a.Id), Does.Contain("give"));
            Assert.That(vm.Lines.Any(l => l.Label != null && l.Label.StartsWith("The town is ")), Is.True);

            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, UtilityRules.Def(sim, Tower).Work, gift));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "The town's cash now", "You give", "Materials: bricks, cement, timber", card.Rows[3].Label, "The town's cash after" }));
            Assert.That(card.GoLabel, Is.EqualTo("Give " + ctx.Fmt.Coins(gift) + " and build"));
            Assert.That(card.Warning, Does.Contain("Utilities"));
            Assert.That(ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, UtilityRules.Def(sim, Tower).Work)), Is.Null, "short without a gift: the sim refuses, no card");
        }

        // With the cash the card is the usual one: build for the cost, the guard says whether the town can pay the upkeep.
        [Test]
        public void Cards()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm tower = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "water_tower"));
            Assert.That(tower.Title, Is.EqualTo("Build the water tower?"));
            Assert.That(tower.Rows.Select(r => r.Label), Does.Not.Contain("You give"));
            Assert.That(tower.GoLabel, Does.StartWith("Build for"));
            Assert.That(tower.Warning, Does.Contain("adds").And.Contain("a season to Utilities"));
            Assert.That(tower.Warning, Does.Contain("ready in"), "the timing, in words");

            ConfirmCardVm gen = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "diesel_generator"));
            Assert.That(gen.Title, Is.EqualTo("Fit a diesel generator?"));
            Assert.That(gen.Rows.Select(r => r.Label), Does.Contain("Diesel generator"));
            Assert.That(gen.Warning, Does.Contain("Its smoke lowers land value on"));

            Build(sim, Generator);
            ctx = ViewKit2.Context(sim);
            ConfirmCardVm grid = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "grid_connection"));
            Assert.That(grid.Warning, Does.Contain("switched off").And.Contain("you save"));
            Assert.That(grid.Title, Is.EqualTo("Connect the grid?"));
        }

        // A tower being built shows the site sheet; once built, its service, reach with homes, capacity, what it costs and does.
        [Test]
        public void Tower_Site_Then_Built()
        {
            Simulation sim = Township();
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "water_tower")).Accepted, Is.True);
            SheetVm site = Plot(sim, Tower);
            Assert.That(site.Title, Is.EqualTo("Water tower"));
            Assert.That(site.Lines.Count, Is.GreaterThan(2));
            for (int i = 0; i < 400 && !UtilityRules.Built(sim, Tower); i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            SheetVm vm = Plot(sim, Tower);
            Assert.That(vm.Subtitle, Does.StartWith("Working"));
            var labels = vm.Lines.Select(l => l.Label).ToList();
            Assert.That(labels, Does.Contain("Utilities service"));
            Assert.That(labels, Does.Contain("Health those homes keep"));
            Assert.That(vm.Lines.Single(l => l.Label == "Reach").Value, Does.StartWith("400 m · "));
            Assert.That(vm.Lines.Single(l => l.Label == "Health those homes keep").Value, Is.EqualTo("4 points a season"));
            AssertWhys(sim, vm);
        }

        // The standing generator names its smoke and offers the grid; the grid switches it off and the sheet says so.
        [Test]
        public void Generator_Built_And_Switched_Off()
        {
            Simulation sim = Township();
            Build(sim, Generator);
            SheetVm vm = Plot(sim, Generator);
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Its smoke"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Shops and workshops in reach"));
            Assert.That(vm.Lines.Single(l => l.Label == "Shops and workshops in reach").Value, Is.EqualTo("evenings at the usual wage (2 hours)"));
            Assert.That(vm.Actions.Select(a => a.Id), Does.Contain("grid"));
            AssertWhys(sim, vm);
            Build(sim, Grid);
            SheetVm off = Plot(sim, Generator);
            Assert.That(off.Subtitle, Does.StartWith("Switched off"));
            AssertWhys(sim, off);
            AssertWhys(sim, Plot(sim, Grid));
        }

        [Test]
        public void Drains_And_Collection_Built()
        {
            Simulation sim = Township();
            Build(sim, Drains);
            Build(sim, Collection);
            foreach (int u in new[] { Drains, Collection })
            {
                SheetVm vm = Plot(sim, u);
                Assert.That(vm.Subtitle, Does.StartWith("Working"));
                AssertWhys(sim, vm);
            }
            Assert.That(Plot(sim, Collection).Lines.Select(l => l.Label), Does.Contain("The collector's wage a season, from Utilities"));
        }

        // The household card: the Water row says whether the pond, a well or the tower serves the home, or what going without costs; drains and waste in a big town.
        [Test]
        public void Household_Water_Drains_And_Waste()
        {
            Simulation sim = Township();
            Sim.Core.People.Household x = sim.State.Citizens.Households[0];
            UtilityDef t = UtilityRules.Def(sim, Tower);
            Sim.Core.Land.Tile far = sim.State.Land.Tiles.First(a => a.Terrain != Terrain.River && !LandValueRules.PondOrWell(sim, a.Id) && Sim.Core.Town.CoverageRules.Within(sim, a.Id, t.Tile, t.Reach));
            var home = new Sim.Core.Land.Building { Id = sim.State.Land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = far.Id };
            sim.State.Land.Buildings.Add(home);
            x.HomeId = home.Id;
            SheetVm vm = HouseholdSheet.Build(ViewKit2.Context(sim), x.Id);
            LineVm water = vm.Lines.Single(l => l.Label == "Water");
            Assert.That(water.Value, Is.EqualTo("No clean water near"));
            Assert.That(water.Note, Does.Contain("lose 4 points of health a season"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Drains"), "the old village makes it a big town");
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Waste taken away"));
            AssertWhys(sim, vm);

            Build(sim, Tower);
            Build(sim, Drains);
            Build(sim, Collection);
            vm = HouseholdSheet.Build(ViewKit2.Context(sim), x.Id);
            Assert.That(vm.Lines.Single(l => l.Label == "Water").Note, Does.StartWith("The water tower serves it"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Not.Contain("Waste taken away"));
        }

        // The Town tab: a section under Builders with the four ladders, a button to the next step's plot, who has no water and what it all costs.
        [Test]
        public void Town_Infrastructure()
        {
            Simulation sim = Township();
            SheetVm vm = TownSheet.Build(ViewKit2.Context(sim));
            int at = vm.Lines.FindIndex(l => l.IsHeader && l.Label == "Infrastructure");
            Assert.That(at, Is.GreaterThanOrEqualTo(0));
            LineVm water = vm.Lines[at + 1], power = vm.Lines[at + 2];
            Assert.That((water.Label, water.Value), Is.EqualTo(("Water", "Pond and wells")));
            Assert.That(power.Value, Is.EqualTo("None"));
            Assert.That(vm.Lines[at + 3].Value, Is.EqualTo("Pits"));
            Assert.That(vm.Lines[at + 4].Value, Is.EqualTo("None"));
            Assert.That(water.Action.Label, Is.EqualTo("Water tower"));
            Assert.That(water.Action.Opens.Value.Id, Is.EqualTo(UtilityRules.Def(sim, Tower).Tile));
            Assert.That(power.Action.Label, Is.EqualTo("Diesel generator"));
            Assert.That(vm.Lines[at + 6].Value, Is.EqualTo("0 coins"), "nothing built, nothing to pay");
            AssertWhys(sim, vm);

            Build(sim, Generator);
            Build(sim, Tower);
            vm = TownSheet.Build(ViewKit2.Context(sim));
            at = vm.Lines.FindIndex(l => l.IsHeader && l.Label == "Infrastructure");
            Assert.That(vm.Lines[at + 1].Value, Does.StartWith("Water tower"));
            Assert.That(vm.Lines[at + 1].Action.Label, Is.EqualTo("Open"));
            Assert.That(vm.Lines[at + 2].Value, Does.StartWith("Diesel generator"));
            Assert.That(vm.Lines[at + 2].Action.Label, Is.EqualTo("Grid connection"), "the next step on the power ladder");
            Assert.That(vm.Lines[at + 6].Value, Is.EqualTo(ViewKit2.Context(sim).Fmt.Coins(UtilityRules.RunningCost(sim))));
            AssertWhys(sim, vm);
        }
    }
}
