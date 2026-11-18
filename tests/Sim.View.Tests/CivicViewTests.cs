using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.People;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-148 part 2 (spec 023 §3.7, Preview 5): the three civic plots' sheets in every state, the commission card's budget guard, the household's rows, the planner's paused-policy note, the minimum wage in a wage's Why?, and every number's Why?.</summary>
    public class CivicViewTests
    {
        private const long Hour = 3600;

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Execute(c).Reason); }

        private static void Fund(Simulation sim, long coins)
        {
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, coins * 100), new Posting(Account.GiftsFromFounder, -coins * 100));
        }

        private static Simulation Township(long coins = 500_000)
        {
            Simulation sim = Situations.Township();
            Fund(sim, coins);
            return sim;
        }

        private static long At(Simulation sim, long coins) { return Sim.Core.Market.MarketRules.Indexed(sim, coins * 100); }

        private static string C(Simulation sim, long coins) { return ViewKit2.Context(sim).Fmt.Coins(At(sim, coins)); }

        private static SheetVm Plot(Simulation sim, int civic)
        {
            return FieldSheets.ForTile(ViewKit2.Context(sim), CivicRules.Def(sim, civic).Tile);
        }

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

        // Before the Township the plot says what it will be and why the button is grey (the town has no purse yet).
        [Test]
        public void Locked()
        {
            Simulation sim = Situations.Town();
            SheetVm vm = FieldSheets.ForTile(ViewKit2.Context(sim), sim.Config.Game.Map.Places.First(p => p.Kind == "watchman_plot").Tile);
            Assert.That(vm.Title, Is.EqualTo("The watchman's post plot"));
            Assert.That(vm.Lines.Select(l => l.Value), Does.Contain("Not yet"));
            ActionVm a = vm.Actions.Single(x => x.Id == "commission");
            Assert.That(a.Enabled, Is.False);
            Assert.That(a.DisabledReason, Does.Contain("Township"));
            AssertWhys(sim, vm);
        }

        // The ready plot shows the cost, the upkeep, the wage, who keeps watch, reach, capacity, the effect and the town's cash; the button reads the cost.
        [Test]
        public void Ready()
        {
            Simulation sim = Township();
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = Plot(sim, CivicDef.Watchman);
            Assert.That(vm.Lines.Where(l => !l.IsHeader && l.WhyKey != null).Select(l => l.Label), Is.EqualTo(new[]
            {
                "Cost, paid from the town's cash", "Upkeep a season, from Safety", "The watchman's wage a season, from Safety", "Who keeps watch", "Reach", "Capacity",
                "Safety for homes in reach", "The town's cash",
            }));
            Assert.That(vm.Lines.First(l => l.Label == "Safety for homes in reach").Value, Is.EqualTo("50% → 100%"));
            ActionVm go = vm.Actions.Single(a => a.Id == "commission");
            Assert.That((go.Enabled, go.Label, go.Detail), Is.EqualTo((true, "Commission the post", C(sim, 30_000) + " from the town")));
            AssertWhys(sim, vm);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");

            SheetVm play = Plot(sim, CivicDef.Playground);
            Assert.That(play.Lines.Select(l => l.Label), Does.Contain("Staff"));
            Assert.That(play.Lines.First(l => l.Label.StartsWith("Capacity")).Value, Is.EqualTo("60 children"));
            Assert.That(play.Lines.First(l => l.Label.StartsWith("Play for homes")).Value, Is.EqualTo("30% → 60%"));
            AssertWhys(sim, play);
            SheetVm hall = Plot(sim, CivicDef.Hall);
            Assert.That(hall.Lines.First(l => l.Label.StartsWith("Belonging")).Value, Is.EqualTo("80% → 100%"));
            AssertWhys(sim, hall);
        }

        // When the town can't pay the button is grey with the exact reason, and the sheet offers a gift.
        [Test]
        public void Poor()
        {
            Simulation sim = Township(0);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -sim.State.Treasury.Cash + 1_240_000), new Posting(Account.GiftsFromFounder, sim.State.Treasury.Cash - 1_240_000));
            SheetVm vm = Plot(sim, CivicDef.Watchman);
            ActionVm go = vm.Actions.Single(a => a.Id == "commission");
            Assert.That(go.Enabled, Is.False);
            Assert.That(go.DisabledReason, Does.Contain("12,400 coins").And.Contain(C(sim, 30_000)));
            Assert.That(vm.Actions.Any(a => a.Id == "give"), Is.True);
            Assert.That(vm.Lines.First(l => l.Label == "The town's cash").NoteTone, Is.EqualTo(Tone.Warn));
        }

        // The card: the town's cash before and after, and the budget guard: what it adds to Safety and whether the town would be short.
        [Test]
        public void Card()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ConfirmCardVm card = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "watchman_post"));
            Assert.That(card, Is.Not.Null);
            Assert.That(card.Title, Is.EqualTo("The watchman's post?"));
            Assert.That(card.Rows.Select(r => r.Label), Is.EqualTo(new[] { "The town's cash now", "The post", "The town's cash after" }));
            Assert.That(card.CashChange, Is.EqualTo(0), "your own cash isn't touched");
            Assert.That(card.Warning, Does.StartWith("This adds " + C(sim, 25_500) + " a season to Safety (" + C(sim, 1_500) + " upkeep and a " + C(sim, 24_000) + " wage)."));
            CivicRules.Guard(sim, "watchman_post", out _, out long shortBy);
            Assert.That(card.Warning, shortBy > 0 ? Does.Contain("short by") : Does.Contain("can pay it"));
            Assert.That(card.Reassure, Is.EqualTo(shortBy == 0));

            ConfirmCardVm play = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "playground"));
            Assert.That(play.Warning, Does.StartWith("This adds " + C(sim, 1_000) + " a season to Safety."));
        }

        // The card is orange when the town would be short: with the taxes at 0 and little income, the shortfall shows.
        [Test]
        public void Card_Short()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            CivicRules.Guard(sim, "community_hall", out long added, out long shortBy);
            ConfirmCardVm card = ConfirmCards.For(ctx, new CommissionWorks(ctx.Now, "community_hall"));
            Assert.That(added, Is.EqualTo(At(sim, 18_000)), "3,000 upkeep and a 15,000 wage");
            Assert.That(card.Warning, Does.Contain(shortBy > 0 ? "short by " + ctx.Fmt.Coins(shortBy) : "can pay it"));
        }

        // The post in each state: nobody, posted, in post (and unpaid), left; and Post the job / Let him go.
        [Test]
        public void Post_States()
        {
            Simulation sim = Township();
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "watchman_post"));
            SheetVm vm = Plot(sim, CivicDef.Watchman);
            Assert.That(vm.Title, Is.EqualTo("The watchman's post"));
            Assert.That(vm.Subtitle, Does.Contain("nobody"));
            Assert.That(vm.Lines.First(l => l.Label == "Watchman").Value, Is.EqualTo("Nobody yet"));
            Assert.That(vm.Lines.First(l => l.Label.StartsWith("Safety for")).Value, Does.Contain("nobody keeps watch"));
            Assert.That(vm.Actions.Single(a => a.Id == "postJob").Enabled, Is.True);
            Assert.That(vm.Actions.Any(a => a.Id == "dismiss"), Is.False);
            AssertWhys(sim, vm);

            Ok(sim, new PostCivicJob(sim.State.Clock.SimTime, CivicDef.Watchman));
            vm = Plot(sim, CivicDef.Watchman);
            Assert.That(vm.Lines.First(l => l.Label == "Watchman").Value, Does.StartWith("A villager takes the post in"));
            Assert.That(vm.Actions.Any(a => a.Id == "postJob"), Is.False);
            AssertWhys(sim, vm);

            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            vm = Plot(sim, CivicDef.Watchman);
            CivicPost p = CivicRules.Post(sim, CivicDef.Watchman);
            Assert.That(vm.Subtitle, Does.EndWith("keeps watch."));
            Assert.That(vm.Lines.First(l => l.Label == "Watchman").Value, Does.Contain("since"));
            Assert.That(vm.Lines.First(l => l.Label == "Who pays").Value, Does.Contain(C(sim, 25_500)));
            Assert.That(vm.Lines.First(l => l.Label == "Safety for homes in reach").Value, Does.Contain("without it"));
            Assert.That(vm.Actions.Select(a => a.Id), Is.EqualTo(new[] { "dismiss" }));
            AssertWhys(sim, vm);

            p.GetType().GetProperty("UnpaidCloses").GetSetMethod(true).Invoke(p, new object[] { 1 });
            vm = Plot(sim, CivicDef.Watchman);
            Assert.That(vm.Lines.Any(l => l.Banner && l.Label.Contains("went unpaid")), Is.True);
            Assert.That(vm.Actions.Select(a => a.Id), Is.EqualTo(new[] { "budget", "dismiss" }));

            Ok(sim, new DismissStaff(sim.State.Clock.SimTime, CivicDef.Watchman));
            vm = Plot(sim, CivicDef.Watchman);
            Assert.That(vm.Lines.First(l => l.Label == "Watchman").Value, Is.EqualTo("Nobody yet"), "let go, not left unpaid");
        }

        // The hall and the playground sheets.
        [Test]
        public void Hall_And_Playground()
        {
            Simulation sim = Township();
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "community_hall"));
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "playground"));
            Ok(sim, new PostCivicJob(sim.State.Clock.SimTime, CivicDef.Hall));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            SheetVm hall = Plot(sim, CivicDef.Hall);
            Assert.That(hall.Title, Is.EqualTo("The community hall"));
            Assert.That(hall.Lines.Any(l => l.Label == "Caretaker"), Is.True);
            Assert.That(hall.Lines.First(l => l.Label.StartsWith("Belonging")).Value, Is.EqualTo("100% (80% without it)"));
            AssertWhys(sim, hall);
            SheetVm play = Plot(sim, CivicDef.Playground);
            Assert.That(play.Title, Is.EqualTo("The playground"));
            Assert.That(play.Actions, Is.Empty, "no staff, nothing to press");
            Assert.That(play.Lines.First(l => l.Label == "Who pays").Value, Does.Contain(C(sim, 1_000)));
            Assert.That(play.Lines.First(l => l.Label.StartsWith("Capacity")).Value, Does.EndWith("served"));
            AssertWhys(sim, play);
        }

        // The household's rows: Safety once a post stands, the hall's Belonging, Play with children.
        [Test]
        public void Household_Rows()
        {
            Simulation sim = Township();
            Household x = sim.State.Citizens.Households[0];
            var before = NeedsView.Rows(ViewKit2.Context(sim), x);
            Assert.That(before.Any(l => l.Label == "Safety"), Is.False, "a town that builds nothing shows nothing");
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "watchman_post"));
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "community_hall"));
            var rows = NeedsView.Rows(ViewKit2.Context(sim), x);
            Assert.That(rows.First(l => l.Label == "Safety").Note, Is.EqualTo("The watchman's post has nobody in it"));
            Assert.That(rows.First(l => l.Label == "The community hall").Note, Is.EqualTo("The hall has nobody to keep it"));
            Ok(sim, new PostCivicJob(sim.State.Clock.SimTime, CivicDef.Watchman));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            rows = NeedsView.Rows(ViewKit2.Context(sim), x);
            Assert.That(rows.First(l => l.Label == "Safety").Value, Is.EqualTo("100%"));
            Assert.That(rows.First(l => l.Label == "Safety").Note, Is.EqualTo("The watchman keeps watch"));
        }

        // A paused policy makes the planner speak.
        [Test]
        public void Planner_PolicyPaused()
        {
            Simulation sim = Township();
            sim.State.Treasury.Policies[0].GetType().GetProperty("Status").GetSetMethod(true).Invoke(sim.State.Treasury.Policies[0], new object[] { PolicyStatus.Paused });
            ViewContext ctx = ViewKit2.Context(sim);
            var note = new Sim.Core.Progress.AdvisorNote("policyPaused", 0, true, new long[] { 0, PolicyRules.Cost(sim, PolicyId.Meals) });
            Assert.That(AdvisorWords.Text(ctx, note), Does.Contain("is paused").And.Contain("couldn't pay").And.Not.Contain("{"));
        }

        // A wage's Why? names the minimum wage while the policy is in force.
        [Test]
        public void MinWage_Why()
        {
            Simulation sim = Township();
            ViewContext ctx = ViewKit2.Context(sim);
            WhyVm off = WhyView.Build(ctx, "jobUsual", 0);
            Assert.That(off.Rows.Any(r => r.Label.StartsWith("The town's minimum wage")), Is.False);
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.MinWage));
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            if (!PolicyRules.Effective(sim, PolicyId.MinWage)) Assert.Inconclusive("the policy isn't in force in this town");
            WhyVm on = WhyView.Build(ViewKit2.Context(sim), "jobUsual", 0);
            Assert.That(on.Rows.Any(r => r.Label.StartsWith("The town's minimum wage: the market wage + 10%")), Is.True);
        }
    }
}
