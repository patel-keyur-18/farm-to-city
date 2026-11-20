using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Market
{
    /// <summary>
    /// Cold-store space in the mandi town (spec 025 §3.6, US-242, ADR 0134), rented like the warehouse's (ADR 0087): by the tonne for a season, upfront at the
    /// close, from the Township. Potato and fruit (<see cref="ColdStoreDef.Factor"/>) fill it by themselves, fastest-spoiling first, and spoil there at a
    /// share of their rate; what does not fit stays in the hut. What it holds is worked out from the stock each time (<see cref="Cold"/>).
    /// </summary>
    public static class ColdStoreRules
    {
        private const long GramsPerTonne = 1_000_000;

        public static ColdStoreDef Def(Simulation sim) { return sim.Config.Game?.Township?.ColdStore; }

        /// <summary>From the Township.</summary>
        public static bool On(Simulation sim) { return Def(sim) != null && sim.State.Godown != null && Town.TownshipRules.Reached(sim); }

        /// <summary>Whether the cold store keeps a good.</summary>
        public static bool Keeps(Simulation sim, int good)
        {
            ColdStoreDef d = Def(sim);
            return d != null && sim.State.Godown != null && good < d.Factor.Count && d.Factor[good] > Fixed.Zero;
        }

        public static long CapacityGrams(Simulation sim) { return sim.State.Godown == null ? 0 : sim.State.Godown.ColdRentedTonnes * GramsPerTonne; }

        /// <summary>
        /// How much of each good is in the cold store: the goods it keeps fill it fastest-spoiling first, then by id (a fixed order); the rest is in the hut
        /// store. Null without a cold store in the content or with no space rented.
        /// </summary>
        public static long[] Cold(Simulation sim)
        {
            ColdStoreDef d = Def(sim);
            if (d == null || sim.State.Godown == null) return null;
            long free = CapacityGrams(sim);
            if (free == 0) return null; // no space rented: nothing is in it (and the hourly passes allocate nothing)
            var goods = sim.Config.Game.Market.Goods;
            long[] stock = sim.State.Market.Stock;
            var cold = new long[goods.Count];
            foreach (int g in FillOrder(sim))
            {
                if (stock[g] == 0) continue;
                long q = System.Math.Min(stock[g], Store.QuantityFitting(goods[g], free));
                cold[g] = q;
                free -= Store.WeightGrams(goods[g], q);
                if (free <= 0) break;
            }
            return cold;
        }

        private static int[] FillOrder(Simulation sim)
        {
            var goods = sim.Config.Game.Market.Goods;
            int n = 0;
            for (int g = 0; g < goods.Count; g++) if (Keeps(sim, g)) n++;
            var order = new int[n];
            n = 0;
            for (int g = 0; g < goods.Count; g++)
            {
                if (!Keeps(sim, g)) continue;
                int i = n++;
                while (i > 0 && goods[order[i - 1]].SpoilagePerDay < goods[g].SpoilagePerDay) { order[i] = order[i - 1]; i--; }
                order[i] = g;
            }
            return order;
        }

        /// <summary>The weight in the cold store, in grams.</summary>
        public static long UsedGrams(Simulation sim)
        {
            long[] cold = Cold(sim);
            if (cold == null) return 0;
            var goods = sim.Config.Game.Market.Goods;
            long used = 0;
            for (int g = 0; g < cold.Length; g++) if (cold[g] > 0) used += Store.WeightGrams(goods[g], cold[g]);
            return used;
        }

        /// <summary>A good's spoilage a day in the cold store: its rate × the cold store's share.</summary>
        public static Fixed SpoilageInCold(Simulation sim, int good) { return sim.Config.Game.Market.Goods[good].SpoilagePerDay * Def(sim).Factor[good]; }

        // ---- is it worth it? (the sheet's worked answer, at today's prices; an estimate, never a rule) ----

        /// <summary>The share of a kg left after <paramref name="days"/> days in the cold store or in the hut: each day's loss compounds.</summary>
        public static Fixed KeptShare(Simulation sim, int good, int days, bool cold)
        {
            Fixed loss = sim.Config.Game.Market.Goods[good].SpoilagePerDay;
            if (cold) loss = loss * Def(sim).Factor[good];
            Fixed keep = Fixed.One;
            for (int i = 0; i < days; i++) keep = keep * (Fixed.One - loss);
            return keep;
        }

        /// <summary>
        /// The best way to use a season's price swing: keep a kg bought or grown in <see cref="FromSeason"/> for <see cref="Seasons"/> seasons (1 to 3) and
        /// sell it then, against selling it at once. <see cref="Net"/> is what is left a kg after the cold store's rent and its spoilage (coins).
        /// </summary>
        public readonly struct Worth
        {
            public int Good { get; }
            public int FromSeason { get; }
            public int Seasons { get; }
            public Fixed StartPrice { get; }
            public Fixed EndPrice { get; }
            public Fixed Rent { get; }
            public Fixed Spoiled { get; }
            public Fixed Net { get; }
            public Worth(int good, int from, int seasons, Fixed start, Fixed end, Fixed rent, Fixed spoiled, Fixed net)
            {
                Good = good; FromSeason = from; Seasons = seasons; StartPrice = start; EndPrice = end; Rent = rent; Spoiled = spoiled; Net = net;
            }

            public int ToSeason { get { return (FromSeason + Seasons) % 4; } }
        }

        /// <summary>The best hold for a good the cold store keeps, by what it nets a kg; false when none nets anything (the price is best at once).</summary>
        public static bool BestHold(Simulation sim, int good, out Worth best)
        {
            best = default(Worth);
            if (!Keeps(sim, good)) return false;
            Definitions.MarketDefs defs = sim.Config.Game.Market;
            GoodDef g = defs.Goods[good];
            Fixed index = g.PriceControlled ? Fixed.One : sim.State.Market.PriceIndex;
            Fixed rentSeason = Fixed.FromRatio(Def(sim).RentPerKgSeason, 100) * sim.State.Market.PriceIndex;
            int days = sim.Clock.DaysPerSeason;
            bool found = false;
            Fixed top = Fixed.Zero;
            for (int seasons = 1; seasons <= 3; seasons++)
                for (int from = 0; from < 4; from++)
                {
                    int to = (from + seasons) % 4;
                    Fixed start = g.BasePrice * index * defs.Region.SeasonalFactor(good, from), end = g.BasePrice * index * defs.Region.SeasonalFactor(good, to);
                    Fixed rent = rentSeason * Fixed.FromInt(seasons);
                    Fixed kept = end * KeptShare(sim, good, days * seasons, true);
                    Fixed net = kept - start - rent;
                    if (net <= top) continue;
                    top = net;
                    found = true;
                    best = new Worth(good, from, seasons, start, end, rent, end - kept, net);
                }
            return found;
        }

        /// <summary>A season's rent for <paramref name="tonnes"/> at today's prices (ADR 0034: every coin amount follows the index).</summary>
        public static long SeasonRent(Simulation sim, int tonnes) { return MarketRules.Indexed(sim, tonnes * 1000L * Def(sim).RentPerKgSeason); }

        /// <summary>Rent for more tonnes booked now: the days left in this season (rounded down).</summary>
        public static long RentForRestOfSeason(Simulation sim, int tonnes)
        {
            long season = sim.Clock.SecondsPerSeason, left = GodownRules.SeasonEnd(sim) - sim.State.Clock.SimTime;
            return (long)(new System.Numerics.BigInteger(SeasonRent(sim, tonnes)) * left / season);
        }

        /// <summary>The tonnes the next close rents: what you booked, or what your goods in it need, whichever is more (never below the goods stored). 0 when none.</summary>
        public static int NextCloseTonnes(Simulation sim)
        {
            if (!On(sim)) return 0;
            int needed = (int)((UsedGrams(sim) + GramsPerTonne - 1) / GramsPerTonne);
            return System.Math.Max(sim.State.Godown.ColdRentNextTonnes, needed);
        }

        /// <summary>What the next close will charge for cold space (ADR 0101 §1).</summary>
        public static long NextCloseRent(Simulation sim)
        {
            int tonnes = NextCloseTonnes(sim);
            return tonnes == 0 ? 0 : SeasonRent(sim, tonnes);
        }

        /// <summary>At the season close, after the warehouse's: the cold space for the season, never below what your goods in it need, its rent paid upfront. Unpaid, the space ends and the goods go back to the hut.</summary>
        internal static void Close(Simulation sim)
        {
            GodownState s = sim.State.Godown;
            if (s == null || Def(sim) == null) return;
            long t = sim.State.Clock.SimTime;
            int tonnes = NextCloseTonnes(sim);
            s.ColdRentedTonnes = s.ColdRentNextTonnes = 0;
            if (tonnes == 0) return;
            long cost = SeasonRent(sim, tonnes);
            if (!sim.State.Ledger.CanAfford(cost))
            {
                sim.Emit(new ObligationUnpaid(t, "Cold store", "coldstore", cost));
                return;
            }
            if (cost > 0) Accrual.Open(sim, Account.Rent, Account.PrepaidRent, cost, t, GodownRules.SeasonEnd(sim), "coldstore");
            s.ColdRentedTonnes = s.ColdRentNextTonnes = tonnes;
            sim.Emit(new ObligationPaid(t, "Cold store", "coldstore", cost));
        }
    }
}
