using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Words for businesses (spec 016 §7): a type's name, its worker, its hours.</summary>
    public static class BusinessWords
    {
        public static string Name(ViewContext ctx, BusinessTypeDef t) { return ctx.Strings.Get(t.NameKey); }

        /// <summary>The name inside a sentence: "Start a flour mill".</summary>
        public static string InSentence(ViewContext ctx, BusinessTypeDef t) { return Name(ctx, t).ToLowerInvariant(); }

        public static string Hours(ViewContext ctx, int open, int close) { return ctx.Strings.Get("business.hoursValue", "open", open + ":00", "close", close + ":00"); }

        /// <summary>"Manoj (miller)", "A worker from a neighbouring village", "You".</summary>
        public static string Worker(ViewContext ctx, Business b)
        {
            var s = ctx.Strings;
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            switch (b.Worker)
            {
                case WorkerKind.Founder: return s.Get("business.workerYou");
                case WorkerKind.Neighbour: return s.Get("business.workerNeighbour");
                case WorkerKind.Citizen:
                    Citizen c = ctx.Sim.State.Citizens.Citizen(b.WorkerCitizen);
                    return c == null ? s.Get("business.workerNone") : s.Get("business.workerCitizen", "name", TownWords.Name(ctx, c), "role", s.Get("business.role." + t.Worker));
                default: return s.Get(b.Status == BusinessStatus.Open ? "business.workerLeft" : "business.workerNone"); // open with nobody: the wage went unpaid
            }
        }

        /// <summary>A type's own wording where it has one ("business.cash.bakery"), else the shared one.</summary>
        public static string Key(ViewContext ctx, string key, BusinessTypeDef t)
        {
            string own = key + "." + t.Id;
            return ctx.Strings.Has(own) ? own : key;
        }

        /// <summary>The good's name in a sentence: "atta".</summary>
        public static string Good(ViewContext ctx, int good) { return ctx.Strings.Get(ctx.Sim.Config.Game.Market.Goods[good].NameKey).ToLowerInvariant(); }

        /// <summary>A recipe line's *Why?* id: the business or type, and the good.</summary>
        public static long GoodId(long owner, int good) { return owner * 1000 + good; }

        /// <summary>The plan's id for a *Why?*: the type, whether you run it, whether it borrows.</summary>
        public static long PlanId(int type, bool self, bool loan = false) { return type * 4 + (self ? 1 : 0) + (loan ? 2 : 0); }

        /// <summary>A plan's sheet argument: "hire" or "self", with ":loan" when it takes a business loan.</summary>
        public static string PlanArg(bool self, bool loan) { return (self ? "self" : "hire") + (loan ? ":loan" : ""); }

        public static bool Self(string arg) { return arg != null && arg.StartsWith("self", System.StringComparison.Ordinal); }

        public static bool Loan(string arg) { return arg != null && arg.EndsWith(":loan", System.StringComparison.Ordinal); }

        /// <summary>The business loan a plan takes: the most it can be (§3.8), or 0.</summary>
        public static long LoanFor(Simulation sim, BusinessTypeDef t, bool loan) { return loan ? BusinessBooks.MaxLoan(sim, t) : 0; }
    }

    /// <summary>Menu → Businesses (spec 016 §7): yours, one row each, and a picture button to start each type the era allows.</summary>
    public static class BusinessesSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "flour_mill", Title = s.Get("businesses.title"), Subtitle = s.Get("businesses.subtitle") };
            BusinessDefs d = BusinessRules.Defs(sim);
            if (d == null || sim.State.Businesses == null) return vm;
            var buttons = new List<ActionVm>();
            for (int i = 0; i < d.Types.Count; i++)
            {
                BusinessTypeDef t = d.Types[i];
                bool open = BusinessRules.Unlocked(sim, t);
                string locked = s.Get("businesses.locked." + sim.Config.Game.Onboarding.Eras[t.Era].Id);
                var a = new ActionVm("start:" + t.Id, s.Get("businesses.start", "name", BusinessWords.InSentence(ctx, t)),
                    s.Get("businesses.startDetail", "cost", ctx.Fmt.Coins(BusinessRules.Cost(sim, t))), ActionKind.Open, open, open ? null : locked, null,
                    new SheetRequest("BusinessResearch", i)) { Icon = t.Id };
                buttons.Add(a);
            }
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });
            vm.Lines.Add(LineVm.Header(s.Get("businesses.yours")));
            int n = 0;
            foreach (Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status == BusinessStatus.Closed) continue;
                n++;
                BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
                var line = new LineVm(BusinessWords.Name(ctx, t), BusinessSheet.StatusText(ctx, b), "businessHours", b.Id) { Icon = t.Id, Note = s.Get("businesses.rowNote", "cash", ctx.Fmt.Coins(b.Ledger.Cash)) };
                line.Action = ActionVm.Open("business:" + b.Id, s.Get("action.see"), new SheetRequest("Business", b.Id));
                vm.Lines.Add(line);
            }
            if (n == 0) vm.Lines.Add(new LineVm(s.Get(sim.State.Businesses.Businesses.Count > 0 ? "businesses.noneOpen" : "businesses.none"), s.Get("businesses.noneNote")));
            return vm;
        }
    }

    /// <summary>Market research (spec 016 §3.3 step 2): what the area brings, what the next village charges, prices today, a wage; nothing paid.</summary>
    public static class BusinessResearchSheet
    {
        public static SheetVm Build(ViewContext ctx, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[type];
            var vm = new SheetVm { Icon = t.Id, Title = s.Get("research.title", "name", BusinessWords.InSentence(ctx, t)), Subtitle = s.Get("research.subtitle") };
            if (t.ByRecipe) AddRecipeResearch(ctx, vm, t, type);
            else AddMillResearch(ctx, vm, t, type);
            vm.Lines.Add(new LineVm(s.Get(BusinessWords.Key(ctx, "research.cost", t)), ctx.Fmt.Coins(BusinessRules.Cost(sim, t)), "planCost", BusinessWords.PlanId(type, false)) { Icon = t.Id });
            vm.Actions.Add(ActionVm.Open("plan", s.Get("research.plan"), new SheetRequest("BusinessPlan", type, "hire")));
            return vm;
        }

        private static void AddMillResearch(ViewContext ctx, SheetVm vm, BusinessTypeDef t, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Fixed index = sim.State.Market.PriceIndex;
            vm.Lines.Add(new LineVm(s.Get("research.area"), s.Get("research.areaValue", "kg", ctx.Fmt.Kg(BusinessRules.AreaGramsPerDay(sim))), "businessArea", 0) { Icon = "wheat" });
            vm.Lines.Add(new LineVm(s.Get("research.nextVillage"), ctx.Fmt.PricePerKg(BusinessRules.Defs(sim).NextVillageFee * index), "businessFee", type) { Icon = "coin" });
            foreach (BusinessOutput o in t.Outputs)
            {
                GoodDef g = sim.Config.Game.Market.Goods[o.Good];
                vm.Lines.Add(new LineVm(s.Get("research.trader", "good", s.Get(g.NameKey)), ctx.Fmt.PricePerKg(MarketRules.Floor(sim, o.Good)), "businessTrader", o.Good) { Icon = g.Id });
            }
            vm.Lines.Add(new LineVm(s.Get("research.wage", "role", s.Get("business.role." + t.Worker)), s.Get("research.wageValue", "wage", ctx.Fmt.Coins(BusinessRules.Wage(sim, t))), "businessWage", type)
                { Icon = "person_hand_m" });
            vm.Lines.Add(new LineVm(s.Get("research.yours", "good", s.Get(sim.Config.Game.Market.Goods[t.Input].NameKey).ToLowerInvariant()), ctx.Fmt.Kg(sim.State.Market.Stock[t.Input]), "store", 0) { Icon = "sack" });
        }

        /// <summary>
        /// A bakery's or dairy's research (spec 016 §3.9c): the people the village shops serve, what the shops would buy of each product
        /// a day and at what price, what the trader takes of what keeps, what its inputs cost, a worker's wage.
        /// </summary>
        private static void AddRecipeResearch(ViewContext ctx, SheetVm vm, BusinessTypeDef t, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int town = sim.State.Citizens?.Population ?? 0;
            vm.Lines.Add(new LineVm(s.Get("research.people"), s.Get("research.peopleValue", "n", ctx.Fmt.Integer(BusinessRules.AreaPeople(sim)), "town", ctx.Fmt.Integer(town)),
                "businessPeople", type) { Icon = "population" });
            foreach (int p in t.Products)
            {
                GoodDef g = sim.Config.Game.Market.Goods[p];
                vm.Lines.Add(new LineVm(s.Get("research.shops", "good", BusinessWords.Good(ctx, p)), s.Get("research.shopsValue", "kg", ctx.Fmt.Kg(BusinessRules.ShopGramsPerDay(sim, t, p)),
                    "price", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, p))), "recipeShops", BusinessWords.GoodId(type, p)) { Icon = g.Id });
            }
            foreach (int p in t.Products)
            {
                if (sim.Config.Game.Market.Region.TraderBuyPerDay(p) <= 0) continue;
                GoodDef g = sim.Config.Game.Market.Goods[p];
                vm.Lines.Add(new LineVm(s.Get("research.traderTakes", "good", BusinessWords.Good(ctx, p)), s.Get("research.shopsValue",
                    "kg", ctx.Fmt.Kg(sim.Config.Game.Market.Region.TraderBuyPerDay(p)), "price", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, p))), "businessTrader", p) { Icon = g.Id });
            }
            foreach (int g in BusinessRules.BoughtInputs(t))
                vm.Lines.Add(new LineVm(s.Get("research.input", "good", s.Get(sim.Config.Game.Market.Goods[g].NameKey)), ctx.Fmt.PricePerKg(BusinessRules.InputPrice(sim, g)),
                    "recipeInput", g) { Icon = sim.Config.Game.Market.Goods[g].Id });
            vm.Lines.Add(new LineVm(s.Get("research.wage", "role", s.Get("business.role." + t.Worker)), s.Get("research.wageValue", "wage", ctx.Fmt.Coins(BusinessRules.Wage(sim, t))), "businessWage", type)
                { Icon = "person_hand_m" });
        }
    }

    /// <summary>
    /// The plan (spec 016 §3.3 step 3): who runs it, the season ahead line by line at today's prices (each with a *Why?*), the cash to
    /// put in and the payback; then choose the acre.
    /// </summary>
    public static class BusinessPlanSheet
    {
        public static SheetVm Build(ViewContext ctx, int type, bool self, bool loan = false)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[type];
            long borrow = BusinessWords.LoanFor(sim, t, loan);
            BusinessRules.Projection p = BusinessRules.Projected(sim, t, self, t.OwnPerDay, t.Fee, borrow);
            long id = BusinessWords.PlanId(type, self, loan);
            var vm = new SheetVm { Icon = t.Id, Title = s.Get("plan.title", "name", BusinessWords.InSentence(ctx, t)), Subtitle = s.Get("plan.subtitle") };
            var who = new LineVm(s.Get("plan.who"), s.Get(self ? "plan.whoYou" : "plan.whoHire", "role", s.Get("business.role." + t.Worker)));
            who.Action = ActionVm.Open("who", s.Get("plan.change"), new SheetRequest("BusinessPlan", type, BusinessWords.PlanArg(!self, loan)));
            vm.Lines.Add(who);
            // How it's paid for (§3.8): your cash, or a co-op bank business loan for 60% of the building and equipment.
            var funding = new LineVm(s.Get("plan.funding"), loan ? s.Get("plan.fundingLoan", "amount", ctx.Fmt.Coins(borrow)) : s.Get("plan.fundingCash"), "planLoan", id);
            string refused = loan ? null : LoanRefused(ctx, t);
            funding.Action = new ActionVm("funding", s.Get("plan.change"), null, ActionKind.Open, refused == null, refused, null,
                new SheetRequest("BusinessPlan", type, BusinessWords.PlanArg(self, !loan)));
            vm.Lines.Add(funding);
            vm.Lines.Add(LineVm.Header(s.Get("plan.season")));
            if (!t.ByRecipe) vm.Lines.Add(new LineVm(s.Get("plan.fees"), ctx.Fmt.Coins(p.Fees), "planFees", id));
            vm.Lines.Add(new LineVm(s.Get(BusinessWords.Key(ctx, "plan.sales", t)), ctx.Fmt.Coins(p.Sales), "planSales", id));
            vm.Lines.Add(new LineVm(s.Get(BusinessWords.Key(ctx, "plan.wheat", t), "kg", ctx.Fmt.Kg(t.OwnPerDay)), "−" + ctx.Fmt.Coins(p.Wheat), "planWheat", id));
            vm.Lines.Add(new LineVm(s.Get(BusinessWords.Key(ctx, "plan.power", t)), "−" + ctx.Fmt.Coins(p.Power), "planPower", id));
            vm.Lines.Add(new LineVm(s.Get("plan.repairs"), "−" + ctx.Fmt.Coins(p.Repairs), "planRepairs", id));
            vm.Lines.Add(new LineVm(s.Get("plan.depreciation"), "−" + ctx.Fmt.Coins(p.Depreciation), "planDepreciation", id));
            if (!self) vm.Lines.Add(new LineVm(s.Get("plan.wage", "role", s.Get("business.role." + t.Worker)), "−" + ctx.Fmt.Coins(p.Wage), "planWage", id));
            if (loan) vm.Lines.Add(new LineVm(s.Get("plan.interest"), "−" + ctx.Fmt.Coins(p.Interest), "planInterest", id));
            if (self)
            {
                // What your hours there are worth elsewhere: the farm work or day labour they replace (spec 016 §6, run it yourself).
                HoursOff(sim, t, out int hours, out long value);
                vm.Lines.Add(new LineVm(s.Get("plan.hours"), s.Get("plan.hoursValue", "hours", ctx.Fmt.Integer(hours), "value", ctx.Fmt.Coins(value)), "planHours", id));
            }
            vm.Lines.Add(new LineVm(s.Get("plan.profit"), ctx.Fmt.Coins(p.Profit), "planProfit", id) { NoteTone = p.Profit > 0 ? Tone.Good : Tone.Warn });
            vm.Lines.Add(LineVm.Header(s.Get("plan.money")));
            if (loan) vm.Lines.Add(new LineVm(s.Get("plan.loan"), s.Get("plan.loanValue", "amount", ctx.Fmt.Coins(borrow), "seasons", ctx.Fmt.Integer(BusinessRules.Defs(sim).TermSeasons)), "planLoan", id));
            long cash = p.CashNeeded - borrow;
            vm.Lines.Add(new LineVm(s.Get("plan.cash"), ctx.Fmt.Coins(cash), "planCash", id));
            vm.Lines.Add(new LineVm(s.Get("plan.payback"), Payback(ctx, p), "planPayback", id));
            bool can = sim.State.Ledger.CanAfford(cash);
            vm.Actions.Add(new ActionVm("place", s.Get("plan.place"), s.Get("plan.placeDetail", "cash", ctx.Fmt.Coins(cash)), ActionKind.Open, can,
                can ? null : s.Get("plan.short", "cash", ctx.Fmt.Coins(cash)), null, new SheetRequest("BusinessPlacement", type, BusinessWords.PlanArg(self, loan))));
            return vm;
        }

        /// <summary>The hours a day you'd work there (its day, up to yours) and what they'd earn a season as day labour.</summary>
        public static void HoursOff(Simulation sim, BusinessTypeDef t, out int hours, out long value)
        {
            hours = System.Math.Min(t.CloseHour - t.OpenHour, (int)sim.State.Farm.Founder.TodayHours.RoundToLong());
            value = Sim.Core.Finance.Money.MulRate(MarketRules.Indexed(sim, sim.Config.Game.Farm.Founder.DayLabourWagePerHour), Fixed.FromInt(hours * sim.Clock.DaysPerSeason));
        }

        /// <summary>Why a business loan can't be had now, in words, or null.</summary>
        internal static string LoanRefused(ViewContext ctx, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            if (sim.State.Credit == null) return ctx.Reject(RejectReason.UnknownLender);
            BusinessDefs d = BusinessRules.Defs(sim);
            int lender = sim.Config.Game.Credit.IndexOfLender(d.LenderId);
            var f = new Financing(d.LenderId, BusinessBooks.MaxLoan(sim, t), d.TermSeasons);
            if (lender < 0 || sim.State.Credit.Score < sim.Config.Game.Credit.Lenders[lender].MinScore) return ctx.Reject(RejectReason.ScoreTooLow);
            return f.Amount < sim.Config.Game.Credit.Lenders[lender].MinAmount ? ctx.Reject(RejectReason.BelowMinimum) : null;
        }

        internal static string Payback(ViewContext ctx, BusinessRules.Projection p)
        {
            if (p.Profit <= 0) return ctx.Strings.Get("plan.never");
            return ctx.Strings.Plural("plan.seasons", (p.Cost + p.Profit - 1) / p.Profit);
        }
    }

    /// <summary>Where a business may stand (spec 016 §3.3 step 4): every owned farm acre, lit where it may, the reason where not.</summary>
    public static class BusinessPlacement
    {
        public static List<HutPlaceVm> Places(ViewContext ctx)
        {
            var list = new List<HutPlaceVm>();
            Simulation sim = ctx.Sim;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                if (t.Terrain != Terrain.Farm || !sim.State.Land.Parcels[t.ParcelId].Owned) continue;
                RejectReason r = BusinessRules.Place(sim, t.Id);
                list.Add(new HutPlaceVm { TileId = t.Id, Allowed = r == RejectReason.None, Reason = r == RejectReason.None ? null : Reason(ctx, t.Id, r) });
            }
            return list;
        }

        public static string ReasonFor(ViewContext ctx, int tileId)
        {
            RejectReason r = BusinessRules.Place(ctx.Sim, tileId);
            return r == RejectReason.None ? null : Reason(ctx, tileId, r);
        }

        /// <summary>"Something is growing here", "A family house stands here", "A business stands here".</summary>
        private static string Reason(ViewContext ctx, int tileId, RejectReason r)
        {
            Simulation sim = ctx.Sim;
            if (r == RejectReason.TileOccupied && HouseRules.HousesOn(sim, tileId) > 0) return ctx.Strings.Get("business.placeHouse");
            if (r == RejectReason.TileOccupied) return ctx.Strings.Get("house.growing");
            if (r == RejectReason.NoRoom) return ctx.Strings.Get("business.placeTaken");
            return ctx.Reject(r);
        }

        /// <summary>Build here: founds the business with the plan's cash put in, and its business loan if the plan takes one.</summary>
        public static ActionVm Confirm(ViewContext ctx, int tileId, int type, bool self, bool loan = false)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[type];
            BusinessDefs d = BusinessRules.Defs(sim);
            long borrow = BusinessWords.LoanFor(sim, t, loan);
            long capital = BusinessRules.Projected(sim, t, self, t.OwnPerDay, t.Fee, borrow).CashNeeded - borrow;
            string id = t.Id, lender = d.LenderId;
            int term = d.TermSeasons;
            return ActionVm.Checked(ctx, "buildHere", ctx.Strings.Get("action.buildHere"), ctx.Fmt.Coins(capital), ActionKind.Primary,
                at => new FoundBusiness(at, id, tileId, capital, self, loan ? new Financing(lender, borrow, term) : null));
        }
    }

    /// <summary>A business's card (spec 016 §7): Today (status, the area's grinding, your wheat, stock, the worker, cash) and Settings.</summary>
    public static class BusinessSheet
    {
        public const string Today = "today", Books = "books", Settings = "settings";

        public static string StatusText(ViewContext ctx, Business b)
        {
            var s = ctx.Strings;
            if (b.Status == BusinessStatus.Building) return s.Get("business.building", "time", ctx.Fmt.Duration(b.ReadyAt - ctx.Now));
            if (b.Status == BusinessStatus.Closed) return s.Get("business.closed");
            int hour = (int)(ctx.Now % 86_400 / 3600);
            if (BusinessRules.OpenAt(b, hour)) return s.Get("business.openNow", "close", b.CloseHour + ":00");
            return s.Get("business.shutNow", "open", b.OpenHour + ":00");
        }

        public static SheetVm Build(ViewContext ctx, long id, string tab = null)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(id);
            if (b == null) return new SheetVm { Icon = "flour_mill", Title = s.Get("reject.UnknownBusiness") };
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            tab = tab ?? Today;
            var vm = new SheetVm { Icon = b.Status == BusinessStatus.Building ? t.Id + "_building" : t.Id, Title = BusinessWords.Name(ctx, t), Subtitle = StatusText(ctx, b), Tab = "tab:" + tab };
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:" + Today, s.Get("business.tabToday"), new SheetRequest("Business", id, Today)),
                ActionVm.Open("tab:" + Books, s.Get("business.tabBooks"), new SheetRequest("Business", id, Books)),
                ActionVm.Open("tab:" + Settings, s.Get("business.tabSettings"), new SheetRequest("Business", id, Settings)),
            };
            if (tab == Settings) AddSettings(ctx, vm, b, t);
            else if (tab == Books) BusinessBooksSheet.AddBooks(ctx, vm, b);
            else AddToday(ctx, vm, b, t);
            return vm;
        }

        private static void AddToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (t.ByRecipe) AddRecipeToday(ctx, vm, b, t);
            else AddMillToday(ctx, vm, b, t);
            AddMoney(ctx, vm, b, t);
        }

        private static void AddMillToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Fixed index = sim.State.Market.PriceIndex;
            bool today = b.Day == (ctx.Now - 3600) / 86_400 || b.Day == ctx.Now / 86_400;
            long area = today ? b.AreaToday : 0, own = today ? b.OwnToday : 0;
            vm.Lines.Add(new LineVm(s.Get("business.area"), s.Get("business.areaValue", "kg", ctx.Fmt.Kg(area), "fees", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(area, b.Fee * index, 1000))),
                "businessArea", b.Id) { Icon = "wheat" });
            vm.Lines.Add(new LineVm(s.Get("business.own", "good", s.Get(sim.Config.Game.Market.Goods[t.Input].NameKey).ToLowerInvariant()), s.Get("business.ownValue", "kg", ctx.Fmt.Kg(own), "of", ctx.Fmt.Kg(b.OwnPerDay)),
                "businessOwn", b.Id) { Icon = "sack" });
            foreach (BusinessOutput o in t.Outputs)
            {
                GoodDef g = sim.Config.Game.Market.Goods[o.Good];
                vm.Lines.Add(new LineVm(s.Get("business.stock", "good", s.Get(g.NameKey)), ctx.Fmt.Kg(b.Stock[o.Good]), "businessTrader", o.Good) { Icon = g.Id });
            }
        }

        /// <summary>A bakery's or dairy's day (§3.9c): each product made toward the day's mix, what's in stock for the 6:00 round, its inputs.</summary>
        private static void AddRecipeToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            foreach (int p in t.Products)
                vm.Lines.Add(new LineVm(s.Get("business.made", "good", s.Get(sim.Config.Game.Market.Goods[p].NameKey)), s.Get("business.madeValue", "kg", ctx.Fmt.Kg(b.MadeToday[p]),
                    "of", ctx.Fmt.Kg(b.Mix[p])), "recipeMade", BusinessWords.GoodId(b.Id, p)) { Icon = sim.Config.Game.Market.Goods[p].Id });
            foreach (int p in t.Products)
                vm.Lines.Add(new LineVm(s.Get("business.stock", "good", s.Get(sim.Config.Game.Market.Goods[p].NameKey)), ctx.Fmt.Kg(b.Stock[p]), "recipeStock", BusinessWords.GoodId(b.Id, p))
                    { Icon = sim.Config.Game.Market.Goods[p].Id });
            foreach (int g in BusinessRules.BoughtInputs(t))
                vm.Lines.Add(new LineVm(s.Get("business.stock", "good", s.Get(sim.Config.Game.Market.Goods[g].NameKey)), ctx.Fmt.Kg(b.Stock[g]), "recipeInput", g)
                    { Icon = sim.Config.Game.Market.Goods[g].Id });
        }

        /// <summary>Who works there, its cash, overdraft and business loan.</summary>
        private static void AddMoney(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            vm.Lines.Add(new LineVm(s.Get("business.worker"), BusinessWords.Worker(ctx, b), "businessWorker", b.Id) { Icon = b.Worker == WorkerKind.Founder ? "person_founder" : "person_hand_m" });
            var cash = new LineVm(s.Get(BusinessWords.Key(ctx, "business.cash", t)), ctx.Fmt.Coins(b.Ledger.Cash), "businessCash", b.Id) { Icon = "coin" };
            if (BusinessBooks.Short(sim, b))
            {
                cash.Note = s.Get(b.Worker == WorkerKind.None ? "business.shortWorker" : BusinessWords.Key(ctx, "business.shortCash", t));
                cash.NoteTone = Tone.Warn;
            }
            vm.Lines.Add(cash);
            if (b.Status != BusinessStatus.Open) return;
            vm.Lines.Add(new LineVm(s.Get("business.overdraft"), s.Get("business.overdraftValue", "used", ctx.Fmt.Coins(BusinessBooks.Overdrawn(b)),
                "limit", ctx.Fmt.Coins(BusinessBooks.OverdraftLimit(sim, b))), "businessOverdraft", b.Id) { Icon = "coin" });
            Sim.Core.Finance.Loan loan = BusinessBooks.LoanOf(sim, b);
            if (loan == null || !loan.IsOpen) return;
            long loanId = loan.Id;
            string value = s.Get(loan.HasArrears ? "money.loanOverdue" : "money.loanValue", "owed", ctx.Fmt.Coins(Sim.Core.Finance.CreditRules.OwedNow(sim, loan)),
                "next", ctx.Fmt.Coins(Repay.NextPayment(sim, loan)));
            var row = new LineVm(s.Get("business.loan"), value, "loan", loan.Id) { Icon = "coin" };
            if (loan.HasArrears && !loan.Restructured)
                row.Action = ActionVm.Checked(ctx, "restructure", s.Get("business.restructure"), null, ActionKind.Secondary, at => new RestructureLoan(at, loanId));
            vm.Lines.Add(row);
        }

        // The choices each setting cycles through (spec 016 §3.4, §3.5): the village's usual day, a morning, a long day; fees round the
        // next village's; your wheat a day.
        private static readonly int[][] HourPresets = { new[] { 8, 18 }, new[] { 8, 14 }, new[] { 6, 20 } };
        private static readonly int[] FeePresets = { 4, 3, 5 };
        private static readonly long[] OwnPresets = { 200_000, 400_000, 0, 100_000 };

        private static void AddSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            long id = b.Id;
            int hi = 0;
            for (int i = 0; i < HourPresets.Length; i++) if (HourPresets[i][0] == b.OpenHour && HourPresets[i][1] == b.CloseHour) hi = i;
            int[] nextHours = HourPresets[(hi + 1) % HourPresets.Length];
            Fixed fee = b.Fee;
            long ownNow = b.OwnPerDay;
            bool market = b.BuyFromMarket;
            int open = b.OpenHour, close = b.CloseHour;

            var hours = new LineVm(s.Get("business.hours"), BusinessWords.Hours(ctx, open, close), "businessHours", id);
            hours.Action = ActionVm.Checked(ctx, "hours", s.Get("business.change"), BusinessWords.Hours(ctx, nextHours[0], nextHours[1]), ActionKind.Secondary,
                at => new SetBusiness(at, id, nextHours[0], nextHours[1], fee, ownNow, market));
            vm.Lines.Add(hours);
            if (t.ByRecipe)
            {
                AddMix(ctx, vm, b, t);
                if (BusinessRules.BoughtInputs(t).Count > 0) AddMarket(ctx, vm, b, t);
            }
            else AddMillSettings(ctx, vm, b, t);
            AddCash(ctx, vm, b, t);
        }

        private static void AddMillSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            int fi = System.Array.IndexOf(FeePresets, (int)b.Fee.RoundToLong());
            Fixed nextFee = Fixed.FromInt(FeePresets[(fi + 1) % FeePresets.Length]);
            int oi = System.Array.IndexOf(OwnPresets, b.OwnPerDay);
            long nextOwn = OwnPresets[(oi + 1) % OwnPresets.Length];
            Fixed fee = b.Fee;
            long ownNow = b.OwnPerDay;
            bool market = b.BuyFromMarket;
            int open = b.OpenHour, close = b.CloseHour;
            var feeLine = new LineVm(s.Get("business.fee"), ctx.Fmt.PricePerKg(fee * sim.State.Market.PriceIndex), "businessFee", b.Type);
            feeLine.Action = ActionVm.Checked(ctx, "fee", s.Get("business.change"), ctx.Fmt.PricePerKg(nextFee * sim.State.Market.PriceIndex), ActionKind.Secondary,
                at => new SetBusiness(at, id, open, close, nextFee, ownNow, market));
            vm.Lines.Add(feeLine);
            var own = new LineVm(s.Get("business.ownDay", "good", s.Get(sim.Config.Game.Market.Goods[t.Input].NameKey).ToLowerInvariant()), ctx.Fmt.Kg(ownNow), "businessOwn", id);
            own.Action = ActionVm.Checked(ctx, "own", s.Get("business.change"), ctx.Fmt.Kg(nextOwn), ActionKind.Secondary,
                at => new SetBusiness(at, id, open, close, fee, nextOwn, market));
            vm.Lines.Add(own);
            AddMarket(ctx, vm, b, t);
        }

        /// <summary>Buy inputs at the market (wheat for the mill, atta for the bakery), yes or no.</summary>
        private static void AddMarket(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            long id = b.Id;
            Fixed fee = b.Fee;
            long ownNow = b.OwnPerDay;
            bool market = b.BuyFromMarket;
            int open = b.OpenHour, close = b.CloseHour;
            var buy = new LineVm(s.Get(BusinessWords.Key(ctx, "business.market", t)), s.Get(market ? "business.yes" : "business.no"), "businessMarket", id);
            buy.Action = ActionVm.Checked(ctx, "market", s.Get(market ? "business.stopMarket" : "business.startMarket"), null, ActionKind.Secondary,
                at => new SetBusiness(at, id, open, close, fee, ownNow, !market));
            vm.Lines.Add(buy);
        }

        /// <summary>
        /// The day's mix (§3.9c): each product's kg a day, Change cycling what the shops buy, that and what the trader takes (for what
        /// keeps), half, and none.
        /// </summary>
        private static void AddMix(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            foreach (int p in t.Products)
            {
                List<long> presets = MixPresets(sim, t, p);
                int at0 = presets.IndexOf(b.Mix[p]);
                long next = presets[(at0 + 1) % presets.Count];
                string good = sim.Config.Game.Market.Goods[p].Id;
                var line = new LineVm(s.Get("business.mixDay", "good", s.Get(sim.Config.Game.Market.Goods[p].NameKey)), ctx.Fmt.Kg(b.Mix[p]), "recipeMix", BusinessWords.GoodId(id, p))
                    { Icon = good };
                line.Action = ActionVm.Checked(ctx, "mix:" + good, s.Get("business.change"), ctx.Fmt.Kg(next), ActionKind.Secondary, at => new SetMix(at, id, good, next));
                vm.Lines.Add(line);
            }
        }

        /// <summary>A product's mix choices: what the shops buy, plus what the trader takes if he takes it, half the shops', none.</summary>
        public static List<long> MixPresets(Simulation sim, BusinessTypeDef t, int good)
        {
            long shops = BusinessRules.ShopGramsPerDay(sim, t, good), trader = sim.Config.Game.Market.Region.TraderBuyPerDay(good);
            var list = new List<long> { shops };
            if (trader > 0) list.Add(shops + trader);
            if (shops / 2 > 0) list.Add(shops / 2);
            list.Add(0);
            return list;
        }

        private static void AddCash(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            // Cash in and out (§3.2): your cash into the business, or its cash out to you.
            vm.Lines.Add(new LineVm(s.Get("business.yourCash"), ctx.Fmt.Coins(sim.State.Ledger.Cash), "cash")
                { Action = ActionVm.Open("putCash", s.Get("business.putCash"), new SheetRequest("BusinessCash", id, BusinessCashSheet.Put)) });
            vm.Lines.Add(new LineVm(s.Get(BusinessWords.Key(ctx, "business.cash", t)), ctx.Fmt.Coins(b.Ledger.Cash), "businessCash", id)
                { Action = ActionVm.Open("takeCash", s.Get("business.takeCash"), new SheetRequest("BusinessCash", id, BusinessCashSheet.Take)) });
            vm.Lines.Add(new LineVm(s.Get("business.closeRow", "name", BusinessWords.InSentence(ctx, t)), null)
                { Action = ActionVm.Open("closeBusiness", s.Get("business.closeButton"), new SheetRequest("BusinessClose", id)) });
        }
    }
}
