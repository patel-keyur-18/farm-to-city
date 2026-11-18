using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-261 (spec 024 §3.6, Preview 8): the lease button and card on an empty field, the leased sheet, the rent's Why?, Your land and the away lines.</summary>
    public class LandLeaseViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Execute(c).Reason); }

        private static string Bought(Simulation sim)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 50_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -50_000_000));
            ParcelDef p = sim.Config.Game.Map.Parcels.First(x => x.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(x.Id)].Owned && ZoningRules.Serviced(sim, sim.Config.Game.Map.IndexOfParcel(x.Id)) && LandSystem.IsAdjacentToOwned(sim, sim.Config.Game.Map.IndexOfParcel(x.Id)));
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, p.Id));
            return p.Id;
        }

        private static int Tile(Simulation sim, string parcel) { return sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel(parcel)].Tiles.First(t => sim.State.Land.Tiles[t].Terrain == Terrain.Farm); }

        // Before the Township no button; from it, Lease out with the rent beneath; zoned land greys it with the way out; an inherited acre can be leased.
        [Test]
        public void ButtonAndCard()
        {
            Simulation before = Situations.Town();
            Tile inheritedBefore = before.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && before.State.Land.Parcels[t.ParcelId].Protected && !t.Occupied);
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(before), inheritedBefore.Id).Actions.Any(a => a.Id == "lease"), Is.False);

            Simulation sim = Situations.Township();
            string id = Bought(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            ActionVm lease = FieldSheets.ForTile(ctx, Tile(sim, id)).Actions.Single(a => a.Id == "lease");
            Assert.That(lease.Enabled, Is.True);
            Assert.That(lease.Label, Is.EqualTo("Lease out"));
            Parcel p = sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)];
            Assert.That(lease.Detail, Is.EqualTo(ctx.Fmt.Coins(LandLeaseRules.Rent(sim, p)) + " a season"));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "looking changes nothing");

            ConfirmCardVm card = ConfirmCards.For(ctx, new LeaseOutLand(ctx.Now, id));
            Assert.That(card.Title, Is.EqualTo("Lease this land out?"));
            Assert.That(card.Reassure, Is.True);
            Assert.That(card.CashChange, Is.EqualTo(0), "nothing at the tap");
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Its land value today", "The rent a season", "The first rent comes" }));
            Assert.That(card.Rows[1].Value, Is.EqualTo(ctx.Fmt.Coins(LandLeaseRules.Rent(sim, p))));

            Ok(sim, new ZoneParcel(sim.State.Clock.SimTime, id));
            ActionVm greyed = FieldSheets.ForTile(ViewKit2.Context(sim), Tile(sim, id)).Actions.Single(a => a.Id == "lease");
            Assert.That(greyed.Enabled, Is.False);
            Assert.That(greyed.DisabledReason, Is.EqualTo("Take it back to farming first."));

            Tile own = sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && LandLeaseRules.CanLease(sim, t.ParcelId) == RejectReason.None && sim.State.Land.Parcels[t.ParcelId].Protected);
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(sim), own.Id).Actions.Any(a => a.Id == "lease" && a.Enabled), Is.True, "inherited land can be leased");
        }

        // A crop on another acre of the parcel greys the button with its reason.
        [Test]
        public void RefusalsSayWhy()
        {
            Simulation sim = Situations.Township();
            string id = Bought(sim);
            var tiles = sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel(id)].Tiles.Where(t => sim.State.Land.Tiles[t].Terrain == Terrain.Farm).ToList();
            Assert.That(tiles.Count, Is.GreaterThan(1), "the test needs a parcel of more than one acre");
            bool sown = sim.Config.Game.Farm.Crops.Any(c => sim.Execute(new Sow(sim.State.Clock.SimTime, tiles[0], c.Id, Sim.Core.Farm.SeedSource.Certified, "none")).Accepted);
            Assert.That(sown, Is.True);
            ActionVm lease = FieldSheets.ForTile(ViewKit2.Context(sim), tiles[1]).Actions.Single(a => a.Id == "lease");
            Assert.That(lease.Enabled, Is.False);
            Assert.That(lease.DisabledReason, Does.StartWith("Empty the acre first"));
        }

        // Leased: the sheet says so, shows the rent and its Why?, drops Sow and Offer to builders, and offers End lease (a card). Not started: Cancel, no card.
        [Test]
        public void LeasedSheet()
        {
            Simulation sim = Situations.Township();
            string id = Bought(sim);
            Ok(sim, new LeaseOutLand(sim.State.Clock.SimTime, id));
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = FieldSheets.ForTile(ctx, Tile(sim, id));
            Assert.That(vm.Subtitle, Does.Contain("locked"));
            Assert.That(vm.Lines.Single(l => l.Label == "Leased out").Value, Is.EqualTo("From the next close"));
            Assert.That(vm.Actions.Select(a => a.Id), Is.EqualTo(new[] { "endLease" }));
            Assert.That(vm.Actions[0].Label, Is.EqualTo("Cancel the lease"));
            Assert.That(ConfirmCards.For(ctx, new EndLease(ctx.Now, id)), Is.Null, "cancelling before the start costs nothing");

            sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            ctx = ViewKit2.Context(sim);
            vm = FieldSheets.ForTile(ctx, Tile(sim, id));
            Assert.That(vm.Subtitle, Does.Contain("leased out"));
            Parcel p = sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)];
            Assert.That(vm.Lines.Single(l => l.Label == "Leased out").Value, Is.EqualTo(ctx.Fmt.Coins(LandLeaseRules.Rent(sim, p)) + " a season"));
            Assert.That(vm.Actions[0].Label, Is.EqualTo("End lease"));
            ConfirmCardVm end = ConfirmCards.For(ctx, new EndLease(ctx.Now, id));
            Assert.That(end.Title, Is.EqualTo("End the lease?"));
            Assert.That(end.Rows.Select(r => r.Label), Is.EqualTo(new[] { "This season's rent", "The tenant leaves", "After that" }));
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null)) WhyTests.AssertWords(l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
            WhyVm why = WhyView.Build(ctx, LandLeaseViews.RentWhy, p.Id);
            Assert.That(why.Rows.Any(r => r.Value != null && r.Value.StartsWith("1 coin in every 40")), Is.True);
        }

        // Your land: a leased parcel says so with its rent and End lease in place of Sell; an inherited acre offers Lease out.
        [Test]
        public void YourLandRows()
        {
            Simulation sim = Situations.Township();
            string id = Bought(sim);
            Ok(sim, new LeaseOutLand(sim.State.Clock.SimTime, id));
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm yours = LandsSheet.Build(ctx);
            LineVm row = yours.Lines.First(l => l.Note != null && l.Note.StartsWith("Leased from the next close"));
            Assert.That(row.Action.Id, Is.EqualTo("endLease:" + id));
            Assert.That(row.Also.Id, Does.StartWith("show:"));
            Assert.That(yours.Lines.Any(l => l.Action != null && l.Action.Id.StartsWith("lease:")), Is.True, "an inherited acre can be leased");
        }

        // An ending lease leaves Show only on Your land (nothing to sell or press), and the End card's safe button says Keep leasing.
        [Test]
        public void EndingRowAndCard()
        {
            Simulation sim = Situations.Township();
            string id = Bought(sim);
            Ok(sim, new LeaseOutLand(sim.State.Clock.SimTime, id));
            sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(ConfirmCards.For(ctx, new EndLease(ctx.Now, id)).SafeLabel, Is.EqualTo("Keep leasing"));
            Ok(sim, new EndLease(sim.State.Clock.SimTime, id));
            LineVm row = LandsSheet.Build(ViewKit2.Context(sim)).Lines.First(l => l.Note != null && l.Note.StartsWith("Lease ends"));
            Assert.That(row.Action.Id, Does.StartWith("show:"));
            Assert.That(row.Also, Is.Null);
            SheetVm field = FieldSheets.ForTile(ViewKit2.Context(sim), Tile(sim, id));
            Assert.That(field.Lines.Any(l => l.Label == "Irrigation"), Is.False, "a leased acre has no water to install");
        }

        // The away card names the rent and the end of a lease.
        [Test]
        public void AwayLines()
        {
            Simulation sim = Situations.Township();
            string id = Bought(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            var rows = TownAway.Rows(ctx, new SimEvent[] { new LandRentPaid(0, id, 958700), new LeaseEnded(0, id) });
            Assert.That(rows.Select(r => r.Label + " " + r.Value), Has.Some.Contain("received"));
            Assert.That(rows.Select(r => r.Label + " " + r.Value), Has.Some.Contain("ended"));
        }
    }
}
