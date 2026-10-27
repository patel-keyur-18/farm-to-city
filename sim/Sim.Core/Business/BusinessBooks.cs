using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;

namespace Sim.Core.Business
{
    /// <summary>
    /// A business's own books (spec 016 §3.2): the founder's cash put in and taken out, the season's report and close to retained
    /// earnings, and its equity in the founder's net worth.
    /// </summary>
    public static class BusinessBooks
    {
        /// <summary>Closed seasons kept on a business: enough for plan vs actual (§3.3).</summary>
        public const int KeptReports = 4;

        /// <summary>Cash in (§3.2): founder Dr 1800 Investment in businesses, Cr Cash; business Dr Cash, Cr Owner's capital.</summary>
        internal static CommandResult PutCash(Simulation sim, long id, long amount)
        {
            Business b = sim.State.Businesses?.Find(id);
            RejectReason r = Check(b, amount);
            if (r == RejectReason.None && !sim.State.Ledger.CanAfford(amount)) r = RejectReason.InsufficientFunds;
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + id;
            sim.State.Ledger.Post(now, sim.Period, "ledger.putCash", reference, new Posting(Account.InvestmentInBusinesses, amount), new Posting(Account.Cash, -amount));
            b.Ledger.Post(now, sim.Period, "ledger.putCash", reference, new Posting(Account.Cash, amount), new Posting(Account.OwnersCapital, -amount));
            return CommandResult.Ok;
        }

        /// <summary>
        /// Drawings (§3.2): business Dr Owner's capital, Cr Cash; founder Dr Cash, Cr 1800 down to what this business still holds of
        /// your capital, the rest Cr 4300 Business drawings income.
        /// </summary>
        internal static CommandResult TakeCash(Simulation sim, long id, long amount)
        {
            Business b = sim.State.Businesses?.Find(id);
            RejectReason r = Check(b, amount);
            if (r == RejectReason.None && !b.Ledger.CanAfford(amount)) r = RejectReason.InsufficientFunds;
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + id;
            long atCost = System.Math.Min(amount, System.Math.Max(0, CapitalIn(b)));
            b.Ledger.Post(now, sim.Period, "ledger.takeCash", reference, new Posting(Account.OwnersCapital, amount), new Posting(Account.Cash, -amount));
            var postings = new List<Posting> { new Posting(Account.Cash, amount) };
            if (atCost > 0) postings.Add(new Posting(Account.InvestmentInBusinesses, -atCost));
            if (amount > atCost) postings.Add(new Posting(Account.BusinessDrawingsIncome, atCost - amount));
            sim.State.Ledger.Post(now, sim.Period, "ledger.takeCash", reference, postings.ToArray());
            return CommandResult.Ok;
        }

        private static RejectReason Check(Business b, long amount)
        {
            if (b == null) return RejectReason.UnknownBusiness;
            if (b.Status == BusinessStatus.Closed) return RejectReason.BusinessClosed;
            return amount <= 0 ? RejectReason.InvalidArgument : RejectReason.None;
        }

        /// <summary>Your capital still in the business: put in less taken out (its Owner's capital).</summary>
        public static long CapitalIn(Business b) { return -b.Ledger.Balance(Account.OwnersCapital); }

        /// <summary>Assets − liabilities on the business's books (§3.2); net worth counts it never below 0.</summary>
        public static long Equity(Business b) { return b.Ledger.TotalOf(AccountGroup.Assets) - b.Ledger.TotalOf(AccountGroup.Liabilities); }

        /// <summary>Every business at its equity, each never below 0 (§3.2, AC-5).</summary>
        public static long AtEquity(Simulation sim)
        {
            long sum = 0;
            if (sim.State.Businesses != null)
                foreach (Business b in sim.State.Businesses.Businesses) sum += System.Math.Max(0, Equity(b));
            return sum;
        }

        /// <summary>A season's report from the business's balances and its entries of <paramref name="period"/>: the closed season's, or this one so far.</summary>
        public static BusinessReport Report(Business b, long period)
        {
            var balances = new List<KeyValuePair<Account, long>>();
            foreach (Account a in Accounts.All)
                if (b.Ledger.Balance(a) != 0) balances.Add(new KeyValuePair<Account, long>(a, b.Ledger.Balance(a)));
            long inflows = 0, outflows = 0;
            foreach (JournalEntry e in b.Ledger.Entries)
            {
                if (e.Period != period) continue;
                foreach (Posting p in e.Postings)
                {
                    if (p.Account != Account.Cash) continue;
                    if (p.Amount > 0) inflows += p.Amount;
                    else outflows -= p.Amount;
                }
            }
            return new BusinessReport
            {
                Period = period, Balances = balances.AsReadOnly(), Opening = b.SeasonOpeningCash, Inflows = inflows, Outflows = outflows,
                AreaGrams = b.AreaSeason, OwnGrams = b.OwnSeason,
            };
        }

        /// <summary>Net profit on a report's P&amp;L: income less expenses (§3.2; a profitable season is one above 0).</summary>
        public static long NetProfit(BusinessReport r)
        {
            long sum = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances)
            {
                AccountGroup g = Accounts.GroupOf(kv.Key);
                if (g == AccountGroup.Income || g == AccountGroup.Expenses) sum -= kv.Value;
            }
            return sum;
        }

        /// <summary>The season's report, then income and expenses to retained earnings (spec 005 §3.4 steps 1–3), kept and checked.</summary>
        internal static void CloseSeason(Simulation sim, Business b, long closing)
        {
            BusinessReport report = Report(b, closing);
            FinanceSystem.CloseToRetainedEarnings(b.Ledger, sim.State.Clock.SimTime, closing);
            string failure = b.Ledger.Reconcile();
            if (failure != null)
            {
                if (sim.Config.Options.StrictChecks) throw new System.InvalidOperationException("Business " + b.Id + " books failed to reconcile: " + failure);
                sim.Emit(new LedgerReconciliationFailed(sim.State.Clock.SimTime, "business " + b.Id + ": " + failure));
            }
            b.Reports.Add(report);
            if (b.Reports.Count > KeptReports) b.Reports.RemoveAt(0);
            b.SeasonOpeningCash = b.Ledger.Cash;
            b.AreaSeason = 0;
            b.OwnSeason = 0;
        }
    }
}
