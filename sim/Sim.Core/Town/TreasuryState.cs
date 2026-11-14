using System.Collections.Generic;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>The budget's window (spec 021 §3.6): the set window at the opening and each Spring close, one revision at the Monsoon close.</summary>
    public enum BudgetWindow
    {
        None = 0,
        Set = 1,
        Revise = 2,
    }

    /// <summary>One department's row of a closed season (spec 021 §3.6): what it was budgeted, what it needed and what it was paid.</summary>
    public sealed class DepartmentRow
    {
        public long Budget { get; internal set; }
        public long Needs { get; internal set; }
        public long Paid { get; internal set; }

        /// <summary>Of <see cref="Paid"/>, what pass 2 paid: the overspend (spec 021 §3.4).</summary>
        public long PaidInPass2 { get; internal set; }

        /// <summary>The service's condition after the close.</summary>
        public Fixed Condition { get; internal set; }

        /// <summary>The variance sentence (spec 021 §3.6), a <see cref="VarianceSentence"/>.</summary>
        public int Sentence { get; internal set; }

        public long Unpaid { get { return Needs - Paid; } }

        /// <summary>Paid ÷ needs, 1 when nothing was needed (spec 021 §3.4).</summary>
        public Fixed Funding { get { return Needs <= 0 ? Fixed.One : Fixed.FromRatio(Paid, Needs); } }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Budget); h.WriteLong(Needs); h.WriteLong(Paid); h.WriteLong(PaidInPass2); h.WriteFixed(Condition); h.WriteInt(Sentence);
        }

        internal static DepartmentRow ReadFrom(SaveReader r)
        {
            return new DepartmentRow { Budget = r.ReadLong(), Needs = r.ReadLong(), Paid = r.ReadLong(), PaidInPass2 = r.ReadLong(), Condition = r.ReadFixed(), Sentence = r.ReadInt() };
        }
    }

    /// <summary>
    /// A closed season of the town's books (spec 021 §3.2, §3.6): every account's balance before the close (the money in and out and what
    /// the town has), the cash that moved, and each department's row. Immutable once added.
    /// </summary>
    public sealed class TreasuryReport
    {
        public long Period { get; internal set; }
        public IReadOnlyList<KeyValuePair<Account, long>> Balances { get; internal set; }
        public long Opening { get; internal set; }
        public long Inflows { get; internal set; }
        public long Outflows { get; internal set; }
        public long Closing { get { return Opening + Inflows - Outflows; } }
        public IReadOnlyList<DepartmentRow> Rows { get; internal set; }

        /// <summary>What the clinic's tied grant would have been but wasn't paid, in hundredths (0 when paid or when there is no clinic yet).</summary>
        public long TiedWithheld { get; internal set; }

        public long Balance(Account a)
        {
            foreach (KeyValuePair<Account, long> kv in Balances) if (kv.Key == a) return kv.Value;
            return 0;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Period);
            h.WriteInt(Balances.Count);
            foreach (KeyValuePair<Account, long> kv in Balances) { h.WriteInt((int)kv.Key); h.WriteLong(kv.Value); }
            h.WriteLong(Opening); h.WriteLong(Inflows); h.WriteLong(Outflows); h.WriteLong(TiedWithheld);
            h.WriteInt(Rows.Count);
            foreach (DepartmentRow row in Rows) row.WriteTo(h);
        }

        internal static TreasuryReport ReadFrom(SaveReader r)
        {
            var report = new TreasuryReport { Period = r.ReadLong() };
            var balances = new List<KeyValuePair<Account, long>>();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) balances.Add(new KeyValuePair<Account, long>((Account)r.ReadInt(), r.ReadLong()));
            report.Balances = balances.AsReadOnly();
            report.Opening = r.ReadLong();
            report.Inflows = r.ReadLong();
            report.Outflows = r.ReadLong();
            report.TiedWithheld = r.ReadLong();
            var rows = new List<DepartmentRow>();
            n = r.ReadCount();
            for (int i = 0; i < n; i++) rows.Add(DepartmentRow.ReadFrom(r));
            report.Rows = rows.AsReadOnly();
            return report;
        }
    }

    /// <summary>A capital work the treasury commissioned (spec 021 §3.7): held at cost and depreciated straight-line at each close.</summary>
    public sealed class WorkRecord
    {
        public string WorkId { get; internal set; }
        public long Cost { get; internal set; }
        public int LifeSeasons { get; internal set; }
        public int SeasonsDepreciated { get; internal set; }
        public long Depreciated { get; internal set; }
        public long At { get; internal set; }

        internal WorkRecord Clone() { return (WorkRecord)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteString(WorkId); h.WriteLong(Cost); h.WriteInt(LifeSeasons); h.WriteInt(SeasonsDepreciated); h.WriteLong(Depreciated); h.WriteLong(At);
        }

        internal static WorkRecord ReadFrom(SaveReader r)
        {
            return new WorkRecord { WorkId = r.ReadString(), Cost = r.ReadLong(), LifeSeasons = r.ReadInt(), SeasonsDepreciated = r.ReadInt(), Depreciated = r.ReadLong(), At = r.ReadLong() };
        }
    }

    /// <summary>
    /// The town treasury (spec 021 §4, ADR 0108): a second wallet with its own ledger. It exists in a town whose content has
    /// <c>treasury.json</c>, but hashes only once it has opened, so a town before the Township hashes as it did.
    /// </summary>
    public sealed class TreasuryState
    {
        public const int Departments = 7;

        /// <summary>When the Township opened it; -1 until it does.</summary>
        public long OpenedAt { get; internal set; } = -1;
        public bool Opened { get { return OpenedAt >= 0; } }

        public Ledger Ledger { get; internal set; } = new Ledger();

        /// <summary>The most each department may spend a season, set for the year (spec 021 §3.6).</summary>
        public long[] Budget { get; internal set; } = new long[Departments];

        /// <summary>Each department's service, 0–1, starting at 1 (spec 021 §3.5).</summary>
        public Fixed[] Condition { get; internal set; } = NewConditions();

        /// <summary>Closes a department has gone unpaid in a row, for the advisor and the doctor's rule (spec 021 §4).</summary>
        public int[] Unpaid { get; internal set; } = new int[Departments];

        /// <summary>Closes a department has been over its budget in a row (the planner's note).</summary>
        public int[] OverBudget { get; internal set; } = new int[Departments];

        /// <summary>Which of the content's lines have appeared, so a new line is added to its budget once (spec 021 §3.6).</summary>
        public bool[] LineSeen { get; internal set; } = new bool[0];

        public BudgetWindow Window { get; internal set; }

        /// <summary>The season the window opened in, and whether the player has looked at it (the proposal is in force either way).</summary>
        public long WindowPeriod { get; internal set; }
        public bool WindowSeen { get; internal set; }
        public int YearBudgeted { get; internal set; }
        public bool Revised { get; internal set; }

        /// <summary>The warning of a close that would leave a department unpaid was raised for this season (spec 021 §3.11).</summary>
        public long ShortWarnedPeriod { get; internal set; } = -1;
        public long ShortWarnedAmount { get; internal set; }

        public long SeasonOpeningCash { get; internal set; }
        public List<WorkRecord> Works { get; internal set; } = new List<WorkRecord>();
        public List<TreasuryReport> Reports { get; internal set; } = new List<TreasuryReport>();

        public long Cash { get { return Ledger.Cash; } }

        /// <summary>
        /// Inside a season close, before the treasury's own pass: pitch rents paid at the boundary are for the new season, so they wait and are
        /// credited after the closing season's books are shut (spec 021 §3.3, §3.10). Never saved: they last one pass.
        /// </summary>
        internal bool Closing { get; set; }
        internal List<KeyValuePair<long, long>> PendingRents { get; set; } = new List<KeyValuePair<long, long>>();

        private static Fixed[] NewConditions()
        {
            var c = new Fixed[Departments];
            for (int i = 0; i < c.Length; i++) c[i] = Fixed.One;
            return c;
        }

        internal TreasuryState Clone()
        {
            var c = (TreasuryState)MemberwiseClone();
            c.Ledger = Ledger.Clone();
            c.Budget = (long[])Budget.Clone();
            c.Condition = (Fixed[])Condition.Clone();
            c.Unpaid = (int[])Unpaid.Clone();
            c.OverBudget = (int[])OverBudget.Clone();
            c.LineSeen = (bool[])LineSeen.Clone();
            c.Works = Works.ConvertAll(x => x.Clone());
            c.PendingRents = new List<KeyValuePair<long, long>>(PendingRents);
            c.Reports = new List<TreasuryReport>(Reports); // reports are immutable
            return c;
        }

        /// <summary>A save writes the opening time always; the hash writes nothing until the treasury opens (the goldens are unchanged).</summary>
        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && !Opened) return;
            h.WriteLong(OpenedAt);
            if (!Opened) return;
            Ledger.WriteTo(h);
            foreach (long b in Budget) h.WriteLong(b);
            foreach (Fixed c in Condition) h.WriteFixed(c);
            foreach (int u in Unpaid) h.WriteInt(u);
            foreach (int u in OverBudget) h.WriteInt(u);
            h.WriteInt(LineSeen.Length);
            foreach (bool b in LineSeen) h.WriteBool(b);
            h.WriteInt((int)Window);
            h.WriteLong(WindowPeriod);
            h.WriteBool(WindowSeen);
            h.WriteInt(YearBudgeted);
            h.WriteBool(Revised);
            h.WriteLong(ShortWarnedPeriod);
            h.WriteLong(ShortWarnedAmount);
            h.WriteLong(SeasonOpeningCash);
            h.WriteInt(Works.Count);
            foreach (WorkRecord w in Works) w.WriteTo(h);
            h.WriteInt(Reports.Count);
            foreach (TreasuryReport r in Reports) r.WriteTo(h);
        }

        internal static TreasuryState ReadFrom(SaveReader r)
        {
            var s = new TreasuryState { OpenedAt = r.ReadLong() };
            if (!s.Opened) return s;
            s.Ledger = Ledger.ReadFrom(r);
            for (int i = 0; i < Departments; i++) s.Budget[i] = r.ReadLong();
            for (int i = 0; i < Departments; i++) s.Condition[i] = r.ReadFixed();
            for (int i = 0; i < Departments; i++) s.Unpaid[i] = r.ReadInt();
            for (int i = 0; i < Departments; i++) s.OverBudget[i] = r.ReadInt();
            s.LineSeen = new bool[r.ReadCount()];
            for (int i = 0; i < s.LineSeen.Length; i++) s.LineSeen[i] = r.ReadBool();
            s.Window = (BudgetWindow)r.ReadInt();
            s.WindowPeriod = r.ReadLong();
            s.WindowSeen = r.ReadBool();
            s.YearBudgeted = r.ReadInt();
            s.Revised = r.ReadBool();
            s.ShortWarnedPeriod = r.ReadLong();
            s.ShortWarnedAmount = r.ReadLong();
            s.SeasonOpeningCash = r.ReadLong();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Works.Add(WorkRecord.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Reports.Add(TreasuryReport.ReadFrom(r));
            return s;
        }
    }

    /// <summary>The five sentences a department's variance can take (spec 021 §3.6).</summary>
    public enum VarianceSentence
    {
        None = 0,
        PricesRose = 1,
        NewLine = 2,
        CashRanOut = 3,
        BudgetBelowNeeds = 4,
        Unspent = 5,
    }
}
