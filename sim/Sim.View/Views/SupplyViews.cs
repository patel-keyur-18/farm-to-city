using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;

namespace Sim.View.Views
{
    /// <summary>
    /// A business's Supply tab (spec 030 §3.4, §3.5, US-268, Preview 24): what it took today from you and bought, good by good, how it is supplied
    /// (Automatic or Manual) and, in Manual, how much of each good it may take from you a day; its milk or wheat a day and buying at the market, which were on Settings.
    /// </summary>
    public static class SupplySheet
    {
        public const string Tab = "supply";
        public const string CapKind = "SupplyCap";

        /// <summary>Every business that takes goods in has the tab, but not a kiln or a tuition centre, nor a closed business.</summary>
        public static bool Applies(ViewContext ctx, Business b)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, b);
            if (b.Status == BusinessStatus.Closed || t.IsKiln || t.IsTuition) return false;
            return SupplyRules.Counted(ctx.Sim, b).Count > 0;
        }

        private static bool MarketSwitch(BusinessTypeDef t) { return !t.IsMeals && (t.IsShop || !t.ByRecipe || BusinessRules.BoughtInputs(t).Count > 0); }

        /// <summary>Quantity words for a good in grams (milk in litres, the rest in kg).</summary>
        private static string Qty(ViewContext ctx, int good, long grams) { return ctx.Fmt.Quantity(ctx.Sim.Config.Game.Market.Goods[good], grams); }

        internal static void Add(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            bool mode = SupplyRules.HasMode(sim, t), manual = b.SupplyManual;
            if (mode)
            {
                var row = new LineVm(s.Get("supply.mode"), s.Get(manual ? "supply.modeManual" : "supply.modeAuto"), "supplyMode", id)
                    { Icon = "coin", Note = s.Get(manual ? "supply.modeManualNote" : "supply.modeAutoNote") };
                row.Action = ActionVm.Checked(ctx, "supplyMode", s.Get(manual ? "supply.useAuto" : "supply.useManual"), null, ActionKind.Secondary, at => new SetSupplyMode(at, id, !manual));
                vm.Lines.Add(row);
            }
            vm.Lines.Add(LineVm.Header(s.Get("supply.today")));
            List<int> capped = SupplyRules.CappedInputs(sim, t);
            foreach (int g in SupplyRules.Counted(sim, b))
            {
                long you = SupplyRules.FromYouTodayOf(sim, b, g), bought = SupplyRules.BoughtTodayOf(sim, b, g);
                GoodDef good = sim.Config.Game.Market.Goods[g];
                if (t.TakesMilk && you + bought == 0 && b.Stock[g] == 0) continue; // a milk this herd doesn't give
                var line = new LineVm(s.Get(good.NameKey), s.Get("supply.value", "you", Qty(ctx, g, you), "bought", Qty(ctx, g, bought)), "supplyToday", BusinessWords.GoodId(id, g)) { Icon = good.Id };
                bool isCapped = capped.Contains(g);
                if (!isCapped && !t.TakesMilk && t.IsShop == false && !t.ByRecipe) line.Note = null; // the mill: its wheat a day follows
                else if (!isCapped && !t.TakesMilk) line.Note = s.Get("supply.notFromYou");
                else if (isCapped)
                {
                    line.Note = CapText(ctx, b, t, g);
                    if (manual && !b.BuyFromMarket && b.CapGrams[g] == 0 && b.CapShare[g] == 0) line.NoteTone = Tone.Warn; // nothing to sell: it buys none either
                    if (manual) line.Action = ActionVm.Open("cap:" + g, s.Get("business.change"), new SheetRequest(CapKind, id, BusinessWords.GoodId(id, g).ToString()));
                }
                vm.Lines.Add(line);
            }
            if (t.TakesMilk) { if (t.IsShop) ShopSheet.AddMilkADay(ctx, vm, b, t); else BusinessSheet.AddMilkADay(ctx, vm, b, t); }
            else if (!t.IsShop && !t.ByRecipe && !t.IsMeals) BusinessSheet.AddOwnDay(ctx, vm, b, t);
            if (MarketSwitch(t)) BusinessSheet.AddMarket(ctx, vm, b, t);
        }

        /// <summary>What the business may take of a good from you: all it needs under Automatic; under Manual its cap in words.</summary>
        internal static string CapText(ViewContext ctx, Business b, BusinessTypeDef t, int g)
        {
            var s = ctx.Strings;
            if (!b.SupplyManual) return s.Get("supply.capAll");
            long cap = SupplyRules.CapOf(ctx.Sim, b, g);
            if (cap == long.MaxValue) return s.Get("supply.capAll");
            if (b.CapGrams[g] > 0) return s.Get("supply.capGrams", "kg", ctx.Fmt.Kg(b.CapGrams[g]));
            if (b.CapShare[g] == 0) return s.Get(b.BuyFromMarket ? "supply.capNone" : "supply.capNoneNoMarket");
            return s.Get("supply.capShare", "share", s.Get("supply.share." + b.CapShare[g]), "kg", ctx.Fmt.Kg(cap));
        }

        // ---- the amounts (Change) ----

        /// <summary>The amounts a good's cap can be: all it needs, shares of its day's amount (not a restaurant), the steps that fit, none; the one in force is marked.</summary>
        public static SheetVm Cap(ViewContext ctx, long businessId, string arg)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            Business b = sim.State.Businesses?.Find(businessId);
            int g = long.TryParse(arg, out long key) ? (int)(key % 1000) : -1;
            if (b == null || g < 0 || g >= sim.Config.Game.Market.Goods.Count) return new SheetVm { Icon = "flour_mill", Title = s.Get("reject.UnknownBusiness") };
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            GoodDef good = sim.Config.Game.Market.Goods[g];
            var vm = new SheetVm { Icon = good.Id, Title = s.Get("supply.pickTitle", "good", s.Get(good.NameKey), "name", BusinessWords.InSentence(ctx, t)), Subtitle = s.Get("supply.pickSubtitle") };
            long day = SupplyRules.DayAmount(sim, b, g);
            string goodId = good.Id;
            bool meals = t.IsMeals;
            void Row(string label, string detail, int share, long grams)
            {
                bool now = grams > 0 ? b.CapGrams[g] == grams : b.CapGrams[g] == 0 && b.CapShare[g] == share;
                var line = new LineVm(label, detail, "supplyCap", BusinessWords.GoodId(b.Id, g));
                if (now) line.Badge = s.Get("supply.inForce");
                else line.Action = ActionVm.Checked(ctx, "cap:" + share + ":" + grams, s.Get("supply.choose"), null, ActionKind.Primary, at => new SetSupplyCap(at, businessId, goodId, share, grams));
                vm.Lines.Add(line);
            }
            Row(s.Get("supply.choice.all"), day > 0 ? s.Get("supply.detail.share", "kg", ctx.Fmt.Kg(day)) : s.Get("supply.detail.all"), 100, 0);
            if (!meals && day > 0)
            {
                Row(s.Get("supply.choice.half"), s.Get("supply.detail.share", "kg", ctx.Fmt.Kg(day * 50 / 100)), 50, 0);
                Row(s.Get("supply.choice.quarter"), s.Get("supply.detail.share", "kg", ctx.Fmt.Kg(day * 25 / 100)), 25, 0);
            }
            foreach (long step in BusinessRules.Defs(sim).SupplySteps)
                if (meals || step < day || b.CapGrams[g] == step) Row(s.Get("supply.choice.grams", "kg", ctx.Fmt.Kg(step)), s.Get("supply.detail.grams", "kg", ctx.Fmt.Kg(step)), 100, step);
            Row(s.Get("supply.choice.none"), s.Get("supply.detail.none"), 0, 0);
            return vm;
        }
    }
}
