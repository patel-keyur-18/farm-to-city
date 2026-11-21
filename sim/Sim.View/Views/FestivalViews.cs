using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Time;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// Festivals (US-149, spec 026 §3.1, §7, Preview 14): this season's festival on the community hall's and the banyan square's sheets, and the sheet where you choose
    /// where, who pays and which day. A view: it reads the state and changes nothing; the choices ride in the sheet's argument (<c>venue:payer:days</c>).
    /// </summary>
    public static class FestivalSheet
    {
        public const string Kind = "Festival";

        private static string Name(ViewContext ctx, int kind) { return ctx.Strings.Get("festival." + FestivalRules.Def(ctx.Sim).Kinds[kind].Id); }

        private static string SeasonName(ViewContext ctx, int kind) { return ctx.Strings.Get("season." + ((Season)kind).ToString().ToLowerInvariant()); }

        public static string VenueName(ViewContext ctx, int venue) { return ctx.Strings.Get(venue == FestivalDef.Hall ? "festival.venue.hall" : "festival.venue.square"); }

        public static string DayName(ViewContext ctx, long daysAhead) { return daysAhead == 1 ? ctx.Strings.Get("festival.day.1") : ctx.Strings.Get("festival.dayN", "n", ctx.Fmt.Integer(daysAhead)); }

        /// <summary>The choices from a sheet argument "venue:payer:days"; the hall if it has its caretaker (else the square), the town paying, tomorrow.</summary>
        public static void Choices(Simulation sim, string arg, out int venue, out int payer, out int days)
        {
            venue = FestivalRules.VenueReady(sim, FestivalDef.Hall) ? FestivalDef.Hall : FestivalDef.Square;
            payer = FestivalDef.Town;
            days = 1;
            if (string.IsNullOrEmpty(arg)) return;
            string[] p = arg.Split(':');
            if (p.Length == 3 && int.TryParse(p[0], out int v) && int.TryParse(p[1], out int y) && int.TryParse(p[2], out int d)) { venue = v; payer = y; days = d; }
        }

        private static string Arg(int venue, int payer, int days) { return venue + ":" + payer + ":" + days; }

        /// <summary>The "Hold a festival" part of a venue's sheet: this season's festival, and a way in; or what is already planned.</summary>
        public static void Section(ViewContext ctx, SheetVm vm, int venue)
        {
            Simulation sim = ctx.Sim;
            if (!FestivalRules.On(sim)) return;
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get("festival.header")));
            int kind = FestivalRules.KindNow(sim);
            TownLifeState st = TownLifeRules.State(sim);
            if (FestivalRules.DoneThisSeason(sim))
            {
                vm.Lines.Add(new LineVm(Planned(ctx, st), null, "festival", st.FestivalKind) { Note = s.Get("festival.onceASeason") });
            }
            else
            {
                vm.Lines.Add(new LineVm(Name(ctx, kind), null, "festival", kind) { Note = s.Get("festival.seasonOne", "season", SeasonName(ctx, kind)) });
                vm.Actions.Add(ActionVm.Open("festival", s.Get("festival.hold"), new SheetRequest(Kind, 0, Arg(venue, FestivalDef.Town, 1))));
            }
            LastFestivals(ctx, vm, st);
        }

        private static string Planned(ViewContext ctx, TownLifeState st)
        {
            var s = ctx.Strings;
            long ahead = st.FestivalDay - FestivalRules.Today(ctx.Sim);
            string when = !st.FestivalOn ? s.Get("festival.over") : ahead > 0 ? DayName(ctx, ahead) : s.Get("festival.today");
            return s.Get("festival.planned", "name", Name(ctx, st.FestivalKind), "venue", VenueName(ctx, st.FestivalVenue), "when", when);
        }

        private static void LastFestivals(ViewContext ctx, SheetVm vm, TownLifeState st)
        {
            var s = ctx.Strings;
            FestivalDef d = FestivalRules.Def(ctx.Sim);
            int shown = 0;
            for (int i = st.Festivals.Count - 1; i >= 0 && shown < 4; i--)
            {
                FestivalRecord f = st.Festivals[i];
                if (shown++ == 0) vm.Lines.Add(LineVm.Header(s.Get("festival.last")));
                CalendarDate date = Calendar.From(f.Day * ClockDef.SecondsPerDay, ctx.Sim.Clock);
                string when = s.Get("festival.when", "season", s.Get("season." + date.Season.ToString().ToLowerInvariant()), "year", ctx.Fmt.Integer(date.Year));
                string paid = s.Get(f.Payer == FestivalDef.Town ? "festival.paidTown" : "festival.paidYou", "amount", ctx.Fmt.Coins(f.Cost));
                vm.Lines.Add(new LineVm(s.Get("festival.lastRow", "name", Name(ctx, f.Kind), "when", when), null) { Note = s.Get("festival.lastNote", "venue", VenueName(ctx, f.Venue), "paid", paid) });
            }
        }

        /// <summary>The planning sheet: where, who pays, which day, the cost and what it does, and the button.</summary>
        public static SheetVm Build(ViewContext ctx, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "community_hall" };
            if (!FestivalRules.On(sim)) { vm.Title = ctx.Reject(RejectReason.NoTownship); return vm; }
            Choices(sim, arg, out int venue, out int payer, out int days);
            FestivalDef d = FestivalRules.Def(sim);
            TownLifeState st = TownLifeRules.State(sim);
            int kind = FestivalRules.KindNow(sim);
            vm.Title = Name(ctx, kind);
            vm.Subtitle = s.Get("festival.seasonOne", "season", SeasonName(ctx, kind));
            if (FestivalRules.DoneThisSeason(sim))
            {
                vm.Lines.Add(new LineVm(Planned(ctx, st), null, "festival", st.FestivalKind) { Note = s.Get("festival.onceASeason") });
                return vm;
            }

            vm.Lines.Add(LineVm.Header(s.Get("festival.where")));
            vm.Lines.Add(Strip(ctx, venue, "v", new[] { FestivalDef.Hall, FestivalDef.Square }, i => VenueName(ctx, i), i => Arg(i, payer, days)));
            vm.Lines.Add(new LineVm(s.Get("festival.reach", "m", ctx.Fmt.Integer(FestivalRules.Reach(sim, venue))), null) { Dim = true, Note = FestivalRules.VenueReady(sim, venue) ? null : s.Get("festival.needsKeeper"), NoteTone = Tone.Warn });
            vm.Lines.Add(LineVm.Header(s.Get("festival.who")));
            vm.Lines.Add(Strip(ctx, payer, "p", new[] { FestivalDef.Town, FestivalDef.You }, i => s.Get(i == FestivalDef.Town ? "festival.townPays" : "festival.youPay"), i => Arg(venue, i, days)));
            long cost = FestivalRules.Cost(sim, kind);
            vm.Lines.Add(new LineVm(s.Get(payer == FestivalDef.Town ? "festival.treasuryHas" : "festival.youHave", "amount", ctx.Fmt.Coins(payer == FestivalDef.Town ? sim.State.Treasury.Cash : sim.State.Ledger.Cash)), null) { Dim = true });
            vm.Lines.Add(LineVm.Header(s.Get("festival.which")));
            var dayIds = new List<int>();
            for (int i = 1; i <= d.DaysAhead; i++) dayIds.Add(i);
            vm.Lines.Add(Strip(ctx, days, "d", dayIds.ToArray(), i => DayName(ctx, i), i => Arg(venue, payer, i)));
            vm.Lines.Add(new LineVm(s.Get("festival.fromMidnight"), null) { Dim = true });

            vm.Lines.Add(new LineVm(s.Get("festival.cost"), ctx.Fmt.Coins(cost), "festival", kind) { Note = s.Get("festival.costNote", "per", ctx.Fmt.Coins(d.Kinds[kind].PerPerson * Sim.Core.Finance.Money.Coin), "people", ctx.Fmt.Integer(FestivalRules.People(sim))) });
            vm.Lines.Add(new LineVm(s.Get("festival.does", "in", ctx.Fmt.Integer(d.InReach), "out", ctx.Fmt.Integer(d.Outside), "m", ctx.Fmt.Integer(FestivalRules.Reach(sim, venue)), "chai", ctx.Fmt.Percent(d.ShopChai), "veg", ctx.Fmt.Percent(d.ShopVeg)), null) { Dim = true });
            vm.Actions.Add(ActionVm.Checked(ctx, "holdFestival", s.Get("festival.holdIt"), null, ActionKind.Primary, at => new ScheduleFestival(at, venue, payer, days)));
            return vm;
        }

        private static LineVm Strip(ViewContext ctx, int current, string group, int[] values, System.Func<int, string> label, System.Func<int, string> arg)
        {
            var tabs = new List<ActionVm>();
            foreach (int v in values) tabs.Add(ActionVm.Open("fest:" + group + v, label(v), new SheetRequest(Kind, 0, arg(v))));
            return new LineVm(null, null) { Tabs = tabs, TabOn = "fest:" + group + current };
        }

        /// <summary>The bell: "Mango Mela is tomorrow" (or today), from the day before; opens the festival.</summary>
        public static void Alerts(ViewContext ctx, List<AlertVm> list)
        {
            Simulation sim = ctx.Sim;
            if (!FestivalRules.On(sim)) return;
            TownLifeState st = TownLifeRules.State(sim);
            if (!st.FestivalOn) return;
            long ahead = st.FestivalDay - FestivalRules.Today(sim);
            if (ahead < 0 || ahead > 1) return;
            var s = ctx.Strings;
            list.Add(new AlertVm
            {
                Level = AlertLevel.Info,
                Text = s.Get(ahead == 0 ? "alert.festivalToday" : "alert.festivalTomorrow", "name", Name(ctx, st.FestivalKind)),
                Note = null,
                Action = ActionVm.Open("festival", s.Get("action.see"), new SheetRequest(Kind, 0)),
            });
        }

        /// <summary>The note beside the clock on the day: "Mango Mela today · the banyan square", or null.</summary>
        public static string TodayNote(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            if (!FestivalRules.IsDay(sim)) return null;
            TownLifeState st = TownLifeRules.State(sim);
            return ctx.Strings.Get("festival.banner", "name", Name(ctx, st.FestivalKind), "venue", VenueName(ctx, st.FestivalVenue));
        }
    }
}
