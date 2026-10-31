using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Finance
{
    /// <summary>Cash paid upfront for a period of time, expensed day by day (spec 005 §3.2).</summary>
    public sealed class Prepayment
    {
        public long Id { get; internal set; }
        public Account ExpenseAccount { get; internal set; }
        public Account PrepaidAccount { get; internal set; }
        public long Total { get; internal set; }
        public long Start { get; internal set; }
        public long End { get; internal set; }
        public long Expensed { get; internal set; }

        /// <summary>Who it's for, e.g. <c>founder:living</c>.</summary>
        public string OwnerRef { get; internal set; }

        internal Prepayment Clone()
        {
            return new Prepayment
            {
                Id = Id, ExpenseAccount = ExpenseAccount, PrepaidAccount = PrepaidAccount, Total = Total,
                Start = Start, End = End, Expensed = Expensed, OwnerRef = OwnerRef,
            };
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt((int)ExpenseAccount);
            h.WriteInt((int)PrepaidAccount);
            h.WriteLong(Total);
            h.WriteLong(Start);
            h.WriteLong(End);
            h.WriteLong(Expensed);
            h.WriteString(OwnerRef);
        }

        internal static Prepayment ReadFrom(SaveReader r)
        {
            return new Prepayment
            {
                Id = r.ReadLong(), ExpenseAccount = (Account)r.ReadInt(), PrepaidAccount = (Account)r.ReadInt(), Total = r.ReadLong(),
                Start = r.ReadLong(), End = r.ReadLong(), Expensed = r.ReadLong(), OwnerRef = r.ReadString(),
            };
        }
    }

    /// <summary>Where a good was sold (spec 019 §3.9, ADR 0083 §9). Append only: the value is saved.</summary>
    public enum SoldTo
    {
        Trader = 0,
        Mandi = 1,
        Market = 2,
        Orders = 3,
        Business = 4,

        /// <summary>Green fodder sold standing to neighbours (spec 012 §3.7).</summary>
        Neighbours = 5,

        /// <summary>Produce seized and sold at a foreclosure (ADR 0038).</summary>
        Lender = 6,
    }

    /// <summary>One good's sales to one buyer in a season, at its gross value (before the mandi's commission).</summary>
    public sealed class Sale
    {
        public string Good { get; }
        public SoldTo To { get; }
        public long Amount { get; internal set; }

        internal Sale(string good, SoldTo to, long amount)
        {
            Good = good;
            To = to;
            Amount = amount;
        }
    }

    /// <summary>
    /// This season's and last season's sales by good and by where sold (spec 019 §3.9, save schema 12): the ledger posts every crop
    /// to one account, so *Your season* reads each good from here. Kept in good id then buyer order.
    /// </summary>
    public sealed class SalesRecord
    {
        public List<Sale> This { get; } = new List<Sale>();
        public List<Sale> Last { get; } = new List<Sale>();

        internal void Add(Simulation sim, int good, SoldTo to, long amount) { Add(sim.Config.Game.Market.Goods[good].Id, to, amount); }

        internal void Add(string good, SoldTo to, long amount)
        {
            if (amount == 0) return;
            int i = 0;
            for (; i < This.Count; i++)
            {
                int c = string.CompareOrdinal(This[i].Good, good);
                if (c == 0 && This[i].To == to) { This[i].Amount += amount; return; }
                if (c > 0 || (c == 0 && This[i].To > to)) break;
            }
            This.Insert(i, new Sale(good, to, amount));
        }

        /// <summary>At the close: this season's becomes last season's.</summary>
        internal void Roll()
        {
            Last.Clear();
            Last.AddRange(This);
            This.Clear();
        }

        internal SalesRecord Clone()
        {
            var c = new SalesRecord();
            foreach (Sale x in This) c.This.Add(new Sale(x.Good, x.To, x.Amount));
            foreach (Sale x in Last) c.Last.Add(new Sale(x.Good, x.To, x.Amount));
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            foreach (List<Sale> list in new[] { This, Last })
            {
                h.WriteInt(list.Count);
                foreach (Sale x in list)
                {
                    h.WriteString(x.Good);
                    h.WriteInt((int)x.To);
                    h.WriteLong(x.Amount);
                }
            }
        }

        internal void ReadFrom(SaveReader r)
        {
            foreach (List<Sale> list in new[] { This, Last })
            {
                int n = r.ReadCount();
                for (int i = 0; i < n; i++) list.Add(new Sale(r.ReadString(), (SoldTo)r.ReadInt(), r.ReadLong()));
            }
        }
    }

    /// <summary>The finance state beside the ledger (spec 005 §4): prepayments and season reports.</summary>
    public sealed class FinanceState
    {
        /// <summary>Open prepayments in id order.</summary>
        public List<Prepayment> Prepayments { get; } = new List<Prepayment>();

        public long NextPrepaymentId { get; internal set; } = 1;

        /// <summary>One report per closed season, oldest first. Reports are immutable.</summary>
        public List<SeasonReport> Reports { get; } = new List<SeasonReport>();

        /// <summary>Cash at the start of the current season (after the opening entry at a new game).</summary>
        public long SeasonOpeningCash { get; internal set; }

        /// <summary>The ledger's first entry of this season and of the last (0 before the first close): *Your season*'s entries (spec 019 §3.9).</summary>
        public long SeasonStartEntry { get; internal set; }
        public long LastSeasonStartEntry { get; internal set; }

        /// <summary>Sales by good and where sold, this season and last (spec 019 §3.9).</summary>
        public SalesRecord Sales { get; private set; } = new SalesRecord();

        public Prepayment FindPrepayment(long id) { return Prepayments.Find(p => p.Id == id); }

        internal FinanceState Clone()
        {
            var c = new FinanceState
            {
                NextPrepaymentId = NextPrepaymentId, SeasonOpeningCash = SeasonOpeningCash, SeasonStartEntry = SeasonStartEntry,
                LastSeasonStartEntry = LastSeasonStartEntry, Sales = Sales.Clone(),
            };
            foreach (Prepayment p in Prepayments) c.Prepayments.Add(p.Clone());
            c.Reports.AddRange(Reports);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(NextPrepaymentId);
            h.WriteLong(SeasonOpeningCash);
            h.WriteInt(Prepayments.Count);
            foreach (Prepayment p in Prepayments) p.WriteTo(h);
            h.WriteInt(Reports.Count);
            foreach (SeasonReport r in Reports) r.WriteTo(h);
            if (!(h is SaveWriter sw) || sw.Schema >= 12) // spec 019 §3.9, schema 12
            {
                h.WriteLong(SeasonStartEntry);
                h.WriteLong(LastSeasonStartEntry);
                Sales.WriteTo(h);
            }
        }

        internal static FinanceState ReadFrom(SaveReader r)
        {
            var s = new FinanceState { NextPrepaymentId = r.ReadLong(), SeasonOpeningCash = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Prepayments.Add(Prepayment.ReadFrom(r));
            n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Reports.Add(SeasonReport.ReadFrom(r));
            if (r.Schema >= 12)
            {
                s.SeasonStartEntry = r.ReadLong();
                s.LastSeasonStartEntry = r.ReadLong();
                s.Sales.ReadFrom(r);
            } // schema 11: FinanceSystem.MigrateSeasons after loading
            return s;
        }
    }
}
