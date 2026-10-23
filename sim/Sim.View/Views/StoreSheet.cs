using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// The Store (spec 011 §7 as amended by ADR 0059): how full it is; picture buttons for the supermarket's orders, buying inputs
    /// and the mandi trip; what the supermarket owes; selling to the trader (the truck, then a row per crop with its Sell); and
    /// the seed and inputs in store.
    /// </summary>
    public static class StoreSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            MarketState m = sim.State.Market;
            var s = ctx.Strings;
            long used = Store.UsedGrams(sim), capacity = Store.CapacityGrams(sim);
            var vm = new SheetVm { Icon = "store", Title = s.Get("store.title") };
            // How full it is, with its Why? (the HUD's store pill is a plain tap since the Sprint 7 device run, ADR 0044 §3).
            vm.Lines.Add(new LineVm(s.Get("store.filled"), s.Get("store.fill", "used", ctx.Fmt.Kg(used), "capacity", ctx.Fmt.Kg(capacity)), "store"));

            // The hub's shortcuts (ADR 0043 §7, ADR 0059 §2): each a picture button that says where it goes.
            var buttons = new List<ActionVm>();
            if (Supermarket.Started(sim) || m.SupermarketOrders.Count > 0)
            {
                int open = OrdersSheet.OpenCount(sim);
                buttons.Add(Shortcut("orders", s.Get("store.orders"), Supermarket.DeliveryOpen(sim) ? s.Plural("store.ordersValue", open) : s.Get("store.ordersClosed"),
                    new SheetRequest("Orders", 0), "orders", open));
            }
            buttons.Add(Shortcut("buyInputs", s.Get("store.buyInputs"), s.Get("store.buyInputsNote"), new SheetRequest("BuyInputs", 0), "seed_wheat", 0));
            buttons.Add(Shortcut("mandi", s.Get("store.mandi"), s.Get("store.mandiNote"), new SheetRequest("Mandi", 0), "cart", 0));
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });
            if (sim.State.Ledger.Balance(Sim.Core.Finance.Account.OwedByBuyers) > 0) vm.Lines.Add(OrdersSheet.OwedLine(ctx));

            // Selling to the trader: the truck's state (spec 011 §7), then one row per crop with its Sell.
            vm.Lines.Add(LineVm.Header(s.Get("store.sellHeading")));
            if (defs.Market.Truck != null)
            {
                vm.Lines.Add(OrdersSheet.TruckLine(ctx));
                if (MarketRules.StallClosed(sim)) vm.Lines.Add(new LineVm(s.Get("store.stallClosed"), null));
            }
            int produce = 0;
            var inputs = new List<LineVm>();
            for (int g = 0; g < defs.Market.Goods.Count; g++)
            {
                GoodDef good = defs.Market.Goods[g];
                if (m.Stock[g] == 0) continue;
                string name = s.Get(good.NameKey);
                if (good.Kind != GoodKind.Produce)
                {
                    if (good.Place != GoodPlace.Store && Sim.Core.Livestock.LivestockRules.Shed(sim) != null) continue; // with the shed, straw, feed and compost show on its sheet (spec 012 §7)
                    inputs.Add(new LineVm(name, s.Get("store.input", "qty", ctx.Fmt.Quantity(good, m.Stock[g])), "input", g) { Icon = good.Id });
                    continue;
                }
                produce++;
                vm.Lines.Add(ProduceLine(ctx, g));
            }
            if (produce == 0) vm.Lines.Add(new LineVm(s.Get("store.nothingToSell"), null));
            if (inputs.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("store.inputsHeading")));
                vm.Lines.AddRange(inputs);
            }
            if (produce == 0 && inputs.Count == 0) vm.Subtitle = s.Get("store.empty");
            return vm;
        }

        private static ActionVm Shortcut(string id, string label, string detail, SheetRequest opens, string icon, int count)
        {
            ActionVm a = ActionVm.Open(id, label, opens, detail);
            a.Icon = icon;
            a.Count = count;
            return a;
        }

        /// <summary>A crop in store: how much and the trader's price, its Sell button (or Take off market when all of it is on offer).</summary>
        private static LineVm ProduceLine(ViewContext ctx, int g)
        {
            Simulation sim = ctx.Sim;
            MarketState m = sim.State.Market;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            Fixed floor = MarketRules.Floor(sim, g);
            long free = m.InStore(g) - m.OnOffer(g);
            long room = MarketRules.TraderCapacityLeft(sim, g);
            long sell = System.Math.Min(free, room);
            long offered = m.OnOffer(g);
            Fixed? min = m.OfferMinPrice(g);
            string value = offered == 0
                ? ctx.Strings.Get("store.produce", "qty", ctx.Fmt.Kg(m.Stock[g]), "price", ctx.Fmt.PricePerKg(floor))
                : min.HasValue
                    ? ctx.Strings.Get("store.produceOffered", "qty", ctx.Fmt.Kg(m.Stock[g]), "offered", ctx.Fmt.Kg(offered), "min", ctx.Fmt.PricePerKg(min.Value))
                    : ctx.Strings.Get("store.produceOfferedAny", "qty", ctx.Fmt.Kg(m.Stock[g]), "offered", ctx.Fmt.Kg(offered));
            var line = new LineVm(ctx.Strings.Get(good.NameKey), value, "price", g) { Icon = good.Id, Opens = new SheetRequest("Good", g) };
            string id = good.Id;
            // The button sits on the good's own row, so the list reads as one row per good (owner feedback, device run 2).
            // Everything on offer at the market (a hand's order puts a harvest there): the way out is to take it back.
            if (sell == 0 && free == 0 && offered > 0)
            {
                line.Action = ActionVm.Checked(ctx, "withdraw:" + id, ctx.Strings.Get("action.withdrawRow"), ctx.Strings.Get("store.withdrawDetail", "qty", ctx.Fmt.Kg(offered)),
                    ActionKind.Secondary, at => new Withdraw(at, id, offered));
                return line;
            }
            long amount = MarketRules.Value(sim, g, sell, floor);
            line.Action = sell > 0
                ? ActionVm.Checked(ctx, "sell:" + id, ctx.Strings.Get("action.sellRow"), ctx.Strings.Get("store.sellDetail", "qty", ctx.Fmt.Kg(sell), "amount", ctx.Fmt.Coins(amount)),
                    ActionKind.Primary, at => new SellToTrader(at, id, sell))
                : ActionVm.Checked(ctx, "sell:" + id, ctx.Strings.Get("action.sellRow"), null, ActionKind.Primary, at => new SellToTrader(at, id, room == 0 ? free : 1));
            return line;
        }
    }
}
