using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;

namespace Sim.Core.Commands
{
    /// <summary>Shared checks and the purchase for the irrigation commands (spec 013 §3.1, ADR 0086, ADR 0087).</summary>
    internal static class Irrigation
    {
        /// <summary>None if the tile is one of your fields, else why not.</summary>
        internal static RejectReason Field(Simulation sim, int tile)
        {
            LandState land = sim.State.Land;
            if (tile < 0 || tile >= land.Tiles.Count) return RejectReason.UnknownTile;
            if (land.Tiles[tile].Terrain != Terrain.Farm) return RejectReason.NotFarmTile;
            if (!land.Parcels[land.Tiles[tile].ParcelId].Owned) return RejectReason.NotOwned;
            return RejectReason.None;
        }

        /// <summary>Capitalises the building (Dr 1600) and adds it on the tile.</summary>
        internal static Building Buy(Simulation sim, int def, int tile, long cost, string key, string reference)
        {
            LandState land = sim.State.Land;
            var b = new Building { Id = land.NextBuildingId++, Def = def, TileId = tile, Cost = cost };
            land.Buildings.Add(b);
            if (cost > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, key, reference, new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            return b;
        }

        /// <summary>
        /// The acres a pumped source waters (1 the pump, 2 the borewell): at most its acres, your fields, each once, none on the other
        /// source's list, and every drip acre on its list kept.
        /// </summary>
        internal static CommandResult SetAcres(Simulation sim, int source, IReadOnlyList<int> tiles)
        {
            if (!IrrigationRules.On(sim) || tiles == null) return CommandResult.Reject(RejectReason.InvalidArgument);
            if ((source == 1 ? IrrigationRules.Pump(sim) : IrrigationRules.Borewell(sim)) == null) return CommandResult.Reject(RejectReason.NoWaterSource);
            PumpedDef d = source == 1 ? IrrigationRules.Def(sim).Pump : IrrigationRules.Def(sim).Borewell;
            if (tiles.Count > d.Acres) return CommandResult.Reject(RejectReason.TooManyAcres);
            List<int> other = IrrigationRules.Acres(sim, source == 1 ? 2 : 1);
            for (int i = 0; i < tiles.Count; i++)
            {
                RejectReason why = Field(sim, tiles[i]);
                if (why != RejectReason.None) return CommandResult.Reject(why);
                for (int j = 0; j < i; j++) if (tiles[j] == tiles[i]) return CommandResult.Reject(RejectReason.InvalidArgument);
                if (other.Contains(tiles[i])) return CommandResult.Reject(RejectReason.AlreadyWatered);
            }
            List<int> list = IrrigationRules.Acres(sim, source);
            foreach (int t in list)
            {
                bool kept = false;
                foreach (int x in tiles) kept |= x == t;
                if (!kept && IrrigationRules.HasDrip(sim, t)) return CommandResult.Reject(RejectReason.DripNeedsWater);
            }
            list.Clear();
            list.AddRange(tiles);
            sim.Emit(new WaterAcresSet(sim.State.Clock.SimTime, source, list.Count));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// An electric pump on the well (spec 013 §3.1): from the Hamlet, one, capitalised to 1600; it starts on the well's acres and
    /// waters up to its acres, chosen with <see cref="SetPumpAcres"/>.
    /// </summary>
    public sealed class InstallPump : TimedCommand
    {
        public InstallPump(long at) : base(at) { }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!IrrigationRules.On(sim)) return CommandResult.Reject(RejectReason.InvalidArgument);
            PumpedDef d = IrrigationRules.Def(sim).Pump;
            if (!IrrigationRules.Unlocked(sim, d.UnlockEra)) return CommandResult.Reject(RejectReason.EraLocked);
            if (IrrigationRules.Pump(sim) != null) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            Building well = sim.State.Land.Buildings.Find(b => sim.Config.Game.Buildings[b.Def].Id == BuildingDef.WellId);
            if (well == null) return CommandResult.Reject(RejectReason.InvalidArgument);
            long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            Irrigation.Buy(sim, d.Building, well.TileId, cost, "ledger.installPump", "pump");
            List<int> acres = sim.State.Irrigation.PumpAcres;
            acres.Clear();
            foreach (int t in sim.Config.Game.Land.WellTiles)
                if (acres.Count < d.Acres && Irrigation.Field(sim, t) == RejectReason.None && !sim.State.Irrigation.BorewellAcres.Contains(t)) acres.Add(t);
            sim.Emit(new PumpInstalled(sim.State.Clock.SimTime, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>The acres the pump waters (spec 013 §3.1, ADR 0087 §3).</summary>
    public sealed class SetPumpAcres : TimedCommand
    {
        public IReadOnlyList<int> Tiles { get; }

        public SetPumpAcres(long at, IReadOnlyList<int> tiles) : base(at) { Tiles = tiles; }

        protected override CommandResult ApplyAt(Simulation sim) { return Irrigation.SetAcres(sim, 1, Tiles); }
    }

    /// <summary>The acres the borewell waters (ADR 0086, ADR 0087 §3).</summary>
    public sealed class SetBorewellAcres : TimedCommand
    {
        public IReadOnlyList<int> Tiles { get; }

        public SetBorewellAcres(long at, IReadOnlyList<int> tiles) : base(at) { Tiles = tiles; }

        protected override CommandResult ApplyAt(Simulation sim) { return Irrigation.SetAcres(sim, 2, Tiles); }
    }

    /// <summary>
    /// A borewell with its own pump on one of your fields (ADR 0086): from the Village, one, a small pump room at the field's back
    /// corner (the acre still farmed); it starts on its own acre (unless the pump waters it).
    /// </summary>
    public sealed class DrillBorewell : TimedCommand
    {
        public int TileId { get; }

        public DrillBorewell(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!IrrigationRules.On(sim)) return CommandResult.Reject(RejectReason.InvalidArgument);
            RejectReason why = Irrigation.Field(sim, TileId);
            if (why != RejectReason.None) return CommandResult.Reject(why);
            PumpedDef d = IrrigationRules.Def(sim).Borewell;
            if (!IrrigationRules.Unlocked(sim, d.UnlockEra)) return CommandResult.Reject(RejectReason.EraLocked);
            if (IrrigationRules.Borewell(sim) != null) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            Irrigation.Buy(sim, d.Building, TileId, cost, "ledger.drillBorewell", "borewell");
            sim.State.Irrigation.BorewellAcres.Clear();
            if (!sim.State.Irrigation.PumpAcres.Contains(TileId)) sim.State.Irrigation.BorewellAcres.Add(TileId);
            sim.Emit(new BorewellDrilled(sim.State.Clock.SimTime, TileId, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>A lined channel from the minor canal on one of your fields that borders the canal or a channelled field (spec 013 §3.1, ADR 0087 §2).</summary>
    public sealed class BuildChannel : TimedCommand
    {
        public int TileId { get; }

        public BuildChannel(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!IrrigationRules.On(sim)) return CommandResult.Reject(RejectReason.InvalidArgument);
            RejectReason why = Irrigation.Field(sim, TileId);
            if (why != RejectReason.None) return CommandResult.Reject(why);
            CanalDef d = IrrigationRules.Def(sim).Canal;
            if (!IrrigationRules.Unlocked(sim, d.UnlockEra)) return CommandResult.Reject(RejectReason.EraLocked);
            if (IrrigationRules.Channelled(sim, TileId)) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            if (!IrrigationRules.CanReach(sim, TileId)) return CommandResult.Reject(RejectReason.NotAdjacent);
            long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            Irrigation.Buy(sim, d.Building, TileId, cost, "ledger.buildChannel", "channel:" + TileId);
            sim.Emit(new ChannelBuilt(sim.State.Clock.SimTime, TileId, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Drip on a field the pump or the borewell waters (spec 013 §3.1): from the Village, half its price paid by the government on
    /// your first acres (PMKSY); what you pay is capitalised.
    /// </summary>
    public sealed class LayDrip : TimedCommand
    {
        public int TileId { get; }

        public LayDrip(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!IrrigationRules.On(sim)) return CommandResult.Reject(RejectReason.InvalidArgument);
            RejectReason why = Irrigation.Field(sim, TileId);
            if (why != RejectReason.None) return CommandResult.Reject(why);
            DripDef d = IrrigationRules.Def(sim).Drip;
            if (!IrrigationRules.Unlocked(sim, d.UnlockEra)) return CommandResult.Reject(RejectReason.EraLocked);
            if (IrrigationRules.HasDrip(sim, TileId)) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            if (IrrigationRules.SourceOf(sim, TileId) == 0) return CommandResult.Reject(RejectReason.NoWaterSource);
            long cost = IrrigationRules.DripPrice(sim);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long full = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
            if (cost < full) sim.State.Irrigation.SubsidisedDripAcres++;
            Irrigation.Buy(sim, d.Building, TileId, cost, "ledger.layDrip", "drip:" + TileId);
            sim.Emit(new DripLaid(sim.State.Clock.SimTime, TileId, cost, full - cost));
            return CommandResult.Ok;
        }
    }
}
