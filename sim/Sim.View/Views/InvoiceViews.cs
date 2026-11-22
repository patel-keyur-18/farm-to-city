using System.Collections.Generic;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;

namespace Sim.View.Views
{
    /// <summary>
    /// The Orders tab of a business sheet (spec 028 §3.1, US-123, Preview 16): who buys from it and on what terms, what is owed to it, what it owes, and the stop rule's
    /// warning. From the Township only; a business with nothing traded says so.
    /// </summary>
    public static class InvoiceSheet
    {
        public const string Tab = "invoices";

        /// <summary>The tab shows from the Township, for an open business or one with invoices.</summary>
        public static bool Applies(ViewContext ctx, Business b) { return InvoiceRules.On(ctx.Sim) && !BusinessRules.TypeOf(ctx.Sim, b).IsTuition && (b.Status != BusinessStatus.Closed || InvoiceRules.Of(ctx.Sim, b, true).Count + InvoiceRules.Of(ctx.Sim, b, false).Count > 0); }

        private static string BuyerName(ViewContext ctx, Invoice i, bool seller)
        {
            Simulation sim = ctx.Sim;
            if (seller)
            {
                if (i.Buyer == 0) return ShopWords.Rival(ctx, RivalDefOf(sim, i.BuyerRival));
                Business x = sim.State.Businesses.Find(i.Buyer);
                return BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, x));
            }
            Business from = sim.State.Businesses.Find(i.Seller);
            return BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, from));
        }

        private static RivalDef RivalDefOf(Simulation sim, string id)
        {
            foreach (RivalDef r in sim.Config.Game.Shops.Rivals) if (r.Id == id) return r;
            return null;
        }

        /// <summary>Whether a type makes a good: by a recipe, or as a mill's output.</summary>
        private static bool Produces(BusinessTypeDef t, int good)
        {
            if (t.Makes(good)) return true;
            foreach (BusinessOutput o in t.Outputs) if (o.Good == good) return true;
            return false;
        }

        private static int GoodIndex(ViewContext ctx, string id) { return ctx.Sim.Config.Game.Market.IndexOf(id); }

        /// <summary>Whether <paramref name="to"/> uses a good that <paramref name="t"/> makes (as an input, or a product it stocks).</summary>
        private static string TakesFrom(ViewContext ctx, BusinessTypeDef from, Business to)
        {
            BusinessTypeDef t = BusinessRules.TypeOf(ctx.Sim, to);
            var names = new List<string>();
            var seen = new HashSet<int>();
            void Add(int g) { if (g >= 0 && Produces(from, g) && seen.Add(g)) names.Add(BusinessWords.Good(ctx, g)); }
            foreach (int g in BusinessRules.BoughtInputs(t)) Add(g);
            foreach (int g in t.Goods) Add(g);
            return string.Join(", ", names);
        }

        public static void AddOrders(ViewContext ctx, SheetVm vm, Business b, BusinessTypeDef t)
        {
            Simulation sim = ctx.Sim;
            var s = ctx.Strings;
            InvoicesDef d = InvoiceRules.Defs(sim);
            string name = BusinessWords.InSentence(ctx, t);
            bool any = false;

            // Who buys from it, and on what terms.
            var buyers = new List<LineVm>();
            foreach (Business to in sim.State.Businesses.Businesses)
            {
                if (to == b || to.Status != BusinessStatus.Open) continue;
                string goods = TakesFrom(ctx, t, to);
                if (goods.Length == 0) continue;
                buyers.Add(TermsLine(ctx, b, to.Id, "", BusinessWords.Name(ctx, BusinessRules.TypeOf(sim, to)), goods, to.Id));
            }
            if (sim.State.Shops != null)
                foreach (WholesaleBuyerDef w in d.Buyers)
                {
                    var goods = new List<string>();
                    foreach (string g in w.Goods) { int gi = GoodIndex(ctx, g); if (gi >= 0 && Produces(t, gi)) goods.Add(BusinessWords.Good(ctx, gi)); }
                    if (goods.Count == 0 || !sim.State.Shops.Rivals.Exists(r => r.Id == w.Rival)) continue;
                    buyers.Add(TermsLine(ctx, b, 0, w.Rival, ShopWords.Rival(ctx, RivalDefOf(sim, w.Rival)), string.Join(", ", goods), 0));
                }
            if (buyers.Count > 0)
            {
                any = true;
                vm.Lines.Add(LineVm.Header(s.Get("invoices.buyers", "name", name)));
                vm.Lines.AddRange(buyers);
            }

            // What is owed to it.
            List<Invoice> owed = InvoiceRules.Of(sim, b, true), owes = InvoiceRules.Of(sim, b, false);
            if (owed.Count > 0)
            {
                any = true;
                long total = 0;
                foreach (Invoice i in owed) total += i.Amount;
                vm.Lines.Add(LineVm.Header(s.Get("invoices.owed", "name", name)));
                vm.Lines.Add(new LineVm(s.Get("invoices.owedTotal", "n", ctx.Fmt.Integer(owed.Count)), ctx.Fmt.Coins(total), "invoiceOwed", b.Id) { Icon = "coin" });
                foreach (Invoice i in owed) vm.Lines.Add(InvoiceLine(ctx, i, true));
            }
            if (owes.Count > 0)
            {
                any = true;
                vm.Lines.Add(LineVm.Header(s.Get("invoices.owes", "name", name)));
                foreach (Invoice i in owes) vm.Lines.Add(InvoiceLine(ctx, i, false));
                foreach (Invoice i in owes)
                    if (i.Stage == InvoiceStage.Overdue)
                    {
                        Business seller = sim.State.Businesses.Find(i.Seller);
                        vm.Lines.Insert(0, new LineVm(s.Get("invoices.stopped", "seller", BusinessWords.InSentence(ctx, BusinessRules.TypeOf(sim, seller)), "good", BusinessWords.Good(ctx, GoodIndex(ctx, i.Good)),
                            "amount", ctx.Fmt.Coins(i.Amount)), s.Get("invoices.stoppedNote", "name", name, "cash", ctx.Fmt.Coins(b.Ledger.Cash)), "invoiceStop", b.Id)
                        {
                            Icon = "coin", NoteTone = Tone.Warn,
                            Action = ActionVm.Open("putCash", s.Get("business.putCash"), new SheetRequest("BusinessCash", b.Id, BusinessCashSheet.Put)),
                        });
                        break;
                    }
            }
            if (!any) vm.Lines.Add(new LineVm(s.Get("invoices.none"), "", "invoiceTerms", b.Id));
        }

        private static LineVm TermsLine(ViewContext ctx, Business seller, long buyer, string rival, string name, string goods, long buyerId)
        {
            InvoicesDef d = InvoiceRules.Defs(ctx.Sim);
            int term = InvoiceRules.TermOf(ctx.Sim, seller.Id, buyer, rival);
            string next = d.Terms[(term + 1) % d.Terms.Count].Id;
            long sid = seller.Id;
            return new LineVm(name, ctx.Strings.Get("invoices.buys", "goods", goods), "invoiceTerms", seller.Id)
            {
                Icon = rival.Length > 0 ? ShopWords.RivalIcon(ctx, ctx.Sim.State.Shops.Rivals.Find(r => r.Id == rival)) : BusinessRules.TypeOf(ctx.Sim, ctx.Sim.State.Businesses.Find(buyerId)).Id,
                Action = ActionVm.Checked(ctx, "terms:" + buyer + ":" + rival, ctx.Strings.Get(d.Terms[term].NameKey), null, ActionKind.Secondary, at => new SetTerms(at, sid, buyer, rival, next)),
            };
        }

        private static LineVm InvoiceLine(ViewContext ctx, Invoice i, bool seller)
        {
            var s = ctx.Strings;
            long left = i.DueAt - ctx.Now;
            string when = i.Stage == InvoiceStage.Current ? s.Get("invoices.due", "when", ctx.Fmt.Duration(left)) : s.Get("invoices.dueLate", "when", ctx.Fmt.Duration(-left));
            string stage = s.Get("invoices.stage." + i.Stage.ToString().ToLowerInvariant());
            var l = new LineVm(s.Get("invoices.invoice", "who", BuyerName(ctx, i, seller), "kg", ctx.Fmt.Kg(i.Grams), "good", BusinessWords.Good(ctx, GoodIndex(ctx, i.Good))), ctx.Fmt.Coins(i.Amount), "invoiceLine", i.Id)
            {
                Icon = i.Good, Note = stage + " · " + when, NoteTone = i.Stage == InvoiceStage.Current ? Tone.Plain : Tone.Warn,
            };
            return l;
        }
    }
}
