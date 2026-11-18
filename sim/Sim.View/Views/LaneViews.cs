using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// A lane acre's sheet before its section is built (spec 025 §3.4, US-253), as previewed to the owner (Preview 2): the cost, the town's money,
    /// how much of the lane is built and what it does, with one button that has the treasury commission the section. Every number has a *Why?*
    /// (the <c>lane</c> source, id = section × 10 + row).
    /// </summary>
    public static class LaneSheet
    {
        public const int Cost = 1, Progress = 3;

        internal static void Fill(ViewContext ctx, SheetVm vm, int tile)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int section = LaneRules.OfTile(sim, tile);
            if (section < 0) return;
            bool open = TownshipRules.Reached(sim);
            string work = sim.Config.Game.Map.Lanes[section].Id;
            long cost = LaneRules.Cost(sim, section), cash = sim.State.Treasury.Cash;
            if (open)
            {
                vm.Lines.Add(new LineVm(s.Get("lane.cost"), ctx.Fmt.Coins(cost), "lane", section * 10 + Cost));
                vm.Lines.Add(new LineVm(s.Get("civic.cash"), ctx.Fmt.Coins(cash), "treasuryBalance", 0));
                LaneRules.Progress(sim, section, out int built, out int total);
                vm.Lines.Add(new LineVm(s.Get("lane.progress"), s.Get("lane.progressValue", "built", ctx.Fmt.Integer(built), "total", ctx.Fmt.Integer(total)), "lane", section * 10 + Progress));
            }
            vm.Lines.Add(LineVm.Header(s.Get("civic.hdrDoes")));
            vm.Lines.Add(new LineVm(s.Get("lane.does"), null));
            string detail = s.Get("lane.buttonDetail", "amount", ctx.Fmt.Coins(cost));
            if (!open)
                vm.Actions.Add(new ActionVm("buildLane", s.Get("lane.build"), s.Get("lane.opensWith"), ActionKind.Primary, false, ctx.Reject(RejectReason.NoTownship), at => new CommissionWorks(at, work)));
            else if (cash < cost)
            {
                vm.Actions.Add(new ActionVm("buildLane", s.Get("lane.build"), s.Get("lane.poorDetail", "cash", ctx.Fmt.Coins(cash), "cost", ctx.Fmt.Coins(cost)), ActionKind.Primary, false,
                    s.Get("civic.poorReason", "cash", ctx.Fmt.Coins(cash), "cost", ctx.Fmt.Coins(cost)), at => new CommissionWorks(at, work)));
                vm.Actions.Add(ActionVm.Open("give", s.Get("clinic.give"), new SheetRequest(TreasurySheet.GiveKind, 0), s.Get("treasury.giveDetail")));
            }
            else
                vm.Actions.Add(ActionVm.Checked(ctx, "buildLane", s.Get("lane.build"), detail, ActionKind.Primary, at => new CommissionWorks(at, work)));
        }

        /// <summary>The card: the town's cash now, the section, the cash after; the guard says whether the town can still pay its bills at the next close.</summary>
        internal static void Card(ViewContext ctx, ConfirmCardVm vm, Simulation after, int section)
        {
            var s = ctx.Strings;
            vm.Title = s.Get("lane.cardTitle");
            vm.Subtitle = s.Get("civic.cardSub");
            vm.Rows.Add(new ConfirmRowVm(s.Get("civic.cardNow"), ctx.Fmt.Coins(ctx.Sim.State.Treasury.Cash)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("lane.cardRow"), "− " + ctx.Fmt.Coins(ctx.Sim.State.Treasury.Cash - after.State.Treasury.Cash)));
            vm.Rows.Add(new ConfirmRowVm(s.Get("civic.cardAfter"), ctx.Fmt.Coins(after.State.Treasury.Cash), total: true));
        }

        /// <summary>The budget guard: whether, with the cash left, the town can still pay its bills at the next close.</summary>
        internal static void Guard(ViewContext ctx, ConfirmCardVm vm, Simulation after)
        {
            long shortBy = TreasuryRules.Forecast(after).Short;
            vm.Warning = shortBy > 0 ? ctx.Strings.Get("lane.guardShort", "amount", ctx.Fmt.Coins(shortBy)) : ctx.Strings.Get("lane.guardFine");
            vm.Reassure = shortBy == 0;
        }
    }

    public static partial class WhyView
    {
        private static WhyVm LaneWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int section = (int)(id / 10);
            if (id % 10 == LaneSheet.Progress)
            {
                LaneRules.Progress(sim, section, out int built, out int total);
                WhyVm p = New(ctx, "why.lane.progress.title", ctx.Strings.Get("lane.progressValue", "built", ctx.Fmt.Integer(built), "total", ctx.Fmt.Integer(total)));
                Text(p, ctx, "why.lane.progress.text");
                return p;
            }
            WhyVm vm = New(ctx, "why.lane.cost.title", Money(ctx, LaneRules.Cost(sim, section)));
            Row(vm, ctx, "why.lane.cost.base", Money(ctx, LaneRules.BaseCost(sim, section)));
            Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
            Text(vm, ctx, "why.lane.cost.text");
            return vm;
        }
    }
}
