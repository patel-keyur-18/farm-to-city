using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.History;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The Chronicle's words (spec 029 §3.1): a line's sentence from its string template, its date and its icon.</summary>
    public static class ChronicleWords
    {
        public static string Date(ViewContext ctx, long at)
        {
            long period = at / ctx.Sim.Clock.SecondsPerSeason;
            return ctx.Strings.Get("report.period", "season", FieldSheets.SeasonName(ctx, period), "year", ctx.Fmt.Integer(period / 4 + 1));
        }

        public static string Year(ViewContext ctx, long at)
        {
            return ctx.Strings.Get("chronicle.year", "n", ctx.Fmt.Integer(at / ctx.Sim.Clock.SecondsPerSeason / 4 + 1));
        }

        private static string Biz(ViewContext ctx, string typeId) { return ctx.Strings.Get("business." + typeId).ToLowerInvariant(); }

        private static string Id(ChronicleKind k) { return ChronicleDefs.KindIds[(int)k]; }

        /// <summary>The line's sentence, made from the string table each time it is shown (so wording and language can change without touching a save).</summary>
        public static string Line(ViewContext ctx, ChronicleEntry e)
        {
            var s = ctx.Strings;
            string key = "chronicle." + Id(e.Kind);
            switch (e.Kind)
            {
                case ChronicleKind.Founding: return s.Get(key, "n", ctx.Fmt.Integer(e.A));
                case ChronicleKind.Hamlet:
                case ChronicleKind.Village:
                case ChronicleKind.Township: return s.Get(key, "n", ctx.Fmt.Integer(e.A));
                case ChronicleKind.FirstSettler:
                case ChronicleKind.FirstChild:
                case ChronicleKind.FirstLiterateChild:
                case ChronicleKind.FirstAdultReader: return s.Get(key, "name", PeopleWords.Name(ctx, (int)e.A));
                case ChronicleKind.FirstFestival:
                    return s.Get(key, "festival", s.Get("festival." + Sim.Core.Town.FestivalRules.Def(ctx.Sim).Kinds[(int)e.A].Id).ToLowerInvariant());
                case ChronicleKind.FirstOrchard: return s.Get(key, "tree", s.Get("tree." + e.Id).ToLowerInvariant());
                case ChronicleKind.FirstProfit:
                case ChronicleKind.BusinessFounded:
                case ChronicleKind.BusinessClosed: return s.Get(key, "business", Biz(ctx, e.Id));
                case ChronicleKind.BusinessSold: return s.Get(key, "business", Biz(ctx, e.Id), "price", ctx.Fmt.Coins(e.A));
                case ChronicleKind.RecordHarvest: return s.Get(key, "crop", s.Get("crop." + e.Id).ToLowerInvariant(), "kg", ctx.Fmt.Kg(e.A));
                case ChronicleKind.FireSurvived: return s.Get(key, "cost", ctx.Fmt.Coins(e.A));
                case ChronicleKind.EpidemicSurvived: return s.Get(key, "n", ctx.Fmt.Integer(e.A));
                case ChronicleKind.LoanCleared: return s.Get(key, "amount", ctx.Fmt.Coins(e.A), "lender", s.Get("lender." + e.Id));
                case ChronicleKind.PetitionKept: return s.Get(key, "topic", s.Get("chronicle.topic." + e.B));
                default: return s.Get(key);
            }
        }

        /// <summary>Whether a line carries numbers worth a ? (the rest are plain facts).</summary>
        public static bool HasWhy(ChronicleEntry e)
        {
            switch (e.Kind)
            {
                case ChronicleKind.Founding:
                case ChronicleKind.Hamlet:
                case ChronicleKind.Village:
                case ChronicleKind.Township:
                case ChronicleKind.RecordHarvest:
                case ChronicleKind.BusinessSold:
                case ChronicleKind.FireSurvived:
                case ChronicleKind.EpidemicSurvived:
                case ChronicleKind.LoanCleared: return true;
                default: return false;
            }
        }
    }

    /// <summary>Menu → Chronicle (spec 029 §7, Preview 19): the town's history newest first by game year, and the Almanacs.</summary>
    public static class ChronicleSheet
    {
        public const string Kind = "Chronicle", Almanacs = "almanacs", Why = "chronicle";

        public static SheetVm Build(ViewContext ctx, string tab = null)
        {
            var s = ctx.Strings;
            bool alm = tab == Almanacs;
            var vm = new SheetVm { Icon = "scroll", Title = s.Get("chronicle.title"), Subtitle = s.Get("chronicle.subtitle"), Tab = alm ? "tab:almanacs" : "tab:timeline" };
            vm.Tabs = new List<ActionVm>
            {
                ActionVm.Open("tab:timeline", s.Get("chronicle.tabTimeline"), new SheetRequest(Kind, 0)),
                ActionVm.Open("tab:almanacs", s.Get("chronicle.tabAlmanacs"), new SheetRequest(Kind, 0, Almanacs)),
            };
            if (alm) vm.Lines.Add(new LineVm(s.Get("chronicle.almanacsEmpty"), null));
            else Timeline(ctx, vm);
            return vm;
        }

        private static void Timeline(ViewContext ctx, SheetVm vm)
        {
            ChronicleState c = ctx.Sim.State.Chronicle;
            string year = null;
            for (int i = c.Entries.Count - 1; i >= 0; i--)
            {
                ChronicleEntry e = c.Entries[i];
                string y = ChronicleWords.Year(ctx, e.At);
                if (y != year) { vm.Lines.Add(LineVm.Header(y)); year = y; }
                var line = new LineVm(ChronicleWords.Line(ctx, e), null, ChronicleWords.HasWhy(e) ? Why : null, i) { Icon = ctx.Sim.Config.Game.Chronicle.Kinds[(int)e.Kind].Icon, Note = ChronicleWords.Date(ctx, e.At) };
                vm.Lines.Add(line);
            }
            if (c.Entries.Count == 0) vm.Lines.Add(new LineVm(ctx.Strings.Get("chronicle.empty"), null));
        }

        /// <summary>A line's Why?: the numbers behind it, in words (never a factor).</summary>
        internal static WhyVm WhyOf(ViewContext ctx, long index)
        {
            var s = ctx.Strings;
            ChronicleState c = ctx.Sim.State.Chronicle;
            ChronicleEntry e = index >= 0 && index < c.Entries.Count ? c.Entries[(int)index] : null;
            string id = e == null ? "founding" : ChronicleDefs.KindIds[(int)e.Kind];
            var vm = new WhyVm { Title = s.Get("chronicle.title." + id), Value = null, Kicker = e == null ? null : ChronicleWords.Date(ctx, e.At) };
            if (e == null) return vm;
            ChronicleDefs d = ctx.Sim.Config.Game.Chronicle;
            void Row(string key, string value) { vm.Rows.Add(new WhyRowVm(s.Get(key), value)); }
            switch (e.Kind)
            {
                case ChronicleKind.Founding: vm.Value = ctx.Fmt.Integer(e.A); Row("chronicle.why.founding", ctx.Fmt.Integer(e.A)); break;
                case ChronicleKind.Hamlet:
                case ChronicleKind.Village:
                case ChronicleKind.Township: vm.Value = ctx.Fmt.Integer(e.A); Row("chronicle.why.era", ctx.Fmt.Integer(e.A)); break;
                case ChronicleKind.RecordHarvest:
                    vm.Value = ctx.Fmt.Kg(e.A);
                    Row("chronicle.why.recordNow", ctx.Fmt.Kg(e.A));
                    Row("chronicle.why.recordBest", ctx.Fmt.Kg(e.B));
                    vm.Rows.Add(new WhyRowVm(s.Get("chronicle.why.recordRule", "kg", ctx.Fmt.Kg(d.RecordMinGrams), "rise", ctx.Fmt.Percent(d.RecordRise)), null));
                    break;
                case ChronicleKind.BusinessSold: vm.Value = ctx.Fmt.Coins(e.A); Row("chronicle.why.sold", ctx.Fmt.Coins(e.A)); break;
                case ChronicleKind.FireSurvived: vm.Value = ctx.Fmt.Coins(e.A); Row("chronicle.why.fire", ctx.Fmt.Coins(e.A)); break;
                case ChronicleKind.EpidemicSurvived: vm.Value = ctx.Fmt.Integer(e.A); Row("chronicle.why.epidemic", ctx.Fmt.Integer(e.A)); break;
                case ChronicleKind.LoanCleared: vm.Value = ctx.Fmt.Coins(e.A); Row("chronicle.why.loan", ctx.Fmt.Coins(e.A)); break;
            }
            vm.Footnote = s.Get("chronicle.why.foot");
            return vm;
        }
    }

    public static partial class WhyView
    {
        private static WhyVm ChronicleLine(ViewContext ctx, long id) { return ChronicleSheet.WhyOf(ctx, id); }
    }
}
