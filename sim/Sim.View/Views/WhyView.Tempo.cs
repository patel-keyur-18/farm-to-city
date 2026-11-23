using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Equipment;

namespace Sim.View.Views
{
    /// <summary>The tempo (US-245): its leg, the ride a load gets, what neighbours pay and whether it pays, in words (ADR 0043).</summary>
    public static partial class WhyView
    {
        private static WhyVm TempoWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            MachineDef d = System.Linq.Enumerable.First(EquipmentRules.Defs(sim).Machines, x => x.IsTempo);
            TripDef t = d.Trip;
            Machine m = TempoRules.Of(sim);
            long leg = TempoRules.LegCost(sim, t), diesel = TempoRules.Diesel(sim, t), wear = TempoRules.Wear(sim, t), trolley = TempoRules.Trolley(sim), pay = TempoRules.NeighbourPay(sim, t);
            switch ((int)id)
            {
                case TempoSheet.Ride:
                {
                    WhyVm vm = New(ctx, "why.tempoRide.title", null);
                    Text(vm, ctx, "why.tempoRide.text", "leg", Money(ctx, leg), "trolley", Money(ctx, trolley), "kg", ctx.Fmt.Kg(t.LoadKg * 1000 * t.LegsPerDay));
                    return vm;
                }
                case TempoSheet.LetOutPay:
                {
                    WhyVm vm = New(ctx, "why.tempoLetOut.title", Money(ctx, pay));
                    Row(vm, ctx, "why.tempoLetOut.pay", Money(ctx, pay));
                    Row(vm, ctx, "why.tempoLetOut.cost", "−" + Money(ctx, leg));
                    Row(vm, ctx, "why.tempoLetOut.net", Money(ctx, pay - leg));
                    Text(vm, ctx, "why.tempoLetOut.text", "legs", ctx.Fmt.Integer(t.LegsPerDay));
                    return vm;
                }
                case TempoSheet.Worth:
                {
                    int days = sim.Clock.DaysPerSeason;
                    WhyVm vm = New(ctx, "why.tempoPays.title", m == null ? "" : Money(ctx, TempoRules.BestPerSeason(sim, m)));
                    Row(vm, ctx, "why.tempoPays.saves", Money(ctx, TempoRules.SavesPerLeg(sim, t)), "trolley", Money(ctx, trolley), "leg", Money(ctx, leg));
                    Row(vm, ctx, "why.tempoPays.legs", ctx.Fmt.Integer(days * t.LegsPerDay), "days", ctx.Fmt.Integer(days), "perDay", ctx.Fmt.Integer(t.LegsPerDay));
                    if (m != null)
                    {
                        Row(vm, ctx, "why.tempoPays.standing", "−" + Money(ctx, TempoRules.StandingPerSeason(sim, m)));
                        Row(vm, ctx, "why.tempoPays.letting", Money(ctx, TempoRules.LetOutPerSeason(sim, m)));
                    }
                    Text(vm, ctx, "why.tempoPays.text");
                    vm.Footnote = ctx.Strings.Get("why.tempoPays.lesson");
                    return vm;
                }
                default:
                {
                    WhyVm vm = New(ctx, "why.tempoLeg.title", Money(ctx, leg));
                    Text(vm, ctx, "why.tempoLeg.text", "kg", ctx.Fmt.Kg(t.LoadKg * 1000), "hours", ctx.Fmt.Integer(t.Hours), "diesel", Money(ctx, diesel), "wear", Money(ctx, wear), "legs", ctx.Fmt.Integer(t.LegsPerDay));
                    if (m != null) Row(vm, ctx, "why.tempoLeg.used", ctx.Fmt.Integer(TempoRules.UsedToday(sim, m)), "of", ctx.Fmt.Integer(t.LegsPerDay));
                    return vm;
                }
            }
        }
    }
}
