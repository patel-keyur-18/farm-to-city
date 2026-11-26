using System.Collections.Generic;
using Sim.Core.Definitions;

namespace Sim.Core.Business
{
    /// <summary>
    /// Today's supply and Manual's caps (spec 030 §3.4, §3.5, US-268; save schema 25). Every gram a business takes from you (your store, your herd,
    /// your other businesses) or buys is counted for the supply day, from 6:00 to 6:00. Automatic is today's rule untouched; under Manual a good's
    /// "from you" is capped a day, and what isn't sent stays in your store. The counters are display and ranking only: no rule reads them but the cap.
    /// </summary>
    public static class SupplyRules
    {
        /// <summary>The save schema that added the supply state.</summary>
        public const int Schema = 25;

        /// <summary>The supply day a time falls in: it turns at the morning round (6:00).</summary>
        public static long DayIndex(Simulation sim)
        {
            int start = sim.Config.Game.Market.Truck?.ArriveHour ?? 6;
            return (sim.State.Clock.SimTime - start * 3600L) / 86_400L;
        }

        /// <summary>Makes the per-good arrays (a business made or loaded before schema 25 has none) and turns the counters to today.</summary>
        internal static void Touch(Simulation sim, Business b)
        {
            if (b.FromYouToday == null)
            {
                int goods = b.Stock.Length;
                b.FromYouToday = new long[goods]; b.BoughtToday = new long[goods]; b.CapGrams = new long[goods]; b.CapShare = new int[goods];
                for (int i = 0; i < goods; i++) b.CapShare[i] = 100;
                b.SupplyDay = DayIndex(sim);
            }
            long day = DayIndex(sim);
            if (b.SupplyDay == day) return;
            int keep = BusinessRules.Defs(sim).SupplyLookbackDays;
            for (long d = b.SupplyDay; d < day && d < b.SupplyDay + keep; d++) // yesterday's buying, then any quiet days between
            {
                b.BoughtDays.Insert(0, d == b.SupplyDay ? (long[])b.BoughtToday.Clone() : new long[b.Stock.Length]);
                if (b.BoughtDays.Count > keep) b.BoughtDays.RemoveAt(b.BoughtDays.Count - 1);
            }
            System.Array.Clear(b.FromYouToday, 0, b.FromYouToday.Length);
            System.Array.Clear(b.BoughtToday, 0, b.BoughtToday.Length);
            b.SupplyDay = day;
        }

        /// <summary>Counts grams of a good that came from you today.</summary>
        internal static void FromYou(Simulation sim, Business b, int good, long grams)
        {
            if (grams <= 0) return;
            Touch(sim, b);
            b.FromYouToday[good] += grams;
        }

        /// <summary>Counts grams of a good bought today (at the market, from neighbours or at the import price).</summary>
        internal static void Bought(Simulation sim, Business b, int good, long grams)
        {
            if (grams <= 0) return;
            Touch(sim, b);
            b.BoughtToday[good] += grams;
        }

        /// <summary>What came from you today (0 when the counters are from an earlier day: they turn at the next count or the next view).</summary>
        public static long FromYouTodayOf(Simulation sim, Business b, int good)
        {
            return b.FromYouToday == null || b.SupplyDay != DayIndex(sim) ? 0 : b.FromYouToday[good];
        }

        public static long BoughtTodayOf(Simulation sim, Business b, int good)
        {
            return b.BoughtToday == null || b.SupplyDay != DayIndex(sim) ? 0 : b.BoughtToday[good];
        }

        /// <summary>Grams of a good bought over today and the days before it that the history keeps (<c>supplyLookbackDays</c> in all).</summary>
        public static long BoughtOver(Simulation sim, Business b, int good)
        {
            long sum = BoughtTodayOf(sim, b, good);
            int keep = BusinessRules.Defs(sim).SupplyLookbackDays - 1;
            for (int i = 0; i < b.BoughtDays.Count && i < keep; i++) sum += b.BoughtDays[i][good];
            return sum;
        }

        // ---- what a business can be set for ----

        /// <summary>
        /// Whether a business has the Automatic or Manual choice: a stall or kirana, a bakery or a sweet shop (their products from your other businesses), the restaurant.
        /// The mill, dairy and tea stall have their own amount a day instead (<c>OwnPerDay</c>).
        /// </summary>
        public static bool HasMode(Simulation sim, BusinessTypeDef t)
        {
            if (t.IsKiln || t.IsTuition || t.TakesMilk) return false;
            return CappedInputs(sim, t).Count > 0;
        }

