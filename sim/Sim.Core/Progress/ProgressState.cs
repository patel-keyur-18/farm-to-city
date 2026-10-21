using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Progress
{
    /// <summary>
    /// The first session and the letter (spec 009 §3.7, §4): the current step, the chapters seen (in the order seen) and
    /// whether the first session was skipped. Changed only by <c>SeeChapter</c>, <c>CompleteStep</c> and <c>SkipFirstSession</c>,
    /// so a replay reproduces it.
    /// </summary>
    public sealed class GuidanceState
    {
        /// <summary>Index of the current first-session step; the step count once done.</summary>
        public int Step { get; internal set; }

        /// <summary>Chapter indices in the order they were seen.</summary>
        public List<int> Seen { get; internal set; } = new List<int>();

        public bool Skipped { get; internal set; }

        public bool HasSeen(int chapter) { return Seen.Contains(chapter); }

        internal GuidanceState Clone()
        {
            return new GuidanceState { Step = Step, Seen = new List<int>(Seen), Skipped = Skipped };
        }

        internal void WriteTo(IStateWriter h, int stepCount)
        {
            h.WriteRef(ContentTable.Step, Step < stepCount ? Step : -1);
            h.WriteBool(Skipped);
            h.WriteInt(Seen.Count);
            foreach (int c in Seen) h.WriteRef(ContentTable.Chapter, c);
        }

        internal static GuidanceState ReadFrom(SaveReader r, int stepCount)
        {
            int step = r.ReadRef(ContentTable.Step);
            var g = new GuidanceState { Step = step < 0 ? stepCount : step, Skipped = r.ReadBool() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) g.Seen.Add(r.ReadRef(ContentTable.Chapter));
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
