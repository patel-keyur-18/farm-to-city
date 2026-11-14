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

        /// <summary>The department's name ("Council's office").</summary>
        public static string Department(ViewContext ctx, int dept)
        {
            return ctx.Strings.Get("dept." + ctx.Sim.Config.Game.Treasury.Departments[dept].Id);
        }

        public static SheetVm Build(ViewContext ctx)
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

            vm.Lines.Add(new LineVm(s.Get("treasury.balance"), ctx.Fmt.Coins(st.Cash), "treasuryBalance", 0));
            vm.Lines.Add(new LineVm(s.Get("treasury.atClose"), f.Short > 0 ? s.Get("treasury.short", "short", ctx.Fmt.Coins(f.Short)) : s.Get("treasury.covered"), "treasuryDue", 0)
                { NoteTone = f.Short > 0 ? Tone.Warn : Tone.Good });

            var buttons = new List<ActionVm>
            {
                Pictured(ActionVm.Open("give", s.Get("treasury.give"), new SheetRequest(GiveKind, 0), s.Get("treasury.giveDetail")), "coin"),
                Pictured(ActionVm.Open("books", s.Get("treasury.books"), new SheetRequest(BooksKind, 0), s.Get("treasury.booksDetail")), "menu"),
            };
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });

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
