using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>The money, market and credit sources (specs 004, 005).</summary>
    public static partial class WhyView
    {
        /// <summary>Today's cash movements by kind, in the order they first happened (spec 008 §3.3).</summary>
        private static WhyVm Cash(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.cash.title", Money(ctx, sim.State.Ledger.Cash));
            long dayStart = ctx.Now - ctx.Now % ClockDef.SecondsPerDay;
            var keys = new List<string>();
            var sums = new List<long>();
            foreach (JournalEntry e in sim.State.Ledger.Entries)
            {
                if (e.SimTime < dayStart) continue;
                long cash = 0;
                foreach (Posting p in e.Postings) if (p.Account == Account.Cash) cash += p.Amount;
                if (cash == 0) continue;
                int i = keys.IndexOf(e.DescriptionKey);
                if (i < 0) { keys.Add(e.DescriptionKey); sums.Add(cash); }
                else sums[i] += cash;
            }
            for (int i = 0; i < keys.Count; i++) vm.Rows.Add(new WhyRowVm(ctx.Strings.Get(keys[i]), SignedMoney(ctx, sums[i])));
            if (keys.Count == 0) Text(vm, ctx, "why.cash.quiet");
            vm.Footnote = ctx.Strings.Get("why.cash.lesson");
            return vm;
        }

        private static string SignedMoney(ViewContext ctx, long money) { return (money > 0 ? "+" : "") + ctx.Fmt.Coins(money); }

        private static WhyVm StoreFill(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            long used = Store.UsedGrams(sim), capacity = Store.CapacityGrams(sim);
            WhyVm vm = New(ctx, "why.store.title", ctx.Strings.Get("store.fill", "used", ctx.Fmt.Kg(used), "capacity", ctx.Fmt.Kg(capacity)));
            MarketState m = sim.State.Market;
            for (int g = 0; g < sim.Config.Game.Market.Goods.Count; g++)
            {
                if (m.Stock[g] == 0) continue;
                GoodDef good = sim.Config.Game.Market.Goods[g];
                vm.Rows.Add(new WhyRowVm(ctx.Strings.Get(good.NameKey), ctx.Fmt.Kg(Store.WeightGrams(good, m.Stock[g]))));
            }
            Text(vm, ctx, "why.store.room");
            vm.Footnote = ctx.Strings.Get("why.store.lesson");
            return vm;
        }

        /// <summary>A good's row in the Store: its trader price. id = the good's index.</summary>
        private static WhyVm Price(ViewContext ctx, long id) { return TraderPrice(ctx, id); }

        private static WhyVm Regional(ViewContext ctx, WhyVm vm, int g)
        {
            Simulation sim = ctx.Sim;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            Season s = SeasonNow(ctx);
            Row(vm, ctx, "why.price.base", Price(ctx, good.BasePrice));
            if (!good.PriceControlled) Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
            else Text(vm, ctx, "why.price.controlled");
            Row(vm, ctx, "why.price.season", Effect(ctx, sim.Config.Game.Market.Region.SeasonalFactor(g, (int)s)), "season", SeasonName(ctx, s));
            if (sim.Config.Game.Market.Truck != null && good.Kind == GoodKind.Produce && !good.PriceControlled)
                Row(vm, ctx, "why.price.today", Effect(ctx, sim.State.Market.DailyFactorOf(g))); // spec 011 §3.1b
            Row(vm, ctx, "why.price.regional", Price(ctx, MarketRules.RegionalPrice(sim, g)));
            return vm;
        }

        private static WhyVm TraderPrice(ViewContext ctx, long id)
        {
            int g = (int)id;
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.traderPrice.title", Price(ctx, MarketRules.Floor(sim, g)), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey));
            Regional(ctx, vm, g);
            Row(vm, ctx, "why.price.transport", "−" + Price(ctx, MarketRules.TransportCost(sim, g)));
            vm.Footnote = ctx.Strings.Get("why.traderPrice.lesson");
            return vm;
        }

        private static WhyVm MarketPrice(ViewContext ctx, long id)
        {
            int g = (int)id;
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.marketPrice.title", Price(ctx, sim.State.Market.MarketPrice(g)), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey));
            Regional(ctx, vm, g);
            Row(vm, ctx, "why.marketPrice.floor", Price(ctx, MarketRules.Floor(sim, g)));
            Row(vm, ctx, "why.marketPrice.ceiling", Price(ctx, MarketRules.Ceiling(sim, g)));
            long town = Sim.Core.People.HouseholdRules.TownDemandPerDay(sim, g);
            if (town > 0) Row(vm, ctx, "why.marketPrice.town", ctx.Fmt.Kg(town)); // spec 014 §3.7: your people buy here too
            Text(vm, ctx, sim.State.Market.OnOffer(g) > 0 ? "why.marketPrice.offered" : "why.marketPrice.demand");
            vm.Footnote = ctx.Strings.Get("why.marketPrice.lesson");
            return vm;
        }

        private static WhyVm TraderRoom(ViewContext ctx, long id)
        {
            int g = (int)id;
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.traderRoom.title", ctx.Fmt.Kg(MarketRules.TraderCapacityLeft(sim, g)));
            Row(vm, ctx, "why.traderRoom.day", ctx.Fmt.Kg(sim.Config.Game.Market.Region.TraderBuyPerDay(g)));
            Row(vm, ctx, "why.traderRoom.bought", ctx.Fmt.Kg(sim.State.Market.TraderBought(g)));
            TruckDef truck = sim.Config.Game.Market.Truck;
            if (truck != null)
                Row(vm, ctx, sim.State.Market.TruckHere ? "why.traderRoom.load" : "why.traderRoom.away",
                    ctx.Fmt.Kg(sim.State.Market.TruckHere ? truck.LoadGrams - sim.State.Market.TruckLoadToday : 0));
            vm.Footnote = ctx.Strings.Get("why.traderRoom.lesson");
            return vm;
        }

        private static WhyVm Spoilage(ViewContext ctx, long id)
        {
            int g = (int)id;
            Simulation sim = ctx.Sim;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            long free = sim.State.Market.InStore(g) - sim.State.Market.OnOffer(g);
            WhyVm vm = New(ctx, "why.spoilage.title", ctx.Fmt.Kg(Store.WeightGrams(good, (Fixed.FromInt(free) * good.SpoilagePerDay).RoundToLong())));
            Row(vm, ctx, "why.spoils.rate", Pct(ctx, good.SpoilagePerDay));
            Text(vm, ctx, "why.spoilage.offer");
            vm.Footnote = ctx.Strings.Get("why.spoilage.lesson");
            return vm;
        }

        /// <summary>An input's stock in the Store. id = the good's index.</summary>
        private static WhyVm Input(ViewContext ctx, long id)
        {
            int g = (int)id;
            Simulation sim = ctx.Sim;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            WhyVm vm = New(ctx, "why.input.title", ctx.Fmt.Kg(sim.State.Market.InStore(g)), "good", ctx.Strings.Get(good.NameKey));
            Row(vm, ctx, "why.input.booked", Money(ctx, sim.State.Market.StockCostOf(g)));
            Text(vm, ctx, "why.input.use");
            vm.Footnote = ctx.Strings.Get("why.input.lesson");
            return vm;
        }

        private static WhyVm InputPrice(ViewContext ctx, long id)
        {
            int g = (int)id;
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.inputPrice.title", Price(ctx, MarketRules.InputPrice(sim, g)), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey));
            Regional(ctx, vm, g);
            vm.Footnote = ctx.Strings.Get("why.inputPrice.lesson");
            return vm;
        }

        /// <summary>The mandi sheet's sum (id = the row: 0 the mandi price, 1 commission, 2 trolley hire, 3 your time, 4 the trader).</summary>
        private static WhyVm Mandi(ViewContext ctx, long id)
        {
            MandiDef d = ctx.Sim.Config.Game.Market.Mandi;
            WhyVm vm = New(ctx, "why.mandi.title", null);
            Text(vm, ctx, "why.mandi." + id, "hours", ctx.Fmt.Integer(d.TripHours), "pct", Pct(ctx, d.CommissionRate), "kg", ctx.Fmt.Kg(d.CartCapacityGrams));
            vm.Footnote = ctx.Strings.Get("why.mandi.lesson");
            return vm;
        }

        private static WhyVm NetWorth(ViewContext ctx, long id)
        {
            NetWorthSnapshot n = Reporting.NetWorth(ctx.Sim);
            WhyVm vm = New(ctx, "why.netWorth.title", Money(ctx, n.Total));
            Row(vm, ctx, "why.netWorth.cash", Money(ctx, n.Cash));
            if (n.Prepaid != 0) Row(vm, ctx, "why.netWorth.prepaid", Money(ctx, n.Prepaid));
            if (n.OwedByBuyers != 0) Row(vm, ctx, "why.netWorth.owed", Money(ctx, n.OwedByBuyers));
            Row(vm, ctx, "why.netWorth.land", Money(ctx, n.LandAtMarket));
            Row(vm, ctx, "why.netWorth.buildings", Money(ctx, n.BuildingsAtBook));
            if (n.TreesAtBook != 0) Row(vm, ctx, "why.netWorth.trees", Money(ctx, n.TreesAtBook));
            if (n.LivestockAtBook != 0) Row(vm, ctx, "why.netWorth.livestock", Money(ctx, n.LivestockAtBook));
            if (n.Businesses != 0) Row(vm, ctx, "why.netWorth.businesses", Money(ctx, n.Businesses));
            Row(vm, ctx, "why.netWorth.goods", Money(ctx, n.StoredGoods));
            if (n.LoanPrincipal + n.InterestPayable != 0) Row(vm, ctx, "why.netWorth.loans", "−" + Money(ctx, n.LoanPrincipal + n.InterestPayable));
            Row(vm, ctx, "why.netWorth.book", Money(ctx, n.BookEquity));
            vm.Footnote = ctx.Strings.Get("why.netWorth.lesson");
            return vm;
        }

        /// <summary>What a lender will lend (spec 005 §3.6). id = the lender's index.</summary>
        private static WhyVm CreditLimitWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LenderDef l = sim.Config.Game.Credit.Lenders[(int)id];
            CreditLimit c = CreditRules.Limit(sim, (int)id);
            WhyVm vm = New(ctx, "why.credit.title", Money(ctx, c.Limit), "lender", ctx.Strings.Get(l.NameKey));
            if (l.BaseLimit > 0) Row(vm, ctx, "why.credit.base", Money(ctx, l.BaseLimit));
            if (l.Secured)
            {
                Row(vm, ctx, "why.credit.land", Money(ctx, Sim.Core.Finance.Money.MulRate(c.Collateral, l.CollateralLtv)), "pct", Pct(ctx, l.CollateralLtv));
                Text(vm, ctx, "why.credit.inherited");
            }
            if (l.IncomeMultiple > Fixed.Zero)
                Row(vm, ctx, "why.credit.income", Money(ctx, Sim.Core.Finance.Money.MulRate(System.Math.Max(0, c.AverageFarmProfit), l.IncomeMultiple)));
            if (c.EraCap > 0 && c.RawLimit > c.EraCap) Row(vm, ctx, "why.credit.cap", Money(ctx, c.EraCap));
            Row(vm, ctx, "why.credit.score", Effect(ctx, c.ScoreFactor), "score", ctx.Fmt.Integer(sim.State.Credit.Score));
            if (c.ScoreTooLow) Text(vm, ctx, "why.credit.tooLow");
            if (c.Owed > 0) Row(vm, ctx, "why.credit.owed", "−" + Money(ctx, c.Owed));
            Row(vm, ctx, "why.credit.rate", Pct(ctx, CreditRules.Rate(l, sim.State.Credit.Score)));
            vm.Footnote = ctx.Strings.Get("why.credit.lesson");
            return vm;
        }

        /// <summary>A loan's row. id = the loan's id.</summary>
        private static WhyVm LoanWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Loan loan = sim.State.Credit.Loans.Find(x => x.Id == id);
            LenderDef l = sim.Config.Game.Credit.Lenders[loan.Lender];
            WhyVm vm = New(ctx, "why.loan.title", Money(ctx, CreditRules.OwedNow(sim, loan)), "lender", ctx.Strings.Get(l.NameKey));
            Row(vm, ctx, "why.loan.borrowed", Money(ctx, loan.OriginalPrincipal));
            Row(vm, ctx, "why.loan.principal", Money(ctx, loan.Principal));
            Row(vm, ctx, "why.loan.rate", Pct(ctx, loan.Rate));
            Row(vm, ctx, "why.loan.left", ctx.Fmt.Integer(loan.Remaining));
            Text(vm, ctx, loan.Schedule == LoanSchedule.Bullet ? "why.loan.bullet" : "why.loan.instalments");
            if (loan.HasArrears) Row(vm, ctx, "why.loan.arrears", Money(ctx, loan.ArrearsPrincipal + loan.ArrearsInterest));
            vm.Footnote = ctx.Strings.Get("why.loan.lesson");
            return vm;
        }

        /// <summary>A borrow term row: id = lender × 1,000 + term in seasons. The amount is the sheet's.</summary>
        private static WhyVm BorrowWhy(ViewContext ctx, long id)
        {
            LenderDef l = ctx.Sim.Config.Game.Credit.Lenders[(int)(id / 1000)];
            WhyVm vm = New(ctx, "why.borrow.title", ctx.Strings.Plural("why.borrow.seasons", (int)(id % 1000)));
            Row(vm, ctx, "why.credit.rate", Pct(ctx, CreditRules.Rate(l, ctx.Sim.State.Credit.Score)));
            Text(vm, ctx, l.Schedule == LoanSchedule.Bullet ? "why.loan.bullet" : "why.loan.instalments");
            vm.Footnote = ctx.Strings.Get("why.borrow.lesson");
            return vm;
        }

        /// <summary>This season so far (the open income and expense accounts).</summary>
        private static WhyVm SeasonSoFar(ViewContext ctx, long id)
        {
            Ledger ledger = ctx.Sim.State.Ledger;
            long income = -(ledger.Balance(Account.CropSales) + ledger.Balance(Account.FruitSales));
            long costs = ledger.Balance(Account.SeedsAndPlanting) + ledger.Balance(Account.FertiliserAndSpraying) + ledger.Balance(Account.HiredLabour)
                + ledger.Balance(Account.Depreciation) + ledger.Balance(Account.TransportAndSelling);
            WhyVm vm = New(ctx, "why.seasonSoFar.title", Money(ctx, income - costs));
            Row(vm, ctx, "report.farmIncome", Money(ctx, income));
            Row(vm, ctx, "report.farmCosts", "−" + Money(ctx, costs));
            Text(vm, ctx, "why.seasonSoFar.close");
            vm.Footnote = ctx.Strings.Get("why.seasonSoFar.lesson");
            return vm;
        }

        /// <summary>A season-report line (spec 008 §3.8): its plain text, then the accounts behind it. id = the report's period.</summary>
        private static WhyVm ReportLineWhy(ViewContext ctx, string line, long period)
        {
            SeasonReport r = ctx.Sim.State.Finance.Reports.Find(x => x.Period == period) ?? ctx.Sim.State.Finance.Reports[ctx.Sim.State.Finance.Reports.Count - 1];
            WhyVm vm = new WhyVm { Title = ctx.Strings.Get("report." + line) };
            if (line == "netWorth")
            {
                NetWorthSnapshot n = r.NetWorth;
                vm.Value = Money(ctx, n.Total);
                Row(vm, ctx, "why.netWorth.cash", Money(ctx, n.Cash));
                if (n.Prepaid != 0) Row(vm, ctx, "why.netWorth.prepaid", Money(ctx, n.Prepaid));
                Row(vm, ctx, "why.netWorth.land", Money(ctx, n.LandAtMarket));
                Row(vm, ctx, "why.netWorth.buildings", Money(ctx, n.BuildingsAtBook));
                if (n.TreesAtBook != 0) Row(vm, ctx, "why.netWorth.trees", Money(ctx, n.TreesAtBook));
            if (n.LivestockAtBook != 0) Row(vm, ctx, "why.netWorth.livestock", Money(ctx, n.LivestockAtBook));
                Row(vm, ctx, "why.netWorth.goods", Money(ctx, n.StoredGoods));
                if (n.LoanPrincipal + n.InterestPayable != 0) Row(vm, ctx, "why.netWorth.loans", "−" + Money(ctx, n.LoanPrincipal + n.InterestPayable));
                vm.Footnote = ctx.Strings.Get("why.netWorth.lesson");
                return vm;
            }
            if (line == "cashFlow")
            {
                vm.Value = Money(ctx, r.CashFlow.Closing);
                Row(vm, ctx, "why.cashFlow.start", Money(ctx, r.CashFlow.Opening));
                Row(vm, ctx, "why.cashFlow.in", "+" + Money(ctx, r.CashFlow.Inflows));
                Row(vm, ctx, "why.cashFlow.out", "−" + Money(ctx, r.CashFlow.Outflows));
                if (r.CashFlow.PaidUpfront > 0) Text(vm, ctx, "report.cashFlow.why", "upfront", Money(ctx, r.CashFlow.PaidUpfront));
                return vm;
            }
            ReportLine l = r.Line(line);
            vm.Value = Money(ctx, l.Amount);
            foreach (KeyValuePair<Account, long> kv in r.ByAccount)
            {
                bool mine = false;
                foreach (Account a in l.Accounts) if (a == kv.Key) mine = true;
                if (mine && kv.Value != 0) vm.Rows.Add(new WhyRowVm(ctx.Strings.Get("account." + kv.Key), Money(ctx, kv.Value)));
            }
            if (vm.Rows.Count == 0) Text(vm, ctx, "why.report.none");
            if (line == "farmIncome" && r.HomeUse > 0)
                Text(vm, ctx, "report.homeUse.why", "homeUse", Money(ctx, r.HomeUse), "garden", Money(ctx, r.GardenHomeUse));
            vm.Footnote = ctx.Strings.Get("why.report." + line);
            return vm;
        }
    }
}
