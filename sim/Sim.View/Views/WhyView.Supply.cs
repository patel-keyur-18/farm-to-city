using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Farm;

namespace Sim.View.Views
{
    /// <summary>Supply (spec 030 §3.4, §3.5): what a business took today, its prices today, how it is supplied and the cap, each in words.</summary>
    public static partial class WhyView
    {
        /// <summary>The price a business pays you for a good it takes from you, and the one it pays when it buys: today's, from the same rules it uses.</summary>
        private static void SupplyPrices(ViewContext ctx, Business b, BusinessTypeDef t, int g, out Fixed yours, out Fixed bought)
        {
            var sim = ctx.Sim;
            int kind = BusinessRules.MilkKind(sim, g);
            yours = kind >= 0 ? Sim.Core.Livestock.LivestockRules.MilkPrice(sim, kind) : MarketRules.Floor(sim, g); // your herd's milk goes in at the co-op's price, the rest at the trader's
            if (t.TakesMilk) bought = BusinessRules.NeighbourMilkPrice(sim, t);
            else if (t.IsMeals) bought = MealsRules.MarketPrice(sim, g); // the kitchen pays the retail price
            else if (ShopRules.Stocked(t) && t.Kind == ShopKind.Grocery) bought = MarketRules.Floor(sim, g);
            else if (ShopRules.Stocked(t)) bought = ShopRules.VegShare(sim, g) > Fixed.Zero ? sim.State.Market.Price[g] : MarketRules.Ceiling(sim, g); // the mandi's produce, else the import price
            else bought = BusinessRules.ImportPrice(sim, b, g);
        }

        /// <summary>One good's day: grams from you and bought, the prices today, and the cap under Manual.</summary>
        private static WhyVm SupplyToday(ViewContext ctx, long id)
        {
            var sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            int g = (int)(id % 1000);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            GoodDef good = sim.Config.Game.Market.Goods[g];
            long you = SupplyRules.FromYouTodayOf(sim, b, g), bought = SupplyRules.BoughtTodayOf(sim, b, g);
            WhyVm vm = New(ctx, "why.supplyToday.title", ctx.Fmt.Quantity(good, you + bought), "good", ctx.Strings.Get(good.NameKey), "name", BusinessWords.InSentence(ctx, t));
            Row(vm, ctx, "why.supplyToday.you", ctx.Fmt.Quantity(good, you));
            Row(vm, ctx, "why.supplyToday.bought", ctx.Fmt.Quantity(good, bought));
            SupplyPrices(ctx, b, t, g, out Fixed yours, out Fixed buys);
            Row(vm, ctx, "why.supplyToday.yourPrice", t.TakesMilk ? ctx.Fmt.PricePerLitre(yours) : ctx.Fmt.PricePerKg(yours));
            Row(vm, ctx, "why.supplyToday.buyPrice", t.TakesMilk ? ctx.Fmt.PricePerLitre(buys) : ctx.Fmt.PricePerKg(buys));
            Text(vm, ctx, "why.supplyToday.day");
            if (SupplyRules.CappedInputs(sim, t).Contains(g))
                Text(vm, ctx, b.SupplyManual ? "why.supplyToday.manual" : "why.supplyToday.auto");
            vm.Footnote = ctx.Strings.Get("why.supplyToday.lesson");
            return vm;
        }

        /// <summary>The mode: Automatic's rule, or Manual's.</summary>
        private static WhyVm SupplyModeWhy(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.supplyMode.title", ctx.Strings.Get(b.SupplyManual ? "supply.modeManual" : "supply.modeAuto"));
            Text(vm, ctx, "why.supplyMode.auto");
            Text(vm, ctx, "why.supplyMode.manual");
            Text(vm, ctx, "why.supplyMode.stays");
            vm.Footnote = ctx.Strings.Get("why.supplyMode.lesson");
            return vm;
        }

