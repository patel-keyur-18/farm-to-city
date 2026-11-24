using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.History;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// The Almanac card at each Winter close and the Chronicle's Almanacs tab (spec 029 §3.4, §3.5, §7, Preview 19): the year's people, net
    /// worth, best return, costliest loss and one person to remember, each with a <b>?</b>.
    /// </summary>
    public static class AlmanacSheet
    {
        public const string Kind = "Almanac", Why = "almanac";

        public const int PeopleRow = 0, WorthRow = 1, BestRow = 2, LossRow = 3, SpotRow = 4;

        public static long WhyId(long year, int row) { return year * 10 + row; }

        public static AlmanacEntry Find(ViewContext ctx, long year)
        {
            AlmanacState a = ctx.Sim.State.Chronicle?.Almanac;
            return a?.Entries.Find(e => e.Year == year);
        }

        /// <summary>The year's best return in words: "Bakery, 38 for every 100", or "a quiet year".</summary>
        private static string BestShort(ViewContext ctx, AlmanacEntry e)
        {
            var s = ctx.Strings;
            if (e.Best == AlmanacBest.None) return s.Get("almanac.bestQuietValue");
            return s.Get("almanac.bestShort", "name", Name(ctx, e), "n", ctx.Fmt.Integer(PerHundred(e)));
        }

        internal static long PerHundred(AlmanacEntry e) { return e.BestPut <= 0 ? 0 : e.BestProfit * 100 / e.BestPut; }

        private static string Name(ViewContext ctx, AlmanacEntry e)
        {
            return e.Best == AlmanacBest.Business ? ctx.Strings.Get("business." + e.BestId) : ctx.Strings.Get("crop." + e.BestId);
        }

        private static string Person(ViewContext ctx, AlmanacEntry e)
        {
            Citizen c = e.Spot == AlmanacSpot.None ? null : ctx.Sim.State.Citizens?.Citizen(e.SpotCitizen);
            return c == null ? null : PeopleWords.Name(ctx, c.Name);
        }

        private static string SpotLine(ViewContext ctx, AlmanacEntry e, string name)
        {
            if (e.Spot == AlmanacSpot.Arc) // the person's story that moved: its latest line in the Chronicle
            {
                ChronicleEntry last = ctx.Sim.State.Chronicle.Entries.FindLast(x => x.Kind == ChronicleKind.Story && x.A == e.SpotCitizen && x.At <= e.At);
                if (last != null) return StoryWords.Line(ctx, last.A, last.B, last.Id);
            }
            return ctx.Strings.Get("almanac.spot." + (int)e.Spot, "name", name);
        }

        private static string LossCause(ViewContext ctx, AlmanacEntry e)
        {
            string name = e.LossId == "" ? "" : ctx.Strings.Get(e.Loss == AlmanacLoss.MachineSold ? "machine." + e.LossId : "business." + e.LossId);
            return ctx.Strings.Get("almanac.loss." + (int)e.Loss, "name", name.ToLowerInvariant());
        }

        /// <summary>The card: Year {n} in the valley.</summary>
        public static SheetVm Build(ViewContext ctx, long year)
        {
            var s = ctx.Strings;
            AlmanacEntry e = Find(ctx, year);
            if (e == null) return new SheetVm { Icon = "scroll", Title = s.Get("almanac.title", "n", ctx.Fmt.Integer(year)), Subtitle = s.Get("almanac.missing") };
            var vm = new SheetVm { Icon = "scroll", Title = s.Get("almanac.title", "n", ctx.Fmt.Integer(year)), Subtitle = s.Get("almanac.subtitle") };

            vm.Lines.Add(new LineVm(s.Get("almanac.people"), s.Get("almanac.peopleValue", "n", ctx.Fmt.Integer(e.People)), Why, WhyId(year, PeopleRow)) { Icon = "person_hand_m", Note = e.Village > 0 ? s.Get("almanac.peopleNote", "n", ctx.Fmt.Integer(e.Households), "m", ctx.Fmt.Integer(e.Village)) : s.Get("almanac.peopleNoteOwn", "n", ctx.Fmt.Integer(e.Households)) });

            long change = e.NetWorth - e.Before;
            vm.Lines.Add(new LineVm(s.Get("almanac.worth"), ctx.Fmt.Coins(e.NetWorth), Why, WhyId(year, WorthRow))
                { Icon = "coin", Note = !e.HadBefore ? s.Get("almanac.worthFirst") : change == 0 ? s.Get("almanac.worthSame") : s.Get(change > 0 ? "almanac.worthUp" : "almanac.worthDown", "amount", ctx.Fmt.Coins(System.Math.Abs(change))), NoteTone = change < 0 && e.HadBefore ? Tone.Warn : Tone.Plain });

            if (e.Best == AlmanacBest.None)
                vm.Lines.Add(new LineVm(s.Get("almanac.best"), s.Get("almanac.bestQuietValue"), Why, WhyId(year, BestRow)) { Icon = "scroll", Note = s.Get("almanac.bestQuiet", "min", ctx.Fmt.Coins(ctx.Sim.Config.Game.Chronicle.MinProfit)) });
            else
                vm.Lines.Add(new LineVm(s.Get("almanac.best"), ctx.Fmt.Coins(e.BestProfit), Why, WhyId(year, BestRow))
                    { Icon = e.Best == AlmanacBest.Business ? e.BestId : "field", Note = s.Get("almanac.bestNote", "name", Name(ctx, e).ToLowerInvariant(), "n", ctx.Fmt.Integer(PerHundred(e))), NoteTone = Tone.Good });

            if (e.Loss == AlmanacLoss.None)
                vm.Lines.Add(new LineVm(s.Get("almanac.loss"), s.Get("almanac.lossNoneValue"), Why, WhyId(year, LossRow)) { Icon = "gear", Note = s.Get("almanac.lossNone") });
            else
                vm.Lines.Add(new LineVm(s.Get("almanac.loss"), MoneySheet.Signed(ctx, -e.LossAmount), Why, WhyId(year, LossRow)) { Icon = "gear", Note = LossCause(ctx, e), NoteTone = Tone.Warn });

            string who = Person(ctx, e);
            if (who == null)
                vm.Lines.Add(new LineVm(s.Get("almanac.spotQuiet"), null, Why, WhyId(year, SpotRow)) { Icon = "person_hand_m", Note = s.Get("almanac.spotQuietNote") });
            else
            {
                Citizen c = ctx.Sim.State.Citizens.Citizen(e.SpotCitizen);
                vm.Lines.Add(new LineVm(who, null, Why, WhyId(year, SpotRow)) { Icon = TownWords.Portrait(ctx, c), Note = SpotLine(ctx, e, who) });
            }
            vm.Actions.Add(ActionVm.Open("almanacChronicle", s.Get("almanac.openChronicle"), new SheetRequest(ChronicleSheet.Kind, 0, ChronicleSheet.Almanacs)));
            vm.Actions.Add(new ActionVm(CloseId, s.Get("action.close"), null, ActionKind.Primary, true, null, null));
            return vm;
        }

        /// <summary>The card's own Close button: the host just puts the sheet away.</summary>
        public const string CloseId = "almanacClose";

        /// <summary>The Chronicle's Almanacs tab: the last 8, newest first, each opening its card.</summary>
        internal static void List(ViewContext ctx, SheetVm vm)
        {
            var s = ctx.Strings;
            AlmanacState a = ctx.Sim.State.Chronicle.Almanac;
            if (a.Entries.Count == 0) { vm.Lines.Add(new LineVm(s.Get("chronicle.almanacsEmpty"), null)); return; }
            for (int i = a.Entries.Count - 1; i >= 0; i--)
            {
                AlmanacEntry e = a.Entries[i];
                string who = Person(ctx, e);
                string note = s.Get("almanac.rowNote", "worth", ctx.Fmt.Coins(e.NetWorth), "best", BestShort(ctx, e));
                if (who != null) note += "\n" + SpotLine(ctx, e, who);
                vm.Lines.Add(new LineVm(s.Get("almanac.title", "n", ctx.Fmt.Integer(e.Year)), null) { Icon = "scroll", Note = note, Action = ActionVm.Open("almanac:" + e.Year, s.Get("action.open"), new SheetRequest(Kind, e.Year)) });
            }
        }

        /// <summary>A card line's Why?: the numbers and the rule behind it, in words.</summary>
        internal static WhyVm WhyOf(ViewContext ctx, long id)
        {
            var s = ctx.Strings;
            long year = id / 10;
            int row = (int)(id % 10);
            AlmanacEntry e = Find(ctx, year);
            var vm = new WhyVm { Title = s.Get("almanac.why.title." + row), Kicker = s.Get("almanac.title", "n", ctx.Fmt.Integer(year)) };
            if (e == null) return vm;
            ChronicleDefs d = ctx.Sim.Config.Game.Chronicle;
            void Row(string key, string value) { vm.Rows.Add(new WhyRowVm(s.Get(key), value)); }
            switch (row)
            {
                case PeopleRow:
                    vm.Value = ctx.Fmt.Integer(e.People);
                    Row("almanac.why.peopleOwn", ctx.Fmt.Integer(e.People));
                    Row("almanac.why.peopleHouseholds", ctx.Fmt.Integer(e.Households));
                    if (e.Village > 0) Row("almanac.why.peopleVillage", ctx.Fmt.Integer(e.Village));
                    vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.peopleRule"), null));
                    break;
                case WorthRow:
                    vm.Value = ctx.Fmt.Coins(e.NetWorth);
                    Row("almanac.why.worthNow", ctx.Fmt.Coins(e.NetWorth));
                    if (e.HadBefore) { Row("almanac.why.worthBefore", ctx.Fmt.Coins(e.Before)); Row("almanac.why.worthChange", MoneySheet.Signed(ctx, e.NetWorth - e.Before)); }
                    vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.worthRule"), null));
                    break;
                case BestRow:
                    if (e.Best == AlmanacBest.None)
                    {
                        vm.Value = s.Get("almanac.bestQuietValue");
                        vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.quiet", "min", ctx.Fmt.Coins(d.MinProfit)), null));
                    }
                    else
                    {
                        vm.Value = ctx.Fmt.Integer(PerHundred(e));
                        Row(e.Best == AlmanacBest.Business ? "almanac.why.bestProfitBusiness" : "almanac.why.bestProfitCrop", ctx.Fmt.Coins(e.BestProfit));
                        Row(e.Best == AlmanacBest.Business ? "almanac.why.bestPutBusiness" : "almanac.why.bestPutCrop", ctx.Fmt.Coins(e.BestPut));
                        Row("almanac.why.bestShare", s.Get("almanac.why.bestShareValue", "n", ctx.Fmt.Integer(PerHundred(e))));
                        vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.bestRule", "min", ctx.Fmt.Coins(d.MinProfit)), null));
                    }
                    break;
                case LossRow:
                    if (e.Loss == AlmanacLoss.None) { vm.Value = s.Get("almanac.lossNoneValue"); vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.lossNone"), null)); }
                    else
                    {
                        vm.Value = ctx.Fmt.Coins(e.LossAmount);
                        Row("almanac.why.lossCause", LossCause(ctx, e));
                        Row("almanac.why.lossCost", ctx.Fmt.Coins(e.LossAmount));
                        vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.lossRule"), null));
                    }
                    break;
                case SpotRow:
                    vm.Rows.Add(new WhyRowVm(s.Get(e.Spot == AlmanacSpot.None ? "almanac.why.spotNone" : "almanac.why.spot." + (int)e.Spot), null));
                    vm.Rows.Add(new WhyRowVm(s.Get("almanac.why.spotRule"), null));
                    break;
            }
            vm.Footnote = s.Get("almanac.why.foot", "keep", ctx.Fmt.Integer(ChronicleRules.Defs(ctx.Sim).AlmanacKeep));
            return vm;
        }
    }

    public static partial class WhyView
    {
        private static WhyVm AlmanacWhy(ViewContext ctx, long id) { return AlmanacSheet.WhyOf(ctx, id); }
    }
}
