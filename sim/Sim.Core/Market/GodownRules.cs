using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;

namespace Sim.Core.Market
{
    /// <summary>
    /// Your godown in the mandi town and the warehouse there (spec 013 §3.4, ADR 0087). The dry goods (<see cref="GodownDef.Holds"/>)
    /// fill your godown first, then the space you rent, then the hut store; what each place holds is worked out from the stock each
    /// time (<see cref="Dry"/>), so taking goods out empties the hut store first. Goods in the godown or the warehouse spoil at
    /// <see cref="GodownDef.SpoilageFactor"/> of their rate.
    /// </summary>
    public static class GodownRules
    {
        private const long GramsPerTonne = 1_000_000;

        public static GodownDef Def(Simulation sim) { return sim.Config.Game?.Godown; }

        /// <summary>The godown and the warehouse come with the Hamlet.</summary>
        public static bool Unlocked(Simulation sim)
        {
            return sim.State.Godown != null && (sim.State.Era == null || sim.State.Era.Current >= Def(sim).UnlockEra);
        }

        /// <summary>Your godown (a building with no tile: it stands in the mandi town), or null.</summary>
        public static Building Owned(Simulation sim)
        {
            GodownDef d = Def(sim);
            if (d == null || sim.State.Land == null) return null;
            foreach (Building b in sim.State.Land.Buildings) if (b.Def == d.Building) return b;
            return null;
        }

        /// <summary>Whether a good is kept in the godown and the warehouse.</summary>
        public static bool Holds(Simulation sim, int good)
        {
            GodownDef d = Def(sim);
            return d != null && sim.State.Godown != null && d.Holds[good];
        }

        /// <summary>Your godown's room for your goods: none while it is let.</summary>
        public static long OwnCapacityGrams(Simulation sim)
        {
            return Owned(sim) == null || sim.State.Godown.LetNow ? 0 : Def(sim).CapacityKg * 1000;
        }

        public static long RentedCapacityGrams(Simulation sim) { return sim.State.Godown == null ? 0 : sim.State.Godown.RentedTonnes * GramsPerTonne; }

        public static long CapacityGrams(Simulation sim) { return OwnCapacityGrams(sim) + RentedCapacityGrams(sim); }

        /// <summary>
        /// How much of each good is in your godown and rented space: the dry goods fill it fastest-spoiling first (they gain the
        /// most), then by id; the rest is in the hut store. Null without a godown in the content.
        /// </summary>
        public static long[] Dry(Simulation sim)
        {
            GodownDef d = Def(sim);
            if (d == null || sim.State.Godown == null) return null;
            var goods = sim.Config.Game.Market.Goods;
            long[] stock = sim.State.Market.Stock;
            var dry = new long[goods.Count];
            long free = CapacityGrams(sim);
            if (free == 0) return dry;
            foreach (int g in FillOrder(sim))
            {
                if (stock[g] == 0) continue;
                long q = System.Math.Min(stock[g], Store.QuantityFitting(goods[g], free));
                dry[g] = q;
                free -= Store.WeightGrams(goods[g], q);
                if (free <= 0) break;
            }
            return dry;
        }

        /// <summary>The dry goods by spoilage rate, fastest first, then by id (a fixed order, never a dictionary's).</summary>
        private static int[] FillOrder(Simulation sim)
        {
            var goods = sim.Config.Game.Market.Goods;
            GodownDef d = Def(sim);
            int n = 0;
            for (int g = 0; g < goods.Count; g++) if (d.Holds[g]) n++;
            var order = new int[n];
            n = 0;
            for (int g = 0; g < goods.Count; g++)
            {
                if (!d.Holds[g]) continue;
                int i = n++;
                while (i > 0 && goods[order[i - 1]].SpoilagePerDay < goods[g].SpoilagePerDay) { order[i] = order[i - 1]; i--; }
                order[i] = g;
            }
            return order;
        }

        /// <summary>The weight in your godown and rented space together, in grams.</summary>
        public static long UsedGrams(Simulation sim)
        {
            long[] dry = Dry(sim);
            if (dry == null) return 0;
            var goods = sim.Config.Game.Market.Goods;
            long used = 0;
            for (int g = 0; g < dry.Length; g++) if (dry[g] > 0) used += Store.WeightGrams(goods[g], dry[g]);
            return used;
        }

