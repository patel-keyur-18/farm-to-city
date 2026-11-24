using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.History
{
    /// <summary>
    /// One line of the town's history (spec 029 §3.1): when, which kind of moment, up to two numbers and an id. The sentence is made
    /// from a string template when the sheet opens, so its wording and language can change without touching a save.
    /// </summary>
    public sealed class ChronicleEntry
    {
        public long At { get; internal set; }
        public ChronicleKind Kind { get; internal set; }

        /// <summary>The kind's own numbers (acres, households, a name, a citizen id, grams, coins, a count: see <see cref="ChronicleRules"/>).</summary>
        public long A { get; internal set; }
        public long B { get; internal set; }

        /// <summary>A content id (a crop, a tree, a business type, a lender, an era, a story's arc), or empty.</summary>
        public string Id { get; internal set; } = "";

        internal ChronicleEntry Clone() { return (ChronicleEntry)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(At); h.WriteInt((int)Kind); h.WriteLong(A); h.WriteLong(B); h.WriteString(Id);
        }

        internal static ChronicleEntry ReadFrom(SaveReader r)
        {
            return new ChronicleEntry { At = r.ReadLong(), Kind = (ChronicleKind)r.ReadInt(), A = r.ReadLong(), B = r.ReadLong(), Id = r.ReadString() };
        }
    }

    /// <summary>
    /// The town's Chronicle (spec 029, save schema 24): the entries newest last, which once-only kinds are written, and each crop's kilos this
    /// season and its best earlier season (for a record harvest, §3.3). Hashed only once it holds an entry.
    /// </summary>
    public sealed class ChronicleState
    {
        public List<ChronicleEntry> Entries { get; internal set; } = new List<ChronicleEntry>();

        /// <summary>A bit for each once-only kind already written.</summary>
        public long Done { get; internal set; }

        /// <summary>By crop: the grams harvested this season and the best season total so far (zero until a season reaches the floor).</summary>
        public long[] SeasonGrams { get; internal set; }
        public long[] BestGrams { get; internal set; }

        /// <summary>The Almanacs and the year in progress (spec 029 §3.4).</summary>
        public AlmanacState Almanac { get; internal set; }

        /// <summary>The citizens' arcs (spec 029 §3.6).</summary>
        public StoriesState Stories { get; internal set; } = new StoriesState();

        /// <summary>A town loaded from an older save that still has to write "The Chronicle begins" (not saved: set by the loader).</summary>
        internal bool NeedsBegin;

        public ChronicleState(int crops)
        {
            SeasonGrams = new long[crops];
            BestGrams = new long[crops];
            Almanac = new AlmanacState(crops);
        }

        internal ChronicleState Clone()
        {
            return new ChronicleState(SeasonGrams.Length)
            {
                Entries = Entries.ConvertAll(x => x.Clone()), Done = Done, SeasonGrams = (long[])SeasonGrams.Clone(), BestGrams = (long[])BestGrams.Clone(), Almanac = Almanac.Clone(), Stories = Stories.Clone(), NeedsBegin = NeedsBegin,
            };
        }

        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && Entries.Count == 0) return; // hashed only once used
            h.WriteInt(Entries.Count);
            foreach (ChronicleEntry e in Entries) e.WriteTo(h);
            h.WriteLong(Done);
            for (int i = 0; i < SeasonGrams.Length; i++) { h.WriteLong(SeasonGrams[i]); h.WriteLong(BestGrams[i]); }
            Almanac.WriteTo(h);
            Stories.WriteTo(h);
        }

        internal static ChronicleState ReadFrom(SaveReader r, int crops)
        {
            var s = new ChronicleState(crops);
            int n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Entries.Add(ChronicleEntry.ReadFrom(r));
            s.Done = r.ReadLong();
            for (int i = 0; i < crops; i++) { s.SeasonGrams[i] = r.ReadLong(); s.BestGrams[i] = r.ReadLong(); }
            s.Almanac = AlmanacState.ReadFrom(r, crops);
            s.Stories = StoriesState.ReadFrom(r);
            return s;
        }
    }
}
