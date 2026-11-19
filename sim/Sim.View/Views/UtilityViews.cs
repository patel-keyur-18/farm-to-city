using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// The five utilities' plot sheets, the commission card and the Town tab's Infrastructure section (spec 025 §3.1, US-144), as previewed to the
    /// owner (Preview 4): before it is built, the cost, upkeep, reach, capacity and what it does, with the town's cash and the one-tap top-up; once built,
    /// its service, who it reaches and what it costs. Every row has a *Why?* (the <c>utility</c> source, id = utility × 100 + row).
    /// </summary>
    public static class UtilitySheet
    {
        public static readonly string[] Ids = { "tower", "generator", "grid", "drains", "collection" };

        /// <summary>A row's *Why?* id within a utility (<see cref="LineVm.WhyId"/> = utility × 100 + this).</summary>
        public const int Status = 0, Cost = 1, Upkeep = 2, Extra = 3, Reach = 4, Capacity = 5, Effect = 6, Service = 7, Cash = 8, Smoke = 9, Who = 10, Homes = 11, Saves = 12, Gift = 13, Both = 14;

        private const string Why = "utility";

        private static string K(int u, string part) { return "utility." + Ids[u] + "." + part; }

        private static LineVm Row(ViewContext ctx, int u, string key, string value, int row)
        {
            return new LineVm(ctx.Strings.Get(key), value, Why, u * 100 + row);
        }

        internal static string Name(ViewContext ctx, int u) { return ctx.Strings.Get(K(u, "title")); }

        internal static string ReachText(ViewContext ctx, int u)
        {
            UtilityDef d = UtilityRules.Def(ctx.Sim, u);
            return d.Reach >= UtilityDef.Whole ? ctx.Strings.Get("utility.reachWhole") : ctx.Strings.Get("utility.reachValue", "m", ctx.Fmt.Integer(d.Reach));
        }

        internal static string CapacityText(ViewContext ctx, int u)
        {
            UtilityDef d = UtilityRules.Def(ctx.Sim, u);
            return d.Capacity >= UtilityDef.Whole ? ctx.Strings.Get("utility.capacityNone")
                : ctx.Strings.Get("utility.capacityValue", "cap", ctx.Fmt.Integer(d.Capacity), "n", ctx.Fmt.Integer(UtilityRules.Served(ctx.Sim)));
        }

        internal static string HomesText(ViewContext ctx, int homes) { return homes == 0 ? ctx.Strings.Get("confirm.kilnHomes.none") : ctx.Strings.Plural("confirm.kilnHomes", homes); }

        /// <summary>The acres a generator's smoke reaches, yours among them, and the homes there: for the sheet and the card.</summary>
        internal static void SmokeCounts(ViewContext ctx, out int acres, out int yours, out int homes)
        {
            Simulation sim = ctx.Sim;
            List<int> fp = SmokeRules.Footprint(sim, UtilityRules.Def(sim, UtilityDef.Generator).Tile, SmokeRules.Def(sim).Source("generator"));
            acres = fp.Count;
            SmokeRules.Touches(sim, fp, out yours, out homes);
        }

        // ---- the plot's sheet ----

        internal static void Fill(ViewContext ctx, SheetVm vm, int u)
        {
            Simulation sim = ctx.Sim;
            if (UtilityRules.Defs(sim) == null) return;
            UtilityDef d = UtilityRules.Def(sim, u);
            if (UtilityRules.Built(sim, u)) { Built(ctx, vm, u); return; }
            if (ConstructionRules.On(sim) && ConstructionRules.OfWork(sim, d.Work) is BuildProject site) // being built (spec 025 §3.2)
            {
                vm.Icon = "place_" + Ids[u] + "_plot";
                vm.Title = Name(ctx, u);
                vm.Subtitle = ctx.Strings.Get("build.civicSub");
                BuildingSheet.Site(ctx, vm, site);
                return;
            }
            Plot(ctx, vm, u);
        }

        private static void Plot(ViewContext ctx, SheetVm vm, int u)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            UtilityDef d = UtilityRules.Def(sim, u);
            vm.Icon = "place_" + Ids[u] + "_plot";
            vm.Title = Name(ctx, u);
            vm.Subtitle = s.Get(K(u, "plotSub"));
            bool open = TreasuryRules.IsOpen(sim) && UtilityRules.On(sim);
            long cost = UtilityRules.Cost(sim, u);
            if (!open)
            {
                vm.Lines.Add(Row(ctx, u, K(u, "status"), s.Get("civic.notYet"), Status));
                vm.Lines.Add(new LineVm(s.Get("utility.lockedNote"), null));
                vm.Actions.Add(ActionVm.Checked(ctx, "commission", s.Get(K(u, "commission")), s.Get("civic.fromTown", "amount", ctx.Fmt.Coins(cost)), ActionKind.Primary, at => new CommissionWorks(at, d.Work)));
                return;
            }
            long cash = sim.State.Treasury.Cash;
            bool poor = cash < cost;
            vm.Lines.Add(LineVm.Header(s.Get(K(u, "hdr"))));
            vm.Lines.Add(Row(ctx, u, "civic.cost", ctx.Fmt.Coins(cost), Cost));
            vm.Lines.Add(Row(ctx, u, "utility.upkeep", ctx.Fmt.Coins(UtilityRules.Upkeep(sim, u)), Upkeep));
            if (d.ExtraLine >= 0) vm.Lines.Add(Row(ctx, u, K(u, "extra"), ctx.Fmt.Coins(UtilityRules.Extra(sim, u)), Extra));
            vm.Lines.Add(Row(ctx, u, "civic.reach", ReachText(ctx, u), Reach));
            vm.Lines.Add(Row(ctx, u, "civic.capacity", CapacityText(ctx, u), Capacity));
            vm.Lines.Add(Row(ctx, u, K(u, "who"), s.Get(K(u, "whoValue")), Who));
            vm.Lines.Add(LineVm.Header(s.Get("civic.hdrDoes")));
            vm.Lines.Add(Row(ctx, u, K(u, "effect"), s.Get(K(u, "effectWill")), Effect));
            vm.Lines.Add(new LineVm(s.Get(K(u, "effectNote")), null));
            if (u == UtilityDef.Generator)
            {
                SmokeCounts(ctx, out int acres, out int yours, out int homes);
                vm.Lines.Add(Row(ctx, u, "utility.smoke", s.Get("utility.smokeWill", "acres", s.Plural("land.acres", acres), "yours", ctx.Fmt.Integer(yours), "homes", HomesText(ctx, homes)), Smoke));
            }
            LineVm town = Row(ctx, u, "civic.cash", ctx.Fmt.Coins(cash), Cash);
            if (poor) town.NoteTone = Tone.Warn;
            vm.Lines.Add(town);
            if (poor) vm.Lines.Add(new LineVm(s.Get("utility.poorNote", "short", ctx.Fmt.Coins(cost - cash)), null) { NoteTone = Tone.Warn });
            else vm.Lines.Add(new LineVm(s.Get("civic.townNote"), null));
            if (poor)
            {
                long gift = UtilityRules.TopUp(sim, cost);
                vm.Actions.Add(ActionVm.Checked(ctx, "commission", s.Get(K(u, "commission")), s.Get("utility.giveAndBuild", "gift", ctx.Fmt.Coins(gift)), ActionKind.Primary, at => new CommissionWorks(at, d.Work, gift)));
                vm.Actions.Add(ActionVm.Open("give", s.Get("clinic.give"), new SheetRequest(TreasurySheet.GiveKind, 0), s.Get("treasury.giveDetail")));
            }
            else vm.Actions.Add(ActionVm.Checked(ctx, "commission", s.Get(K(u, "commission")), s.Get("civic.fromTown", "amount", ctx.Fmt.Coins(cost)), ActionKind.Primary, at => new CommissionWorks(at, d.Work)));
        }

        // ---- a standing utility ----

        private static void Built(ViewContext ctx, SheetVm vm, int u)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            UtilityDef d = UtilityRules.Def(sim, u);
            bool off = UtilityRules.SwitchedOff(sim, u), working = UtilityRules.Working(sim, u);
            Fixed condition = UtilityRules.Condition(sim);
            vm.Icon = "utility_" + Ids[u];
            vm.Title = Name(ctx, u);
            vm.Subtitle = off ? s.Get("utility.lineOff") : working ? s.Get(K(u, "lineOn")) : s.Get("utility.lineUnpaid");
            vm.Lines.Add(LineVm.Header(s.Get(K(u, "hdr"))));
            if (off)
            {
                vm.Lines.Add(new LineVm(s.Get("utility.offNote"), null) { Banner = true, NoteTone = Tone.Plain });
                return;
            }
            LineVm service = Row(ctx, u, "utility.service", ctx.Fmt.Percent(condition), Service);
            if (!working) { service.Note = s.Get("utility.serviceStopped", "pct", ctx.Fmt.Percent(UtilityRules.Defs(sim).MinCondition)); service.NoteTone = Tone.Warn; }
            else if (condition < Fixed.One) service.Note = s.Get("civic.serviceNote");
            vm.Lines.Add(service);
            string reach = ReachText(ctx, u);
            if (d.Reach < UtilityDef.Whole)
            {
                int n = UtilityRules.HomesCovered(sim, u, out int homes);
                reach = s.Get("utility.reachIn", "m", ctx.Fmt.Integer(d.Reach), "n", ctx.Fmt.Integer(n), "of", ctx.Fmt.Integer(homes));
            }
            vm.Lines.Add(Row(ctx, u, "civic.reach", reach, Reach));
            vm.Lines.Add(Row(ctx, u, "civic.capacity", CapacityText(ctx, u), Capacity));
            vm.Lines.Add(Row(ctx, u, "utility.pays", ctx.Fmt.Coins(UtilityRules.Runs(sim, u)), Upkeep));
            if (d.ExtraLine >= 0) vm.Lines.Add(Row(ctx, u, K(u, "extra"), ctx.Fmt.Coins(UtilityRules.Extra(sim, u)), Extra));
            vm.Lines.Add(LineVm.Header(s.Get("civic.hdrDoes")));
            switch (u)
            {
                case UtilityDef.Tower:
                    vm.Lines.Add(Row(ctx, u, "utility.tower.only", ctx.Fmt.Integer(UtilityRules.TowerOnly(sim)), Homes));
                    vm.Lines.Add(Row(ctx, u, "utility.tower.keeps", s.Get("utility.points", "n", ctx.Fmt.Integer((UtilityRules.Defs(sim).WaterHealth * Fixed.FromInt(100)).RoundToLong())), Effect));
                    break;
                case UtilityDef.Generator:
                case UtilityDef.Grid:
                    vm.Lines.Add(Row(ctx, u, "utility.power.homes", s.Get("utility.power.homesValue"), Effect));
                    vm.Lines.Add(Row(ctx, u, "utility.power.shops", s.Get("utility.power.shopsValue", "n", ctx.Fmt.Integer(UtilityRules.Defs(sim).EveningHours)), Both));
                    if (u == UtilityDef.Generator)
                    {
                        SmokeCounts(ctx, out int acres, out int yours, out int homes);
                        SmokeDef sd = SmokeRules.Def(sim);
                        SmokeSource src = sd.Source("generator");
                        vm.Lines.Add(Row(ctx, u, "utility.smoke", s.Get("utility.smokeIs", "acres", s.Plural("land.acres", acres), "lv", ctx.Fmt.Integer((src.LandValue * Fixed.FromInt(100)).RoundToLong()),
                            "health", ctx.Fmt.Integer((src.Health * Fixed.FromInt(100)).RoundToLong()), "homes", HomesText(ctx, homes)), Smoke));
                    }
                    break;
                case UtilityDef.Drains:
                    vm.Lines.Add(Row(ctx, u, K(u, "effect"), s.Get(K(u, "effectOn")), Effect));
                    break;
                default:
                    vm.Lines.Add(Row(ctx, u, K(u, "effect"), s.Get(K(u, "effectOn")), Effect));
                    break;
            }
            vm.Lines.Add(new LineVm(s.Get(K(u, "effectNote")), null));
            if (u == UtilityDef.Generator && !UtilityRules.Built(sim, UtilityDef.Grid))
            {
                long saves = UtilityRules.GridSaves(sim);
                vm.Lines.Add(new LineVm(s.Get("utility.gridNote", "extra", ctx.Fmt.Coins(UtilityRules.Cost(sim, UtilityDef.Grid) - UtilityRules.Cost(sim, UtilityDef.Generator)), "saves", ctx.Fmt.Coins(saves),
                    "n", ctx.Fmt.Integer(UtilityRules.GridPaybackSeasons(sim))), null));
                vm.Actions.Add(ActionVm.Open("grid", s.Get("utility.grid.commission"), new SheetRequest("Tile", UtilityRules.Def(sim, UtilityDef.Grid).Tile), s.Get("utility.gridDetail", "amount", ctx.Fmt.Coins(UtilityRules.Cost(sim, UtilityDef.Grid)))));
            }
        }

        // ---- the commission card ----

        internal static void Card(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after, CommissionWorks works, int u)
        {
            var s = ctx.Strings;
            vm.Title = s.Get(K(u, "cardTitle"));
            vm.Subtitle = s.Get(K(u, "cardSub"));
            vm.Rows.Add(new ConfirmRowVm(s.Get("civic.cardNow"), ctx.Fmt.Coins(sim.State.Treasury.Cash)));
            if (works.Gift > 0) vm.Rows.Add(new ConfirmRowVm(s.Get("utility.cardGift"), "+ " + ctx.Fmt.Coins(works.Gift)));
            if (UtilityRules.TakesMaterials(sim, u))
            {
                BuildQuote q = ConstructionRules.Quote(sim, ConstructionRules.Def(sim).KindOfWork(works.WorkId).Id, BuildProject.Town, 1);
                vm.Rows.Add(new ConfirmRowVm(s.Get("build.cardMaterials"), "− " + ctx.Fmt.Coins(q.Materials)));
                vm.Rows.Add(new ConfirmRowVm(q.Gang ? s.Get("build.cardGang", "days", ctx.Fmt.Integer(q.MasonDays)) : s.Get("build.cardMasons", "days", ctx.Fmt.Integer(q.MasonDays), "n", ctx.Fmt.Integer(q.Masons.Count)), "− " + ctx.Fmt.Coins(q.MasonPay)));
            }
            else vm.Rows.Add(new ConfirmRowVm(s.Get(K(u, "cardRow")), "− " + ctx.Fmt.Coins(UtilityRules.Cost(sim, u))));
            vm.Rows.Add(new ConfirmRowVm(s.Get("civic.cardAfter"), ctx.Fmt.Coins(after.State.Treasury.Cash), total: true));
        }

        /// <summary>The card's note: what the utility adds a season to Utilities, whether the town could pay it, and what it touches or saves.</summary>
        internal static void Guard(ViewContext ctx, ConfirmCardVm vm, Simulation after, string workId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int u = UtilityRules.OfWork(sim, workId);
            if (u < 0) return;
            UtilityRules.Guard(sim, workId, out long added, out long shortBy);
            string head;
            if (u == UtilityDef.Grid && UtilityRules.Active(sim, UtilityDef.Generator))
                head = s.Get("utility.guardGrid", "saves", ctx.Fmt.Coins(UtilityRules.GridSaves(sim)), "extra", ctx.Fmt.Coins(UtilityRules.Cost(sim, UtilityDef.Grid) - UtilityRules.Cost(sim, UtilityDef.Generator)),
                    "n", ctx.Fmt.Integer(UtilityRules.GridPaybackSeasons(sim)));
            else head = s.Get("utility.guardAdds", "amount", ctx.Fmt.Coins(added));
            string tail = shortBy > 0 ? s.Get("utility.guardShort", "amount", ctx.Fmt.Coins(shortBy)) : s.Get("utility.guardFine");
            string extra = "";
            if (u == UtilityDef.Generator)
            {
                SmokeCounts(ctx, out int acres, out int yours, out int homes);
                extra = " " + s.Get("utility.guardSmoke", "acres", s.Plural("land.acres", acres), "homes", HomesText(ctx, homes));
            }
            string timing = UtilityRules.TakesMaterials(sim, u) ? BuildingSheet.Timing(ctx, after, false) + " " : "";
            vm.Warning = timing + head + " " + tail + extra;
            vm.Reassure = shortBy == 0;
        }

        // ---- the Town tab's Infrastructure section ----

        private static readonly int[][] Ladders = { new[] { UtilityDef.Tower }, new[] { UtilityDef.Generator, UtilityDef.Grid }, new[] { UtilityDef.Drains }, new[] { UtilityDef.Collection } };

        /// <summary>Row ids of the Town section's *Why?* (<c>utilityTown</c> source): the four ladders, then the two counts.</summary>
        public const int TownHomes = 4, TownCost = 5;

        internal static void TownRows(ViewContext ctx, SheetVm vm)
        {
            Simulation sim = ctx.Sim;
            if (!UtilityRules.On(sim) || !TreasuryRules.IsOpen(sim)) return;
            var s = ctx.Strings;
            vm.Lines.Add(LineVm.Header(s.Get("utility.header")));
            for (int l = 0; l < Ladders.Length; l++)
            {
                int[] steps = Ladders[l];
                int shown = -1; // the highest step standing
                foreach (int u in steps) if (UtilityRules.Built(sim, u)) shown = u;
                int target = shown;
                foreach (int u in steps) if (!UtilityRules.Built(sim, u)) { target = u; break; }
                string value = shown < 0 ? s.Get("utility.ladder." + l + ".none") : s.Get("utility.ladder." + l + ".is", "name", Name(ctx, shown), "pct", ctx.Fmt.Percent(UtilityRules.Condition(sim)));
                var row = new LineVm(s.Get("utility.ladder." + l), value, "utilityTown", l);
                bool all = shown == steps[steps.Length - 1];
                var open = ActionVm.Open("utility" + target, all ? s.Get("action.open") : Name(ctx, target), new SheetRequest("Tile", UtilityRules.Def(sim, all ? shown : target).Tile));
                row.Action = open;
                vm.Lines.Add(row);
            }
            int without = UtilityRules.HomesWithoutWater(sim, out int homes);
            vm.Lines.Add(new LineVm(s.Get("utility.noWater"), s.Get("utility.noWaterValue", "n", ctx.Fmt.Integer(without), "of", ctx.Fmt.Integer(homes)), "utilityTown", TownHomes));
            vm.Lines.Add(new LineVm(s.Get("utility.costs"), ctx.Fmt.Coins(UtilityRules.RunningCost(sim)), "utilityTown", TownCost));
        }
    }

    public static partial class WhyView
    {
        private static WhyVm UtilityTownWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            UtilitiesDef d = UtilityRules.Defs(sim);
            if (id < 4)
            {
                var vm0 = New(ctx, "why.utility.ladder." + id + ".title", s.Get("utility.ladder." + id));
                Row(vm0, ctx, "why.utility.condition", Pct(ctx, UtilityRules.Condition(sim)));
                Row(vm0, ctx, "why.utility.floor", Pct(ctx, d.MinCondition));
                Text(vm0, ctx, "why.utility.ladder." + id + ".text");
                return vm0;
            }
            WhyVm vm;
            if (id == UtilitySheet.TownHomes)
            {
                int without = UtilityRules.HomesWithoutWater(sim, out int homes);
                vm = New(ctx, "why.utility.noWater.title", s.Get("utility.noWaterValue", "n", ctx.Fmt.Integer(without), "of", ctx.Fmt.Integer(homes)));
                Text(vm, ctx, "why.utility.noWater.text", "m", ctx.Fmt.Integer(sim.Config.Game.Township.LandValue.WaterReach), "tower", ctx.Fmt.Integer(UtilityRules.Def(sim, UtilityDef.Tower).Reach));
                return vm;
            }
            vm = New(ctx, "why.utility.costs.title", Money(ctx, UtilityRules.RunningCost(sim)));
            for (int u = 0; u < UtilityDef.Count; u++) if (UtilityRules.Active(sim, u)) Row(vm, ctx, "utility." + UtilitySheet.Ids[u] + ".title", Money(ctx, UtilityRules.Runs(sim, u)));
            Text(vm, ctx, "why.utility.costs.text");
            return vm;
        }

        private static WhyVm UtilityWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            int u = (int)(id / 100), row = (int)(id % 100);
            UtilityDef d = UtilityRules.Def(sim, u);
            UtilitiesDef defs = UtilityRules.Defs(sim);
            string n = UtilitySheet.Ids[u];
            WhyVm vm;
            switch (row)
            {
                case UtilitySheet.Cost:
                    vm = New(ctx, "why.utility.cost.title", Money(ctx, UtilityRules.Cost(sim, u)), "name", UtilitySheet.Name(ctx, u));
                    if (UtilityRules.TakesMaterials(sim, u))
                    {
                        BuildQuote q = ConstructionRules.Quote(sim, ConstructionRules.Def(sim).KindOfWork(d.Work).Id, BuildProject.Town, 1);
                        Row(vm, ctx, "why.civic.cost.materials", Money(ctx, q.Materials));
                        Row(vm, ctx, q.Gang ? "why.civic.cost.gang" : "why.civic.cost.masons", Money(ctx, q.MasonPay));
                        Text(vm, ctx, "why.civic.cost.textBuilt");
                        return vm;
                    }
                    Row(vm, ctx, "why.civic.cost.base", Money(ctx, BaseOf(sim, d.Work)));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.utility.cost.fitted");
                    return vm;
                case UtilitySheet.Upkeep:
                    vm = New(ctx, "why.utility.upkeep.title", Money(ctx, UtilityRules.Upkeep(sim, u)));
                    Row(vm, ctx, "why.civic.upkeep.base", Money(ctx, TreasuryRules.Def(sim).Lines[d.UpkeepLine].Amount));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.utility.upkeep.text");
                    return vm;
                case UtilitySheet.Extra:
                    vm = New(ctx, "why.utility." + n + ".extra.title", Money(ctx, UtilityRules.Extra(sim, u)));
                    Row(vm, ctx, "why.civic.upkeep.base", Money(ctx, TreasuryRules.Def(sim).Lines[d.ExtraLine].Amount));
                    Row(vm, ctx, "why.price.index", Effect(ctx, sim.State.Market.PriceIndex));
                    Text(vm, ctx, "why.utility." + n + ".extra.text");
                    return vm;
                case UtilitySheet.Reach:
                    vm = New(ctx, "why.utility.reach.title", UtilitySheet.ReachText(ctx, u));
                    Text(vm, ctx, d.Reach >= UtilityDef.Whole ? "why.utility.reach.whole" : "why.utility.reach.text", "m", ctx.Fmt.Integer(d.Reach));
                    return vm;
                case UtilitySheet.Capacity:
                    vm = New(ctx, "why.utility.capacity.title", UtilitySheet.CapacityText(ctx, u));
                    Row(vm, ctx, "why.civic.capacity.scale", Pct(ctx, UtilityRules.Scale(sim, u)));
                    Text(vm, ctx, d.Capacity >= UtilityDef.Whole ? "why.utility.capacity.none" : "why.utility.capacity.text");
                    return vm;
                case UtilitySheet.Service:
                    vm = New(ctx, "why.utility.service.title", Pct(ctx, UtilityRules.Condition(sim)));
                    Row(vm, ctx, "why.civic.service.now", Pct(ctx, UtilityRules.Condition(sim)));
                    Row(vm, ctx, "why.utility.floor", Pct(ctx, defs.MinCondition));
                    Text(vm, ctx, "why.utility.service.text");
                    return vm;
                case UtilitySheet.Effect:
                    vm = New(ctx, "why.utility." + n + ".effect.title", null);
                    if (u == UtilityDef.Tower) Row(vm, ctx, "why.utility.tower.health", ctx.Fmt.Integer((defs.WaterHealth * Fixed.FromInt(100)).RoundToLong()));
                    if (u == UtilityDef.Drains) { Row(vm, ctx, "why.utility.drains.health", ctx.Fmt.Integer((defs.DrainsHealth * Fixed.FromInt(100)).RoundToLong())); Row(vm, ctx, "why.utility.drains.cap", Pct(ctx, defs.DrainsHealthCap)); Row(vm, ctx, "why.utility.drains.land", ctx.Fmt.Integer((defs.DrainsLandValue * Fixed.FromInt(100)).RoundToLong())); }
                    if (u == UtilityDef.Collection) Row(vm, ctx, "why.utility.collection.belonging", ctx.Fmt.Integer((defs.WasteBelonging * Fixed.FromInt(100)).RoundToLong()));
                    if (u == UtilityDef.Generator || u == UtilityDef.Grid) Row(vm, ctx, "why.utility.power.leisure", Pct(ctx, defs.PowerLeisure));
                    if (u == UtilityDef.Drains || u == UtilityDef.Collection) Row(vm, ctx, "why.utility.townSize", ctx.Fmt.Integer(defs.TownSize));
                    Text(vm, ctx, "why.utility." + n + ".effect.text");
                    return vm;
                case UtilitySheet.Homes:
                    vm = New(ctx, "why.utility.homes.title", ctx.Fmt.Integer(UtilityRules.TowerOnly(sim)));
                    Text(vm, ctx, "why.utility.homes.text");
                    return vm;
                case UtilitySheet.Both:
                    vm = New(ctx, "why.utility.evening.title", null);
                    Row(vm, ctx, "why.utility.evening.hours", ctx.Fmt.Integer(defs.EveningHours));
                    Text(vm, ctx, "why.utility.evening.text");
                    return vm;
                case UtilitySheet.Smoke:
                    SmokeSource src = SmokeRules.Def(sim).Source("generator");
                    vm = New(ctx, "why.utility.smoke.title", null);
                    Row(vm, ctx, "why.utility.smoke.reach", s.Get("utility.reachValue", "m", ctx.Fmt.Integer(src.Reach)));
                    Row(vm, ctx, "why.utility.smoke.lv", ctx.Fmt.Integer((src.LandValue * Fixed.FromInt(100)).RoundToLong()));
                    Row(vm, ctx, "why.utility.smoke.health", ctx.Fmt.Integer((src.Health * Fixed.FromInt(100)).RoundToLong()));
                    Row(vm, ctx, "why.utility.smoke.cap", ctx.Fmt.Integer((SmokeRules.Def(sim).LandValueCap * Fixed.FromInt(100)).RoundToLong()));
                    Text(vm, ctx, "why.utility.smoke.text");
                    return vm;
                case UtilitySheet.Who:
                    vm = New(ctx, "why.utility.who.title", s.Get("utility." + n + ".whoValue"));
                    Text(vm, ctx, u == UtilityDef.Collection ? "why.utility.who.collector" : "why.utility.who.none");
                    return vm;
                case UtilitySheet.Cash:
                    vm = New(ctx, "why.civic.cash.title", Money(ctx, sim.State.Treasury.Cash));
                    Text(vm, ctx, "why.civic.cash.text");
                    return vm;
                default:
                    vm = New(ctx, "why.civic.status.title", s.Get("civic.notYet"));
                    Text(vm, ctx, "why.civic.status.text");
                    return vm;
            }
        }

    }
}
