using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>The day-labour pool (spec 006 §3.2, §4).</summary>
    public sealed class LabourPoolDef
    {
        public int RosterSize { get; }

        /// <summary>Free labourers a day, indexed by <see cref="Time.Season"/>.</summary>
        public IReadOnlyList<int> AvailablePerDay { get; }

        /// <summary>Money a game day, before the price index.</summary>
        public long DailyWage { get; }
        public Fixed HoursPerDay { get; }

        /// <summary>Full day-equivalents hired before a labourer asks to move in (ADR 0039).</summary>
        public int RegularThresholdDays { get; }

        public LabourPoolDef(int rosterSize, IReadOnlyList<int> availablePerDay, long dailyWage, Fixed hoursPerDay, int regularThresholdDays)
        {
            RosterSize = rosterSize;
            AvailablePerDay = new List<int>(availablePerDay).AsReadOnly();
            DailyWage = dailyWage;
            HoursPerDay = hoursPerDay;
            RegularThresholdDays = regularThresholdDays;
        }
    }

    /// <summary>The resident hand: hours, wages, applicants and contracts (spec 006 §3.1, §3.5–3.8, §4).</summary>
    public sealed class HandDef
    {
        public Fixed HoursPerDay { get; internal set; }
        public Fixed ProductivityBase { get; internal set; }
        public Fixed ProductivityPerSkill { get; internal set; }
        public long MarketWage { get; internal set; }
        public Fixed WageBase { get; internal set; }
        public Fixed WagePerSkill { get; internal set; }
        public int SkillMin { get; internal set; }
        public int SkillMax { get; internal set; }
        public int SkillRangeHalfWidth { get; internal set; }
        public Fixed SkillGainPerSeason { get; internal set; }
        public int MaxApplicants { get; internal set; }
        public int ApplicantEveryHours { get; internal set; }
        public int ApplicantJitterHours { get; internal set; }
        public int ApplicantWaitDays { get; internal set; }
        public Fixed AcceptChance { get; internal set; }
        public Fixed SeveranceShare { get; internal set; }
        public int LapseGraceDays { get; internal set; }
        public int MaxOrders { get; internal set; }
        public int MaxHandHuts { get; internal set; }
        public int OrderActionsPerHour { get; internal set; }
    }

    /// <summary>A posted job (spec 006 §3.5).</summary>
    public sealed class JobPostDef
    {
        public long Cost { get; internal set; }
        public int Days { get; internal set; }
        public Fixed RateFactor { get; internal set; }
        public Fixed AboveMarketFactor { get; internal set; }
        public int AboveMarketSkillBonus { get; internal set; }
        public Fixed AboveMarketAccept { get; internal set; }
    }

    /// <summary>The hiring board (spec 015 §3.2, §3.3, §3.8): the reference check, the offer and its chances, the starting reputation.</summary>
    public sealed class HiringDef
    {
        public long ReferenceCost { get; internal set; }
        public int ReferenceHours { get; internal set; }
        public int ReferenceHalfWidth { get; internal set; }
        public Fixed LowFactor { get; internal set; }
        public Fixed HighFactor { get; internal set; }
        public Fixed LowAccept { get; internal set; }
        public Fixed HighAccept { get; internal set; }
        public Fixed TownLowAccept { get; internal set; }
        public int ReputationStart { get; internal set; }
    }

    /// <summary>
    /// A worker's output and training (spec 015 §3.4, §3.5): motivation's weights and bounds, fatigue, long days and their overtime,
    /// and the course.
    /// </summary>
    public sealed class WorkDef
    {
        public Fixed MotivationMin { get; internal set; }
        public Fixed MotivationMax { get; internal set; }
        public Fixed FairAbove { get; internal set; }
        public Fixed FairBelow { get; internal set; }
        public int HomeBelow { get; internal set; }
        public Fixed HomePerPoint { get; internal set; }
        public Fixed FatigueUp { get; internal set; }
        public Fixed FatigueDown { get; internal set; }
        public Fixed FatigueMax { get; internal set; }

        /// <summary>Hours a long day adds to the normal day (a hand's 8, a business's open hours).</summary>
        public int LongDayHours { get; internal set; }
        public Fixed OvertimeFactor { get; internal set; }
        public long CourseCost { get; internal set; }
        public int CourseDays { get; internal set; }
        public int CourseGain { get; internal set; }
    }

    /// <summary>A trait (spec 006 §3.7, spec 015 §3.6). Factors are 1 when the trait has no such effect.</summary>
    public sealed class TraitDef
    {
        public string Id { get; }
        public string NameKey { get; }
        public Fixed HoursFactor { get; }
        public bool KeepsWage { get; }
        public Fixed SkillGainFactor { get; }
        public Fixed RenewalWageFactor { get; }
        public bool Careless { get; }

        /// <summary>A business's goods sell for this much more (creative).</summary>
        public Fixed SaleFactor { get; internal set; } = Fixed.One;

        /// <summary>A business's power, fuel and supplies cost this (frugal).</summary>
        public Fixed CostFactor { get; internal set; } = Fixed.One;

        /// <summary>Their household saves half its leisure (frugal).</summary>
        public bool SavesLeisure { get; internal set; }

        /// <summary>Added to each colleague's motivation (sociable).</summary>
        public Fixed ColleagueMotivation { get; internal set; }

        /// <summary>Never spoils a batch (perfectionist).</summary>
        public bool NoBadBatch { get; internal set; }

        /// <summary>The trait never drawn with this one (−1 if none): never hardworking with lazy, or careless with perfectionist.</summary>
        public int Opposite { get; internal set; } = -1;

        public TraitDef(string id, string nameKey, Fixed hoursFactor, bool keepsWage, Fixed skillGainFactor, Fixed renewalWageFactor, bool careless)
        {
            Id = id;
            NameKey = nameKey;
            HoursFactor = hoursFactor;
            KeepsWage = keepsWage;
            SkillGainFactor = skillGainFactor;
            RenewalWageFactor = renewalWageFactor;
            Careless = careless;
        }
    }

    /// <summary>A given name for neighbours and applicants; the gender picks the sprite (ADR 0039).</summary>
    public sealed class NameDef
    {
        public string Id { get; }
        public string NameKey { get; }
        public bool Female { get; }

        public NameDef(string id, string nameKey, bool female)
        {
            Id = id;
            NameKey = nameKey;
            Female = female;
        }
    }

    /// <summary>
    /// Everything in <c>content/people.json</c> (spec 006 §4). Optional content: a game whose buildings have no hand's
    /// hut has no people (the frozen golden content), and its state hashes as before.
    /// </summary>
    public sealed class PeopleDefs
    {
        public const string HandHutId = "hand_hut";

        public LabourPoolDef Pool { get; }
        public HandDef Hand { get; }
        public JobPostDef JobPost { get; }
        public HiringDef Hiring { get; }
        public WorkDef Work { get; }
        public IReadOnlyList<TraitDef> Traits { get; }
        public IReadOnlyList<NameDef> Names { get; }

        /// <summary>Hut places on each acre of the labour quarters (ADR 0040).</summary>
        public int HutPlacesPerAcre { get; }

        /// <summary>Index of the hand's hut in <see cref="GameDefs.Buildings"/>.</summary>
        public int HandHut { get; }

        public PeopleDefs(LabourPoolDef pool, HandDef hand, JobPostDef jobPost, HiringDef hiring, WorkDef work, IReadOnlyList<TraitDef> traits, IReadOnlyList<NameDef> names,
            int hutPlacesPerAcre, int handHut)
        {
            Pool = pool;
            Hand = hand;
            JobPost = jobPost;
            Hiring = hiring;
            Work = work;
            Traits = new List<TraitDef>(traits).AsReadOnly();
            Names = new List<NameDef>(names).AsReadOnly();
            HutPlacesPerAcre = hutPlacesPerAcre;
            HandHut = handHut;
        }

        public int IndexOfTrait(string id) { for (int i = 0; i < Traits.Count; i++) if (Traits[i].Id == id) return i; return -1; }

        /// <summary>Whether two traits can't go together: the same, or opposites (spec 015 §3.6).</summary>
        public bool Clash(int a, int b) { return a == b || Traits[a].Opposite == b || Traits[b].Opposite == a; }
    }
}
