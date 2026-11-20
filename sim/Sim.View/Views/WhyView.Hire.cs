using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Equipment;

namespace Sim.View.Views
{
    /// <summary>The Why? pages of hiring a machine out to neighbours (US-256, Preview 10): the arithmetic in words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        /// <summary>What neighbours pay: the day's hire, the acres it covers, the share, and the most they want a day.</summary>
        private static WhyVm HireFee(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Machine m = EquipmentRules.Find(sim, id);
            if (m == null) return New(ctx, "why.hireFee.title", "");
            MachineDef d = EquipmentRules.Defs(sim).Machines[m.Kind];
            WhyVm vm = New(ctx, "why.hireFee.title", ctx.Fmt.PaiseCoins(EquipmentRules.HireFeePerAcre(sim, d)));
            Row(vm, ctx, "why.hireFee.day", ctx.Fmt.Coins(EquipmentRules.Hire(sim, d)));
            Row(vm, ctx, "why.hireFee.fields", ctx.Fmt.Integer(d.Acres));
            Row(vm, ctx, "why.hireFee.acre", ctx.Fmt.PaiseCoins(EquipmentRules.Hire(sim, d) / d.Acres));
            Row(vm, ctx, "why.hireFee.share", ctx.Fmt.PaiseCoins(EquipmentRules.HireFeePerAcre(sim, d)), "pct", ctx.Fmt.Percent(EquipmentRules.Defs(sim).CustomFeeShare));
            Row(vm, ctx, "why.hireFee.cap", ctx.Fmt.Integer(d.CustomAcresPerDay));
            Text(vm, ctx, "why.hireFee.how");
            vm.Footnote = ctx.Strings.Get("why.hireFee.lesson");
            return vm;
        }

        /// <summary>What it costs you: diesel an acre and a day, the doubled chance of breaking and the repair it risks.</summary>
        private static WhyVm HireCost(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Machine m = EquipmentRules.Find(sim, id);
            if (m == null) return New(ctx, "why.hireCost.title", "");
            MachineDef d = EquipmentRules.Defs(sim).Machines[m.Kind];
            EquipmentDefs e = EquipmentRules.Defs(sim);
            EquipmentRules.HiredOutSeason(sim, m, out int acres, out long fees, out long diesel);
            WhyVm vm = New(ctx, "why.hireCost.title", ctx.Fmt.Coins(diesel));
            Row(vm, ctx, "why.hireCost.dieselAcre", ctx.Fmt.PaiseCoins(EquipmentRules.Diesel(sim, d, 1) / d.Acres));
            Row(vm, ctx, "why.hireCost.dieselDay", ctx.Fmt.Coins(EquipmentRules.HireDiesel(sim, d)), "acres", ctx.Fmt.Integer(d.CustomAcresPerDay));
            Row(vm, ctx, "why.hireCost.chance", ctx.Fmt.Percent((m.Maintained ? e.BreakdownMaintained : e.BreakdownSkipped) * Sim.Core.Maths.Fixed.FromInt(e.CustomWear)),
                "own", ctx.Fmt.Percent(m.Maintained ? e.BreakdownMaintained : e.BreakdownSkipped));
            Row(vm, ctx, "why.hireCost.repair", ctx.Fmt.Coins(EquipmentRules.Repair(sim, m)), "pct", ctx.Fmt.Percent(e.RepairShare));
            Row(vm, ctx, "why.hireCost.wearDay", ctx.Fmt.Coins(EquipmentRules.HireWear(sim, m)));
            Row(vm, ctx, "why.hireCost.season", ctx.Strings.Get("equipment.hireSeasonValue", "acres", ctx.Fmt.Integer(acres), "fees", ctx.Fmt.Coins(fees)));
            Text(vm, ctx, "why.hireCost.how");
            return vm;
        }

        /// <summary>Does hiring it out pay for the machine? The sum of a season at the cap, against what the machine costs to stand there.</summary>
        private static WhyVm HirePays(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Machine m = EquipmentRules.Find(sim, id);
            if (m == null) return New(ctx, "why.hirePays.title", "");
            MachineDef d = EquipmentRules.Defs(sim).Machines[m.Kind];
            int days = sim.Clock.DaysPerSeason;
            long net = EquipmentRules.HireNetPerSeason(sim, m);
            WhyVm vm = New(ctx, "why.hirePays.title", ctx.Fmt.Coins(net));
            Row(vm, ctx, "why.hirePays.fees", ctx.Fmt.Coins(EquipmentRules.HireFee(sim, d) * days), "days", ctx.Fmt.Integer(days), "acres", ctx.Fmt.Integer(d.CustomAcresPerDay));
            Row(vm, ctx, "why.hirePays.diesel", "−" + ctx.Fmt.Coins(EquipmentRules.HireDiesel(sim, d) * days));
            Row(vm, ctx, "why.hirePays.wear", "−" + ctx.Fmt.Coins(EquipmentRules.HireWear(sim, m) * days));
            Row(vm, ctx, "why.hirePays.left", ctx.Fmt.Coins(net));
            Row(vm, ctx, "why.hirePays.depreciation", "−" + ctx.Fmt.Coins(m.Cost / d.LifeSeasons), "cost", ctx.Fmt.Coins(m.Cost), "life", ctx.Fmt.Integer(d.LifeSeasons));
            Row(vm, ctx, "why.hirePays.upkeep", "−" + ctx.Fmt.Coins(EquipmentRules.Maintenance(sim, m)));
            Text(vm, ctx, "why.hirePays.how");
            vm.Footnote = ctx.Strings.Get("why.hirePays.lesson");
            return vm;
        }
    }
}
