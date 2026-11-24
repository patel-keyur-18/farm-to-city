using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.View.Views
{
    /// <summary>A shop's marketing and khata (spec 028 §3.10): what each adds, what it costs, and whether it paid, in words.</summary>
    public static partial class WhyView
    {
        /// <summary>A score part in points to three decimals: "+0.038 points".</summary>
        private static string Pts(ViewContext ctx, Fixed f) { return ctx.Strings.Get("why.shopShare.points", "n", GrowthSheet.Points(ctx, f)); }

        /// <summary>Marketing: each 1,000 a season adds 0.019 to the score, up to 0.15; it lasts only while it is spent.</summary>
        private static WhyVm GrowthMarketing(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            ShopDefs d = ShopRules.Defs(sim);
            WhyVm vm = New(ctx, "why.growthMarketing.title", Pts(ctx, b.MarketLift));
            Row(vm, ctx, "why.growthMarketing.per", Pts(ctx, d.MarketPer1000), "unit", Money(ctx, MarketRules.Indexed(sim, 1000 * 100)));
            Row(vm, ctx, "why.growthMarketing.budget", b.Marketing == 0 ? ctx.Strings.Get("growth.none") : GrowthSheet.Budget(ctx, b.Marketing));
            Row(vm, ctx, "why.growthMarketing.cap", Pts(ctx, d.MarketCap));
            Text(vm, ctx, "why.growthMarketing.size");
            Row(vm, ctx, "why.growthMarketing.fade", ctx.Fmt.Percent(d.MarketFade));
            vm.Footnote = ctx.Strings.Get("why.growthMarketing.lesson");
            return vm;
        }

        /// <summary>Whether marketing paid: what was spent, the customers it drew and their margin (the shop's own, from its last close).</summary>
        private static WhyVm GrowthMarketValue(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            long drewValue = DrewValue(sim, t, b.LastDrew), margin = Sim.Core.Finance.Money.MulRate(drewValue, GrowthSheet.Margin(b));
            WhyVm vm = New(ctx, "why.growthMarketValue.title", margin >= b.LastSpend ? ctx.Strings.Get("why.growthMarketValue.pays") : ctx.Strings.Get("why.growthMarketValue.notYet"));
            Row(vm, ctx, "why.growthMarketValue.spent", Money(ctx, b.LastSpend));
            Row(vm, ctx, "why.growthMarketValue.drew", ShopWords.Amount(ctx, t.Kind, b.LastDrew));
            Row(vm, ctx, "why.growthMarketValue.sold", Money(ctx, drewValue));
            Row(vm, ctx, "why.growthMarketValue.margin", Money(ctx, margin));
            Row(vm, ctx, "why.growthMarketValue.back", Money(ctx, margin - b.LastSpend));
            vm.Footnote = ctx.Strings.Get("why.growthMarketValue.lesson");
            return vm;
        }

        /// <summary>What a quantity of a shop's goods sells for at the reference: each good by its share of the day's demand.</summary>
        private static long DrewValue(Simulation sim, BusinessTypeDef t, long quantity)
        {
            long total = 0, value = 0;
            foreach (int g in ShopRules.KindGoods(sim, t.Kind))
            {
                if (t.Kind == ShopKind.Veg && !t.Sells(g) || t.Kind == ShopKind.Sweets && !t.Makes(g)) continue;
                long demand = ShopRules.DemandPerDay(sim, t.Kind, g, ShopGroup.Town) + ShopRules.DemandPerDay(sim, t.Kind, g, ShopGroup.Village);
                total += demand;
            }
            if (total <= 0) return 0;
            foreach (int g in ShopRules.KindGoods(sim, t.Kind))
            {
                if (t.Kind == ShopKind.Veg && !t.Sells(g) || t.Kind == ShopKind.Sweets && !t.Makes(g)) continue;
                long demand = ShopRules.DemandPerDay(sim, t.Kind, g, ShopGroup.Town) + ShopRules.DemandPerDay(sim, t.Kind, g, ShopGroup.Village);
                long q = (long)((System.Numerics.BigInteger)quantity * demand / total);
                value += g < 0 ? Sim.Core.Finance.Money.Value(q, ShopRules.Reference(sim, t.Kind, g), 1) : MarketRules.Value(sim, g, q, ShopRules.Reference(sim, t.Kind, g));
            }
            return value;
        }

        /// <summary>Khata: who may take credit, the lift for the poorer households, and what is owed.</summary>
        private static WhyVm GrowthKhata(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            KhataRules.Refresh(sim, b);
            ShopDefs d = ShopRules.Defs(sim);
            WhyVm vm = New(ctx, "why.growthKhata.title", ctx.Strings.Get(b.Khata ? "growth.on" : "growth.off"));
            Text(vm, ctx, "why.growthKhata.regular");
            Row(vm, ctx, "why.growthKhata.limit", ctx.Fmt.Coins(KhataRules.Limit(sim, b)));
            Row(vm, ctx, "why.growthKhata.score", Pts(ctx, d.KhataScore), "income", ctx.Fmt.Coins(MarketRules.Indexed(sim, d.KhataLowIncome)));
            Row(vm, ctx, "why.growthKhata.kilos", ctx.Fmt.Percent(d.KhataKilos));
            Row(vm, ctx, "why.growthKhata.now", Pts(ctx, b.KhataLiftNow));
            Row(vm, ctx, "why.growthKhata.owed", Money(ctx, KhataRules.Owed(b)));
            Text(vm, ctx, "why.growthKhata.village");
            vm.Footnote = ctx.Strings.Get("why.growthKhata.lesson");
            return vm;
        }

        /// <summary>The limit: the most a household can owe, and who is cut off.</summary>
        private static WhyVm GrowthLimit(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            int cut = 0;
            foreach (KhataAccount a in b.Credit) if (a.CutOff || a.Balance + a.Late >= KhataRules.Limit(sim, b)) cut++;
            WhyVm vm = New(ctx, "why.growthLimit.title", Money(ctx, KhataRules.Limit(sim, b)));
            string limits = "";
            foreach (long v in ShopRules.Defs(sim).KhataLimits) limits += (limits.Length > 0 ? ", " : "") + ctx.Fmt.Coins(MarketRules.Indexed(sim, v));
            Row(vm, ctx, "why.growthLimit.choices", limits);
            Row(vm, ctx, "why.growthLimit.cut", ctx.Fmt.Integer(cut));
            Text(vm, ctx, "why.growthLimit.you");
            Text(vm, ctx, "why.growthLimit.risk");
            vm.Footnote = ctx.Strings.Get("why.growthLimit.lesson");
            return vm;
        }

        /// <summary>What khata loses: the share never paid by income, the late part and the bad debt written off.</summary>
        private static WhyVm GrowthLoss(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            ShopDefs d = ShopRules.Defs(sim);
            WhyVm vm = New(ctx, "why.growthLoss.title", "−" + Money(ctx, b.LastLost));
            long under = 0;
            foreach (var tier in d.KhataLoss)
            {
                if (tier.Key == 0) Row(vm, ctx, "why.growthLoss.above", ctx.Fmt.Percent(tier.Value), "income", ctx.Fmt.Coins(MarketRules.Indexed(sim, under)));
                else Row(vm, ctx, "why.growthLoss.under", ctx.Fmt.Percent(tier.Value), "income", ctx.Fmt.Coins(MarketRules.Indexed(sim, tier.Key)));
                under = tier.Key;
            }
            Row(vm, ctx, "why.growthLoss.late", ctx.Fmt.Percent(d.KhataLate));
            Text(vm, ctx, "why.growthLoss.writeOff");
            vm.Footnote = ctx.Strings.Get("why.growthLoss.lesson");
            return vm;
        }

        /// <summary>Whether khata paid: the extra sales it brought, their margin, against what was never paid.</summary>
        private static WhyVm GrowthValue(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            long margin = GrowthSheet.KhataMargin(ctx, b), net = margin - b.LastLost;
            WhyVm vm = New(ctx, "why.growthValue.title", (net >= 0 ? "+" : "−") + Money(ctx, System.Math.Abs(net)));
            Row(vm, ctx, "why.growthValue.credit", Money(ctx, b.LastCredit));
            Row(vm, ctx, "why.growthValue.gain", Money(ctx, b.LastGain), "pct", ctx.Fmt.Percent(ShopRules.Defs(ctx.Sim).KhataKilos));
            Row(vm, ctx, "why.growthValue.margin", Money(ctx, margin), "pct", ctx.Fmt.Percent(GrowthSheet.Margin(b)));
            Row(vm, ctx, "why.growthValue.lost", "−" + Money(ctx, b.LastLost));
            Text(vm, ctx, "why.growthValue.how");
            vm.Footnote = ctx.Strings.Get("why.growthValue.lesson");
            return vm;
        }

        /// <summary>What is owed to the shop: the credit sold and not yet collected, and what the close will do with it.</summary>
        private static WhyVm GrowthOwed(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id);
            ShopDefs d = ShopRules.Defs(sim);
            long owed = 0, late = 0;
            foreach (KhataAccount a in b.Credit) { owed += a.Balance; late += a.Late; }
            WhyVm vm = New(ctx, "why.growthOwed.title", Money(ctx, owed + late));
            Row(vm, ctx, "why.growthOwed.credit", Money(ctx, owed), "n", ctx.Fmt.Integer(b.Credit.Count));
            if (late > 0) Row(vm, ctx, "why.growthOwed.late", Money(ctx, late));
            Text(vm, ctx, "why.growthOwed.close", "late", ctx.Fmt.Percent(d.KhataLate));
            Text(vm, ctx, "why.growthOwed.books");
            return vm;
        }

        /// <summary>One household's account: what it owes against the limit, its income, the share never paid.</summary>
        private static WhyVm GrowthHousehold(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            Business b = BusinessOf(ctx, id / 1_000_000);
            KhataAccount a = b == null ? null : KhataRules.AccountOf(b, id % 1_000_000);
            Household h = sim.State.Citizens?.Household(id % 1_000_000);
            if (a == null || h == null) return New(ctx, "why.growthHousehold.gone", null);
            WhyVm vm = New(ctx, "why.growthHousehold.title", Money(ctx, a.Balance + a.Late), "name", TownWords.Household(ctx, h));
            Row(vm, ctx, "why.growthHousehold.owes", Money(ctx, a.Balance));
            if (a.Late > 0) Row(vm, ctx, "why.growthHousehold.late", Money(ctx, a.Late));
            Row(vm, ctx, "why.growthHousehold.limit", Money(ctx, KhataRules.Limit(sim, b)));
            Row(vm, ctx, "why.growthHousehold.income", Money(ctx, h.Income));
            Row(vm, ctx, "why.growthHousehold.rate", ctx.Fmt.Percent(KhataRules.LossRate(sim, h.Income)));
            if (a.WrittenOff > 0) Row(vm, ctx, "why.growthHousehold.written", Money(ctx, a.WrittenOff));
            Text(vm, ctx, a.CutOff ? "why.growthHousehold.cutByYou" : "why.growthHousehold.open");
            return vm;
        }
    }
}
