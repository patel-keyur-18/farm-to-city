using System;

namespace Sim.Core.Finance
{
    /// <summary>Prepaid costs (spec 005 §3.2): paid upfront, expensed day by day, refunded pro rata when closed early.</summary>
    internal static class Accrual
    {
        /// <summary>Pays <paramref name="total"/> now (Dr prepaid, Cr Cash) for [start, end). The caller has checked CanAfford.</summary>
        public static Prepayment Open(Simulation sim, Account expense, Account prepaid, long total, long start, long end, string ownerRef)
        {
            if (end <= start) throw new ArgumentException("A prepayment needs end > start.");
            FinanceState books = sim.State.Finance;
            var p = new Prepayment
            {
                Id = books.NextPrepaymentId++, ExpenseAccount = expense, PrepaidAccount = prepaid, Total = total,
                Start = start, End = end, OwnerRef = ownerRef,
            };
            if (total > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.prepaid", ownerRef,
                    new Posting(prepaid, total), new Posting(Account.Cash, -total));
            books.Prepayments.Add(p);
            return p;
        }

        /// <summary>
        /// Expenses a prepayment up to t: target = total × (min(t, end) − start) ÷ (end − start), rounded down;
        /// posts target − expensed (Dr expense, Cr prepaid) in <paramref name="period"/>.
        /// </summary>
        public static void ExpenseTo(Simulation sim, Prepayment p, long t, long period)
        {
            long elapsed = Math.Max(0, Math.Min(t, p.End) - p.Start);
            long target = (long)((System.Numerics.BigInteger)p.Total * elapsed / (p.End - p.Start));
            long amount = target - p.Expensed;
            if (amount == 0) return;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, period, "ledger.expensed", p.OwnerRef,
                new Posting(p.ExpenseAccount, amount), new Posting(p.PrepaidAccount, -amount));
            p.Expensed = target;
        }

        /// <summary>Daily pass: expenses every open prepayment to now, in id order, and drops the finished ones.</summary>
        public static void ExpenseAll(Simulation sim, long period)
        {
            long t = sim.State.Clock.SimTime;
            foreach (Prepayment p in sim.State.Finance.Prepayments) ExpenseTo(sim, p, t, period);
            sim.State.Finance.Prepayments.RemoveAll(p => p.End <= t);
        }

        /// <summary>
        /// ClosePrepayment(id, t): expenses up to now, then refunds total − expensed to Cash (Dr Cash, Cr prepaid).
        /// Returns the refund.
        /// </summary>
        public static long Close(Simulation sim, long id)
        {
            FinanceState books = sim.State.Finance;
            Prepayment p = books.FindPrepayment(id) ?? throw new ArgumentException("No prepayment " + id + ".");
            ExpenseTo(sim, p, sim.State.Clock.SimTime, sim.Period);
            long refund = p.Total - p.Expensed;
            if (refund > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.prepaidRefund", p.OwnerRef,
                    new Posting(Account.Cash, refund), new Posting(p.PrepaidAccount, -refund));
            books.Prepayments.Remove(p);
            return refund;
        }

        /// <summary>The refund <see cref="Close"/> would give now (for affordability checks).</summary>
        public static long RefundIfClosed(Simulation sim, long id)
        {
            Prepayment p = sim.State.Finance.FindPrepayment(id);
            if (p == null) return 0;
            long elapsed = Math.Max(0, Math.Min(sim.State.Clock.SimTime, p.End) - p.Start);
            return p.Total - (long)((System.Numerics.BigInteger)p.Total * elapsed / (p.End - p.Start));
        }
    }
}
