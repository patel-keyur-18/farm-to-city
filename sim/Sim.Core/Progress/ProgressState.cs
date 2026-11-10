using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Progress
{
    /// <summary>A Codex card found (spec 018 §3.3): when, the player's numbers then (as the card's formats say), and whether it was read.</summary>
    public sealed class CodexFind
    {
        public string Id { get; internal set; }
        public long FoundAt { get; internal set; }
        public long[] Numbers { get; internal set; }
        public bool Read { get; internal set; }

        public CodexFind(string id, long[] numbers) { Id = id; Numbers = numbers ?? new long[0]; }

        internal CodexFind Clone() { return new CodexFind(Id, (long[])Numbers.Clone()) { FoundAt = FoundAt, Read = Read }; }
    }

    /// <summary>
    /// The first session and the letter (spec 009 §3.7, §4): the current step, the chapters seen (in the order seen) and
    /// whether the first session was skipped; the Codex's cards found, in the order found (spec 018 §3.3, schema 15). Changed only by
    /// <c>SeeChapter</c>, <c>CompleteStep</c>, <c>SkipFirstSession</c>, <c>FindCodexCards</c> and <c>ReadCodexCard</c>, so a replay
    /// reproduces it.
    /// </summary>
    public sealed class GuidanceState
    {
        /// <summary>Index of the current first-session step; the step count once done.</summary>
        public int Step { get; internal set; }

        /// <summary>Chapter indices in the order they were seen.</summary>
        public List<int> Seen { get; internal set; } = new List<int>();

        public bool Skipped { get; internal set; }

        public bool HasSeen(int chapter) { return Seen.Contains(chapter); }

        public List<CodexFind> Codex { get; internal set; } = new List<CodexFind>();

        public CodexFind Found(string id) { return Codex.Find(x => x.Id == id); }

        internal GuidanceState Clone()
        {
            return new GuidanceState { Step = Step, Seen = new List<int>(Seen), Skipped = Skipped, Codex = Codex.ConvertAll(x => x.Clone()) };
        }

        internal void WriteTo(IStateWriter h, int stepCount)
        {
            h.WriteRef(ContentTable.Step, Step < stepCount ? Step : -1);
            h.WriteBool(Skipped);
            h.WriteInt(Seen.Count);
            foreach (int c in Seen) h.WriteRef(ContentTable.Chapter, c);
            if (h.Hashing ? Codex.Count > 0 : !(h is SaveWriter w && w.Schema < 15)) // schema 15; hashed only once a card is found
            {
                h.WriteInt(Codex.Count);
                foreach (CodexFind f in Codex)
                {
                    h.WriteString(f.Id);
                    h.WriteLong(f.FoundAt);
                    h.WriteBool(f.Read);
                    h.WriteInt(f.Numbers.Length);
                    foreach (long v in f.Numbers) h.WriteLong(v);
                }
            }
        }

        internal static GuidanceState ReadFrom(SaveReader r, int stepCount)
        {
            int step = r.ReadRef(ContentTable.Step);
            var g = new GuidanceState { Step = step < 0 ? stepCount : step, Skipped = r.ReadBool() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) g.Seen.Add(r.ReadRef(ContentTable.Chapter));
            if (r.Schema < 15) return g; // schema 14: no Codex yet, nothing back-filled (ADR 0095 §7)
            int m = r.ReadCount();
            for (int i = 0; i < m; i++)
            {
                string id = r.ReadString();
                long at = r.ReadLong();
                bool read = r.ReadBool();
                var numbers = new long[r.ReadCount()];
                for (int k = 0; k < numbers.Length; k++) numbers[k] = r.ReadLong();
                g.Codex.Add(new CodexFind(id, numbers) { FoundAt = at, Read = read }); // a card the content dropped stays, unshown
            }
            return g;
        }
    }

    /// <summary>
    /// The era and the next era's progress (spec 009 §3.6, §4): this season's harvested acres (sorted tile ids) and their
    /// Care, the profitable seasons so far, and when the current era unlocked.
    /// </summary>
    public sealed class EraState
    {
        /// <summary>Index into <c>OnboardingDefs.Eras</c>.</summary>
        public int Current { get; internal set; }

        /// <summary>The season (ledger period) the acres and Care below belong to, or −1 before the first field harvest.</summary>
        public long SeasonPeriod { get; internal set; } = -1;

        public List<int> SeasonAcres { get; internal set; } = new List<int>();
        public Fixed SeasonCareSum { get; internal set; }
        public int SeasonHarvests { get; internal set; }
        public int ProfitableSeasons { get; internal set; }

        /// <summary>Sim time the current era unlocked, or −1 for the starting era.</summary>
        public long UnlockedAt { get; internal set; } = -1;

        internal EraState Clone()
        {
            return new EraState
            {
                Current = Current, SeasonPeriod = SeasonPeriod, SeasonAcres = new List<int>(SeasonAcres), SeasonCareSum = SeasonCareSum,
                SeasonHarvests = SeasonHarvests, ProfitableSeasons = ProfitableSeasons, UnlockedAt = UnlockedAt,
            };
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteRef(ContentTable.Era, Current);
            h.WriteLong(SeasonPeriod);
            h.WriteInt(SeasonAcres.Count);
            foreach (int t in SeasonAcres) h.WriteInt(t);
            h.WriteFixed(SeasonCareSum);
            h.WriteInt(SeasonHarvests);
            h.WriteInt(ProfitableSeasons);
            h.WriteLong(UnlockedAt);
        }

        internal static EraState ReadFrom(SaveReader r)
        {
            var e = new EraState { Current = r.ReadRef(ContentTable.Era), SeasonPeriod = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) e.SeasonAcres.Add(r.ReadTile());
            e.SeasonCareSum = r.ReadFixed();
            e.SeasonHarvests = r.ReadInt();
            e.ProfitableSeasons = r.ReadInt();
            e.UnlockedAt = r.ReadLong();
            return e;
        }
    }
}
