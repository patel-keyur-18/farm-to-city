using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>
    /// A forward contract (spec 027 §3.7): an offer the trader made at a close, or one you accepted. One staple, a quantity in the good's units, a price fixed in coins per unit,
    /// and the season (period) it is to be delivered in. An accepted one keeps what has been delivered, the floor prices seen while it runs (for the shortfall) and what it
    /// has gained or lost against the market so far.
    /// </summary>
    public sealed class Contract
    {
        public long Id { get; internal set; }

        /// <summary>The good's id.</summary>
        public string Good { get; internal set; }
        public long Quantity { get; internal set; }

        /// <summary>The fixed price a unit, and the expected price it was set from.</summary>
        public Fixed Price { get; internal set; }
        public Fixed Expected { get; internal set; }

        /// <summary>The delivery season, as a period number.</summary>
        public long Delivery { get; internal set; }

        public long Filled { get; internal set; }

        /// <summary>The trader's floor price at each 6:00 of the delivery season, summed, and how many (the shortfall's "average price").</summary>
        public Fixed FloorSum { get; internal set; }
        public int Samples { get; internal set; }

        /// <summary>What the delivered kilos paid more (+) or less (−) than the market floor would have, in coins.</summary>
        public long Gain { get; internal set; }

        public long Left { get { return Quantity - Filled; } }

        internal Contract Clone() { return (Contract)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteString(Good);
            h.WriteLong(Quantity);
            h.WriteFixed(Price);
            h.WriteFixed(Expected);
            h.WriteLong(Delivery);
            h.WriteLong(Filled);
            h.WriteFixed(FloorSum);
            h.WriteInt(Samples);
            h.WriteLong(Gain);
        }

        internal static Contract ReadFrom(SaveReader r)
        {
            return new Contract
            {
                Id = r.ReadLong(), Good = r.ReadString(), Quantity = r.ReadLong(), Price = r.ReadFixed(), Expected = r.ReadFixed(), Delivery = r.ReadLong(),
                Filled = r.ReadLong(), FloorSum = r.ReadFixed(), Samples = r.ReadInt(), Gain = r.ReadLong(),
            };
        }
    }

    /// <summary>A settled contract, kept for the Store sheet's last few: what was delivered, the shortfall charged, and the whole against the market (delivered gain less the shortfall).</summary>
    public readonly struct ContractRecord
    {
        public string Good { get; }
        public long Quantity { get; }
        public long Filled { get; }
        public Fixed Price { get; }
        public long Delivery { get; }
        public long Shortfall { get; }
        public long Against { get; }

        public ContractRecord(string good, long quantity, long filled, Fixed price, long delivery, long shortfall, long against)
        {
            Good = good; Quantity = quantity; Filled = filled; Price = price; Delivery = delivery; Shortfall = shortfall; Against = against;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteString(Good);
            h.WriteLong(Quantity);
            h.WriteLong(Filled);
            h.WriteFixed(Price);
            h.WriteLong(Delivery);
            h.WriteLong(Shortfall);
            h.WriteLong(Against);
        }

        internal static ContractRecord ReadFrom(SaveReader r) { return new ContractRecord(r.ReadString(), r.ReadLong(), r.ReadLong(), r.ReadFixed(), r.ReadLong(), r.ReadLong(), r.ReadLong()); }
    }

    /// <summary>One good's harvest in a season, in the good's units: the offer's "top 2 crops" and "40% of last season's harvest".</summary>
    public sealed class HarvestTally
    {
        public string Good { get; }
        public long Quantity { get; internal set; }

        internal HarvestTally(string good, long quantity) { Good = good; Quantity = quantity; }
    }

    /// <summary>
    /// Forward contracts (spec 027 §3.7, save schema 22): this and last season's harvest by staple, the trader's offers, your contracts, the shortfall you could not pay yet
    /// and the last settled. A town that never harvested a staple at the Township has the default and hashes as before.
    /// </summary>
    public sealed class ContractsState
    {
        public List<HarvestTally> HarvestThis { get; internal set; } = new List<HarvestTally>();
        public List<HarvestTally> HarvestLast { get; internal set; } = new List<HarvestTally>();
        public List<Contract> Offers { get; internal set; } = new List<Contract>();
        public List<Contract> Open { get; internal set; } = new List<Contract>();
        public List<ContractRecord> History { get; internal set; } = new List<ContractRecord>();

        /// <summary>The next offer's number; accepted offers keep theirs.</summary>
        public long NextId { get; internal set; } = 1;

        /// <summary>A shortfall cash could not pay at a close, carried and paid first at the next (like unpaid tax, though not a ledger liability).</summary>
        public long Owed { get; internal set; }

        internal bool IsDefault { get { return HarvestThis.Count == 0 && HarvestLast.Count == 0 && Offers.Count == 0 && Open.Count == 0 && History.Count == 0 && NextId == 1 && Owed == 0; } }

        internal static void Add(List<HarvestTally> list, string good, long quantity)
        {
            int i = 0;
            for (; i < list.Count; i++)
            {
                int c = string.CompareOrdinal(list[i].Good, good);
                if (c == 0) { list[i].Quantity += quantity; return; }
                if (c > 0) break;
            }
            list.Insert(i, new HarvestTally(good, quantity));
        }

        public static long Of(List<HarvestTally> list, string good)
        {
            foreach (HarvestTally t in list) if (t.Good == good) return t.Quantity;
            return 0;
        }

        internal ContractsState Clone()
        {
            var c = (ContractsState)MemberwiseClone();
            c.HarvestThis = HarvestThis.ConvertAll(x => new HarvestTally(x.Good, x.Quantity));
            c.HarvestLast = HarvestLast.ConvertAll(x => new HarvestTally(x.Good, x.Quantity));
            c.Offers = Offers.ConvertAll(x => x.Clone());
            c.Open = Open.ConvertAll(x => x.Clone());
            c.History = new List<ContractRecord>(History);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            foreach (List<HarvestTally> list in new[] { HarvestThis, HarvestLast })
            {
                h.WriteInt(list.Count);
                foreach (HarvestTally t in list) { h.WriteString(t.Good); h.WriteLong(t.Quantity); }
            }
            foreach (List<Contract> list in new[] { Offers, Open })
            {
                h.WriteInt(list.Count);
                foreach (Contract c in list) c.WriteTo(h);
            }
            h.WriteInt(History.Count);
            foreach (ContractRecord x in History) x.WriteTo(h);
            h.WriteLong(NextId);
            h.WriteLong(Owed);
        }

        internal static ContractsState ReadFrom(SaveReader r)
        {
            var s = new ContractsState();
            foreach (List<HarvestTally> list in new[] { s.HarvestThis, s.HarvestLast })
            {
                int n = r.ReadCount();
                for (int i = 0; i < n; i++) list.Add(new HarvestTally(r.ReadString(), r.ReadLong()));
            }
            foreach (List<Contract> list in new[] { s.Offers, s.Open })
            {
                int n = r.ReadCount();
                for (int i = 0; i < n; i++) list.Add(Contract.ReadFrom(r));
            }
            int h = r.ReadCount();
            for (int i = 0; i < h; i++) s.History.Add(ContractRecord.ReadFrom(r));
            s.NextId = r.ReadLong();
            s.Owed = r.ReadLong();
            return s;
        }
    }
}
