using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 019 §3.6, ADR 0101 §7 (US-136): the Milestones list on Your wealth, its <em>Why?</em>s and the toast.</summary>
    public class MilestoneViewTests
    {
        private static SheetVm Wealth(Simulation sim) { return MoneySheet.Build(ViewKit2.Context(sim), MoneySheet.Wealth); }

        private static LineVm[] Rows(SheetVm vm)
        {
            int head = vm.Lines.FindIndex(l => l.IsHeader && l.Label.StartsWith("Milestones"));
            Assert.That(head, Is.GreaterThanOrEqualTo(0), "a Milestones section");
            return vm.Lines.Skip(head + 1).TakeWhile(l => !l.IsHeader).Where(l => l.Icon == "star").ToArray();
        }

        // All five show; the ones not reached are dimmed with where you stand; every row has its Why? in words.
        [Test]
        public void Section()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            SheetVm vm = Wealth(sim);
            Assert.That(vm.Lines.Any(l => l.IsHeader && l.Label == "Milestones · 0 of 5"));
            LineVm[] rows = Rows(vm);
            Assert.That(rows.Select(r => r.Label), Is.EqualTo(new[] { "First lakh saved", "Twice what you inherited", "First rent", "Debt-free", "Passive income covers your living" }));
            Assert.That(rows.All(r => r.Dim && r.Value == "Not yet"), Is.True);
            Assert.That(rows[0].Note, Does.StartWith("Cash and savings, less what you owe:"));
            Assert.That(rows[1].Note, Does.Contain("twice").Or.Contain("2 times"));
            Assert.That(vm.Lines.Last().Label, Does.StartWith("Just a way to see how far"));
            ViewContext ctx = ViewKit2.Context(sim);
            foreach (LineVm r in rows)
            {
                Assert.That(WhyView.Has(r.WhyKey), Is.True, r.Label);
                WhyTests.AssertWords(r.Label, WhyView.Build(ctx, r.WhyKey, r.WhyId));
            }
        }

        // A reached milestone moves up with its season, undimmed, in the colour of good news.
        [Test]
        public void Reached()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            sim.AdvanceTo(sim.Clock.SecondsPerSeason - 1);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.rent", null, new Posting(Account.Cash, 1_000 * 100), new Posting(Account.RentReceived, -1_000 * 100));
            sim.AdvanceTo(sim.Clock.SecondsPerSeason);
            LineVm[] rows = Rows(Wealth(sim));
            Assert.That(rows[0].Label, Is.EqualTo("First rent"));
            Assert.That(rows[0].Dim, Is.False);
            Assert.That(rows[0].Value, Is.EqualTo(ReportSheet.PeriodName(ViewKit2.Context(sim), 0)));
            Assert.That(rows[0].NoteTone, Is.EqualTo(Tone.Good));
            Assert.That(rows.Skip(1).All(r => r.Dim), Is.True);
            Assert.That(Wealth(sim).Lines.Any(l => l.IsHeader && l.Label == "Milestones · 1 of 5"));
            WhyTests.AssertWords("reached", WhyView.Build(ViewKit2.Context(sim), rows[0].WhyKey, rows[0].WhyId));
        }

        // One to three name themselves; more say how many; an older town's first close says they're from past seasons.
        [Test]
        public void Toast()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame(seed: 5));
            MilestoneReached M(string id, bool past = false) { return new MilestoneReached(0, id, 0, past); }
            Assert.That(MilestoneToastVm.Build(ctx, new MilestoneReached[0]), Is.Null);
            MilestoneToastVm one = MilestoneToastVm.Build(ctx, new[] { M("firstLakh") });
            Assert.That(one.Kicker, Is.EqualTo("Milestone"));
            Assert.That(one.Text, Is.EqualTo("First lakh saved"));
            Assert.That(one.Opens.Kind, Is.EqualTo("Money"));
            Assert.That(MilestoneToastVm.Build(ctx, new[] { M("firstLakh"), M("debtFree") }).Text, Is.EqualTo("First lakh saved, Debt-free"));
            Assert.That(MilestoneToastVm.Build(ctx, new[] { M("firstLakh"), M("debtFree"), M("firstRent"), M("twiceInherited") }).Text, Is.EqualTo("4 milestones"));
            MilestoneToastVm past = MilestoneToastVm.Build(ctx, new[] { M("firstLakh", true), M("firstRent", true), M("debtFree"), M("twiceInherited", true) });
            Assert.That(past.Text, Is.EqualTo("4 milestones from your past seasons"));
        }
    }
}
