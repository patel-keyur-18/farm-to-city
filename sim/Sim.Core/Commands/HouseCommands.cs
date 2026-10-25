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

        /// <summary>A home loan from the bank toward it (spec 014 §3.4, ADR 0071), or null to pay in cash.</summary>
        public Financing Financing { get; }

        public BuildFamilyHouse(long at, int tileId, Financing financing = null) : base(at)
        {
            TileId = tileId;
            Financing = financing;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason place = HouseRules.Place(sim, TileId);
            if (place != RejectReason.None) return CommandResult.Reject(place);
            Definitions.BuildingDef def = sim.Config.Game.Buildings[sim.Config.Game.Population.House];
            long cost = MarketRules.Indexed(sim, def.Value);
            int lender = -1;
            long loan = 0;
            if (Financing != null)
            {
                // The home loan: no more than the house costs; the bank counts it at houseLtv (as the dairy loan counts an animal).
                if (Financing.Amount > cost) return CommandResult.Reject(RejectReason.InvalidAmount);
                RejectReason r = CreditRules.Check(sim, Financing.LenderId, Financing.Amount, Financing.TermSeasons, 0, out lender, 0, cost);
                if (r != RejectReason.None) return CommandResult.Reject(r);
                loan = Financing.Amount;
            }
            if (!sim.State.Ledger.CanAfford(cost - loan)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            if (Financing != null) CreditSystem.Create(sim, lender, loan, Financing.TermSeasons);

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

    /// <summary>A hand's answer to a free family house (spec 014 §3.13): yes brings his family, not now lets strangers come.</summary>
    public sealed class AnswerFamilyRequest : TimedCommand
    {
        public long HandId { get; }
        public bool Yes { get; }

        public AnswerFamilyRequest(long at, long handId, bool yes) : base(at)
        {
            HandId = handId;
            Yes = yes;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return MigrationRules.Answer(sim, HandId, Yes); }
    }
}
