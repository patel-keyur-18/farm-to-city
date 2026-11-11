using System.Collections.Generic;

namespace Sim.Core.Finance
{
    /// <summary>The three parts of the cash flow statement (spec 019 §3.1, ADR 0101 §3).</summary>
    public enum CashGroup
    {
        /// <summary>Every income and expense, and working capital (inputs in the store, wages and living paid ahead, the supermarket's payment, interest payable).</summary>
        Work = 0,

        /// <summary>Land, buildings, machines, trees, animals, cash put into businesses and the savings deposit.</summary>
        Bought = 1,

        /// <summary>Loans taken and principal repaid.</summary>
        Borrowing = 2,
    }

    /// <summary>
    /// One row of a season's cash flow: the cash that moved against one account, in one direction (a land row for what was bought, another
    /// for what was sold). <see cref="Amount"/> is signed: money in positive, money out negative.
    /// </summary>
    public sealed class CashRow
    {
        public Account Account { get; }
        public long Amount { get; }
        public CashGroup Group { get { return CashRows.GroupOf(Account); } }
        public bool In { get { return Amount > 0; } }

        /// <summary>The string-table key of the row's label.</summary>
        public string LabelKey { get { return "cash.row." + (int)Account + (In ? ".in" : ".out"); } }

        public CashRow(Account account, long amount) { Account = account; Amount = amount; }
    }

    /// <summary>
    /// A season's cash flow by the other side of each entry (ADR 0101 §3): each cash posting goes to the account it paid for or came from.
    /// A sale's whole price sits against the asset it sold (its gain or loss and depreciation are in the profit and loss).
    /// </summary>
    public static class CashRows
    {
        public static CashGroup GroupOf(Account a)
        {
            switch (a)
            {
                case Account.Land: case Account.Buildings: case Account.AccumulatedDepreciation: case Account.Equipment: case Account.Trees:
                case Account.Livestock: case Account.InvestmentInBusinesses:
                    return CashGroup.Bought;
                case Account.MoneylenderLoans: case Account.Overdraft: case Account.BankLoans:
                    return CashGroup.Borrowing;
                default:
                    return CashGroup.Work;
            }
        }

        private static bool IsAsset(Account a) { return GroupOf(a) == CashGroup.Bought && a != Account.AccumulatedDepreciation; }

        private static bool IsSaleSide(Account a)
        {
            return a == Account.GainOnAssetSales || a == Account.LossOnAssetSales || a == Account.AccumulatedDepreciation;
        }

        /// <summary>The movements an entry makes to cash, each against an account (positive: money in). Empty when it doesn't touch cash.</summary>
        public static IEnumerable<KeyValuePair<Account, long>> Movements(JournalEntry e)
        {
            bool hasSale = false, hasAsset = false;
            foreach (Posting p in e.Postings)
            {
                if (IsSaleSide(p.Account)) hasSale = true;
                if (IsAsset(p.Account)) hasAsset = true;
            }
            var result = new List<KeyValuePair<Account, long>>();
            if (!Touches(e)) return result;
            bool fold = hasSale && hasAsset; // a sale: the price (and its gain, loss and depreciation) against the asset
            long folded = 0;
            Account foldInto = Account.Cash;
            if (fold)
            {
                long best = -1;
                foreach (Posting p in e.Postings)
                    if (IsAsset(p.Account) && System.Math.Abs(p.Amount) > best) { best = System.Math.Abs(p.Amount); foldInto = p.Account; }
            }
            foreach (Posting p in e.Postings)
            {
                if (p.Account == Account.Cash) continue;
                if (fold && IsSaleSide(p.Account)) { folded -= p.Amount; continue; }
                if (fold && p.Account == foldInto) { folded -= p.Amount; continue; }
                result.Add(new KeyValuePair<Account, long>(p.Account, -p.Amount));
            }
            if (fold && folded != 0) result.Add(new KeyValuePair<Account, long>(foldInto, folded));
            return result;
        }

        private static bool Touches(JournalEntry e)
        {
            foreach (Posting p in e.Postings) if (p.Account == Account.Cash) return true;
            return false;
        }

        /// <summary>
        /// The rows of a season from its entries (ids <paramref name="from"/> up to <paramref name="to"/>): grouped, then in account order, money in
        /// before money out. The inheritance is opening cash, not a row. A season is its entries, not their period: what the market paid at the very
        /// boundary hour is in the season whose cash it was in when the books closed.
        /// </summary>
        internal static List<CashRow> Build(Ledger ledger, long from, long to)
        {
            var sums = new Dictionary<int, long>(); // key: account index × 2 + (1 when money in)
            foreach (JournalEntry e in ledger.Entries)
            {
                if (e.Id < from || e.Id >= to || e.DescriptionKey == FinanceSystem.OpeningKey || !Touches(e)) continue;
                foreach (KeyValuePair<Account, long> m in Movements(e))
                {
                    if (m.Value == 0) continue;
                    int key = Accounts.IndexOf(m.Key) * 2 + (m.Value > 0 ? 1 : 0);
                    sums.TryGetValue(key, out long s);
                    sums[key] = s + m.Value;
                }
            }
            var rows = new List<CashRow>();
            for (int g = 0; g <= (int)CashGroup.Borrowing; g++)
                for (int i = 0; i < Accounts.All.Length; i++)
                    for (int dir = 1; dir >= 0; dir--)
                        if ((int)GroupOf(Accounts.All[i]) == g && sums.TryGetValue(i * 2 + dir, out long v) && v != 0) rows.Add(new CashRow(Accounts.All[i], v));
            return rows;
        }

        /// <summary>The movements behind a row (the row's <em>Why?</em>): each entry of the season that moved cash against it, in order.</summary>
        public static List<CashMovement> MovementsOf(Ledger ledger, SeasonReport report, CashRow row)
        {
            var list = new List<CashMovement>();
            foreach (JournalEntry e in ledger.Entries)
            {
                if (e.Id < report.FirstEntry || e.Id >= report.EndEntry || e.DescriptionKey == FinanceSystem.OpeningKey || !Touches(e)) continue;
                foreach (KeyValuePair<Account, long> m in Movements(e))
                    if (m.Key == row.Account && (m.Value > 0) == row.In && m.Value != 0) list.Add(new CashMovement(e, m.Value));
            }
            return list;
        }
    }

    /// <summary>One cash movement behind a row: the entry (its time, description and reference say what and whom) and the amount.</summary>
    public sealed class CashMovement
    {
        public JournalEntry Entry { get; }
        public long Amount { get; }
        public CashMovement(JournalEntry entry, long amount) { Entry = entry; Amount = amount; }
    }
}
