using System.Collections.Generic;
using Game.Presentation.Art;
using Game.Presentation.Motion;
using Sim.View.Content;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation.Map
{
    public enum HitKind
    {
        None = 0,
        Tile = 1,
        Slot = 2,
        Bed = 3,
        Hut = 4,

        /// <summary>A hand's hut (Id = building id) or one being built (Id = −construction id).</summary>
        HandHut = 5,
    }

    /// <summary>What a tap landed on: a tile, a tree slot, a garden bed or the hut (the store).</summary>
    public readonly struct Hit
    {
        public HitKind Kind { get; }
        public long Id { get; }

        public Hit(HitKind kind, long id)
        {
            Kind = kind;
            Id = id;
        }

        public static readonly Hit None = new Hit(HitKind.None, 0);
    }

    /// <summary>
    /// Draws the isometric homestead from the map view model (spec 008 §3.4): Kenney ground tiles with painted detail, crops in three
    /// stages, trees, the kitchen-garden beds, the hut and the well, markers for ready, spoiling and pests, a selection ring, and the
    /// day and season tint. Sprites are created once and updated in place each refresh; motion (pop-in, bobbing markers, the tap
    /// squash) is code-driven tweens. The simulation never waits for any of it.
    /// </summary>
    public sealed class MapPresenter : MonoBehaviour
    {
        private const float HalfWidth = 0.5f;

        // Where things sit inside a tile, in world units from the tile centre (y up).
        private static readonly Vector2[] EdgeSlots = { new Vector2(-0.36f, -0.02f), new Vector2(0.36f, -0.02f) }; // the acre's left and right corners
        // The yard (Blender hut, ADR 0039): the house at the back, the well behind its right shoulder, the beds in front, and the four
        // yard trees on the corners and the front-right edge, clear of the house.
        private static readonly Vector2[] YardSlots = { new Vector2(-0.49f, 0.0f), new Vector2(0.49f, 0.0f), new Vector2(0.25f, -0.13f), new Vector2(0.02f, -0.24f) };
        private static readonly Vector2[] BedSpots = { new Vector2(-0.33f, -0.07f), new Vector2(-0.14f, -0.16f) };
        private static readonly Vector2 HutSpot = new Vector2(-0.08f, 0.10f), WellSpot = new Vector2(0.30f, 0.07f);

        /// <summary>A hand's hut stands at the head of its field (the acre's top corner, ADR 0039 §2).</summary>
        private static readonly Vector2 HandHutSpot = new Vector2(0.0f, 0.13f);

        /// <summary>Where people stand on a tile, by arrival order, so a crowd doesn't stack.</summary>
        private static readonly Vector2[] PersonSpots = { new Vector2(-0.12f, -0.06f), new Vector2(0.14f, -0.03f), new Vector2(0.0f, -0.13f), new Vector2(-0.22f, 0.04f), new Vector2(0.24f, 0.05f) };

        private sealed class PersonView
        {
            public SpriteRenderer Body, Shadow;
            public string Sprite; // person_<kind>
            public Vector3 Pos, Target;
            public int Tile;
            public bool Working, Leaving;
            public float Phase;
        }

        private readonly Dictionary<long, SpriteRenderer> _handHuts = new Dictionary<long, SpriteRenderer>();
        private readonly Dictionary<long, SpriteRenderer> _sites = new Dictionary<long, SpriteRenderer>();
        private readonly Dictionary<string, PersonView> _people = new Dictionary<string, PersonView>();
        private SpriteRenderer _ghost, _ghostRing;

        private sealed class Spot
        {
            public SpriteRenderer Renderer;
            public SpriteRenderer Marker;
            public Vector3 Home;
            public Sprite Last;
            public string LastKey;
            public bool Empty; // a tree place with no tree (drawn as a planting spot when on the player's land)
        }

        private Tweens _tweens;
        private UiDef _ui;
        private IsoGrid _grid;
        private int _width, _height;
        private readonly List<SpriteRenderer> _ground = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _detail = new List<SpriteRenderer>();
        private readonly List<Spot> _crops = new List<Spot>();
        private readonly List<SpriteRenderer> _signs = new List<SpriteRenderer>();
        private readonly Dictionary<long, Spot> _slots = new Dictionary<long, Spot>();
        private readonly Dictionary<long, Spot> _beds = new Dictionary<long, Spot>();
        private readonly List<SpriteRenderer> _buildings = new List<SpriteRenderer>();
        private readonly Dictionary<long, Vector2> _slotWorld = new Dictionary<long, Vector2>(), _bedWorld = new Dictionary<long, Vector2>();
        private Vector2 _hutWorld, _wellWorld;
        private SpriteRenderer _ring;
        private int _ringTile = -1;
        private Color _tint = Color.white;
        private bool _built;

        public IsoGrid Grid { get { return _grid; } }

        public static MapPresenter Create(Transform parent, Tweens tweens, UiDef ui)
        {
            var go = new GameObject("Map");
            go.transform.SetParent(parent, false);
            MapPresenter m = go.AddComponent<MapPresenter>();
            m._tweens = tweens;
            m._ui = ui;
            return m;
        }

        private Vector3 TileCentre(int tile)
        {
            (float x, float y) = _grid.Centre(tile % _width, tile / _width);
            return new Vector3(x, y, 0);
        }

        private static int Order(int tile, int width, int offset = 0, float offY = 0f) { return (tile % width + tile / width) * 100 + offset - Mathf.RoundToInt(offY * 100f); }

        private SpriteRenderer NewRenderer(string name, Sprite sprite, Vector3 position, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.position = position;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = order;
            return r;
        }

        private void Build(MapVm map)
        {
            _width = map.Width;
            _height = map.Height;
            _grid = new IsoGrid(map.Width, map.Height, HalfWidth);
            foreach (TileVm t in map.Tiles)
            {
                Vector3 c = TileCentre(t.Id);
                _ground.Add(NewRenderer("ground" + t.Id, SpriteBank.Grass, c, Order(t.Id, _width)));
                _detail.Add(NewRenderer("detail" + t.Id, null, c, Order(t.Id, _width, 1)));
                _signs.Add(NewRenderer("sign" + t.Id, SpriteBank.Sign, c + new Vector3(0.0f, 0.06f, 0), Order(t.Id, _width, 30)));
                _signs[_signs.Count - 1].transform.localScale = Vector3.one * 0.5f;
                _signs[_signs.Count - 1].gameObject.SetActive(false);
                var crop = new Spot { Home = c, Renderer = NewRenderer("crop" + t.Id, null, c, Order(t.Id, _width, 20)) };
                crop.Marker = NewRenderer("mark" + t.Id, null, c + new Vector3(0.02f, 0.34f, 0), Order(t.Id, _width, 90));
                _crops.Add(crop);
            }
            foreach (SlotVm s in map.Slots)
            {
                Vector2 off = s.Yard ? YardSlots[Mathf.Min(s.Index, YardSlots.Length - 1)] : EdgeSlots[Mathf.Min(s.Index, EdgeSlots.Length - 1)];
                Vector3 pos = TileCentre(s.TileId) + (Vector3)off;
                var spot = new Spot { Home = pos, Renderer = NewRenderer("slot" + s.Id, null, pos, Order(s.TileId, _width, 50, off.y)) };
                spot.Renderer.transform.localScale = Vector3.one * (s.Yard ? 0.62f : 0.5f);
                spot.Marker = NewRenderer("slotMark" + s.Id, null, pos + new Vector3(0, s.Yard ? 0.5f : 0.42f, 0), Order(s.TileId, _width, 92));
                _slots[s.Id] = spot;
                _slotWorld[s.Id] = pos;
            }
            int bedIndex = 0;
            foreach (BedVm b in map.Beds)
            {
                // Beds sit on the yard tile, beside the hut.
                int yard = map.Tiles.Find(t => t.Kind == TileKind.Yard).Id;
                Vector2 off = BedSpots[Mathf.Min(bedIndex++, BedSpots.Length - 1)];
                Vector3 pos = TileCentre(yard) + (Vector3)off;
                var spot = new Spot { Home = pos, Renderer = NewRenderer("bed" + b.Id, SpriteBank.Bed(null, CropStage.None), pos, Order(yard, _width, 40, off.y)) };
                spot.Renderer.transform.localScale = Vector3.one * 0.64f;
                spot.Marker = NewRenderer("bedMark" + b.Id, null, pos + new Vector3(0, 0.3f, 0), Order(yard, _width, 93));
                _beds[b.Id] = spot;
                _bedWorld[b.Id] = pos;
            }
            foreach (BuildingVm b in map.Buildings)
            {
                bool hut = b.DefId == "hut";
                Vector2 off = hut ? HutSpot : WellSpot;
                Vector3 pos = TileCentre(b.TileId) + (Vector3)off;
                Sprite rendered = SpriteBank.Rendered(b.DefId);
                SpriteRenderer r = NewRenderer(b.DefId, rendered != null ? rendered : hut ? SpriteBank.Hut : SpriteBank.Well, pos, Order(b.TileId, _width, 45, off.y));
                r.transform.localScale = Vector3.one * (rendered != null ? 1f : hut ? 0.62f : 0.5f);
                if (rendered != null)
                {
                    SpriteRenderer shadow = NewRenderer(b.DefId + "Shadow", SpriteBank.ContactShadow, pos, Order(b.TileId, _width, 44, off.y));
                    shadow.transform.localScale = new Vector3(hut ? 0.62f : 0.26f, hut ? 0.62f : 0.26f, 1f);
                }
                _buildings.Add(r);
                if (hut) _hutWorld = pos; else _wellWorld = pos;
            }
            _ring = NewRenderer("ring", SpriteBank.SelectionRing, Vector3.zero, 9000);
            _ring.gameObject.SetActive(false);
            _built = true;
        }

        public void Refresh(MapVm map, bool animate)
        {
            if (!_built) Build(map);
            Rgb t = Tint.Map(_ui, map.Season, map.SecondOfDay);
            _tint = new Color(t.R, t.G, t.B, 1f);

            foreach (TileVm tv in map.Tiles)
            {
                SpriteRenderer ground = _ground[tv.Id], detail = _detail[tv.Id];
                switch (tv.Kind)
                {
                    case TileKind.Unowned:
                        ground.sprite = SpriteBank.Grass;
                        ground.color = _tint * new Color(0.80f, 0.86f, 0.78f, 1f); // desaturated: neighbours' land
                        detail.sprite = SpriteBank.GrassDetail(tv.Id);
                        detail.color = _tint * new Color(0.85f, 0.9f, 0.85f, 0.8f);
                        break;
                    case TileKind.Yard:
                        ground.sprite = SpriteBank.Grass;
                        ground.color = _tint * new Color(1.02f, 1.0f, 0.9f, 1f);
                        detail.sprite = SpriteBank.GrassDetail(tv.Id);
                        detail.color = _tint;
                        break;
                    default:
                        ground.sprite = SpriteBank.SoilTile;
                        ground.color = _tint * SoilColour(tv);
                        detail.sprite = SpriteBank.Furrows;
                        detail.color = _tint;
                        break;
                }
                _signs[tv.Id].gameObject.SetActive(tv.ForSaleSign);
                _signs[tv.Id].color = _tint;

                Spot crop = _crops[tv.Id];
                Sprite cs = tv.CropId == null ? null : SpriteBank.Crop(tv.CropId, tv.Stage);
                Apply(crop.Renderer, crop, cs, tv.CropId + ":" + tv.Stage, animate);
                crop.Renderer.color = _tint;
                Sprite ms = tv.Stage == CropStage.Ready ? SpriteBank.MarkerReady : tv.Stage == CropStage.Spoiling ? SpriteBank.MarkerSpoiling
                    : tv.PestWarning ? SpriteBank.MarkerPest : null;
                Apply(crop.Marker, crop, ms, "m:" + ms?.name, animate, markerOnly: true);
                crop.Marker.color = Color.white;
            }

            foreach (SlotVm s in map.Slots)
            {
                Spot spot = _slots[s.Id];
                bool empty = s.Stage == TreeStage.Empty;
                Sprite ts = empty ? (s.Owned ? SpriteBank.PlantingSpot : null) : SpriteBank.Tree(s.TreeId, s.Stage);
                bool planted = spot.Empty && !empty && spot.Renderer.sprite != null;
                Apply(spot.Renderer, spot, ts, s.TreeId + ":" + s.Stage, animate);
                if (animate && planted) Pop(spot.Renderer.transform, spot.Renderer.transform.localScale.x); // the sapling pops in over the spot
                spot.Empty = empty;
                spot.Renderer.color = _tint;
                Sprite ms = s.Stage == TreeStage.Ripe ? SpriteBank.MarkerReady : s.Stage == TreeStage.Spoiling ? SpriteBank.MarkerSpoiling : null;
                Apply(spot.Marker, spot, ms, "m:" + ms?.name, animate, markerOnly: true);
            }

            foreach (BedVm b in map.Beds)
            {
                Spot spot = _beds[b.Id];
                Sprite bs = SpriteBank.Bed(b.CropId, b.Stage);
                if (spot.Renderer.sprite != bs)
                {
                    bool grew = spot.Renderer.sprite != null && b.Stage != CropStage.None && spot.LastKey != null && spot.LastKey.EndsWith("None");
                    spot.Renderer.sprite = bs;
                    if (animate && grew) Pop(spot.Renderer.transform, 0.5f);
                }
                spot.LastKey = b.CropId + ":" + b.Stage;
                spot.Renderer.color = _tint;
                Sprite ms = b.Stage == CropStage.Ready ? SpriteBank.MarkerReady : b.Stage == CropStage.Spoiling ? SpriteBank.MarkerSpoiling : null;
                Apply(spot.Marker, spot, ms, "m:" + ms?.name, animate, markerOnly: true);
            }
            foreach (SpriteRenderer r in _buildings) r.color = _tint;
            RefreshHandHuts(map, animate);
            RefreshPeople(map, animate);
        }

        // ---- hands' huts, building sites and people (ADR 0039 §2, §8) ----

        private Vector3 HandHutWorld(int tile) { return TileCentre(tile) + (Vector3)HandHutSpot; }

        private SpriteRenderer NewHutRenderer(string name, string sprite, int tile)
        {
            Vector3 pos = HandHutWorld(tile);
            SpriteRenderer r = NewRenderer(name, SpriteBank.Rendered(sprite) ?? SpriteBank.Hut, pos, Order(tile, _width, 12, HandHutSpot.y));
            SpriteRenderer shadow = NewRenderer(name + "Shadow", SpriteBank.ContactShadow, pos, Order(tile, _width, 11, HandHutSpot.y), r.transform);
            shadow.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            return r;
        }

        private void RefreshHandHuts(MapVm map, bool animate)
        {
            var seen = new HashSet<long>();
            foreach (BuildingVm b in map.Buildings)
            {
                if (b.DefId != "hand_hut") continue;
                seen.Add(b.Id);
                if (!_handHuts.TryGetValue(b.Id, out SpriteRenderer r))
                {
                    _handHuts[b.Id] = r = NewHutRenderer("handHut" + b.Id, "hand_hut", b.TileId);
                    if (animate) Pop(r.transform, 1f);
                }
                r.color = _tint;
            }
            foreach (long id in new List<long>(_handHuts.Keys)) if (!seen.Contains(id)) { Destroy(_handHuts[id].gameObject); _handHuts.Remove(id); }

            var sites = new HashSet<long>();
            foreach (ConstructionVm c in map.Constructions)
            {
                sites.Add(c.Id);
                if (!_sites.TryGetValue(c.Id, out SpriteRenderer r))
                {
                    _sites[c.Id] = r = NewHutRenderer("site" + c.Id, "hand_hut_building", c.TileId);
                    if (animate) Pop(r.transform, 1f);
                }
                r.color = _tint;
            }
            foreach (long id in new List<long>(_sites.Keys)) if (!sites.Contains(id)) { Destroy(_sites[id].gameObject); _sites.Remove(id); }
        }

        private static string PersonSprite(PersonVm p)
        {
            switch (p.Kind)
            {
                case PersonKind.Founder: return "person_founder";
                case PersonKind.Hand: return p.Female ? "person_hand_f" : "person_hand_m";
                default: return p.Female ? "person_labourer_f" : "person_labourer_m";
            }
        }

        private void RefreshPeople(MapVm map, bool animate)
        {
            var seen = new HashSet<string>();
            var perTile = new Dictionary<int, int>();
            foreach (PersonVm p in map.People)
            {
                if (p.TileId < 0) continue;
                seen.Add(p.Key);
                perTile.TryGetValue(p.TileId, out int n);
                perTile[p.TileId] = n + 1;
                Vector3 spot = TileCentre(p.TileId) + (Vector3)PersonSpots[n % PersonSpots.Length];
                if (!_people.TryGetValue(p.Key, out PersonView v))
                {
                    v = new PersonView { Sprite = PersonSprite(p), Phase = Random.value * 6f, Tile = p.TileId };
                    v.Body = NewRenderer("person:" + p.Key, SpriteBank.Rendered(v.Sprite + "_front"), spot, 0);
                    v.Shadow = NewRenderer("personShadow:" + p.Key, SpriteBank.ContactShadow, spot, 0);
                    v.Shadow.transform.localScale = new Vector3(0.11f, 0.11f, 1f);
                    // A day labourer walks in from the west edge of the valley; everyone else is simply there.
                    v.Pos = animate && p.Kind == PersonKind.Labourer ? TileCentre(_width * (p.TileId / _width)) + new Vector3(-1.2f, -0.6f, 0) : spot;
                    _people[p.Key] = v;
                }
                v.Target = spot;
                v.Tile = p.TileId;
                v.Working = p.Working || p.Kind != PersonKind.Founder;
                v.Leaving = false;
            }
            foreach (KeyValuePair<string, PersonView> kv in _people)
                if (!seen.Contains(kv.Key) && !kv.Value.Leaving)
                {
                    kv.Value.Leaving = true;
                    kv.Value.Target = kv.Value.Pos + new Vector3(-2.5f, -1.25f, 0); // off to the west, then gone
                }
        }

        /// <summary>People walk to their target (0.55 world units a second), bob while idle and stoop while working.</summary>
        private void AnimatePeople()
        {
            float t = Time.unscaledTime;
            var gone = new List<string>();
            foreach (KeyValuePair<string, PersonView> kv in _people)
            {
                PersonView v = kv.Value;
                Vector3 d = v.Target - v.Pos;
                bool walking = d.magnitude > 0.01f;
                if (walking)
                {
                    float step = Mathf.Min(d.magnitude, 0.55f * Time.unscaledDeltaTime);
                    v.Pos += d.normalized * step;
                    // Two facings rendered, mirrored for the other two: down-left (front), up-left (back).
                    v.Body.sprite = SpriteBank.Rendered(v.Sprite + (d.y > 0 ? "_back" : "_front"));
                    v.Body.flipX = d.x > 0; // mirrored: down-right from the front render, up-right from the back one
                }
                else if (v.Leaving) { gone.Add(kv.Key); continue; }
                else { v.Body.sprite = SpriteBank.Rendered(v.Sprite + "_front"); v.Body.flipX = false; }
                float bob = walking ? Mathf.Abs(Mathf.Sin((t + v.Phase) * 9f)) * 0.012f : Mathf.Sin((t + v.Phase) * 2.2f) * 0.003f;
                float stoop = !walking && v.Working ? 1f - 0.05f * (0.5f + 0.5f * Mathf.Sin((t + v.Phase) * 5f)) : 1f;
                v.Body.transform.position = v.Pos + new Vector3(0, bob, 0);
                v.Body.transform.localScale = new Vector3(1f, stoop, 1f);
                v.Shadow.transform.position = v.Pos;
                int order = Mathf.RoundToInt(-v.Pos.y / _grid.HalfHeight * 100f) + 60;
                v.Body.sortingOrder = order;
                v.Shadow.sortingOrder = order - 1;
                v.Body.color = _tint;
            }
            foreach (string k in gone) { Destroy(_people[k].Body.gameObject); Destroy(_people[k].Shadow.gameObject); _people.Remove(k); }
        }

        /// <summary>The placement ghost (ADR 0039 §2): the hut, see-through, green where it can go and red where it can't.</summary>
        public void ShowGhost(int tile, bool allowed)
        {
            if (_ghost == null)
            {
                _ghost = NewRenderer("ghost", SpriteBank.Rendered("hand_hut") ?? SpriteBank.Hut, Vector3.zero, 9100);
                _ghostRing = NewRenderer("ghostRing", SpriteBank.SelectionRing, Vector3.zero, 9099);
            }
            _ghost.gameObject.SetActive(true);
            _ghostRing.gameObject.SetActive(true);
            _ghost.transform.position = HandHutWorld(tile);
            _ghostRing.transform.position = TileCentre(tile);
            Color c = allowed ? new Color(0.55f, 1f, 0.55f, 0.75f) : new Color(1f, 0.45f, 0.4f, 0.7f);
            _ghost.color = c;
            _ghostRing.color = c;
        }

        public void HideGhost()
        {
            if (_ghost == null) return;
            _ghost.gameObject.SetActive(false);
            _ghostRing.gameObject.SetActive(false);
        }

        private static Color SoilColour(TileVm t)
        {
            // Clay is redder and heavier, sandy paler; low fertility fades towards a dry, tired brown.
            Color c;
            switch (t.SoilId)
            {
                case "clay": c = new Color(0.86f, 0.62f, 0.52f); break;
                case "sandy": c = new Color(1.04f, 0.92f, 0.70f); break;
                default: c = new Color(0.90f, 0.72f, 0.56f); break;
            }
            float tired = Mathf.Clamp01(1f - t.Fertility * 1.2f);
            return Color.Lerp(c, new Color(1.0f, 0.88f, 0.70f), tired * 0.5f);
        }

        private void Apply(SpriteRenderer r, Spot spot, Sprite sprite, string key, bool animate, bool markerOnly = false)
        {
            bool had = r.sprite != null;
            if (r.sprite != sprite) r.sprite = sprite;
            r.enabled = sprite != null;
            if (animate && !had && sprite != null) Pop(r.transform, markerOnly ? 1f : r.transform.localScale.x);
        }

        private void Pop(Transform t, float scale)
        {
            Vector3 target = Vector3.one * scale;
            _tweens.Run(t, 0.25f, Ease.OutBack, k => t.localScale = target * Mathf.LerpUnclamped(0.6f, 1f, k));
        }

        private void LateUpdate()
        {
            if (!_built) return;
            // Ready markers bob (±0.03 world units, 1.2 s loop), and the selection ring pulses.
            float bob = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f) * 0.03f;
            foreach (Spot s in _crops) if (s.Marker.enabled) s.Marker.transform.position = s.Home + new Vector3(0.02f, 0.34f + bob, 0);
            foreach (KeyValuePair<long, Spot> kv in _slots) if (kv.Value.Marker.enabled) kv.Value.Marker.transform.position = kv.Value.Home + new Vector3(0, 0.45f + bob, 0);
            foreach (KeyValuePair<long, Spot> kv in _beds) if (kv.Value.Marker.enabled) kv.Value.Marker.transform.position = kv.Value.Home + new Vector3(0, 0.3f + bob, 0);
            if (_ring.gameObject.activeSelf) _ring.color = new Color(1f, 1f, 1f, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f));
            AnimatePeople();
            if (_ghost != null && _ghost.gameObject.activeSelf)
                _ghost.transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(Time.unscaledTime * 6f));
        }

        // ---- picking and selection ----

        public Vector2 WorldOf(Hit hit)
        {
            switch (hit.Kind)
            {
                case HitKind.Slot: return _slotWorld[hit.Id];
                case HitKind.Bed: return _bedWorld[hit.Id];
                case HitKind.Hut: return _hutWorld;
                case HitKind.HandHut: return hit.Id > 0 ? (Vector2)_handHuts[hit.Id].transform.position : (Vector2)_sites[-hit.Id].transform.position;
                default: return TileCentre((int)hit.Id);
            }
        }

        /// <summary>What is under a world point: trees, beds, the hut, then the tile.</summary>
        public Hit Pick(Vector2 w)
        {
            if (!_built) return Hit.None;
            // A tree is hit on its canopy; an empty planting spot (only drawn on the player's land) on the ground ring and its peg.
            foreach (KeyValuePair<long, Spot> kv in _slots)
                if (kv.Value.Renderer.enabled && Vector2.Distance(w, _slotWorld[kv.Key] + new Vector2(0, kv.Value.Empty ? 0.02f : 0.17f)) < (kv.Value.Empty ? 0.11f : 0.16f))
                    return new Hit(HitKind.Slot, kv.Key);
            foreach (KeyValuePair<long, Spot> kv in _beds)
                if (Mathf.Abs(w.x - _bedWorld[kv.Key].x) / 0.17f + Mathf.Abs(w.y - _bedWorld[kv.Key].y) / 0.085f < 1f) return new Hit(HitKind.Bed, kv.Key);
            if (Vector2.Distance(w, _hutWorld + new Vector2(0, 0.12f)) < 0.24f || Vector2.Distance(w, _wellWorld + new Vector2(0, 0.08f)) < 0.13f) return new Hit(HitKind.Hut, 0);
            foreach (KeyValuePair<long, SpriteRenderer> kv in _handHuts)
                if (Vector2.Distance(w, (Vector2)kv.Value.transform.position + new Vector2(0, 0.12f)) < 0.16f) return new Hit(HitKind.HandHut, kv.Key);
            foreach (KeyValuePair<long, SpriteRenderer> kv in _sites)
                if (Vector2.Distance(w, (Vector2)kv.Value.transform.position + new Vector2(0, 0.12f)) < 0.16f) return new Hit(HitKind.HandHut, -kv.Key);
            int tile = _grid.Pick(w.x, w.y);
            return tile < 0 ? Hit.None : new Hit(HitKind.Tile, tile);
        }

        /// <summary>The ring marks the tile of the selection.</summary>
        public void Select(Hit hit, MapVm map)
        {
            int tile = -1;
            switch (hit.Kind)
            {
                case HitKind.Tile: tile = (int)hit.Id; break;
                case HitKind.Slot: tile = map.Slots.Find(s => s.Id == hit.Id).TileId; break;
                case HitKind.Bed:
                case HitKind.Hut: tile = map.Tiles.Find(t => t.Kind == TileKind.Yard).Id; break;
                case HitKind.HandHut: tile = _grid.Pick(WorldOf(hit).x, WorldOf(hit).y - HandHutSpot.y); break;
            }
            _ringTile = tile;
            _ring.gameObject.SetActive(tile >= 0);
            if (tile >= 0)
            {
                _ring.transform.position = TileCentre(tile);
                _ring.sortingOrder = Order(tile, _width, 95);
            }
        }

        public void ClearSelection() { _ringTile = -1; _ring.gameObject.SetActive(false); }

        /// <summary>The tap squash (spec 008 §3.12): 1 → 1.15 → 1, up in 0.12 s and back in 0.23 s with a little overshoot.</summary>
        public void Bounce(Hit hit)
        {
            Transform t = TransformOf(hit);
            if (t == null) return;
            Vector3 baseScale = t.localScale;
            _tweens.Run(t, 0.12f, Ease.OutCubic, k => t.localScale = baseScale * (1f + 0.15f * k),
                () => _tweens.Run(t, 0.23f, Ease.OutBack, k => t.localScale = baseScale * (1.15f - 0.15f * k)));
        }

        private Transform TransformOf(Hit hit)
        {
            switch (hit.Kind)
            {
                case HitKind.Slot: return _slots[hit.Id].Renderer.transform;
                case HitKind.Bed: return _beds[hit.Id].Renderer.transform;
                case HitKind.Hut: return _buildings.Count > 0 ? _buildings[0].transform : null;
                case HitKind.HandHut: return hit.Id > 0 ? _handHuts[hit.Id].transform : _sites[-hit.Id].transform;
                case HitKind.Tile:
                {
                    Spot crop = _crops[(int)hit.Id];
                    return crop.Renderer.enabled ? crop.Renderer.transform : _ground[(int)hit.Id].transform;
                }
                default: return null;
            }
        }

        /// <summary>A square pop on a crop or tree that was just sown or planted is done in <see cref="Refresh"/>; this is for a command's reply.</summary>
        public void Shake(int tile)
        {
            Transform t = _crops[tile].Renderer.transform;
            Vector3 home = _crops[tile].Home;
            _tweens.Run(t, 0.3f, Ease.Linear, k => t.position = home + new Vector3(Mathf.Sin(k * Mathf.PI * 6f) * 0.03f * (1f - k), 0, 0), () => t.position = home);
        }
    }
}
