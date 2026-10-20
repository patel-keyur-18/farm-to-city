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
            Add("land", new SheetRequest("Lands", 0), "sign");
            Add("you", new SheetRequest("Founder", 0), "person_founder");
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

    /// <summary>Money and loans (ADR 0043 §5): at a glance, the last report, the loans and borrowing.</summary>
    public static class MoneySheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "coin", Title = s.Get("money.title") };
            vm.Lines.Add(LineVm.Header(s.Get("money.glance")));
            vm.Lines.Add(new LineVm(s.Get("money.cash"), ctx.Fmt.Coins(sim.State.Ledger.Cash), "cash"));
            vm.Lines.Add(new LineVm(s.Get("report.netWorth"), ctx.Fmt.Coins(Reporting.NetWorth(sim).Total), "netWorth"));
            vm.Lines.Add(new LineVm(s.Get("money.soFar"), ctx.Fmt.Coins(SeasonSoFar(sim)), "seasonSoFar"));

            vm.Lines.Add(LineVm.Header(s.Get("money.report")));
            List<SeasonReport> reports = sim.State.Finance.Reports;
            if (reports.Count == 0) vm.Lines.Add(new LineVm(s.Get("money.noReport"), s.Get("money.noReportNote")));
            else
            {
                SeasonReport r = reports[reports.Count - 1];
                vm.Lines.Add(new LineVm(ReportSheet.PeriodName(ctx, r.Period), s.Get("money.reportValue", "profit", ctx.Fmt.Coins(r.FarmProfit)), "report.farmProfit", r.Period)
                { Action = ActionVm.Open("report", s.Get("money.seeReport"), new SheetRequest("Report", r.Period)) });
            }

            if (sim.State.Credit == null) return vm;
            vm.Lines.Add(LineVm.Header(s.Get("money.loans")));
            bool any = false;
            foreach (Loan loan in sim.State.Credit.Loans)
            {
                if (!loan.IsOpen) continue;
                any = true;
                LenderDef l = sim.Config.Game.Credit.Lenders[loan.Lender];
                string value = s.Get(loan.HasArrears ? "money.loanOverdue" : "money.loanValue", "owed", ctx.Fmt.Coins(CreditRules.OwedNow(sim, loan)), "next", ctx.Fmt.Coins(Repay.NextPayment(sim, loan)));
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

        internal static long SeasonSoFar(Simulation sim)
        {
            Ledger ledger = sim.State.Ledger;
            long income = -(ledger.Balance(Account.CropSales) + ledger.Balance(Account.FruitSales));
            long costs = ledger.Balance(Account.SeedsAndPlanting) + ledger.Balance(Account.FertiliserAndSpraying) + ledger.Balance(Account.HiredLabour)
                + ledger.Balance(Account.Depreciation) + ledger.Balance(Account.TransportAndSelling);
            return income - costs;
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
        public static long NextPayment(Simulation sim, Loan loan)
        {
            if (!loan.IsOpen) return 0;
            long S = sim.Clock.SecondsPerSeason, close = (sim.State.Clock.SimTime / S + 1) * S;
            CreditRules.AccruedSince(sim, loan, close, out long interest, out long penalty);
            long principal = loan.FirstClosePartial ? loan.ArrearsPrincipal
                : loan.Remaining <= 1 ? loan.Principal
                : loan.ArrearsPrincipal + (loan.Schedule == LoanSchedule.Bullet ? 0 : System.Math.Min(loan.InstalmentBase, loan.Principal - loan.ArrearsPrincipal));
            return loan.ArrearsInterest + loan.InterestAccrued + interest + penalty + principal;
        }

        public static SheetVm Build(ViewContext ctx, long loanId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Loan loan = sim.State.Credit.Loans.Find(x => x.Id == loanId);
            LenderDef l = sim.Config.Game.Credit.Lenders[loan.Lender];
            var vm = new SheetVm { Icon = "coin", Title = s.Get("repay.title", "lender", s.Get(l.NameKey)) };
            long next = System.Math.Min(NextPayment(sim, loan), CreditRules.OwedNow(sim, loan)), all = CreditRules.OwedNow(sim, loan);
            vm.Lines.Add(new LineVm(s.Get("repay.next"), ctx.Fmt.Coins(next), "loan", loanId)
            { Action = ActionVm.Checked(ctx, "repayNext", s.Get("repay.pay"), ctx.Fmt.Coins(next), ActionKind.Primary, at => new RepayLoan(at, loanId, next)) });
            vm.Lines.Add(new LineVm(s.Get("repay.all"), ctx.Fmt.Coins(all), "loan", loanId)
            { Action = ActionVm.Checked(ctx, "repayAll", s.Get("repay.pay"), ctx.Fmt.Coins(all), ActionKind.Primary, at => new RepayLoan(at, loanId, all)) });
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
