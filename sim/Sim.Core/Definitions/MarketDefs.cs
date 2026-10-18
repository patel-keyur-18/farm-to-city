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

    /// <summary>The region's prices, the valley's market and the trader from <c>content/region.json</c> (spec 004 §4).</summary>
    public sealed class RegionDef
    {
        public string RoadTier { get; }

        /// <summary>Coins per kg on the current road tier.</summary>
        public Fixed TransportCostPerKg { get; }

        /// <summary>[good index][season index] (Spring, Summer, Monsoon, Winter).</summary>
        private readonly Fixed[][] _seasonalFactor;

        /// <summary>The trader's daily buying capacity per good, in quantity units (grams); 0 for inputs.</summary>
        private readonly long[] _traderBuyPerDay;

        /// <summary>Per good, in quantity units a day (grams); 0 for inputs.</summary>
        private readonly long[] _demandPerDay, _valleySupplyPerDay, _importCapacityPerDay;

        public RegionDef(string roadTier, Fixed transportCostPerKg, Fixed[][] seasonalFactor, long[] traderBuyPerDay,
            long[] demandPerDay, long[] valleySupplyPerDay, long[] importCapacityPerDay)
        {
            RoadTier = roadTier;
            TransportCostPerKg = transportCostPerKg;
            _seasonalFactor = seasonalFactor;
            _traderBuyPerDay = traderBuyPerDay;
            _demandPerDay = demandPerDay;
            _valleySupplyPerDay = valleySupplyPerDay;
            _importCapacityPerDay = importCapacityPerDay;
        }

        public Fixed SeasonalFactor(int good, int season) { return _seasonalFactor[good][season]; }

        public long TraderBuyPerDay(int good) { return _traderBuyPerDay[good]; }

        /// <summary>The valley households' demand at P_regional (D_ref), grams a day.</summary>
        public long DemandPerDay(int good) { return _demandPerDay[good]; }

        /// <summary>The neighbours' produce on the market, grams a day.</summary>
        public long ValleySupplyPerDay(int good) { return _valleySupplyPerDay[good]; }

        /// <summary>The trader's imports at the ceiling, grams a day (&gt; 0 for every produce good, ADR 0013).</summary>
        public long ImportCapacityPerDay(int good) { return _importCapacityPerDay[good]; }
    }

    /// <summary><c>EconomyDef</c> from <c>content/market.json</c> (spec 004 §4): the price index.</summary>
    public sealed class EconomyDef
    {
        public Fixed StartIndex { get; }
        public Fixed InflationMinPerYear { get; }
        public Fixed InflationMaxPerYear { get; }

        public EconomyDef(Fixed startIndex, Fixed inflationMinPerYear, Fixed inflationMaxPerYear)
        {
            StartIndex = startIndex;
            InflationMinPerYear = inflationMinPerYear;
            InflationMaxPerYear = inflationMaxPerYear;
        }
    }

    /// <summary><c>MarketDef</c> from <c>content/market.json</c> (spec 004 §4): the hourly local market.</summary>
    public sealed class MarketDef
    {
        /// <summary>k: the share of the gap to the target the price closes each hour (ADR 0013).</summary>
        public Fixed AdjustmentRate { get; }
        public Fixed RailMin { get; }
        public Fixed RailMax { get; }
        public int OfferSpreadHours { get; }
        public int UnmetWindowHours { get; }

        /// <summary>The least an offer puts on the stall each hour, in grams (ADR 0034).</summary>
        public long MinLotGrams { get; }

        public MarketDef(Fixed adjustmentRate, Fixed railMin, Fixed railMax, int offerSpreadHours, int unmetWindowHours, long minLotGrams)
        {
            AdjustmentRate = adjustmentRate;
            RailMin = railMin;
            RailMax = railMax;
            OfferSpreadHours = offerSpreadHours;
            UnmetWindowHours = unmetWindowHours;
            MinLotGrams = minLotGrams;
        }
    }

    /// <summary><c>MandiDef</c> from <c>content/market.json</c> (spec 004 §3.6, ADR 0031, 0034): the trip to the town mandi.</summary>
    public sealed class MandiDef
    {
        public int TripHours { get; }
        public long CartCapacityGrams { get; }

        /// <summary>A year-0 price in 1/100 coin (indexed when charged, ADR 0034).</summary>
        public long CartHirePerTrip { get; }
        public Fixed CommissionRate { get; }

        /// <summary>The founder's delivered labour the trip takes, in hours (ADR 0034).</summary>
        public Fixed FounderHours { get; }

        public MandiDef(int tripHours, long cartCapacityGrams, long cartHirePerTrip, Fixed commissionRate, Fixed founderHours)
        {
            TripHours = tripHours;
            CartCapacityGrams = cartCapacityGrams;
            CartHirePerTrip = cartHirePerTrip;
            CommissionRate = commissionRate;
            FounderHours = founderHours;
        }
    }

    /// <summary>Goods, the region and the economy (spec 004). Goods are indexed in file order.</summary>
    public sealed class MarketDefs
    {
        public IReadOnlyList<GoodDef> Goods { get; }
        public RegionDef Region { get; }
        public EconomyDef Economy { get; }
        public MarketDef Market { get; }
        public MandiDef Mandi { get; }

        public MarketDefs(IReadOnlyList<GoodDef> goods, RegionDef region, EconomyDef economy, MarketDef market, MandiDef mandi)
        {
            Goods = new List<GoodDef>(goods).AsReadOnly();
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            Market = market ?? throw new ArgumentNullException(nameof(market));
            Mandi = mandi ?? throw new ArgumentNullException(nameof(mandi));
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
