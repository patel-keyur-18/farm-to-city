using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-147 (spec 025 §3.5, Preview 15): hill land in the list, the planting sheet's apple row, the orchard's sheet, the hill acre on the map, the cold store's apple row.</summary>
    public class AppleViewTests
    {
        private static int HillTile(Simulation sim, int k) { MapDef map = sim.Config.Game.Map; return map.Parcels[map.IndexOfParcel("rh8")].Tiles[k]; }

        // The planting sheet: apple first on hill land, greyed with "Hill land only" on a valley acre, and absent before the hills open.
        [Test]
        public void Plant_Sheet()
        {
            Simulation early = ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
            Assert.That(OrchardSheet.Build(ViewKit2.Context(early), 84).Lines.Any(l => l.Label == "Apple"), Is.False, "hidden until the Township opens the hills");

            Simulation sim = Situations.Hills();
            ViewContext ctx = ViewKit2.Context(sim);
            int valley = Enumerable.Range(0, sim.State.Land.Tiles.Count).First(t => sim.State.Land.Tiles[t].Terrain == Terrain.Farm && OrchardRules.Place(sim, t) == RejectReason.None);
            SheetVm onValley = OrchardSheet.Build(ctx, valley);
            Assert.That(onValley.Lines.First().Label, Is.EqualTo("Apple"), "the hill tree first");
            LineVm apple = onValley.Lines.First();
            Assert.That(apple.Value, Does.StartWith("100 trees · fruits in Monsoon · bears in 32 d").And.Contain("Hill land only"));
            Assert.That((apple.Action.Enabled, apple.Action.DisabledReason), Is.EqualTo((false, "Hill land only.")));
            Assert.That(onValley.Lines.Skip(1).All(l => l.Action.Enabled), Is.True, "the other trees plant on any acre");
            Assert.That(WhyView.Build(ctx, apple.WhyKey, apple.WhyId).Rows.Count, Is.GreaterThan(2));

            Situations.SetCash(sim, 5_000_000_00);
            int free = HillTile(sim, 0);
            Assert.That(OrchardRules.Of(sim, free).Def, Is.EqualTo(sim.Config.Game.Farm.IndexOfTree("apple")));
        }

        // Your land › For sale: a hill row with its own words, the price's ? naming the distance, and a note on what grows there.
        [Test]
        public void Land_List_And_Price()
        {
            Simulation sim = Situations.Township();
            Situations.SetCash(sim, 5_000_000_00);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm sale = LandsSheet.Build(ctx, LandsSheet.ForSale);
            LineVm hills = sale.Lines.First(l => l.Label != null && l.Label.StartsWith("Hills · "));
            Assert.That(hills.Label, Is.EqualTo("Hills · 2 acres").Or.EqualTo("Hills · 4 acres"));
            Assert.That(hills.Note, Is.EqualTo("Not next to your land yet"));
            Assert.That(hills.Value, Does.EndWith(" coins · sandy, dry, cool"));
            WhyVm why = WhyView.Build(ctx, hills.WhyKey, hills.WhyId);
            Assert.That(why.Rows.Any(r => r.Label == "The hills sit far from the road and the market"), Is.True);

            // Beside your land, the note says what the hills are for.
            Simulation near = Situations.Hills();
            SheetVm owned = LandsSheet.Build(ViewKit2.Context(near), LandsSheet.ForSale);
            Assert.That(owned.Lines.Any(l => l.Note == "Apples grow here · no lane yet"), Is.True);
        }

        // The orchard's own sheet says apple; a bearing batch shows its figures and a payback.
        [Test]
        public void Orchard_Sheet()
        {
            Simulation sim = Situations.Hills();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm sheet = FieldSheets.ForTile(ctx, HillTile(sim, 0));
            Assert.That((sheet.Title, sheet.Subtitle), Is.EqualTo(("Apple orchard", "100 young trees")));
            Assert.That(sheet.Lines.Any(l => l.Label == "Bears fruit in"), Is.True);
            Assert.That(sheet.Lines.Single(l => l.Label == "Pays for itself").Value, Is.EqualTo("around the 1st harvest"), "a tree that fruits once a year says the harvest, not a season");
            Assert.That(sheet.Lines.Any(l => l.Label == "Irrigation" || l.Label == "Pump on the well"), Is.False, "the hills are rain-fed: no pump or channel offered");
            Assert.That(sheet.Actions.Concat(sheet.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons)).Any(a => a.Id.StartsWith("pump") || a.Id.StartsWith("borewell")), Is.False);
            Assert.That(sheet.Actions.Select(a => a.Id), Is.EqualTo(new[] { "sow", "clearOrchard" }));
            Assert.That(sheet.Lines.All(l => l.WhyKey == null || WhyView.Has(l.WhyKey)), Is.True);
        }

        // A hill acre you own is a field on the map; one for sale is hill.
        [Test]
        public void Map_Kinds()
        {
            Simulation sim = Situations.Hills();
            MapVm map = MapView.Build(ViewKit2.Context(sim));
            Assert.That(map.Tiles[HillTile(sim, 0)].Kind, Is.EqualTo(TileKind.Farm));
            MapDef defs = sim.Config.Game.Map;
            int other = defs.Parcels[defs.IndexOfParcel("rh1")].Tiles[0];
            Assert.That(map.Tiles[other].Kind, Is.EqualTo(TileKind.Hill));
            Assert.That(map.Orchards.Select(o => o.TreeId), Does.Contain("apple"));
        }

        // The cold store lists apple among the kept fruit and answers "is it worth it?" from the sim's own prices.
        [Test]
        public void Cold_Store_Apple()
        {
            Simulation sim = Situations.Township();
            Situations.SetCash(sim, 5_000_000_00);
            SheetVm vm = ColdStoreSheet.Build(ViewKit2.Context(sim));
            Assert.That(vm.Lines.Single(l => l.Label == "Apple" && l.Note != null).Note, Does.StartWith("Keeps 96 of every 100 kg over a season (85 in the hut)"));
            Assert.That(vm.Lines.Any(l => l.Label == "Apple, Monsoon to Summer" && l.Value.StartsWith("+")), Is.True, "the worked answer: apples picked in the Monsoon fetch the Summer price");
        }
    }
}
