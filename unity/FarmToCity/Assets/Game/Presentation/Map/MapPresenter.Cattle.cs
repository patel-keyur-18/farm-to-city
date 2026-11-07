using System.Collections.Generic;
using Game.Presentation.Art;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    /// <summary>
    /// The cattle acre (ADR 0064), laid out as the owner approved it (docs/art/s12-cattle-acre-*.png): a hedge and wall round it
    /// with the gate on the yard's side, the compost pit in the far corner, the padlock sign before the Hamlet; then the shed and
    /// feed room at the back, the straw stack by them, the neem with its shade, the water trough, a manger at each place, and the
    /// animals spread over four places. Positions are tools/blender/render.py's CATTLE_ACRE and CATTLE_PLACES.
    /// The second cattle acre (ADR 0088, docs/art/s16-second-shed-preview.png) is lighter: the shed, the straw stack and the trough,
    /// the gate on the far side, no neem and no compost pit, and a manger only where an animal stands.
    /// </summary>
    public sealed partial class MapPresenter
    {
        private static readonly Vector2 CattleShed = new Vector2(-0.04f, 0.33f), CattleStraw = new Vector2(0.38f, 0.32f), CattleNeem = new Vector2(-0.34f, -0.32f),
            CattleTrough = new Vector2(0.36f, -0.12f), CattlePit = new Vector2(0.36f, -0.38f), CattleSign = new Vector2(-0.05f, 0.12f);

        /// <summary>The animals' places (0 is the neem's shade, with no manger); each manger is at her head.</summary>
        private static readonly Vector2[] CattlePlaces = { new Vector2(-0.16f, -0.24f), new Vector2(-0.24f, 0.12f), new Vector2(0.18f, 0.06f), new Vector2(0.02f, -0.08f) };

        /// <summary>The second acre's places: 1 and 2 by the shed, 3 lying in the open (no manger), 0 at the front.</summary>
        private static readonly Vector2[] SecondPlaces = { new Vector2(0.02f, -0.08f), new Vector2(-0.24f, 0.12f), new Vector2(0.18f, 0.06f), new Vector2(-0.2f, -0.3f) };
        private static readonly Vector2 MangerAtHead = new Vector2(0.06f, -0.21f);
        private static readonly string[] StrawSprites = { "straw_stack_empty", "straw_stack_low", "straw_stack_mid", "straw_stack_high" };

        /// <summary>One cattle acre's pieces.</summary>
        private sealed class CattleYard
        {
            public int Tile;
            public bool Second;
            public Vector2[] Places;
            public int NoManger; // the place without a manger: the neem's shade, or the open ground
            public SpriteRenderer Shed, Straw, Neem, Trough, Pit, Sign;
            public readonly SpriteRenderer[] Mangers = new SpriteRenderer[4];
            public readonly Dictionary<long, SpriteRenderer> Animals = new Dictionary<long, SpriteRenderer>();
            public readonly List<SpriteRenderer> Static = new List<SpriteRenderer>();
            public int ShedShown = -1;
        }

        private CattleYard _cattle, _secondCattle;

        /// <summary>A point on an acre (render.py's world units: +x lower right, +y upper right) in the map's space.</summary>
        private Vector3 OnAcre(int tile, Vector2 p) { return TileCentre(tile) + new Vector3((p.x + p.y) * HalfWidth, (p.y - p.x) * _grid.HalfHeight, 0f); }

        /// <summary>Further back on the acre draws first; the same scale as the tile's other props.</summary>
        private int AcreOrder(int tile, Vector2 p) { return Order(tile, _width, 50, (p.y - p.x) * _grid.HalfHeight); }

        private SpriteRenderer AcrePiece(int tile, string name, string sprite, Vector2 p)
        {
            SpriteRenderer r = NewRenderer(name, SpriteBank.Rendered(sprite), OnAcre(tile, p), AcreOrder(tile, p));
            r.color = _tint;
            return r;
        }

        private void BuildCattleAcre(MapVm map)
        {
            if (map.Cattle == null) return;
            int t = map.Cattle.TileId;
            var y = new CattleYard { Tile = t, Places = CattlePlaces, NoManger = 0 };
            y.Static.Add(AcrePiece(t, "hedgeNE", "hedge_x", new Vector2(0f, 0.5f)));
            y.Static.Add(AcrePiece(t, "hedgeSE", "hedge_y", new Vector2(0.5f, 0f)));
            y.Static.Add(AcrePiece(t, "hedgeSW", "hedge_x", new Vector2(0f, -0.5f)));
            y.Static.Add(AcrePiece(t, "hedgeGate", "hedge_gate_y", new Vector2(-0.5f, 0f)));
            y.Pit = AcrePiece(t, "compostPit", "compost_pit_0", CattlePit);
            y.Sign = AcrePiece(t, "cattleSign", "sign_locked", CattleSign);
            y.Shed = AcrePiece(t, "cattleShed", "cattle_shed", CattleShed);
            y.Straw = AcrePiece(t, "strawStack", "straw_stack_empty", CattleStraw);
            y.Neem = AcrePiece(t, "neem", "tree_neem", CattleNeem);
            y.Trough = AcrePiece(t, "waterTrough", "water_trough", CattleTrough);
            for (int i = 1; i < 4; i++) y.Mangers[i] = AcrePiece(t, "manger" + i, "cattle_manger", CattlePlaces[i] + MangerAtHead);
            _cattle = y;
        }

        /// <summary>The second cattle acre, when a second shed is built or stands; gone with it (foreclosure).</summary>
        private void SyncSecondCattle(CattleAcreVm acre)
        {
            if (_secondCattle != null && (acre == null || acre.TileId != _secondCattle.Tile))
            {
                foreach (SpriteRenderer r in _secondCattle.Static) Destroy(r.gameObject);
                foreach (SpriteRenderer r in _secondCattle.Animals.Values) Destroy(r.gameObject);
                _secondCattle = null;
            }
            if (acre == null || _secondCattle != null) return;
            int t = acre.TileId;
            var y = new CattleYard { Tile = t, Second = true, Places = SecondPlaces, NoManger = 3 };
            y.Static.Add(AcrePiece(t, "hedge2NE", "hedge_x", new Vector2(0f, 0.5f)));
            y.Static.Add(AcrePiece(t, "hedge2SW", "hedge_x", new Vector2(0f, -0.5f)));
            y.Static.Add(AcrePiece(t, "hedge2NW", "hedge_y", new Vector2(-0.5f, 0f)));
            y.Static.Add(AcrePiece(t, "hedge2Gate", "hedge_gate_y", new Vector2(0.5f, 0f)));
            y.Static.Add(y.Shed = AcrePiece(t, "cattleShed2", "cattle_shed", CattleShed));
            y.Static.Add(y.Straw = AcrePiece(t, "strawStack2", "straw_stack_empty", CattleStraw));
            y.Static.Add(y.Trough = AcrePiece(t, "waterTrough2", "water_trough", CattleTrough));
            for (int i = 0; i < 4; i++)
                if (i != y.NoManger) y.Static.Add(y.Mangers[i] = AcrePiece(t, "manger2_" + i, "cattle_manger", SecondPlaces[i] + MangerAtHead));
            _secondCattle = y;
        }

        private void RefreshCattleAcre(MapVm map, bool animate)
        {
            if (map.Cattle != null && _cattle != null) RefreshYard(_cattle, map.Cattle, animate);
            SyncSecondCattle(map.SecondCattle);
            if (_secondCattle != null) RefreshYard(_secondCattle, map.SecondCattle, animate);
        }

        private void RefreshYard(CattleYard y, CattleAcreVm acre, bool animate)
        {
            foreach (SpriteRenderer r in y.Static) r.color = _tint;
            if (y.Pit != null) y.Pit.sprite = SpriteBank.Rendered("compost_pit_" + acre.Compost);
            if (y.Sign != null) y.Sign.gameObject.SetActive(acre.Locked);
            bool built = acre.Shed == 2;
            y.Shed.gameObject.SetActive(acre.Shed > 0);
            y.Shed.sprite = SpriteBank.Rendered(built ? "cattle_shed" : "cattle_shed_building");
            if (animate && y.ShedShown >= 0 && acre.Shed != y.ShedShown && acre.Shed > 0) Pop(y.Shed.transform, 1f);
            y.ShedShown = acre.Shed;
            // The yard's furniture comes with the finished shed: the straw stack, the neem planted for shade, the trough, the mangers.
            var occupied = new bool[4];
            foreach (AnimalVm a in acre.Animals) occupied[Mathf.Clamp(a.Place, 0, 3)] = true;
            foreach (SpriteRenderer r in new[] { y.Straw, y.Neem, y.Trough }) if (r != null) r.gameObject.SetActive(built);
            for (int i = 0; i < 4; i++)
                if (y.Mangers[i] != null) y.Mangers[i].gameObject.SetActive(built && (!y.Second || occupied[i])); // the second acre: a manger where an animal stands
            y.Straw.sprite = SpriteBank.Rendered(StrawSprites[Mathf.Clamp(acre.Straw, 0, 3)]);
            foreach (SpriteRenderer r in new[] { y.Pit, y.Sign, y.Shed, y.Straw, y.Neem, y.Trough, y.Mangers[0], y.Mangers[1], y.Mangers[2], y.Mangers[3] }) if (r != null) r.color = _tint;

            var seen = new HashSet<long>();
            foreach (AnimalVm a in acre.Animals)
            {
                seen.Add(a.Id);
                int place = Mathf.Clamp(a.Place, 0, 3);
                Vector2 p = y.Places[place];
                // An animal with no manger to eat from stands instead.
                string pose = place == y.NoManger && a.Pose == "eat" ? "stand" : a.Pose;
                Sprite s = SpriteBank.Rendered("animal_" + a.Kind + "_" + pose);
                if (!y.Animals.TryGetValue(a.Id, out SpriteRenderer r))
                {
                    y.Animals[a.Id] = r = AcrePiece(y.Tile, "animal" + a.Id, "animal_" + a.Kind + "_" + pose, p);
                    if (animate) Pop(r.transform, 1f);
                }
                r.sprite = s;
                r.transform.position = OnAcre(y.Tile, p);
                r.sortingOrder = AcreOrder(y.Tile, p) + 1;
                r.color = _tint;
            }
            var gone = new List<long>();
            foreach (KeyValuePair<long, SpriteRenderer> kv in y.Animals) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (long id in gone)
            {
                Destroy(y.Animals[id].gameObject);
                y.Animals.Remove(id);
            }
        }
    }
}
