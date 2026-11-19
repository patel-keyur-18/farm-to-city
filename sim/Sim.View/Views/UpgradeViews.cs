using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>
    /// Pucca upgrades (spec 024 §3.7, US-152), as previewed to the owner (Preview 5): a house's row on its acre's sheet (the offer with its button, or why not,
    /// or the work under way), the confirm card, the builders' count and the *Why?* of every number. The reasons are said with their numbers.
    /// </summary>
    public static class UpgradeSheet
    {
        public const string Why = "upgrade";

        internal static string State(ViewContext ctx, UpgradeCheck c, BuildProject p)
        {
            var s = ctx.Strings;
            if (p != null) return s.Get("upgrade.state.underway");
            switch (c.Why)
            {
                case UpgradeWhy.Pucca: return s.Get("upgrade.state.pucca");
                case UpgradeWhy.LandValue: return s.Get("upgrade.state.landValue", "have", ctx.Fmt.Percent(c.LandValue), "need", ctx.Fmt.Percent(c.Needs));
                case UpgradeWhy.Water: return s.Get("upgrade.state.water");
                case UpgradeWhy.Rent: return s.Get("upgrade.state.rent");
                case UpgradeWhy.Locked: return s.Get("upgrade.state.locked");
                default: return s.Get("upgrade.state.offered");
            }
        }

        /// <summary>The upgrade rows under one house on its acre's sheet: its state with a *Why?*, the work's own rows when under way, the button when offered.</summary>
        internal static void Rows(ViewContext ctx, SheetVm vm, Building b)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (!UpgradeRules.On(sim)) return;
            UpgradeCheck c = UpgradeRules.Check(sim, b);
            BuildProject p = UpgradeRules.Underway(sim, b.Id);
            if (b.Builders && p == null && c.Why != UpgradeWhy.Pucca) return; // a builder's own business until it is pucca or being made so
            vm.Lines.Add(new LineVm(s.Get("upgrade.row"), State(ctx, c, p), Why, b.Id));
            if (p != null)
            {
                BuildingSheet.Site(ctx, vm, p);
                int first = vm.Actions.FindIndex(a => a.Id == "materials");
                if (vm.Actions.FindLastIndex(a => a.Id == "materials") != first) vm.Actions.RemoveAt(vm.Actions.FindLastIndex(a => a.Id == "materials")); // one button for the acre
                return;
            }
            if (!c.Offered || b.Builders) return;
            long id = b.Id, cost = UpgradeRules.Quote(sim, BuildProject.Yours).Total;
            string home = TownWords.Home(ctx, b.Id);
            vm.Actions.Add(ActionVm.Checked(ctx, "upgrade:" + id, s.Get(HouseCount(sim, b.TileId) > 1 ? "upgrade.buttonOf" : "upgrade.button", "home", home),
                s.Get(c.Family == null || c.Hand ? "upgrade.detailNoRent" : "upgrade.detail", "cost", ctx.Fmt.Coins(cost)), ActionKind.Secondary, at => new UpgradeHouse(at, id)));
        }

        private static int HouseCount(Simulation sim, int tile)
        {
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings) if (HouseRules.IsHouse(sim, b) && b.TileId == tile) n++;
            return n;
        }

        /// <summary>The card (the usual confirm card): your cash, the materials and masons, your cash after, and in words what happens to the rent and what comes back.</summary>
        internal static void Card(ViewContext ctx, ConfirmCardVm vm, Simulation sim, Simulation after, UpgradeHouse cmd)
        {
            var s = ctx.Strings;
            Building b = sim.State.Land.Buildings.Find(x => x.Id == cmd.BuildingId);
            BuildQuote q = UpgradeRules.Quote(sim, BuildProject.Yours);
            BuildingSheet.CardRows(ctx, vm, q, "build.cardNowYours", "build.cardAfterYours", sim.State.Ledger.Cash, after.State.Ledger.Cash);
            UpgradeCheck c = UpgradeRules.Check(sim, b);
            string home = TownWords.Home(ctx, b.Id);
            vm.Subtitle = c.Family == null ? s.Get("upgrade.subEmpty", "home", home) : s.Get("upgrade.subFamily", "home", home, "family", TownWords.Household(ctx, c.Family));
        }

        /// <summary>The card's closing words: when the work will be ready, what your kiln saves, and what the rent does and when the cost comes back.</summary>
        internal static string Warning(ViewContext ctx, Simulation sim, Simulation after, UpgradeHouse cmd)
        {
            Building b = sim.State.Land.Buildings.Find(x => x.Id == cmd.BuildingId);
            BuildQuote q = UpgradeRules.Quote(sim, BuildProject.Yours);
            return BuildingSheet.Timing(ctx, after, true) + BuildingSheet.KilnSaving(ctx, q) + " " + RentSentence(ctx, UpgradeRules.Check(sim, b), q.Total);
        }

        /// <summary>What the family's rent will do, and what the upgrade earns back: in words, with the numbers.</summary>
        private static string RentSentence(ViewContext ctx, UpgradeCheck c, long cost)
        {
            var s = ctx.Strings;
            Simulation sim = ctx.Sim;
            if (c.Family == null) return s.Get("upgrade.warnEmpty");
            if (c.Hand) return s.Get("upgrade.warnHand");
            LeaseOptions o = LeaseRules.Options(sim, c.Family, c.PuccaRent);
            long left = LeaseRules.SeasonsLeft(sim, c.Family), back = UpgradeRules.PaybackSeasons(sim, c, cost);
            string pct = ctx.Fmt.Integer(sim.Config.Game.Population.BurdenLimit.MulRatio(100, 1).RoundToLong());
            return s.Plural("upgrade.warnRent", left, "rent", ctx.Fmt.Coins(c.Family.LeaseRent), "most", ctx.Fmt.Coins(o.Allowed), "pucca", ctx.Fmt.Coins(c.PuccaRent), "pct", pct, "seasons", ctx.Fmt.Integer(back));
        }

        /// <summary>The Builders section's pucca count.</summary>
        internal static void TownRows(ViewContext ctx, SheetVm vm)
        {
            if (!UpgradeRules.On(ctx.Sim)) return;
            vm.Lines.Add(new LineVm(ctx.Strings.Get("builders.pucca"), ctx.Fmt.Integer(UpgradeRules.BuildersPucca(ctx.Sim)), Why, -1));
        }
    }

    public static partial class WhyView
    {
        /// <summary>Why a house is or is not offered the upgrade, each reason with its number (id: the building; −1 for the Builders' count).</summary>
        private static WhyVm UpgradeHouseWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            if (id < 0)
            {
                var b = New(ctx, "why.upgrade.builders.title", ctx.Fmt.Integer(UpgradeRules.BuildersPucca(sim)));
                Text(b, ctx, "why.upgrade.builders.text");
                return b;
            }
            Building house = sim.State.Land.Buildings.Find(x => x.Id == id);
            if (house == null || !UpgradeRules.On(sim)) return New(ctx, "why.upgrade.gone.title", s.Get("upgrade.state.underway"));
            UpgradeCheck c = UpgradeRules.Check(sim, house);
            BuildProject p = UpgradeRules.Underway(sim, id);
            WhyVm vm = New(ctx, "why.upgrade.title", UpgradeSheet.State(ctx, c, p));
            if (house.Pucca || p != null)
            {
                Row(vm, ctx, "why.upgrade.rentPucca", Money(ctx, c.PuccaRent));
                Text(vm, ctx, house.Pucca ? "why.upgrade.isPucca" : "why.upgrade.making");
                return vm;
            }
            UpgradeDefCheck(vm, ctx, c, house);
            return vm;
        }

        private static void UpgradeDefCheck(WhyVm vm, ViewContext ctx, UpgradeCheck c, Building house)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Row(vm, ctx, "why.upgrade.landValue", s.Get("why.upgrade.landValueValue", "have", ctx.Fmt.Percent(c.LandValue), "need", ctx.Fmt.Percent(c.Needs)));
            Row(vm, ctx, "why.upgrade.water", s.Get(LandValueRules.HasWater(sim, house.TileId) ? "why.upgrade.yes" : "why.upgrade.no"));
            string pct = ctx.Fmt.Integer(sim.Config.Game.Population.BurdenLimit.MulRatio(100, 1).RoundToLong());
            if (c.Family == null) Row(vm, ctx, "why.upgrade.family", s.Get("why.upgrade.familyEmpty"));
            else if (c.Hand) Row(vm, ctx, "why.upgrade.family", s.Get("why.upgrade.familyHand"));
            else Row(vm, ctx, "why.upgrade.family", s.Get(c.Why == UpgradeWhy.Rent ? "why.upgrade.no" : "why.upgrade.yes"));
            Text(vm, ctx, "why.upgrade.waterText", "m", ctx.Fmt.Integer(sim.Config.Game.Township.LandValue.WaterReach), "tower", ctx.Fmt.Integer(UtilityRules.Def(sim, UtilityDef.Tower).Reach));
            if (c.Family == null) Text(vm, ctx, "why.upgrade.familyEmptyText");
            else if (c.Hand) Text(vm, ctx, "why.upgrade.familyHandText");
            else Text(vm, ctx, c.Why == UpgradeWhy.Rent ? "why.upgrade.familyNoText" : "why.upgrade.familyYesText", "rent", Money(ctx, c.PuccaRent), "income", Money(ctx, c.Income), "pct", pct, "most", Money(ctx, c.Bearable));
            Row(vm, ctx, "why.upgrade.rentNow", Money(ctx, c.KutchaRent));
            Row(vm, ctx, "why.upgrade.rentPucca", Money(ctx, c.PuccaRent));
            long cost = UpgradeRules.Quote(sim, Sim.Core.Town.BuildProject.Yours).Total;
            Row(vm, ctx, "why.upgrade.cost", Money(ctx, cost));
            long back = UpgradeRules.PaybackSeasons(sim, c, cost);
            if (back > 0) Row(vm, ctx, "why.upgrade.payback", s.Get("why.upgrade.paybackValue", "n", ctx.Fmt.Integer(back)));
            Text(vm, ctx, "why.upgrade.text");
        }
    }
}
