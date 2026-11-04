using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Equipment
{
    /// <summary>A machine of yours, bought or leased (spec 013 §3.2).</summary>
    public sealed class Machine
    {
        public long Id { get; internal set; }

        /// <summary>Index into <c>EquipmentDefs.Machines</c>.</summary>
        public int Kind { get; internal set; }
        public bool Leased { get; internal set; }
        public long Since { get; internal set; }

        /// <summary>Money: what it cost (bought), its depreciation so far and the seasons depreciated; book = cost − depreciated.</summary>
        public long Cost { get; internal set; }
        public long Depreciated { get; internal set; }
        public int SeasonsDepreciated { get; internal set; }

        /// <summary>The player's choice to pay maintenance at each close (on by default), and whether this season's was paid.</summary>
        public bool Maintain { get; internal set; } = true;
        public bool Maintained { get; internal set; } = true;

        /// <summary>Broken until this sim time (0 working); a repair not yet paid keeps it broken until it is.</summary>
        public long BrokenUntil { get; internal set; }
        public long RepairDue { get; internal set; }

        /// <summary>This season's diesel (or a lease's rent) couldn't be paid: it stands idle until the next close.</summary>
        public bool Unpaid { get; internal set; }

        /// <summary>The bank loan secured on it, or 0.</summary>
        public long LoanId { get; internal set; }

        internal Machine Clone() { return (Machine)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteRef(ContentTable.Machine, Kind);
            h.WriteBool(Leased);
            h.WriteLong(Since);
            h.WriteLong(Cost);
            h.WriteLong(Depreciated);
            h.WriteInt(SeasonsDepreciated);
            h.WriteBool(Maintain);
            h.WriteBool(Maintained);
            h.WriteLong(BrokenUntil);
            h.WriteLong(RepairDue);
            h.WriteBool(Unpaid);
            h.WriteLong(LoanId);
        }

        internal static Machine ReadFrom(SaveReader r)
        {
            var m = new Machine
            {
                Id = r.ReadLong(), Kind = r.ReadRef(ContentTable.Machine), Leased = r.ReadBool(), Since = r.ReadLong(), Cost = r.ReadLong(), Depreciated = r.ReadLong(),
                SeasonsDepreciated = r.ReadInt(), Maintain = r.ReadBool(), Maintained = r.ReadBool(), BrokenUntil = r.ReadLong(), RepairDue = r.ReadLong(),
                Unpaid = r.ReadBool(), LoanId = r.ReadLong(),
            };
            if (m.Cost < 0 || m.Depreciated < 0 || m.Depreciated > m.Cost || m.RepairDue < 0) throw new SaveContentException("machine " + m.Id);
            return m;
        }
    }

    /// <summary>
    /// The equipment ladder's state (spec 013 §3.2): your machines, the machines hired for the day, and the Tools factor each growing
    /// crop was sown under (ADR 0089). Save schema 14's section after irrigation.
    /// </summary>
    public sealed class EquipmentState
    {
        /// <summary>In id order; at most one of each kind.</summary>
        public List<Machine> Machines { get; internal set; } = new List<Machine>();
        public long NextMachineId { get; internal set; } = 1;

        /// <summary>Per machine kind, hired until this sim time (midnight of the day it was hired), or 0.</summary>
        public long[] HiredUntil { get; internal set; } = new long[0];

        /// <summary>Crops sown under a machine, by crop id, with the Tools factor they were sown at (hand tools aren't listed).</summary>
        public List<KeyValuePair<long, Fixed>> SownTools { get; internal set; } = new List<KeyValuePair<long, Fixed>>();

        internal EquipmentState Clone()
        {
            var c = (EquipmentState)MemberwiseClone();
            c.Machines = Machines.ConvertAll(m => m.Clone());
            c.HiredUntil = (long[])HiredUntil.Clone();
            c.SownTools = new List<KeyValuePair<long, Fixed>>(SownTools);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Machines.Count);
            foreach (Machine m in Machines) m.WriteTo(h);
            h.WriteLong(NextMachineId);
            h.WriteInt(HiredUntil.Length);
            foreach (long t in HiredUntil) h.WriteLong(t);
            h.WriteInt(SownTools.Count);
            foreach (KeyValuePair<long, Fixed> k in SownTools) { h.WriteLong(k.Key); h.WriteFixed(k.Value); }
        }

        internal static EquipmentState ReadFrom(SaveReader r, int kinds)
        {
            var s = new EquipmentState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Machines.Add(Machine.ReadFrom(r));
            s.NextMachineId = r.ReadLong();
            int h = r.ReadCount();
            if (h != kinds) throw new SaveContentException("hired days for " + h + " machines, content has " + kinds);
            s.HiredUntil = new long[h];
            for (int i = 0; i < h; i++) s.HiredUntil[i] = r.ReadLong();
            int k = r.ReadCount();
            for (int i = 0; i < k; i++) s.SownTools.Add(new KeyValuePair<long, Fixed>(r.ReadLong(), r.ReadFixed()));
            return s;
        }
    }
}
