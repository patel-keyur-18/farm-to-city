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
                default: return s.Get("business.workerNone");
            }
        }

        /// <summary>The plan's id for a *Why?*: the type, and whether you run it.</summary>
        public static long PlanId(int type, bool self) { return type * 2 + (self ? 1 : 0); }
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
            if (n == 0) vm.Lines.Add(new LineVm(s.Get("businesses.none"), s.Get("businesses.noneNote")));
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
            vm.Lines.Add(new LineVm(s.Get("research.cost"), ctx.Fmt.Coins(BusinessRules.Cost(sim, t)), "planCost", BusinessWords.PlanId(type, false)) { Icon = t.Id });
            vm.Actions.Add(ActionVm.Open("plan", s.Get("research.plan"), new SheetRequest("BusinessPlan", type, "hire")));
            return vm;
        }
    }

    /// <summary>
    /// The plan (spec 016 §3.3 step 3): who runs it, the season ahead line by line at today's prices (each with a *Why?*), the cash to
    /// put in and the payback; then choose the acre.
    /// </summary>
    public static class BusinessPlanSheet
    {
        public static SheetVm Build(ViewContext ctx, int type, bool self)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[type];
            BusinessRules.Projection p = BusinessRules.Projected(sim, t, self, t.OwnPerDay, t.Fee);
            long id = BusinessWords.PlanId(type, self);
            var vm = new SheetVm { Icon = t.Id, Title = s.Get("plan.title", "name", BusinessWords.InSentence(ctx, t)), Subtitle = s.Get("plan.subtitle") };
            var who = new LineVm(s.Get("plan.who"), s.Get(self ? "plan.whoYou" : "plan.whoHire", "role", s.Get("business.role." + t.Worker)));
            who.Action = ActionVm.Open("who", s.Get("plan.change"), new SheetRequest("BusinessPlan", type, self ? "hire" : "self"));
            vm.Lines.Add(who);
            vm.Lines.Add(LineVm.Header(s.Get("plan.season")));
            vm.Lines.Add(new LineVm(s.Get("plan.fees"), ctx.Fmt.Coins(p.Fees), "planFees", id));
            vm.Lines.Add(new LineVm(s.Get("plan.sales"), ctx.Fmt.Coins(p.Sales), "planSales", id));
            vm.Lines.Add(new LineVm(s.Get("plan.wheat", "kg", ctx.Fmt.Kg(t.OwnPerDay)), "−" + ctx.Fmt.Coins(p.Wheat), "planWheat", id));
            vm.Lines.Add(new LineVm(s.Get("plan.power"), "−" + ctx.Fmt.Coins(p.Power), "planPower", id));
            vm.Lines.Add(new LineVm(s.Get("plan.repairs"), "−" + ctx.Fmt.Coins(p.Repairs), "planRepairs", id));
            vm.Lines.Add(new LineVm(s.Get("plan.depreciation"), "−" + ctx.Fmt.Coins(p.Depreciation), "planDepreciation", id));
            if (!self) vm.Lines.Add(new LineVm(s.Get("plan.wage", "role", s.Get("business.role." + t.Worker)), "−" + ctx.Fmt.Coins(p.Wage), "planWage", id));
            vm.Lines.Add(new LineVm(s.Get("plan.profit"), ctx.Fmt.Coins(p.Profit), "planProfit", id) { NoteTone = p.Profit > 0 ? Tone.Good : Tone.Warn });
            vm.Lines.Add(LineVm.Header(s.Get("plan.money")));
            vm.Lines.Add(new LineVm(s.Get("plan.cash"), ctx.Fmt.Coins(p.CashNeeded), "planCash", id));
            vm.Lines.Add(new LineVm(s.Get("plan.payback"), Payback(ctx, p), "planPayback", id));
            bool can = sim.State.Ledger.CanAfford(p.CashNeeded);
            vm.Actions.Add(new ActionVm("place", s.Get("plan.place"), s.Get("plan.placeDetail", "cash", ctx.Fmt.Coins(p.CashNeeded)), ActionKind.Open, can,
                can ? null : s.Get("plan.short", "cash", ctx.Fmt.Coins(p.CashNeeded)), null, new SheetRequest("BusinessPlacement", type, self ? "self" : "hire")));
            return vm;
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

        /// <summary>Build here: founds the business with the plan's cash put in.</summary>
        public static ActionVm Confirm(ViewContext ctx, int tileId, int type, bool self)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[type];
            long capital = BusinessRules.Projected(sim, t, self, t.OwnPerDay, t.Fee).CashNeeded;
            string id = t.Id;
            return ActionVm.Checked(ctx, "buildHere", ctx.Strings.Get("action.buildHere"), ctx.Fmt.Coins(capital), ActionKind.Primary, at => new FoundBusiness(at, id, tileId, capital, self));
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
            vm.Lines.Add(new LineVm(s.Get("business.worker"), BusinessWords.Worker(ctx, b), "businessWorker", b.Id) { Icon = b.Worker == WorkerKind.Founder ? "person_founder" : "person_hand_m" });
            vm.Lines.Add(new LineVm(s.Get("business.cash"), ctx.Fmt.Coins(b.Ledger.Cash), "businessCash", b.Id) { Icon = "coin" });
        }

        // The choices each setting cycles through (spec 016 §3.4, §3.5): the village's usual day, a morning, a long day; fees round the
        // next village's; your wheat a day.
        private static readonly int[][] HourPresets = { new[] { 8, 18 }, new[] { 8, 14 }, new[] { 6, 20 } };
        private static readonly int[] FeePresets = { 4, 3, 5 };
        private static readonly long[] OwnPresets = { 200_000, 400_000, 0, 100_000 };

        private static void AddSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            int hi = 0;
            for (int i = 0; i < HourPresets.Length; i++) if (HourPresets[i][0] == b.OpenHour && HourPresets[i][1] == b.CloseHour) hi = i;
            int[] nextHours = HourPresets[(hi + 1) % HourPresets.Length];
            int fi = System.Array.IndexOf(FeePresets, (int)b.Fee.RoundToLong());
            Fixed nextFee = Fixed.FromInt(FeePresets[(fi + 1) % FeePresets.Length]);
            int oi = System.Array.IndexOf(OwnPresets, b.OwnPerDay);
            long nextOwn = OwnPresets[(oi + 1) % OwnPresets.Length];
            Fixed fee = b.Fee;
            long ownNow = b.OwnPerDay;
            bool market = b.BuyFromMarket;
            int open = b.OpenHour, close = b.CloseHour;

            var hours = new LineVm(s.Get("business.hours"), BusinessWords.Hours(ctx, open, close), "businessHours", id);
            hours.Action = ActionVm.Checked(ctx, "hours", s.Get("business.change"), BusinessWords.Hours(ctx, nextHours[0], nextHours[1]), ActionKind.Secondary,
                at => new SetBusiness(at, id, nextHours[0], nextHours[1], fee, ownNow, market));
            vm.Lines.Add(hours);
            var feeLine = new LineVm(s.Get("business.fee"), ctx.Fmt.PricePerKg(fee * sim.State.Market.PriceIndex), "businessFee", b.Type);
            feeLine.Action = ActionVm.Checked(ctx, "fee", s.Get("business.change"), ctx.Fmt.PricePerKg(nextFee * sim.State.Market.PriceIndex), ActionKind.Secondary,
                at => new SetBusiness(at, id, open, close, nextFee, ownNow, market));
            vm.Lines.Add(feeLine);
            var own = new LineVm(s.Get("business.ownDay", "good", s.Get(sim.Config.Game.Market.Goods[t.Input].NameKey).ToLowerInvariant()), ctx.Fmt.Kg(ownNow), "businessOwn", id);
            own.Action = ActionVm.Checked(ctx, "own", s.Get("business.change"), ctx.Fmt.Kg(nextOwn), ActionKind.Secondary,
                at => new SetBusiness(at, id, open, close, fee, nextOwn, market));
            vm.Lines.Add(own);
            var buy = new LineVm(s.Get("business.market"), s.Get(market ? "business.yes" : "business.no"), "businessMarket", id);
            buy.Action = ActionVm.Checked(ctx, "market", s.Get(market ? "business.stopMarket" : "business.startMarket"), null, ActionKind.Secondary,
                at => new SetBusiness(at, id, open, close, fee, ownNow, !market));
            vm.Lines.Add(buy);

            // Cash in and out (§3.2): your cash into the business, or its cash out to you.
            vm.Lines.Add(new LineVm(s.Get("business.yourCash"), ctx.Fmt.Coins(sim.State.Ledger.Cash), "cash")
                { Action = ActionVm.Open("putCash", s.Get("business.putCash"), new SheetRequest("BusinessCash", id, BusinessCashSheet.Put)) });
            vm.Lines.Add(new LineVm(s.Get("business.cash"), ctx.Fmt.Coins(b.Ledger.Cash), "businessCash", id)
                { Action = ActionVm.Open("takeCash", s.Get("business.takeCash"), new SheetRequest("BusinessCash", id, BusinessCashSheet.Take)) });
        }
    }
}
