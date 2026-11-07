using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.View.Views
{
    /// <summary>
    /// Whole-acre orchards on the field sheet (spec 013 §3.3, §7, ADR 0090): what the trees are doing, their payback, and a ripe batch
    /// picked in parts or sold on the trees to a fruit contractor; and the sheet that plants one.
    /// </summary>
    public static class OrchardSheet
    {
        public const string Kind = "Orchard";

        /// <summary>"Plant an orchard" on an empty field you own: greyed with the reason until the Hamlet (ADR 0090 §2).</summary>
        public static ActionVm PlantAction(ViewContext ctx, int tileId)
        {
            RejectReason r = OrchardRules.Place(ctx.Sim, tileId);
            bool ok = r == RejectReason.None;
            string why = ok ? null : r == RejectReason.EraLocked // "Comes once you're a Hamlet", as a machine's
                ? ctx.Strings.Get("equipment.locked." + ctx.Sim.Config.Game.Onboarding.Eras[OrchardRules.Defs(ctx.Sim).UnlockEra].Id) : ctx.Reject(r);
            return new ActionVm("orchard", ctx.Strings.Get("orchard.plant"), null, ActionKind.Open, ok, why, null, new SheetRequest(Kind, tileId));
        }

        /// <summary>The orchard's rows: its title and state, and the batch, contractor and payback rows with their Why?.</summary>
        internal static void AddRows(ViewContext ctx, SheetVm vm, Orchard o, string crop)
        {
            Simulation sim = ctx.Sim;
            var str = ctx.Strings;
            TreeDef def = sim.Config.Game.Farm.Trees[o.Def];
            string tree = str.Get(def.NameKey);
            vm.Icon = def.Id;
            vm.Title = str.Get("orchard.title", "tree", tree);
            vm.Subtitle = str.Plural(o.State == TreeState.Growing ? "orchard.young" : o.RipeGrams > 0 ? "orchard.ripe" : "orchard.mature", o.Trees);
            if (crop != null) vm.Lines.Add(new LineVm(str.Get("orchard.between"), crop));
            if (o.State == TreeState.Growing)
                vm.Lines.Add(new LineVm(str.Get("orchard.bears"), ctx.Fmt.Duration(System.Math.Max(0, o.PlantedAt + def.GrowthSeconds - ctx.Now)), "orchardBears", o.TileId));
            else vm.Lines.Add(new LineVm(str.Get("tree.fruits"), FieldSheets.SeasonsText(ctx, def.FruitingSeasons)));
            if (o.RipeGrams > 0)
            {
                long left = o.RipeAt + sim.Config.Game.Farm.Farm.ReadyGraceSeconds - ctx.Now;
                vm.Lines.Add(new LineVm(str.Get("orchard.ripeFruit"), ctx.Fmt.Kg(o.RipeGrams), "orchardBatch", o.TileId));
                vm.Lines.Add(new LineVm(str.Get("orchard.waits"), left > 0 ? str.Get("field.spoilsIn", "time", ctx.Fmt.Duration(left)) : str.Get("field.spoiling"), "orchardSpoils", o.TileId));
                vm.Lines.Add(new LineVm(str.Get("orchard.today"), ctx.Fmt.Kg(OrchardRules.Pickable(sim, o, Overflow.SellRestToTrader)), "orchardToday", o.TileId));
                vm.Lines.Add(new LineVm(str.Get("orchard.contractor"), ctx.Fmt.Coins(OrchardRules.ContractorPrice(sim, o)), "orchardContractor", o.TileId));
            }
            else vm.Lines.Add(new LineVm(str.Get("orchard.sofar"), ctx.Rating(SoFar(sim, o)), "orchardBatch", o.TileId));
            if (o.State == TreeState.Growing) vm.Lines.Add(new LineVm(str.Get("slot.shade"), str.Get("orchard.shadeNote")));
            vm.Lines.Add(new LineVm(str.Get("tree.cost"), ctx.Fmt.Coins(o.Cost), "orchardPayback", o.TileId));
            vm.Lines.Add(new LineVm(str.Get("tree.returns"), ctx.Fmt.Coins(o.Returns), "orchardPayback", o.TileId));
            TreePayback pay = TreeRules.Payback(sim, o);
            vm.Lines.Add(new LineVm(str.Get("tree.payback"), pay.PaidBack ? str.Get("tree.paidBack", "season", FieldSheets.SeasonName(ctx, pay.Season.Value))
                : pay.Season.HasValue ? str.Get("tree.paysBack", "season", FieldSheets.SeasonName(ctx, pay.Season.Value)) : str.Get("tree.neverPaysBack"), "orchardPayback", o.TileId));
        }

        /// <summary>Pick what can go today, sell the rest on the trees, or clear the orchard (asked first: its cost is lost).</summary>
        internal static void AddActions(ViewContext ctx, SheetVm vm, Orchard o)
        {
            var str = ctx.Strings;
            int tile = o.TileId;
            if (o.RipeGrams > 0)
            {
                vm.Actions.Add(ActionVm.Checked(ctx, "pickOrchard", str.Get("orchard.pick"), ctx.Fmt.Kg(OrchardRules.Pickable(ctx.Sim, o, Overflow.SellRestToTrader)),
                    ActionKind.Primary, at => new PickOrchard(at, tile, Overflow.SellRestToTrader)));
                bool picked = o.Last != null && o.Last.Grams > o.RipeGrams; // some of this batch is gone: the contractor takes the rest
                vm.Actions.Add(ActionVm.Checked(ctx, "sellOnTrees", str.Get(picked ? "orchard.sellRestOnTrees" : "orchard.sellOnTrees"), ctx.Fmt.Coins(OrchardRules.ContractorPrice(ctx.Sim, o)),
                    ActionKind.Secondary, at => new SellOrchardOnTrees(at, tile)));
            }
            ActionVm clear = ActionVm.Checked(ctx, "clearOrchard", str.Get("orchard.clear"), null, ActionKind.Danger, at => new ClearOrchard(at, tile));
            clear.Confirm = new ConfirmVm(str.Get("orchard.clearQ", "amount", ctx.Fmt.Coins(o.Cost)), str.Get("orchard.clear"), str.Get("orchard.keep"),
                at => new Command[] { new ClearOrchard(at, tile) }, noSends: false) { YesIsDanger = true };
            vm.Actions.Add(clear);
        }

        /// <summary>The trees' Water × Care × Weather since the last batch (or the planting): what the next batch is heading for.</summary>
        internal static Fixed SoFar(Simulation sim, Orchard o)
        {
            OrchardFactors f = Factors(sim, o);
            return f.Water * f.Care * f.Weather;
        }

        internal struct OrchardFactors { public Fixed Water, Care, Weather; }

        internal static OrchardFactors Factors(Simulation sim, Orchard o)
        {
            long now = sim.State.Clock.SimTime, span = now - o.SumsFrom;
            Fixed care = sim.State.Farm.CareRatio;
            if (span <= 0) return new OrchardFactors { Water = o.WaterRate, Care = care, Weather = o.WeatherRate };
            long dt = System.Math.Max(0, now - sim.State.Farm.LastAccrual); // the sums run to the last accrual: add the rates since
            return new OrchardFactors
            {
                Water = (o.WaterSum + o.WaterRate.MulRatio(dt, 1)).MulRatio(1, span),
                Care = (o.CareSum + care.MulRatio(dt, 1)).MulRatio(1, span),
                Weather = (o.WeatherSum + o.WeatherRate.MulRatio(dt, 1)).MulRatio(1, span),
            };
        }

        /// <summary>The plant sheet: a row per tree planted by the acre, with how many trees, when it fruits and bears, and its price.</summary>
        public static SheetVm Build(ViewContext ctx, int tileId)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            var str = ctx.Strings;
            var vm = new SheetVm { Icon = "mango", Title = str.Get("orchard.plantTitle"),
                Subtitle = str.Get("orchard.plantLine", "share", ctx.Fmt.Percent(OrchardRules.Defs(sim).IntercropShare)) };
            for (int i = 0; i < defs.Farm.Trees.Count; i++)
            {
                int n = OrchardRules.TreesPerAcre(sim, i);
                if (n == 0) continue;
                TreeDef d = defs.Farm.Trees[i];
                var line = new LineVm(str.Get(d.NameKey), str.Get("orchard.plantInfo", "trees", ctx.Fmt.Integer(n), "seasons", FieldSheets.SeasonsText(ctx, d.FruitingSeasons),
                    "time", ctx.Fmt.Duration(d.GrowthSeconds)), "orchardPlant", i) { Icon = defs.Market.Goods[d.FruitGood].Id };
                line.Action = ActionVm.Checked(ctx, "plant:" + d.Id, str.Get("action.plant"), ctx.Fmt.Coins(OrchardRules.Price(sim, i)), ActionKind.Primary,
                    at => new PlantOrchard(at, tileId, d.Id));
                vm.Lines.Add(line);
            }
            return vm;
        }
    }
}
