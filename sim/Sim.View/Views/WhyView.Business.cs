using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Businesses (spec 016): every number on the research, plan and business sheets explained in words.</summary>
    public static partial class WhyView
    {
        private static Business BusinessOf(ViewContext ctx, long id) { return ctx.Sim.State.Businesses?.Find(id); }

        /// <summary>The plan's type and who runs it, from <see cref="BusinessWords.PlanId"/>.</summary>
        private static BusinessRules.Projection PlanOf(ViewContext ctx, long id, out BusinessTypeDef t, out bool self)
        {
            t = BusinessRules.Defs(ctx.Sim).Types[(int)(id / 4)];
            self = id % 2 == 1;
            return BusinessRules.Projected(ctx.Sim, t, self, t.OwnPerDay, t.Fee, BusinessWords.LoanFor(ctx.Sim, t, id % 4 >= 2));
        }

        private static WhyVm BusinessArea(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BusinessDefs d = BusinessRules.Defs(sim);
            WhyVm vm = New(ctx, "why.businessArea.title", ctx.Fmt.Kg(BusinessRules.AreaGramsPerDay(sim)));
            Row(vm, ctx, "why.businessArea.people", ctx.Fmt.Integer(d.CatchmentPeople + (sim.State.Citizens?.Population ?? 0)), "town", ctx.Fmt.Integer(sim.State.Citizens?.Population ?? 0));
            Row(vm, ctx, "why.businessArea.each", ctx.Fmt.Kg((d.GrainPerPersonPerRealDay * sim.Config.Game.Population.RealDaysPerGameDay * Fixed.FromInt(1000)).RoundToLong()));
            Text(vm, ctx, "why.businessArea.hours");
            Text(vm, ctx, "why.businessArea.lost");
            vm.Footnote = ctx.Strings.Get("why.businessArea.lesson");
            return vm;
        }

        private static WhyVm BusinessFee(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BusinessDefs d = BusinessRules.Defs(sim);
            Fixed index = sim.State.Market.PriceIndex;
            WhyVm vm = New(ctx, "why.businessFee.title", ctx.Fmt.PricePerKg(d.NextVillageFee * index));
            Text(vm, ctx, "why.businessFee.next");
            Row(vm, ctx, "why.businessFee.most", ctx.Fmt.PricePerKg((d.NextVillageFee + d.FeeTolerance) * index));
            vm.Footnote = ctx.Strings.Get("why.businessFee.lesson");
            return vm;
        }

        private static WhyVm BusinessTrader(ViewContext ctx, long good)
        {
            Simulation sim = ctx.Sim;
            int g = (int)good;
            WhyVm vm = New(ctx, "why.businessTrader.title", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, g)), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey));
            Row(vm, ctx, "why.businessTrader.regional", ctx.Fmt.PricePerKg(MarketRules.RegionalPrice(sim, g)));
            Row(vm, ctx, "why.businessTrader.transport", ctx.Fmt.PricePerKg(MarketRules.TransportCost(sim, g)));
            Row(vm, ctx, "why.businessTrader.day", ctx.Fmt.Kg(sim.Config.Game.Market.Region.TraderBuyPerDay(g)));
            return vm;
        }

        private static WhyVm BusinessWage(ViewContext ctx, long type)
        {
            BusinessTypeDef t = BusinessRules.Defs(ctx.Sim).Types[(int)type];
            WhyVm vm = New(ctx, "why.businessWage.title", ctx.Fmt.Coins(BusinessRules.Wage(ctx.Sim, t)), "role", ctx.Strings.Get("business.role." + t.Worker));
            Text(vm, ctx, "why.businessWage.paid");
            Text(vm, ctx, "why.businessWage.who");
            Text(vm, ctx, "why.businessWage.you");
            return vm;
        }

        private static WhyVm BusinessOwn(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            WhyVm vm = New(ctx, "why.businessOwn.title", ctx.Fmt.Kg(b.OwnPerDay));
            Row(vm, ctx, "why.businessOwn.price", ctx.Fmt.PricePerKg(MarketRules.Floor(ctx.Sim, t.Input)));
            Text(vm, ctx, "why.businessOwn.books");
            Text(vm, ctx, "why.businessOwn.yields");
            vm.Footnote = ctx.Strings.Get("why.businessOwn.lesson");
            return vm;
        }

        private static WhyVm BusinessWorker(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessWorker.title", BusinessWords.Worker(ctx, b));
            Text(vm, ctx, b.Worker == WorkerKind.Founder ? "why.businessWorker.you" : b.Worker == WorkerKind.Neighbour ? "why.businessWorker.neighbour" : "why.businessWorker.town");
            Text(vm, ctx, "why.businessWorker.townJob");
            return vm;
        }

        private static WhyVm BusinessHours(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessHours.title", BusinessWords.Hours(ctx, b.OpenHour, b.CloseHour));
            Text(vm, ctx, "why.businessHours.area");
            Text(vm, ctx, b.Worker == WorkerKind.Founder ? "why.businessHours.you" : "why.businessHours.worker");
            return vm;
        }

        private static WhyVm BusinessMarket(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            WhyVm vm = New(ctx, "why.businessMarket.title", ctx.Fmt.PricePerKg(MarketRules.Ceiling(ctx.Sim, t.Input)));
            Text(vm, ctx, "why.businessMarket.price");
            vm.Footnote = ctx.Strings.Get("why.businessMarket.lesson");
            return vm;
        }

        private static WhyVm BusinessCash(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessCash.title", ctx.Fmt.Coins(b.Ledger.Cash));
            Text(vm, ctx, "why.businessCash.own");
            Text(vm, ctx, "why.businessCash.flow");
            vm.Footnote = ctx.Strings.Get("why.businessCash.lesson");
            return vm;
        }

        /// <summary>One plan line's *Why?*: its value, how it's reckoned, and the plan's lesson.</summary>
        private static WhyVm PlanLine(ViewContext ctx, long id, string line)
        {
            BusinessRules.Projection p = PlanOf(ctx, id, out BusinessTypeDef t, out bool self);
            Fixed index = ctx.Sim.State.Market.PriceIndex;
            int days = ctx.Sim.Clock.DaysPerSeason;
            long value = line == "loan" ? (p.Loan > 0 ? p.Loan : BusinessBooks.MaxLoan(ctx.Sim, t)) : line == "interest" ? p.Interest : line == "cash" ? p.CashNeeded - p.Loan : line == "fees" ? p.Fees : line == "sales" ? p.Sales : line == "wheat" ? p.Wheat : line == "power" ? p.Power : line == "repairs" ? p.Repairs
                : line == "depreciation" ? p.Depreciation : line == "wage" ? p.Wage : line == "profit" ? p.Profit : line == "cost" ? p.Cost : p.CashNeeded;
            WhyVm vm = New(ctx, "why.plan." + line + ".title", line == "payback" ? BusinessPlanSheet.Payback(ctx, p) : ctx.Fmt.Coins(value), "role", ctx.Strings.Get("business.role." + t.Worker));
            Text(vm, ctx, "why.plan." + line, "days", ctx.Fmt.Integer(days), "area", ctx.Fmt.Kg(p.AreaGrams / days), "own", ctx.Fmt.Kg(p.OwnGrams / days),
                "fee", ctx.Fmt.PricePerKg(t.Fee * index), "power", ctx.Fmt.PricePerKg(t.PowerPerKg * index), "share", ctx.Fmt.Percent(t.RepairsShare),
                "cost", ctx.Fmt.Coins(p.Cost), "profit", ctx.Fmt.Coins(p.Profit), "role", ctx.Strings.Get("business.role." + t.Worker),
                "loan", ctx.Fmt.Coins(p.Loan), "ltv", ctx.Fmt.Percent(BusinessRules.Defs(ctx.Sim).BusinessLtv), "seasons", ctx.Fmt.Integer(BusinessRules.Defs(ctx.Sim).TermSeasons),
                "rate", ctx.Fmt.Percent(p.Loan > 0 ? Fixed.FromRatio(p.Interest, p.Loan) : Fixed.Zero));
            if (line == "profit" || line == "payback") vm.Footnote = ctx.Strings.Get("why.plan.lesson");
            return vm;
        }

        /// <summary>A line of a business's books (US-116): what it counts, the figures behind it, the statement's lesson.</summary>
        private static WhyVm BooksLine(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessBooksSheet.BusinessOf(ctx, id, out int back, out int line);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            Sim.Core.Business.BusinessReport r = BusinessBooksSheet.ReportOf(ctx, b, back);
            long Amount(int l) { return BusinessBooksSheet.Amount(r, l); }
            Fixed index = sim.State.Market.PriceIndex;
            string when = back == 0 ? ctx.Strings.Get("why.books.now") : ReportSheet.PeriodName(ctx, r.Period);
            WhyVm vm = New(ctx, "why.books.title", Money(ctx, Amount(line)), "line", BusinessBooksSheet.Label(ctx, b, line), "period", when);
            Text(vm, ctx, "why.books." + BusinessBooksSheet.Names[line], "area", ctx.Fmt.Kg(r.AreaGrams), "own", ctx.Fmt.Kg(r.OwnGrams), "kg", ctx.Fmt.Kg(r.AreaGrams + r.OwnGrams),
                "fee", ctx.Fmt.PricePerKg(b.Fee * index), "power", ctx.Fmt.PricePerKg(t.PowerPerKg * index), "share", ctx.Fmt.Percent(t.RepairsShare),
                "building", Money(ctx, MarketRules.Indexed(sim, t.BuildingValue)), "buildingLife", ctx.Fmt.Integer(t.BuildingLifeSeasons),
                "equipment", Money(ctx, MarketRules.Indexed(sim, t.EquipmentValue)), "equipmentLife", ctx.Fmt.Integer(t.EquipmentLifeSeasons),
                "role", ctx.Strings.Get("business.role." + t.Worker));
            if (line == BusinessBooksSheet.Margin || line == BusinessBooksSheet.Profit)
            {
                Row(vm, ctx, "why.books.rowIncome", Money(ctx, Amount(BusinessBooksSheet.Fees) + Amount(BusinessBooksSheet.Sales)));
                Row(vm, ctx, "why.books.rowGoods", "−" + Money(ctx, Amount(BusinessBooksSheet.Goods)));
                if (line == BusinessBooksSheet.Profit)
                    Row(vm, ctx, "why.books.rowRunning", "−" + Money(ctx, Amount(BusinessBooksSheet.Wages) + Amount(BusinessBooksSheet.Power) + Amount(BusinessBooksSheet.Repairs)
                        + Amount(BusinessBooksSheet.Depreciation) + Amount(BusinessBooksSheet.Interest)));
            }
            if (line >= BusinessBooksSheet.CashStart && line <= BusinessBooksSheet.CashEnd)
            {
                Row(vm, ctx, "why.books.rowStart", Money(ctx, r.Opening));
                Row(vm, ctx, "why.books.rowIn", Money(ctx, r.Inflows));
                Row(vm, ctx, "why.books.rowOut", "−" + Money(ctx, r.Outflows));
                vm.Footnote = ctx.Strings.Get("why.books.cashLesson");
            }
            else if (line >= BusinessBooksSheet.Cash)
            {
                Row(vm, ctx, "why.books.rowOwns", Money(ctx, Amount(BusinessBooksSheet.Cash) + Amount(BusinessBooksSheet.Stock) + Amount(BusinessBooksSheet.Fixed)));
                Row(vm, ctx, "why.books.rowOwes", "−" + Money(ctx, Amount(BusinessBooksSheet.Debts)));
                Row(vm, ctx, "why.books.rowCapital", Money(ctx, Amount(BusinessBooksSheet.Capital)));
                Row(vm, ctx, "why.books.rowKept", Money(ctx, Amount(BusinessBooksSheet.Kept)));
                vm.Footnote = ctx.Strings.Get("why.books.balanceLesson");
            }
            else vm.Footnote = ctx.Strings.Get("why.books.pnlLesson");
            return vm;
        }

        /// <summary>Plan vs actual (US-116, AC-14): a line's plan and its average over the last 4 seasons, and why they differ.</summary>
        private static WhyVm BooksPlan(ViewContext ctx, long id)
        {
            Business b = BusinessBooksSheet.BusinessOf(ctx, id, out int _, out int line);
            int full = BusinessBooksSheet.FullSeasons(ctx.Sim, b), need = Sim.Core.Business.BusinessBooks.KeptReports;
            if (full < need)
            {
                WhyVm wait = New(ctx, "why.booksPlan.waitTitle", ctx.Strings.Get("books.pvaSoFar", "n", ctx.Fmt.Integer(full), "of", ctx.Fmt.Integer(need)));
                Text(wait, ctx, "why.booksPlan.wait", "n", ctx.Fmt.Integer(need));
                wait.Footnote = ctx.Strings.Get("why.booksPlan.lesson");
                return wait;
            }
            long actual = BusinessBooksSheet.Average(b, line), plan = BusinessBooksSheet.Planned(b.Plan, line);
            WhyVm vm = New(ctx, "why.booksPlan.title", Money(ctx, actual), "line", BusinessBooksSheet.Label(ctx, b, line));
            Row(vm, ctx, "why.booksPlan.plan", Money(ctx, plan));
            Row(vm, ctx, "why.booksPlan.actual", Money(ctx, actual), "n", ctx.Fmt.Integer(need));
            vm.Rows.Add(new WhyRowVm(BusinessBooksSheet.Reason(ctx, b, line), null));
            vm.Footnote = ctx.Strings.Get("why.booksPlan.lesson");
            return vm;
        }

        /// <summary>The overdraft (spec 016 §3.8, §3.10): what's drawn, its limit and why, its interest, what happens past it.</summary>
        private static WhyVm BusinessOverdraft(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessDefs d = BusinessRules.Defs(sim);
            long limit = BusinessBooks.OverdraftLimit(sim, b);
            WhyVm vm = New(ctx, "why.businessOverdraft.title", ctx.Fmt.Coins(BusinessBooks.Overdrawn(b)));
            Row(vm, ctx, "why.businessOverdraft.limit", ctx.Fmt.Coins(limit), "pct", ctx.Fmt.Percent(d.OverdraftLimit));
            Row(vm, ctx, "why.businessOverdraft.rate", ctx.Fmt.Percent(d.OverdraftRate));
            Text(vm, ctx, "why.businessOverdraft.draws");
            Text(vm, ctx, "why.businessOverdraft.past");
            vm.Footnote = ctx.Strings.Get("why.businessOverdraft.lesson");
            return vm;
        }

        /// <summary>Closing a business (spec 016 §3.10): each figure and where the money goes.</summary>
        private static WhyVm BusinessClose(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessDefs d = BusinessRules.Defs(sim);
            BusinessCloseSheet.Figures(sim, b, out long stock, out long sale, out long debts, out long net);
            WhyVm vm = New(ctx, net >= 0 ? "why.businessClose.toYou" : "why.businessClose.yourLoan", ctx.Fmt.Coins(System.Math.Abs(net)));
            Text(vm, ctx, "why.businessClose.stock");
            Text(vm, ctx, "why.businessClose.sale", "pct", ctx.Fmt.Percent(d.ClosingSaleShare));
            Text(vm, ctx, "why.businessClose.debts", "seasons", ctx.Fmt.Integer(d.GuaranteeTermSeasons));
            Text(vm, ctx, "why.businessClose.worker");
            vm.Footnote = ctx.Strings.Get("why.businessClose.lesson");
            return vm;
        }

        /// <summary>Running it yourself (spec 016 §3.6, §6): the hours it takes off your farm, and what they'd earn as day labour.</summary>
        private static WhyVm PlanHours(ViewContext ctx, long id)
        {
            PlanOf(ctx, id, out BusinessTypeDef t, out bool _);
            BusinessPlanSheet.HoursOff(ctx.Sim, t, out int hours, out long value);
            WhyVm vm = New(ctx, "why.planHours.title", ctx.Fmt.Coins(value));
            Row(vm, ctx, "why.planHours.hours", ctx.Strings.Get("plan.hoursShort", "hours", ctx.Fmt.Integer(hours)));
            Row(vm, ctx, "why.planHours.wage", ctx.Fmt.Coins(MarketRules.Indexed(ctx.Sim, ctx.Sim.Config.Game.Farm.Founder.DayLabourWagePerHour)));
            Text(vm, ctx, "why.planHours.farm");
            vm.Footnote = ctx.Strings.Get("why.planHours.lesson");
            return vm;
        }
    }
}
