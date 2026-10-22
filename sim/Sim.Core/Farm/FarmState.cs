using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Farm
{
    public enum CropState
    {
        Growing = 0,
        Ready = 1,
    }

    public enum SeedSource
    {
        Certified = 0,
        Saved = 1,
    }

    /// <summary>The yield formula's factors for one harvest (spec 003 §3.4), for Why? and CropHarvested.</summary>
    public sealed class YieldBreakdown
    {
        /// <summary>kg: seasonYieldPerAcre × cycleShare.</summary>
        public Fixed Base { get; internal set; }
        public Fixed Soil { get; internal set; }
        public Fixed Water { get; internal set; }
        public Fixed Care { get; internal set; }
        public Fixed Seed { get; internal set; }
        public Fixed Weather { get; internal set; }
        public Fixed Tools { get; internal set; }
        public Fixed Fertiliser { get; internal set; }
        public Fixed CropHealth { get; internal set; }

        /// <summary>kg before the cap.</summary>
        public Fixed Uncapped { get; internal set; }
        public bool Capped { get; internal set; }
        public long Grams { get; internal set; }

        internal void WriteTo(IStateWriter h)
        {
            foreach (Fixed f in new[] { Base, Soil, Water, Care, Seed, Weather, Tools, Fertiliser, CropHealth, Uncapped }) h.WriteFixed(f);
            h.WriteBool(Capped);
            h.WriteLong(Grams);
        }

        internal static YieldBreakdown ReadFrom(SaveReader r)
        {
            return new YieldBreakdown
            {
                Base = r.ReadFixed(), Soil = r.ReadFixed(), Water = r.ReadFixed(), Care = r.ReadFixed(), Seed = r.ReadFixed(), Weather = r.ReadFixed(),
                Tools = r.ReadFixed(), Fertiliser = r.ReadFixed(), CropHealth = r.ReadFixed(), Uncapped = r.ReadFixed(), Capped = r.ReadBool(), Grams = r.ReadLong(),
            };
        }
    }

    /// <summary>A field crop on one tile (spec 003 §4).</summary>
    public sealed class Crop
    {
        public long Id { get; internal set; }
        public int TileId { get; internal set; }

        /// <summary>Index into <see cref="Definitions.FarmDefs.Crops"/>.</summary>
        public int Def { get; internal set; }
        public CropState State { get; internal set; }
        public long SownAt { get; internal set; }
        public long ReadyAt { get; internal set; }
        public SeedSource Seed { get; internal set; }
        public Fixed SeedAge { get; internal set; }
        public int Fertiliser { get; internal set; }
        public Fixed SoilFactor { get; internal set; }

        /// <summary>Σ rate × seconds since sowing (spec 003 §3.7).</summary>
        public Fixed WaterSum { get; internal set; }
        public Fixed CareSum { get; internal set; }
        public Fixed WeatherSum { get; internal set; }

        /// <summary>The rates in force since the last accrual point.</summary>
        public Fixed WaterRate { get; internal set; }
        public Fixed WeatherRate { get; internal set; }
        public long JobId { get; internal set; }

        /// <summary>Set when the crop becomes ready.</summary>
        public YieldBreakdown Breakdown { get; internal set; }

        /// <summary>Pests (spec 003 §3.8): seconds of wet weather so far, the current wet flag, same-crop harvests before it.</summary>
        public long WetSeconds { get; internal set; }
        public bool Wet { get; internal set; }
        public int Repeats { get; internal set; }
        public SprayState Spray { get; internal set; }
        public bool PestChecked { get; internal set; }
        public Outbreak Outbreak { get; internal set; }
        public Fixed RolledHealth { get; internal set; } = Fixed.One;
        public long PestJobId { get; internal set; }

        /// <summary>Grams lost while waiting ready after the grace (ADR 0034).</summary>
        public long SpoiledGrams { get; internal set; }

        /// <summary>What a harvest would move into the store now.</summary>
        public long HarvestGrams { get { return Breakdown == null ? 0 : Breakdown.Grams - SpoiledGrams; } }

        internal Crop Clone()
        {
            var c = (Crop)MemberwiseClone();
            return c; // the breakdown is immutable once set
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt(TileId);
            h.WriteRef(ContentTable.Crop, Def);
            h.WriteInt((int)State);
            h.WriteLong(SownAt);
            h.WriteLong(ReadyAt);
            h.WriteInt((int)Seed);
            h.WriteFixed(SeedAge);
            h.WriteRef(ContentTable.Fertiliser, Fertiliser);
            h.WriteFixed(SoilFactor);
            h.WriteFixed(WaterSum);
            h.WriteFixed(CareSum);
            h.WriteFixed(WeatherSum);
            h.WriteFixed(WaterRate);
            h.WriteFixed(WeatherRate);
            h.WriteLong(JobId);
            h.WriteBool(Breakdown != null);
            if (Breakdown != null) Breakdown.WriteTo(h);
            h.WriteLong(SpoiledGrams);
            h.WriteLong(WetSeconds);
            h.WriteBool(Wet);
            h.WriteInt(Repeats);
            h.WriteInt((int)Spray);
            h.WriteBool(PestChecked);
            h.WriteInt((int)Outbreak);
            h.WriteFixed(RolledHealth);
            h.WriteLong(PestJobId);
        }

        internal static Crop ReadFrom(SaveReader r)
        {
            var c = new Crop
            {
                Id = r.ReadLong(), TileId = r.ReadTile(), Def = r.ReadRef(ContentTable.Crop), State = (CropState)r.ReadInt(), SownAt = r.ReadLong(),
                ReadyAt = r.ReadLong(), Seed = (SeedSource)r.ReadInt(), SeedAge = r.ReadFixed(), Fertiliser = r.ReadRef(ContentTable.Fertiliser),
                SoilFactor = r.ReadFixed(), WaterSum = r.ReadFixed(), CareSum = r.ReadFixed(), WeatherSum = r.ReadFixed(), WaterRate = r.ReadFixed(),
                WeatherRate = r.ReadFixed(), JobId = r.ReadLong(),
            };
            c.Breakdown = r.ReadBool() ? YieldBreakdown.ReadFrom(r) : null;
            c.SpoiledGrams = r.ReadLong();
            c.WetSeconds = r.ReadLong();
            c.Wet = r.ReadBool();
            c.Repeats = r.ReadInt();
            c.Spray = (SprayState)r.ReadInt();
            c.PestChecked = r.ReadBool();
            c.Outbreak = (Outbreak)r.ReadInt();
            c.RolledHealth = r.ReadFixed();
            c.PestJobId = r.ReadLong();
            return c;
        }
    }

    public enum BedState
    {
        Empty = 0,
        Growing = 1,
        Ready = 2,
    }

    /// <summary>A kitchen-garden bed (spec 003 §3.13).</summary>
    public sealed class GardenBed
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="Definitions.FarmDefs.GardenCrops"/>, or −1 when empty.</summary>
        public int Crop { get; internal set; } = -1;
        public BedState State { get; internal set; }
        public long ReadyAt { get; internal set; }

        /// <summary>Grams this bed can still give this season.</summary>
        public long RemainingGrams { get; internal set; }
        public long JobId { get; internal set; }

        internal GardenBed Clone() { return (GardenBed)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.GardenCrop, Crop);
            h.WriteInt((int)State);
            h.WriteLong(ReadyAt);
            h.WriteLong(RemainingGrams);
            h.WriteLong(JobId);
        }

        internal static GardenBed ReadFrom(SaveReader r)
        {
            return new GardenBed
            {
                Id = r.ReadLong(), Crop = r.ReadRef(ContentTable.GardenCrop), State = (BedState)r.ReadInt(), ReadyAt = r.ReadLong(),
                RemainingGrams = r.ReadLong(), JobId = r.ReadLong(),
            };
        }
    }

    public enum TreeState
    {
        Growing = 0,
        Mature = 1,
    }

    /// <summary>A fruit tree in a yard or field-edge slot (spec 003 §3.12).</summary>
    public sealed class Tree
    {
        public long Id { get; internal set; }
        public long SlotId { get; internal set; }

        /// <summary>Index into <see cref="Definitions.FarmDefs.Trees"/>.</summary>
        public int Def { get; internal set; }
        public long PlantedAt { get; internal set; }
        public TreeState State { get; internal set; }

        /// <summary>Grams of ripe fruit waiting on the tree (0 = none), and when the batch ripened.</summary>
        public long RipeGrams { get; internal set; }
        public long RipeAt { get; internal set; }

        /// <summary>Batches picked so far.</summary>
        public int Batches { get; internal set; }

        /// <summary>Money: Σ picked batches at the trader's buying price when picked.</summary>
        public long Returns { get; internal set; }

        /// <summary>Money: the sapling's cost, its book value (not depreciated in Phase 1).</summary>
        public long Cost { get; internal set; }

        /// <summary>The season index in which returns first reached the cost (−1 = not yet).</summary>
        public long PaidBackPeriod { get; internal set; } = -1;
        public long JobId { get; internal set; }

        internal Tree Clone() { return (Tree)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteLong(SlotId);
            h.WriteRef(ContentTable.Tree, Def);
            h.WriteLong(PlantedAt);
            h.WriteInt((int)State);
            h.WriteLong(RipeGrams);
            h.WriteLong(RipeAt);
            h.WriteInt(Batches);
            h.WriteLong(Returns);
            h.WriteLong(Cost);
            h.WriteLong(PaidBackPeriod);
            h.WriteLong(JobId);
        }

        internal static Tree ReadFrom(SaveReader r)
        {
            return new Tree
            {
                Id = r.ReadLong(), SlotId = r.ReadLong(), Def = r.ReadRef(ContentTable.Tree), PlantedAt = r.ReadLong(), State = (TreeState)r.ReadInt(),
                RipeGrams = r.ReadLong(), RipeAt = r.ReadLong(), Batches = r.ReadInt(), Returns = r.ReadLong(), Cost = r.ReadLong(),
                PaidBackPeriod = r.ReadLong(), JobId = r.ReadLong(),
            };
        }
    }

    /// <summary>Crops, garden beds, trees and the founder's household (spec 003 §4). Lists are in id order.</summary>
    public sealed class FarmState
    {
        public List<Crop> Crops { get; } = new List<Crop>();
        public long NextCropId { get; internal set; } = 1;

        /// <summary>The shared care ratio in force since the last accrual point (spec 003 §3.6).</summary>
        public Fixed CareRatio { get; internal set; }

        /// <summary>The last accrual point (spec 003 §3.7), for crops and the founder alike.</summary>
        public long LastAccrual { get; internal set; }

        /// <summary>Saved-seed age per crop index (seasons of growth), and whether it has one yet.</summary>
        internal Fixed[] SavedSeedAge;
        internal bool[] HasSavedSeed;

        public List<GardenBed> Beds { get; } = new List<GardenBed>();

        /// <summary>Per tile: the crop of the current run of harvests (−1 none) and its length (spec 003 §3.8).</summary>
        internal int[] TileRunCrop;
        internal int[] TileRun;

        public List<Tree> Trees { get; } = new List<Tree>();
        public long NextTreeId { get; internal set; } = 1;

        public Tree TreeIn(long slotId) { return Trees.Find(t => t.SlotId == slotId); }

        public FounderState Founder { get; internal set; }

        public Crop CropOn(int tileId) { return Crops.Find(c => c.TileId == tileId); }

        public Fixed? SavedSeedAgeOf(int crop) { return HasSavedSeed[crop] ? SavedSeedAge[crop] : (Fixed?)null; }

        internal FarmState Clone()
        {
            var c = new FarmState
            {
                NextCropId = NextCropId, NextTreeId = NextTreeId, CareRatio = CareRatio, LastAccrual = LastAccrual,
                SavedSeedAge = (Fixed[])SavedSeedAge.Clone(), HasSavedSeed = (bool[])HasSavedSeed.Clone(),
                TileRunCrop = (int[])TileRunCrop.Clone(), TileRun = (int[])TileRun.Clone(),
                Founder = Founder.Clone(),
            };
            foreach (Crop x in Crops) c.Crops.Add(x.Clone());
            foreach (GardenBed b in Beds) c.Beds.Add(b.Clone());
            foreach (Tree t in Trees) c.Trees.Add(t.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(NextCropId);
            h.WriteFixed(CareRatio);
            h.WriteLong(LastAccrual);
            for (int i = 0; i < SavedSeedAge.Length; i++)
            {
                h.WriteBool(HasSavedSeed[i]);
                h.WriteFixed(SavedSeedAge[i]);
            }
            h.WriteInt(Crops.Count);
            foreach (Crop x in Crops) x.WriteTo(h);
            for (int i = 0; i < TileRun.Length; i++) { h.WriteRef(ContentTable.Crop, TileRunCrop[i]); h.WriteInt(TileRun[i]); }
            h.WriteInt(Beds.Count);
            foreach (GardenBed b in Beds) b.WriteTo(h);
            h.WriteLong(NextTreeId);
            h.WriteInt(Trees.Count);
            foreach (Tree t in Trees) t.WriteTo(h);
            Founder.WriteTo(h);
        }

        internal static FarmState ReadFrom(SaveReader r, int tiles)
        {
            var s = new FarmState { NextCropId = r.ReadLong(), CareRatio = r.ReadFixed(), LastAccrual = r.ReadLong() };
            int crops = r.Count(ContentTable.Crop);
            // Crops added to the content since the save have no saved seed (spec 012's berseem and sorghum); none may be missing.
            if (r.SavedCount(ContentTable.Crop) > crops) throw new SaveContentException("the content has " + crops + " crops, the save " + r.SavedCount(ContentTable.Crop));
            s.SavedSeedAge = new Fixed[crops];
            s.HasSavedSeed = new bool[crops];
            for (int i = 0; i < r.SavedCount(ContentTable.Crop); i++)
            {
                int at = r.Slot(ContentTable.Crop, i);
                s.HasSavedSeed[at] = r.ReadBool();
                s.SavedSeedAge[at] = r.ReadFixed();
            }
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Crops.Add(Crop.ReadFrom(r));
            s.TileRunCrop = new int[tiles];
            s.TileRun = new int[tiles];
            for (int i = 0; i < tiles; i++) s.TileRunCrop[i] = -1;
            int saved = r.OldMap ? Saves.MapMigration.OldTiles : tiles;
            for (int i = 0; i < saved; i++)
            {
                int at = r.OldMap ? Saves.MapMigration.Remap(i) : i;
                s.TileRunCrop[at] = r.ReadRef(ContentTable.Crop);
                s.TileRun[at] = r.ReadInt();
            }
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Beds.Add(GardenBed.ReadFrom(r));
            s.NextTreeId = r.ReadLong();
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Trees.Add(Tree.ReadFrom(r));
            s.Founder = FounderState.ReadFrom(r);
            return s;
        }
    }

    /// <summary>The founder's household (spec 003 §3.6, §3.11).</summary>
    public sealed class FounderState
    {
        /// <summary>Indices into the diet plans and work-hours options.</summary>
        public int DietPlan { get; internal set; }

        /// <summary>The plan to return to after an automatic drop to subsistence (spec 003 §3.11).</summary>
        public int PreviousPlan { get; internal set; }

        /// <summary>True while on subsistence because a season's living costs couldn't be paid.</summary>
        public bool AutoSubsistence { get; internal set; }
        public int WorkHours { get; internal set; }

        /// <summary>The option worked today (normal while away), fixed at the daily pass; its fatigue change applies at the next.</summary>
        public int TodayOption { get; internal set; }

        /// <summary>Percent of the founder's labour sold as day labour: 0, 25, 50, 75 or 100.</summary>
        public int DayLabourShare { get; internal set; }
        public bool EatFromStore { get; internal set; }

        public Fixed Fatigue { get; internal set; }
        public Fixed Health { get; internal set; }

        /// <summary>Hours × (1 − Fatigue) × Health for today (0 on a sick day); × nutrition gives the labour rate.</summary>
        public Fixed TodayHours { get; internal set; }
        public bool SickToday { get; internal set; }

        /// <summary>The living-cost prepayment, or 0 on subsistence.</summary>
        public long LivingPrepaymentId { get; internal set; }

        /// <summary>The day index of the last medicine (−1 = never).</summary>
        public long MedicineDay { get; internal set; } = -1;

        /// <summary>Accrued since the last daily pass (spec 003 §3.6, §3.7, §3.11).</summary>
        public Fixed DayLabourHours { get; internal set; }
        public Fixed NutritionSum { get; internal set; }

        /// <summary>When the current day's accruals started (the last daily pass, or the new game's start).</summary>
        public long DayStart { get; internal set; }

        /// <summary>The food allowance accrued since the last daily pass, in money (Fixed for exact time-weighting).</summary>
        public Fixed AllowanceSum { get; internal set; }

        /// <summary>Seconds of the current day spent on subsistence in Winter (the warm-clothing penalty, time-weighted; ADR 0034).</summary>
        public long ColdSeconds { get; internal set; }

        /// <summary>Home-use value eaten this season, all goods and the garden's goods (Why?).</summary>
        public long HomeUseSeason { get; internal set; }
        public long GardenHomeUseSeason { get; internal set; }

        internal FounderState Clone() { return (FounderState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(ColdSeconds);
            h.WriteRef(ContentTable.DietPlan, DietPlan);
            h.WriteRef(ContentTable.DietPlan, PreviousPlan);
            h.WriteBool(AutoSubsistence);
            h.WriteLong(DayStart);
            h.WriteRef(ContentTable.WorkHours, WorkHours);
            h.WriteRef(ContentTable.WorkHours, TodayOption);
            h.WriteInt(DayLabourShare);
            h.WriteBool(EatFromStore);
            h.WriteFixed(Fatigue);
            h.WriteFixed(Health);
            h.WriteFixed(TodayHours);
            h.WriteBool(SickToday);
            h.WriteLong(LivingPrepaymentId);
            h.WriteLong(MedicineDay);
            h.WriteFixed(DayLabourHours);
            h.WriteFixed(NutritionSum);
            h.WriteFixed(AllowanceSum);
            h.WriteLong(HomeUseSeason);
            h.WriteLong(GardenHomeUseSeason);
        }

        internal static FounderState ReadFrom(SaveReader r)
        {
            return new FounderState
            {
                ColdSeconds = r.ReadLong(), DietPlan = r.ReadRef(ContentTable.DietPlan), PreviousPlan = r.ReadRef(ContentTable.DietPlan),
                AutoSubsistence = r.ReadBool(), DayStart = r.ReadLong(), WorkHours = r.ReadRef(ContentTable.WorkHours),
                TodayOption = r.ReadRef(ContentTable.WorkHours), DayLabourShare = r.ReadInt(), EatFromStore = r.ReadBool(), Fatigue = r.ReadFixed(),
                Health = r.ReadFixed(), TodayHours = r.ReadFixed(), SickToday = r.ReadBool(), LivingPrepaymentId = r.ReadLong(),
                MedicineDay = r.ReadLong(), DayLabourHours = r.ReadFixed(), NutritionSum = r.ReadFixed(), AllowanceSum = r.ReadFixed(),
                HomeUseSeason = r.ReadLong(), GardenHomeUseSeason = r.ReadLong(),
            };
        }
    }
}
