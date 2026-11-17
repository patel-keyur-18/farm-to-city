using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-141 (spec 022 §7): the Policies tab, its rows, and the enact and repeal cards.</summary>
    public class PolicyViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name); }

        // The tab: the town's money line, a row for each of the five policies with its cost, where it stands, who it helps and who pays, and Enact.
        [Test]
        public void Sheet()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = PolicySheet.Build(ctx);
            Assert.That(vm.Title, Is.EqualTo("Taxes and policies"));
            Assert.That(vm.Tabs.Select(t => t.Label), Is.EqualTo(new[] { "Taxes", "Policies · 0 of 3" }));
            Assert.That(vm.Tab, Is.EqualTo("tab:policies"));
            LineVm[] rows = vm.Lines.Where(l => l.WhyKey == "policy").ToArray();
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "Free school meals", "Farm input subsidy", "Small business grant", "Minimum wage", "Rent control" }));
            LineVm subsidy = rows[1];
            Assert.That(subsidy.Action.Label, Is.EqualTo("Enact"));
            Assert.That(subsidy.Note, Does.Contain("Helps:").And.Contain("Costs:"));
            Assert.That(subsidy.Value, Does.EndWith("a season"));
            Assert.That(rows[0].Action.Enabled, Is.False, "no school, no meals");
            Assert.That(rows[0].Action.DisabledReason, Is.EqualTo("Free school meals need the school."));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");
        }

        // Enacted and repealed: the row says where it stands, the tab counts the slots, and at most three can be held.
        [Test]
        public void States()
        {
            Simulation sim = Situations.Township();
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Grant));
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.MinWage));
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = PolicySheet.Build(ctx);
            Assert.That(vm.Tabs[1].Label, Is.EqualTo("Policies · 3 of 3"));
            LineVm[] rows = vm.Lines.Where(l => l.WhyKey == "policy").ToArray();
            Assert.That(rows[1].Note, Does.StartWith("Paid at the next close; works from "));
            Assert.That(rows[1].Action.Label, Is.EqualTo("Repeal"));
            Assert.That(rows[4].Action.Enabled, Is.False);
            Assert.That(rows[4].Action.DisabledReason, Does.Contain("Repeal one first"));
        }

        // AC-10: enacting is a tap card with the cost, when it is paid, when it works and the town's bill before and after; repealing is held.
        [Test]
        public void Cards()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm enact = ConfirmCards.For(ctx, new EnactPolicy(ctx.Now, PolicyId.Subsidy));
            Assert.That(enact, Is.Not.Null);
            Assert.That(enact.Hold, Is.False);
            Assert.That(enact.Title, Is.EqualTo("Enact this policy?"));
            Assert.That(enact.Subtitle, Is.EqualTo("Farm input subsidy"));
            Assert.That(enact.Rows.Select(r => r.Label), Is.EqualTo(new[] { "Costs the town", "First paid at the close of", "Works from", "The town's bill at the close", "The close" }));
            Assert.That(enact.GoLabel, Is.EqualTo("Enact"));
            Ok(sim, new EnactPolicy(ctx.Now, PolicyId.Subsidy));
            Assert.That(ConfirmCards.For(ViewKit2.Context(sim), new RepealPolicy(ctx.Now, PolicyId.Subsidy)), Is.Null, "never paid: taking it back is free and at once");
            sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            ConfirmCardVm repeal = ConfirmCards.For(ViewKit2.Context(sim), new RepealPolicy(ctx.Now, PolicyId.Subsidy));
            Assert.That(repeal, Is.Not.Null);
            Assert.That(repeal.Hold, Is.True);
            Assert.That(repeal.SafeLabel, Is.EqualTo("Keep it"));
            Assert.That(repeal.Rows.Last().Value, Does.Contain("dip"));
            Ok(sim, new RepealPolicy(ctx.Now, PolicyId.Subsidy));
            LineVm row = PolicySheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Farm input subsidy");
            Assert.That(row.Action.Label, Is.EqualTo("Keep it"), "changing your mind while it is ending");
            Assert.That(ConfirmCards.For(ViewKit2.Context(sim), new EnactPolicy(ctx.Now, PolicyId.Subsidy)), Is.Null, "keeping it asks nothing");
        }

        // While you are away the close pays, starts and pauses policies: the away card says so in words.
        [Test]
        public void Away()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            long t = sim.State.Clock.SimTime;
            var rows = TownAway.Rows(ctx, new SimEvent[] { new PolicyStarted(t, 1), new PolicyPaused(t, 2), new GrantWasted(t), new GrantOpenedShop(t) });
            Assert.That(rows.Select(r => r.Value), Is.EqualTo(new[]
            {
                "Farm input subsidy has started to work.",
                "The town couldn't pay for Small business grant: it is paused this season.",
                "A small business grant came to nothing: the money was spent and no shop opened.",
                "A small business grant helped a new shop open.",
            }));
        }

        // The first-visit note shows once.
        [Test]
        public void Tour()
        {
            Simulation sim = Situations.Township();
            Assert.That(PolicySheet.Build(ViewKit2.Context(sim)).Lines.Any(l => l.Banner && l.Action != null), Is.True);
            Ok(sim, new SeePolicyTour(sim.State.Clock.SimTime));
            Assert.That(PolicySheet.Build(ViewKit2.Context(sim)).Lines.Any(l => l.Banner && l.Action != null), Is.False);
        }
    }
}
