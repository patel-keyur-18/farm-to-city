using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-139 (spec 021 §3.6, §3.11): the budget's proposal and windows, its variance report and the warning a day before a short close.</summary>
    public class BudgetTests
    {
        private const long Clerk = 30_000 * Coin;
        private const int Office = 0, Education = 2;

        private static Simulation Start(long grantCoins = 3_000_000)
        {
            return TreasuryTests.Start((f, j) => f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": " + grantCoins) : j);
        }

        private static TreasuryState T(Simulation sim) { return sim.State.Treasury; }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        private static void Close(Simulation sim) { sim.AdvanceTo(NextClose(sim)); }

        // AC-8: the set window at the opening and each Spring close, one revision at the Monsoon close; a window lasts till it is accepted or the next close.
        [Test]
        public void Windows()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            Assert.That((T(sim).Window, T(sim).WindowSeen), Is.EqualTo((BudgetWindow.Set, false)), "opened at the opening, not yet looked at");
            Assert.That(T(sim).Budget[Office], Is.EqualTo(Clerk), "the first budget is its needs, in force at once");

            Close(sim); // Summer starts
            Assert.That(T(sim).Window, Is.EqualTo(BudgetWindow.None), "the opening's window lasted till this close");
            Assert.That(sim.Execute(new SetBudget(sim.State.Clock.SimTime, Office, Clerk)).Reason, Is.EqualTo(RejectReason.NoWindow));
            Assert.That(sim.Execute(new AcceptBudget(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.NoWindow));

            sim.DrainEvents();
            Close(sim); // Monsoon starts: the revision
            Assert.That(T(sim).Window, Is.EqualTo(BudgetWindow.Revise));
            Assert.That(sim.PendingEvents.OfType<BudgetWindowOpened>().Single().Revision, Is.True);
            Ok(sim, new AcceptBudget(sim.State.Clock.SimTime));
            Assert.That((T(sim).Window, T(sim).WindowSeen), Is.EqualTo((BudgetWindow.None, true)), "accepted: locked until the next window");
            Assert.That(sim.Execute(new AcceptBudget(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.NoWindow));

            Close(sim); // Winter: nothing
            Assert.That(T(sim).Window, Is.EqualTo(BudgetWindow.None));
            sim.DrainEvents();
            Close(sim); // Spring of the next year: the set window
            Assert.That((T(sim).Window, T(sim).WindowSeen), Is.EqualTo((BudgetWindow.Set, false)));
            Assert.That(sim.PendingEvents.OfType<BudgetWindowOpened>().Single().Revision, Is.False);
            Close(sim); Close(sim); // Monsoon again: one revision a year
            Assert.That(T(sim).Window, Is.EqualTo(BudgetWindow.Revise));
        }

        // AC-8: a window opened and ignored leaves the proposal in force and ends at the next close.
        [Test]
        public void Ignored()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            long budget = T(sim).Budget[Office];
            Close(sim);
            Close(sim);
            Assert.That(T(sim).Window, Is.EqualTo(BudgetWindow.Revise));
            Assert.That((T(sim).WindowSeen, T(sim).Budget[Office]), Is.EqualTo((false, budget)), "ignoring the card leaves the proposal");
            Close(sim);
            Assert.That(T(sim).Window, Is.EqualTo(BudgetWindow.None), "a window ends at the next close");
        }

        // AC-8: the proposal is last year's actuals at today's prices; the first year's is the department's needs.
        [Test]
        public void Proposal()
        {
            Simulation full = Start();
            TreasuryTests.Township(full);
            for (int i = 0; i < 4; i++) Close(full);
            Assert.That(T(full).Budget[Office], Is.EqualTo(TreasuryRules.Needs(full, Office)), "paid in full last year: the proposal is its needs");

            Simulation poor = Start(40_000); // pays 30,000, then 10,000, then nothing
            TreasuryTests.Township(poor);
            for (int i = 0; i < 4; i++) Close(poor);
            Assert.That(T(poor).Budget[Office], Is.EqualTo(10_000 * Coin), "a third of its needs was paid over the year, so a third is proposed");
        }

        // AC-8: the range is 0 to 120% of its needs now; a change marks the window seen and the window stays open till accepted.
        [Test]
        public void Range()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SetBudget(t, Office, Clerk * 12 / 10 + 1)).Reason, Is.EqualTo(RejectReason.OutOfRange));
            Assert.That(sim.Execute(new SetBudget(t, Office, -1)).Reason, Is.EqualTo(RejectReason.OutOfRange));
            Assert.That(sim.Execute(new SetBudget(t, 9, 0)).Reason, Is.EqualTo(RejectReason.OutOfRange));
            Assert.That(sim.Execute(new SetBudget(t, Education, 1)).Reason, Is.EqualTo(RejectReason.OutOfRange), "a department with no lines has nothing to budget");
            Ok(sim, new SetBudget(t, Office, Clerk * 12 / 10));
            Assert.That((T(sim).Budget[Office], T(sim).WindowSeen, T(sim).Window), Is.EqualTo((Clerk * 12 / 10, true, BudgetWindow.Set)));
            Ok(sim, new SetBudget(t, Office, 0));
            Assert.That(T(sim).Budget[Office], Is.EqualTo(0));
            Assert.That(sim.PendingEvents.OfType<BudgetSet>().Count(), Is.EqualTo(2));
        }

        // AC-8: a line that appears in the year joins its department's budget at its needs at once.
        [Test]
        public void NewLine()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            Assert.That(T(sim).Budget[Education], Is.EqualTo(0));
            var eras = sim.Config.Game.Onboarding.Eras;
            int hamlet = 0, township = 0;
            for (int i = 0; i < eras.Count; i++) { if (eras[i].Id == "hamlet") hamlet = i; if (eras[i].Id == "township") township = i; }
            long t = sim.State.Clock.SimTime;
            sim.State.Era.Current = hamlet;
            Ok(sim, new BuildSchool(t));
            sim.AdvanceTo(t + 48 * Hour);
            sim.State.Era.Current = township;
            Close(sim);
            long upkeep = Sim.Core.People.SchoolRules.Upkeep(sim);
            Assert.That(T(sim).Budget[Education], Is.EqualTo(upkeep), "its needs, added at once");
            DepartmentRow row = T(sim).Reports.Last().Rows[Education];
            Assert.That((row.Paid, (VarianceSentence)row.Sentence), Is.EqualTo((upkeep, VarianceSentence.NewLine)));
        }

        // AC-9: each close reports budget, paid, variance and one sentence from the fixed list.
        [Test]
        public void Variance()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            Close(sim);
            DepartmentRow row = T(sim).Reports.Last().Rows[Office];
            Assert.That((row.Budget, row.Paid, (VarianceSentence)row.Sentence), Is.EqualTo((Clerk, Clerk, VarianceSentence.None)));
            Assert.That(T(sim).Reports.Last().Rows[1].Sentence, Is.EqualTo((int)VarianceSentence.None), "nothing to pay, nothing to say");

            Close(sim); // the revision window
            Ok(sim, new SetBudget(sim.State.Clock.SimTime, Office, 10_000 * Coin));
            Close(sim);
            Assert.That((VarianceSentence)T(sim).Reports.Last().Rows[Office].Sentence, Is.EqualTo(VarianceSentence.BudgetBelowNeeds), "cut below its needs, paid in pass 2");
            Assert.That(T(sim).Reports.Last().Rows[Office].PaidInPass2, Is.EqualTo(20_000 * Coin));

            Simulation spare = Start();
            TreasuryTests.Township(spare);
            Ok(spare, new SetBudget(spare.State.Clock.SimTime, Office, Clerk * 12 / 10));
            Close(spare);
            Assert.That((VarianceSentence)T(spare).Reports.Last().Rows[Office].Sentence, Is.EqualTo(VarianceSentence.Unspent));

            Simulation poor = Start(10_000);
            TreasuryTests.Township(poor);
            Close(poor);
            Assert.That((VarianceSentence)T(poor).Reports.Last().Rows[Office].Sentence, Is.EqualTo(VarianceSentence.CashRanOut));

            var paid = new DepartmentRow { Budget = Clerk, Needs = Clerk + 900 * Coin, Paid = Clerk + 900 * Coin };
            Assert.That(TreasuryRules.Sentence(paid, Clerk, false), Is.EqualTo(VarianceSentence.PricesRose), "set at its needs, dearer since");
            Assert.That(TreasuryRules.Sentence(paid, Clerk + 900 * Coin, false), Is.EqualTo(VarianceSentence.BudgetBelowNeeds));
        }

        // AC-14: on the season's last day a close that would leave a department unpaid is warned of once, again if the shortfall changes; the
        // notification names no money.
        [Test]
        public void Warning()
        {
            Simulation sim = Start(40_000);
            TreasuryTests.Township(sim);
            Close(sim); // paid 30,000 of 40,000; 10,000 left, 30,000 due
            long close = NextClose(sim);
            sim.DrainEvents();
            sim.AdvanceTo(close - 2 * 24 * Hour);
            Assert.That(sim.PendingEvents.OfType<TreasuryShortAhead>().Count(), Is.EqualTo(0), "not before the last day");
            sim.AdvanceTo(close - 24 * Hour + Hour);
            TreasuryShortAhead w = sim.PendingEvents.OfType<TreasuryShortAhead>().Single();
            Assert.That((w.Short, w.FirstUnpaid), Is.EqualTo((20_000 * Coin, Office)));
            sim.AdvanceTo(close - 12 * Hour);
            Assert.That(sim.PendingEvents.OfType<TreasuryShortAhead>().Count(), Is.EqualTo(1), "once");

            sim.DrainEvents();
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 5_000 * Coin));
            sim.AdvanceTo(close - 10 * Hour);
            Assert.That(sim.PendingEvents.OfType<TreasuryShortAhead>().Single().Short, Is.EqualTo(15_000 * Coin), "a change warns again");

            sim.DrainEvents();
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 15_000 * Coin));
            sim.AdvanceTo(close - 5 * Hour);
            Assert.That(sim.PendingEvents.OfType<TreasuryShortAhead>().Count(), Is.EqualTo(0), "covered: nothing to warn of");

            Assert.That(NotificationPlanner.ImportanceOf(NotifyKind.TreasuryShort), Is.EqualTo(NotifyImportance.NeedsYou));
        }

        // AC-16: the budget and its window survive a save.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start();
            TreasuryTests.Township(sim);
            Ok(sim, new SetBudget(sim.State.Clock.SimTime, Office, 12_345 * Coin));
            Close(sim);
            Close(sim);
            byte[] bytes = SaveCodec.Write(sim.State, sim.Config, Wall0);
            LoadResult loaded = SaveCodec.Read(bytes, sim.Config);
            Assert.That(loaded.Error, Is.EqualTo(SaveError.None), loaded.Detail);
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That((T(loaded).Window, T(loaded).Budget[Office], T(loaded).BudgetNeeds[Office]), Is.EqualTo((T(sim).Window, T(sim).Budget[Office], T(sim).BudgetNeeds[Office])));
        }

        private static TreasuryState T(LoadResult r) { return r.State.Treasury; }
    }
}
