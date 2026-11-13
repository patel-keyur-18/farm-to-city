using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Confirm cards (spec 008 §3.15, AC-22, ADR 0101 §12): every command sorted, cards that show the sim's own numbers.</summary>
    public class ConfirmTests
    {
        private static Simulation Rich() { return ViewKit.NewGame(edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j); }

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        // Every concrete command in the sim is sorted everyday, tap, hold or system: a new command can't be added without choosing how it asks.
        [Test]
        public void EveryCommand()
        {
            Type[] all = typeof(Command).Assembly.GetTypes().Where(t => typeof(Command).IsAssignableFrom(t) && !t.IsAbstract).ToArray();
            Assert.That(all.Length, Is.GreaterThan(80));
            string[] unsorted = all.Where(t => !ConfirmRules.TryKind(t, out _)).Select(t => t.Name).ToArray();
            Assert.That(unsorted, Is.Empty, "sort these in ConfirmRules (spec 008 §3.15)");
            Assert.That(ConfirmRules.TryKind(typeof(SellParcel), out ConfirmKind sell) && sell == ConfirmKind.Hold, Is.True);
            Assert.That(ConfirmRules.TryKind(typeof(BuyParcel), out ConfirmKind buy) && buy == ConfirmKind.Tap, Is.True);
            Assert.That(ConfirmRules.TryKind(typeof(Sow), out ConfirmKind sow) && sow == ConfirmKind.Everyday, Is.True);
            Assert.That(ConfirmRules.TryKind(typeof(SellToTrader), out ConfirmKind trade) && trade == ConfirmKind.Everyday, Is.True);
        }

        // Every command that asks has its words: a title, a verb, and a card that builds.
        [Test]
        public void EveryAskingCommand_HasWords()
        {
            var s = ViewKit.Strings();
            foreach (Type t in ConfirmRules.Sorted.Where(x => ConfirmRules.TryKind(x, out ConfirmKind k) && (k == ConfirmKind.Tap || k == ConfirmKind.Hold)))
            {
                Assert.That(s.Has("confirm." + t.Name + ".title"), Is.True, t.Name + " title");
                Assert.That(s.Has("confirm." + t.Name + ".verb"), Is.True, t.Name + " verb");
            }
        }

        // Sprint 18 review: Always pay from my savings is the standing consent, so it asks on card B; Ask me doesn't.
        [Test]
        public void Tap_AlwaysPayFromSavings()
        {
            Simulation sim = Rich();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(sim.Execute(new DepositSavings(T(sim), 50_000_00)).Accepted, Is.True);
            ConfirmCardVm card = ConfirmCards.For(ctx, new SetSavingsCover(T(sim), true));
            Assert.That(card.Hold, Is.False);
            Assert.That(card.Title, Is.EqualTo("Always pay from your savings?"));
            Assert.That(card.GoLabel, Is.EqualTo("Always pay from savings"));
            Assert.That(card.Rows.Select(r => (r.Label, r.Value)), Is.EqualTo(new[] { ("Your savings now", ctx.Fmt.Coins(50_000_00)), ("The next close would take from them", ctx.Fmt.Coins(0)) }));
            Assert.That(card.Warning, Does.Contain("without asking"));
            Assert.That(ConfirmCards.For(ctx, new SetSavingsCover(T(sim), false)), Is.Null, "back to Ask me asks nothing");
        }

        // Card B: buying land shows its cost and the cash after, as the sim has it; an everyday action has no card.
        [Test]
        public void Tap_BuyLand()
        {
            Simulation sim = Rich();
            ViewContext ctx = ViewKit2.Context(sim);
            long cash = sim.State.Ledger.Cash;
            ConfirmCardVm card = ConfirmCards.For(ctx, new BuyParcel(T(sim), "w1"));
            Assert.That(card.Hold, Is.False);
            Assert.That(card.Title, Is.EqualTo("Buy this land?"));
            Assert.That(card.SafeLabel, Is.EqualTo("Not now"));
            Assert.That(sim.Execute(new BuyParcel(T(sim), "w1")).Accepted, Is.True);
            long paid = cash - sim.State.Ledger.Cash;
            Assert.That(card.CashChange, Is.EqualTo(-paid));
            Assert.That(card.Rows.First().Value, Is.EqualTo(ctx.Fmt.Coins(paid)), "the cost");
            Assert.That(card.Rows.Last().Value, Is.EqualTo(ctx.Fmt.Coins(cash - paid)), "cash after");
            Assert.That(card.GoLabel, Is.EqualTo("Buy for " + ctx.Fmt.Coins(paid)));
            Assert.That(card.Warning, Does.Contain("80%"));
            Assert.That(ConfirmCards.For(ctx, new Harvest(T(sim), 0)), Is.Null, "everyday: one tap");
        }

        // Card C: selling land is a hold; what it shows is what SellParcel posts: the quick sale, the cash received.
        [Test]
        public void Hold_SellLand()
        {
            Simulation sim = Rich();
            Assert.That(sim.Execute(new BuyParcel(T(sim), "w1")).Accepted, Is.True);
            ViewContext ctx = ViewKit2.Context(sim);
            long cash = sim.State.Ledger.Cash;
            ConfirmCardVm card = ConfirmCards.For(ctx, new SellParcel(T(sim), "w1"));
            Assert.That(card.Hold, Is.True);
            Assert.That(card.HoldMillis, Is.EqualTo(1000));
            Assert.That(card.GoLabel, Is.EqualTo("Hold to sell"));
            Assert.That(card.SafeLabel, Is.EqualTo("Keep my land"));
            Assert.That(card.Warning, Does.StartWith("This can't be undone"));
            Assert.That(sim.Execute(new SellParcel(T(sim), "w1")).Accepted, Is.True);
            long received = sim.State.Ledger.Cash - cash;
            Assert.That(card.Rows.Last().Value, Is.EqualTo(ctx.Fmt.Coins(received)), "to your cash");
            Assert.That(card.Rows.Select(r => r.Label), Does.Contain("A quick sale gets 80%"));
        }

        // A command the sim would refuse gets no card: the refusal shows as it does today.
        [Test]
        public void Refused_NoCard()
        {
            Simulation sim = Rich();
            Assert.That(ConfirmCards.For(ViewKit2.Context(sim), new SellParcel(T(sim), "w1")), Is.Null, "not owned");
        }

        // Card C for letting a hand go: what you get back, and the hold; the sim's own refund.
        [Test]
        public void Hold_LetGo()
        {
            Simulation sim = Situations.Growing();
            Sim.Core.People.Hand hand = sim.State.People.Hands.Single();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new DismissHand(T(sim), hand.Id));
            Assert.That(card, Is.Not.Null);
            Assert.That((card.Hold, card.SafeLabel), Is.EqualTo((true, "Keep them")));
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new DismissHand(T(sim), hand.Id)).Accepted, Is.True);
            Assert.That(card.CashChange, Is.EqualTo(sim.State.Ledger.Cash - cash), "what you get back");
            Assert.That(card.Rows.Last().Value, Is.EqualTo(ctx.Fmt.Coins(sim.State.Ledger.Cash - cash)));
        }

        // ADR 0101 §13: Menu → Your land lists what you own (inherited land not sellable) and what's for sale; Sell is only there.
        [Test]
        public void YourLand()
        {
            Simulation sim = Rich();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm none = LandsSheet.Build(ctx);
            Assert.That(none.Title, Is.EqualTo("Your land"));
            Assert.That(none.Lines.Any(l => l.Action != null && l.Action.Id.StartsWith("sellParcel")), Is.False, "nothing bought: nothing to sell");
            Assert.That(none.Lines.Select(l => l.Label), Does.Contain("Can never be sold or seized"));
            Assert.That(sim.Execute(new BuyParcel(T(sim), "w1")).Accepted, Is.True);
            ctx = ViewKit2.Context(sim);
            SheetVm yours = LandsSheet.Build(ctx);
            LineVm row = yours.Lines.Single(l => l.Action != null && l.Action.Id == "sellParcel:w1");
            Assert.That(row.Action.Enabled, Is.True);
            Assert.That(row.Also.Id, Does.StartWith("show:"));
            Assert.That(ConfirmCards.For(ctx, row.Action.Create(T(sim))).Hold, Is.True, "Sell asks with a hold card");
            Assert.That(LandsSheet.Build(ctx, LandsSheet.ForSale).Lines.Any(l => l.Action.Id.StartsWith("show:")), Is.True);
            Assert.That(FieldSheets.ForTile(ctx, 82).Actions.Any(a => a.Id.StartsWith("sellParcel")), Is.False, "the field's own sheet has no Sell");
            // Something on the land stops the sale: Sell is greyed with the sim's own reason.
            Assert.That(sim.Execute(new Sow(T(sim), 82, "radish", Sim.Core.Farm.SeedSource.Certified, "none")).Accepted, Is.True);
            LineVm busy = LandsSheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Action != null && l.Action.Id == "sellParcel:w1");
            Assert.That(busy.Action.Enabled, Is.False);
            Assert.That(busy.Action.DisabledReason, Is.EqualTo("Clear the crops, trees and buildings off this land first."));
        }
    }
}
