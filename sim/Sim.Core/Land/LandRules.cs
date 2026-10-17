using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Land
{
    /// <summary>Spec 002's pure formulas: the Soil factor (§3.4) and parcel prices (§3.5).</summary>
    public static class LandRules
    {
        private static readonly Fixed Hundred = Fixed.FromInt(100);

        /// <summary>min + (max − min) × f ÷ 100.</summary>
        public static Fixed FertilityPriceFactor(LandDef land, Fixed fertility)
        {
            return land.FertilityPriceMin + (land.FertilityPriceMax - land.FertilityPriceMin) * fertility / Hundred;
        }

        /// <summary>
        /// round_coin(acreBasePrice × soil × drainage × fertility × location): the factors multiplied as Fixed,
        /// applied to the money once, then rounded to a whole coin.
        /// </summary>
        public static long TilePrice(GameDefs defs, Tile tile)
        {
            Fixed factor = defs.Soils[tile.Soil].PriceFactor * defs.Drainages[tile.Drainage].PriceFactor
                * FertilityPriceFactor(defs.Land, tile.Fertility) * defs.Map.Parcels[tile.ParcelId].LocationFactor;
            return Money.RoundToCoin(Money.MulRate(defs.Land.AcreBasePrice, factor));
        }

        /// <summary>The sum of the parcel's tile prices; also its market value for net worth.</summary>
        public static long ParcelPrice(Simulation sim, Parcel parcel)
        {
            GameDefs defs = sim.Config.Game;
            long sum = 0;
            foreach (int tileId in defs.Map.Parcels[parcel.Id].Tiles) sum += TilePrice(defs, sim.State.Land.Tiles[tileId]);
            return sum;
        }
    }
}
