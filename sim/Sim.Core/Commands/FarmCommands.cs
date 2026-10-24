using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Commands
{
    /// <summary>Sows a crop on a vacant owned farm tile, in season (spec 003 §3.2). Inputs come from stock first.</summary>
    public sealed class Sow : TimedCommand
    {
        public int TileId { get; }
        public string CropId { get; }
        public SeedSource Seed { get; }
        public string FertiliserId { get; }

        public Sow(long at, int tileId, string cropId, SeedSource seed, string fertiliserId) : base(at)
        {
            TileId = tileId;
            CropId = cropId;
            Seed = seed;
            FertiliserId = fertiliserId;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            if (defs == null || TileId < 0 || TileId >= sim.State.Land.Tiles.Count) return CommandResult.Reject(RejectReason.UnknownTile);
            Tile tile = sim.State.Land.Tiles[TileId];
            if (!sim.State.Land.Parcels[tile.ParcelId].Owned) return CommandResult.Reject(RejectReason.NotOwned);
            if (tile.Terrain != Terrain.Farm) return CommandResult.Reject(RejectReason.NotFarmTile);
            if (tile.Occupied) return CommandResult.Reject(RejectReason.TileOccupied);
            int c = defs.Farm.IndexOfCrop(CropId);
            if (c < 0) return CommandResult.Reject(RejectReason.UnknownCrop);
            CropDef crop = defs.Farm.Crops[c];
            if (crop.Fodder && !Livestock.LivestockRules.Unlocked(sim)) return CommandResult.Reject(RejectReason.EraLocked); // with cattle, from the Hamlet
            if (!crop.SowSeasons[(int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock)]) return CommandResult.Reject(RejectReason.OutOfSeason);
            int f = defs.Farm.IndexOfFertiliser(FertiliserId);
            if (f < 0) return CommandResult.Reject(RejectReason.UnknownFertiliser);
            FertiliserDef fert = defs.Farm.Fertilisers[f];

            long seedGrams = FarmRules.SeedGrams(sim.Clock, crop);
            if (Seed == SeedSource.Saved)
            {
                if (!crop.CanSaveSeed) return CommandResult.Reject(RejectReason.SeedCannotBeSaved);
                if (sim.State.Market.InStore(crop.OutputGood) < seedGrams || sim.State.Farm.SavedSeedAgeOf(c) == null)
                    return CommandResult.Reject(RejectReason.NoSavedSeed);
            }
            long fertGrams = FarmRules.FertiliserGrams(sim.Clock, crop, fert, sim.State.Clock.SimTime);
            long cost = FarmRules.SowingCost(sim, crop, Seed, fert);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            FarmSystem.Accrue(sim);
            FarmState farm = sim.State.Farm;
            long id = farm.NextCropId++;
            string reference = "crop:" + id;
            Fixed age = Fixed.Zero;
            if (Seed == SeedSource.Certified) Store.UseInput(sim, crop.SeedGood, seedGrams, Account.SeedsAndPlanting, reference);
            else
            {
                Store.Take(sim, crop.OutputGood, seedGrams);
                age = farm.SavedSeedAge[c];
            }
            if (fert.Good >= 0) Store.UseInput(sim, fert.Good, fertGrams, Account.FertiliserAndSpraying, reference);

            long t = sim.State.Clock.SimTime;
            var sown = new Crop
            {
                Id = id, TileId = TileId, Def = c, State = CropState.Growing, SownAt = t, ReadyAt = crop.Fodder ? FarmRules.StandEnd(sim.Clock, crop, t) : t + crop.GrowthSeconds,
                Seed = Seed, SeedAge = age, Fertiliser = f, SoilFactor = LandRules.SoilFactor(sim, tile), LastCut = crop.Fodder ? t : 0,
            };
            // A fodder field's job ends its stand (spec 012 §3.8b); it has no pest check (it is cut long before pests build up).
            sown.JobId = sim.ScheduleJob(FarmSystem.CropJob, id, sown.ReadyAt).Id;
            sown.Repeats = PestRules.RepeatsAt(sim, TileId, c);
            if (!crop.Fodder) sown.PestJobId = sim.ScheduleJob(FarmSystem.PestJob, id, t + crop.GrowthSeconds / 2).Id;
            farm.Crops.Add(sown);
            LandSystem.SetOccupied(sim, TileId, true);
            FarmSystem.Refresh(sim);
            sim.Emit(new CropSown(t, TileId, CropId, sown.ReadyAt, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>Moves a ready crop's frozen yield into the store and frees the tile (spec 003 §3.3).</summary>
    public sealed class Harvest : TimedCommand
    {
        public int TileId { get; }
        public Overflow Overflow { get; }

        public Harvest(long at, int tileId, Overflow overflow = Overflow.StoreOnly) : base(at)
        {
            TileId = tileId;
            Overflow = overflow;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Crop c = sim.State.Farm?.CropOn(TileId);
            if (c == null) return CommandResult.Reject(RejectReason.NoCrop);
            if (c.State != CropState.Ready) return CommandResult.Reject(RejectReason.NotReady);
            CropDef def = sim.Config.Game.Farm.Crops[c.Def];
            long grams = c.HarvestGrams;
            RejectReason r = Store.Add(sim, def.OutputGood, grams, Overflow, "crop:" + c.Id);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            Livestock.LivestockSystem.AddStraw(sim, def, grams); // wheat and rice give straw to the stack (spec 012 §3.8)

            // The harvest's saved seed is one cycle older than the seed it grew from (§3.9).
            FarmState farm = sim.State.Farm;
            farm.SavedSeedAge[c.Def] = c.SeedAge + FarmRules.CycleShare(sim.Clock, def);
            farm.HasSavedSeed[c.Def] = def.CanSaveSeed;
            farm.TileRunCrop[TileId] = c.Def; // the run of same-crop harvests (spec 003 §3.8)
            farm.TileRun[TileId] = c.Repeats + 1;
            farm.Crops.Remove(c);
            LandSystem.SetOccupied(sim, TileId, false);
            sim.Emit(new CropHarvested(sim.State.Clock.SimTime, TileId, def.Id, grams, c.Breakdown));
            Progress.EraRules.OnHarvest(sim, TileId, c.Breakdown.Care);
            return CommandResult.Ok;
        }
    }

    /// <summary>Removes a growing or ready crop; nothing is refunded or harvested (spec 003 §3.3).</summary>
    public sealed class ClearField : TimedCommand
    {
        public int TileId { get; }

        public ClearField(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Crop c = sim.State.Farm?.CropOn(TileId);
            if (c == null) return CommandResult.Reject(RejectReason.NoCrop);
            FarmSystem.Accrue(sim);
            if (c.State == CropState.Growing) sim.CompleteJob(sim.State.Jobs.Find(c.JobId));
            if (c.PestJobId != 0) sim.CompleteJob(sim.State.Jobs.Find(c.PestJobId));
            sim.State.Farm.Crops.Remove(c);
            LandSystem.SetOccupied(sim, TileId, false);
            FarmSystem.Refresh(sim);
            sim.Emit(new FieldCleared(sim.State.Clock.SimTime, TileId, sim.Config.Game.Farm.Crops[c.Def].Id));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Sprays a growing crop once a cycle (spec 003 §3.8): before the pest check it halves the risk; after a warning it
    /// cuts the damage by 70%. The dose comes from the store first, the rest bought (Dr 5010).
    /// </summary>
    public sealed class Spray : TimedCommand
    {
        public int TileId { get; }

        public Spray(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Crop c = sim.State.Farm?.CropOn(TileId);
            if (c == null) return CommandResult.Reject(RejectReason.NoCrop);
            if (c.State != CropState.Growing) return CommandResult.Reject(RejectReason.NotGrowing);
            if (c.Spray != SprayState.None) return CommandResult.Reject(RejectReason.AlreadySprayed);
            if (c.PestChecked && c.Outbreak == Outbreak.None) return CommandResult.Reject(RejectReason.NoPestThreat);
            PestRiskDef p = sim.Config.Game.Farm.Pests;
            long dose = PestRules.Dose(sim, sim.Config.Game.Farm.Crops[c.Def]);
            if (!sim.State.Ledger.CanAfford(Store.ShortfallCost(sim, p.SprayGood, dose))) return CommandResult.Reject(RejectReason.InsufficientFunds);
            Store.UseInput(sim, p.SprayGood, dose, Account.FertiliserAndSpraying, "crop:" + c.Id);
            c.Spray = c.PestChecked ? SprayState.AfterWarning : SprayState.BeforeCheck;
            return CommandResult.Ok;
        }
    }

    /// <summary>Sows a quick green in an empty kitchen-garden bed (spec 003 §3.13).</summary>
    public sealed class SowGarden : TimedCommand
    {
        public long BedId { get; }
        public string GardenCropId { get; }

        public SowGarden(long at, long bedId, string gardenCropId) : base(at)
        {
            BedId = bedId;
            GardenCropId = gardenCropId;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            GardenBed bed = sim.State.Farm?.Beds.Find(b => b.Id == BedId);
            if (bed == null) return CommandResult.Reject(RejectReason.UnknownBed);
            if (bed.State != BedState.Empty) return CommandResult.Reject(RejectReason.BedBusy);
            if (bed.RemainingGrams <= 0) return CommandResult.Reject(RejectReason.BedRested);
            FarmDefs farm = sim.Config.Game.Farm;
            int c = farm.IndexOfGardenCrop(GardenCropId);
            if (c < 0) return CommandResult.Reject(RejectReason.UnknownCrop);
            GardenCropDef crop = farm.GardenCrops[c];
            if (!sim.State.Ledger.CanAfford(Store.ShortfallCost(sim, crop.SeedGood, crop.SeedGramsPerSowing)))
                return CommandResult.Reject(RejectReason.InsufficientFunds);

            Store.UseInput(sim, crop.SeedGood, crop.SeedGramsPerSowing, Account.SeedsAndPlanting, "bed:" + BedId);
            bed.Crop = c;
            bed.State = BedState.Growing;
            bed.ReadyAt = sim.State.Clock.SimTime + crop.GrowthMinutes * 60L;
            bed.JobId = sim.ScheduleJob(FarmSystem.GardenJob, BedId, bed.ReadyAt).Id;
            return CommandResult.Ok;
        }
    }

    /// <summary>Picks a ready bed: min(harvest, the bed's remaining season output) into the store (spec 003 §3.13).</summary>
    public sealed class PickGarden : TimedCommand
    {
        public long BedId { get; }
        public Overflow Overflow { get; }

        public PickGarden(long at, long bedId, Overflow overflow = Overflow.StoreOnly) : base(at)
        {
            BedId = bedId;
            Overflow = overflow;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            GardenBed bed = sim.State.Farm?.Beds.Find(b => b.Id == BedId);
            if (bed == null) return CommandResult.Reject(RejectReason.UnknownBed);
            if (bed.State != BedState.Ready) return CommandResult.Reject(RejectReason.NotReady);
            GardenCropDef crop = sim.Config.Game.Farm.GardenCrops[bed.Crop];
            long grams = System.Math.Min(crop.HarvestGrams, bed.RemainingGrams);
            RejectReason r = Store.Add(sim, crop.Good, grams, Overflow, "bed:" + BedId);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            bed.RemainingGrams -= grams;
            bed.Crop = -1;
            bed.State = BedState.Empty;
            sim.Emit(new GardenPicked(sim.State.Clock.SimTime, BedId, sim.Config.Game.Market.Goods[crop.Good].Id, grams, bed.RemainingGrams));
            return CommandResult.Ok;
        }
    }
}
