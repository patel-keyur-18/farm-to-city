using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The brick kiln's sheets (US-243, spec 025 §3.3, Preview 3): its market research and its day. Every number has a *Why?* (the <c>kiln</c> source,
    /// id = the row below).
    /// </summary>
    public static class KilnSheet
    {
        public const string Why = "kiln";
        public const int Want = 1, Trader = 2, Takes = 3, Fuel = 4, Fires = 5, Made = 6, Yard = 7, Builds = 8, Sold = 9, Cost = 10;

        /// <summary>A price a brick, from an amount of 1/100 coin: "8.32 /brick".</summary>
        public static string PerBrick(ViewContext ctx, long amount) { return ctx.Strings.Get("unit.perBrick", "amount", Plain(ctx, amount)); }

        /// <summary>The same without the unit: "8.32".</summary>
        public static string Plain(ViewContext ctx, long amount) { return ctx.Fmt.Decimal2(Fixed.FromRatio(amount, 100)); }

        /// <summary>Bricks the started builds still lack: yours, the town's and the builders'.</summary>
        public static long Wanted(Simulation sim)
        {
            long n = 0;
            if (sim.State.Township == null) return 0;
            foreach (BuildProject p in sim.State.Township.Projects)
                if (!p.Working) n += ConstructionRules.Need(sim, p, 0) - ConstructionRules.Got(p, 0);
            return n;
        }

        internal static void AddResearch(ViewContext ctx, SheetVm vm, BusinessTypeDef t, int type)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            KilnDef k = t.Kiln;
            ConstructionDef d = ConstructionRules.Def(sim);
            vm.Lines.Add(new LineVm(s.Get("research.kilnWant"), s.Get("research.kilnWantValue", "n", ctx.Fmt.Integer(Wanted(sim))), Why, Want) { Icon = "brick_kiln" });
            vm.Lines.Add(new LineVm(s.Get("research.kilnTrader"), s.Get("research.kilnTraderValue", "n", ctx.Fmt.Integer(d.DailyBricks), "price", PerBrick(ctx, KilnRules.TraderPrice(sim))), Why, Trader) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("research.kilnTakes"), s.Get("research.kilnTraderValue", "n", ctx.Fmt.Integer(k.TraderPerDay), "price", PerBrick(ctx, KilnRules.PaysFor(sim, k, 1))), Why, Takes) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("research.kilnFuel"), s.Get("research.kilnFuelValue", "price", ctx.Fmt.PricePerKg(k.FuelPrice * sim.State.Market.PriceIndex), "kg", ctx.Fmt.Decimal1(k.FuelKg)), Why, Fuel) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("research.wage", "role", s.Get("business.role." + t.Worker)), s.Get("research.wageValue", "wage", ctx.Fmt.Coins(BusinessRules.Wage(sim, t))), "businessWage", type)
                { Icon = "person_hand_m" });
            vm.Lines.Add(new LineVm(s.Get("research.kilnFires"), s.Get("research.kilnFiresValue"), Why, Fires) { Icon = "brick_kiln" });
        }

        /// <summary>The acres a kiln on <paramref name="tile"/> would smoke: the placement ghost's red footprint. Empty for any other type.</summary>
        public static System.Collections.Generic.List<int> Footprint(Simulation sim, BusinessTypeDef t, int tile)
        {
            SmokeSource src = t.IsKiln ? SmokeRules.Def(sim)?.Source("kiln") : null;
            return src == null ? new System.Collections.Generic.List<int>() : SmokeRules.Footprint(sim, tile, src);
        }

        /// <summary>The confirm card's sentence: what its smoke touches, counted from the map now, and how long it takes to build.</summary>
        public static string Smoke(ViewContext ctx, BusinessTypeDef t, int tile)
        {
            var s = ctx.Strings;
            System.Collections.Generic.List<int> acres = Footprint(ctx.Sim, t, tile);
            SmokeRules.Touches(ctx.Sim, acres, out int yours, out int homes);
            return s.Get("confirm.FoundBusiness.kiln", "acres", s.Plural("land.acres", acres.Count), "yours", ctx.Fmt.Integer(yours), "homes", homes == 0 ? s.Get("confirm.kilnHomes.none") : s.Plural("confirm.kilnHomes", homes), "time", ctx.Fmt.Duration(t.BuildHours * 3600L));
        }

        /// <summary>The kiln's day: bricks made, in the yard, sent to your builds, sold to the trader, and what a brick costs.</summary>
        internal static void AddToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            KilnDef k = t.Kiln;
            long id = b.Id * 100;
            bool today = b.Day == (ctx.Now - 3600) / 86_400 || b.Day == ctx.Now / 86_400;
            long most = KilnRules.PerHour(sim, b) * (b.CloseHour - b.OpenHour);
            if (b.Status == BusinessStatus.Open && !KilnRules.Fires(sim)) vm.Lines.Add(new LineVm(s.Get("kiln.monsoonNote"), null));
            vm.Lines.Add(new LineVm(s.Get("kiln.made"), s.Get("kiln.madeValue", "n", ctx.Fmt.Integer(today ? b.KilnMade : 0), "of", ctx.Fmt.Integer(KilnRules.Fires(sim) ? most : 0)), Why, id + Made) { Icon = "brick_kiln" });
            vm.Lines.Add(new LineVm(s.Get("kiln.yard"), s.Get("kiln.yardValue", "n", ctx.Fmt.Integer(b.Yard), "of", ctx.Fmt.Integer(k.YardMax)), Why, id + Yard) { Icon = "brick_kiln" });
            vm.Lines.Add(new LineVm(s.Get("kiln.builds"), s.Get("kiln.buildsValue", "n", ctx.Fmt.Integer(today ? b.KilnToBuilds : 0)), Why, id + Builds) { Icon = "family_house" });
            vm.Lines.Add(new LineVm(s.Get("kiln.sold"), s.Get("kiln.soldValue", "n", ctx.Fmt.Integer(today ? b.KilnSold : 0), "price", PerBrick(ctx, KilnRules.PaysFor(sim, k, 1))), Why, id + Sold) { Icon = "coin" });
            vm.Lines.Add(new LineVm(s.Get("kiln.cost"), s.Get("kiln.costValue", "price", Plain(ctx, KilnRules.FuelCost(sim, b, k, 1))), Why, id + Cost) { Icon = "coin" });
        }
    }
}
