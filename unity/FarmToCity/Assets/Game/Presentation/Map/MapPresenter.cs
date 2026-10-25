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

        /// <summary>The trader's truck (opens the Store) and the supermarket's van (opens the Orders), spec 011 §7.</summary>
        Truck = 6,
        Van = 7,
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
    public sealed partial class MapPresenter : MonoBehaviour
    {
        private const float HalfWidth = 0.5f;

        // Where things sit inside a tile, in world units from the tile centre (y up).
        private static readonly Vector2[] EdgeSlots = { new Vector2(-0.36f, -0.02f), new Vector2(0.36f, -0.02f) }; // the acre's left and right corners
        // The 2-acre yard (ADR 0040). The back acre: the house, the well beside it, two trees. The front acre, where the door faces:
        // the kitchen garden, the tulsi planter, the woodpile and two more trees (the straw lives on the cattle acre, ADR 0064). Offsets are from each tile's centre.
        private static readonly Vector2 HutSpot = new Vector2(-0.04f, 0.04f), WellSpot = new Vector2(0.35f, 0.0f);
        private static readonly int[] YardTreeTile = { 0, 0, 1, 1 }; // which yard acre each of the 4 yard tree places is drawn on
        // The house acre: its left corner, and in front of the house beside the well (never behind the roof). The front acre: its
        // left corner, and its bottom corner clear of the beds and the woodpile.
        private static readonly Vector2[] YardSlots = { new Vector2(-0.42f, -0.04f), new Vector2(0.22f, -0.15f), new Vector2(-0.40f, 0.02f), new Vector2(-0.10f, -0.21f) };
        private static readonly Vector2[] BedSpots = { new Vector2(-0.16f, 0.06f), new Vector2(0.06f, -0.05f) };
        private static readonly Vector2 TulsiSpot = new Vector2(0.10f, 0.15f), WoodpileSpot = new Vector2(0.20f, -0.14f);

        /// <summary>The two hut places of the labour quarters (ADR 0040).</summary>
        // A hand's hut is a one-room hut: drawn at 75% so it reads smaller than the founder's house, and the two places sit apart along
        // the quarters' long axis so two huts never crowd each other (owner, device run 1).
        private const float HandHutScale = 0.75f;
        private static readonly Vector2[] QuartersPlaces = { new Vector2(-0.25f, 0.03f), new Vector2(0.25f, -0.03f) };

        // Edge midpoints from a tile's centre: NE and SW edges run along the tile's x axis (fence_x), SE and NW along y (fence_y).
        private static readonly Vector2 EdgeNE = new Vector2(0.25f, 0.125f), EdgeSE = new Vector2(0.25f, -0.125f), EdgeSW = new Vector2(-0.25f, -0.125f), EdgeNW = new Vector2(-0.25f, 0.125f);

        private sealed class PersonView
        {
            public SpriteRenderer Body, Shadow;
            public string Sprite; // person_<kind>
            public string Facing = "_front";
            public Vector3 Pos, Target, Home;
            public readonly List<int> Patrol = new List<int>();
            public int PatrolIndex, PointsLeft;
            public int WorkTile = -1; // the field being worked, or −1 at home
            public float WorkStart = -1f, WorkFor;
            public bool Leaving;
            public float Phase;
        }

        private readonly Dictionary<long, SpriteRenderer> _handHuts = new Dictionary<long, SpriteRenderer>();
        private readonly Dictionary<long, int> _hutTile = new Dictionary<long, int>();
        private readonly List<SpriteRenderer> _props = new List<SpriteRenderer>();
        private readonly List<int> _yardTiles = new List<int>();
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
        private readonly Dictionary<int, SpriteRenderer> _places = new Dictionary<int, SpriteRenderer>();
        private readonly List<SpriteRenderer> _bunds = new List<SpriteRenderer>();
        private int _bundsFor = -1; // the owned acres the bunds were drawn for

        // The valley past the map (spec 010 §3.6): flat tiles sorted with the map's own (so the front ones hide its block sides),
        // the season's crop on the edge fields, hills and scrub; the camera's background is the meadow.
        private sealed class EdgeSpot
        {
            public EdgeTile Tile;
            public SpriteRenderer Ground, Crop, Decor;
        }
        private readonly List<EdgeSpot> _edge = new List<EdgeSpot>();
        private static readonly Color Meadow = new Color(138 / 255f, 179 / 255f, 71 / 255f, 1f);
        private readonly Dictionary<long, Spot> _slots = new Dictionary<long, Spot>();
        private readonly Dictionary<long, Spot> _beds = new Dictionary<long, Spot>();
        private readonly List<SpriteRenderer> _buildings = new List<SpriteRenderer>();
        private readonly Dictionary<long, Vector2> _slotWorld = new Dictionary<long, Vector2>(), _bedWorld = new Dictionary<long, Vector2>();
        private Vector2 _hutWorld, _wellWorld;
        private SpriteRenderer _ring;
        private SpriteRenderer _arrow;
        private Hit _arrowHit = Hit.None;
        private const float ArrowScale = 0.42f;
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

        private const int GroundBase = -20000;

        /// <summary>Crops grow on the land: above all of it, below everyone and everything that stands on it (owner, Sprint 10 review).</summary>
        private const int CropBase = -10000;

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
            foreach (TileVm t in map.Tiles) if (t.Kind == TileKind.Yard) _yardTiles.Add(t.Id);
            foreach (TileVm t in map.Tiles)
            {
                Vector3 c = TileCentre(t.Id);
                // The land is drawn below everything that stands on it (owner, device run): a person on a tile's back half, or at
                // the map's edge, sorts above the row behind but would otherwise go under the land of their own tile.
                _ground.Add(NewRenderer("ground" + t.Id, SpriteBank.Grass, c, GroundBase + Order(t.Id, _width)));
                _detail.Add(NewRenderer("detail" + t.Id, null, c, GroundBase + Order(t.Id, _width, 1)));
                _signs.Add(NewRenderer("sign" + t.Id, SpriteBank.Sign, c + new Vector3(0.0f, 0.06f, 0), Order(t.Id, _width, 30)));
                _signs[_signs.Count - 1].transform.localScale = Vector3.one * 0.5f;
                _signs[_signs.Count - 1].gameObject.SetActive(false);
                var crop = new Spot { Home = c, Renderer = NewRenderer("crop" + t.Id, null, c, CropBase + Order(t.Id, _width, 20)) };
                crop.Marker = NewRenderer("mark" + t.Id, null, c + new Vector3(0.02f, 0.34f, 0), Order(t.Id, _width, 90));
                _crops.Add(crop);
            }
            BuildVillage(map);
            BuildEdge();
            foreach (SlotVm s in map.Slots) NewSlot(s);
            int bedIndex = 0;
            foreach (BedVm b in map.Beds)
            {
                // Beds sit on the yard tile, beside the hut.
                int yard = _yardTiles[_yardTiles.Count - 1]; // the front yard acre
                Vector2 off = BedSpots[Mathf.Min(bedIndex++, BedSpots.Length - 1)];
                Vector3 pos = TileCentre(yard) + (Vector3)off;
                var spot = new Spot { Home = pos, Renderer = NewRenderer("bed" + b.Id, SpriteBank.Bed(null, CropStage.None), pos, Order(yard, _width, 40, off.y)) };
                spot.Renderer.transform.localScale = Vector3.one * 0.64f;
                spot.Marker = NewRenderer("bedMark" + b.Id, null, pos + new Vector3(0, 0.3f, 0), Order(yard, _width, 93, off.y)); // nearer markers in front
                _beds[b.Id] = spot;
                _bedWorld[b.Id] = pos;
            }
            foreach (BuildingVm b in map.Buildings)
            {
                // Only the house and the well are fixed here; hands' huts (already built in a loaded town) are RefreshHandHuts' job,
                // or each would be drawn twice, full size, at the well's spot.
                if (b.DefId != "hut" && b.DefId != "well") continue;
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
            BuildYardDressing(map);
            BuildCattleAcre(map);
            _ring = NewRenderer("ring", SpriteBank.SelectionRing, Vector3.zero, 9000);
            _ring.gameObject.SetActive(false);
            _arrow = NewRenderer("guideArrow", SpriteBank.GuideArrow, Vector3.zero, 9500);
            _arrow.transform.localScale = Vector3.one * ArrowScale;
            _arrow.gameObject.SetActive(false);
            _built = true;
        }

        /// <summary>A tree place's spot: at the start, and for each new field-edge place when you buy land.</summary>
        private Spot NewSlot(SlotVm s)
        {
            int i = Mathf.Min(s.Index, YardSlots.Length - 1);
            Vector2 off = s.Yard ? YardSlots[i] : EdgeSlots[Mathf.Min(s.Index, EdgeSlots.Length - 1)];
            int drawTile = s.Yard ? _yardTiles[Mathf.Min(YardTreeTile[i], _yardTiles.Count - 1)] : s.TileId;
            Vector3 pos = TileCentre(drawTile) + (Vector3)off;
            var spot = new Spot { Home = pos, Renderer = NewRenderer("slot" + s.Id, null, pos, Order(drawTile, _width, 50, off.y)) };
            spot.Renderer.transform.localScale = Vector3.one * (s.Yard ? 0.62f : 0.5f);
            spot.Marker = NewRenderer("slotMark" + s.Id, null, pos + new Vector3(0, s.Yard ? 0.5f : 0.42f, 0), Order(drawTile, _width, 92, off.y));
            _slots[s.Id] = spot;
            _slotWorld[s.Id] = pos;
            return spot;
        }

        private static int EdgeOrder(int x, int y, int offset = 0) { return (x + y) * 100 + offset; }

        private void BuildEdge()
        {
            foreach (EdgeTile e in Sim.View.Views.EdgeView.Tiles(_width, _height))
            {
                (float wx, float wy) = _grid.Centre(e.X, e.Y);
                var c = new Vector3(wx, wy, 0);
                string ground = e.Kind == EdgeKind.River ? "edge_water" : e.Kind == EdgeKind.Field ? "edge_soil" : e.Kind == EdgeKind.Road ? "edge_road" : "edge_grass";
                var v = new EdgeSpot { Tile = e, Ground = NewRenderer("edge" + e.X + "_" + e.Y, SpriteBank.Rendered(ground), c, GroundBase + EdgeOrder(e.X, e.Y)) };
                if (e.Kind == EdgeKind.Field) v.Crop = NewRenderer("edgeCrop", null, c, CropBase + EdgeOrder(e.X, e.Y, 20));
                string decor = null;
                float scale = 1f;
                if (e.Kind == EdgeKind.Hill && e.X <= -2 && ((e.Y - e.X * 2) % 4 + 4) % 4 == 0) decor = "hill_" + ((e.Y + 30) % 3);
                if (e.Kind == EdgeKind.Scrub) decor = "scrub";
                if (e.Kind == EdgeKind.Farmstead) EdgeFarmstead(e, c);
                if (e.Kind == EdgeKind.FarBank && (e.X + e.Y) % 3 == 0) { decor = "tree_mango_mature"; scale = 0.55f; }
                if (decor != null)
                {
                    v.Decor = NewRenderer("edgeDecor", SpriteBank.Rendered(decor), c, EdgeOrder(e.X, e.Y, 50));
                    v.Decor.transform.localScale = Vector3.one * scale;
                }
                _edge.Add(v);
            }
        }

        public void Refresh(MapVm map, bool animate)
        {
            if (!_built) Build(map);
            Rgb t = Tint.Map(_ui, map.Season, map.SecondOfDay);
            _tint = new Color(t.R, t.G, t.B, 1f);
            if (Camera.main != null) Camera.main.backgroundColor = _tint * Meadow; // never sky-blue past the map (spec 010 §3.6)
            foreach (EdgeSpot e in _edge)
            {
                e.Ground.color = _tint;
                if (e.Crop != null)
                {
                    string crop = Sim.View.Views.EdgeView.Crop(e.Tile.X, e.Tile.Y, map.Season);
                    e.Crop.sprite = crop == null ? null : SpriteBank.Crop(crop, CropStage.Growing2);
                    e.Ground.sprite = SpriteBank.Rendered(crop == null ? "edge_grass" : "edge_soil"); // fallow lies under grass
                    e.Crop.color = _tint;
                }
                if (e.Decor != null) e.Decor.color = _tint;
            }
            foreach (KeyValuePair<int, SpriteRenderer> p in _places) p.Value.color = _tint;
            foreach (SpriteRenderer r in _villageProps) r.color = _tint;
            RefreshBunds(map);
            RefreshValley(map);

            foreach (TileVm tv in map.Tiles)
            {
                SpriteRenderer ground = _ground[tv.Id], detail = _detail[tv.Id];
                switch (tv.Kind)
                {
                    case TileKind.Unowned:
                        // A neighbour's acre (spec 010 §3.7, ADR 0062): by its look, never greyed out.
                        NeighbourGround(tv, ground, detail);
                        break;
                    case TileKind.Road:
                        ground.sprite = SpriteBank.Rendered("ground_road_" + tv.RoadLinks) ?? SpriteBank.Rendered("ground_road_3");
                        ground.color = _tint;
                        detail.sprite = null;
                        break;
                    case TileKind.Village:
                        ground.sprite = VillageGround(tv); // worn grass, streets and yards (ADR 0061)
                        ground.color = _tint;
                        detail.sprite = null;
                        break;
                    case TileKind.Commons:
                        ground.sprite = SpriteBank.Rendered("ground_commons");
                        ground.color = _tint;
                        detail.sprite = null;
                        break;
                    case TileKind.Bank:
                        ground.sprite = SpriteBank.Rendered(tv.X == 12 ? "ground_bank_1" : "ground_bank_0"); // the ghat, up from the village
                        ground.color = _tint;
                        detail.sprite = null;
                        break;
                    case TileKind.Quarters:
                        // Beaten earth: the hands' compound.
                        ground.sprite = SpriteBank.Sand;
                        ground.color = _tint * new Color(0.95f, 0.86f, 0.70f, 1f);
                        detail.sprite = null;
                        break;
                    case TileKind.Cattle:
                        // Poor, sandy land under thin grass (ADR 0064); its pieces are MapPresenter.Cattle's.
                        ground.sprite = SpriteBank.Rendered("ground_cattle") ?? SpriteBank.Grass;
                        ground.color = _tint;
                        detail.sprite = null;
                        break;
                    case TileKind.Yard:
                        ground.sprite = SpriteBank.Grass;
                        ground.color = _tint * new Color(1.02f, 1.0f, 0.9f, 1f);
                        detail.sprite = SpriteBank.GrassDetail(tv.Id);
                        detail.color = _tint;
                        break;
                    case TileKind.Farm when tv.Houses > 0 || tv.Business:
                        // A homestead acre (spec 014): grass and beaten earth round the houses, no furrows.
                        ground.sprite = SpriteBank.Rendered("ground_village_" + ((tv.X + tv.Y) % 2)) ?? SpriteBank.Grass;
                        ground.color = _tint;
                        detail.sprite = null;
                        break;
                    default:
                        ground.sprite = SpriteBank.SoilTile;
                        ground.color = _tint * SoilColour(tv);
                        detail.sprite = null; // the furrows are modelled on the Blender soil
                        detail.color = _tint;
                        break;
                }
                _signs[tv.Id].gameObject.SetActive(tv.ForSaleSign);
                _signs[tv.Id].color = _tint;

                Spot crop = _crops[tv.Id];
                Sprite cs = tv.CropId != null ? SpriteBank.Crop(tv.CropId, tv.Stage) : tv.NeighbourCrop != null ? SpriteBank.Crop(tv.NeighbourCrop, CropStage.Growing2) : null;
                Apply(crop.Renderer, crop, cs, (tv.CropId ?? "n" + tv.NeighbourCrop) + ":" + tv.Stage, animate);
                crop.Renderer.color = _tint;
                Sprite ms = tv.Stage == CropStage.Ready ? SpriteBank.MarkerReady : tv.Stage == CropStage.Spoiling ? SpriteBank.MarkerSpoiling
                    : tv.PestWarning ? SpriteBank.MarkerPest : null;
                Apply(crop.Marker, crop, ms, "m:" + ms?.name, animate, markerOnly: true);
                crop.Marker.color = Color.white;
            }

            // Places come with land you buy and go with land you sell.
            var slotIds = new HashSet<long>();
            foreach (SlotVm s in map.Slots) slotIds.Add(s.Id);
            var goneSlots = new List<long>();
            foreach (KeyValuePair<long, Spot> kv in _slots) if (!slotIds.Contains(kv.Key)) goneSlots.Add(kv.Key);
            foreach (long id in goneSlots)
            {
                Destroy(_slots[id].Renderer.gameObject);
                Destroy(_slots[id].Marker.gameObject);
                _slots.Remove(id);
                _slotWorld.Remove(id);
            }
            foreach (SlotVm s in map.Slots)
            {
                if (!_slots.TryGetValue(s.Id, out Spot spot)) spot = NewSlot(s);
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
            foreach (SpriteRenderer r in _props) r.color = _tint;
            RefreshHandHuts(map, animate);
            RefreshHouses(map, animate);
            RefreshBusinesses(map, animate);
            RefreshPeople(map, animate);
            RefreshVehicles(map, animate);
            RefreshCattleAcre(map, animate);
        }

        // ---- hands' huts, building sites and people (ADR 0039 §2, §8) ----

        /// <summary>
        /// The yard's props and the fence (ADR 0040): a tulsi planter by the door and a woodpile on the front acre (the straw lives on the
        /// cattle acre, ADR 0064, so the yard keeps no haystack), and a
        /// low bamboo fence wherever the yard or the quarters meet land that isn't yours (open to your fields and between them).
        /// </summary>
        private void BuildYardDressing(MapVm map)
        {
            if (_yardTiles.Count == 0) return;
            int front = _yardTiles[_yardTiles.Count - 1];
            Prop("tulsi", front, TulsiSpot, 46);
            Prop("woodpile", front, WoodpileSpot, 46);
            foreach (TileVm t in map.Tiles)
            {
                if (t.Kind != TileKind.Yard && t.Kind != TileKind.Quarters) continue;
                int x = t.Id % _width, y = t.Id / _width;
                Fence(map, t.Id, x, y - 1, "fence_x", EdgeNE, 4);
                Fence(map, t.Id, x + 1, y, "fence_y", EdgeSE, 88);
                Fence(map, t.Id, x, y + 1, "fence_x", EdgeSW, 88);
                Fence(map, t.Id, x - 1, y, "fence_y", EdgeNW, 4);
            }
        }

        private static bool Yours(TileKind k) { return k == TileKind.Farm || k == TileKind.Yard || k == TileKind.Quarters || k == TileKind.Cattle; }

        private void Prop(string sprite, int tile, Vector2 off, int order)
        {
            Sprite s = SpriteBank.Rendered(sprite);
            if (s == null) return;
            Vector3 pos = TileCentre(tile) + (Vector3)off;
            _props.Add(NewRenderer(sprite, s, pos, Order(tile, _width, order, off.y)));
        }

        private void Fence(MapVm map, int tile, int nx, int ny, string sprite, Vector2 edge, int order)
        {
            bool inside = nx >= 0 && ny >= 0 && nx < _width && ny < _height;
            if (inside && map.Tiles[ny * _width + nx].Kind != TileKind.Unowned) return; // open to your own land
            Prop(sprite, tile, edge, order);
        }

        private Vector3 HandHutWorld(int tile, int place) { return TileCentre(tile) + (Vector3)QuartersPlaces[Mathf.Clamp(place, 0, QuartersPlaces.Length - 1)]; }

        private SpriteRenderer NewHutRenderer(string name, string sprite, int tile)
        {
            SpriteRenderer r = NewRenderer(name, SpriteBank.Rendered(sprite) ?? SpriteBank.Hut, TileCentre(tile), 0);
            r.transform.localScale = Vector3.one * HandHutScale;
            SpriteRenderer shadow = NewRenderer(name + "Shadow", SpriteBank.ContactShadow, TileCentre(tile), 0, r.transform);
            shadow.transform.localScale = new Vector3(0.42f, 0.42f, 1f);
            return r;
        }

        /// <summary>Puts a hut (or a building site) on its place: huts in id order first, then sites, on each quarters acre.</summary>
        private void Place(SpriteRenderer r, int tile, int place)
        {
            Vector2 off = QuartersPlaces[Mathf.Clamp(place, 0, QuartersPlaces.Length - 1)];
            r.transform.position = HandHutWorld(tile, place);
            r.sortingOrder = Order(tile, _width, 45, off.y);
            SpriteRenderer shadow = r.transform.childCount > 0 ? r.transform.GetChild(0).GetComponent<SpriteRenderer>() : null;
            if (shadow != null) shadow.sortingOrder = r.sortingOrder - 1;
        }

        private int PlacesUsed(int tile)
        {
            int n = 0;
            foreach (KeyValuePair<long, int> kv in _hutTile) if (kv.Value == tile) n++;
            return n;
        }

        /// <summary>Where a hand lives: just in front of their hut's door (the quarters' middle if the hut is gone).</summary>
        private Vector3 HutDoor(long hutId, int tile)
        {
            return _handHuts.TryGetValue(hutId, out SpriteRenderer r) ? r.transform.position + new Vector3(-0.04f, -0.09f, 0) * HandHutScale : TileCentre(tile);
        }

        private void RefreshHandHuts(MapVm map, bool animate)
        {
            var seen = new HashSet<long>();
            var used = new Dictionary<int, int>();
            foreach (BuildingVm b in map.Buildings)
            {
                if (b.DefId != "hand_hut") continue;
                seen.Add(b.Id);
                if (!_handHuts.TryGetValue(b.Id, out SpriteRenderer r))
                {
                    _handHuts[b.Id] = r = NewHutRenderer("handHut" + b.Id, "hand_hut", b.TileId);
                    if (animate) Pop(r.transform, HandHutScale);
                }
                used.TryGetValue(b.TileId, out int place);
                used[b.TileId] = place + 1;
                Place(r, b.TileId, place);
                _hutTile[b.Id] = b.TileId;
                r.color = _tint;
            }
            foreach (long id in new List<long>(_handHuts.Keys)) if (!seen.Contains(id)) { Destroy(_handHuts[id].gameObject); _handHuts.Remove(id); _hutTile.Remove(id); }

            var sites = new HashSet<long>();
            foreach (ConstructionVm c in map.Constructions)
            {
                sites.Add(c.Id);
                if (!_sites.TryGetValue(c.Id, out SpriteRenderer r))
                {
                    _sites[c.Id] = r = NewHutRenderer("site" + c.Id, "hand_hut_building", c.TileId);
                    if (animate) Pop(r.transform, HandHutScale);
                }
                used.TryGetValue(c.TileId, out int place);
                used[c.TileId] = place + 1;
                Place(r, c.TileId, place);
                _hutTile[-c.Id] = c.TileId;
                r.color = _tint;
            }
            foreach (long id in new List<long>(_sites.Keys)) if (!sites.Contains(id)) { Destroy(_sites[id].gameObject); _sites.Remove(id); _hutTile.Remove(-id); }
        }

        private static string PersonSprite(PersonVm p)
        {
            switch (p.Kind)
            {
                case PersonKind.Founder: return "person_founder";
                case PersonKind.Hand: return p.Female ? "person_hand_f" : "person_hand_m";
                case PersonKind.Citizen: case PersonKind.Customer: return "person_" + p.Look;
                default: return p.Female ? "person_labourer_f" : "person_labourer_m";
            }
        }

        private void RefreshPeople(MapVm map, bool animate)
        {
            var seen = new HashSet<string>();
            foreach (PersonVm p in map.People)
            {
                if (p.TileId < 0) continue;
                seen.Add(p.Key);
                Vector3 home = HomeOf(p);
                if (!_people.TryGetValue(p.Key, out PersonView v))
                {
                    v = new PersonView { Sprite = PersonSprite(p), Phase = Random.value * 6f };
                    Vector3 start = p.Patrol.Count > 0 ? WorkPoint(p.Patrol[0]) : home;
                    v.Body = NewRenderer("person:" + p.Key, SpriteBank.Rendered(v.Sprite + "_front"), start, 0);
                    v.Shadow = NewRenderer("personShadow:" + p.Key, SpriteBank.ContactShadow, start, 0);
                    v.Shadow.transform.localScale = new Vector3(0.11f, 0.11f, 1f);
                    // A day labourer walks in from the west edge of the valley; everyone else is simply there.
                    v.Pos = animate && p.Kind == PersonKind.Labourer ? TileCentre(_width * (p.TileId / _width)) + new Vector3(-1.2f, -0.6f, 0) : start;
                    v.Target = start;
                    if (p.Patrol.Count > 0) { v.WorkTile = p.Patrol[0]; v.PointsLeft = 2; }
                    _people[p.Key] = v;
                }
                v.Home = home;
                v.Leaving = false;
                bool changed = !SamePatrol(v.Patrol, p.Patrol);
                v.Patrol.Clear();
                v.Patrol.AddRange(p.Patrol);
                if (v.Patrol.Count == 0)
                {
                    // Nothing to do: home.
                    if (v.WorkTile >= 0 || changed) { v.WorkTile = -1; v.Target = home; v.WorkStart = -1f; }
                }
                else if ((p.Working && v.WorkTile != v.Patrol[0]) || !v.Patrol.Contains(v.WorkTile))
                {
                    // A new order (or the field they were on needs nothing now): straight to the first field with work.
                    GoWork(v, 0);
                }
            }
            foreach (KeyValuePair<string, PersonView> kv in _people)
                if (!seen.Contains(kv.Key) && !kv.Value.Leaving)
                {
                    kv.Value.Leaving = true;
                    kv.Value.WorkTile = -1;
                    kv.Value.Target = kv.Value.Pos + new Vector3(-2.5f, -1.25f, 0); // off to the west, then gone
                }
        }

        private static bool SamePatrol(List<int> a, List<int> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private Vector3 HomeOf(PersonVm p)
        {
            switch (p.Kind)
            {
                case PersonKind.Founder: return _hutWorld + new Vector2(-0.12f, -0.12f);
                case PersonKind.Hand: return _handHuts.ContainsKey(p.HomeHut) ? HutDoor(p.HomeHut, p.HomeTile) : HouseYard(p.HomeHut, p.HomeTile); // a hand whose family has a house
                case PersonKind.Citizen: return HouseYard(p.HomeHut, p.HomeTile) + new Vector3(p.Look == "boy" || p.Look == "girl" ? 0.06f : -0.03f, 0.01f, 0);
                case PersonKind.Customer: return MillQueue(p.HomeHut, p.HomeTile, p.Key[p.Key.Length - 1] - '0');
                default: return TileCentre(p.HomeTile) + new Vector3(-0.1f, -0.08f, 0);
            }
        }

        /// <summary>A spot to work inside a field, kept clear of its edges.</summary>
        private Vector3 WorkPoint(int tile)
        {
            float u = Random.Range(-0.3f, 0.3f), v = Random.Range(-0.3f, 0.3f);
            return TileCentre(tile) + new Vector3((u - v) * HalfWidth, -(u + v) * _grid.HalfHeight, 0);
        }

        private void GoWork(PersonView v, int index)
        {
            v.PatrolIndex = index % Mathf.Max(1, v.Patrol.Count);
            v.WorkTile = v.Patrol[v.PatrolIndex];
            v.Target = WorkPoint(v.WorkTile);
            v.PointsLeft = 2;
            v.WorkStart = -1f;
        }

        /// <summary>
        /// People walk (0.55 world units a second) between the fields with work, hoe a while at two spots on each (the stand and work
        /// frames alternate), then move on round their fields; with nothing to do they stand at home and breathe (ADR 0040).
        /// </summary>
        private void AnimatePeople()
        {
            float t = Time.unscaledTime;
            var gone = new List<string>();
            foreach (KeyValuePair<string, PersonView> kv in _people)
            {
                PersonView v = kv.Value;
                Vector3 d = v.Target - v.Pos;
                bool walking = d.magnitude > 0.01f;
                bool hoeing = false;
                if (walking)
                {
                    float step = Mathf.Min(d.magnitude, 0.55f * Time.unscaledDeltaTime);
                    v.Pos += d.normalized * step;
                    // Two facings rendered, mirrored for the other two: down-left (front), up-left (back).
                    v.Facing = d.y > 0 ? "_back" : "_front";
                    v.Body.flipX = d.x > 0;
                }
                else if (v.Leaving) { gone.Add(kv.Key); continue; }
                else if (v.WorkTile >= 0)
                {
                    if (v.WorkStart < 0f) { v.WorkStart = t; v.WorkFor = Random.Range(4f, 8f); v.Facing = Random.value < 0.5f ? "_front" : "_back"; v.Body.flipX = Random.value < 0.5f; }
                    hoeing = true;
                    if (t - v.WorkStart > v.WorkFor)
                    {
                        if (--v.PointsLeft > 0) { v.Target = WorkPoint(v.WorkTile); v.WorkStart = -1f; }
                        else GoWork(v, v.PatrolIndex + 1);
                    }
                }
                else { v.Facing = "_front"; v.Body.flipX = false; }

                // Walking: four frames at 8 a second (ADR 0041 §8). Hoeing: the stand and work frames alternate, a stroke every 0.7 s.
                bool down = hoeing && Mathf.Repeat(t + v.Phase, 0.7f) < 0.35f;
                string frame = walking ? "_walk" + (int)Mathf.Repeat((t + v.Phase) * 8f, 4f) : down ? "_work" : "";
                v.Body.sprite = SpriteBank.Rendered(v.Sprite + v.Facing + frame) ?? SpriteBank.Rendered(v.Sprite + v.Facing);
                float bob = walking ? Mathf.Abs(Mathf.Sin((t + v.Phase) * 12.6f)) * 0.004f : hoeing ? 0f : Mathf.Sin((t + v.Phase) * 2.2f) * 0.003f;
                v.Body.transform.position = v.Pos + new Vector3(0, bob, 0);
                v.Shadow.transform.position = v.Pos;
                int order = Mathf.RoundToInt(-v.Pos.y / _grid.HalfHeight * 100f) + 60;
                v.Body.sortingOrder = order;
                v.Shadow.sortingOrder = order - 1;
                v.Body.color = _tint;
            }
            foreach (string k in gone) { Destroy(_people[k].Body.gameObject); Destroy(_people[k].Shadow.gameObject); _people.Remove(k); }
        }

        /// <summary>The placement ghost (ADR 0040): the hut, see-through, green where it can go and red where it can't.</summary>
        public void ShowGhost(int tile, bool allowed)
        {
            int place = PlacesUsed(tile);
            PlaceGhost(SpriteBank.Rendered("hand_hut") ?? SpriteBank.Hut, HandHutScale, HandHutWorld(tile, place < QuartersPlaces.Length ? place : 0), tile, allowed);
        }

        /// <summary>A family house's ghost (spec 014 §3.13) on the acre's next plot (<paramref name="n"/> houses there already), or the quarters' next place.</summary>
        public void ShowHouseGhost(int tile, bool allowed, int n, bool quarters)
        {
            Vector3 pos = quarters ? HandHutWorld(tile, PlacesUsed(tile) + n) : OnTile(tile, HousePlots[Mathf.Clamp(n, 0, HousePlots.Length - 1)].At);
            PlaceGhost(SpriteBank.Rendered("family_house_s") ?? SpriteBank.Hut, 1f, pos, tile, allowed);
        }

        /// <summary>A business's ghost (spec 016 §3.3) where it will stand on the acre.</summary>
        public void ShowBusinessGhost(int tile, bool allowed, string typeId)
        {
            PlaceGhost(SpriteBank.Rendered(typeId + "_s") ?? SpriteBank.Hut, 1f, OnTile(tile, MillAt), tile, allowed);
        }

        private void PlaceGhost(Sprite sprite, float scale, Vector3 pos, int tile, bool allowed)
        {
            if (_ghost == null)
            {
                _ghost = NewRenderer("ghost", sprite, Vector3.zero, 9100);
                _ghostRing = NewRenderer("ghostRing", SpriteBank.SelectionRing, Vector3.zero, 9099);
            }
            _ghost.sprite = sprite;
            _ghost.transform.localScale = Vector3.one * scale;
            _ghost.gameObject.SetActive(true);
            _ghostRing.gameObject.SetActive(true);
            _ghost.transform.position = pos;
            _ghostRing.transform.position = TileCentre(tile);
            Color c = allowed ? new Color(0.55f, 1f, 0.55f, 0.75f) : new Color(1f, 0.45f, 0.4f, 0.7f);
            _ghost.color = c;
            _ghostRing.color = c;
        }

        private readonly List<SpriteRenderer> _lit = new List<SpriteRenderer>();

        /// <summary>While placing: a soft green ring on every acre the building may go on (spec 014 §3.13).</summary>
        public void LightPlaces(IEnumerable<int> tiles)
        {
            ClearLit();
            foreach (int t in tiles)
            {
                SpriteRenderer r = NewRenderer("lit" + t, SpriteBank.TileFill, TileCentre(t), 9098);
                r.color = new Color(0.55f, 1f, 0.45f, 0.8f);
                _lit.Add(r);
            }
        }

        private void ClearLit()
        {
            foreach (SpriteRenderer r in _lit) Destroy(r.gameObject);
            _lit.Clear();
        }

        public void HideGhost()
        {
            ClearLit();
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

        /// <summary>
        /// A marker stands just on top of what it marks (a bed, a field's plants, a tree's crown): the rendered bubble's pivot is
        /// the foot of its stand, so it goes at the top of the marked sprite, whatever its size or stage.
        /// </summary>
        private static Vector3 MarkerAt(Spot s, float dx, float bob)
        {
            float top = s.Renderer.sprite != null ? s.Renderer.bounds.max.y - 0.03f : s.Home.y + 0.1f;
            return new Vector3(s.Home.x + dx, top + bob, s.Home.z);
        }

        private void LateUpdate()
        {
            if (!_built) return;
            // Ready markers bob (±0.03 world units, 1.2 s loop), and the selection ring pulses.
            float bob = Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.2f) * 0.03f;
            foreach (Spot s in _crops) if (s.Marker.enabled) s.Marker.transform.position = MarkerAt(s, 0.02f, bob);
            foreach (KeyValuePair<long, Spot> kv in _slots) if (kv.Value.Marker.enabled) kv.Value.Marker.transform.position = MarkerAt(kv.Value, 0f, bob);
            foreach (KeyValuePair<long, Spot> kv in _beds) if (kv.Value.Marker.enabled) kv.Value.Marker.transform.position = MarkerAt(kv.Value, 0f, bob);
            if (_ring.gameObject.activeSelf) _ring.color = new Color(1f, 1f, 1f, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 5f));
            if (_arrow.gameObject.activeSelf) _arrow.transform.position = ArrowAt(_arrowHit) + new Vector3(0, 0.05f + 0.05f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / 1.1f), 0);
            AnimatePeople();
            AnimateVehicles();
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
                case HitKind.Truck: return _truck.transform.position;
                case HitKind.Van: return VanWorld;
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
            Hit vehicle = PickVehicle(w);
            if (vehicle.Kind != HitKind.None) return vehicle;
            if (Vector2.Distance(w, _hutWorld + new Vector2(0, 0.12f)) < 0.24f || Vector2.Distance(w, _wellWorld + new Vector2(0, 0.08f)) < 0.13f) return new Hit(HitKind.Hut, 0);
            foreach (KeyValuePair<long, SpriteRenderer> kv in _handHuts)
                if (Vector2.Distance(w, (Vector2)kv.Value.transform.position + new Vector2(0, 0.12f * HandHutScale)) < 0.16f * HandHutScale) return new Hit(HitKind.HandHut, kv.Key);
            foreach (KeyValuePair<long, SpriteRenderer> kv in _sites)
                if (Vector2.Distance(w, (Vector2)kv.Value.transform.position + new Vector2(0, 0.12f * HandHutScale)) < 0.16f * HandHutScale) return new Hit(HitKind.HandHut, -kv.Key);
            // A village building is hit on its walls and roof, which stand over the acre behind it (spec 010 §3.3): the nearest wins.
            int place = -1;
            float best = 0.24f;
            foreach (KeyValuePair<int, SpriteRenderer> kv in _places)
            {
                float d = Vector2.Distance(w, (Vector2)kv.Value.transform.position + new Vector2(0, 0.12f)); // where it stands (ADR 0061)
                if (d < best) { best = d; place = kv.Key; }
            }
            if (place >= 0) return new Hit(HitKind.Tile, place);
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
                case HitKind.HandHut: tile = _hutTile.TryGetValue(hit.Id, out int t) ? t : -1; break;
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

        /// <summary>
        /// The first session's highlight (spec 009 §7): the target bounces once and a gold arrow bobs over it until the step is
        /// done. Hit.None hides it.
        /// </summary>
        public void Guide(Hit hit)
        {
            if (!_built || (hit.Kind == _arrowHit.Kind && hit.Id == _arrowHit.Id)) return;
            _arrowHit = hit;
            _arrow.gameObject.SetActive(hit.Kind != HitKind.None);
            if (hit.Kind == HitKind.None) return;
            _arrow.transform.position = ArrowAt(hit);
            Bounce(hit);
            Pop(_arrow.transform, ArrowScale);
        }

        /// <summary>Just above what the arrow points at: a field's middle, a bed, the hut's roof.</summary>
        private Vector3 ArrowAt(Hit hit)
        {
            Vector2 w = WorldOf(hit);
            float up = hit.Kind == HitKind.Hut ? 0.42f : hit.Kind == HitKind.Bed ? 0.3f : 0.2f; // above a ready marker
            return new Vector3(w.x, w.y + up, 0);
        }

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
                case HitKind.Truck: return _truck.transform;
                case HitKind.Van: return _van.transform;
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
