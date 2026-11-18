using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Land
{
    /// <summary>Why a tile's fertility changed (spec 002 §3.3), in the order they're totalled and hashed.</summary>
    public enum FertilityReason
    {
        CropUse = 0,
        Fertiliser = 1,
        Compost = 2,
        Lentils = 3,
        Fallow = 4,
    }

    public enum SlotKind
    {
        Yard = 0,
        FieldEdge = 1,
    }

    /// <summary>One acre (spec 002 §4).</summary>
    public sealed class Tile
    {
        public const int ReasonCount = 5;

        public int Id { get; internal set; }
        public Terrain Terrain { get; internal set; }

        /// <summary>Index into <see cref="GameDefs.Soils"/>.</summary>
        public int Soil { get; internal set; }

        /// <summary>Index into <see cref="GameDefs.Drainages"/>.</summary>
        public int Drainage { get; internal set; }

        public Fixed Fertility { get; internal set; }
        public Fixed Moisture { get; internal set; }

        /// <summary>The moisture the well's floor added at the last hourly pass (spec 003 §3.6 bucket labour); 0 off the well acres.</summary>
        public Fixed WellLift { get; internal set; }

        /// <summary>Index into <see cref="MapDef.Parcels"/>.</summary>
        public int ParcelId { get; internal set; }

        /// <summary>Sim time the tile became vacant, or −1 while a crop occupies it.</summary>
        public long VacantSince { get; internal set; }

        internal readonly Fixed[] Changes = new Fixed[ReasonCount];

        /// <summary>This season's seconds with a growing crop on it below the crop's moisture minimum (spec 018 §3.4's dry field); schema 15.</summary>
        public long DrySeconds { get; internal set; }

        /// <summary>This season's applied fertility change for a reason (Why?).</summary>
        public Fixed FertilityChange(FertilityReason reason) { return Changes[(int)reason]; }

        public bool Occupied { get { return VacantSince < 0; } }

        internal Tile Clone()
        {
            var c = new Tile
            {
                Id = Id, Terrain = Terrain, Soil = Soil, Drainage = Drainage, Fertility = Fertility,
                Moisture = Moisture, WellLift = WellLift, ParcelId = ParcelId, VacantSince = VacantSince, DrySeconds = DrySeconds,
            };
            System.Array.Copy(Changes, c.Changes, ReasonCount);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Id);
            h.WriteInt((int)Terrain);
            h.WriteRef(ContentTable.Soil, Soil);
            h.WriteRef(ContentTable.Drainage, Drainage);
            h.WriteFixed(Fertility);
            h.WriteFixed(Moisture);
            h.WriteFixed(WellLift);
            h.WriteRef(ContentTable.Parcel, ParcelId);
            h.WriteLong(VacantSince);
            foreach (Fixed f in Changes) h.WriteFixed(f);
            if (h.Hashing ? DrySeconds != 0 : !(h is SaveWriter w && w.Schema < 15)) h.WriteLong(DrySeconds); // schema 15; hashed once dry
        }

        internal static Tile ReadFrom(SaveReader r)
        {
            var t = new Tile
            {
                Id = r.ReadTile(), Terrain = (Terrain)r.ReadInt(), Soil = r.ReadRef(ContentTable.Soil), Drainage = r.ReadRef(ContentTable.Drainage),
                Fertility = r.ReadFixed(), Moisture = r.ReadFixed(), WellLift = r.ReadFixed(), ParcelId = r.ReadRef(ContentTable.Parcel),
                VacantSince = r.ReadLong(),
            };
            for (int i = 0; i < ReasonCount; i++) t.Changes[i] = r.ReadFixed();
            if (r.Schema >= 15) t.DrySeconds = r.ReadLong();
            return t;
        }
    }

    /// <summary>A lease out (spec 024 §3.6): starts at the next close, rent upfront at each close, ends at a close.</summary>
    public enum LeaseStatus { None = 0, Starting = 1, Active = 2, Ending = 3 }

    public sealed class Parcel
    {
        /// <summary>Index into <see cref="MapDef.Parcels"/>.</summary>
        public int Id { get; internal set; }
        public bool Owned { get; internal set; }

        /// <summary>The inherited parcel: never sold, never seized (ADR 0009).</summary>
        public bool Protected { get; internal set; }

        /// <summary>Money: the price paid, or the opening value of the inherited parcel. 0 while unowned.</summary>
        public long CostBasis { get; internal set; }
        public long AcquiredAt { get; internal set; }

        /// <summary>A slow sale (spec 024 §3.4): when it completes (0 = not listed), the price fixed at listing, and its job. A listed acre is locked.</summary>
        public long SlowSaleEnds { get; internal set; }
        public long SlowSalePrice { get; internal set; }
        public long SlowSaleJobId { get; internal set; }

        /// <summary>The zone you set (spec 024 §3.3): <see cref="Zone"/> as an int; Agricultural (0) is the default and stays farmland.</summary>
        public int Zone { get; internal set; }

        /// <summary>Bought by a builder (spec 024 §3.3): no longer for sale, built over with the builder's homes; never yours again.</summary>
        public bool BuilderOwned { get; internal set; }

        /// <summary>Leased out to a neighbour farmer (spec 024 §3.6): a <see cref="LeaseStatus"/> as an int.</summary>
        public int Lease { get; internal set; }

        /// <summary>Listed for sale: no sowing, building or second listing until it sells or is taken off.</summary>
        public bool Listed { get { return SlowSaleEnds != 0; } }

        /// <summary>Leased, or about to be: no farming, building, zoning or selling.</summary>
        public bool Leased { get { return Lease != (int)LeaseStatus.None; } }

        /// <summary>Listed or leased: the acre is locked.</summary>
        public bool Locked { get { return Listed || Leased; } }

        /// <summary>Why a locked acre can't be used (None if it isn't locked).</summary>
        public Commands.RejectReason LockReason { get { return Listed ? Commands.RejectReason.Listed : Leased ? Commands.RejectReason.Leased : Commands.RejectReason.None; } }

        internal Parcel Clone()
        {
            return new Parcel { Id = Id, Owned = Owned, Protected = Protected, CostBasis = CostBasis, AcquiredAt = AcquiredAt, SlowSaleEnds = SlowSaleEnds, SlowSalePrice = SlowSalePrice, SlowSaleJobId = SlowSaleJobId, Zone = Zone, BuilderOwned = BuilderOwned, Lease = Lease };
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteRef(ContentTable.Parcel, Id);
            h.WriteBool(Owned);
            h.WriteBool(Protected);
            h.WriteLong(CostBasis);
            h.WriteLong(AcquiredAt);
            if (h.Hashing ? SlowSaleEnds != 0 : !(h is SaveWriter w && w.Schema < 20)) // schema 20; hashed once listed
            {
                h.WriteLong(SlowSaleEnds);
                h.WriteLong(SlowSalePrice);
                h.WriteLong(SlowSaleJobId);
            }
            if (h.Hashing ? Zone != 0 || BuilderOwned || Lease != 0 : !(h is SaveWriter z && z.Schema < 20)) // schema 20; hashed once zoned, leased or sold to a builder
            {
                h.WriteInt(Zone);
                h.WriteBool(BuilderOwned);
                h.WriteInt(Lease);
            }
        }

        internal static Parcel ReadFrom(SaveReader r)
        {
            var p = new Parcel { Id = r.ReadRef(ContentTable.Parcel), Owned = r.ReadBool(), Protected = r.ReadBool(), CostBasis = r.ReadLong(), AcquiredAt = r.ReadLong() };
            if (r.Schema >= 20) { p.SlowSaleEnds = r.ReadLong(); p.SlowSalePrice = r.ReadLong(); p.SlowSaleJobId = r.ReadLong(); p.Zone = r.ReadInt(); p.BuilderOwned = r.ReadBool(); p.Lease = r.ReadInt(); }
            return p;
        }
    }

    public sealed class Building
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="GameDefs.Buildings"/>.</summary>
        public int Def { get; internal set; }
        public int TileId { get; internal set; }

        /// <summary>Money: the book cost (the opening value for inherited buildings, spec 005 §3.1).</summary>
        public long Cost { get; internal set; }

        /// <summary>Money depreciated so far, and over how many season closes (spec 005 §3.3).</summary>
        public long Depreciated { get; internal set; }
        public int SeasonsDepreciated { get; internal set; }

        public long BookValue { get { return Cost - Depreciated; } }

        /// <summary>Whose it is (spec 024 §3.3): 0 you, 1 a builder. A builder's home is off your books (cost 0), pays you no rent and is taxed to its owner.</summary>
        public int Owner { get; internal set; }

        public bool Builders { get { return Owner == 1; } }

        internal Building Clone()
        {
            return new Building { Id = Id, Def = Def, TileId = TileId, Cost = Cost, Depreciated = Depreciated, SeasonsDepreciated = SeasonsDepreciated, Owner = Owner };
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Building, Def);
            h.WriteInt(TileId);
            h.WriteLong(Cost);
            h.WriteLong(Depreciated);
            h.WriteInt(SeasonsDepreciated);
            if (h.Hashing ? Owner != 0 : !(h is SaveWriter w && w.Schema < 20)) h.WriteInt(Owner); // schema 20; hashed once a builder's
        }

        internal static Building ReadFrom(SaveReader r)
        {
            var b = new Building
            {
                Id = r.ReadLong(), Def = r.ReadRef(ContentTable.Building), TileId = r.ReadTile(), Cost = r.ReadLong(), Depreciated = r.ReadLong(),
                SeasonsDepreciated = r.ReadInt(),
            };
            if (r.Schema >= 20) b.Owner = r.ReadInt();
            return b;
        }
    }

    /// <summary>A place for one tree (spec 002 §3.6). Spec 003 sets the tree and marks it mature.</summary>
    public sealed class TreeSlot
    {
        public long Id { get; internal set; }
        public SlotKind Kind { get; internal set; }
        public int TileId { get; internal set; }

        /// <summary>0 when empty.</summary>
        public long TreeId { get; internal set; }

        /// <summary>Only mature trees cast shade (spec 002 §3.4), each its own tree's (ADR 0037).</summary>
        public bool TreeMature { get; internal set; }
        public Fixed Shade { get; internal set; }

        internal TreeSlot Clone()
        {
            return new TreeSlot { Id = Id, Kind = Kind, TileId = TileId, TreeId = TreeId, TreeMature = TreeMature, Shade = Shade };
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt((int)Kind);
            h.WriteInt(TileId);
            h.WriteLong(TreeId);
            h.WriteBool(TreeMature);
            h.WriteFixed(Shade);
        }

        internal static TreeSlot ReadFrom(SaveReader r)
        {
            return new TreeSlot { Id = r.ReadLong(), Kind = (SlotKind)r.ReadInt(), TileId = r.ReadTile(), TreeId = r.ReadLong(), TreeMature = r.ReadBool(), Shade = r.ReadFixed() };
        }
    }

    /// <summary>The land part of the game state (spec 002 §4). Every list is kept in id order.</summary>
    public sealed class LandState
    {
        public List<Tile> Tiles { get; } = new List<Tile>();
        public List<Parcel> Parcels { get; } = new List<Parcel>();
        public List<Building> Buildings { get; } = new List<Building>();
        public List<TreeSlot> Slots { get; } = new List<TreeSlot>();

        public long NextSlotId { get; internal set; } = 1;
        public long NextBuildingId { get; internal set; } = 1;

        /// <summary>
        /// The weather index of the hour in progress, captured at each hourly pass. The next hourly pass applies
        /// it to moisture: by then the weather buffer may already have dropped that spell.
        /// </summary>
        public int HourWeather { get; internal set; }

        internal LandState Clone()
        {
            var c = new LandState { NextSlotId = NextSlotId, NextBuildingId = NextBuildingId, HourWeather = HourWeather };
            foreach (Tile t in Tiles) c.Tiles.Add(t.Clone());
            foreach (Parcel p in Parcels) c.Parcels.Add(p.Clone());
            foreach (Building b in Buildings) c.Buildings.Add(b.Clone());
            foreach (TreeSlot s in Slots) c.Slots.Add(s.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteRef(ContentTable.Weather, HourWeather);
            h.WriteInt(Tiles.Count);
            foreach (Tile t in Tiles) t.WriteTo(h);
            h.WriteInt(Parcels.Count);
            foreach (Parcel p in Parcels) p.WriteTo(h);
            h.WriteLong(NextBuildingId);
            h.WriteInt(Buildings.Count);
            foreach (Building b in Buildings) b.WriteTo(h);
            h.WriteLong(NextSlotId);
            h.WriteInt(Slots.Count);
            foreach (TreeSlot s in Slots) s.WriteTo(h);
        }

        /// <summary>Tiles and parcels are indexed by their place on the map, so the map must have the same tiles and parcels.</summary>
        internal static LandState ReadFrom(SaveReader r, MapDef map)
        {
            var s = new LandState { HourWeather = r.ReadRef(ContentTable.Weather) };
            int n = r.ReadCount();
            // Schema 5 (spec 010 §3.4): an old save's 8 × 6 map moves into the 16 × 12 map; the new land starts from content.
            r.OldMap = r.Schema < MapMigration.Schema && n == MapMigration.OldTiles && map.Tiles.Count != n;
            if (n != map.Tiles.Count && !r.OldMap) throw new SaveContentException("the map has " + map.Tiles.Count + " tiles, the save " + n);
            var tiles = new Tile[map.Tiles.Count];
            for (int i = 0; i < n; i++)
            {
                Tile t = Tile.ReadFrom(r);
                if (r.OldMap) t.Terrain = map.Tiles[t.Id].Terrain; // the farm lane's acre becomes a road acre
                tiles[t.Id] = t;
            }
            for (int p = 0; p < map.Parcels.Count; p++)
                foreach (int i in map.Parcels[p].Tiles)
                    if (tiles[i] == null) { tiles[i] = LandSystem.NewTile(map, i); tiles[i].ParcelId = p; }
            s.Tiles.AddRange(tiles);
            n = r.ReadCount();
            // Schema 8 (ADR 0064) added the cattle acre's parcel: an older save has fewer, and the new one starts unowned.
            if (n != map.Parcels.Count && !r.OldMap && !(r.Schema < Saves.CattleAcreMigration.Schema && n < map.Parcels.Count)) throw new SaveContentException("the map has " + map.Parcels.Count + " parcels, the save " + n);
            var parcels = new Parcel[map.Parcels.Count];
            for (int i = 0; i < n; i++)
            {
                Parcel p = Parcel.ReadFrom(r);
                parcels[p.Id] = p;
            }
            for (int i = 0; i < parcels.Length; i++)
                if (parcels[i] == null) parcels[i] = new Parcel { Id = i };
            s.Parcels.AddRange(parcels);
            s.NextBuildingId = r.ReadLong();
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Buildings.Add(Building.ReadFrom(r));
            s.NextSlotId = r.ReadLong();
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Slots.Add(TreeSlot.ReadFrom(r));
            return s;
        }
    }
}
