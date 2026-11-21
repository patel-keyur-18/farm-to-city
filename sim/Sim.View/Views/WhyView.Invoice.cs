using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Definitions;

namespace Sim.View.Views
{
    /// <summary>Invoices (spec 028 §3.1): why a term, why money is owed, why an invoice is late and why a seller stopped.</summary>
    public static partial class WhyView
    {
        private static Invoice InvoiceOf(ViewContext ctx, long id)
        {
            foreach (Invoice i in InvoiceRules.State(ctx.Sim)?.Open ?? new System.Collections.Generic.List<Invoice>()) if (i.Id == id) return i;
            return null;
        }

        private static string InvoiceGood(ViewContext ctx, Invoice i) { return BusinessWords.Good(ctx, ctx.Sim.Config.Game.Market.IndexOf(i.Good)); }

        private static string StageWord(ViewContext ctx, Invoice i) { return ctx.Strings.Get("invoices.stage." + i.Stage.ToString().ToLowerInvariant()).ToLowerInvariant(); }

        private static WhyVm InvoiceTerms(ViewContext ctx, long id)
        {
            InvoicesDef d = InvoiceRules.Defs(ctx.Sim);
            Business b = BusinessOf(ctx, id);
            WhyVm vm = New(ctx, "why.invoiceTerms.title", ctx.Strings.Get(d.Terms[d.DefaultTerm].NameKey), "who", b == null ? "" : BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, b)));
            foreach (InvoiceTermDef t in d.Terms)
                if (t.Days == 0) Text(vm, ctx, "why.invoiceTerms.cash");
                else Text(vm, ctx, "why.invoiceTerms.term", "term", ctx.Strings.Get(t.NameKey), "days", ctx.Fmt.Integer(t.Days));
            Text(vm, ctx, "why.invoiceTerms.shops");
            vm.Footnote = ctx.Strings.Get("why.invoiceTerms.rule");
            return vm;
        }

        private static WhyVm InvoiceOwed(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            long total = 0;
            var rows = InvoiceRules.Of(ctx.Sim, b, true);
            foreach (Invoice i in rows) total += i.Amount;
            WhyVm vm = New(ctx, "why.invoiceOwed.title", Money(ctx, total), "name", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, b)));
            foreach (Invoice i in rows)
                Row(vm, ctx, "why.invoiceOwed.row", Money(ctx, i.Amount), "who", i.Buyer == 0 ? ShopWords.Rival(ctx, Rival(ctx, i.BuyerRival)) : BusinessWords.Name(ctx, BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(i.Buyer))),
                    "kg", ctx.Fmt.Kg(i.Grams), "good", InvoiceGood(ctx, i), "stage", StageWord(ctx, i));
            vm.Footnote = ctx.Strings.Get("why.invoiceOwed.rule");
            return vm;
        }

        private static RivalDef Rival(ViewContext ctx, string id)
        {
            foreach (RivalDef r in ctx.Sim.Config.Game.Shops.Rivals) if (r.Id == id) return r;
            return null;
        }

        private static WhyVm InvoiceLineWhy(ViewContext ctx, long id)
        {
            Invoice i = InvoiceOf(ctx, id);
            if (i == null) return New(ctx, "why.invoiceLine.title", "", "who", "", "kg", "", "good", "");
            Simulation sim = ctx.Sim;
            string who = i.Buyer == 0 ? ShopWords.Rival(ctx, Rival(ctx, i.BuyerRival)) : BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, sim.State.Businesses.Find(i.Buyer)));
            WhyVm vm = New(ctx, "why.invoiceLine.title", Money(ctx, i.Amount), "who", who, "kg", ctx.Fmt.Kg(i.Grams), "good", InvoiceGood(ctx, i));
            Row(vm, ctx, "why.invoiceLine.amount", Money(ctx, i.Amount));
            long left = i.DueAt - ctx.Now;
            Row(vm, ctx, "why.invoiceLine.due", left >= 0 ? ctx.Strings.Get("invoices.due", "when", ctx.Fmt.Duration(left)) : ctx.Strings.Get("invoices.dueLate", "when", ctx.Fmt.Duration(-left)));
            if (i.Buyer != 0)
            {
                Business buyer = sim.State.Businesses.Find(i.Buyer);
                Row(vm, ctx, "why.invoiceLine.buyerCash", Money(ctx, BusinessBooks.CanSpend(sim, buyer)));
            }
            Text(vm, ctx, i.Buyer == 0 ? "why.invoiceLine.shop" : "why.invoiceLine." + i.Stage.ToString().ToLowerInvariant());
            return vm;
        }

        private static WhyVm InvoiceStop(ViewContext ctx, long id)
        {
            Business b = BusinessOf(ctx, id);
            Invoice over = null;
            foreach (Invoice i in InvoiceRules.Of(ctx.Sim, b, false)) if (i.Stage == InvoiceStage.Overdue) { over = i; break; }
            Business seller = over == null ? null : ctx.Sim.State.Businesses.Find(over.Seller);
            WhyVm vm = New(ctx, "why.invoiceStop.title", over == null ? "" : Money(ctx, over.Amount), "seller", seller == null ? "" : BusinessWords.InSentence(ctx, BusinessRules.TypeOf(ctx.Sim, seller)));
            Text(vm, ctx, "why.invoiceStop.rule");
            vm.Footnote = ctx.Strings.Get("why.invoiceStop.fix");
            return vm;
        }
    }
}
