using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>One building being made (spec 025 §3.2, US-145): paid for at its start, drawing materials from the trader's day, then built by its masons.</summary>
    public sealed class BuildProject
    {
        public const int Yours = 0, Town = 1, Builders = 2;

        public long Id { get; internal set; }

        /// <summary>The kind's id in <c>township.json</c> construction.</summary>
        public string Kind { get; internal set; }

        /// <summary>0 yours (a family house), 1 the treasury's (a civic work), 2 the builders' (a parcel's homes).</summary>
        public int Owner { get; internal set; }

        /// <summary>The site: the house's acre, the civic plot, or a builders' parcel's first home acre.</summary>
        public int Tile { get; internal set; }

        /// <summary>A builders' parcel, else −1; and how many homes it will hold.</summary>
        public int Parcel { get; internal set; } = -1;
        public int Units { get; internal set; } = 1;

        /// <summary>Your house's <see cref="Sim.Core.People.Construction"/> (the site on the map), else 0.</summary>
        public long House { get; internal set; }

        /// <summary>What was paid at the start (1/100 coin), the masons' part of it, and the masons reserved (empty: the district's gang).</summary>
        public long Paid { get; internal set; }
        public long MasonPay { get; internal set; }
        public List<long> Masons { get; internal set; } = new List<long>();
        public bool Gang { get { return Masons.Count == 0; } }

        public long Bricks { get; internal set; }
        public long Cement { get; internal set; }
        public long Timber { get; internal set; }

        /// <summary>The build's hours, fixed at the start by its masons.</summary>
        public int Hours { get; internal set; }

        public bool Working { get; internal set; }
        public long StartedAt { get; internal set; }
        public long ReadyAt { get; internal set; }
        public long JobId { get; internal set; }

        /// <summary>The held alert was raised (once).</summary>
        public bool Alerted { get; internal set; }

        internal BuildProject Clone()
        {
            var c = (BuildProject)MemberwiseClone();
            c.Masons = new List<long>(Masons);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteString(Kind);
            h.WriteInt(Owner);
            h.WriteInt(Tile);
            h.WriteInt(Parcel);
            h.WriteInt(Units);
            h.WriteLong(House);
            h.WriteLong(Paid);
            h.WriteLong(MasonPay);
            h.WriteInt(Masons.Count);
            foreach (long m in Masons) h.WriteLong(m);
            h.WriteLong(Bricks);
            h.WriteLong(Cement);
            h.WriteLong(Timber);
            h.WriteInt(Hours);
            h.WriteBool(Working);
            h.WriteLong(StartedAt);
            h.WriteLong(ReadyAt);
            h.WriteLong(JobId);
            h.WriteBool(Alerted);
        }

        internal static BuildProject ReadFrom(SaveReader r)
        {
            var p = new BuildProject { Id = r.ReadLong(), Kind = r.ReadString(), Owner = r.ReadInt(), Tile = r.ReadInt(), Parcel = r.ReadInt(), Units = r.ReadInt(), House = r.ReadLong(), Paid = r.ReadLong(), MasonPay = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) p.Masons.Add(r.ReadLong());
            p.Bricks = r.ReadLong(); p.Cement = r.ReadLong(); p.Timber = r.ReadLong();
            p.Hours = r.ReadInt(); p.Working = r.ReadBool(); p.StartedAt = r.ReadLong(); p.ReadyAt = r.ReadLong(); p.JobId = r.ReadLong(); p.Alerted = r.ReadBool();
            return p;
        }
    }
}
