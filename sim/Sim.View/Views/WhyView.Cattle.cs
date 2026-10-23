using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Livestock;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The dairy co-op's sources (spec 012 §3.5, ADR 0061).</summary>
    public static partial class WhyView
    {
        /// <summary>The co-op's price a litre for an animal kind (id): a base, plus so much a point of fat, at today's prices.</summary>
        private static WhyVm MilkPrice(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LivestockDefs defs = sim.Config.Game.Livestock;
            AnimalDef a = defs.Animals[(int)id];
            LivestockDef l = defs.Livestock;
            WhyVm vm = New(ctx, "why.milkPrice.title", PerLitre(ctx, LivestockRules.MilkPrice(sim, (int)id)), "animal", ctx.Strings.Get(a.NameKey).ToLowerInvariant());
            Row(vm, ctx, "why.milkPrice.base", PerLitre(ctx, l.MilkBasePerLitre));
            Row(vm, ctx, "why.milkPrice.fat", PerLitre(ctx, l.MilkPerFatPoint * a.FatPercent), "fat", ctx.Fmt.Decimal1(a.FatPercent), "per", ctx.Fmt.Paise((l.MilkPerFatPoint * Fixed.FromInt(100)).RoundToLong()));
            Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
            vm.Footnote = ctx.Strings.Get("why.milkPrice.lesson");
            return vm;
        }

        /// <summary>This morning's milk: what went to the co-op and what it paid, and what the household kept.</summary>
        private static WhyVm MilkToday(ViewContext ctx, long id)
        {
            LivestockState s = ctx.Sim.State.Livestock;
            WhyVm vm = New(ctx, "why.milkToday.title", ctx.Fmt.Coins(s.LastMilkAmount));
            Row(vm, ctx, "why.milkToday.sold", ctx.Fmt.Litres(s.LastMilkMl - s.LastHomeMl));
            Row(vm, ctx, "why.milkToday.home", ctx.Fmt.Litres(s.LastHomeMl));
            vm.Footnote = ctx.Strings.Get("why.milkToday.lesson");
            return vm;
        }

        private static string PerLitre(ViewContext ctx, Fixed coins)
        {
            return ctx.Strings.Get("dairy.perLitre", "amount", ctx.Fmt.Paise((coins * Fixed.FromInt(100)).RoundToLong()));
        }
    }
}
