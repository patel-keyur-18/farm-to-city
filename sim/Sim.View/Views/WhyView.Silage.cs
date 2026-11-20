using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>The silage pit's and the chaff cutter's Why? pages (US-255, Preview 9): the arithmetic in words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        /// <summary>The pit: this morning's spare, what is lost, what goes in, the room left, and what a kg put by gains over selling it.</summary>
        private static WhyVm SilagePit(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LivestockState st = sim.State.Livestock;
            LivestockDef ld = LivestockRules.Defs(sim).Livestock;
            FeedingReport f = st.LastFeeding;
            WhyVm vm = New(ctx, "why.silage.title", ctx.Fmt.Kg(st.SilageGrams));
            long spare = f.SilagePut + f.SilageLost + f.GreenSold;
            Row(vm, ctx, "why.silage.spare", ctx.Fmt.Kg(spare));
            Row(vm, ctx, "why.silage.lost", "−" + ctx.Fmt.Kg(f.SilageLost), "pct", Pct(ctx, ld.SilageLossShare));
            Row(vm, ctx, "why.silage.put", ctx.Fmt.Kg(f.SilagePut));
            Row(vm, ctx, "why.silage.room", ctx.Fmt.Kg(System.Math.Max(0, ld.SilagePitKg * 1000L - st.SilageGrams)));
            Fixed price = ld.GreenFodderBuyPrice * sim.State.Market.PriceIndex, standing = ld.GreenFodderSellPrice * sim.State.Market.PriceIndex;
            Row(vm, ctx, "why.silage.standing", ctx.Fmt.PricePerKg(standing));
            Row(vm, ctx, "why.silage.eaten", ctx.Fmt.PricePerKg((Fixed.One - ld.SilageLossShare) * price), "kept", Pct(ctx, Fixed.One - ld.SilageLossShare), "price", ctx.Fmt.PricePerKg(price));
            Row(vm, ctx, "why.silage.gain", "+" + ctx.Fmt.PricePerKg(ShedSheet.SilageGainPerKgFixed(sim)));
            Text(vm, ctx, "why.silage.how");
            vm.Footnote = ctx.Strings.Get("why.silage.lesson");
            return vm;
        }

        /// <summary>Why a yard keeps two sheds: what you have and the limit, in words.</summary>
        private static WhyVm ShedLimit(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.shedLimit.title", ctx.Strings.Get("shed.shedsValue", "n", ctx.Fmt.Integer(LivestockRules.SecondShed(sim) != null ? 2 : 1)));
            Row(vm, ctx, "why.shedLimit.have", ctx.Fmt.Integer(LivestockRules.SecondShed(sim) != null ? 2 : 1));
            Row(vm, ctx, "why.shedLimit.places", ctx.Fmt.Integer(LivestockRules.Stalls(sim)));
            Row(vm, ctx, "why.shedLimit.limit", ctx.Fmt.Integer(2));
            Text(vm, ctx, "why.shedLimit.how");
            vm.Footnote = ctx.Strings.Get("why.shedLimit.lesson");
            return vm;
        }

        /// <summary>The cutter: the herd's need without it and with it, the bought feed that saves, and what that is a day and a season.</summary>
        private static WhyVm ChaffCutter(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LivestockDefs defs = LivestockRules.Defs(sim);
            LivestockDef ld = defs.Livestock;
            long green = 0, straw = 0;
            foreach (Animal a in sim.State.Livestock.Animals)
            {
                var n = LivestockSystem.NeedOf(sim, a, false);
                green += n.Green;
                straw += n.Straw;
            }
            Fixed keep = Fixed.One - ld.ChaffCutterSaving;
            Fixed commons = ld.CommonsShare[(int)Calendar.SeasonAt(ctx.Now, sim.Clock)];
            long greenSaved = (ld.ChaffCutterSaving * (Fixed.One - commons)).MulRatio(green, 1).RoundToLong(), strawSaved = ld.ChaffCutterSaving.MulRatio(straw, 1).RoundToLong();
            long perDay = LivestockRules.ChaffSavingPerDay(sim), perSeason = perDay * (sim.Clock.SecondsPerSeason / ClockDef.SecondsPerDay);
            Fixed greenPrice = ld.GreenFodderBuyPrice * sim.State.Market.PriceIndex;
            WhyVm vm = New(ctx, "why.chaff.title", ctx.Fmt.Coins(perDay));
            Row(vm, ctx, "why.chaff.green", ctx.Fmt.Kg(green));
            Row(vm, ctx, "why.chaff.greenWith", ctx.Fmt.Kg((keep).MulRatio(green, 1).RoundToLong()), "pct", Pct(ctx, ld.ChaffCutterSaving));
            Row(vm, ctx, "why.chaff.straw", ctx.Fmt.Kg(straw) + " → " + ctx.Fmt.Kg(keep.MulRatio(straw, 1).RoundToLong()));
            Row(vm, ctx, "why.chaff.greenSaved", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(greenSaved, greenPrice, 1000)), "kg", ctx.Fmt.Kg(greenSaved), "price", ctx.Fmt.PricePerKg(greenPrice));
            Row(vm, ctx, "why.chaff.strawSaved", ctx.Fmt.Coins(Sim.Core.Finance.Money.Value(strawSaved, MarketRules.InputPrice(sim, defs.Straw), sim.Config.Game.Market.Goods[defs.Straw].UnitSize)), "kg", ctx.Fmt.Kg(strawSaved),
                "price", ctx.Fmt.PricePerKg(MarketRules.InputPrice(sim, defs.Straw)));
            Row(vm, ctx, "why.chaff.season", ctx.Fmt.Coins(perDay) + " · " + ctx.Fmt.Coins(perSeason));
            Text(vm, ctx, "why.chaff.how", "cost", ctx.Fmt.Coins(MarketRules.Indexed(sim, ld.ChaffCutterCost)));
            vm.Footnote = ctx.Strings.Get("why.chaff.lesson");
            return vm;
        }
    }
}
