using System.Collections.Generic;

namespace Sim.View.Views
{
    /// <summary>What a tile of the land past the map's edge shows (spec 010 §3.6). Presentation only: it has no sim state.</summary>
    public enum EdgeKind
    {
        River = 0,
        FarBank = 1,
        Hill = 2,
        Field = 3,
        Scrub = 4,
        Road = 5,

        /// <summary>A neighbour's farmstead on the near sides' scrub (ADR 0062 §2).</summary>
        Farmstead = 6,
    }

    public readonly struct EdgeTile
    {
        public int X { get; }
        public int Y { get; }
        public EdgeKind Kind { get; }

        public EdgeTile(int x, int y, EdgeKind kind)
        {
            X = x;
            Y = y;
            Kind = kind;
        }
    }

    /// <summary>
    /// The valley framing the map (spec 010 §3.6). The camera's background is the meadow, tinted like the land, so nothing past the
    /// map is ever sky-blue. Over it, a ring of flat edge tiles hides the map's block sides and carries the detail: the river past
    /// the north edge (row 0), running the whole width the camera can reach; hills behind, past the west edge (column 0); scrub and
    /// neighbours' fields on the near sides; the district road running on off both ends.
    /// </summary>
    public static class EdgeView
    {
        /// <summary>Rings of detailed edge tiles round the map.</summary>
        public const int Ring = 3;

        /// <summary>Acres of river past the north edge.</summary>
        public const int RiverWidth = 3;

        /// <summary>How far the river and the road run on past the map's ends, in tiles: past anything the camera's clamp can show.</summary>
        public const int Reach = 22;

        public static EdgeKind Kind(int x, int y, int width, int height)
        {
            if (y < 0) return y >= -RiverWidth ? EdgeKind.River : Pattern(x, y) ? EdgeKind.FarBank : EdgeKind.Field;
            if (y == height - 1) return EdgeKind.Road; // the district road, on past the map's ends
            if (x < 0) return EdgeKind.Hill;
            if (Pattern(x, y)) return EdgeKind.Field;
            return Peak(x, y) ? EdgeKind.Farmstead : EdgeKind.Scrub;
        }

        /// <summary>A scattered few of the near side's scrub tiles: draw over 60 and above the four tiles round it.</summary>
        private static bool Peak(int x, int y)
        {
            int h = MapView.Draw((x + 64) * 211 + (y + 64));
            if (h < 60) return false;
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (MapView.Draw((x + dx + 64) * 211 + (y + dy + 64)) >= h) return false;
            return true;
        }

        private static bool Pattern(int x, int y) { return (((x >> 1) + (y >> 1)) % 3 + 3) % 3 != 0; }

        /// <summary>Every edge tile drawn: the ring round the map, then the river and the road on to <see cref="Reach"/>.</summary>
        public static List<EdgeTile> Tiles(int width, int height)
        {
            var list = new List<EdgeTile>();
            for (int y = -Ring - RiverWidth; y < height + Ring; y++)
                for (int x = -Ring; x < width + Ring; x++)
                {
                    if (x >= 0 && x < width && y >= 0 && y < height) continue;
                    if (y < -RiverWidth && (x < 0 || x >= width)) continue; // the far bank only across from the map
                    list.Add(new EdgeTile(x, y, Kind(x, y, width, height)));
                }
            for (int x = -Reach; x < width + Reach; x++)
            {
                if (x >= -Ring && x < width + Ring) continue;
                for (int y = -RiverWidth; y < 0; y++) list.Add(new EdgeTile(x, y, EdgeKind.River));
                list.Add(new EdgeTile(x, height - 1, EdgeKind.Road));
            }
            return list;
        }

        /// <summary>The season's crop on an edge field (the same rule as neighbours' land, by a stable pseudo-parcel).</summary>
        public static string Crop(int x, int y, int season)
        {
            return MapView.NeighbourCrop("loam", ((x >> 1) * 31 + (y >> 1) * 17) & 0xFFFF, season);
        }
    }
}
