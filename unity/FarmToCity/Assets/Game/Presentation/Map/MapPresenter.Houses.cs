using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Family houses (spec 014, US-212): up to four on an acre, each on its own plot with its courtyard towards the camera, the acre
    /// drawn as a homestead (grass and beaten earth) instead of a field; on the labour quarters a house takes a hut's place. Sites
    /// show the building stage. Offsets are render.py's world units from the acre's centre (+x lower right, +y upper right).
    /// </summary>
    public sealed partial class MapPresenter
    {
        private struct HousePlot
        {
            public Vector2 At;
            public string Facing;
            public HousePlot(float x, float y, string facing) { At = new Vector2(x, y); Facing = facing; }
        }

        // Front plots first (nearest the camera), doors towards the camera; a little irregular so a street of them looks built, not stamped.
        // Two homes sharing a wall (as village houses stand along a lane), set back so their courtyards open onto the acre's front.
        private static readonly HousePlot[] HousePlots = { new HousePlot(-0.168f, 0.07f, "s"), new HousePlot(0.168f, 0.07f, "s") };

        /// <summary>The homestead's shared ground: a shade tree behind, a haystack at the side, a charpai on the open ground in front (nothing tall in front of the courtyards).</summary>
        private static readonly VillagePiece[] HomesteadProps =
        {
            new VillagePiece("village_tree", -0.36f, 0.38f),
            new VillagePiece("village_haystack", 0.40f, 0.30f), new VillagePiece("village_charpai", -0.16f, -0.36f),
        };

        private readonly Dictionary<long, SpriteRenderer> _houses = new Dictionary<long, SpriteRenderer>();
        private readonly Dictionary<long, SpriteRenderer> _houseSites = new Dictionary<long, SpriteRenderer>();

        private void RefreshHouses(MapVm map, bool animate)
        {
            // Plots in order on each acre: standing houses by id, then sites; on the quarters after its huts.
            var used = new Dictionary<int, int>();
            var seen = new HashSet<long>();
            foreach (BuildingVm b in map.Buildings)
            {
                if (b.DefId != Sim.Core.Definitions.PopulationDefs.HouseId) continue;
                seen.Add(b.Id);
                PlaceHouse(map, _houses, b.Id, b.TileId, "family_house_", used, animate);
            }
            foreach (long id in new List<long>(_houses.Keys)) if (!seen.Contains(id)) { Destroy(_houses[id].gameObject); _houses.Remove(id); }
            seen.Clear();
            foreach (ConstructionVm c in map.HouseSites)
            {
                seen.Add(c.Id);
                PlaceHouse(map, _houseSites, c.Id, c.TileId, "family_house_building_", used, animate);
            }
            foreach (long id in new List<long>(_houseSites.Keys)) if (!seen.Contains(id)) { Destroy(_houseSites[id].gameObject); _houseSites.Remove(id); }

            // The shared ground of each homestead acre.
            var acres = new HashSet<int>();
            foreach (TileVm t in map.Tiles) if (t.Kind == TileKind.Farm && t.Houses > 0) acres.Add(t.Id);
            foreach (int tile in new List<int>(_homesteadProps.Keys))
                if (!acres.Contains(tile)) { foreach (SpriteRenderer r in _homesteadProps[tile]) Destroy(r.gameObject); _homesteadProps.Remove(tile); }
            foreach (int tile in acres)
            {
                if (!_homesteadProps.TryGetValue(tile, out List<SpriteRenderer> props))
                {
                    _homesteadProps[tile] = props = new List<SpriteRenderer>();
                    foreach (VillagePiece piece in HomesteadProps)
                    {
                        Vector3 at = OnTile(tile, piece.At);
                        props.Add(NewRenderer("homestead" + tile + piece.Sprite, SpriteBank.Rendered(piece.Sprite), at, Order(tile, _width, 45, at.y - TileCentre(tile).y)));
                    }
                }
                foreach (SpriteRenderer r in props) r.color = _tint;
            }
        }

        private readonly Dictionary<int, List<SpriteRenderer>> _homesteadProps = new Dictionary<int, List<SpriteRenderer>>();

        private void PlaceHouse(MapVm map, Dictionary<long, SpriteRenderer> set, long id, int tile, string sprite, Dictionary<int, int> used, bool animate)
        {
            used.TryGetValue(tile, out int n);
            used[tile] = n + 1;
            bool quarters = map.Tiles[tile].Kind == TileKind.Quarters;
            Vector3 pos;
            string facing;
            int order;
            if (quarters)
            {
                int place = PlacesUsed(tile) + n; // after the hands' huts
                Vector2 off = QuartersPlaces[Mathf.Clamp(place, 0, QuartersPlaces.Length - 1)];
                pos = TileCentre(tile) + (Vector3)off;
                facing = "s";
                order = Order(tile, _width, 45, off.y);
            }
            else
            {
                HousePlot plot = HousePlots[Mathf.Clamp(n, 0, HousePlots.Length - 1)];
                pos = OnTile(tile, plot.At);
                facing = plot.Facing;
                order = Order(tile, _width, 45, pos.y - TileCentre(tile).y);
            }
            string name = sprite + facing;
            if (!set.TryGetValue(id, out SpriteRenderer r))
            {
                set[id] = r = NewRenderer(name + id, SpriteBank.Rendered(name) ?? SpriteBank.Hut, pos, order);
                SpriteRenderer shadow = NewRenderer(name + id + "Shadow", SpriteBank.ContactShadow, pos, order - 1, r.transform);
                shadow.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                if (animate) Pop(r.transform, 1f);
            }
            r.sprite = SpriteBank.Rendered(name) ?? r.sprite;
            r.transform.position = pos;
            r.sortingOrder = order;
            r.color = _tint;
        }

        /// <summary>Where a family is seen by day: in the courtyard in front of their house (the acre's middle if it isn't drawn yet).</summary>
        private Vector3 HouseYard(long houseId, int tile)
        {
            return _houses.TryGetValue(houseId, out SpriteRenderer r) ? r.transform.position + new Vector3(-0.05f, -0.09f, 0) : TileCentre(tile);
        }
    }
}
