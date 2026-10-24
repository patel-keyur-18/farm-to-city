using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.People;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Builds a family house (spec 014 §3.4, §3.13): paid upfront and capitalised, ready in <c>buildHours</c>. The acre stops being
    /// farmed while a house stands or is being built (a hand's order on it ends).
    /// </summary>
    public sealed class BuildFamilyHouse : TimedCommand
    {
        public int TileId { get; }

        public BuildFamilyHouse(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason place = HouseRules.Place(sim, TileId);
            if (place != RejectReason.None) return CommandResult.Reject(place);
            Definitions.BuildingDef def = sim.Config.Game.Buildings[sim.Config.Game.Population.House];
            long cost = MarketRules.Indexed(sim, def.Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            CitizensState s = sim.State.Citizens;
            var c = new Construction { Id = s.NextConstructionId++, TileId = TileId, Cost = cost, ReadyAt = t + def.BuildHours * 3600L };
            sim.State.Ledger.Post(t, sim.Period, "ledger.buildHouse", "house:" + c.Id, new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            c.JobId = sim.ScheduleJob(HouseRules.Job, c.Id, c.ReadyAt).Id;
            s.Constructions.Add(c);
            if (sim.State.Land.Tiles[TileId].Terrain == Definitions.Terrain.Farm)
            {
                LandSystem.SetOccupied(sim, TileId, true);
                sim.State.People.Orders.RemoveAll(o => o.Kind == OrderTargetKind.Field && o.Target == TileId);
            }
            sim.Emit(new FamilyHouseStarted(t, c.Id, TileId, cost, c.ReadyAt));
            return CommandResult.Ok;
        }
    }
}
