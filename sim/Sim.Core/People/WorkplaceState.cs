using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.People
{
    /// <summary>
    /// The farm's supervisor (spec 028 §3.9, save schema 24): a hire from the board's Supervisor post who covers up to five hands. His wage is paid upfront from your cash
    /// at the hire (the rest of the season) and at each close; unpaid, he goes. What he has saved you is counted.
    /// </summary>
    public sealed class Supervisor
    {
        public int Name { get; internal set; }
        public Fixed Skill { get; internal set; }
        public long Wage { get; internal set; }
        public long HiredAt { get; internal set; }

        /// <summary>Accidents his half chance spared, conflicts he settled, and associations his half chance kept from forming.</summary>
        public int Spared { get; internal set; }
        public int Settled { get; internal set; }
        public int Kept { get; internal set; }

        internal Supervisor Clone() { return (Supervisor)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteRef(ContentTable.Name, Name);
            h.WriteFixed(Skill);
            h.WriteLong(Wage);
            h.WriteLong(HiredAt);
            h.WriteInt(Spared);
            h.WriteInt(Settled);
            h.WriteInt(Kept);
        }

        internal static Supervisor ReadFrom(SaveReader r)
        {
            return new Supervisor { Name = r.ReadRef(ContentTable.Name), Skill = r.ReadFixed(), Wage = r.ReadLong(), HiredAt = r.ReadLong(), Spared = r.ReadInt(), Settled = r.ReadInt(), Kept = r.ReadInt() };
        }
    }

    /// <summary>One employer's standing with its workers (spec 028 §3.9): closes of tiredness, the quiet after a yes, refusals, and the season its output is cut. A place with none of it has no record.</summary>
    public sealed class PlaceRelations
    {
        /// <summary>0 for the farm, else the business's id.</summary>
        public long Place { get; internal set; }

        /// <summary>Closes in a row the team's average fatigue was above the association's level.</summary>
        public int TiredCloses { get; internal set; }

        /// <summary>The season index before which no association forms (after a yes).</summary>
        public long QuietUntil { get; internal set; }

        /// <summary>Refusals since an association last got what it asked.</summary>
        public int Refusals { get; internal set; }

        /// <summary>The season index + 1 whose output is cut (a refusal), or 0.</summary>
        public long CutSeason { get; internal set; }

        internal bool Used { get { return TiredCloses != 0 || QuietUntil != 0 || Refusals != 0 || CutSeason != 0; } }

        internal PlaceRelations Clone() { return (PlaceRelations)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Place);
            h.WriteInt(TiredCloses);
            h.WriteLong(QuietUntil);
            h.WriteInt(Refusals);
            h.WriteLong(CutSeason);
        }

        internal static PlaceRelations ReadFrom(SaveReader r)
        {
            return new PlaceRelations { Place = r.ReadLong(), TiredCloses = r.ReadInt(), QuietUntil = r.ReadLong(), Refusals = r.ReadInt(), CutSeason = r.ReadLong() };
        }
    }

    /// <summary>The workplace (spec 028 §3.9, schema 24): the supervisor, each place's relations, and when your talking with a quarrelling pair ends. Hashed only once used.</summary>
    public sealed class WorkplaceState
    {
        public Supervisor Supervisor { get; internal set; }
        public List<PlaceRelations> Places { get; } = new List<PlaceRelations>();

        /// <summary>While you talk to a quarrelling pair, your labour is cut until then (0: not talking).</summary>
        public long TalkUntil { get; internal set; }

        public PlaceRelations Relations(long place) { return Places.Find(x => x.Place == place); }

        internal PlaceRelations RelationsFor(long place)
        {
            PlaceRelations r = Relations(place);
            if (r != null) return r;
            r = new PlaceRelations { Place = place };
            int i = 0;
            while (i < Places.Count && Places[i].Place < place) i++;
            Places.Insert(i, r);
            return r;
        }

        internal bool Used { get { return Supervisor != null || TalkUntil != 0 || Places.Exists(x => x.Used); } }

        internal WorkplaceState Clone()
        {
            var c = new WorkplaceState { Supervisor = Supervisor?.Clone(), TalkUntil = TalkUntil };
            foreach (PlaceRelations p in Places) c.Places.Add(p.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(Supervisor != null);
            Supervisor?.WriteTo(h);
            h.WriteLong(TalkUntil);
            h.WriteInt(Places.Count);
            foreach (PlaceRelations p in Places) p.WriteTo(h);
        }

        internal static WorkplaceState ReadFrom(SaveReader r)
        {
            var s = new WorkplaceState();
            if (r.ReadBool()) s.Supervisor = Supervisor.ReadFrom(r);
            s.TalkUntil = r.ReadLong();
            int n = r.ReadCount();
            for (int i = 0; i < n; i++) s.Places.Add(PlaceRelations.ReadFrom(r));
            return s;
        }
    }
}
