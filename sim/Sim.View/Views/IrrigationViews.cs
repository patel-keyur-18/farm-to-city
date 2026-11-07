using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Time;

namespace Sim.View.Views
{
    /// <summary>
    /// The field card's water (spec 013 §7 as amended by ADR 0086 and ADR 0087): where the acre's water comes from, with its
    /// <i>Why?</i>, and picture buttons for what the acre can take next: the pump, a borewell, a canal channel, drip. A step shows
    /// only from its era.
    /// </summary>
    public static class IrrigationViews
    {
        /// <summary>Where the acre's water comes from, in words: "Rain-fed", "The well, by bucket", "Drip from the pump + Canal channel".</summary>
        public static string Water(ViewContext ctx, int tile)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            var parts = new List<string>();
            int source = IrrigationRules.SourceOf(sim, tile);
            if (source != 0)
            {
                string name = s.Get(source == 1 ? "water.pump" : "water.borewell");
                string part = IrrigationRules.HasDrip(sim, tile) ? s.Get("water.drip", "source", name.ToLowerInvariant()) : name;
                if (!IrrigationRules.Running(sim, source)) part = s.Get("water.off", "what", part);
                parts.Add(part);
            }
            if (IrrigationRules.Channelled(sim, tile))
            {
                string part = s.Get("water.channel");
                if (!IrrigationRules.Reached(sim)[tile]) part = s.Get("water.cutOff", "what", part);
                else if (sim.State.Irrigation.CanalUnpaid) part = s.Get("water.off", "what", part);
                else if (!IrrigationRules.CanalFlowing(sim)) part = s.Get("water.dry", "what", part);
                parts.Add(part);
            }
            if (parts.Count == 0) parts.Add(s.Get(LandSystem.IsWellTile(sim.Config.Game, tile) ? "water.buckets" : "water.rainFed"));
            return string.Join(" + ", parts);
        }

        /// <summary>The water row and the irrigation buttons on one of your fields.</summary>
        public static void AddRows(ViewContext ctx, SheetVm vm, int tile)
        {
            Simulation sim = ctx.Sim;
            if (!IrrigationRules.On(sim))
            {
                if (LandSystem.IsWellTile(sim.Config.Game, tile)) vm.Lines.Add(new LineVm(ctx.Strings.Get("field.well"), ctx.Strings.Get("field.wellNote")));
                return;
            }
            vm.Lines.Add(new LineVm(ctx.Strings.Get("field.waterFrom"), Water(ctx, tile), "irrigation", tile));
            List<ActionVm> buttons = Buttons(ctx, tile);
            for (int i = 0; i < buttons.Count; i += 2)
                vm.Lines.Add(new LineVm(null, null) { Buttons = buttons.GetRange(i, System.Math.Min(2, buttons.Count - i)) });
        }

