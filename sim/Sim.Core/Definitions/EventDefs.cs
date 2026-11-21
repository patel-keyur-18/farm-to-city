using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>The nine effect kinds a choice may have (spec 027 §3.2), and the few the engine adds for a choice that has to do something exact.</summary>
    public enum EffectKind
    {
        Cash = 0,
        CropHealth = 1,
        PriceShift = 2,
        CostShift = 3,
        Happiness = 4,
        Offer = 5,
        Flag = 6,
        Downtime = 7,
        Grant = 8,
        Resow = 9,
        SellNow = 10,
        Approval = 11,
        Mediate = 12,
    }

    /// <summary>One effect of a choice. Only the fields its kind uses are set.</summary>
    public sealed class EffectDef
    {
        public EffectKind Kind { get; internal set; }

        /// <summary>Cash: what the amount is for: <c>flat</c>, <c>rainfedAcre</c>, <c>field</c>, <c>person</c>, <c>bill</c> or <c>halfBill</c>.</summary>
        public string Unit { get; internal set; }
        public long Amount { get; internal set; }

        /// <summary>CropHealth: <c>rainfed</c>, <c>all</c>, <c>wet</c> or <c>third</c>, and the multiplier.</summary>
        public string Filter { get; internal set; }
        public Fixed Factor { get; internal set; }

        /// <summary>PriceShift and CostShift: the percent and the days (0: the season). CostShift's <c>Target</c> is <c>fuel</c> or <c>inputs</c>; Downtime's is <c>business</c> or <c>machine</c>.</summary>
        public int Percent { get; internal set; }
        public int Days { get; internal set; }
        public string Target { get; internal set; }

        /// <summary>Happiness: <c>both</c>, <c>one</c> or <c>other</c>, the points (days 0: until the next close). Approval: the points.</summary>
        public string Who { get; internal set; }
        public int Points { get; internal set; }

        /// <summary>Offer: <c>worker</c>, <c>family</c> or <c>parcel</c>. Flag: its name, for <c>Days</c> (0: the season). Downtime and Mediate: the hours.</summary>
        public string What { get; internal set; }
        public string Name { get; internal set; }
        public int Hours { get; internal set; }
    }

    public sealed class EventChoiceDef
    {
        public string Id { get; internal set; }
        public IReadOnlyList<EffectDef> Effects { get; internal set; }
    }

    /// <summary>One event (spec 027 §3.2): a family, minor or major, its weight modifiers' numbers (§3.3) and two or three choices; the default is the first unless set.</summary>
    public sealed class EventDef
    {
        public const int Weather = 0, Economic = 1, Business = 2, Workforce = 3, Social = 4, Opportunity = 5, FamilyDisaster = 6;

        public string Id { get; internal set; }
        public int Family { get; internal set; }
        public bool Major { get; internal set; }
        public bool Disaster { get; internal set; }

        /// <summary>False for a card the engine raises itself (the fire's repair choice).</summary>
        public bool Rolled { get; internal set; } = true;
        public IReadOnlyList<EventChoiceDef> Choices { get; internal set; }
        public int Default { get; internal set; }

        /// <summary>The numbers its weight and gate use (§3.3), by name.</summary>
        public IReadOnlyDictionary<string, Fixed> Mods { get; internal set; }

        public Fixed Mod(string name) { return Mods.TryGetValue(name, out Fixed f) ? f : Fixed.Zero; }
    }

    /// <summary>The engine's numbers (spec 027 §3.1 and §4).</summary>
    public sealed class EventEngineDef
    {
        public Fixed MinorChance { get; internal set; }
        public Fixed MajorChance { get; internal set; }
        public int MaxWaiting { get; internal set; }
        public int MinorHours { get; internal set; }
        public int MajorHours { get; internal set; }
        public int WarningHours { get; internal set; }
        public int RepairHours { get; internal set; }
        public int SlideDays { get; internal set; }
        public int GraceDays { get; internal set; }
        public Fixed CashShareCap { get; internal set; }
        public int BaseWeight { get; internal set; }
        public int DisasterWeight { get; internal set; }
        public Fixed WeightMin { get; internal set; }
        public Fixed WeightMax { get; internal set; }
        public int KeepHistory { get; internal set; }
    }

    public sealed class FireDef
    {
        public Fixed DamageShare { get; internal set; }
        public Fixed TowerFactor { get; internal set; }
        public Fixed PrepareFactor { get; internal set; }
        public int DowntimeDays { get; internal set; }
        public Fixed SlowRepairShare { get; internal set; }
        public int SlowRepairDays { get; internal set; }
    }

    public sealed class EpidemicDef
    {
        public int Days { get; internal set; }
        public Fixed SickFactor { get; internal set; }
        public Fixed ClinicFactor { get; internal set; }
        public Fixed CampFactor { get; internal set; }
        public Fixed CloseFactor { get; internal set; }
        public int CloseDays { get; internal set; }
        public Fixed CloseShopsDemand { get; internal set; }
    }

    /// <summary>Events (spec 027, <c>content/events.json</c>): the engine, its 19 events (the investor's offer comes with spec 028), the fire and the epidemic.</summary>
    public sealed class EventsDef
    {
        public const string Fire = "fire", Epidemic = "epidemic", FireRepair = "fire_repair";

        public EventEngineDef Engine { get; internal set; }
        public FireDef FireDef { get; internal set; }
        public EpidemicDef EpidemicDef { get; internal set; }
        public IReadOnlyList<EventDef> Events { get; internal set; }

        public int IndexOf(string id)
        {
            for (int i = 0; i < Events.Count; i++) if (Events[i].Id == id) return i;
            return -1;
        }
    }
}
