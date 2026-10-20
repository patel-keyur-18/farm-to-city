using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// The minimal Store sheet of the first playable (ADR 0038): what is in the store, what the trader pays now and its room today,
    /// and Sell to trader for each produce. Offers, the mandi and buying inputs come with the full panels (Sprint 7).
    /// </summary>
    public static class StoreSheet
    {
        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            MarketState m = sim.State.Market;
            long used = Store.UsedGrams(sim), capacity = Store.CapacityGrams(sim);
            var vm = new SheetVm
            {
                Icon = "store", Title = ctx.Strings.Get("store.title"),
                Subtitle = ctx.Strings.Get("store.fill", "used", ctx.Fmt.Kg(used), "capacity", ctx.Fmt.Kg(capacity)),
            };
            for (int g = 0; g < defs.Market.Goods.Count; g++)
            {
                GoodDef good = defs.Market.Goods[g];
                if (m.Stock[g] == 0) continue;
                string name = ctx.Strings.Get(good.NameKey);
                if (good.Kind != GoodKind.Produce)
                {
                    vm.Lines.Add(new LineVm(name, ctx.Strings.Get("store.input", "qty", Quantity(ctx, good, m.Stock[g]))) { Icon = good.Id });
                    continue;
                }
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
                var line = new LineVm(name, value, "price") { Icon = good.Id };
                string id = good.Id;
                long amount = MarketRules.Value(sim, g, sell, floor);
                // The button sits on the good's own row, so the list reads as one row per good (owner feedback, device run 2).
                // Everything on offer at the market (a hand's order puts a harvest there): the way out is to take it back.
                if (sell == 0 && free == 0 && offered > 0)
                {
                    line.Action = ActionVm.Checked(ctx, "withdraw:" + id, ctx.Strings.Get("action.withdrawRow"), ctx.Strings.Get("store.withdrawDetail", "qty", ctx.Fmt.Kg(offered)),
                        ActionKind.Secondary, at => new Withdraw(at, id, offered));
                    vm.Lines.Add(line);
                    continue;
                }
                line.Action = sell > 0
                    ? ActionVm.Checked(ctx, "sell:" + id, ctx.Strings.Get("action.sellRow"), ctx.Strings.Get("store.sellDetail", "qty", ctx.Fmt.Kg(sell), "amount", ctx.Fmt.Coins(amount)),
                        ActionKind.Primary, at => new SellToTrader(at, id, sell))
                    : ActionVm.Checked(ctx, "sell:" + id, ctx.Strings.Get("action.sellRow"), null, ActionKind.Primary, at => new SellToTrader(at, id, room == 0 ? free : 1));
                vm.Lines.Add(line);
            }
            if (vm.Lines.Count == 0) vm.Subtitle = ctx.Strings.Get("store.empty");
            return vm;
        }

        private static string Quantity(ViewContext ctx, GoodDef good, long q)
        {
            // Inputs are in grams, millilitres or units; show kg, litres or a count.
            if (good.Unit == GoodUnit.Each) return ctx.Fmt.Integer(q);
            return ctx.Fmt.Kg(q);
        }
    }
}
