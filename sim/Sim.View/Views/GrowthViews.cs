using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>
    /// A shop's marketing and khata (spec 028 §3.10, Preview 21): a line each on Today, their own parts of the score on Customers, a section
    /// each on Settings with an honest line of whether it paid, and on Books who owes what with a Cut off button. Every number has a *Why?*.
    /// </summary>
    public static class GrowthSheet
    {
        public const string MarketingWhy = "growthMarketing", MarketValueWhy = "growthMarketValue", KhataWhy = "growthKhata", LimitWhy = "growthLimit", LossWhy = "growthLoss",
            ValueWhy = "growthValue", OwedWhy = "growthOwed", HouseholdWhy = "growthHousehold";

        /// <summary>How many households the Books lists before "and more".</summary>
        public const int Listed = 8;

        internal static bool Marketing(ViewContext ctx, BusinessTypeDef t) { return KhataRules.On(ctx.Sim) && KhataRules.CanMarket(t); }

        internal static bool Khata(ViewContext ctx, BusinessTypeDef t) { return KhataRules.On(ctx.Sim) && KhataRules.CanKhata(t); }

        /// <summary>A budget at today's prices: "2,000 coins".</summary>
        internal static string Budget(ViewContext ctx, long budget) { return ctx.Fmt.Coins(MarketRules.Indexed(ctx.Sim, budget)); }

        /// <summary>"+0.038": a score part to three decimals.</summary>
        internal static string Points(ViewContext ctx, Fixed f) { return (f < Fixed.Zero ? "" : "+") + ctx.Fmt.Decimal3(f); }

        /// <summary>What a coin of sales left over at its last close (the shop's margin): sales less the goods sold, over sales; 0 before it has a closed season.</summary>
        internal static Fixed Margin(Business b)
        {
            if (b.Reports.Count == 0) return Fixed.Zero;
            BusinessReport r = b.Reports[b.Reports.Count - 1];
            long sales = BusinessBooksSheet.Amount(r, BusinessBooksSheet.Sales);
            return sales <= 0 ? Fixed.Zero : Fixed.FromRatio(sales - BusinessBooksSheet.Amount(r, BusinessBooksSheet.Goods), sales);
        }

        /// <summary>The margin on the extra sales khata brought last season.</summary>
        internal static long KhataMargin(ViewContext ctx, Business b) { return Sim.Core.Finance.Money.MulRate(b.LastGain, Margin(b)); }

        // ---- Today ----

        internal static void AddToday(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            if (Marketing(ctx, t))
                vm.Lines.Add(new LineVm(s.Get("growth.marketing"), b.Marketing == 0 && b.MarketLift == Fixed.Zero ? s.Get("growth.off") : s.Get("growth.perSeason", "amount", Budget(ctx, b.Marketing)), MarketingWhy, b.Id)
                    { Icon = "megaphone", Note = b.MarketLift == Fixed.Zero ? null : s.Get(b.Marketing == 0 ? "growth.fading" : "growth.lifts", "n", Points(ctx, b.MarketLift)) });
            if (Khata(ctx, t))
                vm.Lines.Add(new LineVm(s.Get("growth.khata"), s.Get(b.Khata ? "growth.on" : "growth.off"), KhataWhy, b.Id)
                    { Icon = "menu", Note = b.Khata || b.Credit.Count > 0 ? s.Get("growth.khataNote", "n", ctx.Fmt.Integer(b.Credit.Count), "owed", ctx.Fmt.Coins(KhataRules.Owed(b))) : null });
        }

        // ---- Customers ----

        /// <summary>Marketing's and khata's own parts of your score, under your line, and in words what the village shop does about you.</summary>
        internal static void AddCustomers(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            var s = ctx.Strings;
            Attraction a = ShopRules.Attract(ctx.Sim, b, ShopGroup.Town);
            if (Marketing(ctx, t) && b.MarketLift > Fixed.Zero)
                vm.Lines.Add(new LineVm(s.Get("growth.ofMarketing"), Points(ctx, b.MarketLift), MarketingWhy, b.Id) { Icon = "megaphone" });
            if (Khata(ctx, t) && b.Khata)
                vm.Lines.Add(new LineVm(s.Get("growth.ofKhata"), Points(ctx, a.Khata), KhataWhy, b.Id) { Icon = "menu", Note = s.Get("growth.ofKhataNote", "income", ctx.Fmt.Coins(MarketRules.Indexed(ctx.Sim, ShopRules.Defs(ctx.Sim).KhataLowIncome))) });
        }

        /// <summary>The village shop that reacts to you: it cuts its price when you take a third of its share (ADR 0095 §3), and says so.</summary>
        internal static void AddReaction(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            foreach (Rival r in sim.State.Shops.Rivals)
            {
                if (r.Kind != t.Kind || !ShopRules.Defs(sim).Rivals[r.Def].Reacts) continue;
                bool cut = r.PriceFactor < ShopRules.Defs(sim).Rivals[r.Def].PriceFactor;
                vm.Lines.Add(new LineVm(s.Get("growth.reacts", "name", ShopWords.Rival(ctx, r)), null, "shopShare", ShopWords.ShareId(sim.State.Shops.All().IndexOf(r)))
                    { Icon = ShopWords.RivalIcon(ctx, r), Note = s.Get(cut ? "growth.reactsCut" : "growth.reactsNot", "pct", ctx.Fmt.Percent(ShopRules.Defs(sim).RivalStep), "share", ctx.Fmt.Percent(ShopSheet.Share(b.CustomersToday, ShopSheet.Everyone(sim, t.Kind)))), NoteTone = cut ? Tone.Warn : Tone.Plain });
            }
        }

        // ---- Settings ----

        internal static void AddSettings(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            ShopDefs d = ShopRules.Defs(sim);
            long id = b.Id;
            if (Marketing(ctx, t))
            {
                vm.Lines.Add(LineVm.Header(s.Get("growth.marketing")));
                vm.Lines.Add(new LineVm(s.Get("growth.budget"), b.Marketing == 0 ? s.Get("growth.none") : Budget(ctx, b.Marketing), MarketingWhy, id) { Icon = "megaphone", Note = s.Get("growth.budgetNote") });
                var buttons = new List<ActionVm>();
                foreach (long budget in d.MarketBudgets)
                {
                    long pick = budget;
                    ActionVm choice = ActionVm.Checked(ctx, "marketing:" + budget, budget == 0 ? s.Get("growth.none") : ctx.Fmt.Amount(MarketRules.Indexed(sim, budget)), budget == 0 ? null : Points(ctx, KhataRules.Lift(sim, budget)),
                        ActionKind.Secondary, at => new SetMarketing(at, id, pick));
                    choice.Chosen = budget == b.Marketing;
                    buttons.Add(choice);
                }
                vm.Lines.Add(new LineVm(null, null) { Buttons = buttons });
                if (b.LastSpend > 0 || b.MarketLift > Fixed.Zero)
                    vm.Lines.Add(new LineVm(s.Get("growth.marketGain"), b.LastSpend > 0 ? s.Get("growth.marketGainValue", "spent", ctx.Fmt.Coins(b.LastSpend), "n", ctx.Fmt.Integer(b.LastDrew)) : s.Get("growth.marketFading"),
                        MarketValueWhy, id) { Icon = "coin", Note = b.LastSpend > 0 ? s.Get(b.LastDrew > 0 ? "growth.marketGainNote" : "growth.marketNoGain") : null, NoteTone = b.LastSpend > 0 && b.LastDrew == 0 ? Tone.Warn : Tone.Plain });
            }
            if (!Khata(ctx, t)) return;
            vm.Lines.Add(LineVm.Header(s.Get("growth.khata")));
            bool on = b.Khata;
            var sw = new LineVm(s.Get("growth.khataCredit"), s.Get(on ? "growth.on" : "growth.off"), KhataWhy, id) { Icon = "menu", Note = s.Get("growth.khataCreditNote") };
            sw.Action = ActionVm.Checked(ctx, "khata", s.Get(on ? "growth.switchOff" : "growth.switchOn"), null, ActionKind.Secondary, at => new SetKhata(at, id, !on));
            vm.Lines.Add(sw);
            if (!on) return;
            vm.Lines.Add(new LineVm(s.Get("growth.limit"), ctx.Fmt.Coins(KhataRules.Limit(sim, b)), LimitWhy, id) { Icon = "menu", Note = s.Get("growth.limitNote") });
            var limits = new List<ActionVm>();
            foreach (long limit in d.KhataLimits)
            {
                long pick = limit;
                ActionVm choice = ActionVm.Checked(ctx, "limit:" + limit, ctx.Fmt.Amount(MarketRules.Indexed(sim, limit)), null, ActionKind.Secondary, at => new SetKhataLimit(at, id, pick));
                choice.Chosen = limit == b.KhataLimit;
                limits.Add(choice);
            }
            vm.Lines.Add(new LineVm(null, null) { Buttons = limits });
            if (b.LastCredit > 0 || b.LastLost > 0)
            {
                long margin = KhataMargin(ctx, b), net = margin - b.LastLost;
                vm.Lines.Add(new LineVm(s.Get("growth.khataGain"), s.Get("growth.khataGainValue", "credit", ctx.Fmt.Coins(b.LastCredit), "gain", ctx.Fmt.Coins(b.LastGain), "lost", ctx.Fmt.Coins(b.LastLost)), ValueWhy, id)
                    { Icon = "coin", Note = s.Get(net > 0 ? "growth.khataPays" : "growth.khataNoPay", "margin", ctx.Fmt.Coins(margin), "lost", ctx.Fmt.Coins(b.LastLost)), NoteTone = net > 0 ? Tone.Good : Tone.Warn });
            }
        }

        // ---- Books ----

        /// <summary>Who owes the shop what: the total, the last close's bad debt, and the households with a Cut off button each.</summary>
        internal static void AddBooks(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            if (!Khata(ctx, t) || !(b.Khata || b.Credit.Count > 0 || b.LastCredit > 0)) return;
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            long id = b.Id;
            vm.Lines.Add(LineVm.Header(s.Get("growth.khata")));
            vm.Lines.Add(new LineVm(s.Get("growth.owed"), ctx.Fmt.Coins(KhataRules.Owed(b)), OwedWhy, id) { Icon = "menu", Note = s.Get("growth.owedNote", "n", ctx.Fmt.Integer(b.Credit.Count)) });
            vm.Lines.Add(new LineVm(s.Get("growth.badDebt"), "−" + ctx.Fmt.Coins(b.LastLost), LossWhy, id) { Icon = "menu", Note = s.Get("growth.badDebtNote") });
            if (b.Credit.Count == 0) return;
            vm.Lines.Add(LineVm.Header(s.Get("growth.households")));
            var list = new List<KhataAccount>(b.Credit);
            list.Sort((x, y) => (y.Balance + y.Late).CompareTo(x.Balance + x.Late));
            long limit = KhataRules.Limit(sim, b);
            for (int i = 0; i < list.Count && i < Listed; i++)
            {
                KhataAccount a = list[i];
                Household h = sim.State.Citizens?.Household(a.Household);
                if (h == null) continue;
                long hid = a.Household;
                bool atLimit = !a.CutOff && a.Balance + a.Late >= limit;
                var line = new LineVm(TownWords.Household(ctx, h), s.Get("growth.owes", "owes", ctx.Fmt.Coins(a.Balance + a.Late), "limit", ctx.Fmt.Coins(limit)), HouseholdWhy, id * 1_000_000 + hid)
                    { Icon = "person_hand_m", Note = a.CutOff ? s.Get("growth.cutByYou") : atLimit ? s.Get("growth.atLimit") : a.Late > 0 ? s.Get("growth.late") : s.Get("growth.income", "n", ctx.Fmt.Coins(h.Income)), NoteTone = a.CutOff || atLimit ? Tone.Warn : Tone.Plain };
                bool cut = a.CutOff;
                line.Action = ActionVm.Checked(ctx, "cut:" + hid, s.Get(cut ? "growth.allow" : "growth.cutOff"), null, ActionKind.Secondary, at => new CutOffHousehold(at, id, hid, !cut));
                vm.Lines.Add(line);
            }
            if (list.Count > Listed) vm.Lines.Add(new LineVm(null, s.Get("growth.more", "n", ctx.Fmt.Integer(Listed), "of", ctx.Fmt.Integer(list.Count))) { Dim = true });
        }
    }
}
