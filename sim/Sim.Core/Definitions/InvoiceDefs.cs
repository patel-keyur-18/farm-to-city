using System.Collections.Generic;

namespace Sim.Core.Definitions
{
    /// <summary>One payment term (spec 028 §3.1): cash, or a number of real days after the sale.</summary>
    public sealed class InvoiceTermDef
    {
        public string Id { get; internal set; }
        public string NameKey { get; internal set; }
        public int Days { get; internal set; }

        /// <summary>The term in sim seconds; 0 is cash.</summary>
        public long Seconds { get { return Days * 86_400L; } }
    }

    /// <summary>A village shop that takes your products on terms (spec 028 §3.1): a rival by content id, and the goods it stocks from you (the village shops' morning collection, ADR 0091 §2, split between the shops that stock a good).</summary>
    public sealed class WholesaleBuyerDef
    {
        public string Rival { get; internal set; }
        public IReadOnlyList<string> Goods { get; internal set; }

        public bool Takes(string good)
        {
            foreach (string g in Goods) if (g == good) return true;
            return false;
        }
    }

    /// <summary>Invoices between businesses (spec 028 §3.1, <c>content/invoices.json</c>), from the Township's content, or null.</summary>
    public sealed class InvoicesDef
    {
        public IReadOnlyList<InvoiceTermDef> Terms { get; internal set; }

        /// <summary>Index into <see cref="Terms"/> a pair of businesses trades on until you set another.</summary>
        public int DefaultTerm { get; internal set; }

        /// <summary>An invoice more than this many terms past its due date is overdue (up to that it is late).</summary>
        public int LateTerms { get; internal set; }

        public IReadOnlyList<WholesaleBuyerDef> Buyers { get; internal set; }

        public int TermIndex(string id)
        {
            for (int i = 0; i < Terms.Count; i++) if (Terms[i].Id == id) return i;
            return -1;
        }

        public WholesaleBuyerDef BuyerOf(string rival)
        {
            foreach (WholesaleBuyerDef b in Buyers) if (b.Rival == rival) return b;
            return null;
        }
    }
}
