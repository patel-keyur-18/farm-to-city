using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>
    /// The supermarket's Orders sheet (spec 011 §7): today's orders, what's fresh in store for each with Deliver, "sow by HH:MM"
    /// when nothing is fresh yet, and what the supermarket owes.
    /// </summary>
    public static class OrdersSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MarketState m = sim.State.Market;
            bool open = Supermarket.DeliveryOpen(sim);
            var vm = new SheetVm { Icon = "basket", Title = s.Get("orders.title") };
            vm.Subtitle = m.SupermarketOrders.Count == 0 ? s.Get(Supermarket.Started(sim) ? "orders.none" : "orders.notYet")
                : s.Get(open ? "orders.till" : "orders.closed");
            foreach (SupermarketOrder o in m.SupermarketOrders)
            {
                GoodDef good = sim.Config.Game.Market.Goods[o.Good];
                vm.Lines.Add(new LineVm(s.Get(good.NameKey), s.Get("orders.line", "qty", ctx.Fmt.Kg(o.Amount), "price", ctx.Fmt.PricePerKg(o.Price),
                    "trader", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, o.Good))), "orderPrice", o.Id) { Icon = good.Id });
                vm.Lines.Add(Status(ctx, o, open));
            }
            long owed = sim.State.Ledger.Balance(Account.OwedByBuyers);
            if (owed > 0) vm.Lines.Add(OwedLine(ctx));
            if (m.SupermarketOrders.Count > 0) vm.Lines.Add(new LineVm(s.Get("orders.freshOnly"), null));
            return vm;
        }

        /// <summary>The order's second row: delivered, what's fresh with Deliver, or when to sow for it.</summary>
        private static LineVm Status(ViewContext ctx, SupermarketOrder o, bool open)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (o.Left == 0) return new LineVm(s.Get("orders.done"), ctx.Fmt.Kg(o.Delivered));
            if (!open) return new LineVm(s.Get("orders.lapsing"), s.Get("orders.left", "qty", ctx.Fmt.Kg(o.Left)));
            long fresh = Supermarket.FreshStock(sim, o.Good);
            string done = o.Delivered > 0 ? s.Get("orders.soFar", "qty", ctx.Fmt.Kg(o.Delivered)) : null;
            if (fresh > 0)
            {
                long q = System.Math.Min(fresh, o.Left);
                long id = o.Id;
                var line = new LineVm(s.Get("orders.fresh", "qty", ctx.Fmt.Kg(fresh)), done);
                line.Action = ActionVm.Checked(ctx, "deliver:" + id, s.Get("orders.deliver"), s.Get("orders.deliverDetail", "qty", ctx.Fmt.Kg(q),
                    "amount", ctx.Fmt.Coins(MarketRules.Value(sim, o.Good, q, o.Price))), ActionKind.Primary, at => new DeliverOrder(at, id, q));
                return line;
            }
            return new LineVm(s.Get("orders.noneFresh"), SowBy(ctx, o.Good) + (done == null ? "" : " · " + done));
        }

        /// <summary>"Sow by 22:00" for a field crop, "Pick from the garden" for a green, or "Too late to sow today".</summary>
        public static string SowBy(ViewContext ctx, int good)
        {
            Simulation sim = ctx.Sim;
            foreach (CropDef c in sim.Config.Game.Farm.Crops)
            {
                if (c.OutputGood != good) continue;
                long by = Supermarket.SowBy(sim, c);
                if (by < ctx.Now) return ctx.Strings.Get("orders.tooLate");
                return ctx.Strings.Get("orders.sowBy", "time", Text.Formatter.Clock(Calendar.From(by, sim.Clock)));
            }
            return ctx.Strings.Get("orders.pickGarden");
        }

        /// <summary>"Supermarket pays you 6,691 at 6:00 tomorrow" (or "at 6:00" after midnight), with its Why?.</summary>
        internal static LineVm OwedLine(ViewContext ctx)
        {
            long owed = ctx.Sim.State.Ledger.Balance(Account.OwedByBuyers);
            bool today = !Supermarket.DeliveryOpen(ctx.Sim); // between midnight and 6:00 the payment is this morning
            return new LineVm(ctx.Strings.Get("orders.owed"), ctx.Strings.Get(today ? "orders.owedAt6" : "orders.owedTomorrow", "amount", ctx.Fmt.Coins(owed)), "owed");
        }

        /// <summary>Open orders with something left while deliveries are open: the HUD badge.</summary>
        public static int OpenCount(Simulation sim)
        {
            if (sim.Config.Game?.Market.Orders == null || !Supermarket.DeliveryOpen(sim)) return 0;
            int n = 0;
            foreach (SupermarketOrder o in sim.State.Market.SupermarketOrders) if (o.Left > 0) n++;
            return n;
        }

        /// <summary>The trader's truck on the Store sheet: "here till midnight · 2,800 kg room" or "back at 6:00".</summary>
        internal static LineVm TruckLine(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            MarketState m = sim.State.Market;
            TruckDef truck = sim.Config.Game.Market.Truck;
            string value = m.TruckHere ? ctx.Strings.Get("store.truckHere", "room", ctx.Fmt.Kg(truck.LoadGrams - m.TruckLoadToday))
                : MarketRules.TraderBuys(sim) ? ctx.Strings.Get("store.truckFirstSale") : ctx.Strings.Get("store.truckAway");
            return new LineVm(ctx.Strings.Get("store.truck"), value, "truck");
        }

        /// <summary>The good's price today against yesterday's truck price: "5.62 (yesterday 5.30, ▲6%)" (spec 011 §3.1b).</summary>
        internal static string TodayVsYesterday(ViewContext ctx, int g)
        {
            Simulation sim = ctx.Sim;
            Fixed today = MarketRules.Floor(sim, g);
            Fixed? yesterday = sim.State.Market.TruckPriceYesterdayOf(g);
            if (!yesterday.HasValue || yesterday.Value <= Fixed.Zero) return ctx.Strings.Get("good.todayOnly", "price", ctx.Fmt.PricePerKg(today));
            long pct = ((today / yesterday.Value - Fixed.One) * Fixed.FromInt(100)).RoundToLong();
            string change = pct == 0 ? ctx.Strings.Get("good.todaySame")
                : ctx.Strings.Get(pct > 0 ? "good.todayUp" : "good.todayDown", "n", ctx.Fmt.Integer(System.Math.Abs(pct)));
            return ctx.Strings.Get("good.todayValue", "price", ctx.Fmt.PricePerKg(today), "yesterday", ctx.Fmt.PricePerKg(yesterday.Value), "change", change);
        }
    }

    public static partial class WhyView
    {
        /// <summary>An order's price (spec 011 §7): the valley's price this morning × (1 + the premium). id = the order's id.</summary>
        private static WhyVm OrderPrice(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            SupermarketOrder o = null;
            foreach (SupermarketOrder x in sim.State.Market.SupermarketOrders) if (x.Id == id) o = x;
            if (o == null) return New(ctx, "why.orderPrice.gone", null);
            GoodDef good = sim.Config.Game.Market.Goods[o.Good];
            WhyVm vm = New(ctx, "why.orderPrice.title", Price(ctx, o.Price), "good", ctx.Strings.Get(good.NameKey));
            Row(vm, ctx, "why.orderPrice.regional", Price(ctx, o.Price / (Fixed.One + o.Premium)));
            Row(vm, ctx, "why.orderPrice.premium", "+" + Pct(ctx, o.Premium));
            Row(vm, ctx, "why.orderPrice.trader", Price(ctx, MarketRules.Floor(sim, o.Good)));
            vm.Footnote = ctx.Strings.Get("why.orderPrice.lesson");
            return vm;
        }

        /// <summary>What the supermarket owes: today's deliveries, booked as sales now, paid at 6:00 (spec 011 §7).</summary>
        private static WhyVm Owed(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.owed.title", Money(ctx, sim.State.Ledger.Balance(Account.OwedByBuyers)));
            foreach (SupermarketOrder o in sim.State.Market.SupermarketOrders)
            {
                if (o.Delivered == 0) continue;
                Row(vm, ctx, "why.owed.row", Money(ctx, MarketRules.Value(sim, o.Good, o.Delivered, o.Price)),
                    "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[o.Good].NameKey), "qty", ctx.Fmt.Kg(o.Delivered), "price", Price(ctx, o.Price));
            }
            Text(vm, ctx, "why.owed.when");
            vm.Footnote = ctx.Strings.Get("why.owed.lesson");
            return vm;
        }

        /// <summary>The trader's truck (spec 011 §3.1): its hours, its load and the room left.</summary>
        private static WhyVm Truck(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            MarketState m = sim.State.Market;
            TruckDef truck = sim.Config.Game.Market.Truck;
            WhyVm vm = New(ctx, "why.truck.title", m.TruckHere ? ctx.Fmt.Kg(truck.LoadGrams - m.TruckLoadToday) : ctx.Strings.Get("store.truckAway"));
            Row(vm, ctx, "why.truck.hours", ctx.Strings.Get("why.truck.hoursValue", "from", Hour(ctx, truck.ArriveHour), "to", Hour(ctx, truck.LeaveHour)));
            Row(vm, ctx, "why.truck.load", ctx.Fmt.Kg(truck.LoadGrams));
            Row(vm, ctx, "why.truck.bought", ctx.Fmt.Kg(m.TruckLoadToday));
            Text(vm, ctx, "why.truck.price");
            vm.Footnote = ctx.Strings.Get("why.truck.lesson");
            return vm;
        }

        private static string Hour(ViewContext ctx, int h) { return h == 0 ? ctx.Strings.Get("time.midnight") : h + ":00"; }
    }
}