        private static List<ActionVm> Buttons(ViewContext ctx, int tile)
        {
            Simulation sim = ctx.Sim;
            IrrigationDef d = IrrigationRules.Def(sim);
            var s = ctx.Strings;
            var list = new List<ActionVm>();
            int source = IrrigationRules.SourceOf(sim, tile);

            if (IrrigationRules.Unlocked(sim, d.Pump.UnlockEra))
            {
                if (IrrigationRules.Pump(sim) == null)
                    list.Add(Act(ctx, "installPump", s.Get("irrigation.installPump"), Price(ctx, d.Pump.Building), ActionKind.Primary, "pump", at => new InstallPump(at)));
                else if (source != 2)
                    list.Add(Toggle(ctx, 1, tile, source == 1, d.Pump.Acres));
            }
            if (IrrigationRules.Unlocked(sim, d.Borewell.UnlockEra))
            {
                if (IrrigationRules.Borewell(sim) == null)
                    list.Add(Act(ctx, "drillBorewell", s.Get("irrigation.drill"), Price(ctx, d.Borewell.Building), ActionKind.Secondary, "borewell", at => new DrillBorewell(at, tile)));
                else if (source != 1)
                    list.Add(Toggle(ctx, 2, tile, source == 2, d.Borewell.Acres));
            }
            if (IrrigationRules.Unlocked(sim, d.Canal.UnlockEra) && !IrrigationRules.Channelled(sim, tile))
                list.Add(Act(ctx, "buildChannel", s.Get("irrigation.channel"), Price(ctx, d.Canal.Building), ActionKind.Secondary, "channel", at => new BuildChannel(at, tile)));
            if (IrrigationRules.Unlocked(sim, d.Drip.UnlockEra) && !IrrigationRules.HasDrip(sim, tile))
            {
                long price = IrrigationRules.DripPrice(sim), full = MarketRules.Indexed(sim, sim.Config.Game.Buildings[d.Drip.Building].Value);
                string detail = price < full ? s.Get("irrigation.dripSubsidised", "price", ctx.Fmt.Coins(price)) : ctx.Fmt.Coins(price);
                list.Add(Act(ctx, "layDrip", s.Get("irrigation.drip"), detail, ActionKind.Secondary, "drip", at => new LayDrip(at, tile)));
            }
            return list;
        }

        /// <summary>Puts the acre on a pumped source's list, or takes it off (1 the pump, 2 the borewell).</summary>
        private static ActionVm Toggle(ViewContext ctx, int source, int tile, bool on, int acres)
        {
            var tiles = new List<int>(IrrigationRules.Acres(ctx.Sim, source));
            if (on) tiles.Remove(tile);
            else tiles.Add(tile);
            int[] chosen = tiles.ToArray();
            string what = source == 1 ? "pump" : "borewell";
            string label = ctx.Strings.Get(on ? "irrigation." + what + "Off" : "irrigation." + what + "On");
            string detail = on ? null : ctx.Strings.Get("irrigation.acresUsed", "n", ctx.Fmt.Integer(IrrigationRules.Acres(ctx.Sim, source).Count), "of", ctx.Fmt.Integer(acres));
            return Act(ctx, what + (on ? "Off" : "On"), label, detail, on ? ActionKind.Secondary : ActionKind.Primary, what,
                source == 1 ? (System.Func<long, Command>)(at => new SetPumpAcres(at, chosen)) : at => new SetBorewellAcres(at, chosen));
        }

        private static string Price(ViewContext ctx, int building)
        {
            return ctx.Fmt.Coins(MarketRules.Indexed(ctx.Sim, ctx.Sim.Config.Game.Buildings[building].Value));
        }

        /// <summary>A checked button with irrigation's own words for a refusal where the shared ones would mislead.</summary>
        private static ActionVm Act(ViewContext ctx, string id, string label, string detail, ActionKind kind, string icon, System.Func<long, Command> make)
        {
            CommandResult r = ctx.Sim.Preview(make(ctx.Now));
            string reason = null;
            if (!r.Accepted)
                reason = r.Reason == RejectReason.NotAdjacent ? ctx.Strings.Get("irrigation.notAdjacent")
                    : r.Reason == RejectReason.AlreadyBuilt ? ctx.Strings.Get("irrigation.alreadyBuilt")
                    : ctx.Reject(r.Reason);
            return new ActionVm(id, label, detail, kind, r.Accepted, reason, make) { Icon = icon };
        }

        /// <summary>The season the canal is next dry in, for its <i>Why?</i>.</summary>
        internal static string DrySeasons(ViewContext ctx)
        {
            var names = new List<string>();
            IReadOnlyList<bool> dry = IrrigationRules.Def(ctx.Sim).Canal.DrySeasons;
            for (int i = 0; i < dry.Count; i++) if (dry[i]) names.Add(ctx.Strings.Get("season." + ((Season)i).ToString().ToLowerInvariant()));
            return string.Join(", ", names);
        }
    }
}
