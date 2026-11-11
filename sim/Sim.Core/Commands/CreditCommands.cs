using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Commands
{
    /// <summary>Borrows from a lender within its limit (spec 005 §5). Amount in money (1/100 coin), whole coins.</summary>
    public sealed class TakeLoan : TimedCommand
    {
        public string LenderId { get; }
        public long Amount { get; }
        public int TermSeasons { get; }

        public TakeLoan(long at, string lenderId, long amount, int termSeasons) : base(at)
        {
            LenderId = lenderId;
            Amount = amount;
            TermSeasons = termSeasons;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = CreditRules.Check(sim, LenderId, Amount, TermSeasons, 0, out int lender);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            CreditSystem.Create(sim, lender, Amount, TermSeasons);
            return CommandResult.Ok;
        }
    }

    /// <summary>The financing block of <see cref="BuyParcel"/>: a land-purchase loan (ADR 0028, 0038).</summary>
    public sealed class Financing
    {
        public string LenderId { get; }
        public long Amount { get; }
        public int TermSeasons { get; }

        public Financing(string lenderId, long amount, int termSeasons)
        {
            LenderId = lenderId;
            Amount = amount;
            TermSeasons = termSeasons;
        }
    }

    /// <summary>Repays a loan early, at any time, with no penalty: interest to date, then arrears, then principal (spec 005 §3.6).</summary>
    public sealed class RepayLoan : TimedCommand
    {
        public long LoanId { get; }
        public long Amount { get; }

        public RepayLoan(long at, long loanId, long amount) : base(at)
        {
            LoanId = loanId;
            Amount = amount;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Loan loan = sim.State.Credit == null ? null : sim.State.Credit.Find(LoanId);
            if (loan == null) return CommandResult.Reject(RejectReason.UnknownLoan);
            if (!loan.IsOpen) return CommandResult.Reject(RejectReason.LoanClosed);
            if (Amount <= 0) return CommandResult.Reject(RejectReason.InvalidAmount);
            long pay = System.Math.Min(Amount, CreditRules.OwedNow(sim, loan));
            if (!CreditSystem.LedgerOf(sim, loan).CanAfford(pay)) return CommandResult.Reject(RejectReason.InsufficientFunds); // a business loan from its own cash
            CreditSystem.Accrue(sim, loan, sim.Period);
            CreditSystem.Pay(sim, loan, pay, "ledger.loanPayment", out _, out _, out _);
            CreditSystem.CheckCure(sim);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Restructures a loan in arrears, once (spec 005 §3.6): the arrears join the principal, the term grows, the grace counter
    /// resets and the score drops.
    /// </summary>
    public sealed class RestructureLoan : TimedCommand
    {
        public long LoanId { get; }

        public RestructureLoan(long at, long loanId) : base(at) { LoanId = loanId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Loan loan = sim.State.Credit == null ? null : sim.State.Credit.Find(LoanId);
            if (loan == null) return CommandResult.Reject(RejectReason.UnknownLoan);
            if (loan.State == LoanState.Defaulted) return CommandResult.Reject(RejectReason.LoanDefaulted);
            if (!loan.IsOpen) return CommandResult.Reject(RejectReason.LoanClosed);
            LenderDef def = sim.Config.Game.Credit.Lenders[loan.Lender];
            if (def.RestructureExtraSeasons == 0) return CommandResult.Reject(RejectReason.LenderDoesNotRestructure);
            if (loan.Restructured) return CommandResult.Reject(RejectReason.AlreadyRestructured);
            if (!loan.HasArrears) return CommandResult.Reject(RejectReason.NoArrears);

            CreditSystem.Accrue(sim, loan, sim.Period);
            long t = sim.State.Clock.SimTime;
            if (loan.ArrearsInterest > 0)
            {
                // The interest arrears become principal: Dr Interest payable, Cr the loan account.
                CreditSystem.LedgerOf(sim, loan).Post(t, sim.Period, "ledger.loanRestructured", "loan:" + loan.Id,
                    new Posting(Account.InterestPayable, loan.ArrearsInterest), new Posting(def.LoanAccount, -loan.ArrearsInterest));
                loan.Principal += loan.ArrearsInterest;
                loan.ArrearsInterest = 0;
            }
            loan.ArrearsPrincipal = 0; // it is scheduled principal again, over the longer term
            loan.ArrearsCloses = 0;
            loan.Remaining = System.Math.Min(loan.Remaining + def.RestructureExtraSeasons, def.MaxTerm);
            if (loan.Remaining < 1) loan.Remaining = 1;
            loan.InstalmentBase = loan.Principal / loan.Remaining;
            loan.Term = loan.Remaining;
            loan.Restructured = true;
            CreditSystem.ChangeScore(sim, -sim.Config.Game.Credit.Score.RestructurePenalty, "score.restructured");
            sim.Emit(new Events.LoanRestructured(t, loan.Id, loan.Remaining, loan.Principal));
            return CommandResult.Ok;
        }
    }

    /// <summary>Puts cash into the savings deposit at the co-op bank (spec 019 §3.5): at least the minimum, any time.</summary>
    public sealed class DepositSavings : TimedCommand
    {
        public long Amount { get; }
        public DepositSavings(long at, long amount) : base(at) { Amount = amount; }
        protected override CommandResult ApplyAt(Simulation sim) { return SavingsRules.Put(sim, Amount); }
    }

    /// <summary>Takes cash out of the savings deposit, any time (spec 019 §3.5).</summary>
    public sealed class WithdrawSavings : TimedCommand
    {
        public long Amount { get; }
        public WithdrawSavings(long at, long amount) : base(at) { Amount = amount; }
        protected override CommandResult ApplyAt(Simulation sim) { return SavingsRules.Take(sim, Amount); }
    }

    /// <summary>The standing choice <em>If cash runs short at the close</em>: ask (the default) or always pay from the savings (spec 019 §3.5).</summary>
    public sealed class SetSavingsCover : TimedCommand
    {
        public bool Always { get; }

        public SetSavingsCover(long at, bool always) : base(at) { Always = always; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Finance == null) return CommandResult.Reject(RejectReason.NothingShort);
            sim.State.Finance.Savings.Always = Always;
            return CommandResult.Ok;
        }
    }

    /// <summary>Your yes for the coming close: what it takes beyond your cash comes from the savings (spec 019 §3.5). Refused while the close wouldn't be short.</summary>
    public sealed class CoverFromSavings : TimedCommand
    {
        public CoverFromSavings(long at) : base(at) { }
        protected override CommandResult ApplyAt(Simulation sim) { return SavingsRules.Consent(sim, true); }
    }

    /// <summary>Takes the yes back before the close (spec 019 §3.5, as built).</summary>
    public sealed class TakeBackSavingsCover : TimedCommand
    {
        public TakeBackSavingsCover(long at) : base(at) { }
        protected override CommandResult ApplyAt(Simulation sim) { return SavingsRules.Consent(sim, false); }
    }
}
