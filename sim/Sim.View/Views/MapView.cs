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
    }

    public static class MapView
    {
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

            foreach (Tile t in land.Tiles)
            {
                Parcel parcel = land.Parcels[t.ParcelId];
                var vm = new TileVm
                {
                    Id = t.Id, X = t.Id % defs.Map.Width, Y = t.Id / defs.Map.Width,
                    Kind = !parcel.Owned ? TileKind.Unowned : t.Terrain == Terrain.Yard ? TileKind.Yard : t.Terrain == Terrain.Quarters ? TileKind.Quarters : TileKind.Farm,
                    SoilId = defs.Soils[t.Soil].Id, ParcelId = t.ParcelId, ParcelKey = defs.Map.Parcels[t.ParcelId].Id, Protected = parcel.Protected,
                    WellAcre = LandSystem.IsWellTile(defs, t.Id), Fertility = t.Fertility.Raw / (100f * (1L << Sim.Core.Maths.Fixed.FractionBits)),
                };
                if (!parcel.Owned && defs.Map.Parcels[t.ParcelId].Tiles[0] == t.Id && LandSystem.IsAdjacentToOwned(sim, t.ParcelId)) vm.ForSaleSign = true;
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

            foreach (Building b in land.Buildings)
                map.Buildings.Add(new BuildingVm { Id = b.Id, DefId = defs.Buildings[b.Def].Id, TileId = b.TileId });
            AddPeople(ctx, map);
            return map;
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
                if (!n.HiredToday) continue;
                var vm = new PersonVm { Key = "labourer:" + n.Id, Kind = PersonKind.Labourer, Female = sim.Config.Game.People.Names[n.Name].Female, HomeTile = frontYard };
                // Day labourers tend the growing fields too, each starting at a different one.
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
