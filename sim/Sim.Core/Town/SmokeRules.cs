using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// Smoke (spec 025 §3.3, §3.4): a building that smokes takes land value from the acres within its reach and health from the homes there. One rule
    /// for every source: the brick kiln's (US-243) and, with the utilities, the generator's. An acre never loses more than the cap in all.
    /// </summary>
    public static class SmokeRules
    {
        public static SmokeDef Def(Simulation sim) { return sim.Config.Game?.Township?.Smoke; }

        /// <summary>One smoking building: its tile and what it does.</summary>
        public readonly struct Source
        {
            public int Tile { get; }
            public SmokeSource Def { get; }
            public Source(int tile, SmokeSource def) { Tile = tile; Def = def; }
        }

        /// <summary>The smoking buildings now, in a fixed order: your open kilns by id.</summary>
        public static List<Source> Sources(Simulation sim)
        {
            var list = new List<Source>();
            SmokeDef d = Def(sim);
            if (d == null) return list;
            SmokeSource kiln = d.Source("kiln");
            if (kiln != null) foreach (Business.Business b in KilnRules.Open(sim)) list.Add(new Source(b.TileId, kiln));
            return list;
        }

        /// <summary>The sources whose reach takes in a tile.</summary>
        public static List<Source> Reaching(Simulation sim, int tileId)
        {
            var list = new List<Source>();
            foreach (Source s in Sources(sim)) if (CoverageRules.Within(sim, tileId, s.Tile, s.Def.Reach)) list.Add(s);
            return list;
        }

        /// <summary>The acres a source on <paramref name="tileId"/> would reach (itself included), in tile order: the placement ghost's footprint.</summary>
        public static List<int> Footprint(Simulation sim, int tileId, SmokeSource source)
        {
            var list = new List<int>();
            foreach (Tile t in sim.State.Land.Tiles)
                if (t.Terrain != Terrain.River && CoverageRules.Within(sim, t.Id, tileId, source.Reach)) list.Add(t.Id);
            return list;
        }

        /// <summary>Of a footprint: the acres that are yours, and the homes (houses standing, yours and the builders') in it, for the confirm card.</summary>
        public static void Touches(Simulation sim, List<int> footprint, out int yours, out int homes)
        {
            yours = 0;
            homes = 0;
            foreach (int t in footprint)
            {
                if (sim.State.Land.Parcels[sim.State.Land.Tiles[t].ParcelId].Owned) yours++;
                foreach (Building b in sim.State.Land.Buildings)
                    if (b.TileId == t && HouseRules.IsHouse(sim, b)) homes++;
            }
        }

        /// <summary>The land value smoke takes from an acre (positive), held to the cap, and how many sources reach it.</summary>
        public static Fixed LandValue(Simulation sim, int tileId, out int count)
        {
            Fixed total = Fixed.Zero;
            List<Source> hit = Reaching(sim, tileId);
            count = hit.Count;
            foreach (Source s in hit) total += s.Def.LandValue;
            Fixed cap = Def(sim)?.LandValueCap ?? Fixed.Zero;
            return total > cap ? cap : total;
        }

        /// <summary>The health smoke takes from the people of a home each season, as a share.</summary>
        public static Fixed Health(Simulation sim, Household x)
        {
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            if (home == null || home.TileId < 0) return Fixed.Zero;
            Fixed total = Fixed.Zero;
            foreach (Source s in Reaching(sim, home.TileId)) total += s.Def.Health;
            return total;
        }
    }
}
