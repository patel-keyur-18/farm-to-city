using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>The brick kiln (US-243): every number on its research and its day explained in words.</summary>
    public static partial class WhyView
    {
        /// <summary>A kiln row: the market's rows (id = the row) and the business's day (id = business × 100 + row).</summary>
        private static WhyVm KilnWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int row = (int)(id % 100);
            Business b = id >= 100 ? BusinessOf(ctx, id / 100) : null;
            BusinessTypeDef t = b != null ? BusinessRules.TypeOf(sim, b) : System.Linq.Enumerable.First(BusinessRules.Defs(sim).Types, x => x.IsKiln);
            KilnDef k = t.Kiln;
            ConstructionDef d = ConstructionRules.Def(sim);
            Fixed index = sim.State.Market.PriceIndex;
            switch (row)
            {
                case KilnSheet.Want:
                {
                    WhyVm vm = New(ctx, "why.kilnWant.title", ctx.Fmt.Integer(KilnSheet.Wanted(sim)));
                    Text(vm, ctx, "why.kilnWant.text");
                    return vm;
                }
                case KilnSheet.Trader:
                {
                    WhyVm vm = New(ctx, "why.kilnTrader.title", KilnSheet.PerBrick(ctx, KilnRules.TraderPrice(sim)));
                    Text(vm, ctx, "why.kilnTrader.text", "limit", ctx.Fmt.Integer(d.DailyBricks));
                    return vm;
                }
                case KilnSheet.Takes:
                {
                    WhyVm vm = New(ctx, "why.kilnTakes.title", ctx.Fmt.Integer(k.TraderPerDay));
                    Text(vm, ctx, "why.kilnTakes.text", "max", ctx.Fmt.Integer(k.TraderPerDay), "price", KilnSheet.PerBrick(ctx, KilnRules.PaysFor(sim, k, 1)));
                    return vm;
                }
                case KilnSheet.Fuel:
                {
                    WhyVm vm = New(ctx, "why.kilnFuel.title", ctx.Fmt.PricePerKg(k.FuelPrice * index));
                    Text(vm, ctx, "why.kilnFuel.text", "kg", ctx.Fmt.Decimal1(k.FuelKg));
                    return vm;
                }
                case KilnSheet.Fires:
                {
                    WhyVm vm = New(ctx, "why.kilnFires.title", ctx.Strings.Get("research.kilnFiresValue"));
                    Text(vm, ctx, "why.kilnFires.text");
                    return vm;
                }
                case KilnSheet.Made:
                {
                    WhyVm vm = New(ctx, "why.kilnMade.title", ctx.Fmt.Integer(b == null ? 0 : b.KilnMade));
                    Text(vm, ctx, "why.kilnMade.text", "rate", ctx.Fmt.Integer(b == null ? k.BricksPerHour : KilnRules.PerHour(sim, b)), "hours", ctx.Fmt.Integer(b == null ? t.CloseHour - t.OpenHour : b.CloseHour - b.OpenHour));
                    return vm;
                }
                case KilnSheet.Yard:
                {
                    WhyVm vm = New(ctx, "why.kilnYard.title", ctx.Fmt.Integer(b == null ? 0 : b.Yard));
                    Text(vm, ctx, "why.kilnYard.text", "max", ctx.Fmt.Integer(k.YardMax));
                    return vm;
                }
                case KilnSheet.Builds:
                {
                    WhyVm vm = New(ctx, "why.kilnBuilds.title", ctx.Fmt.Integer(b == null ? 0 : b.KilnToBuilds));
                    Text(vm, ctx, "why.kilnBuilds.text");
                    return vm;
                }
                case KilnSheet.Sold:
                {
                    WhyVm vm = New(ctx, "why.kilnSold.title", ctx.Fmt.Integer(b == null ? 0 : b.KilnSold));
                    Text(vm, ctx, "why.kilnSold.text", "max", ctx.Fmt.Integer(k.TraderPerDay), "price", KilnSheet.PerBrick(ctx, KilnRules.PaysFor(sim, k, 1)));
                    return vm;
                }
                default:
                {
                    WhyVm vm = New(ctx, "why.kilnCost.title", KilnSheet.Plain(ctx, b == null ? KilnRules.FuelCost(sim, k, 1) : KilnRules.FuelCost(sim, b, k, 1)));
                    Text(vm, ctx, "why.kilnCost.text", "kg", ctx.Fmt.Decimal1(k.FuelKg), "price", ctx.Fmt.PricePerKg(k.FuelPrice * index));
                    return vm;
                }
            }
        }
    }
}
