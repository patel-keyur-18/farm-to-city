using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>The orchard rows' Why? (spec 013 §7, ADR 0090), by tile id; the plant sheet's by tree index.</summary>
    public static partial class WhyView
    {
        private static Orchard OrchardOn(ViewContext ctx, long tileId) { return OrchardRules.Of(ctx.Sim, (int)tileId); }

        private static string TreeName(ViewContext ctx, int def) { return ctx.Strings.Get(ctx.Sim.Config.Game.Farm.Trees[def].NameKey); }

        private static WhyVm OrchardBears(ViewContext ctx, long id)
        {
            Orchard o = OrchardOn(ctx, id);
            TreeDef def = ctx.Sim.Config.Game.Farm.Trees[o.Def];
            WhyVm vm = New(ctx, "why.treeMatures.title", ctx.Fmt.Duration(System.Math.Max(0, o.PlantedAt + def.GrowthSeconds - ctx.Now)), "tree", TreeName(ctx, o.Def));
            Row(vm, ctx, "why.treeMatures.grows", ctx.Fmt.Duration(def.GrowthSeconds), "tree", TreeName(ctx, o.Def));
            Text(vm, ctx, "why.orchard.between", "share", Pct(ctx, OrchardRules.Defs(ctx.Sim).IntercropShare));
            vm.Footnote = ctx.Strings.Get("why.payback.lesson");
            return vm;
        }

        /// <summary>The ripe batch (its yield), or before it ripens how the next batch is going.</summary>
        private static WhyVm OrchardBatch(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Orchard o = OrchardOn(ctx, id);
            TreeDef def = sim.Config.Game.Farm.Trees[o.Def];
            string tree = TreeName(ctx, o.Def);
            if (o.RipeGrams > 0 && o.Last != null)
            {
                YieldBreakdown b = o.Last;
                WhyVm ripe = New(ctx, "why.orchardBatch.title", ctx.Fmt.Kg(o.RipeGrams), "tree", tree);
                Row(ripe, ctx, "why.orchardBatch.base", Kg(ctx, b.Base), "n", ctx.Fmt.Integer(o.Trees), "kg", ctx.Fmt.Kg(def.BatchGrams));
                Row(ripe, ctx, "why.yield.water", Effect(ctx, b.Water));
                Row(ripe, ctx, "why.yield.care", Effect(ctx, b.Care));
                Row(ripe, ctx, "why.yield.weather", Effect(ctx, b.Weather));
                if (b.Capped) Row(ripe, ctx, "why.yield.cap", Kg(ctx, b.Base * sim.Config.Game.Farm.Farm.YieldCap));
                if (b.Grams > o.RipeGrams) Row(ripe, ctx, "why.orchardBatch.gone", "−" + ctx.Fmt.Kg(b.Grams - o.RipeGrams));
                ripe.Footnote = ctx.Strings.Get("why.orchardBatch.lesson");
                return ripe;
            }
            OrchardSheet.OrchardFactors f = OrchardSheet.Factors(sim, o);
            WhyVm vm = New(ctx, "why.orchardSoFar.title", ctx.Rating(f.Water * f.Care * f.Weather), "tree", tree);
            Row(vm, ctx, "why.orchardBatch.base", Kg(ctx, Fixed.FromRatio(def.BatchGrams * o.Trees, 1000)), "n", ctx.Fmt.Integer(o.Trees), "kg", ctx.Fmt.Kg(def.BatchGrams));
            Row(vm, ctx, "why.yield.water", Effect(ctx, f.Water));
            Row(vm, ctx, "why.yield.care", Effect(ctx, f.Care));
            Row(vm, ctx, "why.yield.weather", Effect(ctx, f.Weather));
            Text(vm, ctx, "why.orchardSoFar.when", "seasons", FieldSheets.SeasonsText(ctx, def.FruitingSeasons));
            vm.Footnote = ctx.Strings.Get("why.yield.lesson");
            return vm;
        }

        private static WhyVm OrchardSpoils(ViewContext ctx, long id)
        {
            Orchard o = OrchardOn(ctx, id);
            long grace = ctx.Sim.Config.Game.Farm.Farm.ReadyGraceSeconds, left = o.RipeAt + grace - ctx.Now;
            WhyVm vm = New(ctx, "why.spoils.title", left > 0 ? ctx.Fmt.Duration(left) : ctx.Strings.Get("field.spoiling"));
            Row(vm, ctx, "why.orchardSpoils.grace", ctx.Fmt.Duration(grace));
            Row(vm, ctx, "why.spoils.rate", Pct(ctx, ctx.Sim.Config.Game.Market.Goods[ctx.Sim.Config.Game.Farm.Trees[o.Def].FruitGood].SpoilagePerDay));
            vm.Footnote = ctx.Strings.Get("why.orchardSpoils.lesson");
            return vm;
        }

        /// <summary>What can be picked today: the store's free room and what the trader can still take.</summary>
        private static WhyVm OrchardToday(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Orchard o = OrchardOn(ctx, id);
            int good = sim.Config.Game.Farm.Trees[o.Def].FruitGood;
            WhyVm vm = New(ctx, "why.orchardToday.title", ctx.Fmt.Kg(OrchardRules.Pickable(sim, o, Overflow.SellRestToTrader)));
            Row(vm, ctx, "why.orchardToday.store", ctx.Fmt.Kg(Store.Fits(sim, good)));
            Row(vm, ctx, "why.orchardToday.trader", ctx.Fmt.Kg(MarketRules.TraderCapacityLeft(sim, good)));
            Row(vm, ctx, "why.orchardToday.ripe", ctx.Fmt.Kg(o.RipeGrams));
            vm.Footnote = ctx.Strings.Get("why.orchardToday.lesson");
            return vm;
        }

        /// <summary>The contractor's offer: the batch at the trader's price, times his share.</summary>
        private static WhyVm OrchardContractor(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Orchard o = OrchardOn(ctx, id);
            int good = sim.Config.Game.Farm.Trees[o.Def].FruitGood;
            WhyVm vm = New(ctx, "why.orchardContractor.title", Money(ctx, OrchardRules.ContractorPrice(sim, o)));
            Row(vm, ctx, "why.orchardContractor.trader", Money(ctx, MarketRules.Value(sim, good, o.RipeGrams, MarketRules.Floor(sim, good))), "kg", ctx.Fmt.Kg(o.RipeGrams));
            Row(vm, ctx, "why.orchardContractor.share", Pct(ctx, OrchardRules.Defs(sim).ContractorShare));
            Text(vm, ctx, "why.orchardContractor.he");
            vm.Footnote = ctx.Strings.Get("why.orchardContractor.lesson");
            return vm;
        }

        private static WhyVm OrchardPayback(ViewContext ctx, long id)
        {
            TreePayback p = TreeRules.Payback(ctx.Sim, OrchardOn(ctx, id));
            WhyVm vm = New(ctx, "why.payback.title", ctx.Strings.Get(p.PaidBack ? "why.payback.done" : "why.payback.notYet"));
            Row(vm, ctx, "why.payback.cost", Money(ctx, p.Cost));
            Row(vm, ctx, "why.payback.returns", Money(ctx, p.Returns));
            Row(vm, ctx, "why.payback.left", Money(ctx, System.Math.Max(0, p.Cost - p.Returns)));
            if (p.Season.HasValue) Row(vm, ctx, "why.payback.season", ctx.Fmt.Integer(p.Season.Value));
            vm.Footnote = ctx.Strings.Get("why.payback.lesson");
            return vm;
        }

        /// <summary>A tree's row on the plant sheet: the saplings with planting, the work and a batch; id = the tree's index.</summary>
        private static WhyVm OrchardPlant(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            int i = (int)id;
            TreeDef def = sim.Config.Game.Farm.Trees[i];
            OrchardTreeDef od = OrchardRules.Defs(sim).Trees[i];
            WhyVm vm = New(ctx, "why.orchardPlant.title", Money(ctx, OrchardRules.Price(sim, i)), "tree", TreeName(ctx, i));
            Row(vm, ctx, "why.orchardPlant.trees", ctx.Fmt.Integer(od.TreesPerAcre));
            Row(vm, ctx, "why.orchardPlant.sapling", Money(ctx, MarketRules.Value(sim, def.SaplingGood, 1, MarketRules.InputPrice(sim, def.SaplingGood))));
            Row(vm, ctx, "why.treeMatures.grows", ctx.Fmt.Duration(def.GrowthSeconds), "tree", TreeName(ctx, i));
            Row(vm, ctx, "why.orchardPlant.work", ctx.Strings.Get("why.orchardPlant.hours", "young", ctx.Fmt.Decimal1(od.LabourYoung), "bearing", ctx.Fmt.Decimal1(od.LabourBearing)));
            Row(vm, ctx, "why.orchardBatch.base", Kg(ctx, Fixed.FromRatio(def.BatchGrams * od.TreesPerAcre, 1000)), "n", ctx.Fmt.Integer(od.TreesPerAcre), "kg", ctx.Fmt.Kg(def.BatchGrams));
            if (def.HarvestLimit > 0) Row(vm, ctx, "why.orchardPlant.limit", ctx.Fmt.Integer(def.HarvestLimit));
            vm.Footnote = ctx.Strings.Get("why.payback.lesson");
            return vm;
        }
    }
}
