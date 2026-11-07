using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Equipment;

namespace Sim.View.Views
{
    public static partial class WhyView
    {
        /// <summary>A machine (spec 013 §3.2), by kind: what it works, saves and costs to run, and buying against leasing or hiring.</summary>
        private static WhyVm Machine(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            MachineDef d = EquipmentRules.Defs(sim).Machines[(int)id];
            long price = EquipmentRules.Price(sim, d) + EquipmentRules.BullocksPrice(sim, d);
            WhyVm vm = New(ctx, "why.machine.title", d.Bullocks > 0 ? ctx.Strings.Get("equipment.withPair", "price", ctx.Fmt.Coins(price)) : ctx.Fmt.Coins(price),
                "machine", ctx.Strings.Get(d.NameKey).ToLowerInvariant()); // the price: the whole "Works 20 fields · …" line overran the title
            Row(vm, ctx, "why.machine.acres", ctx.Fmt.Integer(d.Acres));
            Row(vm, ctx, "why.machine.labour", ctx.Fmt.Percent(d.LabourCut)); // saved: no minus, so it isn't drawn as a loss
            Row(vm, ctx, "why.machine.tools", Effect(ctx, d.Tools));
            if (d.CartKg > 0) Row(vm, ctx, "why.machine.cart", ctx.Fmt.Kg(d.CartKg * 1000));
            if (d.Bullocks > 0) Row(vm, ctx, "why.machine.bullocks", ctx.Fmt.Integer(d.Bullocks));
            if (d.DieselPerAcreSeason > 0) Row(vm, ctx, "why.machine.diesel", Money(ctx, Sim.Core.Market.MarketRules.Indexed(sim, d.DieselPerAcreSeason)));
            if (d.MaintenanceShare > Sim.Core.Maths.Fixed.Zero) Row(vm, ctx, "why.machine.upkeep", ctx.Fmt.Percent(d.MaintenanceShare));
            Row(vm, ctx, "why.machine.breaks", ctx.Fmt.Percent(EquipmentRules.Defs(sim).BreakdownMaintained) + " · " + ctx.Fmt.Percent(EquipmentRules.Defs(sim).BreakdownSkipped));
            Row(vm, ctx, "why.machine.repair", ctx.Fmt.Percent(EquipmentRules.Defs(sim).RepairShare));
            Row(vm, ctx, "why.machine.life", ctx.Strings.Plural("why.seasons", d.LifeSeasons));
            vm.Footnote = ctx.Strings.Get("why.machine.lesson");
            return vm;
        }

        /// <summary>A bought machine's book and market value (spec 005 §3.3): cost, straight-line depreciation so far, resale at 80% of book.</summary>
        private static WhyVm MachineValue(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Machine m = EquipmentRules.Find(sim, id);
            if (m == null) return New(ctx, "why.machineValue.title", "");
            MachineDef d = EquipmentRules.Defs(sim).Machines[m.Kind];
            WhyVm vm = New(ctx, "why.machineValue.title", Money(ctx, EquipmentRules.Book(m)));
            Row(vm, ctx, "why.machineValue.cost", Money(ctx, m.Cost));
            Row(vm, ctx, "why.machineValue.depreciated", Money(ctx, m.Depreciated), "n", ctx.Fmt.Integer(m.SeasonsDepreciated), "of", ctx.Fmt.Integer(d.LifeSeasons));
            Row(vm, ctx, "why.machineValue.market", Money(ctx, EquipmentRules.Resale(sim, m)), "share", ctx.Fmt.Percent(EquipmentRules.Defs(sim).ResaleShare));
            if (EquipmentRules.Maintenance(sim, m) > 0) Row(vm, ctx, "why.machineValue.upkeep", Money(ctx, EquipmentRules.Maintenance(sim, m)));
            vm.Footnote = ctx.Strings.Get("why.machineValue.lesson");
            return vm;
        }

        /// <summary>The field's "Worked by" (spec 013 §3.2): the machine working it today and what it does here, or hand tools.</summary>
        private static WhyVm FieldTools(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int k = EquipmentRules.Cover(sim)[(int)id];
            if (k < 0)
            {
                WhyVm hand = New(ctx, "why.fieldTools.title", ctx.Strings.Get("field.handTools"));
                Text(hand, ctx, "why.fieldTools.hand");
                return hand;
            }
            MachineDef d = EquipmentRules.Defs(sim).Machines[k];
            WhyVm vm = New(ctx, "why.fieldTools.title", ctx.Strings.Get(d.NameKey));
            Text(vm, ctx, EquipmentRules.Hired(sim, k) ? "why.fieldTools.hired" : "why.fieldTools.yours");
            Row(vm, ctx, "why.machine.labour", ctx.Fmt.Percent(d.LabourCut)); // saved: no minus, so it isn't drawn as a loss
            Row(vm, ctx, "why.fieldTools.tools", Effect(ctx, d.Tools));
            vm.Footnote = ctx.Strings.Get("why.fieldTools.lesson");
            return vm;
        }
    }
}
