using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// Businesses (spec 016 §7): the mill's shed at the back of its acre, its front towards the camera (ADR 0065), the acre drawn as
    /// its work yard with a shade tree behind; while it grinds for the area, people wait in the yard with their grain. Offsets are
    /// render.py's world units from the acre's centre (+x lower right, +y upper right).
    /// </summary>
    public sealed partial class MapPresenter
    {
        private static readonly Vector2 MillAt = new Vector2(0.06f, 0.16f);

        /// <summary>Where people wait their turn: in the yard before the shed, a little apart (the first nearest the door).</summary>
        private static readonly Vector2[] MillQueueAt = { new Vector2(-0.04f, -0.10f), new Vector2(-0.16f, -0.18f), new Vector2(-0.28f, -0.26f) };

        private static readonly VillagePiece[] MillProps = { new VillagePiece("village_tree", -0.38f, 0.36f) };

        private readonly Dictionary<long, SpriteRenderer> _businesses = new Dictionary<long, SpriteRenderer>();
        private readonly Dictionary<long, List<SpriteRenderer>> _businessProps = new Dictionary<long, List<SpriteRenderer>>();

        private void RefreshBusinesses(MapVm map, bool animate)
        {
            var seen = new HashSet<long>();
            foreach (BusinessVm b in map.Businesses)
            {
                seen.Add(b.Id);
                string name = b.TypeId + (b.Building ? "_building_s" : "_s");
                Vector3 pos = OnTile(b.TileId, MillAt);
                int order = Order(b.TileId, _width, 45, pos.y - TileCentre(b.TileId).y);
                if (!_businesses.TryGetValue(b.Id, out SpriteRenderer r))
                {
                    _businesses[b.Id] = r = NewRenderer("business" + b.Id, SpriteBank.Rendered(name) ?? SpriteBank.Hut, pos, order);
                    SpriteRenderer shadow = NewRenderer("business" + b.Id + "Shadow", SpriteBank.ContactShadow, pos, order - 1, r.transform);
                    shadow.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
                    if (animate) Pop(r.transform, 1f);
                    var props = new List<SpriteRenderer>();
                    foreach (VillagePiece piece in MillProps)
                    {
                        Vector3 at = OnTile(b.TileId, piece.At);
                        props.Add(NewRenderer("business" + b.Id + piece.Sprite, SpriteBank.Rendered(piece.Sprite), at, Order(b.TileId, _width, 45, at.y - TileCentre(b.TileId).y)));
                    }
                    _businessProps[b.Id] = props;
                }
                r.sprite = SpriteBank.Rendered(name) ?? r.sprite;
                r.color = _tint;
                foreach (SpriteRenderer p in _businessProps[b.Id]) p.color = _tint;
            }
            foreach (long id in new List<long>(_businesses.Keys))
            {
                if (seen.Contains(id)) continue;
                Destroy(_businesses[id].gameObject);
                foreach (SpriteRenderer p in _businessProps[id]) Destroy(p.gameObject);
                _businesses.Remove(id);
                _businessProps.Remove(id);
            }
        }

        private Vector3 MillQueue(long businessId, int tile, int place)
        {
            return OnTile(tile, MillQueueAt[Mathf.Clamp(place, 0, MillQueueAt.Length - 1)]);
        }
    }
}
