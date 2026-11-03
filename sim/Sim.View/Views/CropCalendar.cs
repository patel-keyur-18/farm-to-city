using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>One crop in a crop-calendar cell: its picture, its name, and its page.</summary>
    public sealed class CalendarCropVm
    {
        public string CropId { get; internal set; }
        public string Name { get; internal set; }

        /// <summary>The crop's picture (<c>icon_good_&lt;id&gt;</c>, spec 020 AC-18).</summary>
        public string Icon { get; internal set; }
        public ActionVm Open { get; internal set; }
    }

    /// <summary>A rung of the ladder (or fodder): its label and, for each season, the crops sown in it on this rung.</summary>
    public sealed class CalendarRowVm
    {
        public string Label { get; internal set; }
        public bool Fodder { get; internal set; }

        /// <summary>Four cells, Spring to Winter; an empty cell has no crop.</summary>
        public List<List<CalendarCropVm>> Cells { get; } = new List<List<CalendarCropVm>>();
    }

    public sealed class CropCalendarVm
    {
        public string Title { get; internal set; }
        public string Subtitle { get; internal set; }

        /// <summary>The four seasons' names, Spring to Winter.</summary>
        public List<string> Seasons { get; } = new List<string>();

        /// <summary>This season (0–3), marked on the grid with <see cref="NowText"/>.</summary>
        public int Now { get; internal set; }
        public string NowText { get; internal set; }
        public List<CalendarRowVm> Rows { get; } = new List<CalendarRowVm>();
    }

    /// <summary>
    /// The crop calendar (spec 020 §3.6b, G22): every crop in content by season and by rung of the ladder, today's season marked,
    /// fodder once cattle unlock, no money. A crop opens its page. Nothing is sown from here.
    /// </summary>
    public static class CropCalendar
    {
        /// <summary>Sheet kinds: the grid and a crop's page (its id is the crop's index).</summary>
        public const string Sheet = "CropCalendar", PageSheet = "CropPage";

        /// <summary>The page's Why? keys, each with the crop's index as its id.</summary>
        public const string GrowsWhy = "cropGrows", WaterWhy = "cropWater", LabourWhy = "cropLabour", SoilWhy = "cropSoil",
            SpoilsWhy = "cropSpoils", SeedWhy = "cropSeed";

        public static CropCalendarVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            FarmDefs farm = sim.Config.Game.Farm;
            var s = ctx.Strings;
            var vm = new CropCalendarVm
            {
                Title = s.Get("calendar.title"),
                Subtitle = s.Get("calendar.subtitle"),
                Now = (int)Calendar.SeasonAt(ctx.Now, sim.Clock),
                NowText = s.Get("calendar.now"),
            };
            for (int season = 0; season < 4; season++) vm.Seasons.Add(s.Get("season." + ((Season)season).ToString().ToLowerInvariant()));

            // Field crops on their rungs (ui.json's upper bounds; the last rung is everything longer), quickest first.
            IReadOnlyList<int> bounds = ctx.Ui.CalendarRungMinutes;
            var order = OrderedCrops(farm);
            for (int rung = 0; rung <= bounds.Count; rung++)
            {
                var row = new CalendarRowVm();
                long lo = long.MaxValue, hi = 0;
                for (int season = 0; season < 4; season++) row.Cells.Add(new List<CalendarCropVm>());
                foreach (int i in order)
                {
                    CropDef c = farm.Crops[i];
                    if (c.Fodder || Rung(bounds, c.GrowthMinutes) != rung) continue;
                    lo = System.Math.Min(lo, c.GrowthSeconds);
                    hi = System.Math.Max(hi, c.GrowthSeconds);
                    for (int season = 0; season < 4; season++) if (c.SowSeasons[season]) row.Cells[season].Add(Cell(ctx, i));
                }
                if (hi == 0) continue; // no crop on this rung
                row.Label = lo == hi ? s.Get("calendar.rungAbout", "time", ctx.Fmt.Duration(lo))
                    : lo % 3600 == 0 && hi % 3600 == 0 ? s.Get("calendar.rungHours", "from", ctx.Fmt.Integer(lo / 3600), "to", ctx.Fmt.Integer(hi / 3600))
                    : s.Get("calendar.rungRange", "from", ctx.Fmt.Duration(lo), "to", ctx.Fmt.Duration(hi));
                vm.Rows.Add(row);
            }

            // Fodder, once cattle unlock (spec 012 §3.8b).
            if (Sim.Core.Livestock.LivestockRules.Unlocked(sim))
            {
                var fodder = new CalendarRowVm { Label = s.Get("calendar.fodder"), Fodder = true };
                for (int season = 0; season < 4; season++) fodder.Cells.Add(new List<CalendarCropVm>());
                foreach (int i in order)
                    if (farm.Crops[i].Fodder)
                        for (int season = 0; season < 4; season++) if (farm.Crops[i].SowSeasons[season]) fodder.Cells[season].Add(Cell(ctx, i));
                vm.Rows.Add(fodder);
            }
            return vm;
        }

        /// <summary>The rung a growing time falls on: the first upper bound it is within, else the last rung.</summary>
        public static int Rung(IReadOnlyList<int> bounds, int growthMinutes)
        {
            for (int r = 0; r < bounds.Count; r++) if (growthMinutes <= bounds[r]) return r;
            return bounds.Count;
        }

        /// <summary>Crop indices quickest first, ties in content order.</summary>
        private static List<int> OrderedCrops(FarmDefs farm)
        {
            var list = new List<int>();
            for (int i = 0; i < farm.Crops.Count; i++) list.Add(i);
            return System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(list, i => farm.Crops[i].GrowthSeconds));
        }

        private static CalendarCropVm Cell(ViewContext ctx, int crop)
        {
            CropDef c = ctx.Sim.Config.Game.Farm.Crops[crop];
            return new CalendarCropVm
            {
                CropId = c.Id, Name = ctx.Strings.Get(c.NameKey), Icon = c.Id,
                Open = ActionVm.Open("crop:" + c.Id, ctx.Strings.Get(c.NameKey), new SheetRequest(PageSheet, crop)),
            };
        }

        /// <summary>
        /// A crop's page (spec 020 §3.6b): its seasons, growing time, the water it likes, the hours a day it needs, what it does to
        /// the soil, how fast it spoils and whether its seed can be saved, each number with its Why?. Nothing is sown from here.
        /// </summary>
        public static SheetVm Page(ViewContext ctx, int crop)
        {
            FarmDefs farm = ctx.Sim.Config.Game.Farm;
            CropDef c = farm.Crops[crop];
            var s = ctx.Strings;
            var vm = new SheetVm
            {
                Icon = c.Id,
                Title = s.Get(c.NameKey),
                Subtitle = s.Has(c.NameKey + ".note") ? s.Get(c.NameKey + ".note") : null,
            };
            vm.Lines.Add(new LineVm(s.Get("cropPage.sowIn"), FieldSheets.SeasonsText(ctx, c.SowSeasons)));
            if (c.Fodder) vm.Lines.Add(new LineVm(s.Get("cropPage.cutIn"), FieldSheets.SeasonsText(ctx, c.CutSeasons)));
            vm.Lines.Add(new LineVm(s.Get("cropPage.grows"), ctx.Fmt.Duration(c.GrowthSeconds), GrowsWhy, crop));
            vm.Lines.Add(new LineVm(s.Get("cropPage.water"), s.Get("cropPage.waterValue", "min", Num(ctx, c.MoistureMin), "max", Num(ctx, c.MoistureMax)), WaterWhy, crop));
            vm.Lines.Add(new LineVm(s.Get("cropPage.hours"), s.Get("cropPage.hoursValue", "hours", Hours(c.LabourPerAcrePerDay)), LabourWhy, crop));
            vm.Lines.Add(new LineVm(s.Get("cropPage.soil"), SoilText(ctx, c), SoilWhy, crop));
            if (c.OutputGood >= 0)
                vm.Lines.Add(new LineVm(s.Get("cropPage.store"), s.Get("cropPage.storeValue", "pct", ctx.Fmt.Percent(ctx.Sim.Config.Game.Market.Goods[c.OutputGood].SpoilagePerDay)), SpoilsWhy, crop));
            vm.Lines.Add(c.CanSaveSeed
                ? new LineVm(s.Get("cropPage.seed"), s.Get("cropPage.seedSaved", "pct", ctx.Fmt.Percent(c.SavedSeedLossPerSeason)), SeedWhy, crop)
                : new LineVm(s.Get("cropPage.seed"), s.Get("cropPage.seedBought")));
            return vm;
        }

        /// <summary>"Takes 3.0 points a season", "Gives back 3.0 points a season", or "Leaves it as it is".</summary>
        internal static string SoilText(ViewContext ctx, CropDef c)
        {
            if (c.FertilityPerSeason == Fixed.Zero) return ctx.Strings.Get("cropPage.soilSame");
            bool gives = c.FertilityPerSeason < Fixed.Zero;
            Fixed n = gives ? Fixed.Zero - c.FertilityPerSeason : c.FertilityPerSeason;
            return ctx.Strings.Get(gives ? "cropPage.soilGives" : "cropPage.soilTakes", "n", ctx.Fmt.Decimal1(n));
        }

        internal static string Num(ViewContext ctx, Fixed v) { return ctx.Fmt.Integer(v.RoundToLong()); }

        /// <summary>One decimal: 4.5.</summary>
        internal static string Hours(Fixed h)
        {
            long tenths = (h * Fixed.FromInt(10)).RoundToLong();
            return (tenths / 10) + "." + (System.Math.Abs(tenths) % 10);
        }
    }
}
