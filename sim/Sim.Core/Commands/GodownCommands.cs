using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Buys your godown in the mandi town (spec 013 §3.4, ADR 0087): from the Hamlet, one, yours at once (an existing godown, not
    /// built), capitalised to 1600 and depreciated with the buildings. It stands off the map, so it has no tile.
    /// </summary>
    public sealed class BuyGodown : TimedCommand
    {
        public BuyGodown(long at) : base(at) { }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            GodownDef d = GodownRules.Def(sim);
            if (d == null || sim.State.Godown == null) return CommandResult.Reject(RejectReason.InvalidArgument);
            if (!GodownRules.Unlocked(sim)) return CommandResult.Reject(RejectReason.EraLocked);
            if (GodownRules.Owned(sim) != null) return CommandResult.Reject(RejectReason.AlreadyBuilt);
            long cost = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            LandState land = sim.State.Land;
            var b = new Building { Id = land.NextBuildingId++, Def = d.Building, TileId = -1, Cost = cost };
            land.Buildings.Add(b);
            sim.State.Ledger.Post(t, sim.Period, "ledger.buyGodown", "godown", new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            sim.Emit(new GodownBought(t, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Books whole tonnes of space in the warehouse in the mandi town (spec 013 §3.4, ADR 0087): more from now, paid now for the
    /// days left in this season; less (0 ends it) from the next close, never below what your goods stored there need.
    /// </summary>
    public sealed class RentWarehouse : TimedCommand
    {
        public int Tonnes { get; }

        public RentWarehouse(long at, int tonnes) : base(at) { Tonnes = tonnes; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            GodownDef d = GodownRules.Def(sim);
            GodownState s = sim.State.Godown;
            if (d == null || s == null || Tonnes < 0 || Tonnes > d.MaxRentTonnes) return CommandResult.Reject(RejectReason.InvalidArgument);
            if (!GodownRules.Unlocked(sim)) return CommandResult.Reject(RejectReason.EraLocked);
            long t = sim.State.Clock.SimTime;
            if (Tonnes <= s.RentedTonnes)
            {
                if (Tonnes * 1_000_000L < GodownRules.InRentedGrams(sim)) return CommandResult.Reject(RejectReason.StockTooHigh);
                s.RentNextTonnes = Tonnes;
                sim.Emit(new WarehouseRented(t, Tonnes, 0));
                return CommandResult.Ok;
            }
            long cost = GodownRules.RentForRestOfSeason(sim, Tonnes - s.RentedTonnes);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            if (cost > 0) Accrual.Open(sim, Account.Rent, Account.PrepaidRent, cost, t, GodownRules.SeasonEnd(sim), "warehouse");
            s.RentedTonnes = s.RentNextTonnes = Tonnes;
            sim.Emit(new WarehouseRented(t, Tonnes, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>Lets your godown to a trader from the next close, renewed each close until switched off (spec 013 §3.4, ADR 0087).</summary>
    public sealed class LetGodown : TimedCommand
    {
        public bool On { get; }

        public LetGodown(long at, bool on) : base(at) { On = on; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.State.Godown == null || GodownRules.Owned(sim) == null) return CommandResult.Reject(RejectReason.NoGodown);
            sim.State.Godown.LetNext = On;
            return CommandResult.Ok;
        }
    }
}
