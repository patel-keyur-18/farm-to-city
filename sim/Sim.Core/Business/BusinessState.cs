using System.Collections.Generic;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Business
{
    /// <summary>Where a business is in its life (spec 016 §3.3, §3.10).</summary>
    public enum BusinessStatus
    {
        Building = 0,
        Open = 1,
        Closed = 2,
    }

    /// <summary>Who works at a business (spec 016 §3.6): a citizen of your town, a daily worker from a neighbouring village, or you.</summary>
    public enum WorkerKind
    {
        None = 0,
        Citizen = 1,
        Neighbour = 2,
        Founder = 3,
    }

    /// <summary>What the founder planned when founding (spec 016 §3.3): a season's projected figures, compared after 4 seasons.</summary>
    public sealed class BusinessPlan
    {
        public long Revenue { get; internal set; }
        public long Costs { get; internal set; }
        public long Profit { get; internal set; }
        public long CashNeeded { get; internal set; }
        public long MadeAt { get; internal set; }

        internal BusinessPlan Clone() { return (BusinessPlan)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Revenue);
            h.WriteLong(Costs);
            h.WriteLong(Profit);
            h.WriteLong(CashNeeded);
            h.WriteLong(MadeAt);
        }

        internal static BusinessPlan ReadFrom(SaveReader r)
        {
            return new BusinessPlan { Revenue = r.ReadLong(), Costs = r.ReadLong(), Profit = r.ReadLong(), CashNeeded = r.ReadLong(), MadeAt = r.ReadLong() };
        }
    }

    /// <summary>A business the founder owns (spec 016 §4): its acre, settings, worker, own books and stock.</summary>
    public sealed class Business
    {
        public long Id { get; internal set; }

        /// <summary>Index into <see cref="Definitions.BusinessDefs.Types"/>.</summary>
        public int Type { get; internal set; }
        public int TileId { get; internal set; }
        public BusinessStatus Status { get; internal set; }
        public long FoundedAt { get; internal set; }
        public long ReadyAt { get; internal set; }

        public int OpenHour { get; internal set; }
        public int CloseHour { get; internal set; }

        /// <summary>The service fee a kg at year-0 prices (§3.5b).</summary>
        public Fixed Fee { get; internal set; }

        /// <summary>The founder's own input taken a day, grams, and whether it may buy more at the market (§3.5c).</summary>
        public long OwnPerDay { get; internal set; }
        public bool BuyFromMarket { get; internal set; }

        public WorkerKind Worker { get; internal set; }

        /// <summary>The citizen working here when <see cref="Worker"/> is Citizen, else 0.</summary>
        public long WorkerCitizen { get; internal set; }

        /// <summary>The business's own books (spec 016 §3.2).</summary>
        public Ledger Ledger { get; internal set; }

        /// <summary>Stock by good (grams) and its cost (1/100 coin), indexed like the goods.</summary>
        public long[] Stock { get; internal set; }
        public long[] StockCost { get; internal set; }

        /// <summary>Today's day index (t ÷ day) and what came today: the founder's input, the area's grain ground (grams).</summary>
        public long Day { get; internal set; }
        public long OwnToday { get; internal set; }
        public long AreaToday { get; internal set; }

        /// <summary>The bank's business loan's id, or 0 (spec 016 §3.8, US-117).</summary>
        public long LoanId { get; internal set; }

        public BusinessPlan Plan { get; internal set; } = new BusinessPlan();

        internal Business Clone()
        {
            var c = (Business)MemberwiseClone();
            c.Ledger = Ledger.Clone();
            c.Stock = (long[])Stock.Clone();
            c.StockCost = (long[])StockCost.Clone();
            c.Plan = Plan.Clone();
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt(Type);
            h.WriteInt(TileId);
            h.WriteInt((int)Status);
            h.WriteLong(FoundedAt);
            h.WriteLong(ReadyAt);
            h.WriteInt(OpenHour);
            h.WriteInt(CloseHour);
            h.WriteFixed(Fee);
            h.WriteLong(OwnPerDay);
            h.WriteBool(BuyFromMarket);
            h.WriteInt((int)Worker);
            h.WriteLong(WorkerCitizen);
            Ledger.WriteTo(h);
            h.WriteInt(Stock.Length);
            for (int i = 0; i < Stock.Length; i++)
            {
                h.WriteLong(Stock[i]);
                h.WriteLong(StockCost[i]);
            }
            h.WriteLong(Day);
            h.WriteLong(OwnToday);
            h.WriteLong(AreaToday);
            h.WriteLong(LoanId);
            Plan.WriteTo(h);
        }

        internal static Business ReadFrom(SaveReader r)
        {
            var b = new Business
            {
                Id = r.ReadLong(), Type = r.ReadInt(), TileId = r.ReadInt(), Status = (BusinessStatus)r.ReadInt(), FoundedAt = r.ReadLong(), ReadyAt = r.ReadLong(),
                OpenHour = r.ReadInt(), CloseHour = r.ReadInt(), Fee = r.ReadFixed(), OwnPerDay = r.ReadLong(), BuyFromMarket = r.ReadBool(),
                Worker = (WorkerKind)r.ReadInt(), WorkerCitizen = r.ReadLong(), Ledger = Ledger.ReadFrom(r),
            };
            int n = r.ReadCount();
            b.Stock = new long[n];
            b.StockCost = new long[n];
            for (int i = 0; i < n; i++)
            {
                b.Stock[i] = r.ReadLong();
                b.StockCost[i] = r.ReadLong();
            }
            b.Day = r.ReadLong();
            b.OwnToday = r.ReadLong();
            b.AreaToday = r.ReadLong();
            b.LoanId = r.ReadLong();
            b.Plan = BusinessPlan.ReadFrom(r);
            return b;
        }
    }

    /// <summary>The founder's businesses (spec 016 §4; save schema 10). Null when the content has no businesses.</summary>
    public sealed class BusinessesState
    {
        /// <summary>In id order, closed ones included (their books stay readable).</summary>
        public List<Business> Businesses { get; internal set; } = new List<Business>();
        public long NextId { get; internal set; } = 1;

        public Business Find(long id) { return Businesses.Find(b => b.Id == id); }

        /// <summary>The business standing on a tile (building or open), or null.</summary>
        public Business On(int tileId) { return Businesses.Find(b => b.TileId == tileId && b.Status != BusinessStatus.Closed); }

        internal BusinessesState Clone()
        {
            var c = (BusinessesState)MemberwiseClone();
            c.Businesses = Businesses.ConvertAll(b => b.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Businesses.Count);
            foreach (Business b in Businesses) b.WriteTo(h);
            h.WriteLong(NextId);
        }

        internal static BusinessesState ReadFrom(SaveReader r)
        {
            var s = new BusinessesState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Businesses.Add(Business.ReadFrom(r));
            s.NextId = r.ReadLong();
            return s;
        }
    }
}
