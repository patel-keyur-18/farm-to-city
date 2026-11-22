using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>Managers (spec 028 §3.2): why his pay, what his style does, why he mis-sets days, what the target is and what a miss costs.</summary>
    public static partial class WhyView
    {
        private static Worker ManagerOf(ViewContext ctx, long businessId) { return businessId <= 0 ? null : ManagerRules.Of(ctx.Sim, businessId); }

        private static WhyVm ManagerWage(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ManagersDef d = sim.Config.Game.Managers;
            Worker m = ManagerOf(ctx, id);
            long usual = HiringRules.AskedWage(sim, HiringRules.ManagerPlace, Fixed.FromInt(50), false);
            WhyVm vm = New(ctx, "why.managerWage.title", Money(ctx, m == null ? usual : m.Wage));
            Row(vm, ctx, "why.managerWage.base", Money(ctx, m == null ? usual : m.Wage));
            Text(vm, ctx, "why.managerWage.skill");
            Row(vm, ctx, "why.managerWage.share", Pct(ctx, d.WageProfitShare));
            if (m != null)
            {
                Business b = sim.State.Businesses.Find(id);
                long profit = BusinessBooks.NetProfit(BusinessBooks.Report(b, sim.Period));
                Row(vm, ctx, "why.managerWage.sofar", Money(ctx, profit > 0 ? Sim.Core.Finance.Money.MulRate(profit, d.WageProfitShare) : 0));
            }
            Text(vm, ctx, "why.managerWage.never");
            vm.Footnote = ctx.Strings.Get("why.managerWage.foot");
            return vm;
        }

        private static WhyVm StyleWhy(ViewContext ctx, int style, string titleKey)
        {
            ManagersDef d = ctx.Sim.Config.Game.Managers;
            ManagerStyleDef st = d.Styles[style];
            WhyVm vm = New(ctx, titleKey, ctx.Strings.Get(st.NameKey));
            Text(vm, ctx, "manager.styleLine." + st.Id);
            Row(vm, ctx, "why.managerStyle.price", st.PriceFactor == Fixed.One ? ctx.Strings.Get("manager.priceAt") : st.PriceFactor < Fixed.One
                ? ctx.Strings.Get("manager.priceUnder", "pct", Pct(ctx, Fixed.One - st.PriceFactor)) : ctx.Strings.Get("manager.priceOver", "pct", Pct(ctx, st.PriceFactor - Fixed.One)));
            Row(vm, ctx, "why.managerStyle.quality", ShopWords.Quality(ctx, st.Quality, false));
            Row(vm, ctx, "why.managerStyle.stock", ctx.Fmt.Integer(st.StockDays));
            Row(vm, ctx, "why.managerStyle.hire", ctx.Strings.Get("why.managerStyle.hire." + st.Id));
            vm.Footnote = ctx.Strings.Get("why.managerStyle.foot");
            return vm;
        }

        private static WhyVm ManagerStyle(ViewContext ctx, long id)
        {
            Worker m = ManagerOf(ctx, id);
            return StyleWhy(ctx, m == null ? 0 : m.Mgr.Style, "why.managerStyle.title");
        }

        private static WhyVm ManagerApplicant(ViewContext ctx, long id)
        {
            Applicant a = ctx.Sim.State.People.Applicant(id);
            return StyleWhy(ctx, a == null ? 0 : ManagerRules.StyleOf(ctx.Sim, a), "why.managerStyle.title");
        }

        private static WhyVm ManagerMiss(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ManagersDef d = sim.Config.Game.Managers;
            Worker m = ManagerOf(ctx, id);
            WhyVm vm = New(ctx, "why.managerMiss.title", m == null ? "" : Pct(ctx, ManagerRules.MissChance(sim, m)));
            if (m != null) Row(vm, ctx, "why.managerMiss.stars", ctx.Fmt.Integer(ManagerRules.Stars(sim, m)));
            for (int i = 0; i < d.MissDay.Count; i++) Row(vm, ctx, "why.managerMiss.row", Pct(ctx, d.MissDay[i]), "n", ctx.Fmt.Integer(i + 1));
            Text(vm, ctx, "why.managerMiss.mood");
            Text(vm, ctx, "why.managerMiss.effect");
            vm.Footnote = ctx.Strings.Get("why.managerMiss.foot");
            return vm;
        }

        private static WhyVm ManagerGoal(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ManagersDef d = sim.Config.Game.Managers;
            Worker m = ManagerOf(ctx, id);
            Business b = sim.State.Businesses?.Find(id);
            WhyVm vm = New(ctx, "why.managerTarget.title", m == null || b == null ? "" : Money(ctx, ManagerRules.Goal(sim, b, m)));
            Text(vm, ctx, "why.managerTarget.profit", "pct", Pct(ctx, d.ProfitGoalRise - Fixed.One));
            Text(vm, ctx, "why.managerTarget.growth", "pct", Pct(ctx, d.GrowthGoalRise - Fixed.One));
            Text(vm, ctx, "why.managerTarget.first");
            Text(vm, ctx, "why.managerTarget.miss");
            vm.Footnote = ctx.Strings.Get("why.managerTarget.foot");
            return vm;
        }

        private static WhyVm ManagerRaise(ViewContext ctx, long id)
        {
            ManagersDef d = ctx.Sim.Config.Game.Managers;
            WhyVm vm = New(ctx, "why.managerRaise.title", "");
            Text(vm, ctx, "why.managerRaise.miss");
            Row(vm, ctx, "why.managerRaise.mood", Pct(ctx, -d.MissedMood));
            Text(vm, ctx, "why.managerRaise.raise");
            vm.Footnote = ctx.Strings.Get("why.managerRaise.foot");
            return vm;
        }
    }
}
