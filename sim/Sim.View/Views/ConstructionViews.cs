using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// Construction from the Township (spec 025 §3.2, US-145), as previewed to the owner (Preview 2): a site's sheet (what a building waits for, its
    /// masons, when it is ready), <b>Building materials</b> (the trader's stock today, the queue, the masons), the cards' rows and the masons' list.
    /// Every number has a *Why?* (the <c>build</c> source, id = project × 100 + row; the stock's rows have no project).
    /// </summary>
    public static class BuildingSheet
    {
        public const string Kind = "Building";
        public const int Bricks = 1, Cement = 2, Timber = 3, Masons = 4, Ready = 5, Status = 6, StockBricks = 7, StockCement = 8, StockTimber = 9, MasonsTown = 10, BuildersHomes = 11, FromKiln = 12;
        private const string Why = "build";

        public static string Good(ViewContext ctx, int g) { return ctx.Strings.Get("build.good." + g); }

        /// <summary>An amount of a good: bricks, bags of cement, timber in cubic metres (the sim keeps tenths).</summary>
        public static string Amount(ViewContext ctx, int g, long qty)
        {
            if (g < 2) return ctx.Strings.Get("build.amount." + g, "n", ctx.Fmt.Integer(qty));
            return ctx.Strings.Get("build.amount.2", "n", qty % 10 == 0 ? ctx.Fmt.Integer(qty / 10) : ctx.Fmt.Integer(qty / 10) + "." + (qty % 10));
        }

        /// <summary>An amount without its unit (the unit follows the other figure): "2,000 of 2,400 bricks".</summary>
        public static string Plain(ViewContext ctx, int g, long qty)
        {
            return g < 2 ? ctx.Fmt.Integer(qty) : qty % 10 == 0 ? ctx.Fmt.Integer(qty / 10) : ctx.Fmt.Integer(qty / 10) + "." + (qty % 10);
        }

        private static string Name(ViewContext ctx, BuildProject p) { return ctx.Strings.Get("build.kind." + p.Kind); }

        // ---- a site ----

        /// <summary>The lines of a building's site: its status, what it has of each material, its masons and when it is ready.</summary>
        internal static void Site(ViewContext ctx, SheetVm vm, BuildProject p)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = p.Id * 100;
            int missing = ConstructionRules.Missing(sim, p);
            vm.Lines.Add(new LineVm(s.Get("build.status"), p.Working ? s.Get("build.building") : s.Get("build.waitingFor", "good", Good(ctx, missing)), Why, id + Status));
            if (p.Working) vm.Lines.Add(new LineVm(s.Get("build.materials"), s.Get("build.allIn"), Why, id + Bricks));
            else
                for (int g = 0; g < 3; g++)
                    vm.Lines.Add(new LineVm(Good(ctx, g).Substring(0, 1).ToUpperInvariant() + Good(ctx, g).Substring(1), s.Get("build.of", "got", Plain(ctx, g, ConstructionRules.Got(p, g)), "need", Amount(ctx, g, ConstructionRules.Need(sim, p, g))), Why, id + 1 + g));
            vm.Lines.Add(new LineVm(s.Get(p.Working ? "build.mastersAt" : "build.mastersKept"), p.Gang ? s.Get("build.gang") : s.Get("build.masonsN", "n", ctx.Fmt.Integer(p.Masons.Count)), Why, id + Masons));
            vm.Lines.Add(new LineVm(s.Get("build.readyIn"), s.Get("build.about", "time", ctx.Fmt.Duration(ConstructionRules.EstimateReady(sim, p))), Why, id + Ready));
            vm.Actions.Add(ActionVm.Open("materials", s.Get("build.materialsToday"), new SheetRequest(Kind, 0), s.Get("build.materialsDetail")));
        }

        /// <summary>A house site's value on the town's list: what it waits for, or when it is ready.</summary>
        internal static string HouseValue(ViewContext ctx, Construction c)
        {
            BuildProject p = Of(ctx.Sim, c);
            if (p == null) return ctx.Strings.Get("people.hutReadyIn", "time", ctx.Fmt.Duration(c.ReadyAt - ctx.Now));
            return p.Working ? ctx.Strings.Get("people.hutReadyIn", "time", ctx.Fmt.Duration(p.ReadyAt - ctx.Now)) : ctx.Strings.Get("build.waitingFor", "good", Good(ctx, ConstructionRules.Missing(ctx.Sim, p)));
        }

        internal static BuildProject Of(Simulation sim, Construction c)
        {
            return sim.State.Township?.Projects.Find(x => x.House == c.Id && x.Owner == BuildProject.Yours && !x.Upgrade);
        }

        // ---- the sheet: building materials ----

        public static SheetVm Build(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var vm = new SheetVm { Icon = "brick", Title = s.Get("build.title"), Subtitle = s.Get("build.subtitle") };
            if (!ConstructionRules.On(sim)) { vm.Subtitle = s.Get("build.notYet"); return vm; }
            vm.Lines.Add(LineVm.Header(s.Get("build.hdrTrader")));
            for (int g = 0; g < 3; g++)
                vm.Lines.Add(new LineVm(Good(ctx, g).Substring(0, 1).ToUpperInvariant() + Good(ctx, g).Substring(1),
                    s.Get("build.left", "left", Plain(ctx, g, ConstructionRules.Left(sim, g)), "limit", Amount(ctx, g, ConstructionRules.Limit(sim, g))), Why, StockBricks + g));
            long fromKiln = ConstructionRules.KilnToday(sim);
            if (fromKiln > 0) vm.Lines.Add(new LineVm(s.Get("build.fromKiln"), Plain(ctx, 0, fromKiln), Why, FromKiln));
            var kilns = KilnRules.Open(sim);
            if (kilns.Count > 0)
            {
                vm.Lines.Add(LineVm.Header(s.Get("build.hdrKiln")));
                foreach (Sim.Core.Business.Business b in kilns)
                    vm.Lines.Add(new LineVm(s.Get("kiln.yard"), s.Get("kiln.yardValue", "n", ctx.Fmt.Integer(b.Yard), "of", ctx.Fmt.Integer(KilnRules.Def(sim, b).YardMax)), KilnSheet.Why, b.Id * 100 + KilnSheet.Yard) { Icon = "brick_kiln" });
            }
            vm.Lines.Add(LineVm.Header(s.Get("build.hdrQueue")));
            int n = 0;
            foreach (BuildProject p in sim.State.Township.Projects)
            {
                if (p.Owner == BuildProject.Builders) continue;
                string state = p.Working ? s.Get("build.buildingLeft", "time", ctx.Fmt.Duration(p.ReadyAt - ctx.Now)) : s.Get("build.waitingFor", "good", Good(ctx, ConstructionRules.Missing(sim, p)));
                var line = new LineVm(s.Get("build.queueRow", "n", ctx.Fmt.Integer(++n), "what", Name(ctx, p)), state, Why, p.Id * 100 + Status);
                vm.Lines.Add(line);
            }
            int builders = ConstructionRules.BuildersProjects(sim);
            if (builders > 0) vm.Lines.Add(new LineVm(s.Get("build.buildersRow"), s.Get("build.buildersValue", "n", ctx.Fmt.Integer(builders)), Why, BuildersHomes));
            if (n == 0 && builders == 0) vm.Lines.Add(new LineVm(s.Get("build.nothing"), null));
            vm.Lines.Add(LineVm.Header(s.Get("build.hdrMasons")));
            int all = ConstructionRules.AllMasons(sim).Count, free = ConstructionRules.FreeMasons(sim).Count;
            vm.Lines.Add(new LineVm(s.Get("build.inTown"), s.Get("build.masonsValue", "n", ctx.Fmt.Integer(all), "kept", ctx.Fmt.Integer(all - free), "free", ctx.Fmt.Integer(free)), Why, MasonsTown));
            return vm;
        }

        // ---- the hiring board's masons ----

        internal static void MasonList(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get("build.hdrMasons")));
            List<long> all = ConstructionRules.AllMasons(sim);
            if (all.Count == 0) vm.Lines.Add(new LineVm(s.Get("board.masonsNone"), null));
            foreach (long id in all)
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                BuildProject kept = sim.State.Township.Projects.Find(x => x.Masons.Contains(id));
                vm.Lines.Add(new LineVm(PeopleWords.Name(ctx, c.Name), kept == null ? s.Get("board.masonFree") : s.Get("board.masonKept", "what", Name(ctx, kept))) { Icon = PeopleWords.Female(ctx, c.Name) ? "person_hand_f" : "person_hand_m" });
            }
            vm.Lines.Add(new LineVm(s.Get("board.masonsNote"), null));
        }

        // ---- the cards ----

        /// <summary>The materials and masons rows of a card, from the quote of what starts now.</summary>
        internal static void CardRows(ViewContext ctx, ConfirmCardVm vm, BuildQuote q, string nowKey, string afterKey, long now, long after)
        {
            var s = ctx.Strings;
            vm.Rows.Add(new ConfirmRowVm(s.Get(nowKey), ctx.Fmt.Coins(now)));
            if (q.KilnBricks > 0) // your own kiln's bricks at what they cost it (US-243), the rest from the trader
            {
                vm.Rows.Add(new ConfirmRowVm(s.Get("build.cardKilnBricks", "n", ctx.Fmt.Integer(q.KilnBricks)), "− " + ctx.Fmt.Coins(q.KilnCost)));
                vm.Rows.Add(new ConfirmRowVm(s.Get(q.KilnBricks == q.Bricks ? "build.cardKilnRest" : "build.cardKilnRestAll"), "− " + ctx.Fmt.Coins(q.Materials - q.KilnCost)));
            }
            else vm.Rows.Add(new ConfirmRowVm(s.Get("build.cardMaterials"), "− " + ctx.Fmt.Coins(q.Materials)));
            vm.Rows.Add(new ConfirmRowVm(q.Gang ? s.Get("build.cardGang", "days", ctx.Fmt.Integer(q.MasonDays)) : s.Get("build.cardMasons", "days", ctx.Fmt.Integer(q.MasonDays), "n", ctx.Fmt.Integer(q.Masons.Count)), "− " + ctx.Fmt.Coins(q.MasonPay)));
            vm.Rows.Add(new ConfirmRowVm(s.Get(afterKey), ctx.Fmt.Coins(after), total: true));
        }

        /// <summary>What your kiln's bricks save on a build: the trader's price less what they cost it, in words ("" without a kiln's bricks).</summary>
        internal static string KilnSaving(ViewContext ctx, BuildQuote q)
        {
            if (q.KilnBricks <= 0) return "";
            long saving = Sim.Core.Market.MarketRules.Indexed(ctx.Sim, q.KilnBricks * ConstructionRules.Def(ctx.Sim).BrickPrice) - q.KilnCost;
            return saving > 0 ? " " + ctx.Strings.Get("build.kilnSaving", "saving", ctx.Fmt.Coins(saving)) : "";
        }

        /// <summary>When the building just started will be ready, in words: what it waits for, or that the masons start now.</summary>
        internal static string Timing(ViewContext ctx, Simulation after, bool yours)
        {
            var s = ctx.Strings;
            List<BuildProject> all = after.State.Township.Projects;
            if (all.Count == 0) return null;
            BuildProject p = all[all.Count - 1];
            string money = s.Get(yours ? "build.moneyYours" : "build.moneyTown");
            if (p.Working) return s.Get("build.timingNow", "time", ctx.Fmt.Duration(p.ReadyAt - after.State.Clock.SimTime)) + " " + money;
            return s.Get("build.timingHeld", "good", Good(ctx, ConstructionRules.Missing(after, p)), "time", ctx.Fmt.Duration(ConstructionRules.EstimateReady(after, p))) + " " + money;
        }

        // ---- the alert ----

        internal static void Alerts(ViewContext ctx, List<AlertVm> list)
        {
            Simulation sim = ctx.Sim;
            if (!ConstructionRules.On(sim)) return;
            var s = ctx.Strings;
            foreach (BuildProject p in sim.State.Township.Projects)
            {
                if (p.Working || p.Owner == BuildProject.Builders) continue;
                list.Add(new AlertVm
                {
                    Level = AlertLevel.Info, Text = s.Get("alert.buildHeld", "what", Name(ctx, p), "good", Good(ctx, ConstructionRules.Missing(sim, p))),
                    Action = ActionVm.Open("materials", s.Get("action.open"), new SheetRequest(Kind, 0)),
                });
            }
        }
    }

    public static partial class WhyView
    {
        private static WhyVm BuildWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ConstructionDefs(sim, out Sim.Core.Definitions.ConstructionDef d);
            int row = (int)(id % 100);
            long projectId = id / 100;
            BuildProject p = projectId == 0 ? null : sim.State.Township.Projects.Find(x => x.Id == projectId);
            WhyVm vm;
            if (row >= BuildingSheet.StockBricks && row <= BuildingSheet.StockTimber)
            {
                int g = row - BuildingSheet.StockBricks;
                vm = New(ctx, "why.build.stock.title", BuildingSheet.Amount(ctx, g, ConstructionRules.Left(sim, g)));
                Row(vm, ctx, "why.build.stock.limit", BuildingSheet.Amount(ctx, g, ConstructionRules.Limit(sim, g)));
                Row(vm, ctx, "why.build.stock.taken", BuildingSheet.Amount(ctx, g, ConstructionRules.Limit(sim, g) - ConstructionRules.Left(sim, g)));
                Text(vm, ctx, "why.build.stock.text", "hour", ctx.Fmt.Integer(d.RestockHour));
                return vm;
            }
            if (row == BuildingSheet.FromKiln)
            {
                vm = New(ctx, "why.build.kiln.title", ctx.Fmt.Integer(ConstructionRules.KilnToday(sim)));
                Text(vm, ctx, "why.build.kiln.text");
                return vm;
            }
            if (row == BuildingSheet.MasonsTown)
            {
                int all = ConstructionRules.AllMasons(sim).Count, free = ConstructionRules.FreeMasons(sim).Count;
                vm = New(ctx, "why.build.masonsTown.title", ctx.Fmt.Integer(all));
                Row(vm, ctx, "why.build.masonsTown.kept", ctx.Fmt.Integer(all - free));
                Row(vm, ctx, "why.build.masonsTown.free", ctx.Fmt.Integer(free));
                Text(vm, ctx, "why.build.masonsTown.text", "day", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, d.MasonDay)));
                return vm;
            }
            if (row == BuildingSheet.BuildersHomes)
            {
                vm = New(ctx, "why.build.builders.title", ctx.Fmt.Integer(ConstructionRules.BuildersProjects(sim)));
                Row(vm, ctx, "why.build.builders.after", ctx.Fmt.Integer(ConstructionRules.BuildersProjects(sim)));
                Text(vm, ctx, "why.build.builders.text");
                return vm;
            }
            if (p == null) { vm = New(ctx, "why.build.gone.title", s.Get("build.building")); Row(vm, ctx, "why.build.gone.row", s.Get("build.allIn")); return vm; }
            Sim.Core.Definitions.BuildKindDef k = d.Kind(p.Kind);
            switch (row)
            {
                case BuildingSheet.Bricks: case BuildingSheet.Cement: case BuildingSheet.Timber:
                    int g = row - 1;
                    vm = New(ctx, "why.build.good.title", BuildingSheet.Amount(ctx, g, ConstructionRules.Need(sim, p, g)), "good", BuildingSheet.Good(ctx, g));
                    Row(vm, ctx, "why.build.good.got", BuildingSheet.Amount(ctx, g, ConstructionRules.Got(p, g)));
                    Row(vm, ctx, "why.build.good.limit", BuildingSheet.Amount(ctx, g, ConstructionRules.Limit(sim, g)));
                    Text(vm, ctx, "why.build.good.text", "good", BuildingSheet.Good(ctx, g));
                    return vm;
                case BuildingSheet.Masons:
                    vm = New(ctx, "why.build.masons.title", p.Gang ? s.Get("build.gang") : s.Get("build.masonsN", "n", ctx.Fmt.Integer(p.Masons.Count)));
                    Row(vm, ctx, "why.build.masons.days", ctx.Fmt.Integer(k.MasonDays * p.Units));
                    Row(vm, ctx, "why.build.masons.pay", Money(ctx, p.MasonPay));
                    Text(vm, ctx, p.Gang ? "why.build.masons.gang" : "why.build.masons.text", "day", ctx.Fmt.Coins(Sim.Core.Market.MarketRules.Indexed(sim, d.MasonDay)));
                    return vm;
                case BuildingSheet.Ready:
                    vm = New(ctx, "why.build.ready.title", s.Get("build.about", "time", ctx.Fmt.Duration(ConstructionRules.EstimateReady(sim, p))));
                    Row(vm, ctx, "why.build.ready.hours", ctx.Fmt.Integer(p.Hours));
                    Text(vm, ctx, "why.build.ready.text");
                    return vm;
                default:
                    vm = New(ctx, "why.build.status.title", p.Working ? s.Get("build.building") : s.Get("build.waitingFor", "good", BuildingSheet.Good(ctx, ConstructionRules.Missing(sim, p))));
                    Row(vm, ctx, "why.build.status.paid", Money(ctx, p.Paid));
                    Text(vm, ctx, "why.build.status.text");
                    return vm;
            }
        }

        private static void ConstructionDefs(Simulation sim, out Sim.Core.Definitions.ConstructionDef d) { d = ConstructionRules.Def(sim); }
    }
}
