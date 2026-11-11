using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Finance;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// The season page (spec 019 §3.1, ADR 0101 §5): Profit and loss | Cash flow | Balance sheet as a business's books, each line beside the same
    /// season last year, every row with a <em>Why?</em> that lists what is behind it. It opens at each close, from <em>Last season</em> and from
    /// <em>Your books</em>.
    /// </summary>
    public static class StatementSheet
    {
        // Why? keys of the page's rows (built so the string-table scan, which reads dotted literals, doesn't take them for strings).
        private const string Prefix = "report.";
        public const string AcctWhy = Prefix + "acct", CashRowWhy = Prefix + "cashrow", BalanceWhy = Prefix + "bs", PeopleWhy = Prefix + "people", YearWhy = Prefix + "year";

        public const string ProfitTab = "pl", CashTab = "cash", BalanceTab = "balance";

        /// <summary>The balance sheet's rows, in order: what you own, what you owe, then the totals.</summary>
        internal static readonly string[] OwnKeys = { "cash", "deposit", "prepaid", "owed", "goods", "land", "buildings", "trees", "livestock", "equipment", "businesses" };
        internal static readonly string[] OweKeys = { "loans", "interest" };

        /// <summary>A why id for a row: the season and the row's place in its page.</summary>
        public static long WhyId(long period, int index) { return period * 1000 + index; }
        internal static long PeriodOf(long whyId) { return whyId / 1000; }
        internal static int IndexOf(long whyId) { return (int)(whyId % 1000); }

        public static string SeasonName(ViewContext ctx, long period) { return FieldSheets.SeasonName(ctx, period); }

        public static SeasonReport Find(Simulation sim, long period) { return sim.State.Finance.Reports.Find(x => x.Period == period); }

        /// <summary>The same season a year before (four seasons back), or null in the first year.</summary>
        public static SeasonReport LastYear(Simulation sim, long period) { return Find(sim, period - 4); }

        public static string Amount(ViewContext ctx, long v) { return v < 0 ? "−" + ctx.Fmt.Coins(-v) : ctx.Fmt.Coins(v); }

        /// <summary>"last Winter: 31,900", or "First Winter" before a year has passed.</summary>
        internal static string Beside(ViewContext ctx, long period, SeasonReport prev, long then)
        {
            string season = FieldSheets.SeasonName(ctx, period);
            return prev == null ? ctx.Strings.Get("stmt.first", "season", season) : ctx.Strings.Get("stmt.last", "season", season, "amount", Amount(ctx, then));
        }

        internal static long AccountAmount(SeasonReport r, Account a)
        {
            if (r == null) return 0;
            foreach (KeyValuePair<Account, long> kv in r.ByAccount) if (kv.Key == a) return kv.Value;
            return 0;
        }

        internal static long CashAmount(SeasonReport r, CashRow row)
        {
            if (r == null || !r.CashKept) return 0;
            foreach (CashRow x in r.CashRows) if (x.Account == row.Account && x.In == row.In) return x.Amount;
            return 0;
        }

        public static SheetVm Build(ViewContext ctx, long period, string tab = null)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            SeasonReport r = Find(sim, period) ?? sim.State.Finance.Reports[sim.State.Finance.Reports.Count - 1];
            period = r.Period;
            tab = tab == CashTab || tab == BalanceTab ? tab : ProfitTab;
            var vm = new SheetVm { Icon = "coin", Title = s.Get("report.title", "period", ReportSheet.PeriodName(ctx, period)), Subtitle = s.Get("stmt.subtitle") };
            vm.Tab = "tab:" + tab;
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:" + ProfitTab, s.Get("stmt.tab.pl"), new SheetRequest("Report", period, ProfitTab)),
                ActionVm.Open("tab:" + CashTab, s.Get("stmt.tab.cash"), new SheetRequest("Report", period, CashTab)),
                ActionVm.Open("tab:" + BalanceTab, s.Get("stmt.tab.balance"), new SheetRequest("Report", period, BalanceTab)),
            };
            if (tab == CashTab) Cash(ctx, vm, r);
            else if (tab == BalanceTab) Balance(ctx, vm, r);
            else Profit(ctx, vm, r);
            return vm;
        }

        // ---- profit and loss ----

        private static void Profit(ViewContext ctx, SheetVm vm, SeasonReport r)
        {
            var s = ctx.Strings;
            SeasonReport prev = LastYear(ctx.Sim, r.Period);
            LineVm advisor = AdvisorWords.Report(ctx, r.Period); // an advisor's line under the title (spec 018 §3.4)
            if (advisor != null) vm.Lines.Add(advisor);
            foreach (string key in ReportSheet.Order)
            {
                ReportLine l = r.Line(key);
                if (l == null || (key == "gifts" && l.Amount == 0)) continue;
                ReportLine before = prev?.Line(key);
                if (l.Accounts.Count == 0 || key == "farmProfit") // a result: profit of the farm, what's left
                {
                    vm.Lines.Add(new LineVm(s.Get(l.LabelKey), Amount(ctx, l.Amount), "report." + key, r.Period) { Note = Beside(ctx, r.Period, prev, before?.Amount ?? 0), Icon = "coin" });
                    continue;
                }
                vm.Lines.Add(LineVm.Header(s.Get("season.head", "head", s.Get(l.LabelKey), "amount", Amount(ctx, l.Amount))));
                foreach (Account a in l.Accounts)
                {
                    long now = AccountAmount(r, a);
                    if (now == 0) continue;
                    vm.Lines.Add(new LineVm(s.Get("account." + a), Amount(ctx, now), StatementSheet.AcctWhy, r.Period * 10000 + (int)a) { Note = Beside(ctx, r.Period, prev, AccountAmount(prev, a)) });
                }
            }
            People(ctx, vm, r);
            if (YearSheet.EndsYear(r.Period))
                vm.Lines.Add(new LineVm(s.Get("stmt.seeYear"), s.Get("stmt.seeYearNote")) { Icon = "coin", Action = ActionVm.Open("year", s.Get("action.open"), new SheetRequest("Year", r.Period)) });
        }

        private static void People(ViewContext ctx, SheetVm vm, SeasonReport r)
        {
            var s = ctx.Strings;
            if (r.People.Count == 0 && r.ReputationBefore == 0 && r.ReputationAfter == 0) return;
            vm.Lines.Add(LineVm.Header(s.Get("stmt.people")));
            for (int i = 0; i < r.People.Count; i++)
            {
                PeopleRow p = r.People[i];
                string name = PeopleWords.Name(ctx, p.Name);
                if (p.Place != 0) name = s.Get("stmt.people.at", "name", name, "place", PlaceName(ctx, p.Place));
                bool gone = p.Outcome == StaffOutcome.LeftForElsewhere || p.Outcome == StaffOutcome.LeftUnpaid || p.Outcome == StaffOutcome.Retired;
                var line = new LineVm(name, gone ? s.Get("stmt.people.outcome." + (int)p.Outcome) : s.Get("stmt.people.paid", "paid", ctx.Fmt.Coins(p.Paid), "asked", ctx.Fmt.Coins(p.Asked)),
                    StatementSheet.PeopleWhy, WhyId(r.Period, i)) { Icon = PeopleWords.Female(ctx, p.Name) ? "person_hand_f" : "person_hand_m" };
                if (!gone)
                {
                    line.Stars = PeopleWords.Stars(ctx, p.Skill, p.Skill);
                    if (p.Outcome != StaffOutcome.Stayed) { line.Note = s.Get("stmt.people.outcome." + (int)p.Outcome); line.NoteTone = Tone.Warn; }
                }
                vm.Lines.Add(line);
            }
            if (r.People.Count == 0) vm.Lines.Add(new LineVm(s.Get("stmt.people.none"), null));
            int change = r.ReputationAfter - r.ReputationBefore;
            vm.Lines.Add(new LineVm(s.Get("stmt.people.rep"), s.Get(change == 0 ? "stmt.people.repSame" : change > 0 ? "stmt.people.repUp" : "stmt.people.repDown",
                "rep", ctx.Fmt.Integer(r.ReputationAfter), "change", ctx.Fmt.Integer(System.Math.Abs(change))), StatementSheet.PeopleWhy, WhyId(r.Period, 999)));
        }

        internal static string PlaceName(ViewContext ctx, long place)
        {
            Sim.Core.Business.Business b = ctx.Sim.State.Businesses?.Find(place);
            return b == null ? ctx.Strings.Get("stmt.people.closedBusiness") : BusinessWords.Name(ctx, Sim.Core.Business.BusinessRules.TypeOf(ctx.Sim, b));
        }

        // ---- cash flow ----

        private static void Cash(ViewContext ctx, SheetVm vm, SeasonReport r)
        {
            var s = ctx.Strings;
            SeasonReport prev = LastYear(ctx.Sim, r.Period);
            vm.Lines.Add(new LineVm(s.Get("stmt.cash.start"), ctx.Fmt.Coins(r.CashFlow.Opening), "report.cashFlow", r.Period) { Icon = "coin", Note = Beside(ctx, r.Period, prev, prev?.CashFlow.Opening ?? 0) });
            if (!r.CashKept)
            {
                vm.Lines.Add(new LineVm(s.Get("stmt.cash.notKept"), s.Get("stmt.cash.notKeptNote"), null) { NoteTone = Tone.Warn });
                vm.Lines.Add(new LineVm(s.Get("stmt.cash.end"), ctx.Fmt.Coins(r.CashFlow.Closing), "report.cashFlow", r.Period) { Icon = "coin" });
                return;
            }
            for (int g = 0; g <= (int)CashGroup.Borrowing; g++)
            {
                long total = 0;
                var group = new List<LineVm>();
                for (int i = 0; i < r.CashRows.Count; i++)
                {
                    CashRow row = r.CashRows[i];
                    if ((int)row.Group != g) continue;
                    total += row.Amount;
                    group.Add(new LineVm(s.Get(row.LabelKey), Amount(ctx, row.Amount), StatementSheet.CashRowWhy, WhyId(r.Period, i)) { Note = Beside(ctx, r.Period, prev, prev != null && prev.CashKept ? CashAmount(prev, row) : 0) });
                }
                if (group.Count == 0) continue;
                vm.Lines.Add(LineVm.Header(s.Get("season.head", "head", s.Get("stmt.group." + g), "amount", Amount(ctx, total))));
                vm.Lines.AddRange(group);
            }
            long moved = r.CashFlow.Closing - r.CashFlow.Opening;
            vm.Lines.Add(new LineVm(s.Get("stmt.cash.moved"), Amount(ctx, moved), "report.cashFlow", r.Period) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("stmt.cash.end"), ctx.Fmt.Coins(r.CashFlow.Closing), "report.cashFlow", r.Period) { Icon = "coin", Note = s.Get("stmt.cash.adds"), NoteTone = Tone.Good });
        }

        // ---- balance sheet ----

        /// <summary>What a balance-sheet key counts, from a season's net worth.</summary>
        public static long Item(NetWorthSnapshot n, string key)
        {
            switch (key)
            {
                case "cash": return n.Cash;
                case "deposit": return n.Deposit;
                case "prepaid": return n.Prepaid;
                case "owed": return n.OwedByBuyers;
                case "goods": return n.StoredGoods;
                case "land": return n.LandAtMarket;
                case "buildings": return n.BuildingsAtBook;
                case "trees": return n.TreesAtBook;
                case "livestock": return n.LivestockAtBook;
                case "equipment": return n.EquipmentAtBook;
                case "businesses": return n.Businesses;
                case "loans": return n.LoanPrincipal;
                case "interest": return n.InterestPayable;
                case "own": return n.Total + n.LoanPrincipal + n.InterestPayable;
                case "owe": return n.LoanPrincipal + n.InterestPayable;
                default: return n.Total;
            }
        }

        /// <summary>The balance sheet's key at a why index: the rows in order, then own, owe and net worth.</summary>
        internal static string KeyAt(int index)
        {
            if (index < OwnKeys.Length) return OwnKeys[index];
            index -= OwnKeys.Length;
            if (index < OweKeys.Length) return OweKeys[index];
            index -= OweKeys.Length;
            return index == 0 ? "own" : index == 1 ? "owe" : "worth";
        }

        private static void Balance(ViewContext ctx, SheetVm vm, SeasonReport r)
        {
            var s = ctx.Strings;
            SeasonReport prev = LastYear(ctx.Sim, r.Period);
            LineVm Row(string key, bool owe)
            {
                long now = Item(r.NetWorth, key);
                return new LineVm(s.Get("stmt.bs." + key), owe ? Amount(ctx, -now) : Amount(ctx, now), StatementSheet.BalanceWhy, WhyId(r.Period, System.Array.IndexOf(AllKeys, key)))
                    { Note = Beside(ctx, r.Period, prev, prev == null ? 0 : owe ? -Item(prev.NetWorth, key) : Item(prev.NetWorth, key)) };
            }
            vm.Lines.Add(LineVm.Header(s.Get("season.head", "head", s.Get("stmt.bs.own"), "amount", ctx.Fmt.Coins(Item(r.NetWorth, "own")))));
            foreach (string key in OwnKeys)
            {
                if (Item(r.NetWorth, key) == 0 && key != "cash") continue;
                vm.Lines.Add(Row(key, false));
            }
            vm.Lines.Add(LineVm.Header(s.Get("season.head", "head", s.Get("stmt.bs.owe"), "amount", Amount(ctx, -Item(r.NetWorth, "owe")))));
            foreach (string key in OweKeys)
                if (Item(r.NetWorth, key) != 0) vm.Lines.Add(Row(key, true));
            if (Item(r.NetWorth, "owe") == 0) vm.Lines.Add(new LineVm(s.Get("stmt.bs.noDebt"), null));
            LineVm worth = Row("worth", false);
            worth = new LineVm(worth.Label, worth.Value, "report.netWorth", r.Period) { Note = worth.Note, Icon = "coin" };
            vm.Lines.Add(worth);
            vm.Lines.Add(new LineVm(s.Get("stmt.bs.note"), null));
        }

        internal static readonly string[] AllKeys = BuildAllKeys();

        private static string[] BuildAllKeys()
        {
            var all = new List<string>(OwnKeys);
            all.AddRange(OweKeys);
            all.Add("own"); all.Add("owe"); all.Add("worth");
            return all.ToArray();
        }
    }

    /// <summary>
    /// <em>Your year</em> (spec 019 §3.7, ADR 0101 §5): opens after the season page at each Winter close. The year's farm profit against last year's,
    /// net worth's change, the best and worst season, the biggest cost line, and the accountant's one line.
    /// </summary>
    public static class YearSheet
    {
        public const int FarmProfit = 0, NetWorth = 1, Best = 2, Worst = 3, BiggestCost = 4;

        /// <summary>A Winter close ends a year (spring is season 0).</summary>
        public static bool EndsYear(long period) { return period % 4 == 3; }

        /// <summary>The closed seasons of the year that ends at <paramref name="winter"/>, oldest first.</summary>
        public static List<SeasonReport> Seasons(Simulation sim, long winter)
        {
            var list = new List<SeasonReport>();
            for (long p = winter - 3; p <= winter; p++)
            {
                SeasonReport r = StatementSheet.Find(sim, p);
                if (r != null) list.Add(r);
            }
            return list;
        }

        internal static long Sum(List<SeasonReport> year, System.Func<SeasonReport, long> f)
        {
            long t = 0;
            foreach (SeasonReport r in year) t += f(r);
            return t;
        }

        /// <summary>The expense account that cost the most over the year, and what it cost (positive); none: Cash.</summary>
        internal static Account BiggestCostOf(List<SeasonReport> year, out long cost)
        {
            Account best = Account.Cash;
            cost = 0;
            foreach (Account a in Accounts.All)
            {
                if (Accounts.GroupOf(a) != AccountGroup.Expenses) continue;
                long c = -Sum(year, r => StatementSheet.AccountAmount(r, a));
                if (c > cost) { cost = c; best = a; }
            }
            return best;
        }

        public static SheetVm Build(ViewContext ctx, long winter)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            List<SeasonReport> year = Seasons(sim, winter);
            var vm = new SheetVm { Icon = "coin", Title = s.Get("year.title", "year", ctx.Fmt.Integer(winter / 4 + 1)), Subtitle = s.Get("year.subtitle") };
            if (year.Count == 0) return vm;
            List<SeasonReport> before = Seasons(sim, winter - 4);
            bool hadLastYear = before.Count == 4;

            long profit = Sum(year, r => r.FarmProfit);
            LineVm line = new LineVm(s.Get("year.farmProfit"), StatementSheet.Amount(ctx, profit), StatementSheet.YearWhy, StatementSheet.WhyId(winter, FarmProfit)) { Icon = "field" };
            line.Note = hadLastYear ? s.Get("year.lastYear", "amount", StatementSheet.Amount(ctx, Sum(before, r => r.FarmProfit))) : s.Get("year.firstYear");
            vm.Lines.Add(line);

            SeasonReport end = year[year.Count - 1];
            SeasonReport start = StatementSheet.Find(sim, winter - 4);
            long worth = end.NetWorth.Total;
            vm.Lines.Add(new LineVm(s.Get("year.netWorth"), StatementSheet.Amount(ctx, worth), StatementSheet.YearWhy, StatementSheet.WhyId(winter, NetWorth))
                { Icon = "coin", Note = start == null ? s.Get("year.firstYear") : s.Get("year.netWorthChange", "start", ctx.Fmt.Coins(start.NetWorth.Total), "change", MoneySheet.Signed(ctx, worth - start.NetWorth.Total)) });

            SeasonReport best = year[0], worst = year[0];
            foreach (SeasonReport r in year)
            {
                if (r.NetResult > best.NetResult) best = r;
                if (r.NetResult < worst.NetResult) worst = r;
            }
            if (year.Count > 1)
            {
                vm.Lines.Add(new LineVm(s.Get("year.best"), FieldSheets.SeasonName(ctx, best.Period), StatementSheet.YearWhy, StatementSheet.WhyId(winter, Best)) { Note = s.Get("year.profitOf", "amount", StatementSheet.Amount(ctx, best.NetResult)) });
                vm.Lines.Add(new LineVm(s.Get("year.worst"), FieldSheets.SeasonName(ctx, worst.Period), StatementSheet.YearWhy, StatementSheet.WhyId(winter, Worst)) { Note = s.Get("year.profitOf", "amount", StatementSheet.Amount(ctx, worst.NetResult)) });
            }
            Account top = BiggestCostOf(year, out long cost);
            if (cost > 0)
                vm.Lines.Add(new LineVm(s.Get("year.biggestCost"), s.Get("account." + top), StatementSheet.YearWhy, StatementSheet.WhyId(winter, BiggestCost)) { Note = ctx.Fmt.Coins(cost) });

            LineVm advisor = AdvisorWords.Report(ctx, winter); // the accountant's one line on what to try next
            if (advisor != null) vm.Lines.Add(advisor);
            return vm;
        }
    }
}
