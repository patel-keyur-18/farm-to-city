using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Finance
{
    /// <summary>
    /// One line of the season report (spec 005 §3.4, ADR 0032): a plain-language label and a Why? explanation
    /// (string-table keys, text in spec 008), the amount as it adds to the net result, and the accounts behind it.
    /// </summary>
    public sealed class ReportLine
    {
        public string Key { get; }
        public string LabelKey { get { return "report." + Key; } }
        public string WhyKey { get { return "report." + Key + ".why"; } }

        /// <summary>Signed contribution: income positive, costs negative; the profit lines are their sums.</summary>
        public long Amount { get; }
        public IReadOnlyList<Account> Accounts { get; }

        internal ReportLine(string key, long amount, Account[] accounts)
        {
            Key = key;
            Amount = amount;
            Accounts = System.Array.AsReadOnly(accounts);
        }
    }

    /// <summary>Opening cash + inflows − outflows = closing cash (spec 005 §3.4 step 3).</summary>
    public sealed class CashFlowSummary
    {
        public long Opening { get; internal set; }
        public long Inflows { get; internal set; }
        public long Outflows { get; internal set; }
        public long Closing { get; internal set; }

        /// <summary>Cash paid this season into prepaid accounts: cash out, but not (yet) expense.</summary>
        public long PaidUpfront { get; internal set; }
    }

    /// <summary>Net worth and its terms (spec 005 §3.5). Liabilities are positive amounts that are subtracted.</summary>
    public sealed class NetWorthSnapshot
    {
        public long Cash { get; internal set; }
        public long Prepaid { get; internal set; }
        public long LandAtMarket { get; internal set; }
        public long BuildingsAtBook { get; internal set; }
        public long TreesAtBook { get; internal set; }
        public long StoredGoods { get; internal set; }
        public long LoanPrincipal { get; internal set; }
        public long InterestPayable { get; internal set; }

        /// <summary>Assets − liabilities on the ledger; the difference from Total is land and stored goods at market value.</summary>
        public long BookEquity { get; internal set; }

        public long Total
        {
            get { return Cash + Prepaid + LandAtMarket + BuildingsAtBook + TreesAtBook + StoredGoods - LoanPrincipal - InterestPayable; }
        }
    }

    /// <summary>The season's credit outcome (spec 005 §3.4 step 8): the score and what the loans paid.</summary>
    public sealed class CreditSummary
    {
        public int Score { get; internal set; }
        public int ScoreChange { get; internal set; }
        public long PrincipalOutstanding { get; internal set; }
        public long InterestPaid { get; internal set; }
        public long PrincipalPaid { get; internal set; }

        /// <summary>The part of payments due at this close that could not be paid.</summary>
        public long Missed { get; internal set; }
    }

    /// <summary>A closed season's P&amp;L, cash flow and net worth (spec 005 §3.4). Immutable.</summary>
    public sealed class SeasonReport
    {
        public long Period { get; internal set; }

        /// <summary>The P&amp;L by account, income and expense accounts with a balance, in account order (signed like <see cref="ReportLine.Amount"/>).</summary>
        public IReadOnlyList<KeyValuePair<Account, long>> ByAccount { get; internal set; }

        /// <summary>farmIncome, farmCosts, farmProfit, otherIncome, household, finance, other, netResult.</summary>
        public IReadOnlyList<ReportLine> Lines { get; internal set; }

        public CashFlowSummary CashFlow { get; internal set; }
        public NetWorthSnapshot NetWorth { get; internal set; }

        /// <summary>Home-grown food the household ate this season, and the garden's part (the household line's Why?, ADR 0032).</summary>
        public long HomeUse { get; internal set; }
        public long GardenHomeUse { get; internal set; }

        /// <summary>Null when the game has no credit content.</summary>
        public CreditSummary Credit { get; internal set; }

        public ReportLine Line(string key)
        {
            foreach (ReportLine l in Lines)
                if (l.Key == key) return l;
            return null;
        }

        public long FarmProfit { get { return Line("farmProfit").Amount; } }
        public long NetResult { get { return Line("netResult").Amount; } }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(Period);
            h.WriteInt(ByAccount.Count);
            foreach (KeyValuePair<Account, long> kv in ByAccount)
            {
                h.WriteInt((int)kv.Key);
                h.WriteLong(kv.Value);
            }
            h.WriteLong(CashFlow.Opening);
            h.WriteLong(CashFlow.Inflows);
            h.WriteLong(CashFlow.Outflows);
            h.WriteLong(CashFlow.PaidUpfront);
            h.WriteLong(HomeUse);
            h.WriteLong(GardenHomeUse);
            h.WriteLong(NetWorth.Total);
            h.WriteLong(NetWorth.BookEquity);
            if (Credit != null)
            {
                h.WriteInt(Credit.Score);
                h.WriteInt(Credit.ScoreChange);
                h.WriteLong(Credit.PrincipalOutstanding);
                h.WriteLong(Credit.InterestPaid);
                h.WriteLong(Credit.PrincipalPaid);
                h.WriteLong(Credit.Missed);
            }
        }
    }
}
