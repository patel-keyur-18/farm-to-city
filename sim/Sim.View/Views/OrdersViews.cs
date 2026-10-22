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
    /// The supermarket's Orders sheet (spec 011 §7, owner's Sprint 11 device review): one row per order with what it wants and pays,
    /// its state on the same row (when to sow, what's fresh to deliver, how much is delivered) and its button, or a "Delivered" tag
    /// once it's filled; then what the supermarket owes.
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
            foreach (SupermarketOrder o in m.SupermarketOrders) vm.Lines.Add(OrderLine(ctx, o, open));
            if (sim.State.Ledger.Balance(Account.OwedByBuyers) > 0) vm.Lines.Add(OwedLine(ctx));
            return vm;
        }

        /// <summary>An order's row: "Wants 50.0 kg at 34.52 /kg · trader pays 27.89 /kg", its state under it, and Deliver or "Delivered".</summary>
        private static LineVm OrderLine(ViewContext ctx, SupermarketOrder o, bool open)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            GoodDef good = sim.Config.Game.Market.Goods[o.Good];
            var line = new LineVm(s.Get(good.NameKey), s.Get("orders.line", "qty", ctx.Fmt.Kg(o.Amount), "price", ctx.Fmt.PricePerKg(o.Price),
                "trader", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, o.Good))), "orderPrice", o.Id) { Icon = good.Id };
            string progress = o.Delivered > 0 ? s.Get("orders.progress", "done", ctx.Fmt.Kg(o.Delivered), "qty", ctx.Fmt.Kg(o.Amount)) : null;
            string With(string note) { return progress == null ? note : note + " · " + progress; }
            if (o.Left == 0)
            {
                line.Badge = s.Get("orders.done");
                line.Note = s.Get("orders.allDelivered", "qty", ctx.Fmt.Kg(o.Delivered));
                line.NoteTone = Tone.Good;
                return line;
            }
            if (!open)
            {
                line.Note = With(s.Get("orders.missed"));
                line.NoteTone = Tone.Warn;
                return line;
            }
            long fresh = Supermarket.FreshStock(sim, o.Good);
            if (fresh > 0)
            {
                long q = System.Math.Min(fresh, o.Left);
                long id = o.Id;
                line.Note = With(s.Get("orders.fresh", "qty", ctx.Fmt.Kg(fresh)));
                line.NoteTone = Tone.Good;
                line.Action = ActionVm.Checked(ctx, "deliver:" + id, s.Get("orders.deliver"), s.Get("orders.deliverDetail", "qty", ctx.Fmt.Kg(q),
                    "amount", ctx.Fmt.Coins(MarketRules.Value(sim, o.Good, q, o.Price))), ActionKind.Primary, at => new DeliverOrder(at, id, q));
                return line;
            }
            line.Note = With(SowBy(ctx, o.Good, out bool late));
            line.NoteTone = late ? Tone.Warn : Tone.Plain;
            return line;
        }

        /// <summary>"Sow by 22:00 to harvest it today" for a field crop, "Pick it from the garden today" for a green, or "Too late to sow for today" (late).</summary>
        public static string SowBy(ViewContext ctx, int good, out bool late)
        {
            Simulation sim = ctx.Sim;
            late = false;
            foreach (CropDef c in sim.Config.Game.Farm.Crops)
            {
                if (c.OutputGood != good) continue;
                long by = Supermarket.SowBy(sim, c);
                late = by < ctx.Now;
                if (late) return ctx.Strings.Get("orders.tooLate");
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
