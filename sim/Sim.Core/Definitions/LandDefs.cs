using System;
using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    public enum Terrain
    {
        Farm = 0,
        Yard = 1,
    }

    /// <summary>A soil type from <c>content/land.json</c> (spec 002 §4).</summary>
    public sealed class SoilDef
    {
        public string Id { get; }
        public string NameKey { get; }
        public Fixed YieldFactor { get; }
        public Fixed GainFactor { get; }
        public Fixed LossFactor { get; }
        public Fixed PriceFactor { get; }
        public Fixed FallowCap { get; }

        public SoilDef(string id, string nameKey, Fixed yieldFactor, Fixed gainFactor, Fixed lossFactor, Fixed priceFactor, Fixed fallowCap)
        {
            Id = id;
            NameKey = nameKey;
            YieldFactor = yieldFactor;
            GainFactor = gainFactor;
            LossFactor = lossFactor;
            PriceFactor = priceFactor;
            FallowCap = fallowCap;
        }
    }

    /// <summary>A drainage class from <c>content/land.json</c> (spec 002 §4).</summary>
    public sealed class DrainageDef
    {
        public string Id { get; }
        public Fixed DrainAbove { get; }
        public Fixed DrainRatePerHour { get; }
        public Fixed PriceFactor { get; }

        public DrainageDef(string id, Fixed drainAbove, Fixed drainRatePerHour, Fixed priceFactor)
        {
            Id = id;
            DrainAbove = drainAbove;
            DrainRatePerHour = drainRatePerHour;
            PriceFactor = priceFactor;
        }
    }

    /// <summary>One point of a piecewise-linear curve.</summary>
    public readonly struct CurvePoint
    {
        public Fixed X { get; }
        public Fixed Y { get; }

        public CurvePoint(Fixed x, Fixed y)
        {
            X = x;
            Y = y;
        }
    }

    /// <summary>
    /// Land parameters from <c>content/land.json</c> (spec 002 §4): <c>LandDef</c> plus <c>WellDef.moistureFloor</c>.
    /// </summary>
    public sealed class LandDef
    {
        /// <summary>Moisture points per hour, indexed like <see cref="WeatherSet.Types"/>.</summary>
        public IReadOnlyList<Fixed> MoistureByWeather { get; }
        public IReadOnlyList<CurvePoint> FertilityFactorCurve { get; }
        public Fixed FallowRecoveryPerSeason { get; }

        /// <summary>Money (1/100 coin).</summary>
        public long AcreBasePrice { get; }
        public Fixed FertilityPriceMin { get; }
        public Fixed FertilityPriceMax { get; }
        public Fixed QuickSaleFactor { get; }
        public int FieldEdgeSlotsPerAcre { get; }
        public Fixed ShadePerMatureTree { get; }
        public Fixed MinShadeFactor { get; }
        public Fixed WellMoistureFloor { get; }

        /// <summary>The well acres (<c>WellDef.irrigatedTiles</c>, ADR 0032): the only tiles the well's floor applies to.</summary>
        public IReadOnlyList<int> WellTiles { get; }

        public LandDef(IReadOnlyList<Fixed> moistureByWeather, IReadOnlyList<CurvePoint> fertilityFactorCurve,
            Fixed fallowRecoveryPerSeason, long acreBasePrice, Fixed fertilityPriceMin, Fixed fertilityPriceMax,
            Fixed quickSaleFactor, int fieldEdgeSlotsPerAcre, Fixed shadePerMatureTree, Fixed minShadeFactor,
            Fixed wellMoistureFloor, IReadOnlyList<int> wellTiles)
        {
            WellTiles = new List<int>(wellTiles).AsReadOnly();
            MoistureByWeather = new List<Fixed>(moistureByWeather).AsReadOnly();
            FertilityFactorCurve = new List<CurvePoint>(fertilityFactorCurve).AsReadOnly();
            FallowRecoveryPerSeason = fallowRecoveryPerSeason;
            AcreBasePrice = acreBasePrice;
            FertilityPriceMin = fertilityPriceMin;
            FertilityPriceMax = fertilityPriceMax;
            QuickSaleFactor = quickSaleFactor;
            FieldEdgeSlotsPerAcre = fieldEdgeSlotsPerAcre;
            ShadePerMatureTree = shadePerMatureTree;
            MinShadeFactor = minShadeFactor;
            WellMoistureFloor = wellMoistureFloor;
        }
    }

    /// <summary>A tile's starting values in <c>content/map.json</c>. Soil and drainage are indices into <see cref="GameDefs"/>.</summary>
    public sealed class TileDef
    {
        public Terrain Terrain { get; }
        public int Soil { get; }
        public int Drainage { get; }
        public Fixed Fertility { get; }
        public Fixed Moisture { get; }

        public TileDef(Terrain terrain, int soil, int drainage, Fixed fertility, Fixed moisture)
        {
            Terrain = terrain;
            Soil = soil;
            Drainage = drainage;
            Fertility = fertility;
            Moisture = moisture;
        }
    }

    public sealed class ParcelDef
    {
        public string Id { get; }
        public IReadOnlyList<int> Tiles { get; }
        public Fixed LocationFactor { get; }

        public ParcelDef(string id, IReadOnlyList<int> tiles, Fixed locationFactor)
        {
            Id = id;
            Tiles = new List<int>(tiles).AsReadOnly();
            LocationFactor = locationFactor;
        }
    }

    /// <summary>The hand-authored homestead map (spec 002 §3.1). Tile id = y × width + x.</summary>
    public sealed class MapDef
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<TileDef> Tiles { get; }

        /// <summary>A parcel's index in this list is its id in the state.</summary>
        public IReadOnlyList<ParcelDef> Parcels { get; }

        public MapDef(int width, int height, IReadOnlyList<TileDef> tiles, IReadOnlyList<ParcelDef> parcels)
        {
            Width = width;
            Height = height;
            Tiles = new List<TileDef>(tiles).AsReadOnly();
            Parcels = new List<ParcelDef>(parcels).AsReadOnly();
        }

        public int IndexOfParcel(string id)
        {
            for (int i = 0; i < Parcels.Count; i++)
                if (Parcels[i].Id == id) return i;
            return -1;
        }
    }

    /// <summary>From <c>content/buildings.json</c> (spec 002 §4; later specs add fields).</summary>
    public sealed class BuildingDef
    {
        /// <summary>Every start needs the well: it keeps the inherited acres above the moisture floor (spec 002 §3.2).</summary>
        public const string WellId = "well";

        public string Id { get; }
        public string NameKey { get; }
        public int YardTreeSlots { get; }

        /// <summary>Opening book value, money (1/100 coin).</summary>
        public long Value { get; }
        public int UsefulLifeSeasons { get; }

        /// <summary>The store's capacity in kg (spec 004 §3.5; hut 2,000). The founder's store is the sum over their buildings.</summary>
        public long StoreCapacityKg { get; }

        /// <summary>How much of a rest day's fatigue recovery the home gives (spec 003 §3.11; hut 0.80).</summary>
        public Fixed RestRecovery { get; }

        /// <summary>Kitchen-garden beds by the home (spec 003 §3.13; hut 2).</summary>
        public int GardenBeds { get; }

        public BuildingDef(string id, string nameKey, int yardTreeSlots, long value, int usefulLifeSeasons, long storeCapacityKg,
            Fixed restRecovery, int gardenBeds)
        {
            RestRecovery = restRecovery;
            GardenBeds = gardenBeds;
            StoreCapacityKg = storeCapacityKg;
            Id = id;
            NameKey = nameKey;
            YardTreeSlots = yardTreeSlots;
            Value = value;
            UsefulLifeSeasons = usefulLifeSeasons;
        }
    }

    /// <summary>A building placed at new game: an index into <see cref="GameDefs.Buildings"/> and a tile id.</summary>
    public readonly struct StartBuilding
    {
        public int Building { get; }
        public int Tile { get; }

        public StartBuilding(int building, int tile)
        {
            Building = building;
            Tile = tile;
        }
    }

    /// <summary>A good in the inherited store: an index into <see cref="MarketDefs.Goods"/> and a quantity in its units (grams, ml, each).</summary>
    public readonly struct StartStock
    {
        public int Good { get; }
        public long Quantity { get; }

        public StartStock(int good, long quantity)
        {
            Good = good;
            Quantity = quantity;
        }
    }

    /// <summary>The inherited start from <c>content/start.json</c> (spec 002 §4, spec 004 §4, spec 005 §4).</summary>
    public sealed class StartDef
    {
        /// <summary>Indices into <see cref="MapDef.Parcels"/>: the protected inheritance (ADR 0030).</summary>
        public IReadOnlyList<int> InheritedParcels { get; }
        public IReadOnlyList<StartBuilding> Buildings { get; }

        /// <summary>Money (1/100 coin).</summary>
        public long StartingCash { get; }

        /// <summary>The inherited store (ADR 0030), in good order of the file.</summary>
        public IReadOnlyList<StartStock> Store { get; }

        /// <summary>Saved-seed age per crop index in seasons of growth, or null for no age yet (ADR 0032).</summary>
        public IReadOnlyList<Fixed?> SavedSeedAge { get; }

        public StartDef(IReadOnlyList<int> inheritedParcels, IReadOnlyList<StartBuilding> buildings, long startingCash,
            IReadOnlyList<StartStock> store, IReadOnlyList<Fixed?> savedSeedAge)
        {
            SavedSeedAge = new List<Fixed?>(savedSeedAge).AsReadOnly();
            Store = new List<StartStock>(store).AsReadOnly();
            InheritedParcels = new List<int>(inheritedParcels).AsReadOnly();
            Buildings = new List<StartBuilding>(buildings).AsReadOnly();
            StartingCash = startingCash;
        }
    }

    /// <summary>From <c>content/finance.json</c> (spec 005 §4).</summary>
    public sealed class LedgerDef
    {
        public int RetainSeasons { get; }

        public LedgerDef(int retainSeasons) { RetainSeasons = retainSeasons; }
    }

    /// <summary>
    /// The game content beyond the clock and weather: land, map, buildings, the start and the ledger. A
    /// simulation without it runs time and weather only (the spec 001 tests and golden replay).
    /// </summary>
    public sealed class GameDefs
    {
        public IReadOnlyList<SoilDef> Soils { get; }
        public IReadOnlyList<DrainageDef> Drainages { get; }
        public LandDef Land { get; }
        public MapDef Map { get; }
        public IReadOnlyList<BuildingDef> Buildings { get; }
        public StartDef Start { get; }
        public LedgerDef Ledger { get; }

        /// <summary>Goods, the region and the economy (spec 004).</summary>
        public MarketDefs Market { get; }

        /// <summary>Crops, the garden, trees and the founder (spec 003).</summary>
        public FarmDefs Farm { get; }

        public GameDefs(IReadOnlyList<SoilDef> soils, IReadOnlyList<DrainageDef> drainages, LandDef land, MapDef map,
            IReadOnlyList<BuildingDef> buildings, StartDef start, LedgerDef ledger, MarketDefs market, FarmDefs farm)
        {
            Farm = farm ?? throw new ArgumentNullException(nameof(farm));
            Market = market ?? throw new ArgumentNullException(nameof(market));
            Soils = new List<SoilDef>(soils).AsReadOnly();
            Drainages = new List<DrainageDef>(drainages).AsReadOnly();
            Land = land ?? throw new ArgumentNullException(nameof(land));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Buildings = new List<BuildingDef>(buildings).AsReadOnly();
            Start = start ?? throw new ArgumentNullException(nameof(start));
            Ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        }
    }
}
