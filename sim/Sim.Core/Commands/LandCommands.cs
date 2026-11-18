using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
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
            if (!sim.Config.Game.Map.Parcels[id].ForSale || parcel.BuilderOwned) return CommandResult.Reject(RejectReason.NotForSale); // a builder has it (spec 024 §3.3)
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
            if (parcel.Leased) return CommandResult.Reject(RejectReason.Leased); // end the lease first (spec 024 §3.6)
            if (LandSystem.InUse(sim, id)) return CommandResult.Reject(RejectReason.ParcelInUse);
            if (LandSystem.WouldStrand(sim, id)) return CommandResult.Reject(RejectReason.WouldStrandLand);

            if (parcel.Listed) return CommandResult.Reject(RejectReason.Listed);
            long price = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, parcel), defs.Land.QuickSaleFactor));
            if (price < CreditSystem.RequiredOnSale(sim, id)) return CommandResult.Reject(RejectReason.LoanSecured); // land that secures a bank loan (ADR 0038)
            LandSystem.Sell(sim, parcel, price);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Lists a bought parcel for a slow sale (US-143, spec 024 §3.4): it sells in <c>slowSaleSeconds</c> at 100% of its land value as it stands now, the
    /// price fixed at listing; the acre is locked until it sells or <see cref="CancelSale"/> takes it off. From the Township.
    /// </summary>
    public sealed class ListParcel : TimedCommand
    {
        public string ParcelId { get; }

        public ListParcel(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = LandSystem.List(sim, ParcelId);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>Takes a parcel off the market before it sells (spec 024 §3.4); the acre is yours again, unlocked.</summary>
    public sealed class CancelSale : TimedCommand
    {
        public string ParcelId { get; }

        public CancelSale(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = LandSystem.Unlist(sim, ParcelId);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>Zones a parcel you own residential, so builders may buy it when families are waiting for homes (US-142, spec 024 §3.3).</summary>
    public sealed class ZoneParcel : TimedCommand
    {
        public string ParcelId { get; }

        public ZoneParcel(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Town.ZoningRules.Set(sim, ParcelId, Town.Zone.Residential);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>Takes a parcel back to farming before a builder buys it.</summary>
    public sealed class UnzoneParcel : TimedCommand
    {
        public string ParcelId { get; }

        public UnzoneParcel(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Town.ZoningRules.Set(sim, ParcelId, Town.Zone.Agricultural);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>
    /// Leases a parcel you own out to a neighbour farmer (US-261, spec 024 §3.6): the acre is locked at once, the lease starts at the next close when the
    /// first rent arrives, and it renews each season until <see cref="EndLease"/>. From the Township.
    /// </summary>
    public sealed class LeaseOutLand : TimedCommand
    {
        public string ParcelId { get; }

        public LeaseOutLand(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Town.LandLeaseRules.Lease(sim, ParcelId);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>Ends a lease at the next close; one that has not started yet is cancelled at once, free.</summary>
    public sealed class EndLease : TimedCommand
    {
        public string ParcelId { get; }

        public EndLease(long at, string parcelId) : base(at) { ParcelId = parcelId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Town.LandLeaseRules.End(sim, ParcelId);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }
}
