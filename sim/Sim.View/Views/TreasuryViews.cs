using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// <em>Town treasury</em> (spec 021 §7, ADR 0108): the town's second wallet, in the Store and People layout. A banner says it is not yours,
    /// a glance (the balance, whether the close is covered), picture buttons (give, the books), then this season's money in and out by
    /// department with each service's condition. A view: it reads the state and changes nothing.
    /// </summary>
    public static class TreasurySheet
    {
        public const string Kind = "Treasury";
        public const string GiveKind = "TreasuryGive";
        public const string BooksKind = "TreasuryBooks";
        public const string StatementKind = "TreasuryStatement";
        public const string BudgetKind = "TreasuryBudget";
        public const string BudgetTab = "budget", SeasonTab = "season";

        /// <summary>The department's name ("Council's office").</summary>
        public static string Department(ViewContext ctx, int dept)
        {
            return ctx.Strings.Get("dept." + ctx.Sim.Config.Game.Treasury.Departments[dept].Id);
        }

        public static SheetVm Build(ViewContext ctx, string tab = null)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "place_council_hall", Title = s.Get("treasury.title"), Subtitle = ReportSheet.PeriodName(ctx, sim.Period) };
            if (!TreasuryRules.IsOpen(sim)) return vm;
            TreasuryState st = TreasuryRules.State(sim);
            TreasuryRules.CloseForecast f = TreasuryRules.Forecast(sim);

            vm.Lines.Add(new LineVm(s.Get("treasury.notYours", "cash", ctx.Fmt.Coins(sim.State.Ledger.Cash)), null) { Banner = true });
            if (f.Short > 0)
                vm.Lines.Add(new LineVm(s.Get("treasury.shortBanner", "short", ctx.Fmt.Coins(f.Short), "dept", Department(ctx, f.FirstUnpaid)), null) { Banner = true, NoteTone = Tone.Warn });

            AdvisorVm planner = AdvisorWords.Planner(ctx); // the town planner's line for the last close (spec 021 §3.12)
            if (planner != null) vm.Lines.Add(new LineVm(planner.Text, null) { Advisor = planner });
            vm.Lines.Add(new LineVm(s.Get("treasury.balance"), ctx.Fmt.Coins(st.Cash), "treasuryBalance", 0));
            vm.Lines.Add(new LineVm(s.Get("treasury.atClose"), f.Short > 0 ? s.Get("treasury.short", "short", ctx.Fmt.Coins(f.Short)) : s.Get("treasury.covered"), "treasuryDue", 0)
                { NoteTone = f.Short > 0 ? Tone.Warn : Tone.Good });

            var buttons = new List<ActionVm>
            {
                Pictured(ActionVm.Open("give", s.Get("treasury.give"), new SheetRequest(GiveKind, 0), s.Get("treasury.giveDetail")), "coin"),
                Pictured(ActionVm.Open("books", s.Get("treasury.books"), new SheetRequest(BooksKind, 0), s.Get("treasury.booksDetail")), "menu"),
            };
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });

            tab = tab ?? SeasonTab;
            bool unseen = st.Window != BudgetWindow.None && !st.WindowSeen;
            vm.Lines.Add(new LineVm(null, null)
            {
                TabOn = "tab:" + tab,
                Tabs = new List<ActionVm>
                {
                    ActionVm.Open("tab:" + SeasonTab, s.Get("treasury.tabSeason"), new SheetRequest(Kind, 0, SeasonTab)),
                    ActionVm.Open("tab:" + BudgetTab, s.Get(unseen ? "treasury.tabBudgetNew" : "treasury.tabBudget"), new SheetRequest(Kind, 0, BudgetTab)),
                },
            });
            if (tab == BudgetTab)
            {
                BudgetRows(ctx, vm, st, f);
                return vm;
            }

            // Money in: the grants as the close will pay them, the rents and gifts already in.
            TreasuryRules.GrantsNow(sim, out long untied, out long tied, out long withheld);
            long rents = -st.Ledger.Balance(Account.PitchRents), gifts = -st.Ledger.Balance(Account.GiftsFromFounder);
            long totalIn = untied + tied + rents + gifts;
            vm.Lines.Add(LineVm.Header(s.Get("treasury.moneyIn", "total", ctx.Fmt.Coins(totalIn))));
            int residents = TreasuryRules.Residents(sim);
            TreasuryDef d = sim.Config.Game.Treasury;
            vm.Lines.Add(new LineVm(s.Get("treasury.untied", "n", ctx.Fmt.Integer(residents), "each", ctx.Fmt.Amount(Sim.Core.Market.MarketRules.Indexed(sim, d.GrantUntied))), ctx.Fmt.Coins(untied), "treasuryUntied", 0));
            vm.Lines.Add(new LineVm(s.Get("treasury.tied"), tied > 0 ? ctx.Fmt.Coins(tied) : s.Get("treasury.tiedWithheld"), "treasuryTied", 0) { Dim = tied == 0 });
            vm.Lines.Add(new LineVm(s.Get("treasury.rents"), ctx.Fmt.Coins(rents), "treasuryRents", 0));
            vm.Lines.Add(new LineVm(s.Get("treasury.gifts"), ctx.Fmt.Coins(gifts), "treasuryGifts", 0) { Dim = gifts == 0 });

            // Money out, a row for each department with its service.
            vm.Lines.Add(LineVm.Header(s.Get("treasury.moneyOut", "total", ctx.Fmt.Coins(f.Due))));
            for (int i = 0; i < TreasuryState.Departments; i++)
            {
                if (f.Needs[i] == 0)
                {
                    vm.Lines.Add(new LineVm(Department(ctx, i), null) { Note = s.Get("treasury.nothingYet"), Dim = true });
                    continue;
                }
                int service = (int)(st.Condition[i] * Fixed.FromInt(100)).FloorToLong();
                var row = new LineVm(s.Get("treasury.deptService", "dept", Department(ctx, i), "pct", ctx.Fmt.Integer(service)), ctx.Fmt.Coins(f.Needs[i]), "treasuryDept", i) { Meter = service };
                vm.Lines.Add(row);
            }
            return vm;
        }

        // ---- the budget (spec 021 §3.6, US-139) ----

        /// <summary>The year the budget is for, as the player counts it ("Year 2").</summary>
        private static string YearName(ViewContext ctx, TreasuryState st) { return ctx.Fmt.Integer(st.YearBudgeted + 1); }

        /// <summary>The next window's season: Monsoon while a revision is still to come this year, else Spring.</summary>
        private static string NextWindow(ViewContext ctx, TreasuryState st)
        {
            int seasons = Sim.Core.Definitions.ClockDef.SeasonsPerYear;
            long period = ctx.Sim.Period;
            bool revisionAhead = !st.Revised && st.YearBudgeted == period / seasons && period % seasons < 2;
            return FieldSheets.SeasonName(ctx, revisionAhead ? period - period % seasons + 2 : (period / seasons + 1) * seasons);
        }

        /// <summary>The next window's season name, for a locked lever's reason.</summary>
        public static string NextWindowName(ViewContext ctx) { return NextWindow(ctx, TreasuryRules.State(ctx.Sim)); }

        private static void BudgetRows(ViewContext ctx, SheetVm vm, TreasuryState st, TreasuryRules.CloseForecast f)
        {
            var s = ctx.Strings;
            bool open = st.Window != BudgetWindow.None, revise = st.Window == BudgetWindow.Revise, looked = st.WindowSeen;
            string title = revise ? s.Get("budget.titleRevision") : s.Get("budget.titleYear", "n", YearName(ctx, st));
            string state = s.Get(!open ? "budget.stateLocked" : looked ? "budget.stateOpen" : revise ? "budget.stateOpen" : "budget.stateProposed");
            string text = !open ? s.Get("budget.winLocked", "next", NextWindow(ctx, st))
                : revise ? s.Get(looked ? "budget.winRevise" : "budget.winReviseNew")
                : s.Get(looked ? "budget.winOpen" : "budget.winProposed");
            var strip = new LineVm(title + " · " + state, null, "treasuryWindow", 0) { Banner = true, Note = text, NoteTone = open ? Tone.Plain : Tone.Good, Dim = !open };
            if (open) strip.Action = ActionVm.Checked(ctx, "accept", s.Get("budget.accept"), null, ActionKind.Primary, at => new AcceptBudget(at));
            vm.Lines.Add(strip);

            long total = 0;
            for (int i = 0; i < TreasuryState.Departments; i++) if (f.Needs[i] > 0) total += st.Budget[i];
            vm.Lines.Add(LineVm.Header(s.Get("budget.header", "total", ctx.Fmt.Coins(total))));
            TreasuryReport last = st.Reports.Count == 0 ? null : st.Reports[st.Reports.Count - 1];
            for (int i = 0; i < TreasuryState.Departments; i++)
            {
                if (f.Needs[i] == 0)
                {
                    vm.Lines.Add(new LineVm(Department(ctx, i), null) { Note = s.Get("treasury.nothingYet"), Dim = true });
                    continue;
                }
                var row = new LineVm(Department(ctx, i), s.Get("budget.rowValue", "budget", ctx.Fmt.Coins(st.Budget[i]), "needs", ctx.Fmt.Coins(f.Needs[i])), "treasuryDept", i);
                DepartmentRow r = last == null || i >= last.Rows.Count || last.Rows[i].Needs == 0 ? null : last.Rows[i];
                if (r != null) { row.Note = Variance(ctx, r, out Tone tone); row.NoteTone = tone; }
                if (open && f.Needs[i] > 0) row.Action = ActionVm.Open("budget:" + i, s.Get("budget.change"), new SheetRequest(BudgetKind, i));
                vm.Lines.Add(row);
            }
            vm.Lines.Add(new LineVm(s.Get(last == null ? "budget.firstHint" : "budget.lastHint"), null) { Dim = true });
        }

        /// <summary>The one sentence for a department's last close (spec 021 §3.6): what it was budgeted against what it needed and was paid.</summary>
        public static string Variance(ViewContext ctx, DepartmentRow r, out Tone tone)
        {
            var s = ctx.Strings;
            var f = ctx.Fmt;
            tone = Tone.Plain;
            switch ((VarianceSentence)r.Sentence)
            {
                case VarianceSentence.PricesRose: return s.Get("budget.var.pricesRose", "extra", f.Coins(r.Needs - r.Budget), "budget", f.Coins(r.Budget));
                case VarianceSentence.NewLine: return s.Get("budget.var.newLine");
                case VarianceSentence.CashRanOut: tone = Tone.Warn; return s.Get("budget.var.cashRanOut", "unpaid", f.Coins(r.Unpaid));
                case VarianceSentence.BudgetBelowNeeds: tone = Tone.Warn; return s.Get("budget.var.belowNeeds", "gap", f.Coins(r.Needs - r.Budget));
                case VarianceSentence.Unspent: return s.Get("budget.var.unspent", "left", f.Coins(r.Budget - r.Paid));
                default: tone = Tone.Good; return s.Get("budget.var.none");
            }
        }

        /// <summary>
        /// <em>Change a department's budget</em> (spec 021 §3.6): drag or type 0 to 120% of its needs now, with its needs marked on the slider.
        /// Under its needs it is paid after the other departments' budgets, which the note says.
        /// </summary>
        public static SheetVm BudgetSet(ViewContext ctx, int dept)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "place_council_hall", Title = s.Get("budget.setTitle", "dept", Department(ctx, dept)), Subtitle = s.Get("budget.setSubtitle") };
            TreasuryState st = TreasuryRules.State(sim);
            if (!TreasuryRules.IsOpen(sim) || dept < 0 || dept >= TreasuryState.Departments) return vm;
            long needs = TreasuryRules.Needs(sim, dept), max = TreasuryRules.MaxBudget(sim, dept);
            if (st.Window == BudgetWindow.None || needs == 0)
            {
                vm.Lines.Add(new LineVm(s.Get("budget.setLocked", "next", NextWindow(ctx, st)), null) { Dim = true });
                return vm;
            }
            vm.Lines.Add(new LineVm(s.Get("budget.setNeeds", "needs", ctx.Fmt.Coins(needs)), null, "treasuryDept", dept));
            long step = Money.Coin; // a whole coin, so the slider can stand exactly on the department's needs
            var amount = new AmountVm("budget:" + dept, 0, max, step, st.Budget[dept], a => BudgetPick(ctx, dept, a, needs, max), ctx.Fmt.Coins)
            {
                Mark = needs, MarkCaption = s.Get("budget.setMark", "needs", ctx.Fmt.Amount(needs)),
                MinCaption = ctx.Fmt.Amount(0),
                MaxCaption = s.Get("budget.setMax", "max", ctx.Fmt.Amount(max)),
            };
            vm.Lines.Add(new LineVm(s.Get("budget.setRange", "max", ctx.Fmt.Amount(max)), null) { Amount = amount });
            return vm;
        }

        private static AmountPick BudgetPick(ViewContext ctx, int dept, long amount, long needs, long max)
        {
            var s = ctx.Strings;
            if (System.Math.Abs(amount - needs) < Money.Coin) amount = needs; // within a coin of its needs the slider stands on them: the whole-coin step can't land on a fraction of a coin
            if (amount < 0 || amount > max)
                return new AmountPick { Action = new ActionVm("budget", s.Get("budget.setButton"), null, ActionKind.Primary, false, null, null), Note = s.Get("budget.setTooMuch", "max", ctx.Fmt.Coins(max)), Tone = Tone.Warn };
            string note = amount < needs ? s.Get("budget.setUnder", "gap", ctx.Fmt.Coins(needs - amount), "dept", Department(ctx, dept))
                : amount > needs ? s.Get("budget.setOver", "extra", ctx.Fmt.Coins(amount - needs)) : s.Get("budget.setExact");
            return new AmountPick
            {
                Action = ActionVm.Checked(ctx, "budget", s.Get("budget.setAmount", "amount", ctx.Fmt.Amount(amount)), null, ActionKind.Primary, at => new SetBudget(at, dept, amount)),
                Note = note, Tone = amount < needs ? Tone.Warn : Tone.Plain,
            };
        }

        /// <summary>
        /// The first window's card (spec 021 §3.6, ADR 0115): the proposal as it stands, <em>Accept</em> or <em>Change</em>. Later windows come by the bell.
        /// </summary>
        public static ConfirmCardVm WindowCard(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            TreasuryState st = TreasuryRules.State(sim);
            if (st == null || st.Window != BudgetWindow.Set || st.WindowSeen) return null;
            TreasuryRules.CloseForecast f = TreasuryRules.Forecast(sim);
            var card = new ConfirmCardVm
            {
                Title = s.Get("budget.cardTitle"), Subtitle = s.Get("budget.cardSubtitle", "n", YearName(ctx, st)),
                SafeLabel = s.Get("budget.change"), GoLabel = s.Get("budget.accept"), Warning = s.Get("budget.cardNote"), Reassure = true,
            };
            for (int i = 0; i < TreasuryState.Departments; i++)
                if (f.Needs[i] > 0) card.Rows.Add(new ConfirmRowVm(Department(ctx, i), s.Get("budget.cardRow", "amount", ctx.Fmt.Coins(st.Budget[i]))));
            card.Rows.Add(new ConfirmRowVm(s.Get("budget.cardHolds", "cash", ctx.Fmt.Coins(st.Cash)), s.Get("budget.cardDue", "due", ctx.Fmt.Coins(f.Due)), true));
            return card;
        }

        private static ActionVm Pictured(ActionVm a, string icon) { a.Icon = icon; return a; }

        // ---- give to the town ----

        /// <summary>
        /// <em>Give to the town</em> (spec 021 §3.8): drag or type an amount, then a card shows what it does to your cash and the town's balance.
        /// It starts at what the next close lacks (or the minimum), never more than your cash.
        /// </summary>
        public static SheetVm Give(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            TreasuryDef d = sim.Config.Game.Treasury;
            var vm = new SheetVm { Icon = "place_council_hall", Title = s.Get("treasury.giveTitle"), Subtitle = s.Get("treasury.giveSubtitle") };
            if (!TreasuryRules.IsOpen(sim)) return vm;
            long cash = sim.State.Ledger.Cash, min = d.GiftMinimum;
            if (cash < min)
            {
                vm.Lines.Add(new LineVm(s.Get("treasury.giveNone", "min", ctx.Fmt.Coins(min)), null) { Dim = true });
                return vm;
            }
            long step = 500 * 100;
            TreasuryRules.CloseForecast f = TreasuryRules.Forecast(sim);
            long start = f.Short > 0 ? System.Math.Min(cash, (f.Short + step - 1) / step * step) : min;
            var amount = new AmountVm("give", min, cash, step, System.Math.Max(min, start), a => GivePick(ctx, a, cash, min), ctx.Fmt.Coins)
            {
                MinCaption = ctx.Fmt.Amount(min),
                MaxCaption = s.Get("treasury.giveMax", "amount", ctx.Fmt.Amount(cash)),
            };
            vm.Lines.Add(new LineVm(s.Get("treasury.giveRange", "min", ctx.Fmt.Amount(min), "max", ctx.Fmt.Amount(cash)), null) { Amount = amount });
            vm.Lines.Add(new LineVm(s.Get("treasury.giveNote"), null));
            if (f.Short > 0) vm.Lines.Add(new LineVm(s.Get("treasury.giveCovers", "short", ctx.Fmt.Coins(f.Short)), null, "treasuryDue", 0));
            return vm;
        }

        private static AmountPick GivePick(ViewContext ctx, long amount, long cash, long min)
        {
            var s = ctx.Strings;
            string bad = amount < min ? s.Get("treasury.giveTooLittle", "min", ctx.Fmt.Coins(min)) : amount > cash ? s.Get("treasury.giveTooMuch", "max", ctx.Fmt.Coins(cash)) : null;
            if (bad != null) return new AmountPick { Action = new ActionVm("give", s.Get("treasury.giveButton"), null, ActionKind.Primary, false, null, null), Note = bad, Tone = Tone.Warn };
            return new AmountPick
            {
                Action = ActionVm.Checked(ctx, "give", s.Get("treasury.giveAmount", "amount", ctx.Fmt.Amount(amount)), null, ActionKind.Primary, at => new GiveToTreasury(at, amount)),
                Note = s.Get("treasury.giveLeaves", "left", ctx.Fmt.Coins(cash - amount), "town", ctx.Fmt.Coins(TreasuryRules.State(ctx.Sim).Cash + amount)),
            };
        }

        // ---- the town's books ----

        /// <summary><em>The town's books</em> (spec 021 §3.2): the last 8 closed seasons, each opening its three statements.</summary>
        public static SheetVm Books(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "place_council_hall", Title = s.Get("treasury.booksTitle"), Subtitle = s.Get("treasury.booksSubtitle") };
            if (!TreasuryRules.IsOpen(sim)) return vm;
            List<TreasuryReport> reports = TreasuryRules.State(sim).Reports;
            if (reports.Count == 0) vm.Lines.Add(new LineVm(s.Get("treasury.booksNone"), null) { Dim = true });
            for (int i = reports.Count - 1; i >= 0; i--)
            {
                TreasuryReport r = reports[i];
                long surplus = Surplus(r);
                string result = surplus >= 0 ? s.Get("treasury.surplus", "amount", ctx.Fmt.Coins(surplus)) : s.Get("treasury.shortfall", "amount", ctx.Fmt.Coins(-surplus));
                vm.Lines.Add(new LineVm(ReportSheet.PeriodName(ctx, r.Period), result + " · " + s.Get("treasury.cashAt", "cash", ctx.Fmt.Coins(r.Closing)), "treasuryFund", r.Period)
                {
                    Icon = "place_council_hall", Note = s.Get("treasury.fundBalance", "amount", ctx.Fmt.Coins(FundBalance(r))),
                    Action = ActionVm.Open("open:" + r.Period, s.Get("action.open"), new SheetRequest(StatementKind, r.Period)),
                });
            }
            return vm;
        }

        /// <summary>The season's surplus or shortfall: money in less money out less depreciation (income and expenses of the report).</summary>
        public static long Surplus(TreasuryReport r)
        {
            long sum = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances)
            {
                AccountGroup g = Accounts.GroupOf(kv.Key);
                if (g == AccountGroup.Income || g == AccountGroup.Expenses) sum -= kv.Value;
            }
            return sum;
        }

        /// <summary>What the town has when the season ends: its assets, the fund balance (the town can't borrow).</summary>
        public static long FundBalance(TreasuryReport r)
        {
            long assets = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances) if (Accounts.GroupOf(kv.Key) == AccountGroup.Assets) assets += kv.Value;
            return assets;
        }

        public const string InOut = "inout", CashFlow = "cash", Has = "has";

        /// <summary>One closed season: <em>Money in and out</em>, <em>Cash flow</em> and <em>What the town has</em> on tabs (spec 021 §3.2).</summary>
        public static SheetVm Statement(ViewContext ctx, long period, string tab = null)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            tab = tab ?? InOut;
            var vm = new SheetVm { Icon = "place_council_hall", Title = ReportSheet.PeriodName(ctx, period), Subtitle = s.Get("treasury.statementSubtitle"), Tab = "tab:" + tab };
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:" + InOut, s.Get("treasury.tabInOut"), new SheetRequest(StatementKind, period, InOut)),
                ActionVm.Open("tab:" + CashFlow, s.Get("treasury.tabCash"), new SheetRequest(StatementKind, period, CashFlow)),
                ActionVm.Open("tab:" + Has, s.Get("treasury.tabHas"), new SheetRequest(StatementKind, period, Has)),
            };
            TreasuryReport r = Find(sim, period);
            if (r == null) return vm;
            long lastPeriod = period - Sim.Core.Definitions.ClockDef.SeasonsPerYear;
            TreasuryReport last = lastPeriod < 0 ? null : Find(sim, lastPeriod);
            string lastName = last == null ? null : ReportSheet.PeriodName(ctx, lastPeriod);
            if (tab == InOut) InOutRows(ctx, vm, r, last, lastName);
            else if (tab == CashFlow) CashRows(ctx, vm, r, last, lastName);
            else HasRows(ctx, vm, r, last, lastName);
            return vm;
        }

        public static TreasuryReport Find(Simulation sim, long period)
        {
            if (sim.State.Treasury == null) return null;
            foreach (TreasuryReport r in sim.State.Treasury.Reports) if (r.Period == period) return r;
            return null;
        }

        private static LineVm Row(ViewContext ctx, string label, long value, long? before, string lastName, string whyKey, long whyId)
        {
            string beside = before.HasValue ? ctx.Strings.Get("treasury.lastYear", "season", lastName, "amount", ctx.Fmt.Coins(before.Value)) : ctx.Strings.Get("treasury.firstYear");
            return new LineVm(label, ctx.Fmt.Coins(value), whyKey, whyId) { Note = beside };
        }

        private static long Bal(TreasuryReport r, Account a, int sign) { return r == null ? 0 : sign * r.Balance(a); }

        private static void InOutRows(ViewContext ctx, SheetVm vm, TreasuryReport r, TreasuryReport last, string lastName)
        {
            var s = ctx.Strings;
            long inTotal = 0, outTotal = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances)
            {
                if (Accounts.GroupOf(kv.Key) == AccountGroup.Income) inTotal -= kv.Value;
                if (kv.Key >= Account.DeptOffice && kv.Key <= Account.DeptPolicies) outTotal += kv.Value;
            }
            long depreciation = r.Balance(Account.Depreciation);
            vm.Lines.Add(LineVm.Header(s.Get("treasury.moneyIn", "total", ctx.Fmt.Coins(inTotal))));
            foreach (Account a in IncomeAccounts)
            {
                long v = -r.Balance(a);
                if (v == 0 && Bal(last, a, -1) == 0) continue;
                vm.Lines.Add(Row(ctx, s.Get("account." + a), v, last == null ? (long?)null : -last.Balance(a), lastName, "treasuryAccount", r.Period * 10000 + (int)a));
            }
            vm.Lines.Add(LineVm.Header(s.Get("treasury.moneyOutHead", "total", ctx.Fmt.Coins(outTotal))));
            for (int i = 0; i < TreasuryState.Departments; i++)
            {
                Account a = Sim.Core.Town.TreasuryRules.Def(ctx.Sim).Departments[i].Account;
                long v = r.Balance(a);
                if (v == 0 && Bal(last, a, 1) == 0) continue;
                vm.Lines.Add(Row(ctx, TreasurySheet.Department(ctx, i), v, last == null ? (long?)null : last.Balance(a), lastName, "treasuryAccount", r.Period * 10000 + (int)a));
            }
            if (depreciation != 0) vm.Lines.Add(Row(ctx, s.Get("account.Depreciation"), depreciation, last == null ? (long?)null : last.Balance(Account.Depreciation), lastName, "treasuryAccount", r.Period * 10000 + (int)Account.Depreciation));
            vm.Lines.Add(LineVm.Header(s.Get("treasury.left", "total", ctx.Fmt.Coins(Surplus(r)))));
            vm.Lines.Add(Row(ctx, s.Get(Surplus(r) >= 0 ? "treasury.surplusLabel" : "treasury.shortfallLabel"), System.Math.Abs(Surplus(r)), last == null ? (long?)null : System.Math.Abs(Surplus(last)), lastName, "treasuryFund", r.Period));
        }

        private static readonly Account[] IncomeAccounts =
        {
            Account.StartUpGrant, Account.GrantUntied, Account.GrantTied, Account.PitchRents, Account.HouseTaxIncome, Account.BusinessTaxIncome, Account.Fees, Account.GiftsFromFounder,
        };

        private static void CashRows(ViewContext ctx, SheetVm vm, TreasuryReport r, TreasuryReport last, string lastName)
        {
            var s = ctx.Strings;
            long gifts = -r.Balance(Account.GiftsFromFounder), works = 0;
            foreach (JournalEntry e in ctx.Sim.State.Treasury.Ledger.Entries) // the cash paid for works this season
                if (e.Period == r.Period) foreach (Posting p in e.Postings) if (p.Account == Account.Buildings && p.Amount > 0) works += p.Amount;
            long work = r.Inflows - r.Outflows + works; // every income and every department's spending, the gifts inside it
            vm.Lines.Add(LineVm.Header(s.Get("treasury.cashWork", "total", ctx.Fmt.Coins(work))));
            vm.Lines.Add(Row(ctx, s.Get("treasury.cashInOut"), work, null, lastName, "treasuryFund", r.Period));
            vm.Lines.Add(Row(ctx, s.Get("treasury.cashGifts"), gifts, null, lastName, "treasuryAccount", r.Period * 10000 + (int)Account.GiftsFromFounder));
            vm.Lines.Add(LineVm.Header(s.Get("treasury.cashWorks", "total", ctx.Fmt.Coins(works))));
            vm.Lines.Add(LineVm.Header(s.Get("treasury.cashCash")));
            vm.Lines.Add(Row(ctx, s.Get("treasury.cashOpening"), r.Opening, last == null ? (long?)null : last.Opening, lastName, "treasuryFund", r.Period));
            vm.Lines.Add(Row(ctx, s.Get("treasury.cashClosing"), r.Closing, last == null ? (long?)null : last.Closing, lastName, "treasuryFund", r.Period));
        }

        private static void HasRows(ViewContext ctx, SheetVm vm, TreasuryReport r, TreasuryReport last, string lastName)
        {
            var s = ctx.Strings;
            long assets = FundBalance(r);
            vm.Lines.Add(LineVm.Header(s.Get("treasury.has", "total", ctx.Fmt.Coins(assets))));
            vm.Lines.Add(Row(ctx, s.Get("treasury.hasCash"), r.Balance(Account.Cash), last == null ? (long?)null : last.Balance(Account.Cash), lastName, "treasuryFund", r.Period));
            long buildings = r.Balance(Account.Buildings) + r.Balance(Account.AccumulatedDepreciation);
            vm.Lines.Add(Row(ctx, s.Get("treasury.hasWorks"), buildings, last == null ? (long?)null : last.Balance(Account.Buildings) + last.Balance(Account.AccumulatedDepreciation), lastName, "treasuryFund", r.Period));
            vm.Lines.Add(Row(ctx, s.Get("treasury.fundBalanceLabel"), assets, last == null ? (long?)null : FundBalance(last), lastName, "treasuryFund", r.Period));
            vm.Lines.Add(new LineVm(s.Get("treasury.noDebt"), null) { Dim = true });
        }
    }
}
