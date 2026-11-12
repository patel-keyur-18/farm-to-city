using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;

namespace Sim.View.Views
{
    /// <summary>
    /// Storage in the mandi town (spec 013 §7 as amended by ADR 0087), opened from the Store's picture button: your own godown (buy it,
    /// let it out), space rented in the warehouse (a tonne more now, a tonne less from the next close), and what is kept there.
    /// </summary>
    public static class GodownSheet
    {
        public const string Kind = "Godown";

        /// <summary>The Store's picture button's note: what is kept in town, or what you can do there.</summary>
        public static string ShortcutNote(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            long capacity = GodownRules.CapacityGrams(sim);
            return capacity == 0 ? ctx.Strings.Get("godown.shortcutNone")
                : ctx.Strings.Get("store.fill", "used", ctx.Fmt.Kg(GodownRules.UsedGrams(sim)), "capacity", ctx.Fmt.Kg(capacity));
        }

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            GodownDef d = GodownRules.Def(sim);
            GodownState st = sim.State.Godown;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "granary", Title = s.Get("godown.title"), Subtitle = s.Get("godown.subtitle") };

            // How goods get here (owner, 2026-11-12: there is nothing to press, so say so first, in plain words).
            vm.Lines.Add(LineVm.Header(s.Get("godown.howHeading")));
            vm.Lines.Add(new LineVm(s.Get("godown.how"), null) { Icon = "granary", Note = s.Get("godown.howNote"), NoteTone = Tone.Good });

