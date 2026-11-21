using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>
    /// Town life as read at each close (spec 026 §4, save schema 22): approval, the five reputation scores with their parts, crime and inequality. A town that
    /// has not read it yet hashes as before, so the golden replays and older towns are unchanged.
    /// </summary>
    public sealed class TownLifeState
    {
        /// <summary>True once a close has read the town (an older save loads unread: approval 50, nothing shown until its next close).</summary>
        public bool Read { get; internal set; }

        public Fixed Approval { get; internal set; }

        /// <summary>The five scores (0–100), in <see cref="Definitions.TownLifeDef"/> order, and each one's three parts.</summary>
        public Fixed[] Reputation { get; internal set; } = new Fixed[Definitions.TownLifeDef.Scores];
        public Fixed[] Parts { get; internal set; } = new Fixed[Definitions.TownLifeDef.Scores * Definitions.TownLifeDef.PartsEach];

        /// <summary>Crime (0–100) and its inputs: the working-age adults of your households without a town job, in tenths of a percent.</summary>
        public Fixed Crime { get; internal set; }
        public int UnemployedPermille { get; internal set; }

        /// <summary>Inequality (§3.6): the ratio (richest tenth ÷ poorest tenth, capped), its score, and whether the town had enough households to read it.</summary>
        public Fixed Ratio { get; internal set; }
        public Fixed Score { get; internal set; }
        public bool RatioKnown { get; internal set; }

        /// <summary>The median and the poorest tenth's top income a person, to tell who feels it (§3.6).</summary>
        public long MedianIncome { get; internal set; }
        public long PoorestTop { get; internal set; }

        /// <summary>What the richest and the poorest tenth earn a person in a season, on average (the ratio's two sides).</summary>
        public long RichestMean { get; internal set; }
        public long PoorestMean { get; internal set; }

        /// <summary>Closes read so far (the petitions' calendar), approval at each of the last closes, and the petitions (open or promised, then the last few ended).</summary>
        public int Closes { get; internal set; }
        public List<int> ApprovalHistory { get; internal set; } = new List<int>();
        public List<TownPetition> Petitions { get; internal set; } = new List<TownPetition>();
        public int NextPetitionId { get; internal set; } = 1;

        /// <summary>Per topic, the last close a petition can't be raised in (§3.2): after one ends, or is declined.</summary>
        public int[] QuietUntil { get; internal set; } = new int[Definitions.PetitionDef.Topics];

        /// <summary>
        /// The season counter (time ÷ a season) a festival was last scheduled in (−1: none), and the one scheduled now: its kind, venue, payer, the day (a day count
        /// from the start of play), what it cost and the people it was for (spec 026 §3.1).
        /// </summary>
        public long FestivalSeason { get; internal set; } = -1;
        public bool FestivalOn { get; internal set; }
        public int FestivalKind { get; internal set; }
        public int FestivalVenue { get; internal set; }
        public int FestivalPayer { get; internal set; }
        public long FestivalDay { get; internal set; }
        public long FestivalCost { get; internal set; }
        public int FestivalPeople { get; internal set; }
        public long FestivalJobId { get; internal set; }

        /// <summary>The festivals held, newest last, the last few kept.</summary>
        public List<FestivalRecord> Festivals { get; internal set; } = new List<FestivalRecord>();

        internal bool IsDefault { get { return !Read && FestivalSeason < 0 && !FestivalOn && Festivals.Count == 0 && Petitions.Count == 0; } } // a festival can be scheduled before the first close has read the town: that is state too

        internal TownLifeState Clone()
        {
            var c = (TownLifeState)MemberwiseClone();
            c.Reputation = (Fixed[])Reputation.Clone();
            c.Parts = (Fixed[])Parts.Clone();
            c.ApprovalHistory = new List<int>(ApprovalHistory);
            c.Petitions = new List<TownPetition>();
            foreach (TownPetition p in Petitions) c.Petitions.Add(p.Clone());
            c.QuietUntil = (int[])QuietUntil.Clone();
            c.Festivals = new List<FestivalRecord>(Festivals);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(Read);
            h.WriteFixed(Approval);
            foreach (Fixed f in Reputation) h.WriteFixed(f);
            foreach (Fixed f in Parts) h.WriteFixed(f);
            h.WriteFixed(Crime);
            h.WriteInt(UnemployedPermille);
            h.WriteFixed(Ratio);
            h.WriteFixed(Score);
            h.WriteBool(RatioKnown);
            h.WriteLong(MedianIncome);
            h.WriteLong(PoorestTop);
            h.WriteLong(RichestMean);
            h.WriteLong(PoorestMean);
            h.WriteInt(Closes);
            h.WriteInt(ApprovalHistory.Count);
            foreach (int a in ApprovalHistory) h.WriteInt(a);
            h.WriteInt(NextPetitionId);
            h.WriteInt(Petitions.Count);
            foreach (TownPetition p in Petitions) p.WriteTo(h);
            foreach (int q in QuietUntil) h.WriteInt(q);
            h.WriteLong(FestivalSeason);
            h.WriteBool(FestivalOn);
            h.WriteInt(FestivalKind);
            h.WriteInt(FestivalVenue);
            h.WriteInt(FestivalPayer);
            h.WriteLong(FestivalDay);
            h.WriteLong(FestivalCost);
            h.WriteInt(FestivalPeople);
            h.WriteLong(FestivalJobId);
            h.WriteInt(Festivals.Count);
            foreach (FestivalRecord f in Festivals) f.WriteTo(h);
        }

        internal static TownLifeState ReadFrom(SaveReader r)
        {
            var s = new TownLifeState { Read = r.ReadBool(), Approval = r.ReadFixed() };
            for (int i = 0; i < s.Reputation.Length; i++) s.Reputation[i] = r.ReadFixed();
            for (int i = 0; i < s.Parts.Length; i++) s.Parts[i] = r.ReadFixed();
            s.Crime = r.ReadFixed();
            s.UnemployedPermille = r.ReadInt();
            s.Ratio = r.ReadFixed();
            s.Score = r.ReadFixed();
            s.RatioKnown = r.ReadBool();
            s.MedianIncome = r.ReadLong();
            s.PoorestTop = r.ReadLong();
            s.RichestMean = r.ReadLong();
            s.PoorestMean = r.ReadLong();
            s.Closes = r.ReadInt();
            int n = r.ReadInt();
            for (int i = 0; i < n; i++) s.ApprovalHistory.Add(r.ReadInt());
            s.NextPetitionId = r.ReadInt();
            n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Petitions.Add(TownPetition.ReadFrom(r));
            for (int i = 0; i < s.QuietUntil.Length; i++) s.QuietUntil[i] = r.ReadInt();
            s.FestivalSeason = r.ReadLong();
            s.FestivalOn = r.ReadBool();
            s.FestivalKind = r.ReadInt();
            s.FestivalVenue = r.ReadInt();
            s.FestivalPayer = r.ReadInt();
            s.FestivalDay = r.ReadLong();
            s.FestivalCost = r.ReadLong();
            s.FestivalPeople = r.ReadInt();
            s.FestivalJobId = r.ReadLong();
            n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Festivals.Add(FestivalRecord.ReadFrom(r));
            return s;
        }
    }

    /// <summary>A festival held (spec 026 §3.1): its kind, venue, who paid, the day, the people it was for and what it cost.</summary>
    public readonly struct FestivalRecord
    {
        public int Kind { get; }
        public int Venue { get; }
        public int Payer { get; }
        public long Day { get; }
        public int People { get; }
        public long Cost { get; }

        public FestivalRecord(int kind, int venue, int payer, long day, int people, long cost) { Kind = kind; Venue = venue; Payer = payer; Day = day; People = people; Cost = cost; }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Kind);
            h.WriteInt(Venue);
            h.WriteInt(Payer);
            h.WriteLong(Day);
            h.WriteInt(People);
            h.WriteLong(Cost);
        }

        internal static FestivalRecord ReadFrom(SaveReader r) { return new FestivalRecord(r.ReadInt(), r.ReadInt(), r.ReadInt(), r.ReadLong(), r.ReadInt(), r.ReadLong()); }
    }
}
