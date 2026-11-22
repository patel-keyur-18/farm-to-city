using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Business
{
    /// <summary>
    /// A business sold (spec 028 §3.3, save schema 23): where every coin went, kept for the card that says so. Prices and shares are as they were on the day.
    /// </summary>
    public sealed class SaleRecord
    {
        public long Id { get; internal set; }
        public long At { get; internal set; }
        public long BusinessId { get; internal set; }

        /// <summary>The business type's content id.</summary>
        public string TypeId { get; internal set; }

        /// <summary>Index into <see cref="Definitions.ExitDef.Buyers"/>.</summary>
        public int Buyer { get; internal set; }

        /// <summary>The price paid, the share of its worth that was, its worth and what it held (its equity) that day.</summary>
        public long Price { get; internal set; }
        public Fixed Share { get; internal set; }
        public long Worth { get; internal set; }
        public long Held { get; internal set; }

        /// <summary>The gain over what it held, the business tax on the season's profit with it, severance paid to its people, and what came to you.</summary>
        public long Gain { get; internal set; }
        public long Tax { get; internal set; }
        public long Severance { get; internal set; }
        public long ToYou { get; internal set; }

        internal SaleRecord Clone() { return (SaleRecord)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id); h.WriteLong(At); h.WriteLong(BusinessId); h.WriteString(TypeId); h.WriteInt(Buyer); h.WriteLong(Price); h.WriteFixed(Share);
            h.WriteLong(Worth); h.WriteLong(Held); h.WriteLong(Gain); h.WriteLong(Tax); h.WriteLong(Severance); h.WriteLong(ToYou);
        }

        internal static SaleRecord ReadFrom(SaveReader r)
        {
            return new SaleRecord
            {
                Id = r.ReadLong(), At = r.ReadLong(), BusinessId = r.ReadLong(), TypeId = r.ReadString(), Buyer = r.ReadInt(), Price = r.ReadLong(), Share = r.ReadFixed(),
                Worth = r.ReadLong(), Held = r.ReadLong(), Gain = r.ReadLong(), Tax = r.ReadLong(), Severance = r.ReadLong(), ToYou = r.ReadLong(),
            };
        }
    }

    /// <summary>The Township's business exits (spec 028 §3.3–§3.4, save schema 23): the last sales. Default (and so unhashed) until a business is sold.</summary>
    public sealed class ExitState
    {
        /// <summary>How many sales are kept.</summary>
        public const int Kept = 8;

        public List<SaleRecord> Sales { get; internal set; } = new List<SaleRecord>();
        public long NextSaleId { get; internal set; } = 1;

        internal bool IsDefault { get { return Sales.Count == 0 && NextSaleId == 1; } }

        internal ExitState Clone()
        {
            var c = (ExitState)MemberwiseClone();
            c.Sales = Sales.ConvertAll(x => x.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Sales.Count);
            foreach (SaleRecord x in Sales) x.WriteTo(h);
            h.WriteLong(NextSaleId);
        }

        internal static ExitState ReadFrom(SaveReader r)
        {
            var s = new ExitState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Sales.Add(SaleRecord.ReadFrom(r));
            s.NextSaleId = r.ReadLong();
            return s;
        }
    }
}
