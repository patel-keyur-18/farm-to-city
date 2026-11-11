using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 019 §3.5, ADR 0101 §2 (US-236): the savings sheet, the alert on a short close and its levers, the cover row on *Next season, already due*.</summary>
    public class SavingsViewTests
    {
        private const long Hour = 3600, Coin = 100;

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        private static void Run(Simulation sim, ActionVm a) { Assert.That(a.Enabled, Is.True, a.DisabledReason); Assert.That(sim.Execute(a.Create(T(sim))).Accepted, Is.True, a.Label); }

        private static ActionVm Find(IEnumerable<ActionVm> actions, string label) { return actions.Single(a => a.Label == label); }

        /// <summary>A town an hour into the season's last day with no cash and <paramref name="deposit"/> coins in the bank: the close will be short.</summary>
        private static Simulation Short(long deposit = 200_000)
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long cash = sim.State.Ledger.Cash;
            sim.State.Ledger.Post(T(sim), sim.Period, "test", "test", new Posting(Account.Cash, -cash), new Posting(Account.OpeningEquity, cash));
            sim.State.Ledger.Post(T(sim), sim.Period, "test", "test", new Posting(Account.BankDeposit, deposit * Coin), new Posting(Account.OpeningEquity, -deposit * Coin));
            sim.AdvanceTo(sim.Clock.SecondsPerSeason - 23 * Hour);
            return sim;
        }

        private static List<AlertVm> Alerts(Simulation sim) { return Views.Alerts.Build(ViewKit2.Context(sim)); }

        // The sheet: put in and take out in the steps business cash uses, one tap each; the standing choice.
        [Test]
        public void Sheet()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long topUp = 20_000 * Coin - sim.State.Ledger.Cash;
            sim.State.Ledger.Post(T(sim), sim.Period, "test", "test", new Posting(Account.Cash, topUp), new Posting(Account.OpeningEquity, -topUp)); // 20,000 coins in hand
            ViewContext ctx = ViewKit2.Context(sim);
            SheetVm vm = SavingsSheet.Build(ctx);
            Assert.That(vm.Title, Is.EqualTo("Savings deposit"));
            List<ActionVm> actions = vm.Lines.Where(l => l.Action != null).Select(l => l.Action).ToList();
            Assert.That(actions.Select(a => a.Id), Is.EquivalentTo(new[] { "in:500000", "in:1000000", "standing:always" }), "nothing to take out yet; 25,000 is more than the cash");
            Run(sim, actions.First(a => a.Id == "in:1000000"));
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(10_000 * Coin));

            vm = SavingsSheet.Build(ViewKit2.Context(sim));
            Assert.That(vm.Lines.Any(l => l.Label == "All 10,000" || l.Label.StartsWith("All ")), Is.True, "take it all out");
            actions = vm.Lines.Where(l => l.Action != null).Select(l => l.Action).ToList();
            Run(sim, actions.First(a => a.Id == "out:500000"));
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(5_000 * Coin));

            Run(sim, actions.First(a => a.Id == "standing:always"));
            Assert.That(SavingsRules.Always(sim), Is.True);
            vm = SavingsSheet.Build(ViewKit2.Context(sim));
            Assert.That(vm.Lines.Single(l => l.Label == "Always pay from my savings").Badge, Is.EqualTo("Chosen"));
            Assert.That(vm.Lines.Single(l => l.Label == "Ask me").Action.Id, Is.EqualTo("standing:ask"));
            Assert.That(vm.Lines.All(l => l.WhyKey == null || WhyView.Has(l.WhyKey)), Is.True);
        }

        // Your wealth shows the deposit as a picture button beside Borrow and Your books.
        [Test]
        public void WealthButton()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            SheetVm vm = MoneySheet.Build(ViewKit2.Context(sim), MoneySheet.Wealth);
            List<ActionVm> buttons = vm.Lines.Where(l => l.Buttons != null).SelectMany(l => l.Buttons).ToList();
            Assert.That(buttons.Select(b => b.Label), Is.EqualTo(new[] { "Borrow", "Savings deposit", "Your books" }));
            ActionVm savings = Find(buttons, "Savings deposit");
            Assert.That(savings.Opens.Value.Kind, Is.EqualTo(SavingsSheet.Kind));
            Assert.That(savings.Icon, Is.EqualTo("savings"));
        }

        // The alert, from the season's last day: what's short and the levers; a yes turns it into a note with a way back; the standing choice needs no alert.
        [Test]
        public void AlertLevers()
        {
            Simulation sim = Short();
            AlertVm alert = Alerts(sim).Single(a => a.Text.StartsWith("Short at the close by"));
            Assert.That(alert.Level, Is.EqualTo(AlertLevel.Urgent));
            Assert.That(alert.Levers.Select(l => l.Label), Is.EqualTo(new[] { "Pay from my savings", "Sell from the store", "People", "Borrow" }));
            Assert.That(alert.Levers.All(l => l.Icon != null), Is.True);

            long cash = sim.State.Ledger.Cash, deposit = SavingsRules.Balance(sim);
            Run(sim, Find(alert.Levers, "Pay from my savings"));
            Assert.That(SavingsRules.Consented(sim), Is.True);
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(deposit));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash), "nothing moves before the close");
            AlertVm yes = Alerts(sim).Single(a => a.Text.StartsWith("Your savings will cover"));
            Assert.That(yes.Level, Is.EqualTo(AlertLevel.Info));
            Assert.That(Alerts(sim).Any(a => a.Text.StartsWith("Short at the close")), Is.False, "one alert for one close");

            Run(sim, Find(yes.Levers, "Take it back"));
            Assert.That(SavingsRules.Consented(sim), Is.False);
            Assert.That(Alerts(sim).Any(a => a.Text.StartsWith("Short at the close by")), Is.True, "short again");

            Assert.That(sim.Execute(new SetSavingsCover(T(sim), true)).Accepted, Is.True);
            Assert.That(Alerts(sim).Any(a => a.Text.StartsWith("Short at the close") || a.Text.StartsWith("Your savings will")), Is.False, "the standing choice covers it: nothing to warn of");
        }

        // Savings that can't cover it all: the alert says what they'll pay and what is still short, before and after the yes.
        [Test]
        public void AlertPartial()
        {
            Simulation sim = Short(deposit: 100);
            AlertVm alert = Alerts(sim).Single(a => a.Level == AlertLevel.Urgent);
            ActionVm cover = Find(alert.Levers, "Pay from my savings");
            Assert.That(cover.Detail, Is.EqualTo("all 100 coins"));
            Run(sim, cover);
            alert = Alerts(sim).Single(a => a.Level == AlertLevel.Urgent);
            Assert.That(alert.Text, Does.StartWith("Your savings will pay 100 coins at the close, and you're still short by"));
            Assert.That(alert.Levers.Select(l => l.Label), Does.Contain("Take it back"));
        }

        // The phone's notification names no money, only what to do.
        [Test]
        public void NotificationText()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame(seed: 5));
            Sim.Core.Saves.PlannedNotification n = Sim.Core.Saves.NotificationPlanner.Group(new[] { new Sim.Core.Saves.AbsenceItem(1_800_000_000, Sim.Core.Saves.NotifyKind.CloseShort, null) },
                new Sim.Core.Definitions.NotifyDef(64, 30, 240, 22 * 60, 7 * 60), 0)[0];
            (string title, string body) = Views.NotificationText.For(ctx, n);
            Assert.That(title, Is.EqualTo("The season's close is short of cash"));
            Assert.That(body, Does.Contain("savings"));
        }

        // Nothing the savings do is an unnamed "other": put in, interest and what they paid at the close each have their own line in *why your cash moved*.
        [Test]
        public void MovedLines()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            Assert.That(sim.Execute(new DepositSavings(T(sim), 1_000 * Coin)).Accepted, Is.True);
            SeasonVm season = SeasonBook.Build(ViewKit2.Context(sim), false);
            Assert.That(season.Moved.Single(l => l.Key == "savedIn").Amount, Is.EqualTo(-1_000 * Coin), "cash that went into the bank");
            Assert.That(season.Moved.Any(l => l.Key == "other"), Is.False);

            Simulation covered = Short();
            Assert.That(covered.Execute(new CoverFromSavings(T(covered))).Accepted, Is.True);
            covered.AdvanceTo(covered.Clock.SecondsPerSeason + Hour);
            season = SeasonBook.Build(ViewKit2.Context(covered), false);
            Assert.That(season.Moved.Single(l => l.Key == "savedOut").Amount, Is.GreaterThan(0), "what the savings paid at the close");
            Assert.That(season.Moved.Any(l => l.Key == "other"), Is.False, "never an Other row");
            SeasonVm last = SeasonBook.Build(ViewKit2.Context(covered), true);
            Assert.That(last.Earned.Any(l => l.Label == "Interest on your savings"), Is.True, "the interest is earned, named");
            Assert.That(last.Moved.Single(l => l.Key == "savedIn").Amount, Is.LessThan(0), "and it brought no cash");
        }

        // *Next season, already due* says the savings could cover the gap, with the same lever, and what a yes means.
        [Test]
        public void SeasonRow()
        {
            Simulation sim = Short();
            SheetVm vm = MoneySheet.Build(ViewKit2.Context(sim));
            LineVm row = vm.Lines.Single(l => l.Label == "Your savings");
            Assert.That(row.Note, Does.StartWith("Could cover"));
            Assert.That(row.Action.Label, Is.EqualTo("Pay from my savings"));
            Run(sim, row.Action);
            row = MoneySheet.Build(ViewKit2.Context(sim)).Lines.Single(l => l.Label == "Your savings");
            Assert.That(row.Note, Does.StartWith("You said yes"));
            Assert.That(row.Action.Label, Is.EqualTo("Take it back"));
        }

        // The away card names the interest and what the savings paid, since nothing the close does by itself goes unsaid.
        [Test]
        public void AwayRows()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            ViewContext ctx = ViewKit2.Context(sim);
            long now = T(sim);
            var events = new List<SimEvent> { new InterestEarned(now, 150 * Coin), new CoveredFromSavings(now, 7_400 * Coin),
                new AwayAdvanceCompleted(now, now - 30 * Hour, now, 30 * Hour, 30 * Hour, false) };
            AwayCard.Vm vm = AwayCard.Build(ctx, events);
            Assert.That(vm.Lines.Select(l => l.Label), Does.Contain("Interest on savings").And.Contain("Your savings paid the close"));
            Assert.That(vm.Lines.Single(l => l.Label == "Your savings paid the close").Value, Does.Contain("7,400"));
            Assert.That(vm.Lines.Where(l => l.Icon == "savings").All(l => l.WhyKey == "away" && l.WhyId == (long)AwayRow.Savings), Is.True);
            Assert.That(WhyView.Build(ctx, "away", (long)AwayRow.Savings).Rows.Any(r => (r.Label + r.Value).Contains("interest")), Is.True);
        }
    }
}
