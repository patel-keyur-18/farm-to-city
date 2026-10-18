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
            return g.BasePrice * index * defs.Region.SeasonalFactor(good, season);
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

        /// <summary>What the trader can still buy of a good today, in quantity units.</summary>
        public static long TraderCapacityLeft(Simulation sim, int good)
        {
            return sim.Config.Game.Market.Region.TraderBuyPerDay(good) - sim.State.Market.TraderBoughtToday[good];
        }
    }
}