        /// <summary>The goods that can come to a business from you and so can be capped: a stall's goods, a recipe's inputs your businesses make, a restaurant's ingredients.</summary>
        public static List<int> CappedInputs(Simulation sim, BusinessTypeDef t)
        {
            var list = new List<int>();
            if (t.IsKiln || t.IsTuition || t.TakesMilk) return list;
            if (ShopRules.Stocked(t)) { list.AddRange(t.Goods); return list; }
            if (t.IsMeals)
            {
                foreach (DishDef d in t.Meals.Dishes) foreach (KeyValuePair<int, long> kv in d.Inputs) if (!list.Contains(kv.Key)) list.Add(kv.Key);
                return list;
            }
            foreach (RecipeDef r in t.Recipes)
                foreach (KeyValuePair<int, long> kv in r.Inputs)
                    if (!t.Makes(kv.Key) && !list.Contains(kv.Key) && MadeByYours(sim, kv.Key)) list.Add(kv.Key);
            return list;
        }

        /// <summary>Whether one of the business kinds you can found makes the good (atta, by the mill, for the bakery).</summary>
        private static bool MadeByYours(Simulation sim, int good)
        {
            foreach (BusinessTypeDef x in BusinessRules.Defs(sim).Types) if (x.Makes(good) || (!x.IsShop && !x.ByRecipe && x.Outputs.Count > 0 && System.Linq.Enumerable.Any(x.Outputs, o => o.Good == good))) return true;
            return false;
        }

        /// <summary>Every good a business counts, capped or not: the recipe's inputs bought too, a mill's wheat, a tea stall's milk.</summary>
        public static List<int> Counted(Simulation sim, Business b)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            var list = CappedInputs(sim, t);
            if (t.IsKiln || t.IsTuition) return list;
            if (t.TakesMilk) { foreach (var kv in MilkGoods(sim)) if (!list.Contains(kv)) list.Add(kv); return list; }
            if (t.ByRecipe) { foreach (RecipeDef r in t.Recipes) foreach (KeyValuePair<int, long> kv in r.Inputs) if (!list.Contains(kv.Key)) list.Add(kv.Key); return list; }
            if (!t.IsShop && t.Input >= 0 && !list.Contains(t.Input)) list.Add(t.Input); // the mill's wheat
            return list;
        }

        private static List<int> MilkGoods(Simulation sim)
        {
            var list = new List<int>();
            foreach (AnimalDef a in sim.Config.Game.Livestock.Animals) { int g = sim.Config.Game.Market.IndexOf("milk_" + a.Id); if (g >= 0) list.Add(g); }
            return list;
        }

        // ---- the cap ----

        /// <summary>A good's day's amount, the whole a share is taken of: a stall's put out, a recipe's inputs for its day's mix. 0 for a restaurant (it has none).</summary>
        public static long DayAmount(Simulation sim, Business b, int good)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            if (ShopRules.Stocked(t)) return b.PutOut[good];
            if (t.IsMeals) return 0;
            long sum = 0;
            foreach (RecipeDef r in t.Recipes)
                foreach (KeyValuePair<int, long> kv in r.Inputs)
                    if (kv.Key == good) sum += (long)((System.Numerics.BigInteger)kv.Value * b.Mix[r.Output] + r.OutputGrams - 1) / r.OutputGrams;
            return sum;
        }

        /// <summary>The most a Manual business takes of a good from you a day, grams; <see cref="long.MaxValue"/> when it has no cap (Automatic, or "all it needs").</summary>
        public static long CapOf(Simulation sim, Business b, int good)
        {
            if (!b.SupplyManual || b.CapGrams == null) return long.MaxValue;
            if (b.CapGrams[good] > 0) return b.CapGrams[good];
            int share = b.CapShare[good];
            if (share >= 100) return long.MaxValue;
            return DayAmount(sim, b, good) * share / 100; // a share floors to the gram
        }

        /// <summary>How much of <paramref name="need"/> the business may still take from you today: all of it under Automatic, else what the cap leaves.</summary>
        internal static long Allowed(Simulation sim, Business b, int good, long need)
        {
            if (!b.SupplyManual) return need;
            long cap = CapOf(sim, b, good);
            if (cap == long.MaxValue) return need;
            Touch(sim, b);
            return System.Math.Max(0, System.Math.Min(need, cap - b.FromYouToday[good]));
        }

        /// <summary>The shares Manual offers a stall or bakery for a good, and the fixed amounts that fit under its day's amount.</summary>
        public static readonly int[] Shares = { 100, 50, 25, 0 };
    }
}
