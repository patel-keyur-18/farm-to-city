using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>Raising calves' Why? pages (US-254, Preview 8): the switch, a heifer's growing and her worth. In words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        /// <summary>The Calves switch: which calves are heifers, what a kept one takes and eats, and what a calf sold fetches.</summary>
        private static WhyVm HeiferKeep(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LivestockDefs defs = LivestockRules.Defs(sim);
            LivestockDef ld = defs.Livestock;
            WhyVm vm = New(ctx, "why.heiferKeep.title", ctx.Strings.Get(sim.State.Livestock.KeepHeifers ? "shed.heifersKept" : "shed.heifersSold"));
            Row(vm, ctx, "why.heiferKeep.share", Pct(ctx, ld.HeiferShare));
            Row(vm, ctx, "why.heiferKeep.ration", Pct(ctx, ld.HeiferRationShare));
            Row(vm, ctx, "why.heiferKeep.labour", ctx.Fmt.Decimal1(ld.HeiferLabourHours) + " h");
            foreach (AnimalDef d in defs.Animals)
                if (!d.Draught)
                    Row(vm, ctx, "why.heiferKeep.seasons", ctx.Strings.Plural("why.seasons", d.GrowSeasons), "animal", ctx.Strings.Get(d.NameKey).ToLowerInvariant());
            Text(vm, ctx, "why.heiferKeep.how");
            vm.Footnote = ctx.Strings.Get("why.heiferKeep.lesson");
            return vm;
        }

        /// <summary>What raising a heifer eats to her first calving, against what a grown animal costs. id = the animal kind.</summary>
        private static WhyVm HeiferRaise(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            LivestockDefs defs = LivestockRules.Defs(sim);
            AnimalDef d = defs.Animals[(int)id];
            Economics.Raising(sim, (int)id, out long bought, out long home);
            string name = ctx.Strings.Get(d.NameKey).ToLowerInvariant();
            WhyVm vm = New(ctx, "why.heiferRaise.title", Money(ctx, home), "animal", name);
            Row(vm, ctx, "why.heiferRaise.days", ctx.Strings.Plural("why.days", LivestockRules.GrowDays(sim, d)), "n", ctx.Strings.Plural("why.seasons", d.GrowSeasons));
            Row(vm, ctx, "why.heiferRaise.share", Pct(ctx, defs.Livestock.HeiferRationShare));
            Row(vm, ctx, "why.heiferRaise.home", Money(ctx, home));
            Row(vm, ctx, "why.heiferRaise.bought", Money(ctx, bought));
            Row(vm, ctx, "why.heiferRaise.price", Money(ctx, MarketRules.Indexed(sim, d.Price)));
            Text(vm, ctx, "why.heiferRaise.how");
            vm.Footnote = ctx.Strings.Get("why.heiferRaise.lesson");
            return vm;
        }

        /// <summary>A heifer's growing: the seasons she needs, how far she is, and why she would wait. id = the animal.</summary>
        private static WhyVm HeiferGrowing(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            AnimalDef d = LivestockRules.Defs(sim).Animals[a.Kind];
            LivestockDef ld = LivestockRules.Defs(sim).Livestock;
            int perSeason = (int)(sim.Clock.SecondsPerSeason / Sim.Core.Definitions.ClockDef.SecondsPerDay);
            WhyVm vm = New(ctx, "why.heiferGrowing.title", ctx.Strings.Plural("why.seasons", ShedSheet.SeasonsLeft(sim, a)), "name", a.Name);
            Row(vm, ctx, "why.heiferGrowing.needs", ctx.Strings.Plural("why.seasons", d.GrowSeasons));
            Row(vm, ctx, "why.heiferGrowing.done", ctx.Strings.Plural("why.days", a.StageDay), "of", ctx.Strings.Plural("why.days", d.GrowSeasons * perSeason));
            Row(vm, ctx, "why.heiferGrowing.fed", Pct(ctx, a.FedScore));
            Row(vm, ctx, "why.heiferGrowing.needFed", Pct(ctx, ld.HungryBelow));
            Text(vm, ctx, a.FedScore < ld.HungryBelow ? "why.heiferGrowing.waiting" : "why.heiferGrowing.growing");
            vm.Footnote = ctx.Strings.Get("why.heiferGrowing.lesson");
            return vm;
        }

        /// <summary>A heifer's worth now: the straight line from the calf's price to the grown price. id = the animal.</summary>
        private static WhyVm HeiferWorth(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Animal a = LivestockRules.Find(sim, id);
            if (a == null) return New(ctx, "why.animal.gone", null);
            AnimalDef d = LivestockRules.Defs(sim).Animals[a.Kind];
            long days = LivestockRules.GrowDays(sim, d), done = System.Math.Min(a.StageDay, days);
            WhyVm vm = New(ctx, "why.heiferWorth.title", Money(ctx, LivestockRules.HeiferValue(sim, a)), "name", a.Name);
            Row(vm, ctx, "why.heiferWorth.calf", Money(ctx, MarketRules.Indexed(sim, d.CalfPrice)));
            Row(vm, ctx, "why.heiferWorth.grown", Money(ctx, MarketRules.Indexed(sim, d.Price)));
            Row(vm, ctx, "why.heiferWorth.done", ctx.Fmt.Integer(done) + " ÷ " + ctx.Fmt.Integer(days));
            Row(vm, ctx, "why.heiferWorth.now", Money(ctx, LivestockRules.HeiferValue(sim, a)));
            Text(vm, ctx, "why.heiferWorth.how");
            vm.Footnote = ctx.Strings.Get("why.heiferWorth.lesson");
            return vm;
        }
    }
}
