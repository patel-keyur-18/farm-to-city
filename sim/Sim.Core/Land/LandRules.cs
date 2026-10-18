using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Land
{
    /// <summary>Spec 002's pure formulas: the Soil factor (§3.4) and parcel prices (§3.5).</summary>
    public static class LandRules
    {
        private static readonly Fixed Hundred = Fixed.FromInt(100);

        /// <summary>SoilDef.yieldFactor × FertilityFactor(fertility) × ShadeFactor (spec 002 §3.4), used by the yield formula.</summary>
        public static Fixed SoilFactor(Simulation sim, Tile tile)
        {
            GameDefs defs = sim.Config.Game;
            return defs.Soils[tile.Soil].YieldFactor * FertilityFactor(defs.Land, tile.Fertility) * ShadeFactor(sim, tile);
        }

        /// <summary>max(minShadeFactor, 1 − Σ shade of the mature trees in the tile's field-edge slots) (ADR 0037: shade per tree).</summary>
        public static Fixed ShadeFactor(Simulation sim, Tile tile)
        {
            Fixed f = Fixed.One;
            foreach (TreeSlot slot in sim.State.Land.Slots)
                if (slot.TileId == tile.Id && slot.Kind == SlotKind.FieldEdge && slot.TreeId != 0 && slot.TreeMature) f -= slot.Shade;
            LandDef land = sim.Config.Game.Land;
            return f < land.MinShadeFactor ? land.MinShadeFactor : f;
        }

        /// <summary>Piecewise linear through LandDef.fertilityFactorCurve: y0 + (y1 − y0) × (f − x0) ÷ (x1 − x0).</summary>
        public static Fixed FertilityFactor(LandDef land, Fixed fertility)
        {
            var curve = land.FertilityFactorCurve;
            int i = 1;
            while (i < curve.Count - 1 && fertility > curve[i].X) i++;
            CurvePoint a = curve[i - 1], b = curve[i];
            return a.Y + (b.Y - a.Y) * (fertility - a.X) / (b.X - a.X);
        }

        /// <summary>min + (max − min) × f ÷ 100.</summary>
        public static Fixed FertilityPriceFactor(LandDef land, Fixed fertility)
        {
            return land.FertilityPriceMin + (land.FertilityPriceMax - land.FertilityPriceMin) * fertility / Hundred;
        }

        /// <summary>
        /// round_coin(Indexed(acreBasePrice) × soil × drainage × fertility × location): the factors multiplied as Fixed,
        /// applied to the money once, then rounded to a whole coin (spec 002 §3.5, ADR 0034).
        /// </summary>
        public static long TilePrice(Simulation sim, Tile tile)
        {
            GameDefs defs = sim.Config.Game;
            Fixed factor = defs.Soils[tile.Soil].PriceFactor * defs.Drainages[tile.Drainage].PriceFactor
                * FertilityPriceFactor(defs.Land, tile.Fertility) * defs.Map.Parcels[tile.ParcelId].LocationFactor;
            return Money.RoundToCoin(Money.MulRate(Market.MarketRules.Indexed(sim, defs.Land.AcreBasePrice), factor));
        }

        /// <summary>The sum of the parcel's tile prices; also its market value for net worth.</summary>
        public static long ParcelPrice(Simulation sim, Parcel parcel)
        {
            GameDefs defs = sim.Config.Game;
            long sum = 0;
            foreach (int tileId in defs.Map.Parcels[parcel.Id].Tiles) sum += TilePrice(sim, sim.State.Land.Tiles[tileId]);
            return sum;
        }
    }
}
