using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// The restaurant's sheets (US-246, spec 028 §3.7, ADR 0163, Preview 18): its market research, its menu and its sittings. Every number has a *Why?* (the <c>meals</c>
    /// source: a research row is the row itself, a business's row is business × 100 + row, a dish's row is 20 + the dish).
    /// </summary>
    public static class MealsSheet
    {
        public const string Why = "meals";
        public const int Diners = 1, Kitchen = 2, Sittings = 3, Menu = 20;

        /// <summary>A dish's price a portion at today's prices and a shop's setting.</summary>
        public static long DishPrice(Simulation sim, DishDef d, Fixed factor) { return Money.Value(1, MealsRules.Reference(sim, d) * factor, 1); }

        /// <summary>"Lunch 11:00 to 15:00 and dinner 18:00 to 22:00".</summary>
        public static string Sittings_(ViewContext ctx, BusinessTypeDef t)
        {
            return ctx.Strings.Get("meals.sittingsValue", "lunch", BusinessWords.Hours(ctx, t.OpenHour, t.Meals.BreakFrom), "dinner", BusinessWords.Hours(ctx, t.Meals.BreakTo, t.CloseHour));
        }

        /// <summary>A dish as a row: its price, the diners' part and what a portion costs to cook.</summary>
        private static LineVm DishLine(ViewContext ctx, DishDef d, int dish, long id, Fixed factor, bool on)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            MealsDef m = MealsRules.Type(sim).Meals;
            string note = on ? s.Get("meals.dishNote", "part", ctx.Fmt.Integer(d.Share), "all", ctx.Fmt.Integer(m.ShareTotal), "cost", ctx.Fmt.Coins(MealsRules.PortionCost(sim, null, d)))
                : s.Get("meals.dishOff");
            return new LineVm(s.Get(d.NameKey), ctx.Fmt.Coins(DishPrice(sim, d, factor)), Why, id * 100 + Menu + dish) { Icon = "restaurant", Note = note, NoteTone = on ? Tone.Plain : Tone.Warn };
        }

        /// <summary>Research: who eats out, what they want, the menu, the kitchen's hours.</summary>
        internal static void AddResearch(ViewContext ctx, SheetVm vm, BusinessTypeDef t, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long demand = 0;
            foreach (ShopGroup g in new[] { ShopGroup.Town, ShopGroup.Village }) demand += ShopRules.DemandPerDay(sim, ShopKind.Meals, -1, g);
            vm.Lines.Add(new LineVm(s.Get("research.mealsDiners"), s.Get("research.mealsDinersValue", "town", ctx.Fmt.Integer(MealsRules.Diners(sim)), "village", ctx.Fmt.Integer(Sim.Core.Town.CohortRules.Households(sim))), Why, Diners) { Icon = "population" });
            vm.Lines.Add(new LineVm(s.Get("research.shopDemand.meals"), ShopWords.Amount(ctx, ShopKind.Meals, demand), "shopDemand", type) { Icon = "restaurant" });
            for (int i = 0; i < t.Meals.Dishes.Count; i++) vm.Lines.Add(DishLine(ctx, t.Meals.Dishes[i], i, 0, Fixed.One, true));
            vm.Lines.Add(new LineVm(s.Get("research.mealsKitchen"), s.Get("research.mealsKitchenValue", "n", ctx.Fmt.Integer(t.CapacityPerHour), "hours", ctx.Fmt.Integer(MealsRules.SittingHours(t))), Why, Kitchen) { Icon = "restaurant" });
            vm.Lines.Add(new LineVm(s.Get("research.mealsSittings"), Sittings_(ctx, t), Why, Sittings) { Icon = "restaurant" });
        }

        /// <summary>Today: the menu with each dish's price, and the kitchen's rate and sittings.</summary>
        internal static void AddToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            long id = b.Id;
            for (int i = 0; i < t.Meals.Dishes.Count; i++) vm.Lines.Add(DishLine(ctx, t.Meals.Dishes[i], i, id, b.PriceFactor, MealsRules.On(b, i)));
            vm.Lines.Add(new LineVm(s.Get("meals.kitchen"), s.Get("meals.kitchenValue", "n", ctx.Fmt.Integer(ShopRules.Capacity(ctx.Sim, b))), Why, id * 100 + Kitchen) { Icon = "restaurant", Note = Sittings_(ctx, t) });
        }

        /// <summary>Settings: the sittings (fixed) and a switch for each dish.</summary>
        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            long id = b.Id;
            vm.Lines.Add(new LineVm(s.Get("meals.sittings"), Sittings_(ctx, t), Why, id * 100 + Sittings) { Icon = "restaurant", Note = s.Get("meals.sittingsNote") });
            int on = MealsRules.DishesOn(t.Meals, b.DishesOff);
            for (int i = 0; i < t.Meals.Dishes.Count; i++)
            {
                int dish = i;
                bool now = MealsRules.On(b, i);
                LineVm line = DishLine(ctx, t.Meals.Dishes[i], i, id, b.PriceFactor, now);
                if (now && on == 1) line.Note = s.Get("meals.lastDish");
                else line.Action = ActionVm.Checked(ctx, "dish:" + i, s.Get(now ? "meals.switchOff" : "meals.switchOn"), null, ActionKind.Secondary, at => new SetDish(at, id, dish, !now));
                vm.Lines.Add(line);
            }
        }
    }
}
