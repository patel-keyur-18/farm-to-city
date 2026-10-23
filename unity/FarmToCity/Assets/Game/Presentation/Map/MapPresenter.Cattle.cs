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
    /// </summary>
    public sealed partial class MapPresenter
    {
        private static readonly Vector2 CattleShed = new Vector2(-0.04f, 0.33f), CattleStraw = new Vector2(0.38f, 0.32f), CattleNeem = new Vector2(-0.34f, -0.32f),
            CattleTrough = new Vector2(0.36f, -0.12f), CattlePit = new Vector2(0.36f, -0.38f), CattleSign = new Vector2(-0.05f, 0.12f);

        /// <summary>The animals' places (0 is the neem's shade, with no manger); each manger is at her head.</summary>
        private static readonly Vector2[] CattlePlaces = { new Vector2(-0.16f, -0.24f), new Vector2(-0.24f, 0.12f), new Vector2(0.18f, 0.06f), new Vector2(0.02f, -0.08f) };
        private static readonly Vector2 MangerAtHead = new Vector2(0.06f, -0.21f);
        private static readonly string[] StrawSprites = { "straw_stack_empty", "straw_stack_low", "straw_stack_mid", "straw_stack_high" };

        private SpriteRenderer _shed, _straw, _neem, _trough, _pit, _cattleSign;
        private readonly SpriteRenderer[] _mangers = new SpriteRenderer[4];
        private readonly Dictionary<long, SpriteRenderer> _animals = new Dictionary<long, SpriteRenderer>();
        private readonly List<SpriteRenderer> _cattleStatic = new List<SpriteRenderer>();
        private int _cattleTile = -1;
        private int _shedShown = -1;

        /// <summary>A point on the acre (render.py's world units: +x lower right, +y upper right) in the map's space.</summary>
        private Vector3 OnAcre(Vector2 p) { return TileCentre(_cattleTile) + new Vector3((p.x + p.y) * HalfWidth, (p.y - p.x) * _grid.HalfHeight, 0f); }

        /// <summary>Further back on the acre draws first; the same scale as the tile's other props.</summary>
        private int AcreOrder(Vector2 p) { return Order(_cattleTile, _width, 50, (p.y - p.x) * _grid.HalfHeight); }

        private SpriteRenderer AcrePiece(string name, string sprite, Vector2 p)
        {
            SpriteRenderer r = NewRenderer(name, SpriteBank.Rendered(sprite), OnAcre(p), AcreOrder(p));
            r.color = _tint;
            return r;
        }

        private void BuildCattleAcre(MapVm map)
        {
            if (map.Cattle == null) return;
            _cattleTile = map.Cattle.TileId;
            _cattleStatic.Add(AcrePiece("hedgeNE", "hedge_x", new Vector2(0f, 0.5f)));
            _cattleStatic.Add(AcrePiece("hedgeSE", "hedge_y", new Vector2(0.5f, 0f)));
            _cattleStatic.Add(AcrePiece("hedgeSW", "hedge_x", new Vector2(0f, -0.5f)));
            _cattleStatic.Add(AcrePiece("hedgeGate", "hedge_gate_y", new Vector2(-0.5f, 0f)));
            _pit = AcrePiece("compostPit", "compost_pit_0", CattlePit);
            _cattleSign = AcrePiece("cattleSign", "sign_locked", CattleSign);
            _shed = AcrePiece("cattleShed", "cattle_shed", CattleShed);
            _straw = AcrePiece("strawStack", "straw_stack_empty", CattleStraw);
            _neem = AcrePiece("neem", "tree_neem", CattleNeem);
            _trough = AcrePiece("waterTrough", "water_trough", CattleTrough);
            for (int i = 1; i < 4; i++) _mangers[i] = AcrePiece("manger" + i, "cattle_manger", CattlePlaces[i] + MangerAtHead);
        }

        private void RefreshCattleAcre(MapVm map, bool animate)
        {
            CattleAcreVm acre = map.Cattle;
            if (acre == null || _cattleTile < 0) return;
            foreach (SpriteRenderer r in _cattleStatic) r.color = _tint;
            _pit.sprite = SpriteBank.Rendered("compost_pit_" + acre.Compost);
            _cattleSign.gameObject.SetActive(acre.Locked);
            bool built = acre.Shed == 2;
            _shed.gameObject.SetActive(acre.Shed > 0);
            _shed.sprite = SpriteBank.Rendered(built ? "cattle_shed" : "cattle_shed_building");
            if (animate && _shedShown >= 0 && acre.Shed != _shedShown && acre.Shed > 0) Pop(_shed.transform, 1f);
            _shedShown = acre.Shed;
            // The yard's furniture comes with the finished shed: the straw stack, the neem planted for shade, the trough, the mangers.
            foreach (SpriteRenderer r in new[] { _straw, _neem, _trough, _mangers[1], _mangers[2], _mangers[3] }) r.gameObject.SetActive(built);
            _straw.sprite = SpriteBank.Rendered(StrawSprites[Mathf.Clamp(acre.Straw, 0, 3)]);
            foreach (SpriteRenderer r in new[] { _pit, _cattleSign, _shed, _straw, _neem, _trough, _mangers[1], _mangers[2], _mangers[3] }) r.color = _tint;

            var seen = new HashSet<long>();
            foreach (AnimalVm a in acre.Animals)
            {
                seen.Add(a.Id);
                Vector2 p = CattlePlaces[Mathf.Clamp(a.Place, 0, 3)];
                // An animal at the neem's shade has no manger to eat from: she stands there instead.
                string pose = a.Place == 0 && a.Pose == "eat" ? "stand" : a.Pose;
                Sprite s = SpriteBank.Rendered("animal_" + a.Kind + "_" + pose);
                if (!_animals.TryGetValue(a.Id, out SpriteRenderer r))
                {
                    _animals[a.Id] = r = AcrePiece("animal" + a.Id, "animal_" + a.Kind + "_" + pose, p);
                    if (animate) Pop(r.transform, 1f);
                }
                r.sprite = s;
                r.transform.position = OnAcre(p);
                r.sortingOrder = AcreOrder(p) + 1;
                r.color = _tint;
            }
            var gone = new List<long>();
            foreach (KeyValuePair<long, SpriteRenderer> kv in _animals) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (long id in gone)
            {
                Destroy(_animals[id].gameObject);
                _animals.Remove(id);
            }
        }
    }
}
