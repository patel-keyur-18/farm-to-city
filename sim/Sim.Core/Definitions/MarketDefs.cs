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

    /// <summary>Where a good is kept (spec 012 §3.3): the hut store, or a place of its own that counts against its own room.</summary>
    public enum GoodPlace
    {
        Store = 0,
        FeedRoom = 1,
        StrawStack = 2,
        CompostPit = 3,
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

        /// <summary>The daily factor's random step and its band around 1 (produce; spec 011 §3.1b). Zero: no daily move.</summary>
        public Fixed DailyVolatility { get; }
        public Fixed DailyBand { get; }

        /// <summary>Where it's kept (spec 012 §3.3); the hut store unless a place is given.</summary>
        public GoodPlace Place { get; }

        public GoodDef(string id, string nameKey, GoodKind kind, GoodUnit unit, Fixed basePrice, Fixed elasticity,
            Fixed spoilagePerDay, SalesKind sales, bool edible, Fixed weightKg, bool priceControlled,
            Fixed dailyVolatility = default, Fixed dailyBand = default, GoodPlace place = GoodPlace.Store)
        {
            Place = place;
            DailyVolatility = dailyVolatility;
            DailyBand = dailyBand;
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

        /// <summary>The share of the daily factor's gap to 1 closed each morning (spec 011 §3.1b).</summary>
        public Fixed DailyReversion { get; }

        public MarketDef(Fixed adjustmentRate, Fixed railMin, Fixed railMax, int offerSpreadHours, int unmetWindowHours, long minLotGrams,
            Fixed dailyReversion = default)
        {
            DailyReversion = dailyReversion;
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

    /// <summary><c>TruckDef</c> from <c>content/market.json</c> (spec 011 §3.1, ADR 0054): the trader's truck.</summary>
    public sealed class TruckDef
    {
        public int ArriveHour { get; }
        public int LeaveHour { get; }

        /// <summary>The total load a day, in grams.</summary>
        public long LoadGrams { get; }

        public TruckDef(int arriveHour, int leaveHour, long loadGrams)
        {
            ArriveHour = arriveHour;
            LeaveHour = leaveHour;
            LoadGrams = loadGrams;
        }

        /// <summary>Whether the truck is at the farm at hour of day h: from arriveHour up to leaveHour.</summary>
        public bool HereAt(int hour) { return (hour - ArriveHour + 24) % 24 < (LeaveHour - ArriveHour + 24) % 24; }

        /// <summary>Whether the hourly pass at hour h clears a closed hour: the passes after leaveHour up to arriveHour (spec 011 §3.2).</summary>
        public bool StallPausedAtPass(int hour)
        {
            int d = (hour - LeaveHour + 24) % 24;
            return d >= 1 && d <= (ArriveHour - LeaveHour + 24) % 24;
        }
    }

    /// <summary><c>OrderDef</c> from <c>content/market.json</c> (spec 011 §3.3, ADR 0055): the city supermarket's daily orders.</summary>
    public sealed class OrderDef
    {
        public int PerDay { get; }
        public Fixed SowingsMin { get; }
        public Fixed SowingsMax { get; }
        public long LotGrams { get; }
        public Fixed PremiumMin { get; }
        public Fixed PremiumMax { get; }
        public int GreensPerDay { get; }
        public Fixed GreenPickingsMin { get; }
        public Fixed GreenPickingsMax { get; }
        public long GreensLotGrams { get; }

        public OrderDef(int perDay, Fixed sowingsMin, Fixed sowingsMax, long lotGrams, Fixed premiumMin, Fixed premiumMax,
            int greensPerDay, Fixed greenPickingsMin, Fixed greenPickingsMax, long greensLotGrams)
        {
            PerDay = perDay;
            SowingsMin = sowingsMin;
            SowingsMax = sowingsMax;
            LotGrams = lotGrams;
            PremiumMin = premiumMin;
            PremiumMax = premiumMax;
            GreensPerDay = greensPerDay;
            GreenPickingsMin = greenPickingsMin;
            GreenPickingsMax = greenPickingsMax;
            GreensLotGrams = greensLotGrams;
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

        /// <summary>The trader's truck (spec 011); null in Phase 1 content, where the trader is always there.</summary>
        public TruckDef Truck { get; }

        /// <summary>Supermarket orders (spec 011 §3.3); null without them. Needs the truck (orders post at its arrival).</summary>
        public OrderDef Orders { get; }

        public MarketDefs(IReadOnlyList<GoodDef> goods, RegionDef region, EconomyDef economy, MarketDef market, MandiDef mandi,
            TruckDef truck = null, OrderDef orders = null)
        {
            Truck = truck;
            Orders = orders;
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
