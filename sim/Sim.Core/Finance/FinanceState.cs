using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Finance
{
    /// <summary>Cash paid upfront for a period of time, expensed day by day (spec 005 §3.2).</summary>
    public sealed class Prepayment
    {
        public long Id { get; internal set; }
        public Account ExpenseAccount { get; internal set; }
        public Account PrepaidAccount { get; internal set; }
        public long Total { get; internal set; }
        public long Start { get; internal set; }
        public long End { get; internal set; }
        public long Expensed { get; internal set; }

        /// <summary>Who it's for, e.g. <c>founder:living</c>.</summary>
        public string OwnerRef { get; internal set; }

        internal Prepayment Clone()
        {
            return new Prepayment
            {
                Id = Id, ExpenseAccount = ExpenseAccount, PrepaidAccount = PrepaidAccount, Total = Total,
                Start = Start, End = End, Expensed = Expensed, OwnerRef = OwnerRef,
            };
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt((int)ExpenseAccount);
            h.WriteInt((int)PrepaidAccount);
            h.WriteLong(Total);
            h.WriteLong(Start);
            h.WriteLong(End);
            h.WriteLong(Expensed);
            h.WriteString(OwnerRef);
        }

        internal static Prepayment ReadFrom(SaveReader r)
        {
            return new Prepayment
            {
                Id = r.ReadLong(), ExpenseAccount = (Account)r.ReadInt(), PrepaidAccount = (Account)r.ReadInt(), Total = r.ReadLong(),
                Start = r.ReadLong(), End = r.ReadLong(), Expensed = r.ReadLong(), OwnerRef = r.ReadString(),
            };
        }
    }

    /// <summary>The finance state beside the ledger (spec 005 §4): prepayments and season reports.</summary>
    public sealed class FinanceState
    {
        /// <summary>Open prepayments in id order.</summary>
        public List<Prepayment> Prepayments { get; } = new List<Prepayment>();

        public long NextPrepaymentId { get; internal set; } = 1;

        /// <summary>One report per closed season, oldest first. Reports are immutable.</summary>
        public List<SeasonReport> Reports { get; } = new List<SeasonReport>();

        /// <summary>Cash at the start of the current season (after the opening entry at a new game).</summary>
        public long SeasonOpeningCash { get; internal set; }

        public Prepayment FindPrepayment(long id) { return Prepayments.Find(p => p.Id == id); }

        internal FinanceState Clone()
        {
            var c = new FinanceState { NextPrepaymentId = NextPrepaymentId, SeasonOpeningCash = SeasonOpeningCash };
            foreach (Prepayment p in Prepayments) c.Prepayments.Add(p.Clone());
            c.Reports.AddRange(Reports);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(NextPrepaymentId);
            h.WriteLong(SeasonOpeningCash);
            h.WriteInt(Prepayments.Count);
            foreach (Prepayment p in Prepayments) p.WriteTo(h);
            h.WriteInt(Reports.Count);
            foreach (SeasonReport r in Reports) r.WriteTo(h);
        }

        internal static FinanceState ReadFrom(SaveReader r)
        {
            var s = new FinanceState { NextPrepaymentId = r.ReadLong(), SeasonOpeningCash = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Prepayments.Add(Prepayment.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Reports.Add(SeasonReport.ReadFrom(r));
            return s;
        }
    }
}
