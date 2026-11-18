using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// Leasing land out (US-261, spec 024 §3.6). A parcel you own that is empty, not zoned and has no pump or borewell on it or watering it is
    /// leased to a neighbour farmer: locked at once, the first rent at the next close, then upfront at every close until the lease ends at a close.
    /// The rent is a part of the parcel's land value (<c>leaseYield</c>); it goes to your cash as rent received, outside farm profit. The tenant is
    /// nobody in the sim: the soil stays as it was.
    /// </summary>
    public static class LandLeaseRules
    {
        public static bool On(Simulation sim) { return ZoningRules.On(sim); }

        public static Fixed Yield(Simulation sim) { return sim.Config.Game.Township.LandValue.LeaseYield; }

        /// <summary>A season's rent of a parcel at today's land value, in whole coins.</summary>
        public static long Rent(Simulation sim, Parcel parcel)
        {
            return Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, parcel), Yield(sim)));
        }

        /// <summary>When the next close comes: the lease starts and the rent is paid then.</summary>
        public static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        public static LeaseStatus StatusOf(Parcel p) { return (LeaseStatus)p.Lease; }

        /// <summary>Why a parcel can't be leased out, or None.</summary>
        public static RejectReason CanLease(Simulation sim, int parcelId)
        {
            if (!On(sim)) return RejectReason.NoTownship;
            Parcel p = sim.State.Land.Parcels[parcelId];
            if (!p.Owned) return RejectReason.NotOwned;
            if (p.Locked) return p.LockReason;
            if (p.Zone != (int)Zone.Agricultural) return RejectReason.AlreadyZoned; // take it back to farming first
            if (ZoningRules.HomeTiles(sim, parcelId).Count == 0) return RejectReason.NotFarmTile;
            if (LandSystem.InUse(sim, parcelId) || HasOrder(sim, parcelId) || HasIrrigation(sim, parcelId)) return RejectReason.ParcelInUse;
            return RejectReason.None;
        }

        /// <summary>A hand has a standing order on an acre of the parcel (orders are never taken off silently, ADR 0039).</summary>
        public static bool HasOrder(Simulation sim, int parcelId)
        {
            if (sim.State.People == null) return false;
            foreach (StandingOrder o in sim.State.People.Orders)
                if (o.Kind == OrderTargetKind.Field && o.Target >= 0 && o.Target < sim.State.Land.Tiles.Count && sim.State.Land.Tiles[(int)o.Target].ParcelId == parcelId) return true;
            return false;
        }

        /// <summary>An acre of the parcel holds irrigation (a pump, a borewell, a channel, drip) or is watered by the pump or the borewell.</summary>
        public static bool HasIrrigation(Simulation sim, int parcelId)
        {
            LandState land = sim.State.Land;
            foreach (Building b in land.Buildings)
                if (b.TileId >= 0 && land.Tiles[b.TileId].ParcelId == parcelId && IrrigationRules.IsIrrigation(sim, b)) return true;
            if (!IrrigationRules.On(sim)) return false;
            foreach (int t in sim.State.Irrigation.PumpAcres) if (land.Tiles[t].ParcelId == parcelId) return true;
            foreach (int t in sim.State.Irrigation.BorewellAcres) if (land.Tiles[t].ParcelId == parcelId) return true;
            return false;
        }

        internal static RejectReason Lease(Simulation sim, string parcelId)
        {
            if (sim.Config.Game == null) return RejectReason.UnknownParcel;
            int id = sim.Config.Game.Map.IndexOfParcel(parcelId);
            if (id < 0) return RejectReason.UnknownParcel;
            RejectReason r = CanLease(sim, id);
            if (r != RejectReason.None) return r;
            Parcel p = sim.State.Land.Parcels[id];
            p.Lease = (int)LeaseStatus.Starting;
            sim.Emit(new LeaseOut(sim.State.Clock.SimTime, parcelId, Rent(sim, p)));
            return RejectReason.None;
        }

        /// <summary>A lease not yet started is cancelled at once; a running one ends at the next close.</summary>
        internal static RejectReason End(Simulation sim, string parcelId)
        {
            if (sim.Config.Game == null) return RejectReason.UnknownParcel;
            int id = sim.Config.Game.Map.IndexOfParcel(parcelId);
            if (id < 0) return RejectReason.UnknownParcel;
            Parcel p = sim.State.Land.Parcels[id];
            switch (StatusOf(p))
            {
                case LeaseStatus.Starting:
                    p.Lease = (int)LeaseStatus.None;
                    sim.Emit(new LeaseEnded(sim.State.Clock.SimTime, parcelId));
                    return RejectReason.None;
                case LeaseStatus.Active:
                    p.Lease = (int)LeaseStatus.Ending;
                    return RejectReason.None;
                default: return RejectReason.NotLeased;
            }
        }

        /// <summary>The close, after land value is read: a lease that was ending ends; one starting or running pays this season's rent upfront.</summary>
        internal static void Close(Simulation sim)
        {
            if (!On(sim)) return;
            long t = sim.State.Clock.SimTime;
            foreach (Parcel p in sim.State.Land.Parcels)
            {
                if (!p.Leased) continue;
                string parcelId = sim.Config.Game.Map.Parcels[p.Id].Id;
                if (StatusOf(p) == LeaseStatus.Ending)
                {
                    p.Lease = (int)LeaseStatus.None;
                    sim.Emit(new LeaseEnded(t, parcelId));
                    continue;
                }
                p.Lease = (int)LeaseStatus.Active;
                long rent = Rent(sim, p);
                if (rent <= 0) continue;
                sim.State.Ledger.Post(t, sim.Period, "ledger.landLeased", "parcel:" + parcelId, new Posting(Account.Cash, rent), new Posting(Account.LandRentReceived, -rent));
                sim.Emit(new LandRentPaid(t, parcelId, rent));
            }
        }
    }
}