        /// <summary>A good's cap: its day's amount, the cap and what your store holds now.</summary>
        private static WhyVm SupplyCapWhy(ViewContext ctx, long id)
        {
            var sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            int g = (int)(id % 1000);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            GoodDef good = sim.Config.Game.Market.Goods[g];
            long cap = SupplyRules.CapOf(sim, b, g);
            WhyVm vm = New(ctx, "why.supplyCap.title", cap == long.MaxValue ? ctx.Strings.Get("supply.capAll") : ctx.Fmt.Kg(cap), "good", ctx.Strings.Get(good.NameKey));
            long day = SupplyRules.DayAmount(sim, b, g);
            if (day > 0) Row(vm, ctx, "why.supplyCap.day", ctx.Fmt.Kg(day));
            Row(vm, ctx, "why.supplyCap.store", ctx.Fmt.Kg(System.Math.Max(0, sim.State.Market.Stock[g] - sim.State.Market.Offered[g])));
            Text(vm, ctx, t.IsMeals ? "why.supplyCap.meals" : "why.supplyCap.rule");
            vm.Footnote = ctx.Strings.Get("why.supplyCap.lesson");
            return vm;
        }

        /// <summary>A business's whole day on the hub: each good it took, from you and bought.</summary>
        private static WhyVm SupplyBusiness(ViewContext ctx, long id)
        {
            var sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            WhyVm vm = New(ctx, "why.supplyBusiness.title", null, "name", BusinessWords.InSentence(ctx, t));
            foreach (int g in SupplyRules.Counted(sim, b))
            {
                long you = SupplyRules.FromYouTodayOf(sim, b, g), bought = SupplyRules.BoughtTodayOf(sim, b, g);
                if (you + bought == 0) continue;
                GoodDef good = sim.Config.Game.Market.Goods[g];
                vm.Rows.Add(new WhyRowVm(ctx.Strings.Get(good.NameKey), ctx.Strings.Get("supply.value", "you", ctx.Fmt.Quantity(good, you), "bought", ctx.Fmt.Quantity(good, bought))));
            }
            Text(vm, ctx, "why.supplyToday.day");
            vm.Footnote = ctx.Strings.Get("why.supplyToday.lesson");
            return vm;
        }

        /// <summary>
        /// Why a hand sows what he does for your vegetable stall (spec 030 §3.6): the stall's day, its own stock, what your store holds and what is
        /// growing, for the vegetable he would sow, then what the stall will still need of each. Id: the stall's id × 1000 + the field's acre.
        /// </summary>
        private static WhyVm StallPick(ViewContext ctx, long id)
        {
            var sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1000);
            int tile = (int)(id % 1000);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            FarmDefs farm = sim.Config.Game.Farm;
            string field = PeopleWords.Field(ctx, tile);
            StandingOrder o = sim.State.People?.OrderFor(OrderTargetKind.Field, tile);
            int fert = o == null ? 0 : o.Fertiliser;
            int pick = StallOrderRules.Pick(sim, tile, fert, b, out bool needless);
            WhyVm vm;
            if (pick < 0)
            {
                vm = New(ctx, needless ? "why.stallPick.needlessTitle" : "why.stallPick.noneTitle", null, "field", field);
                Text(vm, ctx, needless ? "why.stallPick.needless" : "why.stallPick.none");
            }
            else
            {
                CropDef c = farm.Crops[pick];
                GoodDef good = sim.Config.Game.Market.Goods[c.OutputGood];
                string name = ctx.Strings.Get(c.NameKey);
                vm = New(ctx, "why.stallPick.title", name, "crop", name, "field", field);
                string lower = name.ToLowerInvariant();
                Row(vm, ctx, "why.stallPick.day", ctx.Fmt.Quantity(good, b.PutOut[c.OutputGood]), "good", lower);
                Row(vm, ctx, "why.stallPick.stock", ctx.Fmt.Quantity(good, b.Stock[c.OutputGood]), "good", lower);
                Row(vm, ctx, "why.stallPick.store", ctx.Fmt.Quantity(good, System.Math.Max(0, sim.State.Market.Stock[c.OutputGood] - sim.State.Market.Offered[c.OutputGood])));
                Row(vm, ctx, "why.stallPick.growing", ctx.Fmt.Quantity(good, StallOrderRules.GrowingFor(sim, b, c.OutputGood)));
            }
            foreach (var want in StallOrderRules.Wanted(sim, b))
            {
                CropDef w = farm.Crops[want.Key];
                Row(vm, ctx, "why.stallPick.needs", ctx.Fmt.Quantity(sim.Config.Game.Market.Goods[w.OutputGood], want.Value), "good", ctx.Strings.Get(w.NameKey));
            }
            Text(vm, ctx, "why.stallPick.rule");
            vm.Footnote = ctx.Strings.Get("why.stallPick.lesson");
            return vm;
        }
    }
}
