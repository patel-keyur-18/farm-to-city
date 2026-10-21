using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Buys an adjacent parcel, paid upfront at its current price (spec 002 §3.5, §5), optionally part-financed by a
    /// land-purchase loan secured by the parcel itself (spec 005 §3.6, ADR 0028, 0038): both succeed or neither.
    /// </summary>
    public sealed class BuyParcel : TimedCommand
    {
        public string ParcelId { get; }
        public Financing Financing { get; }

        public BuyParcel(long at, string parcelId, Financing financing = null) : base(at)
        {
            ParcelId = parcelId;
            Financing = financing;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (sim.Config.Game == null) return CommandResult.Reject(RejectReason.UnknownParcel);
            int id = sim.Config.Game.Map.IndexOfParcel(ParcelId);
            if (id < 0) return CommandResult.Reject(RejectReason.UnknownParcel);
            Parcel parcel = sim.State.Land.Parcels[id];
            if (parcel.Owned) return CommandResult.Reject(RejectReason.AlreadyOwned);
            if (!sim.Config.Game.Map.Parcels[id].ForSale) return CommandResult.Reject(RejectReason.NotForSale);
            if (!LandSystem.IsAdjacentToOwned(sim, id)) return CommandResult.Reject(RejectReason.NotAdjacent);
            long price = LandRules.ParcelPrice(sim, parcel);
            int lender = -1;
            long loan = 0;
            if (Financing != null)
            {
                // The loan may not exceed the price; the bank counts this parcel as collateral (ADR 0038).
                if (Financing.Amount > price) return CommandResult.Reject(RejectReason.InvalidAmount);
                RejectReason r = CreditRules.Check(sim, Financing.LenderId, Financing.Amount, Financing.TermSeasons, price, out lender);
                if (r != RejectReason.None) return CommandResult.Reject(r);
                loan = Financing.Amount;
            }
            if (!sim.State.Ledger.CanAfford(price - loan)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            long t = sim.State.Clock.SimTime;
            if (Financing != null) CreditSystem.Create(sim, lender, loan, Financing.TermSeasons);
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
            // Land that secures a bank loan: the proceeds repay the bank first, or the sale is refused (ADR 0038).
            long required = CreditSystem.RequiredOnSale(sim, id);
            if (price < required) return CommandResult.Reject(RejectReason.LoanSecured);
            long basis = parcel.CostBasis;
            long gain = price - basis;
            sim.State.Ledger.Post(t, sim.Period, "ledger.sellParcel", "parcel:" + ParcelId,
                new Posting(Account.Cash, price),
                new Posting(Account.Land, -basis),
                gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain));
            LandSystem.Release(sim, parcel);
            if (required > 0) CreditSystem.RepaySecured(sim, required);
            sim.Emit(new ParcelSold(t, ParcelId, price, basis));
            return CommandResult.Ok;
        }
    }
}
