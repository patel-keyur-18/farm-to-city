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

namespace Sim.Core.Saves
{
    /// <summary>
    /// Save schema 8 (ADR 0064): the founder inherits the cattle acre. Every tile of an inherited parcel that a save holds as
    /// something else takes its content terrain and parcel; a crop or tree on it is paid for as when the farm lane was taken
    /// (spec 010 §3.4), its slots and the orders on it go; the parcel becomes owned and protected.
    /// </summary>
    public static class CattleAcreMigration
    {
        public const int Schema = 8;

        /// <summary>True if the town changed.</summary>
        internal static bool Apply(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            if (defs == null) return false;
            LandState land = sim.State.Land;
            long t = sim.State.Clock.SimTime;
            bool changed = false;
            foreach (int p in defs.Start.InheritedParcels)
            {
                foreach (int i in defs.Map.Parcels[p].Tiles)
                {
                    Tile tile = land.Tiles[i];
                    if (tile.ParcelId == p && tile.Terrain == defs.Map.Tiles[i].Terrain) continue;
                    Clear(sim, i, land.Parcels[tile.ParcelId].Owned);
                    Tile fresh = LandSystem.NewTile(defs.Map, i);
                    fresh.ParcelId = p;
                    land.Tiles[i] = fresh;
                    changed = true;
                }
                Parcel home = land.Parcels[p];
                if (home.Owned) continue;
                home.Protected = true;
                home.CostBasis = LandRules.ParcelPrice(sim, home);
                LandSystem.Acquire(sim, home, t);
                // Inherited land enters the books at its value, as the opening entry put the rest (spec 005 §3.1).
                sim.State.Ledger.Post(t, sim.Period, "ledger.cattleAcreInherited", "parcel:" + defs.Map.Parcels[p].Id,
                    new Posting(Account.Land, home.CostBasis), new Posting(Account.OpeningEquity, -home.CostBasis));
                changed = true;
            }
            if (changed) FarmSystem.Refresh(sim);
            return changed;
        }

        private static void Clear(Simulation sim, int tileId, bool owned)
        {
            GameDefs defs = sim.Config.Game;
            LandState land = sim.State.Land;
            FarmSystem.Accrue(sim);
            long crops = 0, trees = 0;
            Crop crop = sim.State.Farm.CropOn(tileId);
            if (crop != null)
            {
                CropDef def = defs.Farm.Crops[crop.Def];
                if (def.OutputGood >= 0)
                {
                    long grams = crop.State == CropState.Ready ? crop.HarvestGrams : FarmRules.CycleGrams(sim.Clock, def, def.SeasonYieldPerAcre);
                    crops = MarketRules.Value(sim, def.OutputGood, grams, MarketRules.Floor(sim, def.OutputGood));
                }
                if (crop.State == CropState.Growing) sim.CompleteJob(sim.State.Jobs.Find(crop.JobId));
                if (crop.PestJobId != 0) sim.CompleteJob(sim.State.Jobs.Find(crop.PestJobId));
                sim.State.Farm.Crops.Remove(crop);
                LandSystem.SetOccupied(sim, tileId, false);
            }
            foreach (TreeSlot slot in land.Slots.FindAll(s => s.TileId == tileId))
            {
                Tree tree = sim.State.Farm.Trees.Find(x => x.SlotId == slot.Id);
                if (tree != null)
                {
                    trees += tree.Cost;
                    FarmSystem.Dispose(sim, tree); // writes the book value off; the payment below makes it good
                }
                sim.State.People?.Orders.RemoveAll(o => o.Kind == People.OrderTargetKind.Tree && o.Target == slot.Id);
                land.Slots.Remove(slot);
            }
            sim.State.People?.Orders.RemoveAll(o => o.Kind == People.OrderTargetKind.Field && o.Target == tileId);
            if (!owned || crops + trees == 0) return;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.cattleAcre", "tile:" + tileId,
                new Posting(Account.Cash, crops + trees), new Posting(Account.CropSales, -crops), new Posting(Account.GainOnAssetSales, -trees));
        }
    }
}
