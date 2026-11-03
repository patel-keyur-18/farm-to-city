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
            int more = System.Math.Max(st.RentedTonnes, st.RentNextTonnes) + 1;
            var buttons = new System.Collections.Generic.List<ActionVm>
            {
                ActionVm.Checked(ctx, "rentMore", s.Get("godown.rentMore"), s.Get("godown.rentMoreDetail", "cost", ctx.Fmt.Coins(GodownRules.RentForRestOfSeason(sim, more - st.RentedTonnes)),
                    "season", ctx.Fmt.Coins(GodownRules.SeasonRent(sim, 1))), ActionKind.Primary, at => new RentWarehouse(at, more)),
            };
            buttons[0].Icon = "sack";
            if (st.RentNextTonnes > 0)
                buttons.Add(ActionVm.Checked(ctx, "rentLess", s.Get("godown.rentLess"), s.Get("godown.rentLessDetail"), ActionKind.Secondary,
                    at => new RentWarehouse(at, st.RentNextTonnes - 1)));
            vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });

            // What is kept there.
            long[] dry = GodownRules.Dry(sim);
            var goods = sim.Config.Game.Market.Goods;
            bool any = false;
            for (int g = 0; g < goods.Count; g++)
            {
                if (dry[g] == 0) continue;
                if (!any) vm.Lines.Add(LineVm.Header(s.Get("godown.keptHeading")));
                any = true;
                vm.Lines.Add(new LineVm(s.Get(goods[g].NameKey), ctx.Fmt.Kg(Store.WeightGrams(goods[g], dry[g])), "godownGood", g) { Icon = goods[g].Id });
            }
            if (!any && GodownRules.CapacityGrams(sim) > 0) vm.Lines.Add(new LineVm(s.Get("godown.nothingKept"), null));
            return vm;
        }
    }
}
