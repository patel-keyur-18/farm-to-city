using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>One kind of animal (spec 012 §3.2): amounts are per real day, as a farmer says them (§3.1).</summary>
    public sealed class AnimalDef
    {
        public string Id { get; }
        public string NameKey { get; }

        /// <summary>Price at the fair, money (× PriceIndex).</summary>
        public long Price { get; }

        /// <summary>Mean milk over a lactation, litres a real day.</summary>
        public Fixed MilkPerDay { get; }

        /// <summary>Fat, percent: sets the co-op's price (§3.5).</summary>
        public Fixed FatPercent { get; }

        /// <summary>Milk factor by season, indexed by <c>Season</c>.</summary>
        public IReadOnlyList<Fixed> SeasonMilk { get; }

        public int MilkDays { get; }
        public int DryDays { get; }

        /// <summary>Ration in milk, kg a real day.</summary>
        public Fixed Green { get; }
        public Fixed Straw { get; }
        public Fixed Feed { get; }

        /// <summary>The feed ration while dry is Feed × this.</summary>
        public Fixed DryFeedFactor { get; }

        /// <summary>Labour hours a game day (§3.9).</summary>
        public Fixed LabourHoursPerDay { get; }

        /// <summary>Compost from her dung, kg a real day (§3.8).</summary>
        public Fixed DungCompostKgPerDay { get; }

        /// <summary>A calf at the fair, money (× PriceIndex).</summary>
        public long CalfPrice { get; }

        /// <summary>Selling her at the fair gives price × this × her health.</summary>
        public Fixed SellShare { get; }

        public AnimalDef(string id, string nameKey, long price, Fixed milkPerDay, Fixed fatPercent, IReadOnlyList<Fixed> seasonMilk, int milkDays,
            int dryDays, Fixed green, Fixed straw, Fixed feed, Fixed dryFeedFactor, Fixed labourHoursPerDay, Fixed dungCompostKgPerDay,
            long calfPrice, Fixed sellShare)
        {
            Id = id;
            NameKey = nameKey;
            Price = price;
            MilkPerDay = milkPerDay;
            FatPercent = fatPercent;
            SeasonMilk = new List<Fixed>(seasonMilk).AsReadOnly();
            MilkDays = milkDays;
            DryDays = dryDays;
            Green = green;
            Straw = straw;
            Feed = feed;
            DryFeedFactor = dryFeedFactor;
            LabourHoursPerDay = labourHoursPerDay;
            DungCompostKgPerDay = dungCompostKgPerDay;
            CalfPrice = calfPrice;
            SellShare = sellShare;
        }
    }

    /// <summary>The rules every animal shares (spec 012 §4, <c>content/livestock.json</c>).</summary>
    public sealed class LivestockDef
    {
        /// <summary>Real days of an animal's life in one game day (§3.1; 22.8 = 365.25 ÷ 16).</summary>
        public Fixed RealDaysPerGameDay { get; set; }

        /// <summary>The hour of the morning pass: milking, feeding (§3.15).</summary>
        public int FeedHour { get; set; }

        /// <summary>The yard acre the shed goes on (§3.3; tile 103).</summary>
        public int ShedTile { get; set; }

        /// <summary>The era from which the shed can be built (§3.13; "hamlet").</summary>
        public string UnlockEra { get; set; }

        /// <summary>Milk the household keeps, litres a real day (§3.5).</summary>
        public Fixed HouseholdLitresPerDay { get; set; }

        /// <summary>The co-op's price a litre: base + perFatPoint × fat% (§3.5), money.</summary>
        public Fixed MilkBasePerLitre { get; set; }
        public Fixed MilkPerFatPoint { get; set; }

        /// <summary>Share of the green need the commons cover, by season (§3.7).</summary>
        public IReadOnlyList<Fixed> CommonsShare { get; set; }

        public Fixed MinMilkFactor { get; set; }
        public Fixed HungryBelow { get; set; }
        public int HungerGraceDays { get; set; }
        public Fixed HungerHealthLoss { get; set; }
        public Fixed HealthRecoveryPerDay { get; set; }
        public Fixed MinHealth { get; set; }

        /// <summary>The fed score's weights: green, straw, feed (sum 1).</summary>
        public Fixed GreenWeight { get; set; }
        public Fixed StrawWeight { get; set; }
        public Fixed FeedWeight { get; set; }

        public int LowDays { get; set; }

        public Fixed DiseaseChance { get; set; }
        public Fixed UnvaccinatedFactor { get; set; }
        public Fixed SickHealthFactor { get; set; }
        public Fixed SickHealthLoss { get; set; }

        /// <summary>Money, × PriceIndex.</summary>
        public long VaccineCost { get; set; }
        public long VetVisitCost { get; set; }

        /// <summary>Milk factor by day of the lactation (§3.2), piecewise linear.</summary>
        public IReadOnlyList<CurvePoint> LactationCurve { get; set; }

        public IReadOnlyList<string> Names { get; set; }
    }

    /// <summary>Spec 012's content: the shared rules, the animals, and where the shed and the feeds are.</summary>
    public sealed class LivestockDefs
    {
        public const string ShedId = "cattle_shed";

        public LivestockDef Livestock { get; }
        public IReadOnlyList<AnimalDef> Animals { get; }

        /// <summary>Index of the cattle shed in <see cref="GameDefs.Buildings"/>.</summary>
        public int Shed { get; }

        /// <summary>Index of <see cref="LivestockDef.UnlockEra"/> in the onboarding eras, or 0 without eras.</summary>
        public int UnlockEra { get; }

        public LivestockDefs(LivestockDef livestock, IReadOnlyList<AnimalDef> animals, int shed, int unlockEra)
        {
            Livestock = livestock;
            Animals = new List<AnimalDef>(animals).AsReadOnly();
            Shed = shed;
            UnlockEra = unlockEra;
        }

        public int IndexOfAnimal(string id)
        {
            for (int i = 0; i < Animals.Count; i++) if (Animals[i].Id == id) return i;
            return -1;
        }
    }
}
