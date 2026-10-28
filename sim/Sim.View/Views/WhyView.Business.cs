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
            return BusinessRules.Projected(ctx.Sim, t, self, BusinessRules.DefaultOwnPerDay(ctx.Sim, t), t.Fee, BusinessWords.LoanFor(ctx.Sim, t, id % 4 >= 2));
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
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            WhyVm vm = New(ctx, "why.businessHours.title", BusinessWords.Hours(ctx, b.OpenHour, b.CloseHour));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.businessHours.area", t));
            Text(vm, ctx, b.Worker == WorkerKind.Founder ? BusinessWords.Key(ctx, "why.businessHours.you", t) : "why.businessHours.worker");
            return vm;
        }

        private static WhyVm BusinessMarket(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            int input = t.ByRecipe ? BusinessRules.BoughtInputs(t)[0] : t.Input;
            WhyVm vm = New(ctx, BusinessWords.Key(ctx, "why.businessMarket.title", t),
                t.TakesMilk ? ctx.Fmt.PricePerLitre(BusinessRules.NeighbourMilkPrice(ctx.Sim, t)) : ctx.Fmt.PricePerKg(MarketRules.Ceiling(ctx.Sim, input)));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.businessMarket.price", t));
            vm.Footnote = ctx.Strings.Get(BusinessWords.Key(ctx, "why.businessMarket.lesson", t));
            return vm;
        }

        private static WhyVm BusinessCash(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            WhyVm vm = New(ctx, BusinessWords.Key(ctx, "why.businessCash.title", t), ctx.Fmt.Coins(b.Ledger.Cash));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.businessCash.own", t));
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
            RecipeDef first = t.ByRecipe ? BusinessRules.First(t, t.Products[0]) : null;
            WhyVm vm = New(ctx, BusinessWords.Key(ctx, "why.plan." + line + ".title", t), line == "payback" ? BusinessPlanSheet.Payback(ctx, p) : ctx.Fmt.Coins(value),
                "role", ctx.Strings.Get("business.role." + t.Worker));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.plan." + line, t), "days", ctx.Fmt.Integer(days), "area", ctx.Fmt.Kg(p.AreaGrams / days), "own", ctx.Fmt.Kg(p.OwnGrams / days),
                "fee", ctx.Fmt.PricePerKg(t.Fee * index), "power", ctx.Fmt.PricePerKg(t.PowerPerKg * index), "share", ctx.Fmt.Percent(t.RepairsShare),
                "cost", ctx.Fmt.Coins(p.Cost), "profit", ctx.Fmt.Coins(p.Profit), "role", ctx.Strings.Get("business.role." + t.Worker),
                "loan", ctx.Fmt.Coins(p.Loan), "ltv", ctx.Fmt.Percent(BusinessRules.Defs(ctx.Sim).BusinessLtv), "seasons", ctx.Fmt.Integer(BusinessRules.Defs(ctx.Sim).TermSeasons),
                "rate", ctx.Fmt.Percent(p.Loan > 0 ? Fixed.FromRatio(p.Interest, p.Loan) : Fixed.Zero),
                "batch", first == null ? "" : ctx.Fmt.Kg(first.OutputGrams), "fuel", first == null ? "" : ctx.Fmt.Coins(Sim.Core.Finance.Money.MulRate(100, first.Fuel * index)),
                "supplies", first == null ? "" : ctx.Fmt.Coins(Sim.Core.Finance.Money.MulRate(100, first.Supplies * index)));
            if (line == "profit" || line == "payback") vm.Footnote = ctx.Strings.Get(BusinessWords.Key(ctx, "why.plan.lesson", t));
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
            Text(vm, ctx, BusinessWords.Key(ctx, "why.books." + BusinessBooksSheet.Names[line], t), "area", ctx.Fmt.Kg(r.AreaGrams), "own", ctx.Fmt.Kg(r.OwnGrams), "kg", ctx.Fmt.Kg(r.AreaGrams + r.OwnGrams),
                "fee", ctx.Fmt.PricePerKg(b.Fee * index), "power", ctx.Fmt.PricePerKg(t.PowerPerKg * index), "share", ctx.Fmt.Percent(t.RepairsShare),
                "building", Money(ctx, MarketRules.Indexed(sim, t.BuildingValue)), "buildingLife", ctx.Fmt.Integer(t.BuildingLifeSeasons),
                "equipment", Money(ctx, MarketRules.Indexed(sim, t.EquipmentValue)), "equipmentLife", ctx.Fmt.Integer(t.EquipmentLifeSeasons),
                "role", ctx.Strings.Get("business.role." + t.Worker));
            if (line == BusinessBooksSheet.Margin || line == BusinessBooksSheet.Profit)
            {
                Row(vm, ctx, BusinessWords.Key(ctx, "why.books.rowIncome", t), Money(ctx, Amount(BusinessBooksSheet.Fees) + Amount(BusinessBooksSheet.Sales)));
                Row(vm, ctx, BusinessWords.Key(ctx, "why.books.rowGoods", t), "−" + Money(ctx, Amount(BusinessBooksSheet.Goods)));
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
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            WhyVm vm = New(ctx, "why.businessOverdraft.title", ctx.Fmt.Coins(BusinessBooks.Overdrawn(b)));
            Row(vm, ctx, "why.businessOverdraft.limit", ctx.Fmt.Coins(limit), "pct", ctx.Fmt.Percent(d.OverdraftLimit));
            Row(vm, ctx, "why.businessOverdraft.rate", ctx.Fmt.Percent(d.OverdraftRate));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.businessOverdraft.draws", t));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.businessOverdraft.past", t));
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
            Text(vm, ctx, BusinessWords.Key(ctx, "why.businessClose.stock", BusinessRules.TypeOf(sim, b)));
            Text(vm, ctx, "why.businessClose.sale", "pct", ctx.Fmt.Percent(d.ClosingSaleShare));
            Text(vm, ctx, "why.businessClose.debts", "seasons", ctx.Fmt.Integer(d.GuaranteeTermSeasons));
            Text(vm, ctx, "why.businessClose.worker");
            vm.Footnote = ctx.Strings.Get("why.businessClose.lesson");
            return vm;
        }

        /// <summary>The people the village shops serve (§3.9c): the area's and your town's.</summary>
        private static WhyVm BusinessPeople(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.businessPeople.title", ctx.Fmt.Integer(BusinessRules.AreaPeople(sim)));
            Row(vm, ctx, "why.businessPeople.area", ctx.Fmt.Integer(BusinessRules.Defs(sim).CatchmentPeople));
            Row(vm, ctx, "why.businessPeople.town", ctx.Fmt.Integer(sim.State.Citizens?.Population ?? 0));
            Text(vm, ctx, "why.businessPeople.shops");
            return vm;
        }

        /// <summary>What the shops buy of a product each morning (§3.9c): a person's amount, the days of life in a real day, the people.</summary>
        private static WhyVm RecipeShops(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[(int)(id / 1000)];
            int good = (int)(id % 1000);
            Fixed each = Fixed.Zero;
            foreach (System.Collections.Generic.KeyValuePair<int, Fixed> kv in t.Shops) if (kv.Key == good) each = kv.Value;
            string name = BusinessWords.Good(ctx, good);
            WhyVm vm = New(ctx, "why.recipeShops.title", ctx.Fmt.Kg(BusinessRules.ShopGramsPerDay(sim, t, good)), "good", name);
            Row(vm, ctx, "why.recipeShops.each", ctx.Fmt.Kg((each * sim.Config.Game.Population.RealDaysPerGameDay * Fixed.FromInt(1000)).RoundToLong()));
            Row(vm, ctx, "why.recipeShops.people", ctx.Fmt.Integer(BusinessRules.AreaPeople(sim)));
            Row(vm, ctx, "why.recipeShops.price", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, good)));
            Text(vm, ctx, "why.recipeShops.round");
            vm.Footnote = ctx.Strings.Get(BusinessWords.Key(ctx, "why.recipeShops.lesson", t));
            return vm;
        }

        /// <summary>What an input costs (§3.9c): your mill's at the trader's price, else the market's import price.</summary>
        private static WhyVm RecipeInput(ViewContext ctx, long good)
        {
            Simulation sim = ctx.Sim;
            int g = (int)good;
            bool yours = BusinessRules.InputPrice(sim, g) == MarketRules.Floor(sim, g) && MarketRules.Floor(sim, g) != MarketRules.Ceiling(sim, g);
            WhyVm vm = New(ctx, "why.recipeInput.title", ctx.Fmt.PricePerKg(BusinessRules.InputPrice(sim, g)), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey));
            Row(vm, ctx, "why.recipeInput.trader", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, g)));
            Row(vm, ctx, "why.recipeInput.market", ctx.Fmt.PricePerKg(MarketRules.Ceiling(sim, g)));
            Text(vm, ctx, yours ? "why.recipeInput.yours" : "why.recipeInput.noMill");
            vm.Footnote = ctx.Strings.Get("why.recipeInput.lesson");
            return vm;
        }

        /// <summary>A product made today toward the day's mix (§3.9c), and the hourly rate that limits it.</summary>
        private static WhyVm RecipeMade(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            int good = (int)(id % 1000);
            RecipeDef r = BusinessRules.First(t, good);
            WhyVm vm = New(ctx, "why.recipeMade.title", ctx.Fmt.Kg(b.MadeToday[good]), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[good].NameKey));
            Row(vm, ctx, "why.recipeMade.mix", ctx.Fmt.Kg(b.Mix[good]));
            Row(vm, ctx, BusinessWords.Key(ctx, "why.recipeMade.rate", t), t.TakesMilk ? ctx.Fmt.Litres(t.GramsPerHour) : ctx.Fmt.Kg(t.GramsPerHour));
            Text(vm, ctx, BusinessWords.Key(ctx, "why.recipeMade.recipe." + sim.Config.Game.Market.Goods[good].Id, t), "batch", ctx.Fmt.Kg(r.OutputGrams));
            Text(vm, ctx, "why.recipeMade.round");
            return vm;
        }

        /// <summary>A product in stock (§3.9c): waiting for the 6:00 round, and how long it keeps.</summary>
        private static WhyVm RecipeStock(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            int good = (int)(id % 1000);
            GoodDef g = sim.Config.Game.Market.Goods[good];
            WhyVm vm = New(ctx, "why.recipeStock.title", ctx.Fmt.Kg(b.Stock[good]), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[good].NameKey));
            Text(vm, ctx, "why.recipeStock.round");
            Text(vm, ctx, g.SpoilagePerDay >= Fixed.One ? "why.recipeStock.fresh" : sim.Config.Game.Market.Region.TraderBuyPerDay(good) > 0 ? "why.recipeStock.keeps" : "why.recipeStock.part",
                "kg", ctx.Fmt.Kg(sim.Config.Game.Market.Region.TraderBuyPerDay(good)));
            return vm;
        }

        /// <summary>The day's mix for a product (§3.9c): what the choices mean.</summary>
        private static WhyVm RecipeMix(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            int good = (int)(id % 1000);
            long trader = sim.Config.Game.Market.Region.TraderBuyPerDay(good);
            WhyVm vm = New(ctx, "why.recipeMix.title", ctx.Fmt.Kg(b.Mix[good]), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[good].NameKey));
            Row(vm, ctx, "why.recipeMix.shops", ctx.Fmt.Kg(BusinessRules.ShopGramsPerDay(sim, t, good)));
            if (trader > 0) Row(vm, ctx, "why.recipeMix.trader", ctx.Fmt.Kg(trader));
            Text(vm, ctx, trader > 0 ? "why.recipeMix.keeps" : "why.recipeMix.fresh");
            if (!BusinessRules.Pays(sim, t, good))
                Text(vm, ctx, "why.recipeMix.loses", "cost", ctx.Fmt.PricePerKg(BusinessRules.UnitCost(sim, t, good)), "price", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, good)));
            vm.Footnote = ctx.Strings.Get("why.recipeMix.lesson");
            return vm;
        }

        /// <summary>Your herd's milk a day of one kind (§3.9b): what they give, what the house drinks, the co-op's price either way.</summary>
        private static WhyVm DairyMilk(ViewContext ctx, long kind)
        {
            Simulation sim = ctx.Sim;
            int k = (int)kind;
            var def = sim.Config.Game.Livestock.Animals[k];
            WhyVm vm = New(ctx, "why.dairyMilk.title", ctx.Fmt.Litres(BusinessRules.HerdMilk(sim)[k]), "kind", ctx.Strings.Get(def.NameKey));
            Row(vm, ctx, "why.dairyMilk.price", ctx.Fmt.PricePerLitre(Sim.Core.Livestock.LivestockRules.MilkPrice(sim, k)));
            Text(vm, ctx, "why.dairyMilk.herd");
            Text(vm, ctx, "why.dairyMilk.books");
            vm.Footnote = ctx.Strings.Get("why.dairyMilk.lesson");
            return vm;
        }

        /// <summary>Neighbours' milk (§3.9b): the co-op's price and the dairy's premium a litre.</summary>
        private static WhyVm DairyNeighbours(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[(int)type];
            int k = BusinessRules.NeighbourMilkKind(sim);
            WhyVm vm = New(ctx, "why.dairyNeighbours.title", ctx.Fmt.PricePerLitre(BusinessRules.NeighbourMilkPrice(sim, t)));
            Row(vm, ctx, "why.dairyNeighbours.coop", ctx.Fmt.PricePerLitre(Sim.Core.Livestock.LivestockRules.MilkPrice(sim, k)));
            Row(vm, ctx, "why.dairyNeighbours.premium", ctx.Fmt.PricePerLitre(t.NeighbourMilkPremium * sim.State.Market.PriceIndex));
            Text(vm, ctx, "why.dairyNeighbours.why");
            vm.Footnote = ctx.Strings.Get("why.dairyNeighbours.lesson");
            return vm;
        }

        /// <summary>The dairy's milk a day (§3.9b): what its mix needs, yours first, then neighbours'.</summary>
        private static WhyVm DairyMilkADay(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            long herd = 0;
            foreach (long ml in BusinessRules.HerdMilk(sim)) herd += ml;
            WhyVm vm = New(ctx, "why.dairyMilkADay.title", ctx.Fmt.Litres(b.OwnPerDay));
            Row(vm, ctx, "why.dairyMilkADay.need", ctx.Fmt.Litres(BusinessRules.MilkFor(t, b.Mix)));
            Row(vm, ctx, "why.dairyMilkADay.herd", ctx.Fmt.Litres(herd));
            Text(vm, ctx, b.BuyFromMarket ? "why.dairyMilkADay.neighbours" : "why.dairyMilkADay.yoursOnly");
            Text(vm, ctx, "why.dairyMilkADay.spoils");
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
