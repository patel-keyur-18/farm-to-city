using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// A living valley (ADR 0062, US-207): each neighbour's acre drawn by its look (a field, ploughed or fallow land, scrub with a
    /// babul, a mango grove, a farmstead), tube-well rooms and haystacks, worn bunds between neighbours' fields; and round
    /// everything you own a hedge on a low mud wall with a gate onto each road, whitewashed corner stones, and fresh bunds between
    /// your fields. Offsets are render.py's world units from the acre's centre (+x lower right, +y upper right on screen).
    /// </summary>
    public sealed partial class MapPresenter
    {
        private readonly struct Piece
        {
            public readonly string Sprite;
            public readonly Vector2 At;
            public readonly float Scale;
            public Piece(string sprite, float x, float y, float scale = 1f) { Sprite = sprite; At = new Vector2(x, y); Scale = scale; }
        }

        /// <summary>
        /// Four farmsteads, each a house at the back of its acre with its yard in front: a mud house with a thatched cattle shelter and a
        /// cow; a lime-washed one with a buffalo; a mud house turned to the lane with two haystacks; a bigger farmer's two-storey house.
        /// </summary>
        private static readonly Piece[][] Farmsteads =
        {
            new[] { new Piece("village_house_0_s", -0.1f, 0.18f), new Piece("chhappar", 0.24f, 0.2f), new Piece("village_cow", 0.18f, -0.04f),
                    new Piece("village_haystack", -0.3f, -0.2f), new Piece("village_charpai", -0.04f, -0.14f), new Piece("village_tree", -0.34f, 0.36f) },
            new[] { new Piece("village_house_1_e", -0.18f, 0.05f), new Piece("chhappar", 0.1f, 0.28f), new Piece("village_buffalo", 0.2f, 0.0f),
                    new Piece("village_haystack", -0.26f, -0.3f), new Piece("village_tree", 0.34f, 0.34f) },
            new[] { new Piece("village_house_0_e", -0.2f, 0.12f), new Piece("village_haystack", 0.2f, 0.26f), new Piece("village_haystack", 0.32f, 0.08f),
                    new Piece("village_buffalo", 0.06f, -0.2f), new Piece("village_charpai", -0.24f, -0.24f), new Piece("tree_neem", 0.3f, -0.3f, 0.8f) },
            new[] { new Piece("village_house_2_s", -0.06f, 0.2f), new Piece("chhappar", -0.3f, -0.06f), new Piece("village_cow", -0.24f, -0.3f),
                    new Piece("village_haystack", 0.3f, 0.12f), new Piece("village_tree", 0.3f, -0.28f) },
        };

        private static readonly Piece[] Grove =
        {
            new Piece("tree_mango_mature", -0.22f, 0.22f, 0.55f), new Piece("tree_mango_mature", 0.22f, 0.2f, 0.55f), new Piece("tree_mango_mature", 0f, 0f, 0.55f),
            new Piece("tree_mango_mature", -0.2f, -0.22f, 0.55f), new Piece("tree_mango_mature", 0.22f, -0.2f, 0.55f),
        };

        private readonly Dictionary<int, List<SpriteRenderer>> _valley = new Dictionary<int, List<SpriteRenderer>>();
        private readonly Dictionary<int, string> _valleyKey = new Dictionary<int, string>();
        private readonly List<SpriteRenderer> _edgeFarmsteads = new List<SpriteRenderer>();

        /// <summary>A neighbour's acre's ground by its look.</summary>
        private void NeighbourGround(TileVm tv, SpriteRenderer ground, SpriteRenderer detail)
        {
            detail.sprite = null;
            ground.color = _tint;
            switch (tv.Neighbour)
            {
                case NeighbourLand.Field:
                case NeighbourLand.Ploughed:
                    ground.sprite = SpriteBank.SoilTile;
                    ground.color = _tint * SoilColour(tv);
                    break;
                case NeighbourLand.Scrub:
                    ground.sprite = SpriteBank.Rendered("ground_scrub") ?? SpriteBank.Grass;
                    break;
                case NeighbourLand.Farmstead:
                    ground.sprite = SpriteBank.Rendered("ground_village_" + (tv.Draw % 2)) ?? SpriteBank.Grass;
                    break;
                default:
                    ground.sprite = SpriteBank.Grass;
                    detail.sprite = SpriteBank.GrassDetail(tv.Id);
                    detail.color = _tint;
                    break;
            }
        }

        /// <summary>What stands on each neighbour's acre, rebuilt only when its look changes (a new season, a purchase).</summary>
        private void RefreshValley(MapVm map)
        {
            foreach (TileVm tv in map.Tiles)
            {
                string key = tv.Kind != TileKind.Unowned ? "" : tv.Neighbour + ":" + tv.TubeWell + ":" + tv.Haystack;
                _valleyKey.TryGetValue(tv.Id, out string was);
                if (key != (was ?? ""))
                {
                    if (_valley.TryGetValue(tv.Id, out List<SpriteRenderer> old)) foreach (SpriteRenderer r in old) Destroy(r.gameObject);
                    _valley[tv.Id] = Pieces(tv);
                    _valleyKey[tv.Id] = key;
                }
                if (_valley.TryGetValue(tv.Id, out List<SpriteRenderer> list)) foreach (SpriteRenderer r in list) r.color = _tint;
            }
            foreach (SpriteRenderer r in _edgeFarmsteads) r.color = _tint;
        }

        private List<SpriteRenderer> Pieces(TileVm tv)
        {
            var list = new List<SpriteRenderer>();
            if (tv.Kind != TileKind.Unowned) return list;
            var pieces = new List<Piece>();
            switch (tv.Neighbour)
            {
                case NeighbourLand.Farmstead: pieces.AddRange(Farmsteads[tv.Draw % Farmsteads.Length]); break;
                case NeighbourLand.Grove: pieces.AddRange(Grove); break;
                case NeighbourLand.Scrub:
                    pieces.Add(new Piece("scrub", 0.2f, -0.12f));
                    pieces.Add(new Piece("scrub", -0.22f, 0.2f));
                    pieces.Add(tv.Draw % 2 == 0 ? new Piece("tree_babul", -0.12f, -0.18f) : new Piece("tree_babul", 0.16f, 0.18f));
                    break;
            }
            if (tv.TubeWell) pieces.Add(new Piece("tubewell", 0.3f, 0.3f, 0.8f));
            if (tv.Haystack) pieces.Add(new Piece("village_haystack", -0.3f, 0.3f));
            foreach (Piece p in pieces) list.Add(PieceOn(tv.Id, p));
            return list;
        }

        private SpriteRenderer PieceOn(int tile, Piece p)
        {
            Vector3 pos = OnTile(tile, p.At);
            SpriteRenderer r = NewRenderer(p.Sprite, SpriteBank.Rendered(p.Sprite), pos, Order(tile, _width, 45, pos.y - TileCentre(tile).y));
            r.transform.localScale = Vector3.one * p.Scale;
            r.color = _tint;
            return r;
        }

        /// <summary>Farmsteads on the near edge's scrub too (ADR 0062 §2: the edge of the map).</summary>
        private void EdgeFarmstead(EdgeTile e, Vector3 centre)
        {
            Piece[] layout = Farmsteads[((e.X * 7 + e.Y * 3) % 4 + 4) % 4];
            foreach (Piece p in layout)
            {
                Vector3 pos = centre + new Vector3((p.At.x + p.At.y) * HalfWidth, (p.At.y - p.At.x) * _grid.HalfHeight, 0f);
                SpriteRenderer r = NewRenderer("edge" + p.Sprite, SpriteBank.Rendered(p.Sprite), pos, EdgeOrder(e.X, e.Y, 50) - Mathf.RoundToInt((pos.y - centre.y) * 100f));
                r.transform.localScale = Vector3.one * p.Scale;
                _edgeFarmsteads.Add(r);
            }
        }

        // ---- your land at a glance (ADR 0062 §3) ----

        /// <summary>
        /// Round your land, redrawn when you buy or sell and with the season (the worn bunds follow the neighbours' fields): on each
        /// field's edge to land not yours, the hedge on its mud wall (a gate onto a road); a fresh bund between two of your fields;
        /// a corner stone at each corner of the boundary; worn bunds between neighbours' fields. The yard and the quarters keep their
        /// fence, the cattle acre its own hedge.
        /// </summary>
        private void RefreshBunds(MapVm map)
        {
            int owned = map.Season;
            foreach (TileVm t in map.Tiles) if (Yours(t.Kind)) owned = owned * 31 + t.Id + 1;
            if (owned == _bundsFor) { foreach (SpriteRenderer b in _bunds) b.color = _tint; return; }
            _bundsFor = owned;
            foreach (SpriteRenderer b in _bunds) Destroy(b.gameObject);
            _bunds.Clear();
            var stones = new HashSet<int>();
            foreach (TileVm t in map.Tiles)
            {
                int x = t.X, y = t.Y;
                if (t.Kind == TileKind.Farm)
                {
                    Edge(map, t.Id, x, y - 1, "x", EdgeNE, 4, false);
                    Edge(map, t.Id, x + 1, y, "y", EdgeSE, 88, true);
                    Edge(map, t.Id, x, y + 1, "x", EdgeSW, 88, true);
                    Edge(map, t.Id, x - 1, y, "y", EdgeNW, 4, false);
                }
                if (Yours(t.Kind))
                {
                    // The corners: N (x, y), E (x+1, y), S (x+1, y+1), W (x, y+1) in the grid of acre corners.
                    Stone(map, stones, t.Id, x, y, -1, -1, new Vector2(-0.5f, 0.5f));
                    Stone(map, stones, t.Id, x + 1, y, 1, -1, new Vector2(0.5f, 0.5f));
                    Stone(map, stones, t.Id, x + 1, y + 1, 1, 1, new Vector2(0.5f, -0.5f));
                    Stone(map, stones, t.Id, x, y + 1, -1, 1, new Vector2(-0.5f, -0.5f));
                }
                if (t.Kind == TileKind.Unowned)
                {
                    Worn(map, t, x + 1, y, "bund_worn_y", EdgeSE);
                    Worn(map, t, x, y + 1, "bund_worn_x", EdgeSW);
                }
            }
        }

        private TileVm At(MapVm map, int x, int y) { return x >= 0 && y >= 0 && x < _width && y < _height ? map.Tiles[y * _width + x] : null; }

        /// <summary>One edge of your field: nothing to your yard, quarters or cattle acre; a fresh bund to your next field (drawn once, from the field behind); the hedge to anything else.</summary>
        private void Edge(MapVm map, int tile, int nx, int ny, string axis, Vector2 edge, int order, bool front)
        {
            TileVm n = At(map, nx, ny);
            if (n != null && Yours(n.Kind))
            {
                if (n.Kind != TileKind.Farm || !front) return;
                AddBund("bund_" + axis, tile, edge, CropBase + Order(tile, _width, order, edge.y));
                return;
            }
            string sprite = n != null && n.Kind == TileKind.Road ? "hedge_gate_" + axis : "hedge_" + axis;
            AddBund(sprite, tile, edge, Order(tile, _width, order, edge.y));
        }

        /// <summary>A corner stone where your land's corner meets land that isn't yours (once a corner).</summary>
        private void Stone(MapVm map, HashSet<int> stones, int tile, int vx, int vy, int dx, int dy, Vector2 corner)
        {
            int x = tile % _width, y = tile / _width;
            TileVm a = At(map, x + dx, y), b = At(map, x, y + dy), c = At(map, x + dx, y + dy);
            bool inside = a != null && Yours(a.Kind) && b != null && Yours(b.Kind) && c != null && Yours(c.Kind);
            if (inside || !stones.Add(vx * 1000 + vy)) return;
            Vector3 pos = OnTile(tile, corner);
            AddBund("corner_stone", tile, (Vector2)(pos - TileCentre(tile)), Order(tile, _width, 89, pos.y - TileCentre(tile).y));
        }

        /// <summary>A neighbour's old, grassed-over bund between two neighbours' acres when either is a field.</summary>
        private void Worn(MapVm map, TileVm t, int nx, int ny, string sprite, Vector2 edge)
        {
            TileVm n = At(map, nx, ny);
            if (n == null || n.Kind != TileKind.Unowned || !Tilled(t) && !Tilled(n)) return;
            AddBund(sprite, t.Id, edge, CropBase + Order(t.Id, _width, 87, edge.y));
        }

        private static bool Tilled(TileVm t) { return t.Neighbour == NeighbourLand.Field || t.Neighbour == NeighbourLand.Ploughed; }

        private void AddBund(string sprite, int tile, Vector2 edge, int order)
        {
            SpriteRenderer r = NewRenderer(sprite, SpriteBank.Rendered(sprite), TileCentre(tile) + (Vector3)edge, order);
            r.color = _tint;
            _bunds.Add(r);
        }
    }
}
