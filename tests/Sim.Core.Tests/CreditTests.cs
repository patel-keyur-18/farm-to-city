using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using static Sim.Core.Tests.CreditKit;

namespace Sim.Core.Tests
{
    /// <summary>Spec 005 §3.6–3.8: lenders, limits, schedules, repayment, arrears, score, default, restructuring (AC-16 to AC-24, AC-31 to AC-33).</summary>
    public class CreditTests
    {
        // AC-16: the formula, the moneylender's limit at a new game, one coin over is refused.
        [Test]
        public void Limit_Formula()
        {
            Simulation sim = NewSim(freeBank: false);
            Assert.That(CreditRules.Limit(sim, 0).Limit, Is.EqualTo(Coins(25_000)), "moneylender: its base limit");
            Assert.That(sim.Execute(new TakeLoan(0, "moneylender", Coins(25_001), 1)).Reason, Is.EqualTo(RejectReason.OverLimit));
            Assert.That(sim.Execute(new TakeLoan(0, "moneylender", Coins(25_000), 1)).Accepted, Is.True);

            // The bank: 0.60 × collateral + 1.0 × average farm profit, × the score factor (score 650 → 0.75), rounded down to 100 coins.
            Simulation bank = NewSim(cash: 1_000_000, freeBank: false);
            Assert.That(bank.Execute(new BuyParcel(0, "w1")).Accepted && bank.Execute(new BuyParcel(0, "n3")).Accepted, Is.True);
            long collateral = bank.State.Land.Parcels.Where(p => p.Owned && !p.Protected).Sum(p => LandRules.ParcelPrice(bank, p));
            FakeReport(bank, Coins(20_000));
            FakeReport(bank, Coins(20_000));
            CreditLimit limit = CreditRules.Limit(bank, 1);
            Assert.That(limit.Collateral, Is.EqualTo(collateral));
            Assert.That(limit.AverageFarmProfit, Is.EqualTo(Coins(20_000)));
            Assert.That(limit.ScoreFactor, Is.EqualTo(Fixed.Parse("0.75")));
            long raw = Money.MulRate(collateral, Fixed.Parse("0.6")) + Coins(20_000);
            long expected = Money.MulRate(raw, Fixed.Parse("0.75"));
            Assert.That(limit.Limit, Is.EqualTo(expected - expected % Coins(100)));
            Assert.That(limit.Limit, Is.GreaterThan(Coins(100_000)));
        }

        // AC-17: the inherited plot is never collateral.
        [Test]
        public void InheritedPlot_NotCollateral()
        {
            Simulation sim = NewSim(freeBank: false);
            Assert.That(CreditRules.Limit(sim, 1).Limit, Is.EqualTo(0));
            Assert.That(CreditRules.Limit(sim, 1).Collateral, Is.EqualTo(0));
            Assert.That(Take(sim, "coop_bank", 10_000, 4).Reason, Is.EqualTo(RejectReason.OverLimit));
        }

