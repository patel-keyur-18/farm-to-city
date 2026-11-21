using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-156 (spec 027 §7, Preview 13): the Store's trader section, the confirm card, the alert, the dues and the away lines.</summary>
    public class ContractViewTests
    {
        private static LineVm Row(SheetVm vm, string label, string header)
        {
            int at = vm.Lines.FindIndex(l => l.IsHeader && l.Label == header);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), header);
            for (int i = at + 1; i < vm.Lines.Count && !vm.Lines[i].IsHeader; i++) if (vm.Lines[i].Label == label) return vm.Lines[i];
            Assert.Fail("no " + label + " under " + header);
            return null;
        }

        // Offers with Accept; the card says it is binding; accepting moves the offer to Your contracts.
        [Test]
        public void Store_Offers_Accept()
        {
            Simulation sim = Situations.Contracted();
            var ctx = ViewKit2.Context(sim);
            SheetVm vm = StoreSheet.Build(ctx);
            LineVm wheat = Row(vm, "Wheat", "Trader's offers");
            Assert.That(wheat.Value, Does.Contain("400 kg"));
            Assert.That(wheat.Note, Does.Contain("Expected").And.Contain("delivered in"));
            Assert.That(wheat.WhyKey, Is.EqualTo(ContractSheet.OfferWhy));
            ConfirmCardVm card = ConfirmCards.For(ctx, wheat.Action.Create(ctx.Now));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Warning, Does.Contain("binding"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Quantity", "The price expected then", "Your fixed price", "Delivered in" }));
            Assert.That(card.CashChange, Is.EqualTo(0), "nothing is paid now");
            Assert.That(sim.Execute(wheat.Action.Create(ctx.Now)).Accepted, Is.True);
            SheetVm after = StoreSheet.Build(ViewKit2.Context(sim));
            Assert.That(after.Lines.Any(l => l.IsHeader && l.Label == "Trader's offers") && Row(after, "Maize", "Trader's offers") != null, Is.True);
            Assert.That(Row(after, "Wheat", "Your contracts").Note, Does.Contain("starts in"));
        }

        // A running contract: what is delivered, what is left, the days, against the market; every row has a working ?.
        [Test]
        public void Store_Contracts()
        {
            Simulation sim = Situations.Contracted();
            var ctx = ViewKit2.Context(sim);
            SheetVm vm = StoreSheet.Build(ctx);
            LineVm rice = Row(vm, "Rice", "Your contracts");
            Assert.That(rice.Value, Is.EqualTo("300 kg of 800 kg delivered"));
            Assert.That(rice.Note, Does.Contain("500 kg to go").And.Contain("left").And.Contain("against the market you lost"));
            Assert.That(rice.NoteTone, Is.EqualTo(Tone.Warn));
            LineVm done = Row(vm, "Mustard", "Last settled");
            Assert.That(done.Note, Does.Contain("Short: you paid"));
            foreach (LineVm l in vm.Lines.Where(l => l.WhyKey != null && l.WhyKey.StartsWith("con")))
                Assert.That(WhyView.Build(ctx, l.WhyKey, l.WhyId).Rows.Count, Is.GreaterThan(2), l.Label);
        }

        // The Sell button shows what the trader will really pay: the contract's price on its first kilos, and the row says so.
        [Test]
        public void Sell_Shows_Contract_Price()
        {
            Simulation sim = Situations.Contracted();
            int rice = sim.Config.Game.Market.IndexOf("rice");
            ((long[])typeof(Sim.Core.Market.MarketState).GetField("Stock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(sim.State.Market))[rice] += 300_000;
            var ctx = ViewKit2.Context(sim);
            Sim.Core.Town.Contract c = ContractRules.Running(sim, rice);
            Assert.That(c, Is.Not.Null);
            LineVm row = StoreSheet.Build(ctx).Lines.First(l => !l.IsHeader && l.Label == "Rice" && l.Action != null && l.Action.Id.StartsWith("sell"));
            long room = Sim.Core.Market.MarketRules.TraderCapacityLeft(sim, rice), sold = System.Math.Min(300_000, room);
            if (sold > 0) Assert.That(row.Action.Detail, Does.Contain(ctx.Fmt.Coins(ContractRules.SaleValue(sim, rice, sold, Sim.Core.Market.MarketRules.Floor(sim, rice)))));
            Assert.That(row.Note, Does.Contain("Your contract: the first 500 kg are paid"));
        }

        // An empty Township still says how the offers come, so the section is never a mystery.
        [Test]
        public void Store_NoOffers_Hint()
        {
            Simulation sim = Situations.TownshipBuilt();
            SheetVm vm = StoreSheet.Build(ViewKit2.Context(sim));
            int at = vm.Lines.FindIndex(l => l.IsHeader && l.Label == "Trader's offers");
            Assert.That(vm.Lines[at + 1].Label, Does.Contain("At each close the trader offers"));
        }

        // The bell says the trader has offers while one is unanswered.
        [Test]
        public void Alert_Offers()
        {
            Simulation sim = Situations.Contracted();
            var list = Alerts.Build(ViewKit2.Context(sim));
            Assert.That(list.Any(a => a.Text.Contains("fixed price for your wheat, maize")), Is.True);
            ContractRules.State(sim).Offers.Clear();
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).Any(a => a.Text.Contains("fixed price for your")), Is.False);
        }

        // The close's dues show the running contract's shortfall before it is taken.
        [Test]
        public void Dues_Shortfall()
        {
            Simulation sim = Situations.Contracted();
            var ctx = ViewKit2.Context(sim);
            SeasonVm season = SeasonBook.Build(ctx, false);
            Assert.That(season.Due.Any(l => l.Key == "contract" && l.Amount == ContractRules.Due(sim) && l.Amount > 0), Is.True);
        }

        // The away card says the trader's offer and a settled contract.
        [Test]
        public void Away_Lines()
        {
            Simulation sim = Situations.Contracted();
            var ctx = ViewKit2.Context(sim);
            var events = new SimEvent[]
            {
                new ContractOffered(sim.State.Clock.SimTime, 5, "wheat", 400_000, Sim.Core.Maths.Fixed.FromInt(24), Sim.Core.Maths.Fixed.FromInt(25), sim.Period + 1),
                new ContractSettled(sim.State.Clock.SimTime, 3, "rice", 800_000, 300_000, 600_00, -600_00),
                new ContractSettled(sim.State.Clock.SimTime, 4, "wheat", 400_000, 400_000, 0, 1_000_00),
            };
            var rows = TownAway.Rows(ctx, events);
            string all = string.Join("|", rows.Select(r => (r.Label ?? "") + (r.Value ?? "") + (r.Note ?? "")));
            Assert.That(all, Does.Contain("fixed price for 400 kg of your wheat"));
            Assert.That(all, Does.Contain("you paid").And.Contain("nothing to pay"));
        }
    }
}
