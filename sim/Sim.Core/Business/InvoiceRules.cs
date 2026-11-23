using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Business
{
    /// <summary>
    /// Sales between businesses on terms (spec 028 §3.1, US-123): a sale to one of your businesses, or to a village shop, becomes an invoice on the term the seller set
    /// for that buyer: the seller books a receivable (Owed by buyers), the buyer a trade payable. At the term's end the buyer pays from its cash, then its overdraft; if
    /// neither covers it the invoice ages (late, then overdue) and a seller stops delivering to a buyer with an overdue invoice. Households, the trader, the supermarket
    /// and the farm's own sales stay cash. Between your own businesses the receivable and the payable cancel in net worth.
    /// </summary>
    public static class InvoiceRules
    {
        private const long Day = 86_400;

        public static InvoicesDef Defs(Simulation sim) { return sim.Config.Game.Invoices; }

        public static InvoicesState State(Simulation sim) { return sim.State.Township?.Invoices; }

        /// <summary>Invoices are on from the Township (and only when the content has them).</summary>
        public static bool On(Simulation sim) { return Defs(sim) != null && State(sim) != null && sim.State.Businesses != null && Town.TownshipRules.Reached(sim); }

        // ---- terms ----

        /// <summary>The term (an index into the definitions' terms) a seller has set for a buyer: a business, or a village shop by its content id.</summary>
        public static int TermOf(Simulation sim, long seller, long buyer, string rival)
        {
            InvoicesDef d = Defs(sim);
            if (d == null) return 0;
            InvoicesState s = State(sim);
            if (s != null)
                foreach (TermSetting t in s.Terms)
                    if (t.Seller == seller && t.Buyer == buyer && t.BuyerRival == (rival ?? ""))
                    {
                        int i = d.TermIndex(t.Term);
                        if (i >= 0) return i;
                    }
            return d.DefaultTerm;
        }

        internal static CommandResult SetTerms(Simulation sim, long seller, long buyer, string rival, string term)
        {
            if (!On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            InvoicesDef d = Defs(sim);
            rival = rival ?? "";
            Business s = sim.State.Businesses.Find(seller);
            if (s == null) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (s.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            if (rival.Length > 0 ? buyer != 0 || d.BuyerOf(rival) == null : sim.State.Businesses.Find(buyer) == null || buyer == seller) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (d.TermIndex(term) < 0) return CommandResult.Reject(RejectReason.InvalidArgument);
            InvoicesState st = State(sim);
            foreach (TermSetting t in st.Terms)
                if (t.Seller == seller && t.Buyer == buyer && t.BuyerRival == rival) { t.Term = term; return CommandResult.Ok; }
            st.Terms.Add(new TermSetting { Seller = seller, Buyer = buyer, BuyerRival = rival, Term = term });
            return CommandResult.Ok;
        }

        // ---- a sale ----

        /// <summary>
        /// One of your businesses sells goods to another at the trader's price (the mill's atta to the bakery, a bakery's pav to a stall): the buyer's stock grows and the seller's
        /// sale is booked, on cash or on the pair's term. The caller has already checked the buyer can pay today (its cash and overdraft, as before: ADR 0155 gap 1).
        /// </summary>
        internal static void Sale(Simulation sim, Business from, Business to, int good, long q, long value)
        {
            long now = sim.State.Clock.SimTime;
            int term = On(sim) && value > 0 ? TermOf(sim, from.Id, to.Id, null) : 0;
            if (term == 0)
            {
                BusinessBooks.Draw(sim, to, value);
                to.Ledger.Post(now, sim.Period, "ledger.boughtFromBusiness", "business:" + to.Id, new Posting(Account.BusinessStock, value), new Posting(Account.Cash, -value));
            }
            else
                to.Ledger.Post(now, sim.Period, "ledger.boughtOnTerms", "business:" + to.Id, new Posting(Account.BusinessStock, value), new Posting(Account.TradePayable, -value));
            to.Stock[good] += q;
            to.StockCost[good] += value;
            BusinessRules.Sold(sim, from, good, q, value, term == 0 ? Account.Cash : Account.OwedByBuyers);
            if (term != 0) Issue(sim, from.Id, to.Id, "", good, q, value, term);
        }

        /// <summary>True when the seller has stopped delivering to this buyer: an invoice of the pair is overdue (the stop rule).</summary>
        internal static bool Blocked(Simulation sim, Business from, Business to)
        {
            InvoicesState s = State(sim);
            if (s == null) return false;
            foreach (Invoice i in s.Open) if (i.Seller == from.Id && i.Buyer == to.Id && i.Stage == InvoiceStage.Overdue) return true;
            return false;
        }

        private static void Issue(Simulation sim, long seller, long buyer, string rival, int good, long grams, long amount, int term)
        {
            InvoicesState s = State(sim);
            long now = sim.State.Clock.SimTime, seconds = Defs(sim).Terms[term].Seconds;
            string goodId = sim.Config.Game.Market.Goods[good].Id;
            foreach (Invoice i in s.Open)
                if (i.Seller == seller && i.Buyer == buyer && i.BuyerRival == rival && i.Good == goodId && i.IssuedAt / Day == now / Day && i.TermSeconds == seconds)
                {
                    i.Grams += grams; // the day's sales to this buyer of this good are one invoice
                    i.Amount += amount;
                    return;
                }
            s.Open.Add(new Invoice { Id = s.NextId++, Seller = seller, Buyer = buyer, BuyerRival = rival, Good = goodId, Grams = grams, Amount = amount, IssuedAt = now, TermSeconds = seconds });
            sim.Emit(new InvoiceIssued(now, seller, buyer, rival, amount));
        }

        // ---- the village shops (§3.1) ----

        /// <summary>
        /// The village shops' morning collection of a product (ADR 0091 §2: what the village buys of it, at the trader's price): the grocer and the tea stall that stock the good
        /// take it between them, each on the term set for it, and never default. Before the Township, or on cash, one cash sale as before.
        /// </summary>
        internal static void VillageShops(Simulation sim, Business b, int good, long q, long amount)
        {
            if (q <= 0) return;
            var buyers = new List<WholesaleBuyerDef>();
            if (On(sim) && sim.State.Shops != null)
            {
                string goodId = sim.Config.Game.Market.Goods[good].Id;
                foreach (WholesaleBuyerDef w in Defs(sim).Buyers)
                    if (w.Takes(goodId) && sim.State.Shops.Rivals.Exists(r => r.Id == w.Rival)) buyers.Add(w);
            }
            if (buyers.Count == 0) { BusinessRules.Sold(sim, b, good, q, amount); return; }
            long left = q, leftAmount = amount;
            for (int k = 0; k < buyers.Count; k++)
            {
                long part = k == buyers.Count - 1 ? left : q / buyers.Count;
                long value = k == buyers.Count - 1 ? leftAmount : (long)((System.Numerics.BigInteger)amount * part / q);
                int term = TermOf(sim, b.Id, 0, buyers[k].Rival);
                if (part <= 0) continue;
                BusinessRules.Sold(sim, b, good, part, value, term == 0 ? Account.Cash : Account.OwedByBuyers);
                if (term != 0 && value > 0) Issue(sim, b.Id, 0, buyers[k].Rival, good, part, value, term);
                left -= part;
                leftAmount -= value;
            }
        }

        // ---- the term's end ----

        /// <summary>Every hour: an invoice at its due time is paid if the buyer can; otherwise it ages, and going overdue stops the seller's deliveries.</summary>
        internal static void Hourly(Simulation sim)
        {
            InvoicesState s = State(sim);
            if (s == null || s.Open.Count == 0) return;
            long now = sim.State.Clock.SimTime;
            InvoicesDef d = Defs(sim);
            foreach (Invoice i in new List<Invoice>(s.Open))
            {
                if (now < i.DueAt) continue;
                if (Pay(sim, i)) { s.Open.Remove(i); continue; }
                InvoiceStage was = i.Stage;
                i.Stage = now > i.DueAt + d.LateTerms * i.TermSeconds ? InvoiceStage.Overdue : InvoiceStage.Late;
                if (i.Stage == InvoiceStage.Overdue && was != InvoiceStage.Overdue)
                {
                    sim.Emit(new InvoiceOverdue(now, i.Seller, i.Buyer, i.Amount));
                    sim.Emit(new SupplyStopped(now, i.Seller, i.Buyer));
                }
            }
        }

        /// <summary>The buyer pays: from its cash, then its overdraft; a village shop always pays. False when a business buyer can't.</summary>
        private static bool Pay(Simulation sim, Invoice i)
        {
            long now = sim.State.Clock.SimTime;
            Business seller = sim.State.Businesses.Find(i.Seller);
            if (i.BuyerIsBusiness)
            {
                Business buyer = sim.State.Businesses.Find(i.Buyer);
                if (buyer == null || buyer.Status == BusinessStatus.Closed || BusinessBooks.CanSpend(sim, buyer) < i.Amount) return false;
                BusinessBooks.Draw(sim, buyer, i.Amount);
                buyer.Ledger.Post(now, sim.Period, "ledger.invoicePaid", "business:" + buyer.Id, new Posting(Account.TradePayable, i.Amount), new Posting(Account.Cash, -i.Amount));
            }
            seller.Ledger.Post(now, sim.Period, "ledger.invoiceCollected", "business:" + seller.Id, new Posting(Account.Cash, i.Amount), new Posting(Account.OwedByBuyers, -i.Amount));
            BusinessBooks.Sweep(sim, seller);
            sim.Emit(new InvoicePaid(now, i.Seller, i.Buyer, i.BuyerRival, i.Amount));
            return true;
        }

        // ---- a business closing ----

        /// <summary>
        /// A business closing collects what its customers owe it now: a village shop pays at once, a business of yours pays if it can; what it can't pay is forgiven between the
        /// two (a loss for the closing seller, a gain for the buyer: both yours, so your net worth does not move).
        /// </summary>
        internal static void CollectAll(Simulation sim, Business b)
        {
            InvoicesState s = State(sim);
            if (s == null) return;
            long now = sim.State.Clock.SimTime;
            foreach (Invoice i in new List<Invoice>(s.Open))
            {
                if (i.Seller != b.Id) continue;
                if (!Pay(sim, i))
                {
                    b.Ledger.Post(now, sim.Period, "ledger.invoiceForgiven", "business:" + b.Id, new Posting(Account.LossOnAssetSales, i.Amount), new Posting(Account.OwedByBuyers, -i.Amount));
                    Business buyer = sim.State.Businesses.Find(i.Buyer);
                    if (buyer != null && buyer.Status != BusinessStatus.Closed)
                        buyer.Ledger.Post(now, sim.Period, "ledger.invoiceForgiven", "business:" + buyer.Id, new Posting(Account.TradePayable, i.Amount), new Posting(Account.GainOnAssetSales, -i.Amount));
                }
                s.Open.Remove(i);
            }
        }

        /// <summary>What a business owes other businesses on terms.</summary>
        public static long Payables(Business b) { return -b.Ledger.Balance(Account.TradePayable); }

        /// <summary>A business closing pays what it owes its suppliers from its cash (the caller has made sure it has it).</summary>
        internal static void PayAll(Simulation sim, Business b)
        {
            InvoicesState s = State(sim);
            if (s == null) return;
            foreach (Invoice i in new List<Invoice>(s.Open))
            {
                if (i.Buyer != b.Id) continue;
                if (Pay(sim, i)) s.Open.Remove(i);
            }
        }

        // ---- what a sheet shows ----

        /// <summary>The open invoices a business is owed (as seller) or owes (as buyer), in id order.</summary>
        public static List<Invoice> Of(Simulation sim, Business b, bool asSeller)
        {
            var list = new List<Invoice>();
            InvoicesState s = State(sim);
            if (s == null) return list;
            foreach (Invoice i in s.Open) if (asSeller ? i.Seller == b.Id : i.Buyer == b.Id) list.Add(i);
            return list;
        }
    }
}
