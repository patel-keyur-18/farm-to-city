using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-142 (spec 024 §3.3, Preview 7): the zone rows and cards on an empty field, the families waiting and their Why?, the Town sheet's Builders section, a builder's family, the builders' acre and the away lines.</summary>
    public class ZoningViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Execute(c).Reason); }

        private static Simulation Township() { return Situations.Township(); }

        private static string Bought(Simulation sim)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 50_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -50_000_000));
            ParcelDef p = sim.Config.Game.Map.Parcels.First(x => x.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(x.Id)].Owned && ZoningRules.Serviced(sim, sim.Config.Game.Map.IndexOfParcel(x.Id)) && LandSystem.IsAdjacentToOwned(sim, sim.Config.Game.Map.IndexOfParcel(x.Id)));
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, p.Id));
            return p.Id;
        }

        private static int Tile(Simulation sim, string parcel) { return sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel(parcel)].Tiles.First(t => sim.State.Land.Tiles[t].Terrain == Terrain.Farm); }

        private static void Waiting(Simulation sim, int n) { typeof(TownshipState).GetProperty("TurnedAway").GetSetMethod(true).Invoke(sim.State.Township, new object[] { n }); }

        // Before the Township an empty field has no zone rows and no button; from it, the rows and Offer to builders; the inherited acres never.
        [Test]
        public void FieldRows()
        {
            Simulation before = Situations.Town();
            Assert.That(ZoningRules.On(before), Is.False);
            Tile inherited = before.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && before.State.Land.Parcels[t.ParcelId].Protected && !before.State.Farm.CropOn(t.Id)?.Equals(null) != false && !before.State.Land.Tiles[t.Id].Occupied);
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(before), inherited.Id).Actions.Any(a => a.Id == "zone"), Is.False);

            Simulation sim = Township();
            string id = Bought(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = FieldSheets.ForTile(ctx, Tile(sim, id));
            int acres = ZoningRules.HomeTiles(sim, sim.Config.Game.Map.IndexOfParcel(id)).Count;
            Assert.That(vm.Lines.Single(l => l.Label == "Zoned for").Value, Is.EqualTo("Farming · " + ctx.Strings.Plural("land.acres", acres)));
            Assert.That(vm.Lines.Any(l => l.Label == "Land value of this acre"), Is.True);
            ActionVm zone = vm.Actions.Single(a => a.Id == "zone");
            Assert.That(zone.Label, Is.EqualTo("Offer to builders"));
            Assert.That(vm.Actions.Any(a => a.Id == "unzone"), Is.False);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash));

            Tile own = sim.State.Land.Tiles.First(t => sim.State.Land.Parcels[t.ParcelId].Protected && t.Terrain == Terrain.Farm && !t.Occupied);
            Assert.That(FieldSheets.ForTile(ctx, own.Id).Actions.Any(a => a.Id == "zone"), Is.False, "inherited land is never sold");
        }

        // The offer card, the zoned sheet with the families waiting, and taking it back.
        [Test]
        public void ZoneCardAndSheet()
        {
            Simulation sim = Township();
            string id = Bought(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            int pid = sim.Config.Game.Map.IndexOfParcel(id);
            ConfirmCardVm card = ConfirmCards.For(ctx, new ZoneParcel(ctx.Now, id));
            Assert.That(card.Title, Is.EqualTo("Offer to builders?"));
            Assert.That(card.Hold, Is.False);
            Assert.That(card.Reassure, Is.True);
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Its land value today", "A builder buys it when", "You receive then" }));
            Assert.That(card.Rows[1].Value, Is.EqualTo(ctx.Strings.Plural("confirm.ZoneParcel.families", ZoningRules.Need(sim, pid))));
            Assert.That(card.Rows[2].Value, Is.EqualTo(ctx.Fmt.Coins(LandRules.ParcelPrice(sim, sim.State.Land.Parcels[pid]))));
            Assert.That(card.CashChange, Is.EqualTo(0), "nothing is paid by you");

            Ok(sim, new ZoneParcel(sim.State.Clock.SimTime, id));
            Waiting(sim, 2);
            ctx = ViewKit2.Context(sim);
            SheetVm vm = FieldSheets.ForTile(ctx, Tile(sim, id));
            Assert.That(vm.Lines.Single(l => l.Label == "Zoned for").Value, Does.StartWith("Houses"));
            Assert.That(vm.Lines.Single(l => l.Label == "Families waiting for a home").Value, Is.EqualTo("2 of " + ZoningRules.Need(sim, pid)));
            Assert.That(vm.Actions.Select(a => a.Id), Does.Contain("unzone"));
            Assert.That(vm.Actions.Any(a => a.Id == "zone"), Is.False);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                WhyTests.AssertWords(l.Label, why);
            }
            WhyVm waiting = WhyView.Build(ctx, ZoningViews.WaitingWhy, pid);
            Assert.That(waiting.Rows.Select(r => r.Label), Does.Contain("Road and water near it"));
            Assert.That(waiting.Rows.Single(r => r.Label == "A builder buys").Value, Is.EqualTo("at most 1 parcel a season"));
            Ok(sim, new UnzoneParcel(sim.State.Clock.SimTime, id));
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), Tile(sim, id)).Actions.Any(a => a.Id == "zone"), Is.True);
        }

        // After a builder buys: the acre's sheet, the Town sheet's Builders section, the family's tag and rent, the For sale list and the away line.
        [Test]
        public void AfterBuilders()
        {
            Simulation sim = Township();
            int first = ZoningRules.Candidates(sim).First();
            string id = sim.Config.Game.Map.Parcels[first].Id;
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(LandsSheet.Build(ctx, LandsSheet.ForSale).Lines.Any(l => l.Label.Contains("acre")), Is.True);
            int forSale = LandsSheet.Build(ctx, LandsSheet.ForSale).Lines.Count(l => l.Action != null && l.Action.Id.StartsWith("show:"));
            Waiting(sim, ZoningRules.Need(sim, first));
            sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason - 1);
            Waiting(sim, ZoningRules.Need(sim, first));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3600);
            ctx = ViewKit2.Context(sim);
            Assert.That(sim.State.Land.Parcels[first].BuilderOwned, Is.True);
            Assert.That(sim.DrainEvents().OfType<AcreSoldToBuilder>().Count(), Is.EqualTo(1));

            SheetVm acre = FieldSheets.ForTile(ctx, sim.Config.Game.Map.Parcels[first].Tiles[0]);
            Assert.That(acre.Title, Is.EqualTo("Builders' homes"));
            Assert.That(acre.Lines.Single(l => l.Label == "The builders").Value, Does.StartWith("Waiting for bricks"), "their homes take bricks like yours (spec 025 §3.2)");
            Assert.That(acre.Lines.Single(l => l.Label == "Homes here").Value, Is.EqualTo("0"), "none stands until the materials are in and the builders are done");
            Assert.That(LandsSheet.Build(ctx, LandsSheet.ForSale).Lines.Count(l => l.Action != null && l.Action.Id.StartsWith("show:")), Is.EqualTo(forSale - 1), "a builder's parcel is no longer for sale");
            Assert.That(MapView.Build(ctx).Tiles.First(t => t.Id == sim.Config.Game.Map.Parcels[first].Tiles[0]).ForSaleSign, Is.False);

            for (int day = 0; day < 120 && !sim.State.Citizens.Households.Exists(h => HouseholdRules.InBuildersHome(sim, h)); day++) sim.AdvanceTo(sim.State.Clock.SimTime + 86400); // bricks, masons' hours, then a family
            ctx = ViewKit2.Context(sim);
            SheetVm town = TownSheet.Build(ctx);
            Assert.That(town.Lines.Select(l => l.Label), Does.Contain("Homes built by builders"));
            Assert.That(town.Lines.Single(l => l.Label == "Homes built by builders").Value, Is.EqualTo(ctx.Fmt.Integer(sim.State.Land.Buildings.Count(b => b.Builders))));
            Household x = sim.State.Citizens.Households.FirstOrDefault(h => HouseholdRules.InBuildersHome(sim, h));
            Assert.That(x, Is.Not.Null);
            Assert.That(town.Lines.Any(l => l.Note != null && l.Note.Contains("a builder's home")), Is.True);
            SheetVm hh = HouseholdSheet.Build(ctx, x.Id);
            Assert.That(hh.Lines.First(l => l.WhyKey == "household.rent").Value, Does.Contain("paid to the builder"));
            Assert.That(hh.Lines.Any(l => l.Label == "Lease"), Is.False.Or.True);
            foreach (LineVm l in town.Lines.Where(l => l.WhyKey == ZoningViews.WaitingWhy)) WhyTests.AssertWords(l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
        }

        // The away card names what builders bought and built, yours or a neighbour's.
        [Test]
        public void AwayLines()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            var events = new SimEvent[] { new AcreSoldToBuilder(0, "e1", 383467_00, true), new AcreSoldToBuilder(0, "ne1", 0, false) };
            var rows = TownAway.Rows(ctx, events);
            Assert.That(rows.Select(r => r.Label + " " + r.Value), Has.Some.Contain("A builder bought your land for"));
            Assert.That(rows.Select(r => r.Label + " " + r.Value), Has.Some.Contain("Builders bought a neighbour's land"));
        }
    }
}
