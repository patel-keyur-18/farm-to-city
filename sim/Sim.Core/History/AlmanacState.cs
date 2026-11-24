using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.History
{
    /// <summary>What the year's best return was (spec 029 §3.4).</summary>
    public enum AlmanacBest { None = 0, Business = 1, Crop = 2 }

    /// <summary>The closed list of one-off losses the costliest loss is chosen from (§3.4). Wages, seed, rent and fuel are never on it.</summary>
    public enum AlmanacLoss
    {
        None = 0,
        FireRepair = 1,
        BusinessSold = 2,
        BusinessClosed = 3,
        MachineSold = 4,
        Spoiled = 5,
        Seized = 6,
        LosingSeason = 7,
    }

    /// <summary>Who the citizen spotlight is, in the fixed order of §3.5 (the arc's step comes with the stories).</summary>
    public enum AlmanacSpot { None = 0, Arc = 1, Reader = 2, Baby = 3, Worker = 4, Elder = 5 }

    /// <summary>One year's Almanac (§3.4): written at the Winter close and never changed after.</summary>
    public sealed class AlmanacEntry
    {
        /// <summary>The game year, 1-based (the Winter close's season ÷ 4 + 1, like <i>Your year</i>).</summary>
        public long Year { get; internal set; }
        public long At { get; internal set; }

        /// <summary>Your households, and the old village's head-count, at the close.</summary>
        public int Households { get; internal set; }
        public int People { get; internal set; }
        public int Village { get; internal set; }

        /// <summary>Net worth at the close and a year before (<see cref="HadBefore"/> false in the first year).</summary>
        public long NetWorth { get; internal set; }
        public long Before { get; internal set; }
        public bool HadBefore { get; internal set; }

        /// <summary>The best return: a business (its id and type) or a crop (its id), what it earned in the year and what was put into it.</summary>
        public AlmanacBest Best { get; internal set; }
        public string BestId { get; internal set; } = "";
        public long BestRef { get; internal set; }
        public long BestProfit { get; internal set; }
        public long BestPut { get; internal set; }

        public AlmanacLoss Loss { get; internal set; }
        public long LossAmount { get; internal set; }
        public string LossId { get; internal set; } = "";

        public AlmanacSpot Spot { get; internal set; }
        public long SpotCitizen { get; internal set; }

        internal AlmanacEntry Clone() { return (AlmanacEntry)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Year); h.WriteLong(At); h.WriteInt(Households); h.WriteInt(People); h.WriteInt(Village); h.WriteLong(NetWorth); h.WriteLong(Before); h.WriteBool(HadBefore);
            h.WriteInt((int)Best); h.WriteString(BestId); h.WriteLong(BestRef); h.WriteLong(BestProfit); h.WriteLong(BestPut);
            h.WriteInt((int)Loss); h.WriteLong(LossAmount); h.WriteString(LossId);
            h.WriteInt((int)Spot); h.WriteLong(SpotCitizen);
        }

        internal static AlmanacEntry ReadFrom(SaveReader r)
        {
            return new AlmanacEntry
            {
                Year = r.ReadLong(), At = r.ReadLong(), Households = r.ReadInt(), People = r.ReadInt(), Village = r.ReadInt(), NetWorth = r.ReadLong(), Before = r.ReadLong(), HadBefore = r.ReadBool(),
                Best = (AlmanacBest)r.ReadInt(), BestId = r.ReadString(), BestRef = r.ReadLong(), BestProfit = r.ReadLong(), BestPut = r.ReadLong(),
                Loss = (AlmanacLoss)r.ReadInt(), LossAmount = r.ReadLong(), LossId = r.ReadString(),
                Spot = (AlmanacSpot)r.ReadInt(), SpotCitizen = r.ReadLong(),
            };
        }
    }

    /// <summary>
    /// The Almanacs kept (the last <c>almanacKeep</c>), and the year in progress: each crop's income and its sowing cost so far, the worst
    /// one-off loss so far (kept as it happens, so nothing scans a ledger), a day's spoiled goods in progress and the first child to read.
    /// </summary>
    public sealed class AlmanacState
    {
        public List<AlmanacEntry> Entries { get; internal set; } = new List<AlmanacEntry>();

        /// <summary>By crop, this year: what its sales earned and what its sowings cost (1/100 coin).</summary>
        public long[] CropIncome { get; internal set; }
        public long[] CropCost { get; internal set; }

        /// <summary>The worst one-off loss so far this year.</summary>
        public AlmanacLoss WorstLoss { get; internal set; }
        public long WorstAmount { get; internal set; }
        public string WorstId { get; internal set; } = "";

        /// <summary>The day (+1) whose spoiled goods are being added up, and their value: a day's spoilage is one loss.</summary>
        public long SpoilDay { get; internal set; }
        public long SpoilValue { get; internal set; }

        /// <summary>The first child to become literate this year (a citizen id), or 0.</summary>
        public long Reader { get; internal set; }

        public AlmanacState(int crops)
        {
            CropIncome = new long[crops];
            CropCost = new long[crops];
        }

        /// <summary>Nothing written and nothing in progress: a town that never sowed, lost or taught anything this year.</summary>
        internal bool Unused
        {
            get
            {
                if (Entries.Count != 0 || WorstAmount != 0 || SpoilDay != 0 || SpoilValue != 0 || Reader != 0) return false;
                for (int i = 0; i < CropIncome.Length; i++) if (CropIncome[i] != 0 || CropCost[i] != 0) return false;
                return true;
            }
        }

        internal AlmanacState Clone()
        {
            return new AlmanacState(CropIncome.Length)
            {
                Entries = Entries.ConvertAll(x => x.Clone()), CropIncome = (long[])CropIncome.Clone(), CropCost = (long[])CropCost.Clone(),
                WorstLoss = WorstLoss, WorstAmount = WorstAmount, WorstId = WorstId, SpoilDay = SpoilDay, SpoilValue = SpoilValue, Reader = Reader,
            };
        }

        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && Unused) return; // hashed only once used
            h.WriteInt(Entries.Count);
            foreach (AlmanacEntry e in Entries) e.WriteTo(h);
            for (int i = 0; i < CropIncome.Length; i++) { h.WriteLong(CropIncome[i]); h.WriteLong(CropCost[i]); }
            h.WriteInt((int)WorstLoss); h.WriteLong(WorstAmount); h.WriteString(WorstId);
            h.WriteLong(SpoilDay); h.WriteLong(SpoilValue); h.WriteLong(Reader);
        }

        internal static AlmanacState ReadFrom(SaveReader r, int crops)
        {
            var s = new AlmanacState(crops);
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Entries.Add(AlmanacEntry.ReadFrom(r));
            for (int i = 0; i < crops; i++) { s.CropIncome[i] = r.ReadLong(); s.CropCost[i] = r.ReadLong(); }
            s.WorstLoss = (AlmanacLoss)r.ReadInt(); s.WorstAmount = r.ReadLong(); s.WorstId = r.ReadString();
            s.SpoilDay = r.ReadLong(); s.SpoilValue = r.ReadLong(); s.Reader = r.ReadLong();
            return s;
        }
    }
}
