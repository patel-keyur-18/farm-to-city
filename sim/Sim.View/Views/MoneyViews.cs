using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>A loan's projected payments, one per season close (spec 005 §3.6): what the term rows show.</summary>
    public sealed class LoanPlanVm
    {
        public List<long> Payments { get; } = new List<long>();
        public long TotalInterest { get; internal set; }
    }

    /// <summary>The Menu (spec 008 §3.3, ADR 0043 §4): one row per section with its button.</summary>
    public static class MenuSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            var s = ctx.Strings;
            var vm = new SheetVm { Title = s.Get("menu.title") };
            void Add(string key, SheetRequest r, string icon)
            {
                vm.Lines.Add(new LineVm(s.Get("menu." + key), s.Get("menu." + key + ".note")) { Icon = icon, Action = ActionVm.Open("menu:" + key, s.Get("action.open"), r) });
            }
            Add("store", new SheetRequest("Store", 0), "sack");
            Add("money", new SheetRequest("Money", 0), "coin");
            if (ctx.Sim.State.People != null) Add("people", new SheetRequest("People", 0), "person_hand_f");
            if (ctx.Sim.State.Businesses != null) Add("businesses", new SheetRequest("Businesses", 0), "flour_mill"); // spec 016 §3.3
            Add("land", new SheetRequest("Lands", 0), "sign");
            Add("you", new SheetRequest("Founder", 0), "person_founder");
            if (ctx.Sim.State.Guidance != null) Add("letter", new SheetRequest("Letter", 0), "letter");
            Add("settings", new SheetRequest("Settings", 0), "gear");
            return vm;
        }
    }

    /// <summary>Buy land from the Menu: each parcel for sale with its price and Show (ADR 0043 §4).</summary>
    public static class LandsSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            var vm = new SheetVm { Icon = "land", Title = ctx.Strings.Get("lands.title"), Subtitle = ctx.Strings.Get("lands.subtitle") };
            foreach (Parcel p in sim.State.Land.Parcels)
            {
                if (p.Owned) continue;
                ParcelDef d = defs.Map.Parcels[p.Id];
                int tile = d.Tiles[0];
                string soil = ctx.Strings.Get(defs.Soils[sim.State.Land.Tiles[tile].Soil].NameKey);
                var line = new LineVm(ctx.Strings.Get("lands.parcel", "soil", soil, "acres", ctx.Strings.Plural("land.acres", d.Tiles.Count)),
                    ctx.Fmt.Coins(LandRules.ParcelPrice(sim, p)), "landPrice", tile)
                { Action = ActionVm.Open("show:" + tile, ctx.Strings.Get("action.show"), new SheetRequest("ShowTile", tile)) };
                vm.Lines.Add(line);
            }
            if (vm.Lines.Count == 0) vm.Subtitle = ctx.Strings.Get("lands.none");
            return vm;
        }
    }

    /// <summary>
    /// Money and loans (ADR 0043 §5, spec 019 §3.9): opens on *This season* (earned, spent, profit, why your cash moved differently,
    /// next season already due), with *Last season* beside it after the first close; *Your wealth* holds net worth, the loans and borrowing.
    /// </summary>
    public static class MoneySheet
    {
        public const string Wealth = "wealth", LastSeason = "last";

        /// <summary>A *Your season* line's *Why?* id: last season or this, the block, the line.</summary>
        public const int EarnedBlock = 0, SpentBlock = 1, MovedBlock = 2, DueBlock = 3, Totals = 9;
        public const int ProfitLine = 0, CashLine = 1, DueLine = 2;

        public static long WhyId(bool last, int block, int line) { return (last ? 1_000_000 : 0) + block * 10_000 + line; }

        public static SheetVm Build(ViewContext ctx, string tab = null)
        {
            return tab == Wealth ? BuildWealth(ctx) : BuildSeason(ctx, tab == LastSeason);
        }

        private static void Tabs(ViewContext ctx, SheetVm vm, string tab)
        {
            vm.Tab = tab;
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:season", ctx.Strings.Get("money.tabSeason"), new SheetRequest("Money", 0)),
                ActionVm.Open("tab:wealth", ctx.Strings.Get("money.tabWealth"), new SheetRequest("Money", 0, Wealth)),
            };
        }

        private static SheetVm BuildSeason(ViewContext ctx, bool last)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            SeasonVm season = SeasonBook.Build(ctx, last);
            last = season.Last;
            var vm = new SheetVm { Icon = "coin", Title = s.Get("money.title"), Subtitle = ReportSheet.PeriodName(ctx, season.Period) };
            Tabs(ctx, vm, "tab:season");
            if (SeasonBook.HasLast(sim))
            {
                vm.Switch = new List<ActionVm>
                {
                    ActionVm.Open("switch:this", s.Get("money.thisSeason"), new SheetRequest("Money", 0)),
                    ActionVm.Open("switch:last", s.Get("money.lastSeason"), new SheetRequest("Money", 0, LastSeason)),
                };
                vm.SwitchOn = last ? "switch:last" : "switch:this";
            }
            if (!last) vm.Lines.Add(new LineVm(s.Get("money.cash"), ctx.Fmt.Coins(sim.State.Ledger.Cash), "cash") { Icon = "coin" });

            void Block(string head, long total, List<SeasonLineVm> lines, int block, bool signed)
            {
                vm.Lines.Add(LineVm.Header(total < 0 ? head : s.Get("season.head", "head", head, "amount", signed ? Signed(ctx, total) : ctx.Fmt.Coins(total))));
                for (int i = 0; i < lines.Count; i++)
                    vm.Lines.Add(new LineVm(lines[i].Label, signed ? Signed(ctx, lines[i].Amount) : ctx.Fmt.Coins(lines[i].Amount), "season", WhyId(last, block, i)) { Icon = lines[i].Icon });
            }
            Block(s.Get("season.earned"), season.EarnedTotal, season.Earned, EarnedBlock, false);
            if (season.Earned.Count == 0) vm.Lines.Add(new LineVm(s.Get("season.nothingEarned"), null));
            Block(s.Get("season.spent"), season.SpentTotal, season.Spent, SpentBlock, false);
            if (season.Spent.Count == 0) vm.Lines.Add(new LineVm(s.Get("season.nothingSpent"), null));
            vm.Lines.Add(LineVm.Header(s.Get(last ? "season.profitLast" : "season.profit")));
            vm.Lines.Add(new LineVm(s.Get(season.Profit < 0 ? "season.loss" : "season.profitLine"), Signed(ctx, season.Profit), "season", WhyId(last, Totals, ProfitLine)) { Icon = "coin" });

            Block(s.Get("season.moved"), -1, season.Moved, MovedBlock, true);
            vm.Lines.Add(new LineVm(s.Get("season.cashLine"), s.Get(last ? "season.cashValueLast" : "season.cashValue", "start", ctx.Fmt.Coins(season.CashStart), "end", ctx.Fmt.Coins(season.CashEnd)),
                "season", WhyId(last, Totals, CashLine)) { Icon = "coin" });

            if (last)
            {
                vm.Lines.Add(new LineVm(s.Get("season.report"), s.Get("season.reportNote"))
                    { Action = ActionVm.Open("report", s.Get("money.seeReport"), new SheetRequest("Report", season.Period)) });
                return vm;
            }
            Block(s.Get("season.due"), season.DueTotal, season.Due, DueBlock, false);
            if (season.Due.Count == 0) { vm.Lines.Add(new LineVm(s.Get("season.nothingDue"), null)); return vm; }
            if (season.Short == 0)
                vm.Lines.Add(new LineVm(s.Get("season.dueTotal"), ctx.Fmt.Coins(season.DueTotal), "season", WhyId(false, Totals, DueLine)) { Note = s.Get("season.covered"), NoteTone = Tone.Good });
            else
                vm.Lines.Add(new LineVm(s.Get("season.dueTotal"), ctx.Fmt.Coins(season.DueTotal), "season", WhyId(false, Totals, DueLine))
                {
                    Note = s.Get("season.short", "amount", ctx.Fmt.Coins(season.Short), "who", season.ShortWho), NoteTone = Tone.Warn,
                    Action = ActionVm.Open("store", s.Get("season.toStore"), new SheetRequest("Store", 0)),
                });
            return vm;
        }

        public static string Signed(ViewContext ctx, long v) { return v > 0 ? "+" + ctx.Fmt.Coins(v) : v < 0 ? "−" + ctx.Fmt.Coins(-v) : ctx.Fmt.Coins(0); }

        private static SheetVm BuildWealth(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "coin", Title = s.Get("money.title") };
            Tabs(ctx, vm, "tab:wealth");
            vm.Lines.Add(LineVm.Header(s.Get("money.glance")));
            vm.Lines.Add(new LineVm(s.Get("money.cash"), ctx.Fmt.Coins(sim.State.Ledger.Cash), "cash"));
            NetWorthSnapshot worth = Reporting.NetWorth(sim);
            vm.Lines.Add(new LineVm(s.Get("report.netWorth"), ctx.Fmt.Coins(worth.Total), "netWorth"));
            vm.Lines.Add(new LineVm(s.Get("money.landPart"), ctx.Fmt.Coins(worth.LandAtMarket), "netWorth"));
            if (worth.Businesses > 0) vm.Lines.Add(new LineVm(s.Get("money.businessPart"), ctx.Fmt.Coins(worth.Businesses), "netWorth"));
            if (worth.OwedByBuyers > 0) vm.Lines.Add(new LineVm(s.Get("money.owed"), ctx.Fmt.Coins(worth.OwedByBuyers), "owed"));

            if (sim.State.Credit == null) return vm;
            vm.Lines.Add(LineVm.Header(s.Get("money.loans")));
            bool any = false;
            foreach (Loan loan in sim.State.Credit.Loans)
            {
                if (!loan.IsOpen) continue;
                any = true;
                LenderDef l = sim.Config.Game.Credit.Lenders[loan.Lender];
                string value = s.Get(loan.HasArrears ? "money.loanOverdue" : "money.loanValue", "owed", ctx.Fmt.Coins(CreditRules.OwedNow(sim, loan)), "next", ctx.Fmt.Coins(Repay.NextPayment(sim, loan)));
                if (loan.BusinessId != 0)
                {
                    // A business's loan, paid from its own cash; yours only as its guarantor (spec 016 §3.8).
                    var b = sim.State.Businesses.Find(loan.BusinessId);
                    vm.Lines.Add(new LineVm(s.Get("money.businessLoan", "lender", s.Get(l.NameKey), "name", BusinessWords.InSentence(ctx, Sim.Core.Business.BusinessRules.TypeOf(sim, b))), value, "loan", loan.Id)
                        { Action = ActionVm.Open("business:" + b.Id, s.Get("action.open"), new SheetRequest("Business", b.Id)) });
                    continue;
                }
                vm.Lines.Add(new LineVm(s.Get(l.NameKey), value, "loan", loan.Id) { Action = ActionVm.Open("repay:" + loan.Id, s.Get("money.repay"), new SheetRequest("Repay", loan.Id)) });
            }
            if (!any) vm.Lines.Add(new LineVm(s.Get("money.noLoans"), s.Get("money.noLoansNote")));

            vm.Lines.Add(LineVm.Header(s.Get("money.borrow")));
            for (int i = 0; i < sim.Config.Game.Credit.Lenders.Count; i++)
            {
                LenderDef l = sim.Config.Game.Credit.Lenders[i];
                CreditLimit c = CreditRules.Limit(sim, i);
                bool can = !c.ScoreTooLow && c.Limit >= l.MinAmount;
                string why = c.ScoreTooLow ? ctx.Reject(RejectReason.ScoreTooLow) : can ? null : s.Get("money.cantBorrow", "min", ctx.Fmt.Coins(l.MinAmount));
                vm.Lines.Add(new LineVm(s.Get(l.NameKey), s.Get("money.lenderValue", "limit", ctx.Fmt.Coins(c.Limit), "rate", ctx.Fmt.Percent(CreditRules.Rate(l, sim.State.Credit.Score))), "creditLimit", i)
                {
                    Action = new ActionVm("borrow:" + l.Id, s.Get("money.borrowButton"), null, ActionKind.Open, can, why, null, new SheetRequest("Borrow", i)),
                });
            }
            return vm;
        }
    }

    /// <summary>The season report (spec 008 §3.8, ADR 0032): every line with its plain label and its Why?.</summary>
    public static class ReportSheet
    {
        public static readonly string[] Order = { "farmIncome", "farmCosts", "farmProfit", "otherIncome", "household", "finance", "other", "netResult" };

        public static string PeriodName(ViewContext ctx, long period)
        {
            return ctx.Strings.Get("report.period", "season", FieldSheets.SeasonName(ctx, period), "year", ctx.Fmt.Integer(period / 4 + 1));
        }

        public static SheetVm Build(ViewContext ctx, long period)
        {
            Simulation sim = ctx.Sim;
            SeasonReport r = sim.State.Finance.Reports.Find(x => x.Period == period);
            var vm = new SheetVm { Icon = "coin", Title = ctx.Strings.Get("report.title", "period", PeriodName(ctx, period)) };
            foreach (string key in Order)
            {
                ReportLine l = r.Line(key);
                if (l == null) continue;
                vm.Lines.Add(new LineVm(ctx.Strings.Get(l.LabelKey), ctx.Fmt.Coins(l.Amount), "report." + key, period));
            }
            vm.Lines.Add(new LineVm(ctx.Strings.Get("report.cashFlow"),
                ctx.Strings.Get("report.cashFlowValue", "start", ctx.Fmt.Coins(r.CashFlow.Opening), "end", ctx.Fmt.Coins(r.CashFlow.Closing)), "report.cashFlow", period));
            vm.Lines.Add(new LineVm(ctx.Strings.Get("report.netWorth"), ctx.Fmt.Coins(r.NetWorth.Total), "report.netWorth", period));
            return vm;
        }
    }

    /// <summary>Borrowing (ADR 0043 §5): amounts, then terms with their payments and total interest.</summary>
    public static class BorrowSheet
    {
        /// <summary>The terms offered: doubling from the lender's shortest, at most three, never past its longest (moneylender 1, 2, 4; bank 2, 4, 8).</summary>
        public static List<int> Terms(LenderDef l)
        {
            var terms = new List<int>();
            for (int t = l.MinTerm; t <= l.MaxTerm && terms.Count < 3; t *= 2) terms.Add(t);
            return terms;
        }

        /// <summary>A quarter, a half, three quarters and all of the limit, rounded down to 1,000 coins, at least the minimum, no repeats.</summary>
        public static List<long> Amounts(Simulation sim, int lender, long limit)
        {
            LenderDef l = sim.Config.Game.Credit.Lenders[lender];
            var list = new List<long>();
            foreach (int quarters in new[] { 1, 2, 3, 4 })
            {
                long a = limit * quarters / 4 / (1000 * Money.Coin) * (1000 * Money.Coin);
                if (a < l.MinAmount || a > limit || list.Contains(a)) continue;
                list.Add(a);
            }
            return list;
        }

        /// <summary>Each season close's payment for a loan taken now (interest only at a part-season close, spec 005 §3.6).</summary>
        public static LoanPlanVm Plan(Simulation sim, int lender, long amount, int term)
        {
            LenderDef l = sim.Config.Game.Credit.Lenders[lender];
            Fixed rate = CreditRules.Rate(l, sim.State.Credit.Score);
            long S = sim.Clock.SecondsPerSeason, t = sim.State.Clock.SimTime;
            long first = (t / S + 1) * S - t;
            var plan = new LoanPlanVm();
            long principal = amount, step = amount / term;
            bool partial = t % S != 0;
            int left = term;
            long seconds = first;
            while (left > 0)
            {
                long interest = CreditRules.Interest(principal, rate, seconds, S);
                long pay = 0;
                if (partial) partial = false;
                else
                {
                    left--;
                    pay = left == 0 ? principal : l.Schedule == LoanSchedule.Bullet ? 0 : System.Math.Min(step, principal);
                }
                plan.Payments.Add(interest + pay);
                plan.TotalInterest += interest;
                principal -= pay;
                seconds = S;
            }
            return plan;
        }

        public static SheetVm Build(ViewContext ctx, int lender, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LenderDef l = sim.Config.Game.Credit.Lenders[lender];
            CreditLimit c = CreditRules.Limit(sim, lender);
            var vm = new SheetVm { Icon = "coin", Title = s.Get("borrow.title", "lender", s.Get(l.NameKey)) };
            if (string.IsNullOrEmpty(arg))
            {
                vm.Subtitle = s.Get("borrow.amountSubtitle", "limit", ctx.Fmt.Coins(c.Limit));
                List<long> amounts = Amounts(sim, lender, c.Limit);
                string[] parts = { "borrow.quarter", "borrow.half", "borrow.threeQuarters", "borrow.all" };
                foreach (long a in amounts)
                {
                    int q = (int)System.Math.Min(4, System.Math.Max(1, (a * 4 + c.Limit - 1) / System.Math.Max(1, c.Limit)));
                    vm.Lines.Add(new LineVm(ctx.Fmt.Coins(a), s.Get(a == amounts[amounts.Count - 1] ? parts[3] : parts[q - 1]), "creditLimit", lender)
                    { Action = ActionVm.Open("amount:" + a, s.Get("borrow.choose"), new SheetRequest("Borrow", lender, a.ToString(System.Globalization.CultureInfo.InvariantCulture))) });
                }
                return vm;
            }
            long amount = long.Parse(arg, System.Globalization.CultureInfo.InvariantCulture);
            vm.Subtitle = s.Get("borrow.termSubtitle", "amount", ctx.Fmt.Coins(amount));
            foreach (int term in Terms(l))
            {
                int tm = term;
                LoanPlanVm plan = Plan(sim, lender, amount, term);
                string value = s.Get("borrow.termValue", "first", ctx.Fmt.Coins(plan.Payments[0]), "last", ctx.Fmt.Coins(plan.Payments[plan.Payments.Count - 1]),
                    "interest", ctx.Fmt.Coins(plan.TotalInterest));
                vm.Lines.Add(new LineVm(s.Plural("borrow.over", term), value, "borrow", lender * 1000 + term)
                {
                    Action = ActionVm.Checked(ctx, "take:" + term, s.Get("borrow.take"), ctx.Fmt.Coins(amount), ActionKind.Primary, at => new TakeLoan(at, l.Id, amount, tm)),
                });
            }
            return vm;
        }
    }

    /// <summary>Repaying a loan (ADR 0043 §5): the next payment, all of it, or more time.</summary>
    public static class Repay
    {
        /// <summary>
        /// What the next season close will ask (spec 005 §3.6): the interest owed and accruing up to the close, any arrears, and the
        /// principal due then (none at a part-season first close; all of it at the last; a bank's part of it in between).
        /// </summary>
        public static long NextPayment(Simulation sim, Loan loan) { return NextPayment(sim, loan, out _, out _); }

        /// <summary>The next payment as interest (with any late interest) and principal: paying back what you borrowed, not a cost.</summary>
        public static long NextPayment(Simulation sim, Loan loan, out long interestDue, out long principalDue)
        {
            interestDue = principalDue = 0;
            if (!loan.IsOpen) return 0;
            long S = sim.Clock.SecondsPerSeason, close = (sim.State.Clock.SimTime / S + 1) * S;
            CreditRules.AccruedSince(sim, loan, close, out long interest, out long penalty);
            long principal = loan.FirstClosePartial ? loan.ArrearsPrincipal
                : loan.Remaining <= 1 ? loan.Principal
                : loan.ArrearsPrincipal + (loan.Schedule == LoanSchedule.Bullet ? 0 : System.Math.Min(loan.InstalmentBase, loan.Principal - loan.ArrearsPrincipal));
            interestDue = loan.ArrearsInterest + loan.InterestAccrued + interest + penalty;
            principalDue = principal;
            return interestDue + principalDue;
        }

        private static long NextNow(Simulation sim, Loan loan) { return System.Math.Min(NextPayment(sim, loan), CreditRules.OwedNow(sim, loan)); }

        public static SheetVm Build(ViewContext ctx, long loanId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Loan loan = sim.State.Credit.Loans.Find(x => x.Id == loanId);
            LenderDef l = sim.Config.Game.Credit.Lenders[loan.Lender];
            var vm = new SheetVm { Icon = "coin", Title = s.Get("repay.title", "lender", s.Get(l.NameKey)) };
            long next = NextNow(sim, loan), all = CreditRules.OwedNow(sim, loan);
            vm.Lines.Add(new LineVm(s.Get("repay.next"), ctx.Fmt.Coins(next), "loan", loanId)
            { Action = ActionVm.Checked(ctx, "repayNext", s.Get("repay.pay"), ctx.Fmt.Coins(next), ActionKind.Primary, at => new RepayLoan(at, loanId, NextNow(sim, loan))) });
            // Both amounts are worked out when pressed: interest accrues every second, and a sheet built earlier would leave a few paise owing.
            vm.Lines.Add(new LineVm(s.Get("repay.all"), ctx.Fmt.Coins(all), "loan", loanId)
            { Action = ActionVm.Checked(ctx, "repayAll", s.Get("repay.pay"), ctx.Fmt.Coins(all), ActionKind.Primary, at => new RepayLoan(at, loanId, CreditRules.OwedNow(sim, loan))) });
            vm.Lines.Add(new LineVm(s.Get("repay.time"), s.Get("repay.timeNote"))
            { Action = ActionVm.Checked(ctx, "restructure", s.Get("repay.ask"), null, ActionKind.Secondary, at => new RestructureLoan(at, loanId)) });
            return vm;
        }
    }

    /// <summary>Buying a parcel with a bank loan secured by the land it buys (ADR 0028, ADR 0043 §5): one row per term.</summary>
    public static class LandLoanSheet
    {
        public static SheetVm Build(ViewContext ctx, int tileId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Parcel parcel = sim.State.Land.Parcels[sim.State.Land.Tiles[tileId].ParcelId];
            string parcelId = sim.Config.Game.Map.Parcels[parcel.Id].Id;
            long price = LandRules.ParcelPrice(sim, parcel);
            int bank = Bank(sim);
            LenderDef l = sim.Config.Game.Credit.Lenders[bank];
            long loan = Loan(sim, bank, price);
            var vm = new SheetVm
            {
                Icon = "land", Title = s.Get("landLoan.title"),
                Subtitle = s.Get("landLoan.subtitle", "price", ctx.Fmt.Coins(price), "loan", ctx.Fmt.Coins(loan), "down", ctx.Fmt.Coins(price - loan)),
            };
            foreach (int term in BorrowSheet.Terms(l))
            {
                int tm = term;
                LoanPlanVm plan = BorrowSheet.Plan(sim, bank, loan, term);
                vm.Lines.Add(new LineVm(s.Plural("borrow.over", term), s.Get("borrow.termValue", "first", ctx.Fmt.Coins(plan.Payments[0]),
                    "last", ctx.Fmt.Coins(plan.Payments[plan.Payments.Count - 1]), "interest", ctx.Fmt.Coins(plan.TotalInterest)), "borrow", bank * 1000 + term)
                {
                    Action = ActionVm.Checked(ctx, "buyLoan:" + term, s.Get("action.buy"), ctx.Fmt.Coins(price - loan), ActionKind.Primary,
                        at => new BuyParcel(at, parcelId, new Financing(l.Id, loan, tm))),
                });
            }
            return vm;
        }

        internal static int Bank(Simulation sim)
        {
            var lenders = sim.Config.Game.Credit.Lenders;
            for (int i = 0; i < lenders.Count; i++) if (lenders[i].Secured) return i;
            return -1;
        }

        /// <summary>The most the bank lends towards this land (its limit with the land as collateral), in whole thousands, at most the price.</summary>
        internal static long Loan(Simulation sim, int bank, long price)
        {
            long limit = CreditRules.Limit(sim, bank, price).Limit;
            return System.Math.Min(price, limit) / (1000 * Money.Coin) * (1000 * Money.Coin);
        }
    }
}
