using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>Land value as the player reads it (US-143, spec 024 §3.1, §3.4): the multiplier of an acre and of a parcel, its Why?, and the sale choice.</summary>
    public static class LandValueViews
    {
        /// <summary>"×1.14", or null before the Township (land value exists from it on).</summary>
        public static string Mult(ViewContext ctx, Fixed lv) { return ctx.Strings.Get("landValue.mult", "n", ctx.Fmt.Decimal2(lv)); }

        /// <summary>The average land value of a parcel's acres.</summary>
        public static Fixed Average(Simulation sim, int parcelId)
        {
            var tiles = sim.Config.Game.Map.Parcels[parcelId].Tiles;
            Fixed sum = Fixed.Zero;
            foreach (int t in tiles) sum += LandValueRules.Of(sim, t);
            return sum / Fixed.FromInt(tiles.Count);
        }

        /// <summary>The land value of the acre the player tapped, as a sheet row with its Why?; none before the Township.</summary>
        public static LineVm AcreRow(ViewContext ctx, int tileId)
        {
            if (!LandValueRules.On(ctx.Sim)) return null;
            return new LineVm(ctx.Strings.Get("land.value"), Mult(ctx, LandValueRules.Of(ctx.Sim, tileId)), "landValue", tileId);
        }

        /// <summary>The Why? of an acre's land value: each part that counts in words with what it adds, what is not near enough yet, the bounds.</summary>
        internal static WhyVm Why(ViewContext ctx, int tileId)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            LandValueDef d = LandValueRules.Def(sim);
            var vm = new WhyVm { Title = s.Get("why.landValue.title"), Value = WhyView.Effect(ctx, LandValueRules.Of(sim, tileId)) }; // words, not a factor (ADR 0043)
            vm.Rows.Add(new WhyRowVm(s.Get("why.landValue.start"), s.Get("why.landValue.plain")));
            var missing = new List<string>();
            string[] keys = { "district", "lane", "school", "clinic", "square", "water", "jobs", "size" };
            var parts = LandValueRules.Parts(sim, tileId);
            foreach (LandPart p in parts)
                vm.Rows.Add(new WhyRowVm(s.Get("why.landValue." + p.Key, "m", ctx.Fmt.Integer(Metres(d, p.Key, sim)), "n", ctx.Fmt.Integer(p.Count)), "+" + ctx.Fmt.Integer((p.Value * Fixed.FromInt(100)).RoundToLong()) + "%"));
            foreach (string k in new[] { "school", "clinic", "square", "water", "jobs" })
                if (!parts.Exists(p => p.Key == k)) missing.Add(s.Get("why.landValue.not." + k));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landValue.bounds"), s.Get("why.landValue.boundsValue", "low", WhyView.Effect(ctx, d.Floor), "high", WhyView.Effect(ctx, d.Ceiling))));
            if (missing.Count > 0) vm.Rows.Add(new WhyRowVm(s.Get("why.landValue.missing", "list", string.Join(", ", missing)), null));
            vm.Rows.Add(new WhyRowVm(s.Get("why.landValue.read"), null));
            vm.Footnote = s.Get("why.landValue.lesson");
            return vm;
        }

        private static int Metres(LandValueDef d, string key, Simulation sim)
        {
            switch (key)
            {
                case "school": return sim.Config.Game.School.Reach;
                case "clinic": return sim.Config.Game.Township.Clinic.Reach;
                case "square": return d.SquareReach;
                case "water": return d.WaterReach;
                case "jobs": return d.JobReach;
                default: return 0;
            }
        }

        /// <summary>"2 days": a wait in whole days.</summary>
        public static string Days(ViewContext ctx, long seconds) { return ctx.Strings.Plural("road.daysValue", (int)(seconds / 86400)); }

        /// <summary>The note under an owned parcel on Your land: its land value, or what a listing is waiting for.</summary>
        public static string ListedNote(ViewContext ctx, Parcel p)
        {
            return ctx.Strings.Get("lands.listed", "price", ctx.Fmt.Coins(p.SlowSalePrice), "left", ctx.Fmt.Duration(System.Math.Max(0, p.SlowSaleEnds - ctx.Now)));
        }
    }

    /// <summary>"Sell this land" in the Township (spec 024 §3.4): the two ways side by side, with the difference in coins.</summary>
    public static class SellLandSheet
    {
        public static SheetVm Build(ViewContext ctx, int tileId)
        {
            Simulation sim = ctx.Sim;
            GameDefs defs = sim.Config.Game;
            var s = ctx.Strings;
            Parcel parcel = sim.State.Land.Parcels[sim.State.Land.Tiles[tileId].ParcelId];
            ParcelDef d = defs.Map.Parcels[parcel.Id];
            long value = LandRules.ParcelPrice(sim, parcel);
            long quick = Money.RoundToCoin(Money.MulRate(value, defs.Land.QuickSaleFactor));
            string soil = s.Get(defs.Soils[sim.State.Land.Tiles[d.Tiles[0]].Soil].NameKey);
            var vm = new SheetVm { Icon = "land", Title = s.Get("sellLand.title"), Subtitle = s.Get("lands.parcel", "soil", soil, "acres", s.Plural("land.acres", d.Tiles.Count)) };
            vm.Lines.Add(new LineVm(s.Get("sellLand.value"), ctx.Fmt.Coins(value), "landPrice", tileId));
            vm.Lines.Add(LineVm.Header(s.Get("sellLand.now")));
            vm.Lines.Add(new LineVm(s.Get("sellLand.nowReceive", "percent", ctx.Fmt.Percent(defs.Land.QuickSaleFactor)), ctx.Fmt.Coins(quick), "resale", tileId));
            vm.Lines.Add(LineVm.Header(s.Get("sellLand.slow")));
            vm.Lines.Add(new LineVm(s.Get("sellLand.slowReceive", "wait", LandValueViews.Days(ctx, LandValueRules.Def(sim).SlowSaleSeconds)), ctx.Fmt.Coins(value), "landPrice", tileId));
            vm.Lines.Add(new LineVm(s.Get("sellLand.more"), "+" + ctx.Fmt.Coins(value - quick), "landPrice", tileId));
            vm.Lines.Add(new LineVm(s.Get("sellLand.note"), null));
            string id = d.Id;
            vm.Actions.Add(ActionVm.Checked(ctx, "sellSlow:" + id, s.Get("action.sellSlow"), s.Get("sellLand.slowDetail", "price", ctx.Fmt.Coins(value), "wait", LandValueViews.Days(ctx, LandValueRules.Def(sim).SlowSaleSeconds)),
                ActionKind.Primary, at => new ListParcel(at, id)));
            vm.Actions.Add(ActionVm.Checked(ctx, "sellNow:" + id, s.Get("action.sellNow"), s.Get("sellLand.nowDetail", "price", ctx.Fmt.Coins(quick)), ActionKind.Danger, at => new SellParcel(at, id)));
            return vm;
        }
    }
}
