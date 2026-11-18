namespace Sim.Core.Town
{
    /// <summary>
    /// One rule for every civic building, the school included (spec 023 §3.6): a home is covered if its distance to the building (metres between
    /// two tiles, an acre is 100 m, Manhattan, as the shops') is within the building's reach. Out of reach a home gets nothing. Capacity scales
    /// the benefit, <c>min(1, capacity ÷ people served)</c>, and a building with no staff covers nobody.
    /// </summary>
    public static class CoverageRules
    {
        /// <summary>Metres between two tiles: Manhattan distance in acres × 100.</summary>
        public static int Metres(Simulation sim, int tileA, int tileB)
        {
            int w = sim.Config.Game.Map.Width;
            int dx = System.Math.Abs(tileA % w - tileB % w), dy = System.Math.Abs(tileA / w - tileB / w);
            return (dx + dy) * 100;
        }

        /// <summary>A tile is within <paramref name="reach"/> metres of a building's tile.</summary>
        public static bool Within(Simulation sim, int homeTile, int buildingTile, int reach) { return Metres(sim, homeTile, buildingTile) <= reach; }
    }
}
