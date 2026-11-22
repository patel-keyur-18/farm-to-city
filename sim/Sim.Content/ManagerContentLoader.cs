using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>managers.json</c> (spec 028 §3.2 and §4): the wage, the mis-set days by stars and the styles.</summary>
        public static ManagersDef LoadManagers(string json)
        {
            const string root = "managers";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "wageBase", "wageProfitShare", "starPoints", "missDay", "missOutput", "profitGoalRise", "growthGoalRise", "missedMood", "styles");
            var def = new ManagersDef
            {
                WageBase = Money(Required(o, root, "wageBase"), Join(root, "wageBase")),
                WageProfitShare = Number(Required(o, root, "wageProfitShare"), Join(root, "wageProfitShare")),
                StarPoints = Int(Required(o, root, "starPoints"), Join(root, "starPoints")),
                MissOutput = Number(Required(o, root, "missOutput"), Join(root, "missOutput")),
                ProfitGoalRise = Number(Required(o, root, "profitGoalRise"), Join(root, "profitGoalRise")),
                GrowthGoalRise = Number(Required(o, root, "growthGoalRise"), Join(root, "growthGoalRise")),
                MissedMood = Number(Required(o, root, "missedMood"), Join(root, "missedMood")),
            };
            if (def.WageBase < 0) throw new ContentException(Join(root, "wageBase"), "not below 0");
            Range(def.WageProfitShare, Fixed.Zero, Fixed.One, Join(root, "wageProfitShare"));
            Range(def.StarPoints, 10, 50, Join(root, "starPoints"));
            Range(def.MissOutput, Fixed.Zero, Fixed.One, Join(root, "missOutput"));
            Range(def.ProfitGoalRise, Fixed.One, Fixed.FromInt(2), Join(root, "profitGoalRise"));
            Range(def.GrowthGoalRise, Fixed.One, Fixed.FromInt(2), Join(root, "growthGoalRise"));
            Range(def.MissedMood, -Fixed.One, Fixed.Zero, Join(root, "missedMood"));
            var miss = new List<Fixed>();
            var marr = Array(Required(o, root, "missDay"), Join(root, "missDay"));
            if (marr.Count != 5) throw new ContentException(Join(root, "missDay"), "five values, one for each of 1 to 5 stars");
            for (int i = 0; i < 5; i++)
            {
                Fixed f = Number(marr[i], Index(Join(root, "missDay"), i));
                Range(f, Fixed.Zero, Fixed.One, Index(Join(root, "missDay"), i));
                if (i > 0 && f > miss[i - 1]) throw new ContentException(Index(Join(root, "missDay"), i), "more stars never mis-set more days");
                miss.Add(f);
            }
            def.MissDay = miss;
            var styles = new List<ManagerStyleDef>();
            var sarr = Array(Required(o, root, "styles"), Join(root, "styles"));
            if (sarr.Count == 0) throw new ContentException(Join(root, "styles"), "at least one style");
            for (int i = 0; i < sarr.Count; i++)
            {
                string at = Index(Join(root, "styles"), i);
                JObject s = Object(sarr[i], at);
                OnlyFields(s, at, "id", "nameKey", "priceFactor", "quality", "stockDays", "offer", "pick");
                string offer = String(Required(s, at, "offer"), Join(at, "offer")), pick = String(Required(s, at, "pick"), Join(at, "pick"));
                var style = new ManagerStyleDef
                {
                    Id = String(Required(s, at, "id"), Join(at, "id")), NameKey = String(Required(s, at, "nameKey"), Join(at, "nameKey")),
                    PriceFactor = Number(Required(s, at, "priceFactor"), Join(at, "priceFactor")), Quality = Int(Required(s, at, "quality"), Join(at, "quality")),
                    StockDays = Int(Required(s, at, "stockDays"), Join(at, "stockDays")),
                    Offer = offer == "low" ? ManagerOffer.Low : offer == "asked" ? ManagerOffer.Asked : offer == "high" ? ManagerOffer.High : throw new ContentException(Join(at, "offer"), "low, asked or high"),
                    PicksCheapest = pick == "cheapest" ? true : pick == "best" ? false : throw new ContentException(Join(at, "pick"), "cheapest or best"),
                };
                Range(style.PriceFactor, Fixed.FromRatio(1, 2), Fixed.FromInt(2), Join(at, "priceFactor"));
                Range(style.Quality, 1, 2, Join(at, "quality"));
                Range(style.StockDays, 1, 7, Join(at, "stockDays"));
                foreach (ManagerStyleDef x in styles) if (x.Id == style.Id) throw new ContentException(Join(at, "id"), "'" + style.Id + "' is listed twice");
                styles.Add(style);
            }
            def.Styles = styles;
            return def;
        }
    }
}
