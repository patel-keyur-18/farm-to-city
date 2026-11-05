using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Whole-acre orchards (spec 013 §3.3, ADR 0090; the light layout approved 2026-11-05): a grid of trees over the acre in their
    /// stage, a crop between young rows drawn under them, and one ripe marker over the acre.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private sealed class OrchardSpot
        {
            public string Key;
            public readonly List<SpriteRenderer> Trees = new List<SpriteRenderer>();
            public SpriteRenderer Marker;
        }

        /// <summary>Trees a side, their scale and the margin from the acre's edge (tile-local, −0.5 to 0.5): an art layout, not a count.</summary>
        private static (int side, float scale, float margin) OrchardGrid(string tree)
        {
            switch (tree)
            {
                case "mango": return (3, 0.4f, 0.15f);
                case "guava": return (3, 0.36f, 0.14f);
                default: return (4, 0.27f, 0.11f); // banana, papaya
            }
        }

        private readonly Dictionary<int, OrchardSpot> _orchards = new Dictionary<int, OrchardSpot>();

        private void RefreshOrchards(MapVm map, bool animate)
        {
            var tiles = new HashSet<int>();
            foreach (OrchardVm o in map.Orchards) tiles.Add(o.TileId);
            var gone = new List<int>();
            foreach (KeyValuePair<int, OrchardSpot> kv in _orchards) if (!tiles.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (int t in gone)
            {
                foreach (SpriteRenderer r in _orchards[t].Trees) Destroy(r.gameObject);
                Destroy(_orchards[t].Marker.gameObject);
                _orchards.Remove(t);
            }
            foreach (OrchardVm o in map.Orchards)
            {
                bool planted = !_orchards.TryGetValue(o.TileId, out OrchardSpot spot);
                if (planted)
                {
                    // Over the trees, beside where a field's own marker stands (a crop left between the rows has one too).
                    spot = new OrchardSpot { Marker = NewRenderer("orchardMark" + o.TileId, null, TileCentre(o.TileId) + new Vector3(0.16f, 0.3f, 0), Order(o.TileId, _width, 95)) };
                    _orchards[o.TileId] = spot;
                }
                string key = o.TreeId + ":" + o.Stage;
                if (spot.Key != key)
                {
                    spot.Key = key;
                    (int side, float scale, float margin) = OrchardGrid(o.TreeId);
                    Sprite s = SpriteBank.Tree(o.TreeId, o.Stage);
                    float step = (1f - 2f * margin) / side;
                    int n = 0;
                    for (int i = 0; i < side; i++)
                        for (int j = 0; j < side; j++, n++)
                        {
                            var at = new Vector2(-0.5f + margin + step * (i + 0.5f), -0.5f + margin + step * (j + 0.5f));
                            if (n == spot.Trees.Count) spot.Trees.Add(NewRenderer("orchard" + o.TileId + "_" + n, null, OnAcre(o.TileId, at), AcreOrder(o.TileId, at)));
                            SpriteRenderer r = spot.Trees[n];
                            r.sprite = s;
                            r.transform.position = OnAcre(o.TileId, at);
                            r.transform.localScale = Vector3.one * scale;
                            r.sortingOrder = AcreOrder(o.TileId, at);
                            if (animate && planted) Pop(r.transform, scale);
                        }
                    while (spot.Trees.Count > n)
                    {
                        Destroy(spot.Trees[n].gameObject);
                        spot.Trees.RemoveAt(n);
                    }
                }
                foreach (SpriteRenderer r in spot.Trees) r.color = _tint;
                Sprite ms = o.Stage == TreeStage.Ripe ? SpriteBank.MarkerReady : o.Stage == TreeStage.Spoiling ? SpriteBank.MarkerSpoiling : null;
                if (spot.Marker.sprite != ms)
                {
                    spot.Marker.sprite = ms;
                    if (animate && ms != null) Pop(spot.Marker.transform, 1f);
                }
            }
        }
    }
}
