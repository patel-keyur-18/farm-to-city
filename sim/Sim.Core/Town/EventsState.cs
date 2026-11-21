using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>One card waiting for an answer (spec 027 §3.1): the event, when it came and when its clock runs out, and what it is about.</summary>
    public sealed class PendingEvent
    {
        public int Id { get; internal set; }
        public int Def { get; internal set; }
        public long RaisedAt { get; internal set; }
        public long ExpiresAt { get; internal set; }

        /// <summary>What it is about: a building, a business, a machine, a household, a good, a parcel, by the event's own meaning; the second is the other household or a count.</summary>
        public long Target { get; internal set; }
        public long Target2 { get; internal set; }

        /// <summary>The content table <see cref="Target"/> indexes when it is a content index and not an instance id (a price event's good, an auction's parcel), else −1: saved by id, not by position (ADR 0041).</summary>
        public int TargetTable { get; internal set; } = -1;

        /// <summary>A number the card carries: a fire's repair bill, a grant's sum, a price shift's percent.</summary>
        public long Param { get; internal set; }

        /// <summary>Which modifier made it likely (spec 027 §3.3): 0 none, else the event's own numbering (its <c>why</c> string).</summary>
        public int Why { get; internal set; }

        internal PendingEvent Clone() { return (PendingEvent)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Id);
            h.WriteRef(ContentTable.Event, Def);
            h.WriteLong(RaisedAt);
            h.WriteLong(ExpiresAt);
            h.WriteInt(TargetTable);
            if (TargetTable >= 0) h.WriteRef((ContentTable)TargetTable, (int)Target); else h.WriteLong(Target);
            h.WriteLong(Target2);
            h.WriteLong(Param);
            h.WriteInt(Why);
        }

        internal static PendingEvent ReadFrom(SaveReader r)
        {
            var p = new PendingEvent { Id = r.ReadInt(), Def = r.ReadRef(ContentTable.Event), RaisedAt = r.ReadLong(), ExpiresAt = r.ReadLong() };
            p.TargetTable = r.ReadInt();
            p.Target = p.TargetTable >= 0 ? r.ReadRef((ContentTable)p.TargetTable) : r.ReadLong();
            p.Target2 = r.ReadLong();
            p.Param = r.ReadLong();
            p.Why = r.ReadInt();
            return p;
        }
    }

    /// <summary>What an event leaves running for a while: a price or cost shift, a happiness lift, a flag, a building or machine shut (spec 027 §3.2).</summary>
    public sealed class EventEffect
    {
        public const int Price = 0, Cost = 1, Lift = 2, Flag = 3, Shut = 4, Epidemic = 5;

        /// <summary>The cost shifts' targets and the shut kinds.</summary>
        public const int Fuel = 0, Inputs = 1, ShutBusiness = 0, ShutMachine = 1, ShutBuilding = 2;

        public int Kind { get; internal set; }

        /// <summary>A good, a cost target, a flag code or a shut kind.</summary>
        public int Code { get; internal set; }
        public long Target { get; internal set; }

        /// <summary>A percent (a shift), points (a lift), or the epidemic's sick factor in hundredths.</summary>
        public int Percent { get; internal set; }

        /// <summary>Until this time (a season's effect is made to end where the season does).</summary>
        public long Until { get; internal set; }

        /// <summary>The event it came from.</summary>
        public int Def { get; internal set; }

        internal EventEffect Clone() { return (EventEffect)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Kind);
            if (Kind == Price) h.WriteRef(ContentTable.Good, Code); else h.WriteInt(Code); // a price shift names a good, any other effect a code of its own
            h.WriteLong(Target);
            h.WriteInt(Percent);
            h.WriteLong(Until);
            h.WriteRef(ContentTable.Event, Def);
        }

        internal static EventEffect ReadFrom(SaveReader r)
        {
            int kind = r.ReadInt();
            return new EventEffect { Kind = kind, Code = kind == Price ? r.ReadRef(ContentTable.Good) : r.ReadInt(), Target = r.ReadLong(), Percent = r.ReadInt(), Until = r.ReadLong(), Def = r.ReadRef(ContentTable.Event) };
        }
    }

    /// <summary>A card answered (or defaulted), kept for the Events sheet (spec 027 §3.1): the event, the choice, when, and what it cost (negative) or paid.</summary>
    public readonly struct EventRecord
    {
        public int Def { get; }
        public int Choice { get; }
        public bool Defaulted { get; }
        public long At { get; }
        public long Cash { get; }
        public long Target { get; }

        /// <summary>The content table <see cref="Target"/> indexes, or −1 (see <see cref="PendingEvent.TargetTable"/>).</summary>
        public int TargetTable { get; }

        public EventRecord(int def, int choice, bool defaulted, long at, long cash, long target, int targetTable = -1) { Def = def; Choice = choice; Defaulted = defaulted; At = at; Cash = cash; Target = target; TargetTable = targetTable; }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteRef(ContentTable.Event, Def);
            h.WriteInt(Choice);
            h.WriteBool(Defaulted);
            h.WriteLong(At);
            h.WriteLong(Cash);
            h.WriteInt(TargetTable);
            if (TargetTable >= 0) h.WriteRef((ContentTable)TargetTable, (int)Target); else h.WriteLong(Target);
        }

        internal static EventRecord ReadFrom(SaveReader r)
        {
            int def = r.ReadRef(ContentTable.Event), choice = r.ReadInt();
            bool defaulted = r.ReadBool();
            long at = r.ReadLong(), cash = r.ReadLong();
            int table = r.ReadInt();
            long target = table >= 0 ? r.ReadRef((ContentTable)table) : r.ReadLong();
            return new EventRecord(def, choice, defaulted, at, cash, target, table);
        }
    }

    /// <summary>
    /// The event engine (spec 027 §4, save schema 22): the cards waiting, what the last answers were, the mercy rules' marks, what is running and the one disaster
    /// signalled. A town before the Township has none of it and hashes as before.
    /// </summary>
    public sealed class EventsState
    {
        /// <summary>The first 6:00 the engine saw the Township (−1: not yet): no event in the days after it (§3.1).</summary>
        public long StartedAt { get; internal set; } = -1;
        public int NextId { get; internal set; } = 1;
        public List<PendingEvent> Waiting { get; internal set; } = new List<PendingEvent>();
        public List<EventRecord> History { get; internal set; } = new List<EventRecord>();

        /// <summary>Per event (content order), the season counter it last came in (−1: never).</summary>
        public List<long> LastSeason { get; internal set; } = new List<long>();

        /// <summary>The season the last major came in, and the last season a crop major is barred from (−1 for none) (§3.1 mercy rules 1 and 2).</summary>
        public long MajorSeason { get; internal set; } = -1;
        public long CropQuietUntil { get; internal set; } = -1;

        /// <summary>The rain hours' log, for the weather events (§3.3): the last time it rained, and the last day's heavy-rain hours as a mask (bit 0 the latest hour).</summary>
        public long LastRainAt { get; internal set; } = -1;
        public long HeavyMask { get; internal set; }
        public long LastHourAt { get; internal set; } = -1;
        public long LastHeatAt { get; internal set; } = -1;

        /// <summary>The price index when each season began, for the fuel spike's weight (§3.3): the last two seasons'.</summary>
        public long IndexThen { get; internal set; }
        public long IndexBefore { get; internal set; }

        public List<EventEffect> Effects { get; internal set; } = new List<EventEffect>();

        /// <summary>The disaster signalled, or none (<see cref="DisasterDef"/> −1): its target, when it was warned and when it strikes, the days it has slid, and the choice made.</summary>
        public int DisasterDef { get; internal set; } = -1;
        public long DisasterTarget { get; internal set; }

        /// <summary>0: the target is a building; 1: a business (a fire on a shop or mill).</summary>
        public long DisasterTarget2 { get; internal set; }
        public long DisasterWarnedAt { get; internal set; }
        public long DisasterStrikeAt { get; internal set; }
        public int DisasterSlides { get; internal set; }
        public int DisasterChoice { get; internal set; } = -1;

        /// <summary>The last fire: where, the bill and when it struck (the books' line and the map's smoke).</summary>
        public long FireTarget { get; internal set; } = -1;
        public long FireBill { get; internal set; }
        public long FireAt { get; internal set; }

        /// <summary>Whether the fire that struck was insured when it struck (a policy whose cover began no later than the warning, spec 027 §3.6): its repair pays a claim.</summary>
        public bool FireInsured { get; internal set; }

        /// <summary>The last day (a day number) whose roll ran while you were here, and the last whose minor roll ran while you were away: a day rolls once, at your first live hour from 6:00 (ADR 0154, spec 027 §3.1).</summary>
        public long RollDay { get; internal set; } = -1;
        public long AwayRollDay { get; internal set; } = -1;

        /// <summary>Money moved by events this year of play, for the *Why?* and the books (kept as totals).</summary>
        public long LossTotal { get; internal set; }

        internal bool IsDefault { get { return StartedAt < 0 && Waiting.Count == 0 && History.Count == 0 && Effects.Count == 0 && DisasterDef < 0 && LastRainAt < 0; } }

        public long LastSeasonOf(int def) { return def < LastSeason.Count ? LastSeason[def] : -1; }

        internal void SetLastSeason(int def, long season)
        {
            while (LastSeason.Count <= def) LastSeason.Add(-1);
            LastSeason[def] = season;
        }

        internal EventsState Clone()
        {
            var c = (EventsState)MemberwiseClone();
            c.Waiting = Waiting.ConvertAll(x => x.Clone());
            c.History = new List<EventRecord>(History);
            c.LastSeason = new List<long>(LastSeason);
            c.Effects = Effects.ConvertAll(x => x.Clone());
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(StartedAt);
            h.WriteInt(NextId);
            h.WriteInt(Waiting.Count);
            foreach (PendingEvent p in Waiting) p.WriteTo(h);
            h.WriteInt(History.Count);
            foreach (EventRecord r in History) r.WriteTo(h);
            h.WriteInt(LastSeason.Count);
            foreach (long l in LastSeason) h.WriteLong(l);
            h.WriteLong(MajorSeason);
            h.WriteLong(CropQuietUntil);
            h.WriteLong(LastRainAt);
            h.WriteLong(HeavyMask);
            h.WriteLong(LastHourAt);
            h.WriteLong(LastHeatAt);
            h.WriteLong(IndexThen);
            h.WriteLong(IndexBefore);
            h.WriteInt(Effects.Count);
            foreach (EventEffect e in Effects) e.WriteTo(h);
            h.WriteRef(ContentTable.Event, DisasterDef);
            h.WriteLong(DisasterTarget);
            h.WriteLong(DisasterTarget2);
            h.WriteLong(DisasterWarnedAt);
            h.WriteLong(DisasterStrikeAt);
            h.WriteInt(DisasterSlides);
            h.WriteInt(DisasterChoice);
            h.WriteLong(FireTarget);
            h.WriteLong(FireBill);
            h.WriteLong(FireAt);
            h.WriteBool(FireInsured);
            h.WriteLong(LossTotal);
            h.WriteLong(RollDay);
            h.WriteLong(AwayRollDay);
        }

        internal static EventsState ReadFrom(SaveReader r)
        {
            var s = new EventsState { StartedAt = r.ReadLong(), NextId = r.ReadInt() };
            int n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Waiting.Add(PendingEvent.ReadFrom(r));
            n = r.ReadInt();
            for (int i = 0; i < n; i++) s.History.Add(EventRecord.ReadFrom(r));
            n = r.ReadInt();
            for (int i = 0; i < n; i++) // the saved list is by the saved content's order: each season goes to its event's place in the running content
            {
                long season = r.ReadLong();
                if (season >= 0) s.SetLastSeason(r.Slot(ContentTable.Event, i), season);
            }
            s.MajorSeason = r.ReadLong();
            s.CropQuietUntil = r.ReadLong();
            s.LastRainAt = r.ReadLong();
            s.HeavyMask = r.ReadLong();
            s.LastHourAt = r.ReadLong();
            s.LastHeatAt = r.ReadLong();
            s.IndexThen = r.ReadLong();
            s.IndexBefore = r.ReadLong();
            n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Effects.Add(EventEffect.ReadFrom(r));
            s.DisasterDef = r.ReadRef(ContentTable.Event);
            s.DisasterTarget = r.ReadLong();
            s.DisasterTarget2 = r.ReadLong();
            s.DisasterWarnedAt = r.ReadLong();
            s.DisasterStrikeAt = r.ReadLong();
            s.DisasterSlides = r.ReadInt();
            s.DisasterChoice = r.ReadInt();
            s.FireTarget = r.ReadLong();
            s.FireBill = r.ReadLong();
            s.FireAt = r.ReadLong();
            s.FireInsured = r.ReadBool();
            s.LossTotal = r.ReadLong();
            s.RollDay = r.ReadLong();
            s.AwayRollDay = r.ReadLong();
            return s;
        }
    }
}