        // AC-18: a loan adds cash and the lender's account, fixes the rate and emits LoanTaken; bad terms and sizes are refused.
        [Test]
        public void TakeLoan()
        {
            Simulation sim = NewSim(freeBank: false);
            long cash = Cash(sim);
            Assert.That(Take(sim, "moneylender", 20_000, 2).Accepted, Is.True);
            Assert.That(Cash(sim), Is.EqualTo(cash + Coins(20_000)));
            Assert.That(sim.State.Ledger.Balance(Account.MoneylenderLoans), Is.EqualTo(-Coins(20_000)));
            LoanTaken e = Last<LoanTaken>(sim.DrainEvents());
            Assert.That(e.Rate, Is.EqualTo(Fixed.Parse("0.06")));
            Assert.That(e.Amount, Is.EqualTo(Coins(20_000)));
            ReconcileOk(sim);

            Assert.That(Take(sim, "moneylender", 500, 2).Reason, Is.EqualTo(RejectReason.BelowMinimum));
            Assert.That(Take(sim, "moneylender", 2_000, 5).Reason, Is.EqualTo(RejectReason.InvalidTerm));
            Assert.That(Take(sim, "moneylender", 2_000, 0).Reason, Is.EqualTo(RejectReason.InvalidTerm));
            Assert.That(Take(sim, "nobody", 2_000, 1).Reason, Is.EqualTo(RejectReason.UnknownLender));
            Assert.That(sim.Execute(new TakeLoan(0, "moneylender", 150_050, 1)).Reason, Is.EqualTo(RejectReason.InvalidAmount), "not whole coins");
            Assert.That(Take(sim, "moneylender", 0, 1).Reason, Is.EqualTo(RejectReason.InvalidAmount));
            Assert.That(Take(sim, "coop_bank", 5_000, 4).Reason, Is.EqualTo(RejectReason.BelowMinimum), "the bank's minimum");
            Assert.That(Take(sim, "coop_bank", 10_000, 17).Reason, Is.EqualTo(RejectReason.InvalidTerm), "the bank's maximum is 16");

            // The bank's rate at score 650 is 3.0% (a land-purchase loan: the parcel is the collateral).
            Simulation land = NewSim(cash: 400_000, freeBank: false);
            long price = LandRules.ParcelPrice(land, land.State.Land.Parcels[land.Config.Game.Map.IndexOfParcel("w1")]);
            Assert.That(land.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", Coins(10_000), 4))).Accepted, Is.True);
            Assert.That(Last<LoanTaken>(land.DrainEvents()).Rate.Raw, Is.EqualTo(Fixed.Parse("0.03").Raw).Within(2), "3.0% (within Fixed rounding)");
            Assert.That(Cash(land), Is.EqualTo(Coins(400_000) - price + Coins(10_000)));
        }

        // AC-19: the schedules pay exactly; a part-season loan pays its part-season interest first.
        [Test]
        public void Schedules_Exact()
        {
            Simulation bank = NewSim();
            bank.AdvanceTo(S);
            bank.State.Credit.Score = 750; // the bank's best rate, 2.5%
            Assert.That(Take(bank, "coop_bank", 100_000, 4).Accepted, Is.True);
            long[] expected = { 27_500, 26_875, 26_250, 25_625 };
            for (int k = 0; k < 4; k++) Assert.That(CloseCashChange(bank, (2 + k) * S), Is.EqualTo(-Coins(expected[k])), "instalment " + (k + 1));
            Assert.That(Loan(bank).State, Is.EqualTo(LoanState.Repaid));
            Assert.That(bank.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(0));
            Assert.That(bank.State.Ledger.Balance(Account.InterestPayable), Is.EqualTo(0));
            ReconcileOk(bank);

            Simulation bullet = NewSim();
            bullet.AdvanceTo(S);
            Assert.That(Take(bullet, "moneylender", 20_000, 2).Accepted, Is.True);
            Assert.That(CloseCashChange(bullet, 2 * S), Is.EqualTo(-Coins(1_200)));
            Assert.That(CloseCashChange(bullet, 3 * S), Is.EqualTo(-Coins(21_200)));
            Assert.That(Loan(bullet).State, Is.EqualTo(LoanState.Repaid));

            // Taken half-way through a season: half a season's interest at the first close, the instalment at the next.
            Simulation half = NewSim();
            half.AdvanceTo(S + S / 2);
            Assert.That(Take(half, "moneylender", 20_000, 1).Accepted, Is.True);
            Assert.That(CloseCashChange(half, 2 * S), Is.EqualTo(-Coins(600)), "6% × 20,000 × ½");
            Assert.That(CloseCashChange(half, 3 * S), Is.EqualTo(-Coins(21_200)));
        }

        // AC-20: early repayment pays interest first, later instalments shrink, total interest falls.
        [Test]
        public void RepayEarly()
        {
            Simulation sim = NewSim();
            sim.AdvanceTo(S);
            sim.State.Credit.Score = 750;
            Take(sim, "coop_bank", 100_000, 4);
            Assert.That(CloseCashChange(sim, 2 * S), Is.EqualTo(-Coins(27_500)));

            long t = 2 * S + S / 2;
            sim.AdvanceTo(t);
            Assert.That(sim.Execute(new RepayLoan(t, 1, Coins(30_000))).Accepted, Is.True);
            RepaymentPaid(sim.DrainEvents().OfType<LoanRepayment>().Last(), out long interest, out long principal);
            Assert.That(interest, Is.EqualTo(Coins(937) + 50), "75,000 × 2.5% × ½ = 937.50 first");
            Assert.That(principal, Is.EqualTo(Coins(30_000) - interest));
            Assert.That(Loan(sim).Principal, Is.EqualTo(Coins(75_000) - principal));

            long third = -CloseCashChange(sim, 3 * S);
            Assert.That(third, Is.LessThan(Coins(26_875)), "the next instalment is smaller than scheduled");
            while (Loan(sim).State == LoanState.Active) sim.AdvanceTo(sim.State.Clock.SimTime + S);
            long interestPaid = sim.State.Ledger.Balance(Account.Interest) + sim.State.Finance.Reports.Sum(r => -r.ByAccount.Where(a => a.Key == Account.Interest).Sum(a => a.Value));
            Assert.That(Loan(sim).State, Is.EqualTo(LoanState.Repaid));
            Assert.That(sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(0));
            Assert.That(interestPaid, Is.LessThan(Coins(6_250)), "less than on schedule");
            ReconcileOk(sim);
        }

        private static void RepaymentPaid(LoanRepayment e, out long interest, out long principal)
        {
            interest = e.Interest;
            principal = e.Arrears + e.Principal;
        }

        // AC-21: a short close takes all the cash, interest first; the rest is arrears with penalty interest.
        [Test]
        public void MissedPayment_ArrearsAndPenalty()
        {
            Simulation sim = NewSim(cash: 0);
            sim.AdvanceTo(S);
            Assert.That(Take(sim, "moneylender", 20_000, 1).Accepted, Is.True);
            Assert.That(CloseCashChange(sim, 2 * S), Is.EqualTo(-Coins(20_000)), "takes all the cash");
            LoanPaymentMissed missed = Last<LoanPaymentMissed>(sim.DrainEvents());
            Assert.That(missed.Unpaid, Is.EqualTo(Coins(1_200)), "21,200 due, 20,000 paid");
            Loan loan = Loan(sim);
            Assert.That(loan.ArrearsPrincipal, Is.EqualTo(Coins(1_200)), "interest was paid first (1,200), so principal is short");
            Assert.That(loan.ArrearsInterest, Is.EqualTo(0));
            Assert.That(Cash(sim), Is.EqualTo(0));

            sim.AdvanceTo(3 * S);
            // A season on 1,200 of arrears: 6% interest and 3% penalty on it.
            Assert.That(sim.State.Finance.Reports.Last().ByAccount.Single(a => a.Key == Account.PenaltyInterest).Value, Is.EqualTo(-Coins(36)));
            Assert.That(sim.State.Finance.Reports.Last().ByAccount.Single(a => a.Key == Account.Interest).Value, Is.EqualTo(-Coins(72)));
            ReconcileOk(sim);
        }

        // AC-22: +10 on time, −40 missed, +5 clean; once a season, not per loan; clamped; a same-season loan earns nothing.
        [Test]
        public void Score_Changes()
        {
            Simulation clean = NewSim();
            Assert.That(clean.State.Credit.Score, Is.EqualTo(650));
            clean.AdvanceTo(S);
            Assert.That(clean.State.Credit.Score, Is.EqualTo(655), "no loan all season: +5");

            Simulation two = NewSim();
            two.AdvanceTo(S);
            Take(two, "moneylender", 5_000, 1);
            Take(two, "moneylender", 5_000, 1);
            two.AdvanceTo(2 * S);
            Assert.That(two.State.Credit.Score, Is.EqualTo(655 + 10), "two loans paid on time: +10 once, not per loan");

            Simulation missed = NewSim(cash: 0);
            missed.AdvanceTo(S);
            Take(missed, "moneylender", 5_000, 1);
            Burn(missed);
            missed.AdvanceTo(2 * S);
            Assert.That(missed.State.Credit.Score, Is.EqualTo(655 - 40), "a missed payment: −40");

            Simulation quick = NewSim();
            quick.AdvanceTo(S);
            Take(quick, "moneylender", 5_000, 1);
            quick.AdvanceTo(S + 100);
            Assert.That(quick.Execute(new RepayLoan(S + 100, 1, Coins(5_100))).Accepted, Is.True);
            quick.AdvanceTo(2 * S);
            Assert.That(quick.State.Credit.Score, Is.EqualTo(655), "a loan taken and repaid inside one season earns nothing");

            Simulation top = NewSim();
            top.State.Credit.Score = 898;
            top.AdvanceTo(S);
            Assert.That(top.State.Credit.Score, Is.EqualTo(900), "clamped to the maximum");
            Assert.That(top.State.Credit.History.Last().Change, Is.EqualTo(2));

            Simulation low = NewSim(cash: 0);
            low.AdvanceTo(S);
            Take(low, "moneylender", 5_000, 1);
            Burn(low);
            low.State.Credit.Score = 310;
            low.AdvanceTo(2 * S);
            Assert.That(low.State.Credit.Score, Is.EqualTo(300), "clamped to the minimum");
        }

        // AC-23: a bank loan with arrears at 3 consecutive closes (grace 2) defaults at the third; clearing them resets the count.
        [Test]
        public void Default_AfterGrace()
        {
            Simulation sim = NewSim(cash: 0);
            sim.AdvanceTo(S);
            Take(sim, "coop_bank", 20_000, 8);
            Burn(sim);
            sim.AdvanceTo(2 * S);
            Assert.That(Loan(sim).ArrearsCloses, Is.EqualTo(1));
            sim.AdvanceTo(3 * S);
            Assert.That(Loan(sim).ArrearsCloses, Is.EqualTo(2));
            Assert.That(Loan(sim).State, Is.EqualTo(LoanState.Active));
            sim.AdvanceTo(4 * S);
            Assert.That(Loan(sim).ArrearsCloses, Is.EqualTo(3));
            Assert.That(Loan(sim).State, Is.EqualTo(LoanState.Defaulted));
            Assert.That(sim.DrainEvents().OfType<LoanDefaulted>().Count(), Is.EqualTo(1));

            // Clearing the arrears after the second close resets the count: no default at the third.
            Simulation cured = NewSim(cash: 0);
            cured.AdvanceTo(S);
            Take(cured, "coop_bank", 20_000, 8);
            Burn(cured);
            cured.AdvanceTo(2 * S);
            cured.AdvanceTo(3 * S);
            Assert.That(Loan(cured).ArrearsCloses, Is.EqualTo(2));
            AddCash(cured, 50_000);
            Loan arrearsLoan = Loan(cured);
            long arrears = CreditRules.OwedNow(cured, arrearsLoan) - (arrearsLoan.Principal - arrearsLoan.ArrearsPrincipal);
            Assert.That(cured.Execute(new RepayLoan(3 * S, 1, arrears)).Accepted, Is.True);
            Assert.That(Loan(cured).HasArrears, Is.False, "the arrears are paid");
            Assert.That(Loan(cured).ArrearsCloses, Is.EqualTo(0));
            cured.AdvanceTo(4 * S);
            Assert.That(Loan(cured).State, Is.Not.EqualTo(LoanState.Defaulted));
            ReconcileOk(cured);
        }

        // AC-24: restructuring adds the arrears to the principal, extends the term, resets the count, costs 20 points; once; not the moneylender.
        [Test]
        public void Restructure()
        {
            Simulation sim = NewSim(cash: 0);
            sim.AdvanceTo(S);
            Take(sim, "coop_bank", 20_000, 4);
            Take(sim, "moneylender", 5_000, 1);
            Assert.That(sim.Execute(new RestructureLoan(S, 1)).Reason, Is.EqualTo(RejectReason.NoArrears));
            Burn(sim);
            sim.AdvanceTo(2 * S);
            Assert.That(Loan(sim).HasArrears, Is.True);
            int score = sim.State.Credit.Score;
            Assert.That(sim.Execute(new RestructureLoan(2 * S, 1)).Accepted, Is.True);
            Loan bank = Loan(sim);
            Assert.That(bank.ArrearsPrincipal + bank.ArrearsInterest, Is.EqualTo(0));
            Assert.That(bank.Remaining, Is.EqualTo(3 + 4), "three instalments left, four more seasons");
            Assert.That(bank.ArrearsCloses, Is.EqualTo(0));
            Assert.That(sim.State.Credit.Score, Is.EqualTo(score - 20));
            Assert.That(bank.Restructured, Is.True);
            Assert.That(sim.Execute(new RestructureLoan(2 * S, 1)).Reason, Is.EqualTo(RejectReason.AlreadyRestructured));
            Assert.That(sim.Execute(new RestructureLoan(2 * S, 2)).Reason, Is.EqualTo(RejectReason.LenderDoesNotRestructure));
            Assert.That(sim.Execute(new RestructureLoan(2 * S, 99)).Reason, Is.EqualTo(RejectReason.UnknownLoan));
            ReconcileOk(sim);
        }

        // AC-31: selling land under a bank loan repays the bank first; an underwater sale is refused.
        [Test]
        public void SellCollateral_RepaysBankFirst()
        {
            Simulation sim = NewSim(cash: 400_000, freeBank: false);
            long price = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel("w1")]);
            long sale = Money.RoundToCoin(Money.MulRate(price, Fixed.Parse("0.8")));
            long loan = (sale / Coins(1) / 2) * Coins(1); // half the quick-sale value, in whole coins
            loan -= loan % Coins(1000);
            Assert.That(loan, Is.GreaterThanOrEqualTo(Coins(10_000)));
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", loan, 8))).Accepted, Is.True);
            Burn(sim);
            long cashBefore = Cash(sim);
            sim.AdvanceTo(S / 2);
            Assert.That(sim.Execute(new SellParcel(S / 2, "w1")).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(0), "the bank is repaid in full");
            Assert.That(Loan(sim).State, Is.EqualTo(LoanState.Repaid));
            Assert.That(Cash(sim), Is.GreaterThan(cashBefore));
            ReconcileOk(sim);

