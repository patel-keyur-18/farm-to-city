using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-143 (spec 024 §3.1–§3.5, Preview 6): the land value row and its Why?, the price's Why?, Your land with the sale choice, the slow-sale cards, a listed parcel and the rent's place.</summary>
    public class LandValueViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Execute(c).Reason); }

        private static Simulation Township()
        {
            Simulation sim = Situations.Township();
            Assert.That(LandValueRules.On(sim), Is.True, "the Township has read land values");
            return sim;
        }

        private static string Bought(Simulation sim)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 50_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -50_000_000));
            ParcelDef p = sim.Config.Game.Map.Parcels.First(x => x.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(x.Id)].Owned && LandSystem.IsAdjacentToOwned(sim, sim.Config.Game.Map.IndexOfParcel(x.Id)));
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, p.Id));
            return p.Id;
        }

        private static int Tile(Simulation sim, string parcel) { return sim.Config.Game.Map.Parcels[sim.Config.Game.Map.IndexOfParcel(parcel)].Tiles[0]; }

        private static void AssertWhys(Simulation sim, SheetVm vm)
        {
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.Label);
                WhyVm why = WhyView.Build(ctx, l.WhyKey, l.WhyId);
                Assert.That(why, Is.Not.Null, l.Label);
                Assert.That(why.Rows.Count, Is.GreaterThan(0), l.Label);
            }
        }

        // Before the Township the sheet has no land value row; from it, one row under the price with its Why? in words.
        [Test]
        public void AcreRow()
        {
            Simulation before = Situations.Town();
            Tile unowned = before.State.Land.Tiles.First(t => !before.State.Land.Parcels[t.ParcelId].Owned && before.Config.Game.Map.Parcels[t.ParcelId].ForSale);
            Assert.That(FieldSheets.ForTile(ViewKit2.Context(before), unowned.Id).Lines.Any(l => l.Label == "Land value of this acre"), Is.False);

            Simulation sim = Township();
            int tile = sim.Config.Game.Map.Parcels.Where(p => p.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(p.Id)].Owned).SelectMany(p => p.Tiles).First(t => LandValueRules.Of(sim, t) > Sim.Core.Maths.Fixed.One);
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = FieldSheets.ForTile(ctx, tile);
            LineVm row = vm.Lines.Single(l => l.Label == "Land value of this acre");
            Assert.That(row.Value, Is.EqualTo("×" + ctx.Fmt.Decimal2(LandValueRules.Of(sim, tile))));
            Assert.That(vm.Lines.IndexOf(row), Is.EqualTo(vm.Lines.FindIndex(l => l.Label == "Price") + 1));
            AssertWhys(sim, vm);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        // The Why? names each part that counts with what it adds, and what is not near enough yet; every part is a row.
        [Test]
        public void WhyInWords()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            int tile = sim.Config.Game.Map.Parcels.SelectMany(p => p.Tiles).First(t => LandValueRules.Parts(sim, t).Any(p => p.Key == "lane"));
            WhyVm why = WhyView.Build(ctx, "landValue", tile);
            Assert.That(why.Value, Is.EqualTo(WhyView.Effect(ctx, LandValueRules.Of(sim, tile))), "words, not a factor");
            Assert.That(why.Rows[0].Label, Is.EqualTo("Every acre starts at"));
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("On the village lane"));
            Assert.That(why.Rows.Single(r => r.Label == "On the village lane").Value, Is.EqualTo("+10%"));
            Assert.That(why.Rows.Select(r => r.Label), Has.Some.StartsWith("Not near enough yet:"));
            Assert.That(why.Rows.Select(r => r.Label), Has.Some.StartsWith("It is read once a season"));
            Assert.That(why.Footnote, Is.Not.Empty);
            // the price's Why? gets its row
            Assert.That(WhyView.Build(ctx, "landPrice", tile).Rows.Select(r => r.Label), Does.Contain("Near what makes it valuable (average of its acres)"));
        }

        // Your land: a bought parcel says its land value and its Sell button opens the choice; before the Township it holds to sell at once.
        [Test]
        public void YoursAndChoice()
        {
            Simulation sim = Township();
            string id = Bought(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm yours = LandsSheet.Build(ctx);
            LineVm row = yours.Lines.First(l => l.Note != null && l.Note.StartsWith("Bought"));
            Assert.That(row.Note, Does.Contain("land value ×"));
            Assert.That(row.Action.Id, Is.EqualTo("sellLand:" + id));
            Assert.That(row.Action.Opens.Value.Kind, Is.EqualTo("SellLand"));

            SheetVm choice = SellLandSheet.Build(ctx, (int)row.Action.Opens.Value.Id);
            long value = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)]);
            long quick = Sim.Core.Finance.Money.RoundToCoin(Sim.Core.Finance.Money.MulRate(value, sim.Config.Game.Land.QuickSaleFactor));
            Assert.That(choice.Title, Is.EqualTo("Sell this land"));
            Assert.That(choice.Lines.Single(l => l.Label == "Its land value today").Value, Is.EqualTo(ctx.Fmt.Coins(value)));
            Assert.That(choice.Lines.Single(l => l.Label == "You receive today (80%)").Value, Is.EqualTo(ctx.Fmt.Coins(quick)));
            Assert.That(choice.Lines.Single(l => l.Label == "You receive in 2 days (100%)").Value, Is.EqualTo(ctx.Fmt.Coins(value)));
            Assert.That(choice.Lines.Single(l => l.Label == "Waiting pays you more").Value, Is.EqualTo("+" + ctx.Fmt.Coins(value - quick)));
            Assert.That(choice.Actions.Select(a => a.Id), Is.EqualTo(new[] { "sellSlow:" + id, "sellNow:" + id }));
            AssertWhys(sim, choice);

            Simulation before = Situations.Town();
            string b = Bought(before);
            LineVm old = LandsSheet.Build(ViewKit2.Context(before)).Lines.First(l => l.Note != null && l.Note.StartsWith("Bought"));
            Assert.That(old.Action.Id, Is.EqualTo("sellParcel:" + b), "before the Township: the hold card at once");
        }

        // The slow sale's card, the listed row, and the card to take it off.
        [Test]
        public void SlowSaleCards()
        {
            Simulation sim = Township();
            string id = Bought(sim);
            ViewContext ctx = ViewKit2.Context(sim);
            long value = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(id)]);
            Assert.That(sim.Preview(new ListParcel(ctx.Now, id)).Reason, Is.EqualTo(RejectReason.None));
            ConfirmCardVm card = ConfirmCards.For(ctx, new ListParcel(ctx.Now, id));
            Assert.That(card.Title, Is.EqualTo("Sell slowly?"));
            Assert.That(card.Hold, Is.False, "a slow sale can be taken off");
            Assert.That(card.Reassure, Is.True);
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Its land value today", "It waits", "You receive then" }));
            Assert.That(card.Rows[1].Value, Is.EqualTo("2 days"));
            Assert.That(card.Rows[2].Value, Is.EqualTo(ctx.Fmt.Coins(value)));
            Assert.That(card.Warning, Does.Contain("locked"));
            Assert.That(card.Subtitle, Is.Not.Empty);

            Ok(sim, new ListParcel(sim.State.Clock.SimTime, id));
            ctx = ViewKit2.Context(sim);
            LineVm row = LandsSheet.Build(ctx).Lines.First(l => l.Note != null && l.Note.StartsWith("For sale"));
            Assert.That(row.Note, Does.Contain(ctx.Fmt.Coins(value)).And.Contain("in 2 d").And.Contain("locked"));
            Assert.That(row.Action.Id, Is.EqualTo("cancelSale:" + id));
            ConfirmCardVm off = ConfirmCards.For(ctx, new CancelSale(ctx.Now, id));
            Assert.That(off.Title, Is.EqualTo("Take it off the market?"));
            Assert.That(off.Rows.Select(r => r.Value), Is.EqualTo(new[] { "unlocked now", "nothing" }));
        }

        // The buy card in the Township says selling slowly fetches all of it.
        [Test]
        public void BuyCardMentionsSlowSale()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            string id = sim.Config.Game.Map.Parcels.First(x => x.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(x.Id)].Owned && LandSystem.IsAdjacentToOwned(sim, sim.Config.Game.Map.IndexOfParcel(x.Id))).Id;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 50_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -50_000_000));
            Assert.That(ConfirmCards.For(ViewKit2.Context(sim), new BuyParcel(ctx.Now, id)).Warning, Does.Contain("selling slowly"));
            Simulation before = Situations.Town();
            string b = before.Config.Game.Map.Parcels.First(x => x.ForSale && LandSystem.IsAdjacentToOwned(before, before.Config.Game.Map.IndexOfParcel(x.Id)) && !before.State.Land.Parcels[before.Config.Game.Map.IndexOfParcel(x.Id)].Owned).Id;
            before.State.Ledger.Post(before.State.Clock.SimTime, before.Period, "test", "test", new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, 50_000_000), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Sales, -50_000_000));
            Assert.That(ConfirmCards.For(ViewKit2.Context(before), new BuyParcel(ViewKit2.Context(before).Now, b)).Warning, Does.Not.Contain("slowly"));
        }

        // The rent's Why? names the acre's land value instead of the three roads.
        [Test]
        public void RentPlace()
        {
            Simulation sim = Township();
            Sim.Core.People.Household x = sim.State.Citizens.Households.FirstOrDefault(h => Sim.Core.People.HouseholdRules.InFamilyHouse(sim, h));
            if (x == null) Assert.Ignore("no family house in this situation");
            string text = LeaseViews.Parts(ViewKit2.Context(sim), x);
            Assert.That(text, Does.Contain("from the land's value"));
        }
    }
}
