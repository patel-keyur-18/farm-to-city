using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Maths;

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

        /// <summary>Index into <see cref="MapDef.Parcels"/>.</summary>
        public int ParcelId { get; internal set; }

        /// <summary>Sim time the tile became vacant, or −1 while a crop occupies it.</summary>
        public long VacantSince { get; internal set; }

        internal readonly Fixed[] Changes = new Fixed[ReasonCount];

        /// <summary>This season's applied fertility change for a reason (Why?).</summary>
        public Fixed FertilityChange(FertilityReason reason) { return Changes[(int)reason]; }

        public bool Occupied { get { return VacantSince < 0; } }

        internal Tile Clone()
        {
            var c = new Tile
            {
                Id = Id, Terrain = Terrain, Soil = Soil, Drainage = Drainage, Fertility = Fertility,
                Moisture = Moisture, ParcelId = ParcelId, VacantSince = VacantSince,
            };
            System.Array.Copy(Changes, c.Changes, ReasonCount);
            return c;
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteInt(Id);
            h.WriteInt((int)Terrain);
            h.WriteInt(Soil);
            h.WriteInt(Drainage);
            h.WriteFixed(Fertility);
            h.WriteFixed(Moisture);
            h.WriteInt(ParcelId);
            h.WriteLong(VacantSince);
            foreach (Fixed f in Changes) h.WriteFixed(f);
        }
    }

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

        internal Parcel Clone()
        {
            return new Parcel { Id = Id, Owned = Owned, Protected = Protected, CostBasis = CostBasis, AcquiredAt = AcquiredAt };
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteInt(Id);
            h.WriteBool(Owned);
            h.WriteBool(Protected);
            h.WriteLong(CostBasis);
            h.WriteLong(AcquiredAt);
        }
    }

    public sealed class Building
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="GameDefs.Buildings"/>.</summary>
        public int Def { get; internal set; }
        public int TileId { get; internal set; }

        internal Building Clone() { return new Building { Id = Id, Def = Def, TileId = TileId }; }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(Id);
            h.WriteInt(Def);
            h.WriteInt(TileId);
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

        /// <summary>Only mature trees cast shade (spec 002 §3.4).</summary>
        public bool TreeMature { get; internal set; }

        internal TreeSlot Clone()
        {
            return new TreeSlot { Id = Id, Kind = Kind, TileId = TileId, TreeId = TreeId, TreeMature = TreeMature };
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(Id);
            h.WriteInt((int)Kind);
            h.WriteInt(TileId);
            h.WriteLong(TreeId);
            h.WriteBool(TreeMature);
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

        internal void WriteTo(StateHasher h)
        {
            h.WriteInt(HourWeather);
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
    }
}
