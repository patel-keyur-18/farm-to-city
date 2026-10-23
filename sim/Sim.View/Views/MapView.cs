using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.View.Content;

namespace Sim.View.Views
{
    public enum TileKind
    {
        Unowned = 0,
        Farm = 1,
        Yard = 2,

        /// <summary>The labour quarters: hands' huts (ADR 0040).</summary>
        Quarters = 3,

        /// <summary>The village's land (spec 010): a road acre, the village centre, the grazing commons, the river bank.</summary>
        Road = 4,
        Village = 5,
        Commons = 6,
        Bank = 7,

        /// <summary>The cattle acre (ADR 0064): the shed's place from the Hamlet.</summary>
        Cattle = 8,
    }

    /// <summary>What stands on a field (spec 008 §3.4): vacant, growing in three stages, ready, or waiting past its grace.</summary>
    public enum CropStage
    {
        None = 0,
        Growing1 = 1,
        Growing2 = 2,
        Growing3 = 3,
        Ready = 4,
        Spoiling = 5,
    }

    public sealed class TileVm
    {
        public int Id { get; internal set; }
        public int X { get; internal set; }
        public int Y { get; internal set; }
        public TileKind Kind { get; internal set; }
        public string SoilId { get; internal set; }
        public int ParcelId { get; internal set; }

        /// <summary>The parcel's id in the content (<c>w1</c>), for buying it.</summary>
        public string ParcelKey { get; internal set; }
        public bool Protected { get; internal set; }

        /// <summary>The crop's id when something grows or waits here, else null.</summary>
        public string CropId { get; internal set; }
        public CropStage Stage { get; internal set; }

        /// <summary>0..1 through the growth; 1 once ready.</summary>
        public float Progress { get; internal set; }

        /// <summary>The well acres show a bucket marker (ADR 0032).</summary>
        public bool WellAcre { get; internal set; }

        /// <summary>An outbreak was found on the growing crop and it isn't sprayed yet: the field shows a pest marker.</summary>
        public bool PestWarning { get; internal set; }

        /// <summary>The first tile of an unowned parcel next to the founder's land: it shows a "for sale" sign.</summary>
        public bool ForSaleSign { get; internal set; }

        /// <summary>Fertility 0..1, for the soil's shade of brown.</summary>
        public float Fertility { get; internal set; }

        /// <summary>A neighbour's field: the season's crop drawn half-grown, or null for fallow (spec 010 §3.7; no sim state).</summary>
        public string NeighbourCrop { get; internal set; }

        /// <summary>A road acre's links to the road acres beside it: 1 = x+1, 2 = x−1, 4 = y+1, 8 = y−1 (and off the map's edge).</summary>
        public int RoadLinks { get; internal set; }

        /// <summary>The place on a village tile (<c>house</c>, <c>temple</c>, …), else null (spec 010 §3.3).</summary>
        public string Place { get; internal set; }

        /// <summary>Which look of its place: houses come in two (by tile).</summary>
        public int Variant { get; internal set; }
    }

    public enum TreeStage
    {
        Empty = 0,
        Sapling = 1,
        Young = 2,
        Mature = 3,
        Ripe = 4,
        Spoiling = 5,
    }

    public sealed class SlotVm
    {
        public long Id { get; internal set; }
        public int TileId { get; internal set; }
        public bool Yard { get; internal set; }

        /// <summary>On the player's land: an empty slot there is drawn as a planting spot and can be tapped.</summary>
        public bool Owned { get; internal set; }

        /// <summary>Which of the tile's slots this is (0, 1, ...), so the sprites sit at different corners.</summary>
        public int Index { get; internal set; }
        public string TreeId { get; internal set; }
        public TreeStage Stage { get; internal set; }
    }

    public sealed class BedVm
    {
        public long Id { get; internal set; }
        public string CropId { get; internal set; }

        /// <summary>None when empty, Growing1..3, Ready or Spoiling (the same stages as a field).</summary>
        public CropStage Stage { get; internal set; }
        public bool Rested { get; internal set; }
    }

    public sealed class BuildingVm
    {
        public long Id { get; internal set; }
        public string DefId { get; internal set; }
        public int TileId { get; internal set; }
    }

    /// <summary>A hand's hut being built (spec 006 §3.4): drawn as its building stage on the acre's edge.</summary>
    public sealed class ConstructionVm
    {
        public long Id { get; internal set; }
        public int TileId { get; internal set; }

        /// <summary>0 at the start, 1 when done.</summary>
        public float Progress { get; internal set; }
    }

    public enum PersonKind
    {
        Founder = 0,
        Hand = 1,
        Labourer = 2,
    }

