using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Farm
{
    /// <summary>
    /// A whole acre of one fruit tree (spec 013 §3.3, ADR 0090): its trees grow, bear and are picked together, by spec 003's tree rules
    /// per tree; a batch is the trees' fruit × the Water, Care and Weather the acre had since the last one.
    /// </summary>
    public sealed class Orchard
    {
        public long Id { get; internal set; }
        public int TileId { get; internal set; }

        /// <summary>Index into <see cref="Definitions.FarmDefs.Trees"/>.</summary>
        public int Def { get; internal set; }
        public int Trees { get; internal set; }
        public long PlantedAt { get; internal set; }
        public TreeState State { get; internal set; }

        /// <summary>Grams of ripe fruit waiting (0 = none), and when the batch ripened.</summary>
        public long RipeGrams { get; internal set; }
        public long RipeAt { get; internal set; }
        public int Batches { get; internal set; }

        /// <summary>Money: Σ picked batches at the trader's buying price when picked; the saplings with planting, its book value.</summary>
        public long Returns { get; internal set; }
        public long Cost { get; internal set; }

        /// <summary>The season index in which returns first reached the cost (−1 = not yet).</summary>
        public long PaidBackPeriod { get; internal set; } = -1;
        public long JobId { get; internal set; }

        /// <summary>The factor sums since <see cref="SumsFrom"/> (the planting or the last batch), and the rates in force now.</summary>
        public long SumsFrom { get; internal set; }
        public Fixed WaterSum { get; internal set; }
        public Fixed CareSum { get; internal set; }
        public Fixed WeatherSum { get; internal set; }
        public Fixed WaterRate { get; internal set; }
        public Fixed WeatherRate { get; internal set; }

        /// <summary>The last batch's yield, for its Why? (null before the first).</summary>
        public YieldBreakdown Last { get; internal set; }

        internal Orchard Clone() { return (Orchard)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id);
            h.WriteInt(TileId);
            h.WriteRef(ContentTable.Tree, Def);
            h.WriteInt(Trees);
            h.WriteLong(PlantedAt);
            h.WriteInt((int)State);
            h.WriteLong(RipeGrams);
            h.WriteLong(RipeAt);
            h.WriteInt(Batches);
            h.WriteLong(Returns);
            h.WriteLong(Cost);
            h.WriteLong(PaidBackPeriod);
            h.WriteLong(JobId);
            h.WriteLong(SumsFrom);
            foreach (Fixed f in new[] { WaterSum, CareSum, WeatherSum, WaterRate, WeatherRate }) h.WriteFixed(f);
            h.WriteBool(Last != null);
            if (Last != null) Last.WriteTo(h);
        }

        internal static Orchard ReadFrom(SaveReader r)
        {
            var o = new Orchard
            {
                Id = r.ReadLong(), TileId = r.ReadTile(), Def = r.ReadRef(ContentTable.Tree), Trees = r.ReadInt(), PlantedAt = r.ReadLong(), State = (TreeState)r.ReadInt(),
                RipeGrams = r.ReadLong(), RipeAt = r.ReadLong(), Batches = r.ReadInt(), Returns = r.ReadLong(), Cost = r.ReadLong(), PaidBackPeriod = r.ReadLong(),
                JobId = r.ReadLong(), SumsFrom = r.ReadLong(), WaterSum = r.ReadFixed(), CareSum = r.ReadFixed(), WeatherSum = r.ReadFixed(), WaterRate = r.ReadFixed(),
                WeatherRate = r.ReadFixed(),
            };
            o.Last = r.ReadBool() ? YieldBreakdown.ReadFrom(r) : null;
            return o;
        }
    }

    /// <summary>The orchards, in id order (save schema 14's last section).</summary>
    public sealed class OrchardState
    {
        public List<Orchard> Orchards { get; private set; } = new List<Orchard>();
        public long NextOrchardId { get; internal set; } = 1;

        public Orchard On(int tileId) { return Orchards.Find(o => o.TileId == tileId); }

        internal OrchardState Clone()
        {
            var c = (OrchardState)MemberwiseClone();
            c.Orchards = Orchards.ConvertAll(o => o.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Orchards.Count);
            foreach (Orchard o in Orchards) o.WriteTo(h);
            h.WriteLong(NextOrchardId);
        }

        internal static OrchardState ReadFrom(SaveReader r)
        {
            var s = new OrchardState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Orchards.Add(Orchard.ReadFrom(r));
            s.NextOrchardId = r.ReadLong();
            return s;
        }
    }
}
