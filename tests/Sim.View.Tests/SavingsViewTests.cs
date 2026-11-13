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

        // The sheet (owner, 2026-11-13): put in and take out by dragging or typing 1,000 to 1,00,000, never more than you have; put in starts
        // at what the close won't need, marked; under 1,000 in savings, all of it at once; the standing choice.
        [Test]
        public void Sheet()
        {
            Simulation sim = ViewKit.NewGame(seed: 5);
            long topUp = 38_611 * Coin - sim.State.Ledger.Cash;
            sim.State.Ledger.Post(T(sim), sim.Period, "test", "test", new Posting(Account.Cash, topUp), new Posting(Account.OpeningEquity, -topUp));
            ViewContext ctx = ViewKit2.Context(sim);
            long due = CloseDues.Next(sim).Sum(d => d.Amount);
            Assert.That(due, Is.GreaterThan(1_000 * Coin).And.LessThan(37_000 * Coin), "the family's living is due");
            SheetVm vm = SavingsSheet.Build(ctx);
            Assert.That(vm.Title, Is.EqualTo("Savings deposit"));
            AmountVm put = vm.Lines.Single(l => l.Amount != null).Amount;
            Assert.That(vm.Lines.Single(l => l.Label == "Nothing in the bank yet").Dim, Is.True, "nothing to take out yet");
            Assert.That((put.Min, put.Max, put.Step), Is.EqualTo((1_000 * Coin, 38_611 * Coin, 1_000 * Coin)), "never more than your cash");
            long spare = 38_611 * Coin - due;
            Assert.That(put.Start, Is.EqualTo(put.Snap(spare / (1_000 * Coin) * (1_000 * Coin))), "starts at what the close won't need");
            Assert.That(put.Mark, Is.EqualTo(spare));
            Assert.That(put.MaxCaption, Is.EqualTo("38,611 (all your cash)"));
            Assert.That(put.Words(32_000 * Coin), Is.EqualTo("32,000 coins"), "the box while not being typed in");

            Assert.That(put.Snap(20_400 * Coin), Is.EqualTo(20_000 * Coin), "drags in steps of 1,000");
            Assert.That(put.Snap(38_200 * Coin), Is.EqualTo(38_611 * Coin), "the end is all of it");
            Assert.That(put.Snap(10), Is.EqualTo(1_000 * Coin));

            AmountPick ok = put.Pick(2_000 * Coin);
            Assert.That((ok.Action.Label, ok.Action.Enabled, ok.Tone), Is.EqualTo(("Put in 2,000", true, Tone.Good)));
            Assert.That(ok.Note, Does.StartWith("Leaves 36,611 coins in cash. The season's close takes"));
            AmountPick shortPick = put.Pick(38_000 * Coin);
            Assert.That((shortPick.Tone, shortPick.Note.Contains("short by")), Is.EqualTo((Tone.Warn, true)));
            AmountPick typed = put.Pick(45_000 * Coin);
            Assert.That((typed.Action.Enabled, typed.Note), Is.EqualTo((false, "More than your cash: you can put in 38,611 coins at most.")));
            Assert.That(put.Pick(999 * Coin).Note, Is.EqualTo("At least 1,000 coins at a time."));
            Run(sim, put.Pick(20_000 * Coin).Action);
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(20_000 * Coin));

            vm = SavingsSheet.Build(ViewKit2.Context(sim));
            AmountVm take = vm.Lines.Single(l => l.Amount != null && l.Amount.Id == "out").Amount;
            Assert.That((take.Min, take.Max, take.Start), Is.EqualTo((1_000 * Coin, 20_000 * Coin, 1_000 * Coin)));
            Assert.That(take.Pick(25_000 * Coin).Note, Is.EqualTo("More than your savings: you can take out 20,000 coins at most."));
            Run(sim, take.Pick(19_258 * Coin).Action);
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(742 * Coin), "any whole amount can be typed");

            vm = SavingsSheet.Build(ViewKit2.Context(sim));
            LineVm small = vm.Lines.Single(l => l.Action != null && l.Action.Id == "out:all");
            Assert.That(small.Action.Label, Is.EqualTo("Take out all 742"));
            Run(sim, small.Action);
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(0));

            vm = SavingsSheet.Build(ViewKit2.Context(sim));
            Run(sim, vm.Lines.Single(l => l.Label == "Always pay from my savings").Action);
            Assert.That(SavingsRules.Always(sim), Is.True);
            vm = SavingsSheet.Build(ViewKit2.Context(sim));
            Assert.That(vm.Lines.Single(l => l.Label == "Always pay from my savings").Badge, Is.EqualTo("Chosen"));
            Assert.That(vm.Lines.Single(l => l.Label == "Ask me").Action.Id, Is.EqualTo("standing:ask"));
            Assert.That(vm.Lines.All(l => l.WhyKey == null || WhyView.Has(l.WhyKey)), Is.True);

            Simulation poor = ViewKit.NewGame(seed: 5);
            poor.State.Ledger.Post(T(poor), poor.Period, "test", "test", new Posting(Account.Cash, 640 * Coin - poor.State.Ledger.Cash), new Posting(Account.OpeningEquity, poor.State.Ledger.Cash - 640 * Coin));
            Assert.That(SavingsSheet.Build(ViewKit2.Context(poor)).Lines.Any(l => l.Label == "You need at least 1,000 coins in cash to put in."), Is.True);
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
