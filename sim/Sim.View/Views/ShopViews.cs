using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Words for shops (spec 017 §7): a pitch's name, a shop's terms, a rival's name, a share's *Why?* id.</summary>
    public static class ShopWords
    {
        /// <summary>"Pitch I", "Pitch II", "Pitch III".</summary>
        public static string Pitch(ViewContext ctx, int pitch) { return ctx.Strings.Get("shop.pitch", "n", Roman(pitch + 1)); }

        private static string Roman(int n) { return n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n == 4 ? "IV" : n == 5 ? "V" : n.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        public static string Rival(ViewContext ctx, RivalDef d) { return ctx.Strings.Get("shop.rival." + d.Id); }

        /// <summary>"10% below", "the reference", "20% above".</summary>
        public static string PriceSetting(ViewContext ctx, Fixed factor)
        {
            if (factor == Fixed.One) return ctx.Strings.Get("shop.priceReference");
            Fixed off = factor > Fixed.One ? factor - Fixed.One : Fixed.One - factor;
            return ctx.Strings.Get(factor > Fixed.One ? "shop.priceAbove" : "shop.priceBelow", "pct", ctx.Fmt.Percent(off));
        }

        /// <summary>A shop's price in its unit: a cup of chai, a kg of a sweet, or the stall's prices as a share of the reference.</summary>
        public static string Price(ViewContext ctx, ShopKind kind, Fixed factor)
        {
            Simulation sim = ctx.Sim;
            if (kind == ShopKind.Chai) return ctx.Strings.Get("shop.perCup", "price", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(1, ShopRules.Reference(sim, kind, -1) * factor, 1)));
            if (kind == ShopKind.Sweets)
            {
                int g = ShopRules.Defs(sim).Kind(kind).References[0].Key;
                return ctx.Fmt.PricePerKg(ShopRules.Reference(sim, kind, g) * factor);
            }
            return PriceSetting(ctx, factor);
        }

        /// <summary>A share's *Why?* id: a business, or a rival (1,000,000 + its index).</summary>
        public static long ShareId(Business b) { return b.Id; }
        public static long ShareId(int rival) { return RivalBase + rival; }
        public const long RivalBase = 1_000_000;

        /// <summary>"cups" or "kg" figures for a kind.</summary>
        public static string Amount(ViewContext ctx, ShopKind kind, long n) { return kind == ShopKind.Chai ? Cups(ctx, n) : ctx.Fmt.Kg(n); }

        public static string Cups(ViewContext ctx, long n) { return ctx.Strings.Get(n == 1 ? "shop.cups.one" : "shop.cups.other", "n", ctx.Fmt.Integer(n)); }

        /// <summary>Stars to one decimal: "2.5 stars".</summary>
        public static string Stars(ViewContext ctx, Fixed stars) { return ctx.Strings.Get("shop.stars", "n", ctx.Fmt.Decimal1(stars)); }

        public static string Quality(ViewContext ctx, int quality) { return ctx.Strings.Get(quality >= 2 ? "shop.qualityGood" : "shop.qualityStandard"); }

        /// <summary>A shop's terms in a line: price, quality, stars and its queue.</summary>
        public static string Terms(ViewContext ctx, ShopKind kind, Fixed factor, int quality, Fixed stars, Fixed wait)
        {
            string queue = wait <= Fixed.Zero ? ctx.Strings.Get("shop.noWait") : ctx.Strings.Get("shop.wait", "min", ctx.Fmt.Integer(wait.RoundToLong()));
            return Price(ctx, kind, factor) + " · " + Quality(ctx, quality) + " · " + Stars(ctx, stars) + " · " + queue;
        }
    }

    /// <summary>A shop's research (spec 017 §3.2, §3.3, §7): who buys, how much, at what price, the rivals and the share you'd get.</summary>
    public static class ShopResearch
    {
        internal static void Add(ViewContext ctx, SheetVm vm, BusinessTypeDef t, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ShopDefs d = ShopRules.Defs(sim);
            vm.Lines.Add(new LineVm(s.Get("research.shopPeople"), s.Get("research.shopPeopleValue", "n", ctx.Fmt.Integer(d.VillagePeople), "town", ctx.Fmt.Integer(sim.State.Citizens?.Population ?? 0)),
                "shopPeople", type) { Icon = "population" });
            long demand = 0;
            foreach (int g in ShopRules.KindGoods(sim, t.Kind))
                if (t.Kind != ShopKind.Veg || t.Sells(g))
                    foreach (ShopGroup grp in new[] { ShopGroup.Town, ShopGroup.Village }) demand += ShopRules.DemandPerDay(sim, t.Kind, g, grp);
            vm.Lines.Add(new LineVm(s.Get("research.shopDemand." + t.Kind.ToString().ToLowerInvariant()), ShopWords.Amount(ctx, t.Kind, demand), "shopDemand", type) { Icon = t.Id });
            if (t.Kind == ShopKind.Veg)
                vm.Lines.Add(new LineVm(s.Get("research.shopReference.veg"), s.Get("research.shopMarkup", "pct", ctx.Fmt.Percent(d.RetailMarkup - Fixed.One)), "shopReference", type) { Icon = "coin" });
            else
                foreach (int g in ShopRules.KindGoods(sim, t.Kind))
                    vm.Lines.Add(new LineVm(s.Get("research.shopReference", "good", g < 0 ? s.Get("shop.chai") : s.Get(sim.Config.Game.Market.Goods[g].NameKey)),
                        g < 0 ? ShopWords.Price(ctx, t.Kind, Fixed.One) : ctx.Fmt.PricePerKg(ShopRules.Reference(sim, t.Kind, g)), "shopReference", type) { Icon = "coin" });
            for (int i = 0; i < d.Rivals.Count; i++)
            {
                RivalDef r = d.Rivals[i];
                if (r.Kind != t.Kind) continue;
                Rival x = sim.State.Shops.Rivals[i];
                int metres = ShopRules.Metres(sim, ShopGroup.Village, r.Tile, r.Metres);
                vm.Lines.Add(new LineVm(ShopWords.Rival(ctx, r), s.Get("research.rivalValue", "terms", ShopWords.Price(ctx, t.Kind, x.PriceFactor) + " · " + ShopWords.Quality(ctx, r.Quality) + " · " + ShopWords.Stars(ctx, x.Stars),
                    "m", ctx.Fmt.Integer(metres)), "shopShare", ShopWords.ShareId(i)) { Icon = "place_" + (r.Tile >= 0 ? PlaceKind(sim, r.Tile) : "halwai") });
            }
            vm.Lines.Add(new LineVm(s.Get("research.shopExpected"), ctx.Fmt.Percent(ShopRules.ExpectedShare(sim, t.Kind, ShopGroup.Village, d.SquareTile)), "shopExpected", type) { Icon = "population" });
            vm.Lines.Add(new LineVm(s.Get("research.pitchRent"), s.Get("research.pitchRentValue", "rent", ctx.Fmt.Coins(MarketRules.Indexed(sim, d.PitchRent))), "pitchRent", type) { Icon = "coin" });
            if (t.Kind == ShopKind.Chai)
            {
                long[] herd = BusinessRules.HerdMilk(sim);
                long mine = 0;
                foreach (long ml in herd) mine += ml;
                string price = ctx.Fmt.PricePerLitre(BusinessRules.NeighbourMilkPrice(sim, t));
                vm.Lines.Add(new LineVm(s.Get("research.shopMilk"), mine > 0 ? s.Get("research.shopMilkValue", "milk", ctx.Fmt.Litres(mine), "price", price) : s.Get("research.shopMilkNeighbours", "price", price),
                    "dairyNeighbours", type) { Icon = "milk" });
            }
            foreach (int g in BusinessRules.BoughtInputs(t))
                vm.Lines.Add(new LineVm(s.Get("research.input", "good", s.Get(sim.Config.Game.Market.Goods[g].NameKey)), ctx.Fmt.PricePerKg(BusinessRules.InputPrice(sim, g)), "recipeInput", g)
                    { Icon = sim.Config.Game.Market.Goods[g].Id });
            vm.Lines.Add(new LineVm(s.Get("research.wage", "role", s.Get("business.role." + t.Worker)), s.Get("research.wageValue", "wage", ctx.Fmt.Coins(BusinessRules.Wage(sim, t))), "businessWage", type)
                { Icon = "person_hand_m" });
        }

        internal static string PlaceKind(Simulation sim, int tile)
        {
            foreach (PlaceDef p in sim.Config.Game.Map.Places) if (p.Tile == tile) return p.Kind;
            return "house";
        }
    }

    /// <summary>
    /// Where a shop stands (spec 017 §3.4, §7): the square's pitches (rent, the village passing by), each free or whose; or your own acre
    /// (no rent, fewer pass by), chosen on the map.
    /// </summary>
    public static class ShopPlaceSheet
    {
        public const string Kind = "ShopPlace";

        public static SheetVm Build(ViewContext ctx, int type, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            BusinessTypeDef t = BusinessRules.Defs(sim).Types[type];
            ShopDefs d = ShopRules.Defs(sim);
            bool self = BusinessWords.Self(arg), loan = BusinessWords.Loan(arg);
            var vm = new SheetVm { Icon = t.Id, Title = s.Get("shopPlace.title", "name", BusinessWords.InSentence(ctx, t)), Subtitle = s.Get("shopPlace.subtitle") };
            vm.Lines.Add(LineVm.Header(s.Get("shopPlace.square")));
            long borrow = BusinessWords.LoanFor(sim, t, loan);
            for (int p = 0; p < d.Pitches; p++)
            {
                Business there = sim.State.Businesses.OnPitch(p);
                var line = new LineVm(ShopWords.Pitch(ctx, p), there != null ? s.Get("shopPlace.taken", "name", BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, there)))
                    : s.Get("shopPlace.free", "rent", ctx.Fmt.Coins(MarketRules.Indexed(sim, d.PitchRent))), "pitchRent", type) { Icon = "pitch" };
                if (there == null)
                {
                    long capital = BusinessRules.Projected(sim, t, self, BusinessRules.DefaultOwnPerDay(sim, t, d.SquareTile), t.Fee, borrow, null, p).CashNeeded - borrow;
                    int pitch = p;
                    string id = t.Id, lender = BusinessRules.Defs(sim).LenderId;
                    int term = BusinessRules.Defs(sim).TermSeasons;
                    line.Action = ActionVm.Checked(ctx, "pitch:" + p, s.Get("action.buildHere"), ctx.Fmt.Coins(capital), ActionKind.Primary,
                        at => new FoundBusiness(at, id, 0, capital, self, loan ? new Financing(lender, borrow, term) : null, pitch));
                    line.Note = s.Get("shopPlace.share", "pct", ctx.Fmt.Percent(ShopRules.ExpectedShare(sim, t.Kind, ShopGroup.Village, d.SquareTile)));
                }
                vm.Lines.Add(line);
            }
            vm.Lines.Add(LineVm.Header(s.Get("shopPlace.acre")));
            vm.Lines.Add(new LineVm(s.Get("shopPlace.acreRow"), s.Get("shopPlace.acreValue"))
                { Icon = "field", Action = ActionVm.Open("acre", s.Get("shopPlace.chooseAcre"), new SheetRequest("BusinessPlacement", type, arg)) });
            return vm;
        }
    }

    /// <summary>A shop's card (spec 017 §7): its Today, its Customers (each shop's share with a *Why?*), its Settings.</summary>
    public static class ShopSheet
    {
        public const string Customers = "customers";

        private static readonly int[][] HourPresets = { new[] { 8, 20 }, new[] { 8, 14 }, new[] { 14, 20 } };

        /// <summary>Today: what's out and in stock, what's been sold.</summary>
        internal static void AddToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            vm.Lines.Add(new LineVm(s.Get("shop.soldToday"), ShopWords.Amount(ctx, t.Kind, b.ServedToday), "shopToday", b.Id) { Icon = t.Id });
            if (t.Kind == ShopKind.Chai)
            {
                vm.Lines.Add(new LineVm(s.Get("shop.milk"), ctx.Fmt.Litres(ShopRules.Milk(sim, b)), "shopMilk", b.Id) { Icon = "milk" });
                foreach (int g in t.Goods)
                    vm.Lines.Add(new LineVm(s.Get("business.stock", "good", s.Get(sim.Config.Game.Market.Goods[g].NameKey)), ctx.Fmt.Kg(b.Stock[g]), "shopPutOut", BusinessWords.GoodId(b.Id, g))
                        { Icon = sim.Config.Game.Market.Goods[g].Id });
            }
            else if (t.Kind == ShopKind.Veg)
            {
                foreach (int g in t.Goods)
                {
                    if (b.PutOut[g] <= 0 && b.Stock[g] <= 0) continue;
                    vm.Lines.Add(new LineVm(s.Get(sim.Config.Game.Market.Goods[g].NameKey), s.Get("shop.outValue", "kg", ctx.Fmt.Kg(b.Stock[g]), "of", ctx.Fmt.Kg(b.PutOut[g])), "shopPutOut",
                        BusinessWords.GoodId(b.Id, g)) { Icon = sim.Config.Game.Market.Goods[g].Id });
                }
            }
        }

        /// <summary>
        /// Customers (spec 017 §7): today's customers of its kind and your share, then each shop open today (yours, the village's, the
        /// town's) with its terms and share, and those no shop could serve.
        /// </summary>
        internal static void AddCustomers(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ShopsState st = sim.State.Shops;
            long lost = st.LostToday[(int)t.Kind], all = Everyone(sim, t.Kind) - lost;
            vm.Lines.Add(new LineVm(s.Get("shop.today"), s.Get("shop.todayValue", "n", ShopWords.Amount(ctx, t.Kind, all + lost), "pct", ctx.Fmt.Percent(Share(b.CustomersToday, all + lost))),
                "shopToday", b.Id) { Icon = "population" });
            foreach (Business x in sim.State.Businesses.Businesses)
            {
                if (x.Status != BusinessStatus.Open || BusinessRules.TypeOf(sim, x).Kind != t.Kind) continue;
                string name = x == b ? s.Get("shop.you") : s.Get("shop.yourOther", "name", x.Pitch >= 0 ? ShopWords.Pitch(ctx, x.Pitch) : s.Get("shop.yourAcre"));
                vm.Lines.Add(new LineVm(name, ctx.Fmt.Percent(Share(x.CustomersToday, all + lost)), "shopShare", ShopWords.ShareId(x))
                    { Icon = t.Id, Note = ShopWords.Terms(ctx, t.Kind, x.PriceFactor, x.Quality, x.Stars, ShopRules.WaitMinutes(x.LastArrivals, x.LastCapacity)) });
            }
            for (int i = 0; i < st.Rivals.Count; i++)
            {
                Rival r = st.Rivals[i];
                RivalDef d = ShopRules.Defs(sim).Rivals[r.Def];
                if (d.Kind != t.Kind) continue;
                vm.Lines.Add(new LineVm(ShopWords.Rival(ctx, d), ctx.Fmt.Percent(Share(r.CustomersToday, all + lost)), "shopShare", ShopWords.ShareId(i))
                    { Icon = "place_" + (d.Tile >= 0 ? ShopResearch.PlaceKind(sim, d.Tile) : "halwai"), Note = ShopWords.Terms(ctx, t.Kind, r.PriceFactor, d.Quality, r.Stars, ShopRules.WaitMinutes(r.LastArrivals, d.CapacityPerHour)) });
            }
            vm.Lines.Add(new LineVm(s.Get("shop.lost"), ShopWords.Amount(ctx, t.Kind, lost), "shopLost", b.Id) { Icon = "population" });
        }

        /// <summary>Today's customers of a kind: every shop's, and those no shop could serve.</summary>
        internal static long Everyone(Simulation sim, ShopKind kind)
        {
            long all = sim.State.Shops.LostToday[(int)kind];
            foreach (Business x in sim.State.Businesses.Businesses) if (x.Status == BusinessStatus.Open && BusinessRules.TypeOf(sim, x).Kind == kind) all += x.CustomersToday;
            foreach (Rival r in sim.State.Shops.Rivals) if (ShopRules.Defs(sim).Rivals[r.Def].Kind == kind) all += r.CustomersToday;
            return all;
        }

        internal static Fixed Share(long part, long all) { return all <= 0 ? Fixed.Zero : Fixed.FromRatio(part, all); }

        /// <summary>Settings: hours, price, what a stall puts out (the tea stall's milk and snacks), buying at the market.</summary>
        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            Fixed fee = b.Fee;
            long own = b.OwnPerDay;
            bool market = b.BuyFromMarket;
            int open = b.OpenHour, close = b.CloseHour;
            int hi = 0;
            for (int i = 0; i < HourPresets.Length; i++) if (HourPresets[i][0] == open && HourPresets[i][1] == close) hi = i;
            int[] next = HourPresets[(hi + 1) % HourPresets.Length];
            var hours = new LineVm(s.Get("business.hours"), BusinessWords.Hours(ctx, open, close), "businessHours", id);
            hours.Action = ActionVm.Checked(ctx, "hours", s.Get("business.change"), BusinessWords.Hours(ctx, next[0], next[1]), ActionKind.Secondary,
                at => new SetBusiness(at, id, next[0], next[1], fee, own, market));
            vm.Lines.Add(hours);

            var presets = ShopRules.Defs(sim).PricePresets;
            int pi = 0;
            for (int i = 0; i < presets.Count; i++) if (presets[i] == b.PriceFactor) pi = i;
            Fixed nextPrice = presets[(pi + 1) % presets.Count];
            var price = new LineVm(s.Get("shop.price"), ShopWords.Price(ctx, t.Kind, b.PriceFactor), "shopPrice", id) { Icon = "coin", Note = t.Kind == ShopKind.Veg ? null : ShopWords.PriceSetting(ctx, b.PriceFactor) };
            price.Action = new ActionVm("price", s.Get("business.change"), ShopWords.Price(ctx, t.Kind, nextPrice), ActionKind.Secondary, true, null, at => new SetShop(at, id, nextPrice));
            vm.Lines.Add(price);

            if (t.Kind == ShopKind.Sweets) BusinessSheet.AddMixFor(ctx, vm, b, t);
            if (t.Kind == ShopKind.Chai)
            {
                long need = ShopRules.DefaultMilk(sim, t, b.TileId);
                var milkPresets = new List<long>();
                foreach (long v in new[] { need, need * 3 / 2, need / 2 }) if (v > 0 && !milkPresets.Contains(v)) milkPresets.Add(v);
                if (milkPresets.Count == 0) milkPresets.Add(0);
                long nextMilk = milkPresets[(milkPresets.IndexOf(own) + 1) % milkPresets.Count];
                var milk = new LineVm(s.Get("business.milkADay"), ctx.Fmt.Litres(own), "shopMilk", id) { Icon = "milk" };
                milk.Action = new ActionVm("milkADay", s.Get("business.change"), ctx.Fmt.Litres(nextMilk), ActionKind.Secondary, true, null, at => new SetBusiness(at, id, open, close, fee, nextMilk, market));
                vm.Lines.Add(milk);
            }
            if (ShopRules.Stocked(t))
            {
                long[] usual = ShopRules.DefaultPutOut(sim, t, b.TileId);
                foreach (int g in t.Goods)
                {
                    var list = new List<long>();
                    foreach (long v in new[] { usual[g], usual[g] * 3 / 2, usual[g] / 2, 0L }) if (!list.Contains(v)) list.Add(v);
                    long nextPut = list[(list.IndexOf(b.PutOut[g]) + 1) % list.Count];
                    string good = sim.Config.Game.Market.Goods[g].Id;
                    var line = new LineVm(s.Get("shop.putOut", "good", s.Get(sim.Config.Game.Market.Goods[g].NameKey)), ctx.Fmt.Kg(b.PutOut[g]), "shopPutOut", BusinessWords.GoodId(id, g)) { Icon = good };
                    line.Action = new ActionVm("put:" + good, s.Get("business.change"), ctx.Fmt.Kg(nextPut), ActionKind.Secondary, true, null, at => new SetStallGoods(at, id, good, nextPut));
                    vm.Lines.Add(line);
                }
            }
            var buy = new LineVm(s.Get(BusinessWords.Key(ctx, "business.market", t)), s.Get(market ? "business.yes" : "business.no"), "businessMarket", id);
            buy.Action = new ActionVm("market", s.Get(market ? "business.stopMarket" : "business.startMarket"), null, ActionKind.Secondary, true, null, at => new SetBusiness(at, id, open, close, fee, own, !market));
            vm.Lines.Add(buy);
        }
    }

    /// <summary>The village square (spec 017 §3.4): the banyan's three pitches, whose shop is on each, or free to rent.</summary>
    public static class SquareView
    {
        internal static void AddPitches(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            ShopDefs d = ShopRules.Defs(sim);
            if (d == null || sim.State.Businesses == null) return;
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get("square.pitches")));
            for (int p = 0; p < d.Pitches; p++)
            {
                Business b = sim.State.Businesses.OnPitch(p);
                if (b != null)
                {
                    BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
                    vm.Lines.Add(new LineVm(ShopWords.Pitch(ctx, p), s.Get("square.yours", "name", BusinessWords.InSentence(ctx, t)), "pitchRent", b.Type)
                        { Icon = t.Id, Action = ActionVm.Open("pitch:" + p, s.Get("action.see"), new SheetRequest("Business", b.Id)), Note = BusinessSheet.StatusText(ctx, b) });
                    continue;
                }
                bool open = false;
                foreach (BusinessTypeDef t in BusinessRules.Defs(sim).Types) if (t.IsShop && BusinessRules.Unlocked(sim, t)) open = true;
                vm.Lines.Add(new LineVm(ShopWords.Pitch(ctx, p), s.Get("square.free", "rent", ctx.Fmt.Coins(MarketRules.Indexed(sim, d.PitchRent))), "pitchRent", 0)
                {
                    Icon = "pitch",
                    Action = new ActionVm("pitch:" + p, s.Get("square.openShop"), null, ActionKind.Open, open, open ? null : s.Get("businesses.locked.village"), null, new SheetRequest("Businesses", 0)),
                });
            }
        }
    }
}
