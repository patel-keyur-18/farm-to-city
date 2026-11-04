using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Irrigation on the map (spec 013 §3.1, ADR 0087; art approved by the owner 2026-11-04): the minor canal along its tile edge,
    /// ending in a headwall at the village lane, in place of the bunds and hedges there; a lined channel on a channelled field's west
    /// edge with a low gate where it leaves the canal; drip lines under a field's crop; a cement outlet at the back corner of each
    /// pumped field; the borewell's small pump room at its field's back corner; the pump on the well's parapet. Offsets are
    /// render.py's world units from the acre's centre (+x lower right, +y upper right on screen).
    /// </summary>
    public sealed partial class MapPresenter
    {
        private const string ChannelId = "canal_channel";
        private readonly HashSet<int> _channels = new HashSet<int>();
        private readonly List<SpriteRenderer> _irrigation = new List<SpriteRenderer>();
        private string _irrigationFor;
        private SpriteRenderer _well;

        /// <summary>The edge between two acres carries water: the canal, or a channel on the east acre's west edge.</summary>
        private bool WaterEdge(MapVm map, int tile, int nx, int ny)
        {
            int x = tile % _width, y = tile / _width;
            if (map.CanalRow >= 0 && nx == x && System.Math.Min(y, ny) == map.CanalRow && System.Math.Max(y, ny) == map.CanalRow + 1
                && x >= map.CanalFrom && x <= map.CanalTo) return true;
            if (ny != y) return false;
            if (nx == x - 1) return _channels.Contains(tile);
            return nx == x + 1 && _channels.Contains(y * _width + nx);
        }

        /// <summary>The canal's segments and its headwall at the lane, and each field channel with its gate (drawn with the bunds).</summary>
        private void DrawCanal(MapVm map)
        {
            if (map.CanalRow < 0) return;
            for (int x = map.CanalFrom; x <= map.CanalTo; x++)
            {
                int t = map.CanalRow * _width + x;
                AddBund("canal_x", t, EdgeSW, CropBase + Order(t, _width, 88, EdgeSW.y));
            }
            int end = map.CanalRow * _width + map.CanalTo;
            Vector3 head = OnTile(end, new Vector2(0.5f, -0.5f));
            AddBund("canal_end", end, (Vector2)(head - TileCentre(end)), Order(end, _width, 89, head.y - TileCentre(end).y));
            foreach (int t in _channels)
            {
                AddBund("channel_y", t, EdgeNW, CropBase + Order(t, _width, 4, EdgeNW.y));
                int y = t / _width;
                if (y != map.CanalRow && y != map.CanalRow + 1) continue;
                // The gate at the channel's head, on the field's own edge where it meets the canal: the north corner below the canal,
                // the west corner above it.
                Vector3 gate = OnTile(t, y == map.CanalRow + 1 ? new Vector2(-0.5f, 0.42f) : new Vector2(-0.5f, -0.42f));
                AddBund("channel_gate", t, (Vector2)(gate - TileCentre(t)), Order(t, _width, 5, gate.y - TileCentre(t).y));
            }
        }

        /// <summary>Drip under the crops, outlets on pumped fields, the borewell's room and the pump on the well: redrawn when they change.</summary>
        private void RefreshIrrigation(MapVm map)
        {
            var key = new System.Text.StringBuilder();
            foreach (TileVm t in map.Tiles) if (t.Pumped) key.Append('p').Append(t.Id);
            foreach (BuildingVm b in map.Buildings)
                if (b.DefId == "drip" || b.DefId == "borewell" || b.DefId == "pump") key.Append(b.DefId[0]).Append(b.TileId);
            string k = key.ToString();
            if (k == _irrigationFor) { foreach (SpriteRenderer r in _irrigation) r.color = _tint; return; }
            _irrigationFor = k;
            foreach (SpriteRenderer r in _irrigation) Destroy(r.gameObject);
            _irrigation.Clear();

            bool pump = false;
            foreach (BuildingVm b in map.Buildings)
            {
                if (b.DefId == "pump") pump = true;
                if (b.DefId == "drip") Irrigation("drip", b.TileId, Vector2.zero, CropBase + Order(b.TileId, _width, 19), 1f);
                if (b.DefId == "borewell") Irrigation("tubewell", b.TileId, new Vector2(-0.34f, 0.34f), 0, 0.8f);
            }
            foreach (TileVm t in map.Tiles)
                if (t.Pumped && !HasBorewell(map, t.Id)) Irrigation("pump_outlet", t.Id, new Vector2(-0.24f, 0.24f), 0, 1f);
            if (_well == null) foreach (SpriteRenderer r in _buildings) if (r.name == "well") _well = r;
            Sprite well = SpriteBank.Rendered(pump ? "well_pump" : "well");
            if (_well != null && well != null) _well.sprite = well;
        }

        private static bool HasBorewell(MapVm map, int tile)
        {
            foreach (BuildingVm b in map.Buildings) if (b.DefId == "borewell" && b.TileId == tile) return true;
            return false;
        }

        /// <summary>One piece on a tile; order 0 sorts it by its own depth on the tile.</summary>
        private void Irrigation(string sprite, int tile, Vector2 at, int order, float scale)
        {
            Vector3 pos = at == Vector2.zero ? TileCentre(tile) : OnTile(tile, at);
            SpriteRenderer r = NewRenderer(sprite, SpriteBank.Rendered(sprite), pos, order != 0 ? order : Order(tile, _width, 45, pos.y - TileCentre(tile).y));
            r.transform.localScale = Vector3.one * scale;
            r.color = _tint;
            _irrigation.Add(r);
        }
    }
}
