using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Market
{
    /// <summary>Spec 004 §3.1's prices: the regional price, the trader's floor, the import ceiling and input prices.</summary>
    public static class MarketRules
    {
        /// <summary>basePrice × PriceIndex × seasonalFactor (a price-controlled good ignores the index), coins per unit.</summary>
        public static Fixed RegionalPrice(Simulation sim, int good)
        {
            MarketDefs defs = sim.Config.Game.Market;
            GoodDef g = defs.Goods[good];
            Fixed index = g.PriceControlled ? Fixed.One : sim.State.Market.PriceIndex;
            int season = (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
            Fixed p = g.BasePrice * index * defs.Region.SeasonalFactor(good, season);
            // Today's market (spec 011 §3.1b): produce only; inputs keep their seasonal price.
            return g.Kind == GoodKind.Produce && !g.PriceControlled ? p * sim.State.Market.DailyFactor[good] : p;
        }

        /// <summary>transportCostPerKg[roadTier] × weightKg × PriceIndex.</summary>
        public static Fixed TransportCost(Simulation sim, int good)
        {
            MarketDefs defs = sim.Config.Game.Market;
            return defs.Region.TransportCostPerKg * defs.Goods[good].WeightKg * sim.State.Market.PriceIndex;
        }

        /// <summary>The trader's buying price: P_regional − TransportCost.</summary>
        public static Fixed Floor(Simulation sim, int good) { return RegionalPrice(sim, good) - TransportCost(sim, good); }

        /// <summary>The import price: P_regional + TransportCost.</summary>
        public static Fixed Ceiling(Simulation sim, int good) { return RegionalPrice(sim, good) + TransportCost(sim, good); }

        /// <summary>The village dealer's price for an input: its P_regional, delivery included (spec 004 §3.1).</summary>
        public static Fixed InputPrice(Simulation sim, int good) { return RegionalPrice(sim, good); }

        /// <summary>Value(q, P) for this good's unit size.</summary>
        public static long Value(Simulation sim, int good, long quantity, Fixed price)
        {
            return Money.Value(quantity, price, sim.Config.Game.Market.Goods[good].UnitSize);
        }

        /// <summary>
        /// A year-0 coin amount from content charged now: MulRate(amount, PriceIndex) (ADR 0034). Not for
        /// price-controlled goods, loans or book values.
        /// </summary>
        public static long Indexed(Simulation sim, long amount) { return Money.MulRate(amount, sim.State.Market.PriceIndex); }

        /// <summary>
        /// What the trader can still buy of a good today, in quantity units: the crop's room, and with the truck (spec 011
        /// §3.1) the load's room too, or 0 while the truck is away.
        /// </summary>
        public static long TraderCapacityLeft(Simulation sim, int good)
        {
            MarketState m = sim.State.Market;
            long room = sim.Config.Game.Market.Region.TraderBuyPerDay(good) - m.TraderBoughtToday[good];
            TruckDef truck = sim.Config.Game.Market.Truck;
            if (truck == null) return room;
            if (!TraderBuys(sim)) return 0;
            return System.Math.Max(0, System.Math.Min(room, truck.LoadGrams - m.TruckLoadToday));
        }

        /// <summary>
        /// Whether the trader buys now: always without a truck; with one, while it is here, and at any hour until the first
        /// session's first sale is done (spec 011 §3.1, §10 G6).
        /// </summary>
        public static bool TraderBuys(Simulation sim)
        {
            if (sim.Config.Game.Market.Truck == null || sim.State.Market.TruckHere) return true;
            Progress.GuidanceState g = sim.State.Guidance;
            if (g == null || g.Skipped) return false;
            var steps = sim.Config.Game.Onboarding.Steps;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].Id == "firstSale") return g.Step <= i;
            return false;
        }

        /// <summary>The hour of day now, 0–23 (spec 001).</summary>
        public static int HourOfDay(Simulation sim) { return (int)(sim.State.Clock.SimTime % ClockDef.SecondsPerDay / 3600); }

        /// <summary>Whether the stall is closed now, midnight to 6:00 (spec 011 §3.2); never without a truck.</summary>
        public static bool StallClosed(Simulation sim)
        {
            TruckDef truck = sim.Config.Game.Market.Truck;
            return truck != null && truck.StallPausedAtPass((HourOfDay(sim) + 1) % 24);
        }
    }
}
