using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Buys an adjacent parcel, paid in full upfront at its current price (spec 002 §3.5, §5). Financing
    /// (spec 005 §3.6) joins with credit in Sprint 4.
    /// </summary>
    public sealed class BuyParcel : TimedCommand
    {
        public string ParcelId { get; }

        public BuyParcel(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.Config.Game == null) return CommandResult.Reject(RejectReason.UnknownParcel);
            int id = sim.Config.Game.Map.IndexOfParcel(ParcelId);
            if (id < 0) return CommandResult.Reject(RejectReason.UnknownParcel);
            Parcel parcel = sim.State.Land.Parcels[id];
            if (parcel.Owned) return CommandResult.Reject(RejectReason.AlreadyOwned);
            if (!LandSystem.IsAdjacentToOwned(sim, id)) return CommandResult.Reject(RejectReason.NotAdjacent);
            long price = LandRules.ParcelPrice(sim, parcel);
            if (!sim.State.Ledger.CanAfford(price)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(t, sim.Period, "ledger.buyParcel", "parcel:" + ParcelId,
                new Posting(Account.Land, price), new Posting(Account.Cash, -price));
            parcel.CostBasis = price;
            LandSystem.Acquire(sim, parcel, t);
            sim.Emit(new ParcelBought(t, ParcelId, price, sim.Config.Game.Map.Parcels[id].Tiles.Count));
            return CommandResult.Ok;
        }
    }

    /// <summary>Quick sale of a bought parcel to a neighbour at LandDef.quickSaleFactor of its price (spec 002 §3.5).</summary>
    public sealed class SellParcel : TimedCommand
    {
        public string ParcelId { get; }

        public SellParcel(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.Config.Game == null) return CommandResult.Reject(RejectReason.UnknownParcel);
            GameDefs defs = sim.Config.Game;
            int id = defs.Map.IndexOfParcel(ParcelId);
            if (id < 0) return CommandResult.Reject(RejectReason.UnknownParcel);
            Parcel parcel = sim.State.Land.Parcels[id];
            if (!parcel.Owned) return CommandResult.Reject(RejectReason.NotOwned);
            if (parcel.Protected) return CommandResult.Reject(RejectReason.Protected);
            if (LandSystem.InUse(sim, id)) return CommandResult.Reject(RejectReason.ParcelInUse);
            if (LandSystem.WouldStrand(sim, id)) return CommandResult.Reject(RejectReason.WouldStrandLand);

            long t = sim.State.Clock.SimTime;
            long price = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, parcel), defs.Land.QuickSaleFactor));
            long basis = parcel.CostBasis;
            long gain = price - basis;
            sim.State.Ledger.Post(t, sim.Period, "ledger.sellParcel", "parcel:" + ParcelId,
                new Posting(Account.Cash, price),
                new Posting(Account.Land, -basis),
                gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain));
            LandSystem.Release(sim, parcel);
            sim.Emit(new ParcelSold(t, ParcelId, price, basis));
            return CommandResult.Ok;
        }
    }
}
