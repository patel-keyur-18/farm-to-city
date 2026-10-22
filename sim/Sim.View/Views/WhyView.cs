using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>One cause on a Why? sheet: a plain sentence and its effect ("12% less", "no change") or its coins (spec 008 §3.6).</summary>
    public sealed class WhyRowVm
    {
        public string Label { get; }
        public string Value { get; }

        /// <summary>A short note under the row, or null.</summary>
        public string Note { get; }

        public WhyRowVm(string label, string value, string note = null)
        {
            Label = label;
            Value = value;
            Note = note;
        }
    }

    /// <summary>A Why? sheet: the number asked about, its causes in words, and the lesson (ADR 0043 §3). Never a factor.</summary>
    public sealed class WhyVm
    {
        public string Title { get; internal set; }
        public string Value { get; internal set; }
        public List<WhyRowVm> Rows { get; } = new List<WhyRowVm>();
        public string Footnote { get; internal set; }
    }

    /// <summary>
    /// Builds every Why? (spec 008 §3.6, ADR 0043 §3). A row's key (<see cref="LineVm.WhyKey"/>) and id (<see cref="LineVm.WhyId"/>)
    /// pick the source; <see cref="Has"/> says whether a key has one, so the coverage test can find a number without a Why?.
    /// </summary>
    public static partial class WhyView
    {
        private delegate WhyVm Source(ViewContext ctx, long id);

        private static readonly Dictionary<string, Source> Sources = new Dictionary<string, Source>
        {
            // Land and crops (specs 002, 003).
            { "fertility", Fertility }, { "moisture", Moisture }, { "soil", Soil }, { "timeLeft", TimeLeft }, { "care", Care },
            { "water", Water }, { "weather", Weather }, { "yield", Yield }, { "spoils", Spoils }, { "landPrice", LandPrice },
            { "resale", Resale }, { "pests", Pests },
            // The garden and trees.
            { "bedTime", BedTime }, { "gardenSeason", GardenSeason }, { "gardenCrop", GardenCrop }, { "treeMatures", TreeMatures }, { "payback", Payback },
            { "sapling", Sapling },
            // Money and the market (specs 004, 005).
            { "cash", Cash }, { "store", StoreFill }, { "price", Price }, { "marketPrice", MarketPrice }, { "traderPrice", TraderPrice },
            { "traderRoom", TraderRoom }, { "spoilage", Spoilage }, { "input", Input }, { "inputPrice", InputPrice }, { "mandi", Mandi },
            { "netWorth", NetWorth }, { "creditLimit", CreditLimitWhy }, { "loan", LoanWhy }, { "borrow", BorrowWhy },
            { "seasonSoFar", SeasonSoFar },
            // The truck and supermarket orders (spec 011).
            { "orderPrice", OrderPrice }, { "owed", Owed }, { "truck", Truck },
            // Time and weather (spec 001).
            { "calendar", CalendarWhy }, { "forecast", Forecast }, { "saved", Saved }, { "importFiles", ImportFiles }, { "playLog", PlayLogWhy }, { "textSize", TextSize },
            // The founder (spec 003).
            { "labour", Labour }, { "health", Health }, { "fatigue", Fatigue }, { "diet", Diet }, { "hours", WorkHoursWhy },
            { "dayLabour", DayLabour }, { "medicine", Medicine },
            // People (spec 006).
            { "hire", Hire }, { "hand.skill", HandSkill }, { "hand.hours", HandHours }, { "hand.wage", HandWage },
            { "hand.lapse", HandLapse }, { "hand.prepaid", HandPrepaid }, { "hut", Hut }, { "applicant", Applicant },
            { "orderMin", OrderMin }, { "order", Order }, { "post", Post }, { "free", FreeLabourers },
            // The away card (spec 008 §3.9).
            { "away", Away },
            // The Hamlet (spec 009 §3.6).
            { "era", EraWhy },
        };

        /// <summary>True when <paramref name="key"/> has a Why? source (report lines are "report.&lt;line&gt;").</summary>
        public static bool Has(string key)
        {
            return key != null && (Sources.ContainsKey(key) || key.StartsWith("report.", System.StringComparison.Ordinal));
        }

        public static WhyVm Build(ViewContext ctx, string key, long id)
        {
            if (key.StartsWith("report.", System.StringComparison.Ordinal)) return ReportLineWhy(ctx, key.Substring(7), id);
            return Sources[key](ctx, id);
        }

        // ---- Wording helpers -------------------------------------------------------------------------------------------------

        /// <summary>A factor's effect in words: "12% more", "12% less" or "no change" (never the factor itself).</summary>
        public static string Effect(ViewContext ctx, Fixed factor)
        {
            long pct = ((factor - Fixed.One) * Fixed.FromInt(100)).RoundToLong();
            if (pct == 0) return ctx.Strings.Get("why.same");
            return ctx.Strings.Get(pct > 0 ? "why.more" : "why.less", "n", ctx.Fmt.Integer(System.Math.Abs(pct)));
        }

        private static WhyVm New(ViewContext ctx, string titleKey, string value, params string[] args)
        {
            return new WhyVm { Title = ctx.Strings.Get(titleKey, args), Value = value };
        }

        private static void Row(WhyVm vm, ViewContext ctx, string labelKey, string value, params string[] args)
        {
            vm.Rows.Add(new WhyRowVm(ctx.Strings.Get(labelKey, args), value));
        }

        private static void Text(WhyVm vm, ViewContext ctx, string key, params string[] args)
        {
            vm.Rows.Add(new WhyRowVm(ctx.Strings.Get(key, args), null));
        }

        private static string Pct(ViewContext ctx, Fixed share) { return ctx.Fmt.Percent(share); }

        private static string Kg(ViewContext ctx, Fixed kg) { return ctx.Fmt.Kg((kg * Fixed.FromInt(1000)).RoundToLong()); }

        private static string Money(ViewContext ctx, long money) { return ctx.Fmt.Coins(money); }

        private static string Price(ViewContext ctx, Fixed p) { return ctx.Fmt.PricePerKg(p); }

        private static string Num(ViewContext ctx, Fixed v) { return ctx.Fmt.Integer(v.RoundToLong()); }

        private static Tile TileOf(ViewContext ctx, long id) { return ctx.Sim.State.Land.Tiles[(int)id]; }

        private static string CropName(ViewContext ctx, CropDef def) { return ctx.Strings.Get(def.NameKey); }

        private static Crop CropOn(ViewContext ctx, long tileId) { return ctx.Sim.State.Farm.CropOn((int)tileId); }

        private static string SeasonName(ViewContext ctx, Season s) { return ctx.Strings.Get("season." + s.ToString().ToLowerInvariant()); }

        private static Season SeasonNow(ViewContext ctx) { return Calendar.SeasonAt(ctx.Now, ctx.Sim.Clock); }
    }
}