    /// <summary>
    /// Someone on the map (ADR 0039 §8; presentation only, never state): where they stand and whether they just went there to
    /// work an order (the presenter walks them over). Key is stable across refreshes ("founder", "hand:3", "labourer:5").
    /// </summary>
    public sealed class PersonVm
    {
        public string Key { get; internal set; }
        public PersonKind Kind { get; internal set; }
        public bool Female { get; internal set; }
        public int TileId { get; internal set; }

        /// <summary>A hand who carried out an order this hour: walks to the tile rather than appearing there.</summary>
        public bool Working { get; internal set; }

        /// <summary>
        /// The fields (or tree tiles) with work for them now, in the order to visit them (ADR 0040): the presenter walks them round,
        /// working a while on each. Empty: nothing to do, so they go home.
        /// </summary>
        public List<int> Patrol { get; } = new List<int>();

        /// <summary>Where home is: a tile, and for a hand the building id of their hut (else 0).</summary>
        public int HomeTile { get; internal set; }
        public long HomeHut { get; internal set; }
    }

    /// <summary>Everything the map draws, built from the state: tiles, tree slots, garden beds and buildings (spec 008 §3.4).</summary>
    public sealed class MapVm
    {
        public int Width { get; internal set; }
        public int Height { get; internal set; }
        public List<TileVm> Tiles { get; } = new List<TileVm>();
        public List<SlotVm> Slots { get; } = new List<SlotVm>();
        public List<BedVm> Beds { get; } = new List<BedVm>();
        public List<BuildingVm> Buildings { get; } = new List<BuildingVm>();
        public List<ConstructionVm> Constructions { get; } = new List<ConstructionVm>();
        public List<PersonVm> People { get; } = new List<PersonVm>();
        public long SimTime { get; internal set; }
        public int Season { get; internal set; }
        public long SecondOfDay { get; internal set; }

        /// <summary>Where the camera opens: the middle of the yard, in tile coordinates (spec 010 §3.5).</summary>
        public float HomeX { get; internal set; }
        public float HomeY { get; internal set; }

        /// <summary>
        /// The trader's truck (spec 011 §7): its road from the district road's east end up to the stop by the labour quarters (tile
        /// ids, the stop last), whether it is here, and the supermarket's van at the stop while today's orders are open.
        /// </summary>
        public List<int> TruckRoute { get; } = new List<int>();
        public bool TruckHere { get; internal set; }
        public bool VanHere { get; internal set; }
    }

