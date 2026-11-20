using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The cold store's Why? pages (US-242, Preview 7): the rent, what a good keeps, and whether keeping it pays. In words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        private static WhyVm ColdRent(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            WhyVm vm = New(ctx, "why.cold.rent.title", ctx.Strings.Get("why.warehouse.value", "t", ctx.Fmt.Integer(sim.State.Godown.ColdRentedTonnes)));
            Row(vm, ctx, "why.cold.rent.rent", ctx.Fmt.Coins(ColdStoreRules.SeasonRent(sim, 1)));
            Row(vm, ctx, "why.cold.rent.stored", ctx.Fmt.Kg(ColdStoreRules.UsedGrams(sim)));
            Text(vm, ctx, "why.cold.rent.how");
            vm.Footnote = ctx.Strings.Get("why.cold.rent.lesson");
            return vm;
        }

        /// <summary>What a good keeps in the cold store against the hut over a season. id = the good's index.</summary>
        private static WhyVm ColdGood(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int g = (int)id;
            GoodDef good = sim.Config.Game.Market.Goods[g];
            int days = sim.Clock.DaysPerSeason;
            Fixed cold = ColdStoreRules.KeptShare(sim, g, days, true), hut = ColdStoreRules.KeptShare(sim, g, days, false);
            WhyVm vm = New(ctx, "why.cold.good.title", ctx.Fmt.Integer((cold * Fixed.FromInt(100)).RoundToLong()), "good", ctx.Strings.Get(good.NameKey), "n", ctx.Fmt.Integer((cold * Fixed.FromInt(100)).RoundToLong()));
            Row(vm, ctx, "why.cold.good.hut", Pct(ctx, good.SpoilagePerDay));
            Row(vm, ctx, "why.cold.good.cold", Pct(ctx, ColdStoreRules.SpoilageInCold(sim, g)));
            Row(vm, ctx, "why.cold.good.hutKeeps", ctx.Fmt.Integer((hut * Fixed.FromInt(100)).RoundToLong()), "days", ctx.Fmt.Integer(days));
            Row(vm, ctx, "why.cold.good.coldKeeps", ctx.Fmt.Integer((cold * Fixed.FromInt(100)).RoundToLong()));
            Text(vm, ctx, "why.cold.good.how");
            vm.Footnote = ctx.Strings.Get("why.cold.good.lesson");
            return vm;
        }

        /// <summary>Whether keeping a good for a better season's price pays, at today's prices. id = the good's index.</summary>
        private static WhyVm ColdWorth(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int g = (int)id;
            string name = ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey);
            if (!ColdStoreRules.BestHold(sim, g, out ColdStoreRules.Worth w))
            {
                WhyVm none = New(ctx, "why.cold.worth.noneTitle", ctx.Strings.Get("cold.worthNone"), "good", name);
                Text(none, ctx, "why.cold.worth.noneHow", "good", name);
                none.Footnote = ctx.Strings.Get("why.cold.worth.lesson");
                return none;
            }
            string from = ColdStoreSheet.Season(ctx, w.FromSeason), to = ColdStoreSheet.Season(ctx, w.ToSeason);
            WhyVm vm = New(ctx, "why.cold.worth.title", "+" + ctx.Fmt.PricePerKg(w.Net), "good", name, "from", from, "to", to);
            Row(vm, ctx, "why.cold.worth.start", ctx.Fmt.PricePerKg(w.StartPrice), "season", from);
            Row(vm, ctx, "why.cold.worth.end", ctx.Fmt.PricePerKg(w.EndPrice), "season", to);
            Row(vm, ctx, "why.cold.worth.rise", "+" + ctx.Fmt.PricePerKg(w.EndPrice - w.StartPrice));
            Row(vm, ctx, "why.cold.worth.rent", "−" + ctx.Fmt.PricePerKg(w.Rent), "n", ctx.Strings.Plural("why.cold.worth.seasons", w.Seasons));
            Row(vm, ctx, "why.cold.worth.spoiled", "−" + ctx.Fmt.PricePerKg(w.Spoiled));
            Row(vm, ctx, "why.cold.worth.net", "+" + ctx.Fmt.PricePerKg(w.Net));
            Text(vm, ctx, "why.cold.worth.how", "pct", Pct(ctx, w.Net / w.StartPrice));
            vm.Footnote = ctx.Strings.Get("why.cold.worth.lesson");
            return vm;
        }
    }
}
