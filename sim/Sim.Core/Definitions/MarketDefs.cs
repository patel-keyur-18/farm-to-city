using System;
using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    public enum GoodKind
    {
        Produce = 0,
        Input = 1,
    }

    /// <summary>How a good is counted: grams for kg goods, millilitres for litre goods, units for goods sold each (spec 004 §3).</summary>
    public enum GoodUnit
    {
        Kg = 0,
        Litre = 1,
        Each = 2,
    }

    /// <summary>Which sales account a produce good's sales post to (spec 004 §4).</summary>
    public enum SalesKind
    {
        None = 0,
        Crop = 1,
        Fruit = 2,
    }

    /// <summary>A tradeable good from <c>content/goods.json</c> (spec 004 §4).</summary>
    public sealed class GoodDef
    {
        public string Id { get; }
        public string NameKey { get; }
        public GoodKind Kind { get; }
        public GoodUnit Unit { get; }

        /// <summary>The year-0 price in coins per kg, litre or unit; never edited for inflation (ADR 0012).</summary>
        public Fixed BasePrice { get; }

        /// <summary>Price elasticity of demand (produce; the market in Sprint 3). Zero for inputs.</summary>
        public Fixed Elasticity { get; }
        public Fixed SpoilagePerDay { get; }
        public SalesKind Sales { get; }
        public bool Edible { get; }

        /// <summary>Weight of one kg, litre or unit, in kg (store capacity).</summary>
        public Fixed WeightKg { get; }

        /// <summary>A government-fixed price that ignores the price index (chemical fertiliser, ADR 0031).</summary>
        public bool PriceControlled { get; }

        public GoodDef(string id, string nameKey, GoodKind kind, GoodUnit unit, Fixed basePrice, Fixed elasticity,
            Fixed spoilagePerDay, SalesKind sales, bool edible, Fixed weightKg, bool priceControlled)
        {
            Id = id;
            NameKey = nameKey;
            Kind = kind;
            Unit = unit;
            BasePrice = basePrice;
            Elasticity = elasticity;
            SpoilagePerDay = spoilagePerDay;
            Sales = sales;
            Edible = edible;
            WeightKg = weightKg;
            PriceControlled = priceControlled;
        }

        /// <summary>Quantity units per kg, litre or unit: 1,000 for g and ml, 1 for goods sold each.</summary>
        public long UnitSize { get { return Unit == GoodUnit.Each ? 1 : 1000; } }
    }

    /// <summary>The region's prices and trader from <c>content/region.json</c> (spec 004 §4; the market fields join in Sprint 3).</summary>
    public sealed class RegionDef
    {
        public string RoadTier { get; }

        /// <summary>Coins per kg on the current road tier.</summary>
        public Fixed TransportCostPerKg { get; }

        /// <summary>[good index][season index] (Spring, Summer, Monsoon, Winter).</summary>
        private readonly Fixed[][] _seasonalFactor;

        /// <summary>The trader's daily buying capacity per good, in quantity units (grams); 0 for inputs.</summary>
        private readonly long[] _traderBuyPerDay;

        public RegionDef(string roadTier, Fixed transportCostPerKg, Fixed[][] seasonalFactor, long[] traderBuyPerDay)
        {
            RoadTier = roadTier;
            TransportCostPerKg = transportCostPerKg;
            _seasonalFactor = seasonalFactor;
            _traderBuyPerDay = traderBuyPerDay;
        }

        public Fixed SeasonalFactor(int good, int season) { return _seasonalFactor[good][season]; }

        public long TraderBuyPerDay(int good) { return _traderBuyPerDay[good]; }
    }

    /// <summary><c>EconomyDef</c> from <c>content/market.json</c> (spec 004 §4).</summary>
    public sealed class EconomyDef
    {
        public Fixed StartIndex { get; }

        public EconomyDef(Fixed startIndex) { StartIndex = startIndex; }
    }

    /// <summary>Goods, the region and the economy (spec 004). Goods are indexed in file order.</summary>
    public sealed class MarketDefs
    {
        public IReadOnlyList<GoodDef> Goods { get; }
        public RegionDef Region { get; }
        public EconomyDef Economy { get; }

        public MarketDefs(IReadOnlyList<GoodDef> goods, RegionDef region, EconomyDef economy)
        {
            Goods = new List<GoodDef>(goods).AsReadOnly();
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        /// <summary>The good's index, or −1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < Goods.Count; i++)
                if (Goods[i].Id == id) return i;
            return -1;
        }
    }
}
