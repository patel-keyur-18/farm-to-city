using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;

namespace Sim.View.Views
{
    /// <summary>
    /// A business's books (spec 016 §3.2, §7): the Books tab (this season's P&amp;L, cash in and out, what it owns and owes, past seasons,
    /// plan vs actual), a past season's report, and putting cash in or taking it out. Every number has a *Why?*.
    /// </summary>
    public static class BusinessBooksSheet
    {
        // The lines of a statement, for the *Why?* id.
        public const int Fees = 0, Sales = 1, Goods = 2, Margin = 3, Wages = 4, Power = 5, Repairs = 6, Depreciation = 7, Interest = 8, Profit = 9,
            CashStart = 10, CashIn = 11, CashOut = 12, CashEnd = 13, Cash = 14, Stock = 15, Fixed = 16, Debts = 17, Capital = 18, Kept = 19, Worth = 20;

        internal static readonly string[] Names =
        {
            "fees", "sales", "goods", "margin", "wages", "power", "repairs", "depreciation", "interest", "profit",
            "cashStart", "cashIn", "cashOut", "cashEnd", "cash", "stock", "fixed", "debts", "capital", "kept", "worth",
        };

        private static readonly int[] PlanLines = { Fees, Sales, Goods, Wages, Power, Repairs, Depreciation, Interest, Profit };

        /// <summary>A line's *Why?* id: the business, how many seasons back (0 = this season so far), the line.</summary>
        public static long WhyId(Business b, int back, int line) { return b.Id * 100_000 + back * 100 + line; }

        internal static Business BusinessOf(ViewContext ctx, long id, out int back, out int line)
        {
            back = (int)(id / 100 % 1000);
            line = (int)(id % 100);
            return ctx.Sim.State.Businesses?.Find(id / 100_000);
        }

        /// <summary>This season so far (0), or the closed season <paramref name="back"/> seasons ago.</summary>
        internal static BusinessReport ReportOf(ViewContext ctx, Business b, int back)
        {
            return back == 0 || back > b.Reports.Count ? BusinessBooks.Report(b, ctx.Sim.Period) : b.Reports[b.Reports.Count - back];
        }

        /// <summary>A line's amount on a report: income and costs as positive amounts, profit signed.</summary>
        public static long Amount(BusinessReport r, int line)
        {
            switch (line)
            {
                case Fees: return -r.Balance(Account.ServiceIncome);
                case Sales: return -r.Balance(Account.Sales);
                case Goods: return r.Balance(Account.CostOfGoodsSold);
                case Margin: return Amount(r, Fees) + Amount(r, Sales) - Amount(r, Goods);
                case Wages: return r.Balance(Account.HiredLabour);
                case Power: return r.Balance(Account.Power);
                case Repairs: return r.Balance(Account.Repairs);
                case Depreciation: return r.Balance(Account.Depreciation);
                case Interest: return r.Balance(Account.Interest);
                case Profit: return BusinessBooks.NetProfit(r);
                case CashStart: return r.Opening;
                case CashIn: return r.Inflows;
                case CashOut: return r.Outflows;
                case CashEnd: return r.Closing;
                case Cash: return r.Balance(Account.Cash);
                case Stock: return r.Balance(Account.BusinessStock);
                case Fixed: return r.Balance(Account.Buildings) + r.Balance(Account.Equipment) + r.Balance(Account.AccumulatedDepreciation);
                case Debts: return Total(r, AccountGroup.Liabilities);
                case Capital: return -r.Balance(Account.OwnersCapital);
                case Kept: return -r.Balance(Account.RetainedEarnings) + BusinessBooks.NetProfit(r);
                default: return Total(r, AccountGroup.Assets) - Total(r, AccountGroup.Liabilities);
            }
        }

        private static long Total(BusinessReport r, AccountGroup g)
        {
            long sum = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances)
                if (Accounts.GroupOf(kv.Key) == g) sum += kv.Value;
            return g == AccountGroup.Assets ? sum : -sum;
        }

