using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using static Sim.Core.Tests.CreditKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 019 §3.5, ADR 0101 §2 (US-236): the savings deposit, its interest, the warning before a short close and the yes that lets the savings pay.</summary>
    public class DepositTests
    {
        private const long Day = 86_400;

        private static CommandResult Put(Simulation sim, long coins) { return sim.Execute(new DepositSavings(sim.State.Clock.SimTime, Coins(coins))); }

        /// <summary>A founder with 10,000 coins in savings, a bank loan whose payment falls due at the first close, and no cash.</summary>
        private static Simulation Squeezed()
        {
            Simulation sim = NewSim(cash: 20_000);
            Assert.That(Take(sim, "coop_bank", 10_000, 3).Accepted, Is.True);
            Assert.That(Put(sim, 10_000).Accepted, Is.True);
            Burn(sim);
            Assert.That(Cash(sim), Is.EqualTo(0));
            return sim;
        }

        // The deposit moves cash both ways, within what you hold; the minimum is 100 coins; the books balance.
        [Test]
        public void PutAndTake()
        {
            Simulation sim = NewSim(cash: 20_000);
            long worth = Reporting.NetWorth(sim).Total, coopRoom = CreditRules.Limit(sim, 1).RawLimit, moneylenderRoom = CreditRules.Limit(sim, 0).RawLimit;
            Assert.That(sim.Execute(new DepositSavings(0, Coins(99))).Reason, Is.EqualTo(RejectReason.BelowSavingsMinimum));
            Assert.That(sim.Execute(new DepositSavings(0, Coins(20_001))).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(Put(sim, 5_000).Accepted, Is.True);
            Assert.That(Cash(sim), Is.EqualTo(Coins(15_000)));
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(Coins(5_000)));
            Assert.That(sim.Execute(new WithdrawSavings(0, Coins(5_001))).Reason, Is.EqualTo(RejectReason.InsufficientDeposit));
            Assert.That(sim.Execute(new WithdrawSavings(0, Coins(2_000))).Accepted, Is.True);
            Assert.That(Cash(sim), Is.EqualTo(Coins(17_000)));
            Assert.That(Reporting.NetWorth(sim).Deposit, Is.EqualTo(Coins(3_000)));
            Assert.That(CreditRules.Limit(sim, 1).RawLimit, Is.EqualTo(coopRoom), "never collateral: the lenders' room ignores the deposit");
            Assert.That(CreditRules.Limit(sim, 0).RawLimit, Is.EqualTo(moneylenderRoom));
            Assert.That(Reporting.NetWorth(sim).Total, Is.EqualTo(worth), "cash into the deposit leaves net worth as it was");
            ReconcileOk(sim);
        }

        // AC-6: 1.5% a season, added at each close before the statements, so it compounds; Other money, not farm profit.
        [Test]
        public void Interest()
        {
            Simulation sim = NewSim(cash: 20_000);
            Put(sim, 10_000);
            sim.AdvanceTo(S);
            long first = Money.MulRate(Coins(10_000), sim.Config.Game.Ledger.SavingsInterest);
            Assert.That(first, Is.EqualTo(Coins(150)));
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(Coins(10_000) + first));
            SeasonReport report = sim.State.Finance.Reports.Last();
            Assert.That(report.ByAccount.Single(kv => kv.Key == Account.InterestOnSavings).Value, Is.EqualTo(first));
            Assert.That(report.Line("otherIncome").Amount, Is.EqualTo(first), "other money");
            Assert.That(report.FarmProfit, Is.EqualTo(0).Or.LessThan(0), "not farm profit");
            Assert.That(report.NetWorth.Deposit, Is.EqualTo(Coins(10_000) + first), "in the report's net worth, interest included");

            sim.DrainEvents();
            sim.AdvanceTo(2 * S);
            long second = Money.MulRate(Coins(10_000) + first, sim.Config.Game.Ledger.SavingsInterest);
            Assert.That(second, Is.GreaterThan(first), "it compounds");
            Assert.That(SavingsRules.Balance(sim), Is.EqualTo(Coins(10_000) + first + second));
            Assert.That(sim.DrainEvents().OfType<InterestEarned>().Single().Amount, Is.EqualTo(second));
            ReconcileOk(sim);
        }

        // AC-17: without a yes the close runs as before and the deposit is untouched; with one, the savings pay exactly what the charges need.
        [Test]
        public void Consent()
        {
            Simulation none = Squeezed();
            none.DrainEvents();
            none.AdvanceTo(S);
            Assert.That(none.DrainEvents().OfType<LoanPaymentMissed>().Any(), Is.True, "no yes: the payment goes unpaid");
            Assert.That(SavingsRules.Balance(none), Is.EqualTo(Coins(10_000) + Money.MulRate(Coins(10_000), none.Config.Game.Ledger.SavingsInterest)), "the deposit is untouched");

            Simulation yes = Squeezed();
            Assert.That(sim_Due(yes), Is.GreaterThan(0));
            long due = sim_Due(yes);
            Assert.That(yes.Execute(new CoverFromSavings(0)).Accepted, Is.True);
            yes.AdvanceTo(S - 1);
            long interest = Money.MulRate(Coins(10_000), yes.Config.Game.Ledger.SavingsInterest);
            yes.DrainEvents();
            yes.AdvanceTo(S);
            var events = yes.DrainEvents();
            Assert.That(events.OfType<LoanPaymentMissed>().Any(), Is.False, "the savings paid it");
            CoveredFromSavings covered = events.OfType<CoveredFromSavings>().Single();
            Assert.That(covered.Amount, Is.EqualTo(due), "exactly what the close took beyond cash");
            Assert.That(Cash(yes), Is.EqualTo(0), "no more than that left the deposit");
            Assert.That(SavingsRules.Balance(yes), Is.EqualTo(Coins(10_000) + interest - due));
            ReconcileOk(yes);

            // A yes lasts one close.
            yes.DrainEvents();
            yes.AdvanceTo(2 * S);
            Assert.That(yes.DrainEvents().OfType<CoveredFromSavings>().Any(), Is.False, "the next close needs its own yes");

            // It can be taken back; and it can't be given when nothing is short.
            Simulation back = Squeezed();
            back.Execute(new CoverFromSavings(0));
            back.Execute(new TakeBackSavingsCover(0));
            back.AdvanceTo(S);
            Assert.That(back.DrainEvents().OfType<CoveredFromSavings>().Any(), Is.False);
            Simulation fine = NewSim(cash: 20_000);
            Assert.That(fine.Execute(new CoverFromSavings(0)).Reason, Is.EqualTo(RejectReason.NothingShort));
        }

        private static long sim_Due(Simulation sim) { return CloseDues.Next(sim).Sum(d => d.Amount); }

        // The standing choice pays every close without asking; the savings never pay more than the deposit holds.
        [Test]
        public void Standing()
        {
            Simulation sim = Squeezed();
            Assert.That(sim.Execute(new SetSavingsCover(0, true)).Accepted, Is.True);
            sim.DrainEvents();
            sim.AdvanceTo(S);
            var events = sim.DrainEvents();
            Assert.That(events.OfType<CoveredFromSavings>().Single().Amount, Is.GreaterThan(0));
            Assert.That(events.OfType<LoanPaymentMissed>().Any(), Is.False);

            Simulation thin = NewSim(cash: 20_000);
            Take(thin, "coop_bank", 10_000, 3);
            Put(thin, 100);
            Burn(thin);
            thin.Execute(new SetSavingsCover(0, true));
            thin.DrainEvents();
            thin.AdvanceTo(S);
            var thinEvents = thin.DrainEvents();
            Assert.That(thinEvents.OfType<CoveredFromSavings>().Single().Amount, Is.EqualTo(Coins(100) + Money.MulRate(Coins(100), thin.Config.Game.Ledger.SavingsInterest)), "all it holds, no more");
            Assert.That(thinEvents.OfType<LoanPaymentMissed>().Any(), Is.True, "and the rest goes unpaid");
            Assert.That(SavingsRules.Balance(thin), Is.EqualTo(0));
            ReconcileOk(thin);
        }

        // AC-18: a warning at 00:00 of the last day when the close would come up short; once more if something later makes it short again.
        [Test]
        public void Warning()
        {
            Simulation sim = Squeezed();
            sim.DrainEvents();
            sim.AdvanceTo(S - Day - 1);
            Assert.That(sim.DrainEvents().OfType<CloseShortAhead>().Any(), Is.False, "not before the last day");
            sim.AdvanceTo(S - Day);
            CloseShortAhead warning = sim.DrainEvents().OfType<CloseShortAhead>().Single();
            Assert.That(warning.Short, Is.EqualTo(sim_Due(sim)));
            Assert.That(warning.FirstUnpaid, Does.StartWith("loan:"));
            sim.AdvanceTo(S - Day + 6 * 3600);
            Assert.That(sim.DrainEvents().OfType<CloseShortAhead>().Any(), Is.False, "once, not every hour");

            AddCash(sim, 5_000); // enough again
            sim.AdvanceTo(S - Day + 8 * 3600);
            Assert.That(sim.DrainEvents().OfType<CloseShortAhead>().Any(), Is.False);
            Burn(sim); // short again
            sim.AdvanceTo(S - Day + 10 * 3600);
            Assert.That(sim.DrainEvents().OfType<CloseShortAhead>().Count(), Is.EqualTo(1), "short again, warned again");

            Simulation covered = Squeezed();
            covered.Execute(new SetSavingsCover(0, true));
            covered.DrainEvents();
            covered.AdvanceTo(S - 1);
            Assert.That(covered.DrainEvents().OfType<CloseShortAhead>().Any(), Is.False, "the savings will cover it: nothing to warn of");
        }

        // Sprint 18 review (F1): the bills are priced as the close prices them, the season's rise included, so cash that only covers them at
        // today's prices is warned of.
        [Test]
        public void Warning_CountsTheRiseInPrices()
        {
            Simulation sim = TestSim.NewFarm(seed: 3);
            sim.AdvanceTo(S - Day - 1);
            var farm = sim.Config.Game.Farm;
            long today = Sim.Core.Market.MarketRules.Indexed(sim, farm.DietPlans[sim.State.Farm.Founder.DietPlan].CostPerSeason);
            long rise = CloseDues.Next(sim).Single(d => d.Key == "living").Amount - today;
            Assert.That(rise, Is.GreaterThan(0), "the close's prices are higher than today's");
            long set = today - Cash(sim); // cash enough at today's prices, short by the rise at the close's
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.set", null, new Posting(Account.Cash, set), new Posting(Account.OpeningEquity, -set));
            sim.DrainEvents();
            sim.AdvanceTo(S - Day);
            CloseShortAhead warning = sim.DrainEvents().OfType<CloseShortAhead>().Single();
            Assert.That(warning.FirstUnpaid, Is.EqualTo("living"));
            Assert.That(warning.Short, Is.GreaterThan(0).And.LessThanOrEqualTo(rise));
        }

        // The notification: a short close is planned for a player who'll be away; none when the savings will cover it.
        [Test]
        public void Notification()
        {
            Simulation sim = Squeezed();
            sim.AdvanceTo(S - 2 * Day);
            var items = Sim.Core.Saves.NotificationPlanner.Absence(sim).Where(i => i.Kind == Sim.Core.Saves.NotifyKind.CloseShort).ToList();
            Assert.That(items.Count, Is.EqualTo(1));
            Assert.That(Sim.Core.Saves.NotificationPlanner.ImportanceOf(Sim.Core.Saves.NotifyKind.CloseShort), Is.EqualTo(Sim.Core.Saves.NotifyImportance.NeedsYou));

            Simulation covered = Squeezed();
            covered.Execute(new SetSavingsCover(0, true));
            covered.AdvanceTo(S - 2 * Day);
            Assert.That(Sim.Core.Saves.NotificationPlanner.Absence(covered).Any(i => i.Kind == Sim.Core.Saves.NotifyKind.CloseShort), Is.False);
        }

        // No money loop: borrowing to deposit loses, at either lender, because a loan costs more a season than the deposit earns.
        [Test]
        public void NoMoneyLoop()
        {
            foreach (string lender in new[] { "coop_bank", "moneylender" })
            {
                Simulation plain = NewSim(cash: 20_000), loop = NewSim(cash: 20_000);
                Assert.That(Take(loop, lender, 10_000, 4).Accepted, Is.True, lender);
                Assert.That(Put(loop, 10_000).Accepted, Is.True);
                plain.AdvanceTo(4 * S);
                loop.AdvanceTo(4 * S);
                long plainWorth = Reporting.NetWorth(plain).Total, loopWorth = Reporting.NetWorth(loop).Total;
                Assert.That(loopWorth, Is.LessThan(plainWorth), lender + ": the loan's interest is more than the deposit's");
                ReconcileOk(loop);
            }
        }

        // The deposit is never collateral and a foreclosure never takes it (as cash in hand isn't taken).
        [Test]
        public void NeverSeized()
        {
            Simulation sim = NewSim(cash: 1_000_000, freeBank: false);
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", Coins(60_000), 8))).Accepted, Is.True);
            Assert.That(Take(sim, "moneylender", 20_000, 1).Accepted, Is.True);
            Put(sim, 5_000);
            Burn(sim);
            sim.AdvanceTo(2 * S);
            Assert.That(sim.State.Credit.PendingForeclosure, Is.True);
            sim.AdvanceTo(2 * S + 24 * 3600);
            Assert.That(sim.State.Credit.PendingForeclosure, Is.False, "foreclosed");
            Assert.That(SavingsRules.Balance(sim), Is.GreaterThanOrEqualTo(Coins(5_000)), "the deposit stays");
            ReconcileOk(sim);
        }

        // The state survives a save, and the hash is what it was while nothing is chosen (the goldens).
        [Test]
        public void SavedAndHashed()
        {
            Simulation sim = NewSim(cash: 20_000);
            ulong plain = sim.State.ComputeHash();
            sim.Execute(new SetSavingsCover(0, true));
            Assert.That(sim.State.ComputeHash(), Is.Not.EqualTo(plain), "a choice is state");
            byte[] bytes = Sim.Core.Saves.SaveCodec.Write(sim.State, sim.Config, Sim.Core.Tests.TestSim.Wall0);
            var loaded = Sim.Core.Saves.SaveCodec.Read(bytes, sim.Config);
            Assert.That(loaded.State.Finance.Savings.Always, Is.True);
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
