using System;
using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Finance
{
    /// <summary>One line of a journal entry: positive = debit, negative = credit (spec 005 §3.1).</summary>
    public readonly struct Posting
    {
        public Account Account { get; }
        public long Amount { get; }

        public Posting(Account account, long amount)
        {
            Account = account;
            Amount = amount;
        }
    }

    /// <summary>A balanced journal entry. Immutable once posted.</summary>
    public sealed class JournalEntry
    {
        public long Id { get; }
        public long SimTime { get; }

        /// <summary>The season index since t = 0 the entry belongs to.</summary>
        public long Period { get; }

        /// <summary>String-table key for the Why? text.</summary>
        public string DescriptionKey { get; }

        /// <summary>What the entry is about, e.g. <c>parcel:3</c>, or null.</summary>
        public string Reference { get; }

        public IReadOnlyList<Posting> Postings { get; }

        internal JournalEntry(long id, long simTime, long period, string descriptionKey, string reference, Posting[] postings)
        {
            Id = id;
            SimTime = simTime;
            Period = period;
            DescriptionKey = descriptionKey;
            Reference = reference;
            Postings = Array.AsReadOnly(postings);
        }
    }

    /// <summary>
    /// The founder's double-entry ledger, exact to 1/100 coin (spec 005 §3.1). Like a bank's general
    /// ledger: every change is a balanced entry, and balances are kept per account.
    /// </summary>
    public sealed class Ledger
    {
        private readonly long[] _balances = new long[Accounts.All.Length];
        private readonly List<JournalEntry> _entries = new List<JournalEntry>();

        public long NextEntryId { get; private set; } = 1;

        /// <summary>Retained entries, oldest first (spec 005 §3.1 retention).</summary>
        public IReadOnlyList<JournalEntry> Entries { get { return _entries; } }

        public long Balance(Account a) { return _balances[Accounts.IndexOf(a)]; }

        public long Cash { get { return Balance(Account.Cash); } }

        /// <summary>True exactly when cash ≥ <paramref name="amount"/>.</summary>
        public bool CanAfford(long amount) { return Cash >= amount; }

        /// <summary>The group's balance with its natural sign: assets and expenses as debits, the others as credits.</summary>
        public long TotalOf(AccountGroup group)
        {
            long sum = 0;
            for (int i = 0; i < _balances.Length; i++)
                if (Accounts.GroupOf(Accounts.All[i]) == group) sum += _balances[i];
            return group == AccountGroup.Assets || group == AccountGroup.Expenses ? sum : -sum;
        }

        /// <summary>Income − expenses not yet closed to retained earnings.</summary>
        public long NetIncome { get { return TotalOf(AccountGroup.Income) - TotalOf(AccountGroup.Expenses); } }

        /// <summary>
        /// Posts a balanced entry. Throws (a bug) and changes nothing if the postings don't sum to zero or
        /// Cash would go negative.
        /// </summary>
        internal JournalEntry Post(long simTime, long period, string descriptionKey, string reference, params Posting[] postings)
        {
            if (postings == null || postings.Length == 0) throw new ArgumentException("An entry needs postings.", nameof(postings));
            long sum = 0, cashChange = 0;
            foreach (Posting p in postings)
            {
                Accounts.IndexOf(p.Account); // validates the account
                sum = checked(sum + p.Amount);
                if (p.Account == Account.Cash) cashChange = checked(cashChange + p.Amount);
            }
            if (sum != 0) throw new InvalidOperationException("Unbalanced entry '" + descriptionKey + "': postings sum to " + sum + ".");
            if (checked(Cash + cashChange) < 0)
                throw new InvalidOperationException("Entry '" + descriptionKey + "' would make Cash negative (" + Cash + " + " + cashChange + ").");

            foreach (Posting p in postings)
            {
                int i = Accounts.IndexOf(p.Account);
                _balances[i] = checked(_balances[i] + p.Amount);
            }
            var entry = new JournalEntry(NextEntryId++, simTime, period, descriptionKey, reference, (Posting[])postings.Clone());
            _entries.Add(entry);
            return entry;
        }

        /// <summary>
        /// The reconciliation check (spec 005 §3.1): the trial balance sums to zero, Cash isn't negative, and
        /// assets = liabilities + equity (+ the net income not yet closed). Returns null when the books balance,
        /// otherwise what failed.
        /// </summary>
        public string Reconcile()
        {
            long trial = 0;
            foreach (long b in _balances) trial = checked(trial + b);
            if (trial != 0) return "Trial balance sums to " + trial + ", not 0.";
            if (Cash < 0) return "Cash is negative (" + Cash + ").";
            long assets = TotalOf(AccountGroup.Assets);
            long claims = TotalOf(AccountGroup.Liabilities) + TotalOf(AccountGroup.Equity) + NetIncome;
            if (assets != claims) return "Assets " + assets + " ≠ liabilities + equity + net income " + claims + ".";
            return null;
        }

        /// <summary>Drops entries of periods before <paramref name="firstKeptPeriod"/> (retention).</summary>
        internal void DropEntriesBefore(long firstKeptPeriod)
        {
            int n = 0;
            while (n < _entries.Count && _entries[n].Period < firstKeptPeriod) n++;
            _entries.RemoveRange(0, n);
        }

        internal Ledger Clone()
        {
            var c = new Ledger { NextEntryId = NextEntryId };
            Array.Copy(_balances, c._balances, _balances.Length);
            c._entries.AddRange(_entries); // entries are immutable
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(NextEntryId);
            if (!h.Hashing) h.WriteInt(_balances.Length); // a save is always the current schema // the chart of accounts is code: checked on load
            for (int i = 0; i < _balances.Length; i++)
                if (!h.Hashing || Accounts.SinceSchema(Accounts.All[i]) == 0 || _balances[i] != 0) h.WriteLong(_balances[i]); // late accounts hash only once used: the goldens stay as they were
            h.WriteInt(_entries.Count);
            foreach (JournalEntry e in _entries)
            {
                h.WriteLong(e.Id);
                h.WriteLong(e.SimTime);
                h.WriteLong(e.Period);
                h.WriteString(e.DescriptionKey);
                h.WriteString(e.Reference);
                h.WriteInt(e.Postings.Count);
                foreach (Posting p in e.Postings)
                {
                    h.WriteInt((int)p.Account);
                    h.WriteLong(p.Amount);
                }
            }
        }

        internal static Ledger ReadFrom(SaveReader r)
        {
            var l = new Ledger { NextEntryId = r.ReadLong() };
            int n = r.ReadInt();
            int expected = 0;
            foreach (Account a in Accounts.All) if (Accounts.SinceSchema(a) <= r.Schema) expected++; // accounts added after the save's schema aren't in it
            if (n != expected) throw new SaveFormatException(n + " accounts, schema " + r.Schema + " has " + expected);
            for (int i = 0; i < l._balances.Length; i++)
                if (Accounts.SinceSchema(Accounts.All[i]) <= r.Schema) l._balances[i] = r.ReadLong();
            n = r.ReadCount();
            for (int i = 0; i < n; i++)
            {
                long id = r.ReadLong(), t = r.ReadLong(), period = r.ReadLong();
                string key = r.ReadString(), reference = r.ReadString();
                var postings = new Posting[r.ReadCount()];
                for (int k = 0; k < postings.Length; k++)
                {
                    var a = (Account)r.ReadInt();
                    postings[k] = new Posting(a, r.ReadLong());
                }
                l._entries.Add(new JournalEntry(id, t, period, key, reference, postings));
            }
            return l;
        }
    }
}
