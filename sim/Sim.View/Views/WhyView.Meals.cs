using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The restaurant (US-246): who eats out, the menu's prices and costs, the kitchen and its sittings explained in words.</summary>
    public static partial class WhyView
    {
        /// <summary>What a household's leisure buys a season (spec 028 §3.7): who eats out, the share of leisure, the portion's price, spread over the days.</summary>
        private static WhyVm MealsDemand(ViewContext ctx, long type)
        {
            Simulation sim = ctx.Sim;
            BusinessTypeDef t = MealsRules.Type(sim);
            MealsDef m = t.Meals;
            long total = 0;
            foreach (ShopGroup g in new[] { ShopGroup.Town, ShopGroup.Village }) total += ShopRules.DemandPerDay(sim, ShopKind.Meals, -1, g);
            WhyVm vm = New(ctx, "why.shopDemand.title.meals", ShopWords.Amount(ctx, ShopKind.Meals, total));
            Row(vm, ctx, "why.mealsDemand.town", ShopWords.Amount(ctx, ShopKind.Meals, ShopRules.DemandPerDay(sim, ShopKind.Meals, -1, ShopGroup.Town)), "n", ctx.Fmt.Integer(MealsRules.Diners(sim)));
            Row(vm, ctx, "why.mealsDemand.village", ShopWords.Amount(ctx, ShopKind.Meals, ShopRules.DemandPerDay(sim, ShopKind.Meals, -1, ShopGroup.Village)), "n", ctx.Fmt.Integer(Sim.Core.Town.CohortRules.Households(sim)));
            Row(vm, ctx, "why.mealsDemand.price", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(1, MealsRules.Average(sim, m), 1)));
            Text(vm, ctx, "why.mealsDemand.how", "share", ctx.Fmt.Percent(m.LeisureShare), "min", ctx.Fmt.Coins(MarketRules.Indexed(sim, m.MinIncome)), "days", ctx.Fmt.Integer(sim.Clock.DaysPerSeason));
            Row(vm, ctx, "why.shopDemand.price", ctx.Fmt.Percent(ShopRules.Defs(sim).Kind(ShopKind.Meals).Elasticity.MulRatio(1, 10)));
            return vm;
        }

        /// <summary>A restaurant row: the research rows (id = the row) and the business's rows (id = business × 100 + row; a dish is row 20 + the dish).</summary>
        private static WhyVm MealsWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int row = (int)(id % 100);
            Business b = id >= 100 ? BusinessOf(ctx, id / 100) : null;
            BusinessTypeDef t = MealsRules.Type(sim);
            MealsDef m = t.Meals;
            switch (row)
            {
                case MealsSheet.Diners:
                {
                    WhyVm vm = New(ctx, "why.mealsDiners.title", ctx.Fmt.Integer(MealsRules.Diners(sim) + Sim.Core.Town.CohortRules.Households(sim)));
                    Text(vm, ctx, "why.mealsDiners.text", "min", ctx.Fmt.Coins(MarketRules.Indexed(sim, m.MinIncome)));
                    Row(vm, ctx, "why.mealsDiners.yours", ctx.Fmt.Integer(MealsRules.Diners(sim)));
                    Row(vm, ctx, "why.mealsDiners.village", ctx.Fmt.Integer(Sim.Core.Town.CohortRules.Households(sim)));
                    return vm;
                }
                case MealsSheet.Kitchen:
                {
                    WhyVm vm = New(ctx, "why.mealsKitchen.title", ctx.Fmt.Integer(b == null ? t.CapacityPerHour : ShopRules.Capacity(sim, b)));
                    Text(vm, ctx, "why.mealsKitchen.text", "hours", ctx.Fmt.Integer(MealsRules.SittingHours(t)), "day", ctx.Fmt.Integer((b == null ? t.CapacityPerHour : ShopRules.Capacity(sim, b)) * MealsRules.SittingHours(t)));
                    Text(vm, ctx, "why.mealsKitchen.cash");
                    return vm;
                }
                case MealsSheet.Sittings:
                {
                    WhyVm vm = New(ctx, "why.mealsSittings.title", MealsSheet.Sittings_(ctx, t));
                    Text(vm, ctx, "why.mealsSittings.text", "from", ctx.Fmt.Integer(m.BreakFrom), "to", ctx.Fmt.Integer(m.BreakTo));
                    return vm;
                }
                default:
                {
                    DishDef d = m.Dishes[row - MealsSheet.Menu];
                    Fixed factor = b == null ? Fixed.One : b.PriceFactor;
                    WhyVm vm = New(ctx, "why.mealsDish.title", ctx.Fmt.Coins(MealsSheet.DishPrice(sim, d, factor)), "name", ctx.Strings.Get(d.NameKey));
                    Text(vm, ctx, "why.mealsDish.text", "part", ctx.Fmt.Integer(d.Share), "all", ctx.Fmt.Integer(m.ShareTotal));
                    foreach (System.Collections.Generic.KeyValuePair<int, long> kv in d.Inputs)
                        Row(vm, ctx, "why.mealsDish.input", ctx.Fmt.Coins(MarketRules.Value(sim, kv.Key, kv.Value, MealsRules.MarketPrice(sim, kv.Key))), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[kv.Key].NameKey), "g", ctx.Fmt.Integer(kv.Value));
                    Fixed index = sim.State.Market.PriceIndex;
                    Row(vm, ctx, "why.mealsDish.fuel", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(1, d.Fuel * index, 1)));
                    Row(vm, ctx, "why.mealsDish.supplies", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(1, d.Supplies * index, 1)));
                    Row(vm, ctx, "why.mealsDish.cost", ctx.Fmt.Coins(MealsRules.PortionCost(sim, b, d)));
                    Text(vm, ctx, "why.mealsDish.buying");
                    return vm;
                }
            }
        }
    }
}
