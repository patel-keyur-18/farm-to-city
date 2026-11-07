using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Land
{
    /// <summary>
    /// The irrigation you chose (spec 013 §3.1, ADR 0086, ADR 0087). The pump, the borewell, each channel and each drip acre are
    /// buildings on their acre (their books are the buildings'); kept here are the acres each pumped source waters, whose running
    /// costs went unpaid at the last close (that source is off until the next), and how many drip acres had the subsidy.
    /// </summary>
    public sealed class IrrigationState
    {
        /// <summary>The acres the well's pump waters, in the order chosen.</summary>
        public List<int> PumpAcres { get; } = new List<int>();

        /// <summary>The acres the borewell waters, in the order chosen.</summary>
        public List<int> BorewellAcres { get; } = new List<int>();

        /// <summary>Running costs unpaid at the last close: the source is off this season.</summary>
        public bool PumpUnpaid { get; internal set; }
        public bool BorewellUnpaid { get; internal set; }
        public bool CanalUnpaid { get; internal set; }
        public bool DripUnpaid { get; internal set; }

        /// <summary>Drip acres laid with the subsidy so far (the first few only, PMKSY).</summary>
        public int SubsidisedDripAcres { get; internal set; }

        internal IrrigationState Clone()
        {
            var copy = new IrrigationState { PumpUnpaid = PumpUnpaid, BorewellUnpaid = BorewellUnpaid, CanalUnpaid = CanalUnpaid, DripUnpaid = DripUnpaid, SubsidisedDripAcres = SubsidisedDripAcres };
            copy.PumpAcres.AddRange(PumpAcres);
            copy.BorewellAcres.AddRange(BorewellAcres);
            return copy;
        }

        internal void WriteTo(IStateWriter h)
        {
            Write(h, PumpAcres);
            Write(h, BorewellAcres);
            h.WriteBool(PumpUnpaid);
            h.WriteBool(BorewellUnpaid);
            h.WriteBool(CanalUnpaid);
            h.WriteBool(DripUnpaid);
            h.WriteInt(SubsidisedDripAcres);
        }

        private static void Write(IStateWriter h, List<int> tiles)
        {
            h.WriteInt(tiles.Count);
            foreach (int t in tiles) h.WriteInt(t);
        }

        internal static IrrigationState ReadFrom(SaveReader r)
        {
            var s = new IrrigationState();
            Read(r, s.PumpAcres);
            Read(r, s.BorewellAcres);
            s.PumpUnpaid = r.ReadBool();
            s.BorewellUnpaid = r.ReadBool();
            s.CanalUnpaid = r.ReadBool();
            s.DripUnpaid = r.ReadBool();
            s.SubsidisedDripAcres = r.ReadInt();
            if (s.SubsidisedDripAcres < 0) throw new SaveFormatException("negative subsidised drip acres");
            return s;
        }

        private static void Read(SaveReader r, List<int> tiles)
        {
            int n = r.ReadInt();
            if (n < 0 || n > 1000) throw new SaveFormatException("a list of " + n + " watered acres");
            for (int i = 0; i < n; i++)
            {
                int t = r.ReadInt();
                if (t < 0 || tiles.Contains(t)) throw new SaveFormatException("watered acre " + t);
                tiles.Add(t);
            }
        }
    }
}
