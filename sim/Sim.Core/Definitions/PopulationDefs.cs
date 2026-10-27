using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>An Engel curve point (spec 014 §3.5): income a person a season (money) and the share of it spent on food.</summary>
    public readonly struct EngelPoint
    {
        public long Income { get; }
        public Fixed FoodShare { get; }

        public EngelPoint(long income, Fixed foodShare)
        {
            Income = income;
            FoodShare = foodShare;
        }
    }

    /// <summary>A good in the households' food basket (spec 014 §3.7): kg per adult equivalent per real day.</summary>
    public readonly struct BasketItem
    {
        public int Good { get; }
        public Fixed KgPerRealDay { get; }

        public BasketItem(int good, Fixed kgPerRealDay)
        {
            Good = good;
            KgPerRealDay = kgPerRealDay;
        }
    }

    /// <summary>Citizens, households and population (spec 014 §4), from <c>content/population.json</c>.</summary>
    public sealed class PopulationDefs
    {
        /// <summary>The family house in <c>buildings.json</c>: its presence brings population.json (spec 014 §3.4).</summary>
        public const string HouseId = "family_house";

        /// <summary>Index of the family house in the buildings.</summary>
        public int House { get; internal set; }

        // The life clock (§3.2, §3.3, §3.11).
        public int LifeYearsPerSeason { get; internal set; }
        public int AdultFrom { get; internal set; }
        public int ElderFrom { get; internal set; }
        public int RetireAge { get; internal set; }
        public Fixed MinHealth { get; internal set; }
        public Fixed HealthLossPerSeason { get; internal set; }
        public Fixed HealthGainPerSeason { get; internal set; }
        public Fixed PrimaryEducationShare { get; internal set; }
        public int ApplicantAgeMin { get; internal set; }
        public int ApplicantAgeMax { get; internal set; }
        public int MigratedHandAgeMin { get; internal set; }
        public int MigratedHandAgeMax { get; internal set; }

        // The budget (§3.5, §3.6).
        public Fixed RemitShare { get; internal set; }
        public Fixed OutsideDaysShare { get; internal set; }
        public IReadOnlyList<EngelPoint> EngelCurve { get; internal set; }
        public Fixed ClothingShare { get; internal set; }
        public Fixed HealthShare { get; internal set; }
        public Fixed EducationShare { get; internal set; }
        public Fixed LeisureOfRest { get; internal set; }
        public long FoodBasketPerPerson { get; internal set; }
        public Fixed ChildEquivalent { get; internal set; }
        public Fixed ElderEquivalent { get; internal set; }

        // The basket (§3.7), in good id order.
        public Fixed RealDaysPerGameDay { get; internal set; }
        public IReadOnlyList<BasketItem> Basket { get; internal set; }

        // Needs and happiness (§3.8).
        public Fixed SurvivalWeight { get; internal set; }
        public Fixed SecurityWeight { get; internal set; }
        public Fixed BelongingWeight { get; internal set; }
        public Fixed GrowthWeight { get; internal set; }
        public Fixed Gate { get; internal set; }
        public Fixed Chemist { get; internal set; }
        public Fixed Belonging { get; internal set; }
        public Fixed Leisure { get; internal set; }

        // Births, marriages, deaths and migration (§3.9, §3.10).
        public Fixed BirthChance { get; internal set; }
        public Fixed CrowdedBirthFactor { get; internal set; }
        public int BirthAgeMin { get; internal set; }
        public int BirthAgeMax { get; internal set; }
        public int MaxChildren { get; internal set; }
        public Fixed MarriageChance { get; internal set; }
        public int MarriageAgeMin { get; internal set; }
        public int MarriageAgeMax { get; internal set; }
        public Fixed DeathBase { get; internal set; }
        public int DeathDoublingYears { get; internal set; }
        public Fixed DeathCap { get; internal set; }
        public Fixed PoorHealthBelow { get; internal set; }
        public Fixed PoorHealthDeathFactor { get; internal set; }
        public int Mourning { get; internal set; }
        public int FamilyEveryHours { get; internal set; }
        public int FamilyJitterHours { get; internal set; }
        public int FamilySizeMin { get; internal set; }
        public int FamilySizeMax { get; internal set; }

        /// <summary>The chance a family moving in brings an elder (a widowed parent).</summary>
        public Fixed FamilyElderChance { get; internal set; }
        public int LeaveBelow { get; internal set; }
        public int LeaveSeasons { get; internal set; }
        public Fixed LeaveChance { get; internal set; }
        public int RentBehindSeasons { get; internal set; }

        // The family house (§3.4, §3.13).
        public string HouseUnlockEra { get; internal set; }
        public int HouseCapacity { get; internal set; }
        public int HousesPerAcre { get; internal set; }
        public long RentPerSeason { get; internal set; }

        /// <summary>Home villages' ids ("from Sitapur"); their names are <c>village.&lt;id&gt;</c> strings.</summary>
        public IReadOnlyList<string> Villages { get; internal set; }
    }
}
