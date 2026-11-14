using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Town;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>US-139 (spec 021 §3.6, §7, ADR 0115): the Budget tab, the set-a-budget sheet, the first window's card and the bell's rows.</summary>
    public class BudgetViewTests
    {
        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name); }

        // The tab: a window strip with Accept, a row for each department with its budget, needs and last close's sentence, a Change button
        // while the window is open and a Why? on every row.
        [Test]
        public void BudgetTab()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            ulong hash = sim.State.ComputeHash();
            SheetVm vm = TreasurySheet.Build(ctx, TreasurySheet.BudgetTab);
            Assert.That(vm.Lines.Single(l => l.Tabs != null).Tabs.Select(t => t.Label), Is.EqualTo(new[] { "This season", "Budget •" }), "an unlooked window is marked");
            LineVm strip = vm.Lines.Single(l => l.Banner && l.Action != null);
            Assert.That(strip.Label, Does.Contain("open").Or.Contain("proposed"));
            Assert.That(strip.Action.Label, Is.EqualTo("Accept"));
            LineVm office = vm.Lines.Single(l => l.Label == "Council's office");
            Assert.That(office.Value, Does.StartWith("Budget ").And.Contain("needs"));
            Assert.That(office.Action.Label, Is.EqualTo("Change"));
            Assert.That(office.WhyKey, Is.EqualTo("treasuryDept"));
            Assert.That(office.Note, Is.Not.Null.And.Not.Empty, "the last close's sentence");
            Assert.That(vm.Lines.Single(l => l.Label == "Health").Note, Is.EqualTo("Nothing to pay yet"));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a view reads and never changes");

            Ok(sim, new AcceptBudget(ctx.Now));
            vm = TreasurySheet.Build(ViewKit2.Context(sim), TreasurySheet.BudgetTab);
            Assert.That(vm.Lines.Any(l => l.Banner && l.Action != null), Is.False, "accepted: nothing to accept");
            Assert.That(vm.Lines.Single(l => l.Label == "Council's office").Action, Is.Null, "locked: no Change");
            Assert.That(vm.Lines.Single(l => l.Banner && l.Note != null && l.Note.StartsWith("Locked until")).Label, Does.EndWith("locked"));
        }

        // The sheet to change one department: 0 to 120% of its needs, the needs marked, a warning under them.
        [Test]
        public void SetSheet()
        {
            Simulation sim = Situations.Township();
            ViewContext ctx = ViewKit2.Context(sim);
            long needs = TreasuryRules.Needs(sim, 0);
            SheetVm vm = TreasurySheet.BudgetSet(ctx, 0);
            AmountVm a = vm.Lines.Single(l => l.Amount != null).Amount;
            Assert.That((a.Min, a.Max, a.Mark), Is.EqualTo((0L, TreasuryRules.MaxBudget(sim, 0), needs)));
            AmountPick under = a.Pick(needs / 2);
            Assert.That(under.Tone, Is.EqualTo(Tone.Warn));
            Assert.That(under.Note, Does.Contain("first to go without"));
            Assert.That(under.Action.Create(ctx.Now), Is.TypeOf<SetBudget>());
            Assert.That(a.Pick(needs).Note, Does.StartWith("Exactly"));
            Assert.That(a.Pick(a.Max + 1).Action.Enabled, Is.False);
            Ok(sim, a.Pick(needs / 2).Action.Create(ctx.Now));
            Assert.That(sim.State.Treasury.Budget[0], Is.EqualTo(needs / 2));

            Ok(sim, new AcceptBudget(ctx.Now));
            Assert.That(TreasurySheet.BudgetSet(ViewKit2.Context(sim), 0).Lines.Single().Label, Does.StartWith("The budget is locked"));
        }

        // The first window's card (a window of the set kind, not yet looked at), and the bell's Decide row for a window that isn't the first.
        [Test]
        public void CardAndBell()
        {
            Simulation sim = Situations.Township();
            TreasuryState st = sim.State.Treasury;
            Assert.That(st.Window, Is.Not.EqualTo(BudgetWindow.None), "the Township's first close left a window open");
            ViewContext ctx = ViewKit2.Context(sim);
            AlertVm row = Alerts.Build(ctx).Single(a => a.Text.Contains("budget"));
            Assert.That((row.Level, row.Action.Opens.Value.Arg), Is.EqualTo((AlertLevel.Decision, TreasurySheet.BudgetTab)));
            Ok(sim, new AcceptBudget(ctx.Now));
            Assert.That(Alerts.Build(ViewKit2.Context(sim)).Any(a => a.Text.Contains("budget")), Is.False, "accepted: the row goes");

            st.Window = BudgetWindow.Set;
            st.WindowSeen = false;
            ConfirmCardVm card = TreasurySheet.WindowCard(ViewKit2.Context(sim));
            Assert.That(card.Title, Is.EqualTo("Set the town's budget"));
            Assert.That((card.SafeLabel, card.GoLabel, card.Reassure), Is.EqualTo(("Change", "Accept", true)));
            Assert.That(card.Rows.Any(r => r.Label == "Council's office"), Is.True);
            Assert.That(card.Rows.Last().Total, Is.True);
            st.WindowSeen = true;
            Assert.That(TreasurySheet.WindowCard(ViewKit2.Context(sim)), Is.Null, "a window already looked at has no card");
        }

        // The warning's alert: on the last day, with give, tax and revise levers; revise greyed with its reason when the window is shut.
        [Test]
        public void ShortAlert()
        {
            Simulation sim = Situations.Township();
            Assert.That(sim.Execute(new TreasuryViewTests.DrainTreasury()).Accepted, Is.True);
            Ok(sim, new AcceptBudget(sim.State.Clock.SimTime));
            long close = (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(close - 12 * 3600);
            AlertVm alert = Alerts.Build(ViewKit2.Context(sim)).Single(a => a.Level == AlertLevel.Urgent && a.Text.Contains("short at the close"));
            Assert.That(alert.Levers.Select(l => l.Label), Is.EqualTo(new[] { "Give to the town", "Change a tax", "Revise the budget" }));
            Assert.That(alert.Levers[0].Opens.Value.Kind, Is.EqualTo(TreasurySheet.GiveKind));
            Assert.That(alert.Levers[1].Enabled, Is.False);
            Assert.That(alert.Levers[2].Enabled, Is.False);
            Assert.That(alert.Levers[2].DisabledReason, Does.StartWith("Locked until the"));
        }
    }
}
