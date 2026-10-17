using Sim.Core.Definitions;
using Sim.Core.Scheduling;
using Sim.Core.Time;

namespace Sim.Core.Land
{
    /// <summary>Land and soil (spec 002): the new-game layout, hourly moisture and daily fallow recovery.</summary>
    internal sealed class LandSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Agriculture; } }

        /// <summary>Lays out the map, the inherited parcel, its buildings and their tree slots.</summary>
        public static void Initialise(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            LandState land = sim.State.Land;
            long t = sim.State.Clock.SimTime;

            for (int i = 0; i < defs.Map.Tiles.Count; i++)
            {
                TileDef d = defs.Map.Tiles[i];
                land.Tiles.Add(new Tile
                {
                    Id = i, Terrain = d.Terrain, Soil = d.Soil, Drainage = d.Drainage,
                    Fertility = d.Fertility, Moisture = d.Moisture,
                    VacantSince = 0, // the land lay fallow before the game began
                });
            }
            for (int p = 0; p < defs.Map.Parcels.Count; p++)
            {
                land.Parcels.Add(new Parcel { Id = p });
                foreach (int tileId in defs.Map.Parcels[p].Tiles) land.Tiles[tileId].ParcelId = p;
            }

            Parcel home = land.Parcels[defs.Start.InheritedParcel];
            home.Protected = true;
            home.CostBasis = LandRules.ParcelPrice(sim, home);
            Acquire(sim, home, t);

            foreach (StartBuilding b in defs.Start.Buildings)
            {
                land.Buildings.Add(new Building { Id = land.NextBuildingId++, Def = b.Building, TileId = b.Tile });
                for (int k = 0; k < defs.Buildings[b.Building].YardTreeSlots; k++) AddSlot(land, SlotKind.Yard, b.Tile);
            }
            land.HourWeather = sim.State.Weather.WeatherOf(Calendar.SpellAt(t, sim.Clock));
        }

        /// <summary>Marks a parcel owned and creates the field-edge slots of its farm tiles, in tile id order.</summary>
        internal static void Acquire(Simulation sim, Parcel parcel, long t)
        {
            GameDefs defs = sim.Config.Game;
            LandState land = sim.State.Land;
            parcel.Owned = true;
            parcel.AcquiredAt = t;
            foreach (Tile tile in land.Tiles)
            {
                if (tile.ParcelId != parcel.Id || tile.Terrain != Terrain.Farm) continue;
                for (int k = 0; k < defs.Land.FieldEdgeSlotsPerAcre; k++) AddSlot(land, SlotKind.FieldEdge, tile.Id);
            }
        }

        private static void AddSlot(LandState land, SlotKind kind, int tileId)
        {
            land.Slots.Add(new TreeSlot { Id = land.NextSlotId++, Kind = kind, TileId = tileId });
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
        }
    }
}
