using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>benefits.json</c> (spec 028 §3.8): the four benefits' costs and effects, loyalty, and the night shift.</summary>
        public static BenefitsDef LoadBenefits(string json)
        {
            const string root = "benefits";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "mealCost", "mealMotivation", "healthCost", "healthMotivation", "healthSick", "festivalCost", "festivalMotivation", "festivalReputation", "leaveCost", "leaveMotivation",
                "leaveDays", "leaveOf", "leaveFatigue", "loyalty", "keptForReputation", "nightWageUp", "nightFatigue", "nightStart", "nightEnd", "nightNeedDays", "nightLookBack");
            Fixed F(string n) { return Number(Required(o, root, n), Join(root, n)); }
            int I(string n) { return Int(Required(o, root, n), Join(root, n)); }
            long M(string n) { return Money(Required(o, root, n), Join(root, n)); }
            var d = new BenefitsDef
            {
                MealCost = M("mealCost"), MealMotivation = F("mealMotivation"), HealthCost = M("healthCost"), HealthMotivation = F("healthMotivation"), HealthSick = F("healthSick"),
                FestivalCost = M("festivalCost"), FestivalMotivation = F("festivalMotivation"), FestivalReputation = I("festivalReputation"), LeaveCost = M("leaveCost"),
                LeaveMotivation = F("leaveMotivation"), LeaveDays = I("leaveDays"), LeaveOf = I("leaveOf"), LeaveFatigue = F("leaveFatigue"), Loyalty = F("loyalty"),
                KeptForReputation = I("keptForReputation"), NightWageUp = F("nightWageUp"), NightFatigue = F("nightFatigue"), NightStart = I("nightStart"), NightEnd = I("nightEnd"),
                NightNeedDays = I("nightNeedDays"), NightLookBack = I("nightLookBack"),
            };
            foreach (string c in new[] { "mealCost", "healthCost", "festivalCost", "leaveCost" })
                if (M(c) < 0) throw new ContentException(Join(root, c), "not below 0");
            foreach (string c in new[] { "mealMotivation", "healthMotivation", "festivalMotivation", "leaveMotivation" })
                Range(F(c), Fixed.Zero, Fixed.FromRatio(1, 2), Join(root, c));
            Range(d.HealthSick, Fixed.Zero, Fixed.One, Join(root, "healthSick"));
            Range(d.Loyalty, Fixed.FromRatio(1, 2), Fixed.One, Join(root, "loyalty"));
            Range(d.LeaveOf, 1, 366, Join(root, "leaveOf"));
            Range(d.LeaveDays, 0, d.LeaveOf, Join(root, "leaveDays"));
            Range(d.LeaveFatigue, Fixed.Zero, Fixed.One, Join(root, "leaveFatigue"));
            Range(d.KeptForReputation, 1, 4, Join(root, "keptForReputation"));
            Range(d.NightWageUp, Fixed.Zero, Fixed.One, Join(root, "nightWageUp"));
            Range(d.NightFatigue, Fixed.Zero, Fixed.One, Join(root, "nightFatigue"));
            Range(d.NightStart, 12, 23, Join(root, "nightStart"));
            Range(d.NightEnd, 0, 11, Join(root, "nightEnd"));
            Range(d.NightLookBack, 1, 16, Join(root, "nightLookBack"));
            Range(d.NightNeedDays, 1, d.NightLookBack, Join(root, "nightNeedDays"));
            return d;
        }
    }
}