        public static string Label(ViewContext ctx, Business b, int line)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            return ctx.Strings.Get(BusinessWords.Key(ctx, "books." + Names[line], t), "good", t.ByRecipe ? "" : BusinessWords.Good(ctx, t.Input));
        }

        private static bool IsCost(int line) { return line == Goods || (line >= Wages && line <= Interest) || line == CashOut || line == Debts; }

        private static string Value(ViewContext ctx, int line, long amount) { return (IsCost(line) && amount != 0 ? "−" : "") + ctx.Fmt.Coins(amount); }

        private static void Add(ViewContext ctx, SheetVm vm, Business b, BusinessReport r, int back, int line)
        {
            long amount = Amount(r, line);
            var l = new LineVm(Label(ctx, b, line), Value(ctx, line, amount), "booksLine", WhyId(b, back, line));
            if (line == Profit || line == Worth) l.NoteTone = amount > 0 ? Tone.Good : Tone.Warn;
            vm.Lines.Add(l);
        }

        /// <summary>The three statements of a report: profit and loss, cash in and out, what it owns and owes.</summary>
        internal static void AddStatements(ViewContext ctx, SheetVm vm, Business b, BusinessReport r, int back)
        {
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get(back == 0 ? "books.pnlNow" : "books.pnl")));
            bool recipe = BusinessRules.TypeOf(ctx.Sim, b).ByRecipe; // a bakery or dairy charges no fees
            foreach (int line in new[] { Fees, Sales, Goods, Margin })
                if (line != Fees || !recipe) Add(ctx, vm, b, r, back, line);
            if (Amount(r, Wages) != 0 || b.Worker != WorkerKind.Founder) Add(ctx, vm, b, r, back, Wages);
            foreach (int line in new[] { Power, Repairs, Depreciation })
                Add(ctx, vm, b, r, back, line);
            if (Amount(r, Interest) != 0) Add(ctx, vm, b, r, back, Interest);
            Add(ctx, vm, b, r, back, Profit);
            vm.Lines.Add(LineVm.Header(s.Get(back == 0 ? "books.cashFlowNow" : "books.cashFlow")));
            foreach (int line in new[] { CashStart, CashIn, CashOut, CashEnd })
                Add(ctx, vm, b, r, back, line);
            vm.Lines.Add(LineVm.Header(s.Get(back == 0 ? "books.balanceNow" : "books.balance")));
            foreach (int line in new[] { Cash, Stock, Fixed })
                Add(ctx, vm, b, r, back, line);
            if (Amount(r, Debts) != 0) Add(ctx, vm, b, r, back, Debts);
            foreach (int line in new[] { Capital, Kept, Worth })
                Add(ctx, vm, b, r, back, line);
        }

        /// <summary>The Books tab: this season so far, past seasons (each opens its report), plan vs actual.</summary>
        internal static void AddBooks(ViewContext ctx, SheetVm vm, Business b)
        {
            var s = ctx.Strings;
            AddStatements(ctx, vm, b, ReportOf(ctx, b, 0), 0);
            if (b.Reports.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("books.past")));
                for (int back = 1; back <= b.Reports.Count; back++)
                {
                    BusinessReport r = b.Reports[b.Reports.Count - back];
                    long profit = BusinessBooks.NetProfit(r);
                    vm.Lines.Add(new LineVm(ReportSheet.PeriodName(ctx, r.Period), s.Get("books.pastValue", "profit", ctx.Fmt.Coins(profit)), "booksLine", WhyId(b, back, Profit))
                    { Action = ActionVm.Open("season:" + back, s.Get("money.seeReport"), new SheetRequest("BusinessReport", b.Id, back.ToString(System.Globalization.CultureInfo.InvariantCulture))) });
                }
            }
            AddPlanVsActual(ctx, vm, b);
        }

        /// <summary>Full seasons closed since it was founded (the founding season is part of one).</summary>
        public static int FullSeasons(Simulation sim, Business b)
        {
            long founded = b.FoundedAt / sim.Clock.SecondsPerSeason;
            int n = 0;
            foreach (BusinessReport r in b.Reports)
                if (r.Period > founded) n++;
            return n;
        }

        /// <summary>
        /// Plan vs actual (spec 016 §3.3, AC-14): after 4 full seasons, each line's average a season against the plan made at founding,
        /// and in words why they differ.
        /// </summary>
        private static void AddPlanVsActual(ViewContext ctx, SheetVm vm, Business b)
        {
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get("books.pva")));
            int full = FullSeasons(ctx.Sim, b);
            if (full < BusinessBooks.KeptReports)
            {
                vm.Lines.Add(new LineVm(s.Get("books.pvaWait", "n", ctx.Fmt.Integer(BusinessBooks.KeptReports)),
                    s.Get("books.pvaSoFar", "n", ctx.Fmt.Integer(full), "of", ctx.Fmt.Integer(BusinessBooks.KeptReports)), "booksPlan", WhyId(b, 0, Profit)));
                return;
            }
            bool recipe = BusinessRules.TypeOf(ctx.Sim, b).ByRecipe;
            foreach (int line in PlanLines)
            {
                if (line == Fees && recipe) continue;
                long plan = Planned(b.Plan, line), actual = Average(b, line);
                if ((line == Wages || line == Interest) && plan == 0 && actual == 0) continue;
                var l = new LineVm(Label(ctx, b, line), s.Get("books.pvaValue", "actual", Value(ctx, line, actual), "plan", Value(ctx, line, plan)), "booksPlan", WhyId(b, 0, line))
                { Note = Reason(ctx, b, line) };
                if (line == Profit) l.NoteTone = actual >= plan ? Tone.Good : Tone.Warn;
                vm.Lines.Add(l);
            }
        }

        public static long Planned(BusinessPlan p, int line)
        {
            switch (line)
            {
                case Fees: return p.Fees;
                case Sales: return p.Sales;
                case Goods: return p.Input;
                case Wages: return p.Wage;
                case Power: return p.Power;
                case Repairs: return p.Repairs;
                case Depreciation: return p.Depreciation;
                case Interest: return p.Interest;
                default: return p.Profit;
            }
        }

        /// <summary>A line's average a season over the kept (full) seasons.</summary>
        public static long Average(Business b, int line)
        {
            long sum = 0;
            foreach (BusinessReport r in b.Reports) sum += Amount(r, line);
            return b.Reports.Count == 0 ? 0 : sum / b.Reports.Count;
        }

        private static long AverageGrams(Business b, bool area)
        {
            long sum = 0;
            foreach (BusinessReport r in b.Reports) sum += area ? r.AreaGrams : r.OwnGrams;
            return b.Reports.Count == 0 ? 0 : sum / b.Reports.Count;
        }

        /// <summary>Within 5% of the plan.</summary>
        private static bool Near(long actual, long plan) { return System.Math.Abs(actual - plan) * 20 <= System.Math.Max(System.Math.Abs(plan), 1); }

        /// <summary>Why a line differs from the plan, in words: the grain that came, your wheat milled, who ran it, prices.</summary>
        public static string Reason(ViewContext ctx, Business b, int line)
        {
            var s = ctx.Strings;
            BusinessPlan p = b.Plan;
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            long plan = Planned(p, line), actual = Average(b, line);
            long area = AverageGrams(b, true), own = AverageGrams(b, false);
            string Kg(string key, long kg, long planned) { return s.Get(BusinessWords.Key(ctx, key, t), "kg", ctx.Fmt.Kg(kg), "plan", ctx.Fmt.Kg(planned)); }
            string Prices() { return s.Get(Near(actual, plan) ? "books.why.asPlanned" : "books.why.prices"); }
            switch (line)
            {
                case Fees:
                    if (Near(area, p.AreaGrams)) return Prices();
                    if (area > p.AreaGrams) return Kg("books.why.moreGrain", area, p.AreaGrams);
                    if (!BusinessRules.AreaComes(ctx.Sim, b)) return s.Get("books.why.feeHigh");
                    if (b.CloseHour - b.OpenHour < t.CloseHour - t.OpenHour) return Kg("books.why.fewerHours", area, p.AreaGrams);
                    return Kg("books.why.lessGrain", area, p.AreaGrams);
                case Sales: case Goods:
                    if (own == 0 && p.OwnGrams > 0) return s.Get(BusinessWords.Key(ctx, "books.why.noOwn", t));
                    return Near(own, p.OwnGrams) ? Prices() : Kg("books.why.ownKg", own, p.OwnGrams);
                case Power:
                    return Near(area + own, p.AreaGrams + p.OwnGrams) ? Prices() : Kg("books.why.groundKg", area + own, p.AreaGrams + p.OwnGrams);
                case Wages:
                    if (plan > 0 && actual == 0) return s.Get("books.why.ranYourself");
                    if (plan == 0 && actual > 0) return s.Get("books.why.hired");
                    return Prices();
                case Profit:
                    if (Near(actual, plan)) return s.Get("books.why.asPlanned");
                    // The line that moved profit most the same way: income above plan or costs below it raise it.
                    int most = t.ByRecipe ? Sales : Fees;
                    long biggest = 0;
                    foreach (int l in PlanLines)
                    {
                        if (l == Profit || (l == Fees && t.ByRecipe)) continue;
                        long effect = (Average(b, l) - Planned(p, l)) * (IsCost(l) ? -1 : 1);
                        if ((effect > 0) == (actual > plan) && System.Math.Abs(effect) > biggest) { biggest = System.Math.Abs(effect); most = l; }
                    }
                    return s.Get(actual > plan ? "books.why.above" : "books.why.below", "amount", ctx.Fmt.Coins(System.Math.Abs(actual - plan)),
                        "line", Label(ctx, b, most).ToLowerInvariant());
                default: return Prices();
            }
        }
    }

    /// <summary>A business's closed season (spec 016 §7): its profit and loss, cash in and out, what it owned and owed.</summary>
    public static class BusinessReportSheet
    {
        public static SheetVm Build(ViewContext ctx, long id, string arg)
        {
            Business b = ctx.Sim.State.Businesses?.Find(id);
            int back = int.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
            if (b == null || back < 1 || back > b.Reports.Count) return new SheetVm { Icon = "flour_mill", Title = ctx.Reject(RejectReason.UnknownBusiness) };
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            BusinessReport r = b.Reports[b.Reports.Count - back];
            var vm = new SheetVm { Icon = t.Id, Title = ctx.Strings.Get("books.title", "name", BusinessWords.Name(ctx, t), "period", ReportSheet.PeriodName(ctx, r.Period)) };
            BusinessBooksSheet.AddStatements(ctx, vm, b, r, back);
            return vm;
        }
    }

    /// <summary>Putting your cash into a business or taking its cash out (spec 016 §3.2): amounts to choose, each with what it does.</summary>
    public static class BusinessCashSheet
    {
        public const string Put = "put", Take = "take";
        private static readonly long[] Steps = { 5_000_00, 10_000_00, 25_000_00, 50_000_00 };

        public static SheetVm Build(ViewContext ctx, long id, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(id);
            if (b == null) return new SheetVm { Icon = "coin", Title = ctx.Reject(RejectReason.UnknownBusiness) };
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            bool put = arg == Put;
            long have = put ? sim.State.Ledger.Cash : b.Ledger.Cash;
            var vm = new SheetVm
            {
                Icon = "coin", Title = s.Get(put ? "cashIn.title" : "cashOut.title", "name", BusinessWords.InSentence(ctx, t)),
                Subtitle = s.Get(put ? "cashIn.subtitle" : "cashOut.subtitle", "cash", ctx.Fmt.Coins(have)),
            };
            // Taking out, say what the next close will need: its worker's wage, paid upfront, and the repairs.
            if (!put && b.Status == BusinessStatus.Open && b.Worker != WorkerKind.Founder)
                vm.Subtitle = s.Get("cashOut.subtitleKeep", "cash", ctx.Fmt.Coins(have),
                    "keep", ctx.Fmt.Coins(BusinessRules.Wage(sim, t) + Money.MulRate(Sim.Core.Market.MarketRules.Indexed(sim, t.EquipmentValue), t.RepairsShare)));
            var amounts = new List<long>();
            foreach (long a in Steps)
                if (a <= have) amounts.Add(a);
            long all = have / (1000 * Money.Coin) * (1000 * Money.Coin); // all of it, to the 1,000 below
            if (!put && all > 0 && !amounts.Contains(all)) amounts.Add(all);
            foreach (long a in amounts)
            {
                long amount = a;
                string note = put ? s.Get("cashIn.note") : s.Get(amount <= BusinessBooks.CapitalIn(b) ? "cashOut.noteCapital" : "cashOut.noteIncome");
                vm.Lines.Add(new LineVm(ctx.Fmt.Coins(amount), note, put ? "cash" : "businessCash", put ? 0 : id)
                {
                    Action = ActionVm.Checked(ctx, (put ? "put:" : "take:") + amount, s.Get(put ? "cashIn.button" : "cashOut.button"), null, ActionKind.Primary,
                        at => put ? (Command)new PutCash(at, id, amount) : new TakeCash(at, id, amount)),
                });
            }
            if (amounts.Count == 0) vm.Lines.Add(new LineVm(s.Get(put ? "cashIn.none" : "cashOut.none"), ctx.Fmt.Coins(have), put ? "cash" : "businessCash", put ? 0 : id));
            return vm;
        }
    }

    /// <summary>
    /// Closing a business (spec 016 §3.10), before you do: its stock to the trader, the building and equipment at 60% of book value,
    /// its debts repaid, and what comes to you or becomes your loan.
    /// </summary>
    public static class BusinessCloseSheet
    {
        /// <summary>What closing now would raise and repay: stock, the sale, cash, debts; the net (to you when positive, your loan when not).</summary>
        public static void Figures(Simulation sim, Business b, out long stock, out long sale, out long debts, out long net)
        {
            stock = 0;
            for (int g = 0; g < b.Stock.Length; g++)
                if (b.Stock[g] > 0) stock += Sim.Core.Market.MarketRules.Value(sim, g, b.Stock[g], Sim.Core.Market.MarketRules.Floor(sim, g));
            long book = b.Ledger.Balance(Account.Buildings) + b.Ledger.Balance(Account.Equipment) + b.Ledger.Balance(Account.AccumulatedDepreciation);
            sale = Money.RoundToCoin(Money.MulRate(book, BusinessRules.Defs(sim).ClosingSaleShare));
            Loan loan = BusinessBooks.LoanOf(sim, b);
            debts = BusinessBooks.Overdrawn(b) + (loan != null && loan.IsOpen ? CreditRules.OwedNow(sim, loan) : 0);
            net = b.Ledger.Cash + stock + sale - debts;
        }

        public static SheetVm Build(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(id);
            if (b == null || b.Status == BusinessStatus.Closed) return new SheetVm { Icon = "flour_mill", Title = ctx.Reject(RejectReason.BusinessClosed) };
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            Figures(sim, b, out long stock, out long sale, out long debts, out long net);
            var vm = new SheetVm { Icon = t.Id, Title = s.Get("close.title", "name", BusinessWords.InSentence(ctx, t)), Subtitle = s.Get("close.subtitle") };
            vm.Lines.Add(new LineVm(s.Get("close.cash"), ctx.Fmt.Coins(b.Ledger.Cash), "businessClose", id));
            vm.Lines.Add(new LineVm(s.Get("close.stock"), ctx.Fmt.Coins(stock), "businessClose", id));
            vm.Lines.Add(new LineVm(s.Get("close.sale", "pct", ctx.Fmt.Percent(BusinessRules.Defs(sim).ClosingSaleShare)), ctx.Fmt.Coins(sale), "businessClose", id));
            if (debts > 0) vm.Lines.Add(new LineVm(s.Get("close.debts"), "−" + ctx.Fmt.Coins(debts), "businessClose", id));
            vm.Lines.Add(net >= 0
                ? new LineVm(s.Get("close.toYou"), ctx.Fmt.Coins(net), "businessClose", id) { NoteTone = Tone.Good }
                : new LineVm(s.Get("close.yourLoan", "seasons", ctx.Fmt.Integer(BusinessRules.Defs(sim).GuaranteeTermSeasons)), ctx.Fmt.Coins(-net), "businessClose", id) { NoteTone = Tone.Warn });
            vm.Actions.Add(ActionVm.Checked(ctx, "closeBusiness", s.Get("close.button", "name", BusinessWords.InSentence(ctx, t)), null, ActionKind.Danger, at => new CloseBusiness(at, id)));
            return vm;
        }
    }
}
