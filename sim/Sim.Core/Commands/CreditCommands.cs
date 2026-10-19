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
            if (!sim.State.Ledger.CanAfford(pay)) return CommandResult.Reject(RejectReason.InsufficientFunds);
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
                sim.State.Ledger.Post(t, sim.Period, "ledger.loanRestructured", "loan:" + loan.Id,
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
}
