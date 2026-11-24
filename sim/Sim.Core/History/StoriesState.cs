using System.Collections.Generic;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.History
{
    /// <summary>One citizen's arc (spec 029 §3.6): where it is, when its last step fell (a season index), and the stall's kind.</summary>
    public sealed class StoryArcState
    {
        public long Citizen { get; internal set; }
        public StoryArc Arc { get; internal set; }

        /// <summary>The last step taken, 1 to 4 (a long life's fourth is the death remembered).</summary>
        public int Step { get; internal set; }
        public long At { get; internal set; }

        /// <summary>A stall's kind (<c>ShopKind</c>), else 0.</summary>
        public int Other { get; internal set; }
        public bool Done { get; internal set; }

        internal StoryArcState Clone() { return (StoryArcState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Citizen); h.WriteInt((int)Arc); h.WriteInt(Step); h.WriteLong(At); h.WriteInt(Other); h.WriteBool(Done);
        }

        internal static StoryArcState ReadFrom(SaveReader r)
        {
            return new StoryArcState { Citizen = r.ReadLong(), Arc = (StoryArc)r.ReadInt(), Step = r.ReadInt(), At = r.ReadLong(), Other = r.ReadInt(), Done = r.ReadBool() };
        }
    }

    /// <summary>The citizens' arcs and the season (+1) of the last step taken anywhere: at most one a season across the town. Hashed only once used.</summary>
    public sealed class StoriesState
    {
        public List<StoryArcState> Arcs { get; internal set; } = new List<StoryArcState>();
        public long LastStep { get; internal set; }

        internal bool Unused { get { return Arcs.Count == 0 && LastStep == 0; } }

        internal StoriesState Clone() { return new StoriesState { Arcs = Arcs.ConvertAll(x => x.Clone()), LastStep = LastStep }; }

        internal void WriteTo(IStateWriter h)
        {
            if (h.Hashing && Unused) return;
            h.WriteInt(Arcs.Count);
            foreach (StoryArcState a in Arcs) a.WriteTo(h);
            h.WriteLong(LastStep);
        }

        internal static StoriesState ReadFrom(SaveReader r)
        {
            var s = new StoriesState();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Arcs.Add(StoryArcState.ReadFrom(r));
            s.LastStep = r.ReadLong();
            return s;
        }
    }
}
