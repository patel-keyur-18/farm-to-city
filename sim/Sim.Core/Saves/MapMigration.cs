using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;

namespace Sim.Core.Saves
{
    /// <summary>What the village paid for the farm lane's acre when an old save owned it (spec 010 §3.4), for the one-time notice.</summary>
    public sealed class RoadTake
    {
        public string ParcelId { get; internal set; }

        /// <summary>Money (1/100 coin): 2 × the parcel's price.</summary>
        public long Compensation { get; internal set; }

        /// <summary>Money: the crop and trees that stood on it.</summary>
        public long OnIt { get; internal set; }

        /// <summary>Money: the bank loan secured on it, repaid first (ADR 0038).</summary>
        public long LoanRepaid { get; internal set; }
    }

    /// <summary>
    /// Save schema 5 (spec 010 §3.4, ADR 0052): a Phase 1 save's 8 × 6 map sits unchanged at columns 2–9, rows 3–8 of the
    /// 16 × 12 map. Readers remap every tile id they read; afterwards the farm lane takes parcel <c>s5</c>.
    /// </summary>
    public static class MapMigration
    {
        public const int Schema = 5;
        public const int OldTiles = 48;
        public const string FarmLane = "s5";
        private const int OldWidth = 8, NewWidth = 16, OffsetX = 2, OffsetY = 3;

        public static int Remap(int old) { return (old / OldWidth + OffsetY) * NewWidth + old % OldWidth + OffsetX; }

        /// <summary>
        /// The farm lane takes <c>s5</c>: the village pays 2 × its price plus what stands on it, a loan secured on it is repaid
        /// first, and the parcel is released (its tile is a road acre in content). Null when the save didn't own it.
        /// </summary>
        internal static RoadTake TakeFarmLane(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            int id = defs.Map.IndexOfParcel(FarmLane);
            if (id < 0) return null;
            Parcel parcel = sim.State.Land.Parcels[id];
            if (!parcel.Owned) return null;

            long t = sim.State.Clock.SimTime;
            long period = sim.Period;
            Ledger ledger = sim.State.Ledger;
            FarmSystem.Accrue(sim);
            long crops = 0, trees = 0;
            foreach (int tile in defs.Map.Parcels[id].Tiles)
            {
                Crop crop = sim.State.Farm.CropOn(tile);
                if (crop == null) continue;
                CropDef def = defs.Farm.Crops[crop.Def];
                long grams = crop.State == CropState.Ready ? crop.HarvestGrams : FarmRules.CycleGrams(sim.Clock, def, def.SeasonYieldPerAcre);
                crops += MarketRules.Value(sim, def.OutputGood, grams, MarketRules.Floor(sim, def.OutputGood));
                if (crop.State == CropState.Growing) sim.CompleteJob(sim.State.Jobs.Find(crop.JobId));
                if (crop.PestJobId != 0) sim.CompleteJob(sim.State.Jobs.Find(crop.PestJobId));
                sim.State.Farm.Crops.Remove(crop);
                LandSystem.SetOccupied(sim, tile, false);
            }
            foreach (Tree tree in sim.State.Farm.Trees.ToArray())
            {
                TreeSlot slot = sim.State.Land.Slots.Find(s => s.Id == tree.SlotId);
                if (slot == null || sim.State.Land.Tiles[slot.TileId].ParcelId != id) continue;
                trees += tree.Cost;
                FarmSystem.Dispose(sim, tree); // writes the book value off; the payment below makes it good
            }

            long price = Money.RoundToCoin(Money.MulRate(LandRules.ParcelPrice(sim, parcel), defs.Land.RoadTakeFactor));
            long basis = parcel.CostBasis, gain = price - basis + trees; // the trees' payment offsets their write-off
            long required = Finance.CreditSystem.RequiredOnSale(sim, id);
            ledger.Post(t, period, "ledger.landTakenForRoad", "parcel:" + FarmLane,
                new Posting(Account.Cash, price + crops + trees), new Posting(Account.Land, -basis),
                gain >= 0 ? new Posting(Account.GainOnAssetSales, -gain) : new Posting(Account.LossOnAssetSales, -gain),
                new Posting(Account.CropSales, -crops));
            LandSystem.Release(sim, parcel);
            if (required > 0) Finance.CreditSystem.RepaySecured(sim, required);
            FarmSystem.Refresh(sim);
            return new RoadTake { ParcelId = FarmLane, Compensation = price, OnIt = crops + trees, LoanRepaid = required };
        }
    }
}