    public static class MapView
    {
        /// <summary>
        /// The truck's road: a breadth-first search over road acres from the stop (the road acre beside the labour quarters) to the
        /// road's end on the map's east edge (or any edge), neighbours in a fixed order so it is the same every time.
        /// </summary>
        private static void TruckRoute(MapVm map)
        {
            int w = map.Width, h = map.Height;
            bool Road(int x, int y) { return x >= 0 && y >= 0 && x < w && y < h && map.Tiles[y * w + x].Kind == TileKind.Road; }
            int stop = -1;
            foreach (TileVm t in map.Tiles)
            {
                if (t.Kind != TileKind.Quarters) continue;
                foreach (int[] d in new[] { new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { -1, 0 } })
                    if (stop < 0 && Road(t.X + d[0], t.Y + d[1])) stop = (t.Y + d[1]) * w + t.X + d[0];
            }
            if (stop < 0) return;
            var parent = new int[w * h];
            for (int i = 0; i < parent.Length; i++) parent[i] = -2;
            var queue = new Queue<int>();
            queue.Enqueue(stop);
            parent[stop] = -1;
            int entry = -1, edge = -1;
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                int cx = c % w, cy = c / w;
                if (cx == w - 1 && entry < 0) entry = c;
                if ((cx == 0 || cy == 0 || cx == w - 1 || cy == h - 1) && edge < 0 && c != stop) edge = c;
                foreach (int[] d in new[] { new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 0 }, new[] { 0, -1 } })
                {
                    int nx = cx + d[0], ny = cy + d[1];
                    if (!Road(nx, ny) || parent[ny * w + nx] != -2) continue;
                    parent[ny * w + nx] = c;
                    queue.Enqueue(ny * w + nx);
                }
            }
            if (entry < 0) entry = edge;
            for (int c = entry; c >= 0; c = parent[c]) map.TruckRoute.Add(c);
        }

        public static MapVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            LandState land = sim.State.Land;
            long now = ctx.Now;
            long grace = defs.Farm.Farm.ReadyGraceSeconds;
            var map = new MapVm
            {
                Width = defs.Map.Width, Height = defs.Map.Height, SimTime = now,
                Season = (int)Sim.Core.Time.Calendar.SeasonAt(now, sim.Clock), SecondOfDay = now % 86400,
            };

            var places = new Dictionary<int, string>();
            foreach (PlaceDef p in defs.Map.Places) places[p.Tile] = p.Kind;
            int yards = 0;
            foreach (Tile t in land.Tiles)
            {
                Parcel parcel = land.Parcels[t.ParcelId];
                ParcelDef pdef = defs.Map.Parcels[t.ParcelId];
                var vm = new TileVm
                {
                    Id = t.Id, X = t.Id % defs.Map.Width, Y = t.Id / defs.Map.Width,
                    Kind = Kind(t.Terrain, parcel.Owned),
                    SoilId = defs.Soils[t.Soil].Id, ParcelId = t.ParcelId, ParcelKey = defs.Map.Parcels[t.ParcelId].Id, Protected = parcel.Protected,
                    WellAcre = LandSystem.IsWellTile(defs, t.Id), Fertility = t.Fertility.Raw / (100f * (1L << Sim.Core.Maths.Fixed.FractionBits)),
                };
                // Every neighbour's parcel for sale shows its sign (spec 010 §3.7); a parcel not for sale grows the season's crop.
                if (!parcel.Owned && pdef.ForSale && pdef.Tiles[0] == t.Id) vm.ForSaleSign = true;
                if (vm.Kind == TileKind.Unowned) vm.NeighbourCrop = NeighbourCrop(defs.Soils[t.Soil].Id, t.ParcelId, map.Season);
                if (vm.Kind == TileKind.Road) vm.RoadLinks = RoadLinks(land, defs.Map, t.Id);
                if (places.TryGetValue(t.Id, out string place)) { vm.Place = place; vm.Variant = (vm.X + vm.Y) % 2; }
                if (vm.Kind == TileKind.Yard) { map.HomeX += vm.X; map.HomeY += vm.Y; yards++; }
                Crop crop = sim.State.Farm.CropOn(t.Id);
                if (crop != null)
                {
                    vm.PestWarning = crop.State == CropState.Growing && crop.Outbreak != Outbreak.None && crop.Spray == SprayState.None;
                    vm.CropId = defs.Farm.Crops[crop.Def].Id;
                    long span = crop.ReadyAt - crop.SownAt;
                    vm.Progress = crop.State == CropState.Ready || span <= 0 ? 1f : Clamp01((now - crop.SownAt) / (float)span);
                    vm.Stage = crop.State == CropState.Ready ? (now > crop.ReadyAt + grace ? CropStage.Spoiling : CropStage.Ready) : GrowingStage(vm.Progress);
                }
                map.Tiles.Add(vm);
            }

            if (defs.Market.Truck != null)
            {
                TruckRoute(map);
                map.TruckHere = sim.State.Market.TruckHere;
                map.VanHere = sim.State.Market.SupermarketOrders.Count > 0 && Sim.Core.Market.Supermarket.DeliveryOpen(sim);
            }

            var perTile = new Dictionary<int, int>();
            foreach (TreeSlot s in land.Slots)
            {
                perTile.TryGetValue(s.TileId, out int index);
                perTile[s.TileId] = index + 1;
                var vm = new SlotVm { Id = s.Id, TileId = s.TileId, Yard = s.Kind == SlotKind.Yard, Index = index, Stage = TreeStage.Empty,
                    Owned = land.Parcels[land.Tiles[s.TileId].ParcelId].Owned };
                Tree tree = sim.State.Farm.TreeIn(s.Id);
                if (tree != null)
                {
                    TreeDef def = defs.Farm.Trees[tree.Def];
                    vm.TreeId = def.Id;
                    long matures = tree.PlantedAt + def.GrowthSeconds;
                    vm.Stage = tree.State == TreeState.Growing ? (now - tree.PlantedAt < def.GrowthSeconds / 2 ? TreeStage.Sapling : TreeStage.Young)
                        : tree.RipeGrams > 0 ? (now > tree.RipeAt + grace ? TreeStage.Spoiling : TreeStage.Ripe) : TreeStage.Mature;
                    if (matures < 0) vm.Stage = TreeStage.Mature;
                }
                map.Slots.Add(vm);
            }

            foreach (GardenBed b in sim.State.Farm.Beds)
            {
                var vm = new BedVm { Id = b.Id, Stage = CropStage.None };
                if (b.State != BedState.Empty)
                {
                    GardenCropDef crop = defs.Farm.GardenCrops[b.Crop];
                    vm.CropId = crop.Id;
                    long span = crop.GrowthMinutes * 60L;
                    long started = b.ReadyAt - span;
                    vm.Stage = b.State == BedState.Ready ? (now > b.ReadyAt + grace ? CropStage.Spoiling : CropStage.Ready)
                        : GrowingStage(Clamp01((now - started) / (float)span));
                }
                vm.Rested = b.State == BedState.Empty && b.RemainingGrams <= 0;
                map.Beds.Add(vm);
            }

            if (yards > 0) { map.HomeX /= yards; map.HomeY /= yards; }

            foreach (Building b in land.Buildings)
                map.Buildings.Add(new BuildingVm { Id = b.Id, DefId = defs.Buildings[b.Def].Id, TileId = b.TileId });
            AddPeople(ctx, map);
            return map;
        }

        private static TileKind Kind(Terrain t, bool owned)
        {
            switch (t)
            {
                case Terrain.Road: return TileKind.Road;
                case Terrain.Village: return TileKind.Village;
                case Terrain.Commons: return TileKind.Commons;
                case Terrain.Bank: return TileKind.Bank;
            }
            if (!owned) return TileKind.Unowned;
            return t == Terrain.Yard ? TileKind.Yard : t == Terrain.Quarters ? TileKind.Quarters : t == Terrain.Cattle ? TileKind.Cattle : TileKind.Farm;
        }

        /// <summary>
        /// What a neighbour grows this season (spec 010 §3.7), stable per parcel, null for fallow: Spring potato, radish or fallow;
        /// the dry Summer mostly fallow, some lentils; the Monsoon mostly rice (sandy land and a third lie fallow); Winter wheat or potato.
        /// </summary>
        public static string NeighbourCrop(string soil, int parcel, int season)
        {
            int k = ((parcel * 7 + season * 3) % 3 + 3) % 3;
            switch (season)
            {
                case 0: return k == 0 ? "potato" : k == 1 ? "radish" : null;
                case 1: return k == 0 ? "lentils" : null;
                case 2: return soil == "sandy" || k == 2 ? null : "rice";
                default: return k == 1 ? "potato" : "wheat";
            }
        }

        /// <summary>A road acre's links (1 = x+1, 2 = x−1, 4 = y+1, 8 = y−1): to road acres beside it, and on through the map's edge.</summary>
        private static int RoadLinks(LandState land, MapDef map, int id)
        {
            int x = id % map.Width, y = id / map.Width, links = 0;
            if (x + 1 >= map.Width || land.Tiles[id + 1].Terrain == Terrain.Road) links |= 1;
            if (x - 1 < 0 || land.Tiles[id - 1].Terrain == Terrain.Road) links |= 2;
            if (y + 1 < map.Height && land.Tiles[id + map.Width].Terrain == Terrain.Road) links |= 4;
            if (y - 1 >= 0 && land.Tiles[id - map.Width].Terrain == Terrain.Road) links |= 8;
            return links;
        }

        /// <summary>
        /// Constructions and people (ADR 0039 §8): the founder on the first growing field (else the yard), each hand on the target of
        /// their last order if it was this hour (else another growing field), and today's day labourers spread over the growing fields.
        /// </summary>
        private static void AddPeople(ViewContext ctx, MapVm map)
        {
            Simulation sim = ctx.Sim;
            Sim.Core.People.PeopleState p = sim.State.People;
            var growing = new List<int>();
            foreach (Crop c in sim.State.Farm.Crops) if (c.State == CropState.Growing) growing.Add(c.TileId);
            growing.Sort();
            int yard = -1, frontYard = -1;
            foreach (TileVm t in map.Tiles)
                if (t.Kind == TileKind.Yard) { if (yard < 0) yard = t.Id; else frontYard = t.Id; }
            if (frontYard < 0) frontYard = yard;

            // Work given to a hand is theirs: the founder (and day labourers, when there's other work) keep off those fields (owner,
            // 2026-10-19). In the sim the Care pool is still shared; this is who walks where.
            var free = new List<int>(growing);
            if (p != null) foreach (Sim.Core.People.StandingOrder o in p.Orders) if (o.Kind == Sim.Core.People.OrderTargetKind.Field) free.Remove((int)o.Target);

            // The founder tends the growing fields nobody else looks after; home is the house door.
            var founder = new PersonVm { Key = "founder", Kind = PersonKind.Founder, TileId = free.Count > 0 ? free[0] : yard, HomeTile = yard };
            founder.Patrol.AddRange(free);
            map.People.Add(founder);
            if (p == null) return;
            foreach (Sim.Core.People.Construction c in p.Constructions)
            {
                long total = System.Math.Max(1, sim.Config.Game.Buildings[sim.Config.Game.People.HandHut].BuildHours * 3600L);
                float progress = 1f - (c.ReadyAt - ctx.Now) / (float)total;
                map.Constructions.Add(new ConstructionVm { Id = c.Id, TileId = c.TileId, Progress = progress < 0 ? 0 : progress > 1 ? 1 : progress });
            }
            int k = 1;
            foreach (Sim.Core.People.Hand h in p.Hands)
            {
                // A hand works their own orders: fields with a crop or due a sowing, trees with fruit; nothing to do means home.
                Sim.Core.People.StandingOrder last = null;
                var patrol = new List<int>();
                foreach (Sim.Core.People.StandingOrder o in p.Orders)
                {
                    if (o.HandId != h.Id) continue;
                    if (o.LastAction != Sim.Core.People.OrderAction.None && (last == null || o.LastAt > last.LastAt)) last = o;
                    int tile;
                    if (o.Kind == Sim.Core.People.OrderTargetKind.Field)
                    {
                        tile = (int)o.Target;
                        bool work = sim.State.Farm.CropOn(tile) != null || o.Waiting == Sim.Core.People.OrderWait.None;
                        if (!work) continue;
                    }
                    else
                    {
                        Tree tree = sim.State.Farm.TreeIn(o.Target);
                        if (tree == null) continue;
                        tile = sim.State.Land.Slots.Find(x => x.Id == o.Target).TileId;
                    }
                    if (!patrol.Contains(tile)) patrol.Add(tile);
                }
                bool working = last != null && ctx.Now - last.LastAt < 3600 && h.Contract == Sim.Core.People.ContractState.Active;
                // His hours tend every growing field, so after his own he walks round the rest (ADR 0048 §2); home only when nothing
                // grows or while unpaid.
                foreach (int tile in growing) if (!patrol.Contains(tile)) patrol.Add(tile);
                if (h.Contract != Sim.Core.People.ContractState.Active) patrol.Clear();
                if (working && last.Kind == Sim.Core.People.OrderTargetKind.Field)
                {
                    // The field they just worked comes first, so they walk straight there.
                    patrol.Remove((int)last.Target);
                    patrol.Insert(0, (int)last.Target);
                }
                Building hut = sim.State.Land.Buildings.Find(b => b.Id == h.HutId);
                var vm = new PersonVm
                {
                    Key = "hand:" + h.Id, Kind = PersonKind.Hand, Female = sim.Config.Game.People.Names[h.Name].Female, Working = working,
                    HomeTile = hut != null ? hut.TileId : frontYard, HomeHut = h.HutId,
                };
                vm.Patrol.AddRange(patrol);
                vm.TileId = patrol.Count > 0 ? patrol[0] : vm.HomeTile;
                map.People.Add(vm);
                k++;
            }
            foreach (Sim.Core.People.Neighbour n in p.Roster)
            {
                if (!n.Hired) continue;
                var vm = new PersonVm { Key = "labourer:" + n.Id, Kind = PersonKind.Labourer, Female = sim.Config.Game.People.Names[n.Name].Female, HomeTile = frontYard };
                // Labourers tend the growing fields too, each starting at a different one.
                List<int> theirs = free.Count > 0 ? free : growing;
                for (int i = 0; i < theirs.Count; i++) vm.Patrol.Add(theirs[(i + k) % theirs.Count]);
                vm.TileId = vm.Patrol.Count > 0 ? vm.Patrol[0] : frontYard;
                map.People.Add(vm);
                k++;
            }
        }

        private static float Clamp01(float v) { return v < 0 ? 0 : v > 1 ? 1 : v; }

        /// <summary>Under a third, under two thirds, beyond (spec 008 §3.4).</summary>
        public static CropStage GrowingStage(float progress)
        {
            return progress < 1f / 3f ? CropStage.Growing1 : progress < 2f / 3f ? CropStage.Growing2 : CropStage.Growing3;
        }
    }

    /// <summary>The day and season colour of the map (spec 008 §3.4): the day tint at the sim hour × the season × the valley.</summary>
    public static class Tint
    {
        /// <summary>The colour every map sprite is multiplied by.</summary>
        public static Rgb Map(UiDef ui, int season, long secondOfDay)
        {
            return ui.TintAt(secondOfDay) * ui.SeasonTint[season] * ui.ValleyTint;
        }

        /// <summary>The ground tint without the time of day (for the HUD's backdrop).</summary>
        public static Rgb Ground(UiDef ui, int season) { return ui.SeasonTint[season] * ui.ValleyTint; }
    
    }
}
