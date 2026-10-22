using System;
using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.People;

namespace Sim.Core.Saves
{
    /// <summary>What a notification is about (spec 007 §3.5, ADR 0041), most important first.</summary>
    public enum NotifyKind
    {
        LoanMissed = 0,
        FinalNotice = 1,
        ContractLapsed = 2,
        CropReady = 3,
        BedReady = 4,
        FruitRipe = 5,
        HutBuilt = 6,
        HandsWork = 7,

        /// <summary>The supermarket's 6:00 orders (spec 011 §3.5); CropId lists the goods, comma-separated.</summary>
        OrdersPosted = 8,

        /// <summary>Straw or cattle feed will last less than 2 days (spec 012 §3.14); CropId is the good.</summary>
        FeedLow = 9,
    }

    public enum NotifyImportance
    {
        Urgent = 0,
        NeedsYou = 1,
        Information = 2,
    }

    /// <summary>One thing that happens while the player is away: when (wall clock) and what.</summary>
    public readonly struct AbsenceItem
    {
        public long WallUtc { get; }
        public NotifyKind Kind { get; }

        /// <summary>The crop, garden crop, fruit or good it is about, or null.</summary>
        public string CropId { get; }

        public AbsenceItem(long wallUtc, NotifyKind kind, string cropId)
        {
            WallUtc = wallUtc;
            Kind = kind;
            CropId = cropId;
        }
    }

    /// <summary>A notification to schedule: its text is a string-table key for the kind, the count and the crop (spec 008).</summary>
    public sealed class PlannedNotification
    {
        public long WallUtc { get; internal set; }
        public NotifyKind Kind { get; internal set; }
        public int Count { get; internal set; }

        /// <summary>The crop when every grouped item is about the same one, else null ("3 fields are ready").</summary>
        public string CropId { get; internal set; }
        public NotifyImportance Importance { get { return NotificationPlanner.ImportanceOf(Kind); } }
    }

    /// <summary>
    /// Plans local notifications when the app goes to the background (spec 007 §3.5, ADR 0041): the away advance runs on a
    /// copy of the town up to the away cap, and its events become notifications, ranked urgent, needs you, information,
    /// grouped, kept out of quiet hours and within the platform's pending limit. Deterministic, so what it says will happen
    /// does happen if the player stays away. The real town is never touched.
    /// </summary>
    public static class NotificationPlanner
    {
        public static NotifyImportance ImportanceOf(NotifyKind kind)
        {
            if (kind <= NotifyKind.ContractLapsed) return NotifyImportance.Urgent;
            return kind == NotifyKind.HandsWork ? NotifyImportance.Information : NotifyImportance.NeedsYou;
        }

        public static List<PlannedNotification> Plan(Simulation sim, NotifyDef def, bool enabled, int utcOffsetMinutes)
        {
            if (!enabled) return new List<PlannedNotification>();
            return Group(Absence(sim), def, utcOffsetMinutes);
        }

        /// <summary>The events of an away advance to the cap, run on a copy of the state.</summary>
        public static List<SimEvent> SimulateAbsence(Simulation sim)
        {
            Simulation copy = Simulation.FromState(sim.Config, sim.State.Clone(withCommandLog: false));
            copy.Execute(new Sync(copy.State.Clock.LastWallUtc + sim.Clock.AwayCapSeconds));
            return copy.DrainEvents();
        }

        /// <summary>What would be worth a notification during a full absence, at wall-clock times.</summary>
        public static List<AbsenceItem> Absence(Simulation sim)
        {
            long simTime = sim.State.Clock.SimTime, wall = sim.State.Clock.LastWallUtc;
            List<SimEvent> events = SimulateAbsence(sim);
            var items = new List<AbsenceItem>();
            for (int i = 0; i < events.Count; i++)
            {
                SimEvent e = events[i];
                long at = wall + (e.SimTime - simTime);
                switch (e)
                {
                    case LoanPaymentMissed _: items.Add(new AbsenceItem(at, NotifyKind.LoanMissed, null)); break;
                    case FinalNotice _: items.Add(new AbsenceItem(at, NotifyKind.FinalNotice, null)); break;
                    case ContractLapsed _: items.Add(new AbsenceItem(at, NotifyKind.ContractLapsed, null)); break;
                    case HandHutBuilt _: items.Add(new AbsenceItem(at, NotifyKind.HutBuilt, null)); break;
                    case BedReady b: items.Add(new AbsenceItem(at, NotifyKind.BedReady, b.GardenCropId)); break;
                    case CropReady c:
                        if (!HandWillTake(events, i, OrderTargetKind.Field, c.TileId)) items.Add(new AbsenceItem(at, NotifyKind.CropReady, c.CropId));
                        break;
                    case FruitRipe f:
                        if (!HandWillTake(events, i, OrderTargetKind.Tree, f.SlotId)) items.Add(new AbsenceItem(at, NotifyKind.FruitRipe, f.TreeId));
                        break;
                    case OrdersPosted p:
                        var goods = new List<string>();
                        foreach (Market.SupermarketOrder so in p.Orders) goods.Add(sim.Config.Game.Market.Goods[so.Good].Id);
                        items.Add(new AbsenceItem(at, NotifyKind.OrdersPosted, string.Join(",", goods)));
                        break;
                    case FeedLow f: items.Add(new AbsenceItem(at, NotifyKind.FeedLow, f.Good)); break;
                    case OrderExecuted o:
                        if (o.Action == OrderAction.Harvested || o.Action == OrderAction.Picked) items.Add(new AbsenceItem(at, NotifyKind.HandsWork, o.GoodId));
                        break;
                }
            }
            return items;
        }

