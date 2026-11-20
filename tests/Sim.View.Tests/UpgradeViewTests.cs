using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-152 (spec 024 §3.7, Preview 5): the acre's rows and buttons, the card, the work under way, the made-pucca family sheet, why a house is not offered, the builders' count, and every number's Why?.</summary>
    public class UpgradeViewTests
    {
        private const long Hour = 3600;

        private static Simulation Township()
        {
            Simulation sim = Situations.Township();
            Situations.SetCash(sim, 5_000_000_00);
            return sim;
        }

        private static int WaterTile(Simulation sim, string lv)
        {
            foreach (Tile t in sim.State.Land.Tiles)
                if (t.Terrain == Terrain.Farm && LandValueRules.HasWater(sim, t.Id) && sim.State.Land.Parcels[t.ParcelId].Owned)
                {
                    sim.State.Township.LandValues[t.Id] = Fixed.Parse(lv);
                    return t.Id;
                }
            Assert.Fail("no owned acre with water near");
            return -1;
        }

        /// <summary>The Situation's family house, moved to an acre with water near at land value <paramref name="lv"/>, with its family on a lease.</summary>
        private static Building TheHouse(Simulation sim, string lv, out Household x, out int tile)
        {
            tile = WaterTile(sim, lv);
            Building b = sim.State.Land.Buildings.First(h => HouseRules.IsHouse(sim, h) && !h.Builders);
            long home = b.Id;
            x = sim.State.Citizens.Households.First(h => h.HomeId == home);
            b.TileId = tile;
            x.Income = 1_000_000_00;
            x.LeaseStart = LeaseRules.Season(sim);
            x.LeaseRent = LeaseRules.MarketRentFor(sim, x);
            return b;
        }

        private static Building EmptyHouse(Simulation sim, int tile)
        {
            var b = new Building { Id = sim.State.Land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = tile, Cost = 6_000_000 };
            sim.State.Land.Buildings.Add(b);
            return b;
        }

        private static SheetVm Acre(Simulation sim, int tile) { return HomesteadSheet.Build(ViewKit2.Context(sim), sim.State.Land.Tiles[tile]); }

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

        // The acre's sheet: a house offered has the button with its cost; the card says the cash, materials, masons, when and what the rent does.
        [Test]
        public void Offered_Button_And_Card()
        {
            Simulation sim = Township();
            Building b = TheHouse(sim, "1.20", out Household x, out int tile);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = Acre(sim, tile);
            Assert.That(vm.Lines.Single(l => l.Label == "Pucca upgrade").Value, Is.EqualTo("Offered"));
            ActionVm go = vm.Actions.Single(a => a.Id == "upgrade:" + b.Id);
            Assert.That(go.Enabled, Is.True);
            Assert.That(go.Label, Is.EqualTo("Upgrade to pucca"));
            Assert.That(go.Detail, Does.Contain(ctx.Fmt.Coins(UpgradeRules.Quote(sim, BuildProject.Yours).Total)).And.Contain("rent rises"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Land value of this acre"));
            AssertWhys(sim, vm);

            ConfirmCardVm card = ConfirmCards.For(ctx, go.Create(ctx.Now));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Title, Is.EqualTo("Upgrade to pucca?"));
            Assert.That(card.Subtitle, Does.Contain("Family house 1").And.Contain("stay"));
            Assert.That(card.Rows.First().Label, Is.EqualTo("Your cash now"));
            Assert.That(card.Rows.Last().Label, Is.EqualTo("Your cash after"));
            Assert.That(card.Warning, Does.Contain("comes back in about").And.Contain("Their rent stays"));
            Assert.That(-card.CashChange, Is.EqualTo(UpgradeRules.Quote(sim, BuildProject.Yours).Total));
        }

        // An empty house or a hand's: the card says nothing comes back in rent.
        [Test]
        public void Empty_House_Card_Says_Nothing_Comes_Back()
        {
            Simulation sim = Township();
            TheHouse(sim, "1.20", out Household x, out int tile);
            Building b = EmptyHouse(sim, tile);
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new UpgradeHouse(ctx.Now, b.Id));
            Assert.That(card.Subtitle, Does.Contain("empty"));
            Assert.That(card.Warning, Does.Contain("Nothing comes back in rent"));
            Assert.That(Acre(sim, tile).Actions.Single(a => a.Id == "upgrade:" + b.Id).Detail, Does.Contain("nothing comes back"));
        }

        // The reasons are said with their numbers; there is no button, and the row's Why? lists every test.
        [Test]
        public void Not_Offered_Says_Why()
        {
            Simulation sim = Township();
            Building b = TheHouse(sim, "1.08", out Household x, out int tile);
            SheetVm vm = Acre(sim, tile);
            Assert.That(vm.Lines.Single(l => l.Label == "Pucca upgrade").Value, Is.EqualTo("Land value 108%, needs 115%"));
            Assert.That(vm.Actions.Any(a => a.Id == "upgrade:" + b.Id), Is.False);
            WhyVm why = WhyView.Build(ViewKit2.Context(sim), "upgrade", b.Id);
            Assert.That(why.Rows.Where(r => r.Value != null).Select(r => r.Label), Is.EquivalentTo(new[] { "Land value of the acre", "Clean water near", "The family can pay", "Rent as a kutcha house", "Rent as a pucca house", "The upgrade costs", "Back in about" }));
            sim.State.Township.LandValues[tile] = Fixed.Parse("1.20");
            x.Income = 10_000_00;
            Assert.That(Acre(sim, tile).Lines.Single(l => l.Label == "Pucca upgrade").Value, Is.EqualTo("Rent is over what they can pay"));
            Assert.That(WhyView.Build(ViewKit2.Context(sim), "upgrade", b.Id).Rows.Single(r => r.Label == "The family can pay").Value, Is.EqualTo("No"));
        }

        // Under way: the site's own rows (what it waits for, masons, ready in) are the construction ones; then the house is pucca and the family's sheet shows the rent in steps.
        [Test]
        public void Underway_Then_Pucca()
        {
            Simulation sim = Township();
            Building b = TheHouse(sim, "1.20", out Household x, out int tile);
            CommandResult res = sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, b.Id));
            Assert.That(res.Accepted, Is.True, res.ToString());
            SheetVm vm = Acre(sim, tile);
            Assert.That(vm.Lines.Single(l => l.Label == "Pucca upgrade").Value, Is.EqualTo("Being made pucca"));
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Ready in"));
            Assert.That(vm.Actions.Any(a => a.Id == "upgrade:" + b.Id), Is.False);
            AssertWhys(sim, vm);
            SheetVm family = HouseholdSheet.Build(ViewKit2.Context(sim), x.Id);
            Assert.That(family.Lines.Single(l => l.Label == "The house").Value, Is.EqualTo("Being made pucca"));
            for (int i = 0; i < 400 && !b.Pucca; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(b.Pucca, Is.True);
            vm = Acre(sim, tile);
            Assert.That(vm.Lines.Single(l => l.Label == "Pucca upgrade").Value, Is.EqualTo("Pucca house"));
            family = HouseholdSheet.Build(ViewKit2.Context(sim), x.Id);
            Assert.That(family.Lines.Single(l => l.Label == "The house").Value, Is.EqualTo("Pucca"));
            Assert.That(family.Lines.Select(l => l.Label), Does.Contain("Pucca rent, in steps").And.Contain("The next renewal"));
            AssertWhys(sim, family);
            Assert.That(WhyView.Build(ViewKit2.Context(sim), "household.rent", x.Id).Rows.Any(r => r.Label.Contains("pucca")), Is.True);
        }

        // The Builders section counts the pucca homes.
        [Test]
        public void Builders_Count()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim, "1.20");
            Building a = EmptyHouse(sim, tile);
            a.Owner = 1;
            a.Pucca = true;
            EmptyHouse(sim, tile).Owner = 1;
            sim.State.Township.BuilderHomes = 2;
            var vm = new SheetVm();
            ZoningViews.TownRows(ViewKit2.Context(sim), vm);
            Assert.That(vm.Lines.Single(l => l.Label == "Of them pucca").Value, Is.EqualTo("1"));
            AssertWhys(sim, vm);
        }

        // The map draws a house pucca, or in its scaffolding while it is made so.
        [Test]
        public void Map_Marks_Pucca_And_Scaffold()
        {
            Simulation sim = Township();
            Building b = TheHouse(sim, "1.20", out Household x, out int tile);
            Assert.That(MapView.Build(ViewKit2.Context(sim)).Buildings.Single(v => v.Id == b.Id).Pucca, Is.False);
            Assert.That(sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, b.Id)).Accepted, Is.True);
            BuildingVm v = MapView.Build(ViewKit2.Context(sim)).Buildings.Single(h => h.Id == b.Id);
            Assert.That((v.Pucca, v.Upgrading), Is.EqualTo((false, true)));
            for (int i = 0; i < 400 && !b.Pucca; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            v = MapView.Build(ViewKit2.Context(sim)).Buildings.Single(h => h.Id == b.Id);
            Assert.That((v.Pucca, v.Upgrading), Is.EqualTo((true, false)));
        }
    }
}
