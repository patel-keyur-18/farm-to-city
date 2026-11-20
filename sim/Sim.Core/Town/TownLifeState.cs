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

        internal bool IsDefault { get { return !Read; } }

        internal TownLifeState Clone()
        {
            var c = (TownLifeState)MemberwiseClone();
            c.Reputation = (Fixed[])Reputation.Clone();
            c.Parts = (Fixed[])Parts.Clone();
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
            return s;
        }
    }
}
