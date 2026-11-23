using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Business
{
    /// <summary>
    /// The restaurant (spec 028 §3.7, US-246, ADR 0163): a shop of the kind "meals". Two sittings a day (lunch and dinner), diners from the households' leisure
    /// budgets, shared with the town dhaba by the shops' attraction score; a portion is cooked to order from your store, your businesses, then the market, so nothing is left
    /// over. Two dishes, a fixed share of diners each, and a dish can be switched off.
    /// </summary>
    public static class MealsRules
    {
        /// <summary>The restaurant's type, or null with no restaurant in the content.</summary>
        public static BusinessTypeDef Type(Simulation sim)
        {
            foreach (BusinessTypeDef t in sim.Config.Game.Businesses.Types) if (t.IsMeals) return t;
            return null;
        }

        /// <summary>Whether any restaurant of yours is open: the diners are only worked out then (a dhaba alone sells to nobody the player can see).</summary>
        public static bool Running(Simulation sim)
        {
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status == BusinessStatus.Open && BusinessRules.TypeOf(sim, b).IsMeals) return true;
            return false;
        }

        /// <summary>Whether <paramref name="hour"/> falls in a sitting: inside the open hours and outside the break.</summary>
        public static bool Sitting(BusinessTypeDef t, int hour)
        {
            return t != null && hour >= t.OpenHour && hour < t.CloseHour && !(hour >= t.Meals.BreakFrom && hour < t.Meals.BreakTo);
        }

        /// <summary>Hours a day it serves: the type's sittings.</summary>
        public static int SittingHours(BusinessTypeDef t) { return t.OpenSpan(t.OpenHour, t.CloseHour); }

        /// <summary>Whether a dish is on the menu.</summary>
        public static bool On(Business b, int dish) { return (b.DishesOff & (1 << dish)) == 0; }

        /// <summary>The dishes on the menu.</summary>
        public static int DishesOn(MealsDef m, int off)
        {
            int n = 0;
            for (int i = 0; i < m.Dishes.Count; i++) if ((off & (1 << i)) == 0) n++;
            return n;
        }

        // ---- prices (§3.7) ----

        /// <summary>A dish's reference price a portion at today's prices.</summary>
        public static Fixed Reference(Simulation sim, DishDef d) { return d.Price * sim.State.Market.PriceIndex; }

        /// <summary>The average portion's reference price: each dish's price weighted by the diners' parts (the households' leisure is sized to it).</summary>
        public static Fixed Average(Simulation sim, MealsDef m)
        {
            Fixed sum = Fixed.Zero;
            foreach (DishDef d in m.Dishes) sum += Reference(sim, d) * Fixed.FromInt(d.Share);
            return sum / Fixed.FromInt(m.ShareTotal);
        }

        /// <summary>What a kg of an input costs the kitchen at the market: the retail price, the market's with the markup (§3.7).</summary>
        public static Fixed MarketPrice(Simulation sim, int good) { return ShopRules.Reference(sim, ShopKind.Veg, good); }

        // ---- demand (§3.7) ----

        /// <summary>
        /// Portions a real day a group wants: each household earning the minimum sets aside a share of its leisure and buys as many portions as that pays for, over a
        /// season; the old village counts as its households at its budget.
        /// </summary>
        public static long PerDay(Simulation sim, ShopGroup g)
        {
            BusinessTypeDef t = Type(sim);
            if (t == null) return 0;
            MealsDef m = t.Meals;
            long price = Money.Value(1, Average(sim, m), 1), min = MarketRules.Indexed(sim, m.MinIncome);
            if (price <= 0) return 0;
            long perSeason = 0;
            if (g == ShopGroup.Town)
            {
                if (sim.State.Citizens == null) return 0;
                foreach (People.Household x in sim.State.Citizens.Households)
                    if (x.Income >= min) perSeason += Money.MulRate(x.Leisure, m.LeisureShare) / price;
            }
            else if (Town.CohortRules.Joined(sim))
            {
                Town.CohortRules.Budget bud = Town.CohortRules.BudgetOf(sim);
                if (bud.Income >= min) perSeason = Town.CohortRules.Households(sim) * (Money.MulRate(bud.Leisure, m.LeisureShare) / price);
            }
            return perSeason / sim.Clock.DaysPerSeason;
        }

        /// <summary>Households of yours that eat out: income at the minimum or more.</summary>
        public static int Diners(Simulation sim)
        {
            BusinessTypeDef t = Type(sim);
            if (t == null || sim.State.Citizens == null) return 0;
            long min = MarketRules.Indexed(sim, t.Meals.MinIncome);
            int n = 0;
            foreach (People.Household x in sim.State.Citizens.Households) if (x.Income >= min) n++;
            return n;
        }

        // ---- cooking ----

        /// <summary>The portions of each dish among <paramref name="q"/> served: the diners' parts, the dishes switched off handing theirs to the rest (the remainder to the first on).</summary>
        public static long[] Split(MealsDef m, int off, long q)
        {
            var n = new long[m.Dishes.Count];
            int total = 0, first = -1;
            for (int i = 0; i < n.Length; i++)
                if ((off & (1 << i)) == 0)
                {
                    total += m.Dishes[i].Share;
                    if (first < 0) first = i;
                }
            if (total == 0 || q <= 0) return n;
            long given = 0;
            for (int i = 0; i < n.Length; i++)
                if ((off & (1 << i)) == 0)
                {
                    n[i] = q * m.Dishes[i].Share / total;
                    given += n[i];
                }
            n[first] += q - given;
            return n;
        }

        /// <summary>A dish's ingredients a portion at the market's price, with its fuel and supplies: what a portion costs at the dearest the kitchen pays (a null business: the plan's, standard quality).</summary>
        public static long PortionCost(Simulation sim, Business b, DishDef d)
        {
            long cost = 0;
            foreach (KeyValuePair<int, long> kv in d.Inputs) cost += MarketRules.Value(sim, kv.Key, kv.Value, MarketPrice(sim, kv.Key));
            Fixed index = sim.State.Market.PriceIndex * (b == null ? Fixed.One : BusinessRules.CostFactor(sim, b));
            long supplies = Money.Value(1, d.Supplies * index, 1);
            return cost + Money.Value(1, d.Fuel * index, 1) + supplies + (b == null ? 0 : BusinessRules.GoodExtra(sim, b, cost + supplies));
        }

        /// <summary>Portions an hour's cash can cook at the dearest (the market's) price, over the dishes on the menu by their diners' parts: the kitchen cooks no more than it can pay for.</summary>
        public static long Afford(Simulation sim, Business b)
        {
            MealsDef m = BusinessRules.TypeOf(sim, b).Meals;
            long cost = 0;
            int total = 0;
            for (int i = 0; i < m.Dishes.Count; i++)
                if (On(b, i))
                {
                    cost += PortionCost(sim, b, m.Dishes[i]) * m.Dishes[i].Share;
                    total += m.Dishes[i].Share;
                }
            if (total == 0) return 0;
            cost = (cost + total - 1) / total;
            return cost <= 0 ? long.MaxValue : System.Math.Max(0, BusinessBooks.CanSpend(sim, b)) / cost;
        }

        /// <summary>
        /// Cooks and sells <paramref name="q"/> portions at the shop's price: the sales in cash, the ingredients from the store at the trader's price, then your businesses'
        /// goods at the trader's price, then the market's (retail) price; fuel and supplies, and good quality's finer ingredients. Returns the sales.
        /// </summary>
        internal static long Sell(Simulation sim, Business b, BusinessTypeDef t, long q)
        {
            MealsDef m = t.Meals;
            long[] n = Split(m, b.DishesOff, q);
            long now = sim.State.Clock.SimTime, total = 0;
            string reference = "business:" + b.Id;
            Fixed index = sim.State.Market.PriceIndex;
            var took = new long[sim.Config.Game.Market.Goods.Count];
            for (int i = 0; i < n.Length; i++)
            {
                if (n[i] <= 0) continue;
                DishDef d = m.Dishes[i];
                long amount = BusinessRules.SalePrice(sim, b, Money.Value(n[i], d.Price * index * b.PriceFactor, 1));
                if (amount > 0) b.Ledger.Post(now, sim.Period, "ledger.sales", reference, new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
                total += amount;
                long ingredients = 0;
                foreach (KeyValuePair<int, long> kv in d.Inputs) ingredients += Cook(sim, b, kv.Key, kv.Value * n[i], took);
                Fixed unit = index * BusinessRules.CostFactor(sim, b);
                long fuel = Money.Value(n[i], d.Fuel * unit, 1), supplies = Money.Value(n[i], d.Supplies * unit, 1);
                supplies += BusinessRules.GoodExtra(sim, b, ingredients + supplies); // good quality: the finer ingredients
                BusinessBooks.Draw(sim, b, fuel + supplies);
                if (fuel > 0) b.Ledger.Post(now, sim.Period, "ledger.fuel", reference, new Posting(Account.Power, fuel), new Posting(Account.Cash, -fuel));
                if (supplies > 0) b.Ledger.Post(now, sim.Period, "ledger.supplies", reference, new Posting(Account.CostOfGoodsSold, supplies), new Posting(Account.Cash, -supplies));
            }
            return total;
        }

        /// <summary>Takes <paramref name="need"/> grams of an ingredient: what the kitchen holds, then your store, your businesses, then the market; used up at its cost.</summary>
        private static long Cook(Simulation sim, Business b, int g, long need, long[] took)
        {
            long want = need - b.Stock[g];
            if (want > 0) want -= ShopRules.FromStore(sim, b, g, want);
            if (want > 0) want -= ShopRules.FromYours(sim, b, g, want, took);
            if (want > 0) ShopRules.FromMarket(sim, b, g, want, MarketPrice(sim, g));
            long use = System.Math.Min(need, b.Stock[g]);
            return use > 0 ? ShopRules.Use(sim, b, g, use) : 0;
        }

        // ---- the plan (§3.7) ----

        /// <summary>
        /// A season's sales and costs for the plan: the portions the restaurant is expected to sell (its share of the diners, capped by the cook's hours), the dishes' prices, their
        /// ingredients at what the kitchen would pay (the store's trader price while it lasts, the market's after), fuel and supplies.
        /// </summary>
        internal static void Plan(Simulation sim, BusinessTypeDef t, long perDay, out long sales, out long inputs, out long fuel)
        {
            MealsDef m = t.Meals;
            int days = sim.Clock.DaysPerSeason;
            long q = System.Math.Min(perDay, t.CapacityPerHour * SittingHours(t)) * days;
            sales = inputs = fuel = 0;
            Fixed index = sim.State.Market.PriceIndex;
            foreach (DishDef d in m.Dishes)
            {
                long n = q * d.Share / m.ShareTotal;
                sales += Money.Value(n, d.Price * index, 1);
                foreach (KeyValuePair<int, long> kv in d.Inputs)
                {
                    long grams = kv.Value * n, held = System.Math.Min(grams, System.Math.Max(0, sim.State.Market.Stock[kv.Key] - sim.State.Market.Offered[kv.Key]));
                    inputs += MarketRules.Value(sim, kv.Key, held, MarketRules.Floor(sim, kv.Key)) + MarketRules.Value(sim, kv.Key, grams - held, MarketPrice(sim, kv.Key));
                }
                inputs += Money.Value(n, d.Supplies * index, 1);
                fuel += Money.Value(n, d.Fuel * index, 1);
            }
        }

        // ---- the menu's switches ----

        /// <summary>Switches a dish on or off (SetDish): a dish off hands its diners to the others, and the last dish cannot be switched off.</summary>
        internal static CommandResult SetDish(Simulation sim, long id, int dish, bool on)
        {
            Business b = sim.State.Businesses?.Find(id);
            if (b == null || !BusinessRules.TypeOf(sim, b).IsMeals || b.Status != BusinessStatus.Open) return CommandResult.Reject(RejectReason.UnknownBusiness);
            MealsDef m = BusinessRules.TypeOf(sim, b).Meals;
            if (dish < 0 || dish >= m.Dishes.Count) return CommandResult.Reject(RejectReason.InvalidArgument);
            int next = on ? b.DishesOff & ~(1 << dish) : b.DishesOff | (1 << dish);
            if (DishesOn(m, next) == 0) return CommandResult.Reject(RejectReason.InvalidArgument);
            b.DishesOff = next;
            return CommandResult.Ok;
        }
    }
}
