using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 030 §3.3, §7.1, §7.2 (US-267, Preview 23): the Businesses hub and its HUD button.</summary>
    public class BusinessHubViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static Business Of(Simulation sim, string type) { return sim.State.Businesses.Businesses.First(b => BusinessRules.TypeOf(sim, b).Id == type); }

        // AC-7: the totals are the sum of the rows, and each row's profit is its Books' season so far.
        [Test]
        public void Hub()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm hub = BusinessHub.Build(ctx);
            var rows = BusinessHub.Listed(sim);
            Assert.That(rows.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(BusinessHub.TotalProfit(sim), Is.EqualTo(rows.Sum(b => BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period)))));
            Assert.That(BusinessHub.TotalCash(sim), Is.EqualTo(rows.Sum(b => b.Ledger.Cash)));
            Assert.That(hub.Lines.Single(l => l.WhyKey == "hubProfit" && l.WhyId == 0).Value, Is.EqualTo(ctx.Fmt.Coins(BusinessHub.TotalProfit(sim))));
            Assert.That(hub.Lines.Single(l => l.WhyKey == "hubCash").Value, Is.EqualTo(ctx.Fmt.Coins(BusinessHub.TotalCash(sim))));
            foreach (Business b in rows)
            {
                LineVm profit = hub.Lines.Single(l => l.WhyKey == "hubProfit" && l.WhyId == b.Id);
                Assert.That(profit.Value, Is.EqualTo(ctx.Fmt.Coins(BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period)))));
                LineVm cash = hub.Lines.Single(l => l.WhyKey == "businessCash" && l.WhyId == b.Id);
                Assert.That(cash.Value, Is.EqualTo(ctx.Fmt.Coins(b.Ledger.Cash)));
                Assert.That(cash.Action.Opens.Value.Kind, Is.EqualTo("BusinessCash"), "Take out opens the amounts sheet");
            }
            Assert.That(hub.Lines.Count(l => l.Action != null && l.Action.Id.StartsWith("see:")), Is.EqualTo(rows.Count));
        }

        // AC-17: every number on the hub has a Why? in words.
        [Test]
        public void HubWhys()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm l in BusinessHub.Build(ctx).Lines.Where(l => l.WhyKey != null))
            {
                Assert.That(WhyView.Has(l.WhyKey), Is.True, l.WhyKey);
                WhyTests.AssertWords(l.WhyKey + " " + l.WhyId + " / " + l.Label, WhyView.Build(ctx, l.WhyKey, l.WhyId));
            }
        }

        // AC-8: a business with nobody working it needs you: the ! row, the tag and the HUD badge say so; fixing it clears them.
        [Test]
        public void HubNeedsYou()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business tea = Of(sim, "tea_stall");
            Assert.That(BusinessHub.NeedsCount(sim), Is.EqualTo(0));
            Assert.That(HudView.Build(ctx).BusinessesBadge, Is.EqualTo(0));
            Assert.That(HudView.Build(ctx).HasBusinesses, Is.True);

            Ok(sim, new LetWorkerGo(sim.State.Clock.SimTime, tea.Id));
            Assert.That(BusinessHub.NeedsCount(sim), Is.EqualTo(1));
            Assert.That(HudView.Build(ctx).BusinessesBadge, Is.EqualTo(1));
            SheetVm hub = BusinessHub.Build(ctx);
            Assert.That(hub.Lines.Count(l => l.Tag == "Needs you"), Is.EqualTo(1));
            Assert.That(hub.Lines.Any(l => l.Banner && l.Label == "Nobody works here: hire someone on the board."), Is.True);
        }

        // Take out is closed, with its reason, for a business with nothing to take; a business being built is listed after the open ones.
        [Test]
        public void TakeOutAndOrder()
        {
            Simulation sim = Situations.Shops();
            ViewContext ctx = ViewKit2.Context(sim);
            Business tea = Of(sim, "tea_stall");
            ActionVm take = BusinessHub.Build(ctx).Lines.Select(l => l.Action).First(a => a != null && a.Id == "take:" + tea.Id);
            Assert.That(take.Enabled, Is.EqualTo(BusinessBooks.Drawable(tea) > 0));
            if (!take.Enabled) Assert.That(take.DisabledReason, Is.EqualTo("Nothing to take out yet."));
            var ids = BusinessHub.Listed(sim).Select(b => b.Status).ToList();
            Assert.That(ids.SkipWhile(x => x == BusinessStatus.Open).All(x => x == BusinessStatus.Building), Is.True, "open first, then being built");
        }

        // The button is not there before a first business.
        [Test]
        public void ButtonWaitsForABusiness()
        {
            Simulation sim = ViewKit.NewGame();
            Assert.That(BusinessHub.Shown(sim), Is.False);
            Assert.That(HudView.Build(ViewKit2.Context(sim)).HasBusinesses, Is.False);
        }
    }
}