            // Underwater: a loan of the whole price (a big farm income lifts the limit) is bigger than the sale proceeds (80%).
            Simulation under = NewSim(cash: 400_000, freeBank: false);
            for (int k = 0; k < 4; k++) FakeReport(under, Coins(400_000));
            long all = LandRules.ParcelPrice(under, under.State.Land.Parcels[under.Config.Game.Map.IndexOfParcel("w1")]);
            Assert.That(under.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", all - all % Coins(1), 16))).Accepted, Is.True);
            ulong hash = under.State.ComputeHash();
            Assert.That(under.Execute(new SellParcel(0, "w1")).Reason, Is.EqualTo(RejectReason.LoanSecured));
            Assert.That(under.State.ComputeHash(), Is.EqualTo(hash), "a refusal changes nothing");
        }

        // AC-33: BuyParcel with financing: at most the price, any lender, both succeed or neither.
        [Test]
        public void BuyParcel_Financing()
        {
            Simulation sim = NewSim(cash: 20_000, freeBank: false);
            long price = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel("w1")]);
            ulong hash = sim.State.ComputeHash();
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", price + Coins(1), 8))).Reason, Is.EqualTo(RejectReason.InvalidAmount));
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", Coins(10_000), 8))).Reason, Is.EqualTo(RejectReason.InsufficientFunds), "cash for the down payment is missing");
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("moneylender", Coins(30_000), 1))).Reason, Is.EqualTo(RejectReason.OverLimit));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash), "a rejection changes nothing");

            AddCash(sim, 1_000_000);
            long cash = Cash(sim);
            Assert.That(sim.Execute(new BuyParcel(0, "w1", new Financing("coop_bank", Coins(30_000), 8))).Accepted, Is.True);
            Assert.That(Cash(sim), Is.EqualTo(cash - price + Coins(30_000)));
            Assert.That(sim.State.Credit.Loans.Count, Is.EqualTo(1));
            Assert.That(sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel("w1")].Owned, Is.True);
            ReconcileOk(sim);

            // A moneylender loan can finance land too (it is just cash); the parcel is no collateral for it.
            Assert.That(sim.Execute(new BuyParcel(0, "n3", new Financing("moneylender", Coins(10_000), 1))).Accepted, Is.True);
            Assert.That(sim.State.Credit.Loans.Count, Is.EqualTo(2));
        }

        // AC-35 / AC-4: over 3 game years of seeded borrowing, repaying, missing and foreclosure the books reconcile at every close.
        [Test]
        public void Reconciles_WithCredit()
        {
            int loans = 0, missed = 0, defaults = 0, foreclosures = 0;
            for (ulong seed = 1; seed <= 6; seed++)
            {
                bool reckless = seed % 2 == 0;
                Simulation sim = NewSim(seed: seed, cash: 30_000, freeBank: false, edit: (f, j) => f == "finance.json" ? j.Replace("\"retainSeasons\": 4", "\"retainSeasons\": 16") : j);
                long end = 3 * sim.Clock.SecondsPerYear;
                ulong k = 0;
                foreach (long t in FarmBot.Rounds(0, end))
                {
                    FarmBot.Play(sim, t, seed, k);
                    CreditBot.Play(sim, t, seed, k++, reckless);
                }
                sim.AdvanceTo(end); // every season close reconciles (strict: a failure throws)
                var events = sim.DrainEvents();
                loans += sim.State.Credit.Loans.Count;
                missed += events.OfType<LoanPaymentMissed>().Count();
                defaults += events.OfType<LoanDefaulted>().Count();
                foreclosures += sim.State.Credit.Foreclosures;

                Ledger l = sim.State.Ledger;
                Assert.That(l.Entries.SelectMany(e => e.Postings).Where(p => p.Account == Account.Cash).Sum(p => p.Amount), Is.EqualTo(l.Cash), "seed " + seed);
                Assert.That(l.Reconcile(), Is.Null, "seed " + seed);
                long owed = sim.State.Credit.Loans.Where(x => x.IsOpen).Sum(x => x.Principal);
                Assert.That(-(l.Balance(Account.MoneylenderLoans) + l.Balance(Account.BankLoans)), Is.EqualTo(owed), "the loan accounts equal the loans, seed " + seed);
                long interest = sim.State.Credit.Loans.Where(x => x.IsOpen).Sum(x => x.ArrearsInterest + x.InterestAccrued);
                Assert.That(-l.Balance(Account.InterestPayable), Is.EqualTo(interest), "interest payable equals the loans', seed " + seed);
            }
            Assert.That(loans, Is.GreaterThan(10), "the borrowers borrowed");
            Assert.That(missed, Is.GreaterThan(0), "payments were missed");
            Assert.That(defaults, Is.GreaterThan(0), "loans defaulted");
            Assert.That(foreclosures, Is.GreaterThan(0), "a foreclosure ran");
        }

        // AC-30: the spec 001 chunking check holds with the ledger, loans, missed payments and a foreclosure over 2 game years.
        [Test]
        public void ChunkingInvariance()
        {
            const ulong seed = 8;
            GameDefs game = Game(cash: 30_000, freeBank: false);
            Simulation one = TestSim.NewFarm(seed: seed, localSeconds: 9 * 3600, game: game);
            long start = one.State.Clock.SimTime, end = start + 2 * one.Clock.SecondsPerYear;
            ulong k = 0;
            foreach (long t in FarmBot.Rounds(start, end)) { FarmBot.Play(one, t, seed, k); CreditBot.Play(one, t, seed, k++, true); }
            one.AdvanceTo(end);

            Simulation many = TestSim.NewFarm(seed: seed, localSeconds: 9 * 3600, game: game);
            var chunks = new System.Collections.Generic.List<long>();
            for (ulong i = 0; i < 500; i++) chunks.Add(start + 1 + (long)Rng.Below(Rng.Next(seed, (RngStream)905, i, 0), (ulong)(end - start - 1)));
            chunks.Sort();
            int next = 0;
            k = 0;
            foreach (long t in FarmBot.Rounds(start, end))
            {
                while (next < chunks.Count && chunks[next] < t) many.AdvanceTo(chunks[next++]);
                FarmBot.Play(many, t, seed, k);
                CreditBot.Play(many, t, seed, k++, true);
            }
            while (next < chunks.Count) many.AdvanceTo(chunks[next++]);
            many.AdvanceTo(end);

            Assert.That(one.State.Credit.Loans.Count, Is.GreaterThan(3), "the borrower borrowed");
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
        }

        // AC-28: an unpaid living-cost obligation creates no liability: the loan is paid first, the household drops to subsistence.
        [Test]
        public void UnpaidLivingCosts_CreateNoDebt()
        {
            Simulation sim = TestSim.NewFarm(game: TestContent.Game((f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 18000") : j));
            sim.AdvanceTo(S - 100);
            Assert.That(Take(sim, "moneylender", 5_000, 1).Accepted, Is.True);
            Burn(sim);
            sim.DrainEvents();
            sim.AdvanceTo(2 * S - 100);
            long loans = sim.State.Ledger.Balance(Account.MoneylenderLoans) + sim.State.Ledger.Balance(Account.BankLoans);
            sim.AdvanceTo(2 * S);
            var events = sim.DrainEvents();
            Assert.That(events.OfType<ObligationUnpaid>().Any(e => e.Kind == "Living"), Is.True);
            Assert.That(sim.State.Farm.Founder.AutoSubsistence, Is.True);
            Assert.That(sim.State.Ledger.Balance(Account.MoneylenderLoans) + sim.State.Ledger.Balance(Account.BankLoans), Is.GreaterThanOrEqualTo(loans), "the loan account never grows from a living cost");
            Assert.That(Cash(sim), Is.GreaterThanOrEqualTo(0));
            ReconcileOk(sim);
        }
    }
}