            // Your own godown.
            vm.Lines.Add(LineVm.Header(s.Get("godown.ownHeading")));
            long own = d.CapacityKg * 1000;
            if (GodownRules.Owned(sim) == null)
            {
                long price = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Building].Value);
                var line = new LineVm(s.Get("godown.own"), s.Get("godown.ownOffer", "capacity", ctx.Fmt.Kg(own)), "godown") { Icon = "granary" };
                line.Action = ActionVm.Checked(ctx, "buyGodown", s.Get("godown.buy", "price", ctx.Fmt.Coins(price)), s.Get("godown.buyDetail"),
                    ActionKind.Primary, at => new BuyGodown(at));
                vm.Lines.Add(line);
            }
            else
            {
                string value = st.LetNow ? s.Get("godown.letNow", "rent", ctx.Fmt.Coins(GodownRules.LetRent(sim)))
                    : s.Get("store.fill", "used", ctx.Fmt.Kg(System.Math.Min(GodownRules.UsedGrams(sim), own)), "capacity", ctx.Fmt.Kg(own));
                vm.Lines.Add(new LineVm(s.Get("godown.yours"), value, "godown") { Icon = "granary" });
                var let = new LineVm(s.Get("godown.letOut"), s.Get(st.LetNext ? "godown.letOn" : "godown.letOff"), "godownLet");
                if (st.LastOutcome == LetOutcome.NoTenant) { let.Note = s.Get("godown.noTenant"); let.NoteTone = Tone.Warn; }
                if (st.LastOutcome == LetOutcome.YourGoods) { let.Note = s.Get("godown.yourGoods"); let.NoteTone = Tone.Warn; }
                let.Action = ActionVm.Checked(ctx, "letGodown", s.Get(st.LetNext ? "godown.stopLetting" : "godown.startLetting"),
                    st.LetNext ? null : s.Get("godown.letDetail", "rent", ctx.Fmt.Coins(GodownRules.LetRent(sim))), ActionKind.Secondary, at => new LetGodown(at, !st.LetNext));
                vm.Lines.Add(let);
            }

            // Space rented in the warehouse.
            vm.Lines.Add(LineVm.Header(s.Get("godown.rentHeading")));
            long inRented = GodownRules.InRentedGrams(sim);
            var rented = new LineVm(s.Get("godown.rented"),
                st.RentedTonnes == 0 ? s.Get("godown.rentedNone")
                : inRented == 0 ? s.Get("godown.rentedEmpty", "t", ctx.Fmt.Integer(st.RentedTonnes))
                : s.Get("godown.rentedValue", "t", ctx.Fmt.Integer(st.RentedTonnes), "stored", ctx.Fmt.Kg(inRented)), "warehouse");
            if (st.RentNextTonnes != st.RentedTonnes) rented.Note = s.Get("godown.rentNext", "t", ctx.Fmt.Integer(st.RentNextTonnes));
            vm.Lines.Add(rented);
            vm.Lines.Add(RentStatus(ctx));
            int more = System.Math.Max(st.RentedTonnes, st.RentNextTonnes) + 1;
            var buttons = new System.Collections.Generic.List<ActionVm>
            {
                ActionVm.Checked(ctx, "rentMore", s.Get("godown.rentMore"), s.Get("godown.rentMoreDetail", "cost", ctx.Fmt.Coins(GodownRules.RentForRestOfSeason(sim, more - st.RentedTonnes)),
                    "season", ctx.Fmt.Coins(GodownRules.SeasonRent(sim, 1))), ActionKind.Primary, at => new RentWarehouse(at, more)),
            };
            buttons[0].Icon = "sack";
            if (st.RentNextTonnes > 1)
                buttons.Add(ActionVm.Checked(ctx, "rentLess", s.Get("godown.rentLess"), s.Get("godown.rentLessDetail"), ActionKind.Secondary,
                    at => new RentWarehouse(at, st.RentNextTonnes - 1)));
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });
            // Ending it, or changing your mind, on a row of its own (owner, 2026-11-12: "stop renting from next season" must be plain).
            if (st.RentNextTonnes > 0)
                vm.Lines.Add(new LineVm(null, null) { Buttons = new System.Collections.Generic.List<ActionVm>
                    { ActionVm.Checked(ctx, "stopRenting", s.Get("godown.stopRenting"), s.Get("godown.stopRentingDetail"), ActionKind.Secondary, at => new RentWarehouse(at, 0)) } });
            else if (st.RentedTonnes > 0)
                vm.Lines.Add(new LineVm(null, null) { Buttons = new System.Collections.Generic.List<ActionVm>
                    { ActionVm.Checked(ctx, "keepRenting", s.Get("godown.keepRenting", "t", ctx.Fmt.Integer(st.RentedTonnes)), s.Get("godown.keepRentingDetail"), ActionKind.Primary,
                        at => new RentWarehouse(at, st.RentedTonnes)) } });

            // What goes here, and where each good is now: the six dry goods always (so the player sees what counts), seed once there is some.
            long[] dry = GodownRules.Dry(sim);
            var goods = sim.Config.Game.Market.Goods;
            vm.Lines.Add(LineVm.Header(s.Get("godown.keptHeading")));
            for (int g = 0; g < goods.Count; g++)
            {
                if (!d.Holds[g]) continue;
                long stock = sim.State.Market.Stock[g];
                if (goods[g].Kind != GoodKind.Produce && stock == 0) continue;
                long here = dry == null ? 0 : dry[g];
                string where = stock == 0 ? s.Get("godown.noneYet")
                    : here == 0 ? s.Get("godown.inHut", "hut", ctx.Fmt.Kg(Store.WeightGrams(goods[g], stock)))
                    : here == stock ? s.Get("godown.allHere", "here", ctx.Fmt.Kg(Store.WeightGrams(goods[g], here)))
                    : s.Get("godown.where", "here", ctx.Fmt.Kg(Store.WeightGrams(goods[g], here)), "hut", ctx.Fmt.Kg(Store.WeightGrams(goods[g], stock - here)));
                vm.Lines.Add(here > 0 ? new LineVm(s.Get(goods[g].NameKey), where, "godownGood", g) { Icon = goods[g].Id } : new LineVm(s.Get(goods[g].NameKey), where) { Icon = goods[g].Id });
            }
            vm.Lines.Add(new LineVm(s.Get("godown.notKept"), null) { Note = s.Get("godown.notKeptNote") });
            if (GodownRules.CapacityGrams(sim) == 0) vm.Lines.Add(new LineVm(s.Get("godown.nothingKept"), null));
            return vm;
        }

        /// <summary>
        /// What the next close does with the rented space, in words (owner, 2026-11-12): it renews each season until stopped, it ends, or it
        /// renews anyway because your goods are in it. The figure is the close's own (<see cref="GodownRules.NextCloseRent"/>).
        /// </summary>
        private static LineVm RentStatus(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            GodownState st = sim.State.Godown;
            int tonnes = GodownRules.NextCloseTonnes(sim);
            string rent = ctx.Fmt.Coins(GodownRules.NextCloseRent(sim)), t = ctx.Fmt.Integer(tonnes);
            LineVm line;
            if (tonnes == 0 && st.RentedTonnes == 0) return new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentNone"));
            if (tonnes == 0) line = new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentEnds")) { Note = s.Get("godown.rentEndsNote"), NoteTone = Tone.Good };
            else if (st.RentNextTonnes == 0) line = new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentForced")) { Note = s.Get("godown.rentForcedNote", "t", t, "rent", rent), NoteTone = Tone.Warn };
            else line = new LineVm(s.Get("godown.rentStatus"), s.Get("godown.rentRenews", "rent", rent)) { Note = s.Get("godown.rentRenewsNote", "rent", rent, "t", t) };
            return line;
        }
    }
}
