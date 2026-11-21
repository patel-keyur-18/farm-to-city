using Sim.Core;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Town;

namespace Sim.View.Views
{
    /// <summary>Forward contracts' <em>Why?</em>s (spec 027 §7): an offer's price against the expected one, a contract's delivery and what it gained or lost, a shortfall. Words, never a factor (ADR 0043).</summary>
    public static partial class WhyView
    {
        /// <summary>"+1,200 coins" or "−382 coins": short enough for a Why? header's value.</summary>
        private static string SignedCoins(ViewContext ctx, long amount) { return (amount > 0 ? "+" : amount < 0 ? "−" : "") + Money(ctx, System.Math.Abs(amount)); }

        private static GoodDef ContractGood(Simulation sim, string id) { return sim.Config.Game.Market.Goods[sim.Config.Game.Market.IndexOf(id)]; }

        /// <summary>An offer: the expected price for its season, the discount, the fixed price, and how the quantity was chosen.</summary>
        private static WhyVm ContractOfferWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ContractsState st = ContractRules.State(sim);
            Contract o = st?.Offers.Find(x => x.Id == id);
            if (o == null) return New(ctx, "why.conOffer.title", "");
            ContractsDef d = ContractRules.Def(sim);
            WhyVm vm = New(ctx, "why.conOffer.title", Price(ctx, o.Price), "good", ctx.Strings.Get(ContractGood(sim, o.Good).NameKey));
            Row(vm, ctx, "why.conOffer.expected", Price(ctx, o.Expected), "season", FieldSheets.SeasonName(ctx, o.Delivery));
            Row(vm, ctx, "why.conOffer.discount", "−" + Price(ctx, o.Expected - o.Price), "pct", Pct(ctx, d.Discount));
            Row(vm, ctx, "why.conOffer.price", Price(ctx, o.Price));
            Row(vm, ctx, "why.conOffer.qty", ctx.Fmt.Kg(o.Quantity), "pct", Pct(ctx, d.ShareOfHarvest), "harvest", ctx.Fmt.Kg(ContractsState.Of(st.HarvestLast, o.Good)));
            Text(vm, ctx, "why.conOffer.text", "pct", Pct(ctx, d.Discount));
            return vm;
        }

        /// <summary>A contract you hold: the fixed price, what is delivered, what the market pays now, and what a shortfall would cost.</summary>
        private static WhyVm ContractOpenWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ContractsState st = ContractRules.State(sim);
            Contract c = st?.Open.Find(x => x.Id == id);
            if (c == null) return New(ctx, "why.conOpen.title", "");
            GoodDef good = ContractGood(sim, c.Good);
            int g = sim.Config.Game.Market.IndexOf(c.Good);
            WhyVm vm = New(ctx, "why.conOpen.title", c.Delivery == sim.Period ? SignedCoins(ctx, ContractRules.Against(sim, c)) : "", "good", ctx.Strings.Get(good.NameKey));
            Row(vm, ctx, "why.conOpen.price", Price(ctx, c.Price), "expected", Price(ctx, c.Expected));
            Row(vm, ctx, "why.conOpen.qty", ctx.Fmt.Kg(c.Quantity), "season", FieldSheets.SeasonName(ctx, c.Delivery));
            if (c.Delivery == sim.Period)
            {
                Row(vm, ctx, "why.conOpen.filled", ctx.Fmt.Kg(c.Filled), "left", ctx.Fmt.Kg(c.Left));
                Row(vm, ctx, "why.conOpen.market", Price(ctx, Sim.Core.Market.MarketRules.Floor(sim, g)));
                Row(vm, ctx, "why.conOpen.gain", Money(ctx, c.Gain));
                Row(vm, ctx, "why.conOpen.short", Money(ctx, ContractRules.Shortfall(sim, c)), "avg", Price(ctx, ContractRules.AverageFloor(sim, c)));
            }
            Text(vm, ctx, c.Delivery == sim.Period ? "why.conOpen.textRunning" : "why.conOpen.textWaiting");
            return vm;
        }

        /// <summary>A settled contract: what was delivered, the shortfall charged, and the whole against the market.</summary>
        private static WhyVm ContractDoneWhy(ViewContext ctx, long index)
        {
            Simulation sim = ctx.Sim;
            ContractsState st = ContractRules.State(sim);
            if (st == null || index < 0 || index >= st.History.Count) return New(ctx, "why.conDone.title", "");
            ContractRecord r = st.History[(int)index];
            WhyVm vm = New(ctx, "why.conDone.title", SignedCoins(ctx, r.Against), "good", ctx.Strings.Get(ContractGood(sim, r.Good).NameKey));
            Row(vm, ctx, "why.conDone.price", Price(ctx, r.Price));
            Row(vm, ctx, "why.conDone.filled", ctx.Fmt.Kg(r.Filled), "qty", ctx.Fmt.Kg(r.Quantity));
            Row(vm, ctx, "why.conDone.short", Money(ctx, r.Shortfall));
            Row(vm, ctx, "why.conDone.against", Money(ctx, r.Against));
            Text(vm, ctx, "why.conDone.text");
            return vm;
        }

        /// <summary>The close's contract line: the running contracts' shortfall as it stands, and what was carried.</summary>
        private static WhyVm ContractDueWhy(ViewContext ctx, long id)
        {
            Simulation sim = ctx.Sim;
            ContractsState st = ContractRules.State(sim);
            WhyVm vm = New(ctx, "why.conDue.title", Money(ctx, ContractRules.Due(sim)));
            if (st != null)
            {
                if (st.Owed > 0) Row(vm, ctx, "why.conDue.carried", Money(ctx, st.Owed));
                foreach (Contract c in st.Open)
                    if (c.Delivery == sim.Period && ContractRules.Shortfall(sim, c) > 0)
                        Row(vm, ctx, "why.conDue.row", Money(ctx, ContractRules.Shortfall(sim, c)), "good", ctx.Strings.Get(ContractGood(sim, c.Good).NameKey), "left", ctx.Fmt.Kg(c.Left));
            }
            Text(vm, ctx, "why.conDue.text");
            return vm;
        }
    }
}
