using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Finance
{
    public enum LoanState
    {
        Active = 0,
        Repaid = 1,
        Defaulted = 2,
        WrittenOff = 3,
    }

    /// <summary>One loan (spec 005 §4). Money is 1/100 coin; interest is posted to the ledger when it accrues.</summary>
    public sealed class Loan
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="CreditDefs.Lenders"/>.</summary>
        public int Lender { get; internal set; }
        public long OriginalPrincipal { get; internal set; }

        /// <summary>All principal outstanding, arrears included (it stays in the loan account).</summary>
        public long Principal { get; internal set; }
        public Fixed Rate { get; internal set; }
        public Fixed PenaltyRate { get; internal set; }
        public LoanSchedule Schedule { get; internal set; }
        public int Term { get; internal set; }
        public long TakenAt { get; internal set; }

        /// <summary>Instalments still to fall due (not counting a partial first season).</summary>
        public int Remaining { get; internal set; }

        /// <summary>The principal of one instalment (original ÷ term, rounded down); the last takes what is left.</summary>
        public long InstalmentBase { get; internal set; }

        /// <summary>Taken part-way through a season: the first close charges interest only.</summary>
        public bool FirstClosePartial { get; internal set; }

        public long ArrearsPrincipal { get; internal set; }

        /// <summary>Interest unpaid from earlier closes (inside Interest payable).</summary>
        public long ArrearsInterest { get; internal set; }

        /// <summary>Interest accrued since the last close, due at the next one (inside Interest payable).</summary>
        public long InterestAccrued { get; internal set; }
        public int ArrearsCloses { get; internal set; }

        /// <summary>Interest is accrued up to here; a new segment starts when the principal changes and at each close.</summary>
        public long SegmentStart { get; internal set; }
        public bool Restructured { get; internal set; }
        public LoanState State { get; internal set; }

        /// <summary>The business the loan was made to (its entries go to that business's books, spec 016 §3.8), or 0 for yours.</summary>
        public long BusinessId { get; internal set; }

        public bool IsOpen { get { return State == LoanState.Active || State == LoanState.Defaulted; } }
        public bool HasArrears { get { return ArrearsPrincipal > 0 || ArrearsInterest > 0; } }

        internal Loan Clone() { return (Loan)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Lender, Lender);
            h.WriteLong(OriginalPrincipal);
            h.WriteLong(Principal);
            h.WriteFixed(Rate);
            h.WriteFixed(PenaltyRate);
            h.WriteInt((int)Schedule);
            h.WriteInt(Term);
            h.WriteLong(TakenAt);
            h.WriteInt(Remaining);
            h.WriteLong(InstalmentBase);
            h.WriteBool(FirstClosePartial);
            h.WriteLong(ArrearsPrincipal);
            h.WriteLong(ArrearsInterest);
            h.WriteLong(InterestAccrued);
            h.WriteInt(ArrearsCloses);
            h.WriteLong(SegmentStart);
            h.WriteBool(Restructured);
            h.WriteInt((int)State);
            if (h.Hashing ? BusinessId != 0 : !(h is SaveWriter sw && sw.Schema < 10)) h.WriteLong(BusinessId); // hashed only once used: the goldens stay
        }

        internal static Loan ReadFrom(SaveReader r)
        {
            var loan = new Loan
            {
                Id = r.ReadLong(), Lender = r.ReadRef(ContentTable.Lender), OriginalPrincipal = r.ReadLong(), Principal = r.ReadLong(),
                Rate = r.ReadFixed(), PenaltyRate = r.ReadFixed(), Schedule = (LoanSchedule)r.ReadInt(), Term = r.ReadInt(), TakenAt = r.ReadLong(),
                Remaining = r.ReadInt(), InstalmentBase = r.ReadLong(), FirstClosePartial = r.ReadBool(), ArrearsPrincipal = r.ReadLong(),
                ArrearsInterest = r.ReadLong(), InterestAccrued = r.ReadLong(), ArrearsCloses = r.ReadInt(), SegmentStart = r.ReadLong(),
                Restructured = r.ReadBool(), State = (LoanState)r.ReadInt(),
            };
            if (r.Schema >= 10) loan.BusinessId = r.ReadLong();
            return loan;
        }
    }

    /// <summary>One change to the credit score, with its reason key, for Why? (spec 005 §3.7).</summary>
    public readonly struct ScoreChange
    {
        public long Time { get; }
        public int Change { get; }
        public int NewScore { get; }
        public string Reason { get; }

        public ScoreChange(long time, int change, int newScore, string reason)
        {
            Time = time;
            Change = change;
            NewScore = newScore;
            Reason = reason;
        }
    }

    /// <summary>The founder's credit: the score, the loans and the foreclosure clock (spec 005 §4). Null when the game has no credit content.</summary>
    public sealed class CreditState
    {
        public int Score { get; internal set; }
        public List<ScoreChange> History { get; } = new List<ScoreChange>();

        /// <summary>Every loan ever taken, in id order (closed ones stay for Why? and the hash).</summary>
        public List<Loan> Loans { get; } = new List<Loan>();
        public long NextLoanId { get; internal set; } = 1;

        /// <summary>A default has been noticed and the foreclosure clock runs (ADR 0038).</summary>
        public bool PendingForeclosure { get; internal set; }

        /// <summary>Seconds of live play since the final notice; away time doesn't count.</summary>
        public long NoticeLiveSeconds { get; internal set; }

        /// <summary>False when the notice was raised while away and not yet shown on return.</summary>
        public bool NoticeAnnounced { get; internal set; }
        public int Foreclosures { get; internal set; }

        /// <summary>A loan was outstanding at some moment of the current season (so no clean-season gain).</summary>
        public bool LoanSeenThisSeason { get; internal set; }

        public Loan Find(long id) { return Loans.Find(l => l.Id == id); }

        internal CreditState Clone()
        {
            var c = new CreditState
            {
                Score = Score, NextLoanId = NextLoanId, PendingForeclosure = PendingForeclosure, NoticeLiveSeconds = NoticeLiveSeconds,
                NoticeAnnounced = NoticeAnnounced, Foreclosures = Foreclosures, LoanSeenThisSeason = LoanSeenThisSeason,
            };
            c.History.AddRange(History);
            foreach (Loan l in Loans) c.Loans.Add(l.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Score);
            h.WriteLong(NextLoanId);
            h.WriteBool(PendingForeclosure);
            h.WriteLong(NoticeLiveSeconds);
            h.WriteBool(NoticeAnnounced);
            h.WriteInt(Foreclosures);
            h.WriteBool(LoanSeenThisSeason);
            h.WriteInt(History.Count);
            foreach (ScoreChange s in History)
            {
                h.WriteLong(s.Time);
                h.WriteInt(s.Change);
                h.WriteInt(s.NewScore);
                h.WriteString(s.Reason);
            }
            h.WriteInt(Loans.Count);
            foreach (Loan l in Loans) l.WriteTo(h);
        }

        internal static CreditState ReadFrom(SaveReader r)
        {
            var s = new CreditState
            {
                Score = r.ReadInt(), NextLoanId = r.ReadLong(), PendingForeclosure = r.ReadBool(), NoticeLiveSeconds = r.ReadLong(),
                NoticeAnnounced = r.ReadBool(), Foreclosures = r.ReadInt(), LoanSeenThisSeason = r.ReadBool(),
            };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++)
            {
                long t = r.ReadLong();
                int change = r.ReadInt(), score = r.ReadInt();
                s.History.Add(new ScoreChange(t, change, score, r.ReadString()));
            }
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Loans.Add(Loan.ReadFrom(r));
            return s;
        }
    }
}
