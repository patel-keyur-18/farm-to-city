using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Businesses (spec 016): every number on the research, plan and business sheets explained in words.</summary>
    public static partial class WhyView
    {
        private static Business BusinessOf(ViewContext ctx, long id) { return ctx.Sim.State.Businesses?.Find(id); }

        /// <summary>The plan's type and who runs it, from <see cref="BusinessWords.PlanId"/>.</summary>
        private static BusinessRules.Projection PlanOf(ViewContext ctx, long id, out BusinessTypeDef t, out bool self)
        {
            t = BusinessRules.Defs(ctx.Sim).Types[(int)(id / 2)];
            self = id % 2 == 1;
            return BusinessRules.Projected(ctx.Sim, t, self, t.OwnPerDay, t.Fee);
        }

        private static WhyVm BusinessArea(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BusinessDefs d = BusinessRules.Defs(sim);
            WhyVm vm = New(ctx, "why.businessArea.title", ctx.Fmt.Kg(BusinessRules.AreaGramsPerDay(sim)));
            Row(vm, ctx, "why.businessArea.people", ctx.Fmt.Integer(d.CatchmentPeople + (sim.State.Citizens?.Population ?? 0)), "town", ctx.Fmt.Integer(sim.State.Citizens?.Population ?? 0));
            Row(vm, ctx, "why.businessArea.each", ctx.Fmt.Kg((d.GrainPerPersonPerRealDay * sim.Config.Game.Population.RealDaysPerGameDay * Fixed.FromInt(1000)).RoundToLong()));
            Text(vm, ctx, "why.businessArea.hours");
            Text(vm, ctx, "why.businessArea.lost");
            vm.Footnote = ctx.Strings.Get("why.businessArea.lesson");
            return vm;
        }

        private static WhyVm BusinessFee(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            BusinessDefs d = BusinessRules.Defs(sim);
            Fixed index = sim.State.Market.PriceIndex;
            WhyVm vm = New(ctx, "why.businessFee.title", ctx.Fmt.PricePerKg(d.NextVillageFee * index));
            Text(vm, ctx, "why.businessFee.next");
            Row(vm, ctx, "why.businessFee.most", ctx.Fmt.PricePerKg((d.NextVillageFee + d.FeeTolerance) * index));
            vm.Footnote = ctx.Strings.Get("why.businessFee.lesson");
            return vm;
        }

        private static WhyVm BusinessTrader(ViewContext ctx, long good)
        {
            Simulation sim = ctx.Sim;
            int g = (int)good;
            WhyVm vm = New(ctx, "why.businessTrader.title", ctx.Fmt.PricePerKg(MarketRules.Floor(sim, g)), "good", ctx.Strings.Get(sim.Config.Game.Market.Goods[g].NameKey));
            Row(vm, ctx, "why.businessTrader.regional", ctx.Fmt.PricePerKg(MarketRules.RegionalPrice(sim, g)));
            Row(vm, ctx, "why.businessTrader.transport", ctx.Fmt.PricePerKg(MarketRules.TransportCost(sim, g)));
            Row(vm, ctx, "why.businessTrader.day", ctx.Fmt.Kg(sim.Config.Game.Market.Region.TraderBuyPerDay(g)));
            return vm;
        }

        private static WhyVm BusinessWage(ViewContext ctx, long type)
        {
            BusinessTypeDef t = BusinessRules.Defs(ctx.Sim).Types[(int)type];
            WhyVm vm = New(ctx, "why.businessWage.title", ctx.Fmt.Coins(BusinessRules.Wage(ctx.Sim, t)), "role", ctx.Strings.Get("business.role." + t.Worker));
            Text(vm, ctx, "why.businessWage.paid");
            Text(vm, ctx, "why.businessWage.who");
            Text(vm, ctx, "why.businessWage.you");
            return vm;
        }

        private static WhyVm BusinessOwn(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            WhyVm vm = New(ctx, "why.businessOwn.title", ctx.Fmt.Kg(b.OwnPerDay));
            Row(vm, ctx, "why.businessOwn.price", ctx.Fmt.PricePerKg(MarketRules.Floor(ctx.Sim, t.Input)));
            Text(vm, ctx, "why.businessOwn.books");
            Text(vm, ctx, "why.businessOwn.yields");
            vm.Footnote = ctx.Strings.Get("why.businessOwn.lesson");
            return vm;
        }

        private static WhyVm BusinessWorker(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessWorker.title", BusinessWords.Worker(ctx, b));
            Text(vm, ctx, b.Worker == WorkerKind.Founder ? "why.businessWorker.you" : b.Worker == WorkerKind.Neighbour ? "why.businessWorker.neighbour" : "why.businessWorker.town");
            Text(vm, ctx, "why.businessWorker.townJob");
            return vm;
        }

        private static WhyVm BusinessHours(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessHours.title", BusinessWords.Hours(ctx, b.OpenHour, b.CloseHour));
            Text(vm, ctx, "why.businessHours.area");
            Text(vm, ctx, b.Worker == WorkerKind.Founder ? "why.businessHours.you" : "why.businessHours.worker");
            return vm;
        }

        private static WhyVm BusinessMarket(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            WhyVm vm = New(ctx, "why.businessMarket.title", ctx.Fmt.PricePerKg(MarketRules.Ceiling(ctx.Sim, t.Input)));
            Text(vm, ctx, "why.businessMarket.price");
            vm.Footnote = ctx.Strings.Get("why.businessMarket.lesson");
            return vm;
        }

        private static WhyVm BusinessCash(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.businessCash.title", ctx.Fmt.Coins(b.Ledger.Cash));
            Text(vm, ctx, "why.businessCash.own");
            Text(vm, ctx, "why.businessCash.flow");
            vm.Footnote = ctx.Strings.Get("why.businessCash.lesson");
            return vm;
        }

        /// <summary>One plan line's *Why?*: its value, how it's reckoned, and the plan's lesson.</summary>
        private static WhyVm PlanLine(ViewContext ctx, long id, string line)
        {
            BusinessRules.Projection p = PlanOf(ctx, id, out BusinessTypeDef t, out bool self);
            Fixed index = ctx.Sim.State.Market.PriceIndex;
            int days = ctx.Sim.Clock.DaysPerSeason;
            long value = line == "fees" ? p.Fees : line == "sales" ? p.Sales : line == "wheat" ? p.Wheat : line == "power" ? p.Power : line == "repairs" ? p.Repairs
                : line == "depreciation" ? p.Depreciation : line == "wage" ? p.Wage : line == "profit" ? p.Profit : line == "cost" ? p.Cost : p.CashNeeded;
            WhyVm vm = New(ctx, "why.plan." + line + ".title", line == "payback" ? BusinessPlanSheet.Payback(ctx, p) : ctx.Fmt.Coins(value), "role", ctx.Strings.Get("business.role." + t.Worker));
            Text(vm, ctx, "why.plan." + line, "days", ctx.Fmt.Integer(days), "area", ctx.Fmt.Kg(p.AreaGrams / days), "own", ctx.Fmt.Kg(p.OwnGrams / days),
                "fee", ctx.Fmt.PricePerKg(t.Fee * index), "power", ctx.Fmt.PricePerKg(t.PowerPerKg * index), "share", ctx.Fmt.Percent(t.RepairsShare),
                "cost", ctx.Fmt.Coins(p.Cost), "profit", ctx.Fmt.Coins(p.Profit), "role", ctx.Strings.Get("business.role." + t.Worker));
            if (line == "profit" || line == "payback") vm.Footnote = ctx.Strings.Get("why.plan.lesson");
            return vm;
        }
    }
}
