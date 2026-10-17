using Sim.Core.Definitions;
using Sim.Core.Maths;
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
            LandState land = sim.State.Land;
            long t = sim.State.Clock.SimTime;
            switch (pass)
            {
                case ItemClass.Hourly:
                    foreach (Tile tile in land.Tiles) UpdateMoisture(sim, tile, land.HourWeather);
                    land.HourWeather = sim.State.Weather.WeatherOf(Calendar.SpellAt(t, sim.Clock));
                    break;
                case ItemClass.Daily:
                    foreach (Tile tile in land.Tiles) RecoverFallow(sim, tile, t);
                    break;
                case ItemClass.Seasonal:
                    foreach (Tile tile in land.Tiles) System.Array.Clear(tile.Changes, 0, Tile.ReasonCount);
                    break;
            }
        }

        /// <summary>One hour of moisture change on a tile under the given weather (spec 002 §3.2).</summary>
        internal static void UpdateMoisture(Simulation sim, Tile tile, int weather)
        {
            GameDefs defs = sim.Config.Game;
            SoilDef soil = defs.Soils[tile.Soil];
            DrainageDef drainage = defs.Drainages[tile.Drainage];
            Fixed d = defs.Land.MoistureByWeather[weather];
            Fixed m = tile.Moisture;
            if (d > Fixed.Zero) m += d * soil.GainFactor;
            else if (d < Fixed.Zero) m += d * soil.LossFactor;
            m = Clamp(m);
            if (m > drainage.DrainAbove) m -= (m - drainage.DrainAbove) * drainage.DrainRatePerHour;
            if (tile.ParcelId == WellParcel(sim) && m < defs.Land.WellMoistureFloor) m = defs.Land.WellMoistureFloor;
            tile.Moisture = m;
        }

        /// <summary>
        /// A farm tile with no crop for the whole previous day gains fallowRecoveryPerSeason ÷ daysPerSeason,
        /// up to its soil's cap (spec 002 §3.3).
        /// </summary>
        private static void RecoverFallow(Simulation sim, Tile tile, long t)
        {
            if (tile.Terrain != Terrain.Farm || tile.Occupied || tile.VacantSince > t - ClockDef.SecondsPerDay) return;
            GameDefs defs = sim.Config.Game;
            Fixed cap = defs.Soils[tile.Soil].FallowCap;
            if (tile.Fertility >= cap) return;
            Fixed gain = defs.Land.FallowRecoveryPerSeason / Fixed.FromInt(sim.Clock.DaysPerSeason);
            Fixed next = tile.Fertility + gain;
            Apply(tile, (next > cap ? cap : next) - tile.Fertility, FertilityReason.Fallow);
        }

        /// <summary>Adds <paramref name="delta"/> to a tile's fertility, clamped to [0, 100], and records the applied change.</summary>
        internal static void AdjustFertility(Simulation sim, int tileId, Fixed delta, FertilityReason reason)
        {
            Tile tile = sim.State.Land.Tiles[tileId];
            Apply(tile, Clamp(tile.Fertility + delta) - tile.Fertility, reason);
        }

        /// <summary>Spec 003 marks a tile occupied when sown and vacant when harvested or cleared.</summary>
        internal static void SetOccupied(Simulation sim, int tileId, bool occupied)
        {
            Tile tile = sim.State.Land.Tiles[tileId];
            if (occupied) tile.VacantSince = -1;
            else if (tile.Occupied) tile.VacantSince = sim.State.Clock.SimTime;
        }

        private static void Apply(Tile tile, Fixed applied, FertilityReason reason)
        {
            tile.Fertility += applied;
            tile.Changes[(int)reason] += applied;
        }

        private static readonly Fixed Hundred = Fixed.FromInt(100);

        private static Fixed Clamp(Fixed v) { return v < Fixed.Zero ? Fixed.Zero : v > Hundred ? Hundred : v; }

        /// <summary>The parcel holding the well, or −1.</summary>
        private static int WellParcel(Simulation sim)
        {
            LandState land = sim.State.Land;
            foreach (Building b in land.Buildings)
                if (sim.Config.Game.Buildings[b.Def].Id == BuildingDef.WellId) return land.Tiles[b.TileId].ParcelId;
            return -1;
        }
    }
}
