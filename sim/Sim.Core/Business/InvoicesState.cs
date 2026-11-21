using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Business
{
    /// <summary>How late an invoice is (spec 028 §3.1): current before it is due, late up to a term past it, overdue after.</summary>
    public enum InvoiceStage
    {
        Current = 0,
        Late = 1,
        Overdue = 2,
    }

    /// <summary>
    /// A sale on terms (spec 028 §3.1, save schema 23): one invoice a day for each seller, buyer and good (the day's sales add to it). The seller is one of your businesses; the buyer is one of your businesses or a village shop (<see cref="BuyerRival"/>).
    /// </summary>
    public sealed class Invoice
    {
        public long Id { get; internal set; }

        /// <summary>The selling business's id.</summary>
        public long Seller { get; internal set; }

        /// <summary>The buying business's id, or 0 when a village shop buys (<see cref="BuyerRival"/>).</summary>
        public long Buyer { get; internal set; }
        public string BuyerRival { get; internal set; } = "";

        /// <summary>The good's content id, the grams sold and what they are worth.</summary>
        public string Good { get; internal set; }
        public long Grams { get; internal set; }
        public long Amount { get; internal set; }

        /// <summary>When the first sale of the day was made, the term it was made on and the day it is due.</summary>
        public long IssuedAt { get; internal set; }
        public long TermSeconds { get; internal set; }
        public long DueAt { get { return IssuedAt + TermSeconds; } }

        public InvoiceStage Stage { get; internal set; }

        public bool BuyerIsBusiness { get { return Buyer != 0; } }

        internal Invoice Clone() { return (Invoice)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id); h.WriteLong(Seller); h.WriteLong(Buyer); h.WriteString(BuyerRival); h.WriteString(Good); h.WriteLong(Grams); h.WriteLong(Amount);
            h.WriteLong(IssuedAt); h.WriteLong(TermSeconds); h.WriteInt((int)Stage);
        }

        internal static Invoice ReadFrom(SaveReader r)
        {
            return new Invoice
            {
                Id = r.ReadLong(), Seller = r.ReadLong(), Buyer = r.ReadLong(), BuyerRival = r.ReadString(), Good = r.ReadString(), Grams = r.ReadLong(), Amount = r.ReadLong(),
                IssuedAt = r.ReadLong(), TermSeconds = r.ReadLong(), Stage = (InvoiceStage)r.ReadInt(),
            };
        }
    }

    /// <summary>The term a seller set for one buyer (a business or a village shop); a pair with none uses the default.</summary>
    public sealed class TermSetting
    {
        public long Seller { get; internal set; }
        public long Buyer { get; internal set; }
        public string BuyerRival { get; internal set; } = "";

        /// <summary>The term's content id.</summary>
        public string Term { get; internal set; }

        internal TermSetting Clone() { return (TermSetting)MemberwiseClone(); }
    }

    /// <summary>
    /// The invoices between businesses (spec 028 §3.1, save schema 23): the open ones in id order, the terms each seller set. Default (and so unhashed) until a sale is made on terms or a term is set.
    /// </summary>
    public sealed class InvoicesState
    {
        public List<Invoice> Open { get; internal set; } = new List<Invoice>();
        public List<TermSetting> Terms { get; internal set; } = new List<TermSetting>();
        public long NextId { get; internal set; } = 1;

        internal bool IsDefault { get { return Open.Count == 0 && Terms.Count == 0 && NextId == 1; } }

        internal InvoicesState Clone()
        {
            var c = (InvoicesState)MemberwiseClone();
            c.Open = Open.ConvertAll(i => i.Clone());
            c.Terms = Terms.ConvertAll(t => t.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Open.Count);
            foreach (Invoice i in Open) i.WriteTo(h);
            h.WriteInt(Terms.Count);
            foreach (TermSetting t in Terms) { h.WriteLong(t.Seller); h.WriteLong(t.Buyer); h.WriteString(t.BuyerRival); h.WriteString(t.Term); }
            h.WriteLong(NextId);
        }

        internal static InvoicesState ReadFrom(SaveReader r)
        {
            var s = new InvoicesState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Open.Add(Invoice.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Terms.Add(new TermSetting { Seller = r.ReadLong(), Buyer = r.ReadLong(), BuyerRival = r.ReadString(), Term = r.ReadString() });
            s.NextId = r.ReadLong();
            return s;
        }
    }
}