        /// <summary>All your dry goods, wherever they are (the godown, rented space or the hut store), in grams.</summary>
        public static long AllDryGrams(Simulation sim)
        {
            GodownDef d = Def(sim);
            if (d == null) return 0;
            var goods = sim.Config.Game.Market.Goods;
            long all = 0;
            for (int g = 0; g < goods.Count; g++) if (d.Holds[g]) all += Store.WeightGrams(goods[g], sim.State.Market.Stock[g]);
            return all;
        }

        /// <summary>What your goods need of the rented space: everything past your own godown.</summary>
        public static long InRentedGrams(Simulation sim)
        {
            return System.Math.Max(0, UsedGrams(sim) - OwnCapacityGrams(sim));
        }

        /// <summary>A season's warehouse rent for <paramref name="tonnes"/> at today's prices (ADR 0034: every coin amount follows the index).</summary>
        public static long SeasonRent(Simulation sim, int tonnes)
        {
            return MarketRules.Indexed(sim, tonnes * 1000L * Def(sim).WarehouseRentPerKgSeason);
        }

        /// <summary>What a trader pays for your godown for a season, at today's prices.</summary>
        public static long LetRent(Simulation sim)
        {
            return MarketRules.Indexed(sim, Def(sim).CapacityKg * Def(sim).LetRentPerKgSeason);
        }

        /// <summary>The end of the current season (the next season close).</summary>
        public static long SeasonEnd(Simulation sim)
        {
            long season = sim.Clock.SecondsPerSeason;
            return (sim.State.Clock.SimTime / season + 1) * season;
        }

        /// <summary>Rent for more tonnes booked now: the days left in this season (rounded down).</summary>
        public static long RentForRestOfSeason(Simulation sim, int tonnes)
        {
            long season = sim.Clock.SecondsPerSeason, left = SeasonEnd(sim) - sim.State.Clock.SimTime;
            return (long)(new System.Numerics.BigInteger(SeasonRent(sim, tonnes)) * left / season);
        }

        /// <summary>
        /// At the season close, with the payments that belong to the new season (spec 013 §3.4, ADR 0087): first whether your
        /// godown is let (only if all your dry goods, the hut store's too, fit the space you rent from now; a seeded chance a trader takes it; his rent upfront,
        /// Cr 4200), then the warehouse space for the season, never below what your goods need, its rent paid upfront (prepaid,
        /// expensed to 5300 through the season). Unpaid, the space ends and the goods go back to the hut store.
        /// </summary>
        internal static void Close(Simulation sim)
        {
            GodownState s = sim.State.Godown;
            if (s == null) return;
            GodownDef d = Def(sim);
            long t = sim.State.Clock.SimTime;
            long used = UsedGrams(sim);

            s.LetNow = false;
            s.LastOutcome = LetOutcome.None;
            if (s.LetNext && Owned(sim) != null)
            {
                if (AllDryGrams(sim) > s.RentNextTonnes * GramsPerTonne) s.LastOutcome = LetOutcome.YourGoods;
                else if (!Rng.Chance(Rng.Next(sim.State.Seed, RngStream.Godown, 0, (ulong)sim.Period), d.TenantChance)) s.LastOutcome = LetOutcome.NoTenant;
                else
                {
                    s.LetNow = true;
                    long rent = LetRent(sim);
                    if (rent > 0)
                        sim.State.Ledger.Post(t, sim.Period, "ledger.godownLet", "godown", new Posting(Account.Cash, rent), new Posting(Account.RentReceived, -rent));
                    sim.Emit(new GodownLet(t, rent));
                }
                if (s.LastOutcome != LetOutcome.None) sim.Emit(new GodownNotLet(t, s.LastOutcome));
            }

            if (!s.LetNow) used = UsedGrams(sim); // a godown no longer let takes your goods back
            long own = Owned(sim) == null || s.LetNow ? 0 : d.CapacityKg * 1000;
            long needed = System.Math.Max(0, used - own);
            int tonnes = System.Math.Max(s.RentNextTonnes, (int)((needed + GramsPerTonne - 1) / GramsPerTonne));
            s.RentedTonnes = s.RentNextTonnes = 0;
            if (tonnes == 0) return;
            long cost = SeasonRent(sim, tonnes);
            if (!sim.State.Ledger.CanAfford(cost))
            {
                sim.Emit(new ObligationUnpaid(t, "Warehouse", "warehouse", cost));
                return;
            }
            if (cost > 0) Accrual.Open(sim, Account.Rent, Account.PrepaidRent, cost, t, SeasonEnd(sim), "warehouse");
            s.RentedTonnes = s.RentNextTonnes = tonnes;
            sim.Emit(new ObligationPaid(t, "Warehouse", "warehouse", cost));
        }
    }
}
