using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// Cold storage in the mandi town (US-242, spec 025 §3.6, Preview 7), opened from the Store's picture button from the Township: space rented by
    /// the tonne for a season (the warehouse's rules), what is kept there with how much of it keeps, and whether keeping it pays.
    /// </summary>
    public static class ColdStoreSheet
    {
        public const string Kind = "ColdStore";

        /// <summary>The Store's picture button's note: what is kept, or what you can do.</summary>
        public static string ShortcutNote(ViewContext ctx)
        {
            long capacity = ColdStoreRules.CapacityGrams(ctx.Sim);
            return capacity == 0 ? ctx.Strings.Get("cold.shortcutNone")
                : ctx.Strings.Get("store.fill", "used", ctx.Fmt.Kg(ColdStoreRules.UsedGrams(ctx.Sim)), "capacity", ctx.Fmt.Kg(capacity));
        }

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            GodownState st = sim.State.Godown;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "cold_store", Title = s.Get("cold.title"), Subtitle = s.Get("cold.subtitle") };

            vm.Lines.Add(LineVm.Header(s.Get("cold.howHeading")));
            vm.Lines.Add(new LineVm(s.Get("cold.how"), null) { Icon = "cold_store", Note = s.Get("cold.howNote"), NoteTone = Tone.Good });

            // Space rented in the cold store.
            vm.Lines.Add(LineVm.Header(s.Get("cold.rentHeading")));
            long stored = ColdStoreRules.UsedGrams(sim);
            var rented = new LineVm(s.Get("cold.rented"),
                st.ColdRentedTonnes == 0 ? s.Get("godown.rentedNone")
                : stored == 0 ? s.Get("godown.rentedEmpty", "t", ctx.Fmt.Integer(st.ColdRentedTonnes))
                : s.Get("godown.rentedValue", "t", ctx.Fmt.Integer(st.ColdRentedTonnes), "stored", ctx.Fmt.Kg(stored)), "coldRent");
            if (st.ColdRentNextTonnes != st.ColdRentedTonnes) rented.Note = s.Get("godown.rentNext", "t", ctx.Fmt.Integer(st.ColdRentNextTonnes));
            vm.Lines.Add(rented);
            vm.Lines.Add(RentStatus(ctx));
            int more = System.Math.Max(st.ColdRentedTonnes, st.ColdRentNextTonnes) + 1;
            var buttons = new List<ActionVm>();
            if (more <= ColdStoreRules.Def(sim).MaxRentTonnes)
            {
                ActionVm rentMore = ActionVm.Checked(ctx, "coldMore", s.Get("godown.rentMore"), s.Get("godown.rentMoreDetail", "cost", ctx.Fmt.Coins(ColdStoreRules.RentForRestOfSeason(sim, more - st.ColdRentedTonnes)),
                    "season", ctx.Fmt.Coins(ColdStoreRules.SeasonRent(sim, 1))), ActionKind.Primary, at => new RentColdStore(at, more));
                rentMore.Icon = "cold_store";
                buttons.Add(rentMore);
            }
            if (st.ColdRentNextTonnes > 1)
                buttons.Add(ActionVm.Checked(ctx, "coldLess", s.Get("godown.rentLess"), s.Get("godown.rentLessDetail"), ActionKind.Secondary, at => new RentColdStore(at, st.ColdRentNextTonnes - 1)));
            if (buttons.Count > 0) vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });
            if (st.ColdRentNextTonnes > 0)
                vm.Lines.Add(new LineVm(null, null) { Buttons = new List<ActionVm>
                    { ActionVm.Checked(ctx, "coldStop", s.Get("godown.stopRenting"), s.Get("godown.stopRentingDetail"), ActionKind.Secondary, at => new RentColdStore(at, 0)) } });
            else if (st.ColdRentedTonnes > 0)
                vm.Lines.Add(new LineVm(null, null) { Buttons = new List<ActionVm>
                    { ActionVm.Checked(ctx, "coldKeep", s.Get("godown.keepRenting", "t", ctx.Fmt.Integer(st.ColdRentedTonnes)), s.Get("godown.keepRentingDetail"), ActionKind.Primary,
                        at => new RentColdStore(at, st.ColdRentedTonnes)) } });

            // What goes here, with what keeps over a season, in the hut and here.
            vm.Lines.Add(LineVm.Header(s.Get("cold.keptHeading")));
            long[] cold = ColdStoreRules.Cold(sim);
            var goods = sim.Config.Game.Market.Goods;
            int days = sim.Clock.DaysPerSeason;
            var worth = new List<LineVm>();
            for (int g = 0; g < goods.Count; g++)
            {
                if (!ColdStoreRules.Keeps(sim, g)) continue;
                long stock = sim.State.Market.Stock[g], here = cold == null ? 0 : cold[g];
                string where = stock == 0 ? s.Get("godown.noneYet")
                    : here == 0 ? s.Get("godown.inHut", "hut", ctx.Fmt.Kg(Store.WeightGrams(goods[g], stock)))
                    : here == stock ? s.Get("godown.allHere", "here", ctx.Fmt.Kg(Store.WeightGrams(goods[g], here)))
                    : s.Get("godown.where", "here", ctx.Fmt.Kg(Store.WeightGrams(goods[g], here)), "hut", ctx.Fmt.Kg(Store.WeightGrams(goods[g], stock - here)));
                vm.Lines.Add(new LineVm(s.Get(goods[g].NameKey), where, "coldGood", g)
                {
                    Icon = goods[g].Id,
                    Note = s.Get("cold.keeps", "cold", Hundred(ColdStoreRules.KeptShare(sim, g, days, true)), "hut", Hundred(ColdStoreRules.KeptShare(sim, g, days, false))),
                });
                worth.Add(WorthLine(ctx, g));
            }
            vm.Lines.Add(new LineVm(s.Get("cold.notKept"), null) { Note = s.Get("cold.notKeptNote") });

            vm.Lines.Add(LineVm.Header(s.Get("cold.worthHeading")));
            vm.Lines.AddRange(worth);
            return vm;
        }

        /// <summary>Of every 100 kg, how many are left: "99".</summary>
        private static string Hundred(Fixed share) { return (share * Fixed.FromInt(100)).RoundToLong().ToString(); }

        /// <summary>The worked answer for a good: its best hold and what it nets a kg, or that it pays best sold at once.</summary>
        private static LineVm WorthLine(ViewContext ctx, int g)
        {
            var s = ctx.Strings;
            string name = s.Get(ctx.Sim.Config.Game.Market.Goods[g].NameKey);
            if (!ColdStoreRules.BestHold(ctx.Sim, g, out ColdStoreRules.Worth w))
                return new LineVm(s.Get("cold.worthLabelNone", "good", name), s.Get("cold.worthNone"), "coldWorth", g) { Note = s.Get("cold.worthNoneNote") };
            return new LineVm(s.Get("cold.worthLabel", "good", name, "from", Season(ctx, w.FromSeason), "to", Season(ctx, w.ToSeason)), "+" + ctx.Fmt.PricePerKg(w.Net), "coldWorth", g)
            {
                Note = s.Get("cold.worthNote", "start", ctx.Fmt.PricePerKg(w.StartPrice), "end", ctx.Fmt.PricePerKg(w.EndPrice), "pct", ctx.Fmt.Percent(w.Net / w.StartPrice)),
            };
        }

        internal static string Season(ViewContext ctx, int season) { return ctx.Strings.Get(new[] { "season.spring", "season.summer", "season.monsoon", "season.winter" }[season]); }

        /// <summary>What the next close does with the cold space, in words (the godown's wording): renews, ends, or renews anyway because goods are in it.</summary>
        private static LineVm RentStatus(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            GodownState st = sim.State.Godown;
            int tonnes = ColdStoreRules.NextCloseTonnes(sim);
            string rent = ctx.Fmt.Coins(ColdStoreRules.NextCloseRent(sim)), t = ctx.Fmt.Integer(tonnes);
            if (tonnes == 0 && st.ColdRentedTonnes == 0) return new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentNone"));
            if (tonnes == 0) return new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentEnds")) { Note = s.Get("godown.rentEndsNote"), NoteTone = Tone.Good };
            if (st.ColdRentNextTonnes == 0) return new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentForced")) { Note = s.Get("cold.rentForcedNote", "t", t, "rent", rent), NoteTone = Tone.Warn };
            return new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentRenews", "rent", rent)) { Note = s.Get("godown.rentRenewsNote", "rent", rent, "t", t) };
        }
    }
}
