using System.Collections.Generic;
using Sim.Core.Maths;

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

        internal void WriteTo(StateHasher h)
        {
            foreach (Fixed f in new[] { Base, Soil, Water, Care, Seed, Weather, Tools, Fertiliser, CropHealth, Uncapped }) h.WriteFixed(f);
            h.WriteBool(Capped);
            h.WriteLong(Grams);
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

        internal Crop Clone()
        {
            var c = (Crop)MemberwiseClone();
            return c; // the breakdown is immutable once set
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(Id);
            h.WriteInt(TileId);
            h.WriteInt(Def);
            h.WriteInt((int)State);
            h.WriteLong(SownAt);
            h.WriteLong(ReadyAt);
            h.WriteInt((int)Seed);
            h.WriteFixed(SeedAge);
            h.WriteInt(Fertiliser);
            h.WriteFixed(SoilFactor);
            h.WriteFixed(WaterSum);
            h.WriteFixed(CareSum);
            h.WriteFixed(WeatherSum);
            h.WriteFixed(WaterRate);
            h.WriteFixed(WeatherRate);
            h.WriteLong(JobId);
            h.WriteBool(Breakdown != null);
            if (Breakdown != null) Breakdown.WriteTo(h);
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

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(Id);
            h.WriteInt(Crop);
            h.WriteInt((int)State);
            h.WriteLong(ReadyAt);
            h.WriteLong(RemainingGrams);
            h.WriteLong(JobId);
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

        public FounderState Founder { get; internal set; }

        public Crop CropOn(int tileId) { return Crops.Find(c => c.TileId == tileId); }

        public Fixed? SavedSeedAgeOf(int crop) { return HasSavedSeed[crop] ? SavedSeedAge[crop] : (Fixed?)null; }

        internal FarmState Clone()
        {
            var c = new FarmState
            {
                NextCropId = NextCropId, CareRatio = CareRatio, LastAccrual = LastAccrual,
                SavedSeedAge = (Fixed[])SavedSeedAge.Clone(), HasSavedSeed = (bool[])HasSavedSeed.Clone(),
                Founder = Founder.Clone(),
            };
            foreach (Crop x in Crops) c.Crops.Add(x.Clone());
            foreach (GardenBed b in Beds) c.Beds.Add(b.Clone());
            return c;
        }

        internal void WriteTo(StateHasher h)
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
            h.WriteInt(Beds.Count);
            foreach (GardenBed b in Beds) b.WriteTo(h);
            Founder.WriteTo(h);
        }
    }

    /// <summary>The founder's household (spec 003 §3.6, §3.11).</summary>
    public sealed class FounderState
    {
        /// <summary>Indices into the diet plans and work-hours options.</summary>
        public int DietPlan { get; internal set; }
        public int PreviousPlan { get; internal set; }
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

        /// <summary>The food allowance accrued since the last daily pass, in money (Fixed for exact time-weighting).</summary>
        public Fixed AllowanceSum { get; internal set; }

        /// <summary>Home-use value eaten this season, all goods and the garden's goods (Why?).</summary>
        public long HomeUseSeason { get; internal set; }
        public long GardenHomeUseSeason { get; internal set; }

        internal FounderState Clone() { return (FounderState)MemberwiseClone(); }

        internal void WriteTo(StateHasher h)
        {
            h.WriteInt(DietPlan);
            h.WriteInt(PreviousPlan);
            h.WriteInt(WorkHours);
            h.WriteInt(TodayOption);
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
    }
}