        /// <summary>True when a standing order harvests or picks the target later in the same absence: then it isn't "ready" for the player.</summary>
        private static bool HandWillTake(List<SimEvent> events, int from, OrderTargetKind kind, long target)
        {
            for (int i = from + 1; i < events.Count; i++)
                if (events[i] is OrderExecuted o && o.Kind == kind && o.Target == target && (o.Action == OrderAction.Harvested || o.Action == OrderAction.Picked))
                    return true;
            return false;
        }

        /// <summary>
        /// Items moved out of quiet hours, grouped by kind (information within <c>infoGroupMinutes</c>, others within
        /// <c>groupWindowMinutes</c> of a group's first item), the most important first up to <c>maxPending</c>, in time order.
        /// </summary>
        public static List<PlannedNotification> Group(IReadOnlyList<AbsenceItem> items, NotifyDef def, int utcOffsetMinutes)
        {
            var moved = new List<AbsenceItem>(items.Count);
            foreach (AbsenceItem i in items) moved.Add(new AbsenceItem(OutOfQuiet(i.WallUtc, def, utcOffsetMinutes), i.Kind, i.CropId));
            moved.Sort((a, b) => a.WallUtc != b.WallUtc ? a.WallUtc.CompareTo(b.WallUtc) : a.Kind.CompareTo(b.Kind));

            var groups = new List<PlannedNotification>();
            foreach (AbsenceItem i in moved)
            {
                long window = 60L * (ImportanceOf(i.Kind) == NotifyImportance.Information ? def.InfoGroupMinutes : def.GroupWindowMinutes);
                PlannedNotification g = null;
                for (int k = groups.Count - 1; k >= 0; k--)
                    if (groups[k].Kind == i.Kind && i.WallUtc - groups[k].WallUtc <= window) { g = groups[k]; break; }
                if (g == null) groups.Add(new PlannedNotification { WallUtc = i.WallUtc, Kind = i.Kind, Count = 1, CropId = i.CropId });
                else
                {
                    g.Count++;
                    if (g.CropId != i.CropId) g.CropId = null;
                }
            }

            groups.Sort((a, b) => a.Importance != b.Importance ? a.Importance.CompareTo(b.Importance) : a.WallUtc.CompareTo(b.WallUtc));
            if (groups.Count > def.MaxPending) groups.RemoveRange(def.MaxPending, groups.Count - def.MaxPending);
            groups.Sort((a, b) => a.WallUtc != b.WallUtc ? a.WallUtc.CompareTo(b.WallUtc) : a.Kind.CompareTo(b.Kind));
            return groups;
        }

        /// <summary>A time inside quiet hours (device local time) moves to the end of them.</summary>
        private static long OutOfQuiet(long wallUtc, NotifyDef def, int utcOffsetMinutes)
        {
            long local = wallUtc + utcOffsetMinutes * 60L;
            long day = local - Mod(local, 86400);
            long minute = Mod(local, 86400) / 60;
            int from = def.QuietFromMinute, to = def.QuietToMinute;
            bool wraps = from > to;
            bool quiet = wraps ? (minute >= from || minute < to) : (minute >= from && minute < to);
            if (!quiet) return wallUtc;
            long end = day + to * 60L;
            if (wraps && minute >= from) end += 86400; // the evening part: quiet ends the next morning
            return end - utcOffsetMinutes * 60L;
        }

        private static long Mod(long a, long m) { long r = a % m; return r < 0 ? r + m : r; }
    }
}
