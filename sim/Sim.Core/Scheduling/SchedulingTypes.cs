using System;

namespace Sim.Core.Scheduling
{
    /// <summary>
    /// The item class, the second key of the processing order (spec 001 §3.3). At a timestamp that is both an
    /// hour and a day boundary, jobs run first, then the hourly pass, then the daily pass, and so on.
    /// </summary>
    public enum ItemClass
    {
        Job = 0,
        Hourly = 1,
        Daily = 2,
        Seasonal = 3,
        Yearly = 4,
    }

    /// <summary>The GDD's fixed system order, the third key of the processing order (spec 001 §3.3).</summary>
    public enum SystemOrder
    {
        Time = 0,
        Weather = 1,
        Agriculture = 2,
        Production = 3,
        Market = 4,
        Labour = 5,
        Population = 6,
        BusinessFinance = 7,
        Civic = 8,
        Treasury = 9,
        Events = 10,
    }

    /// <summary>Job lifecycle (ADR 0002): Running until due, then Ready (waiting for the player) or Done.</summary>
    public enum JobState
    {
        Running = 0,
        Ready = 1,
        Done = 2,
    }

    /// <summary>
    /// One scheduler entry. Ordered by (dueTime, class, systemOrder, entityId, sequence); the sequence is
    /// unique, so the order is total.
    /// </summary>
    public readonly struct ScheduledItem : IComparable<ScheduledItem>
    {
        public long DueTime { get; }
        public ItemClass Class { get; }
        public SystemOrder System { get; }
        public long EntityId { get; }
        public long Sequence { get; }

        /// <summary>The job this item runs, or 0 for a settlement pass.</summary>
        public long JobId { get; }

        public ScheduledItem(long dueTime, ItemClass cls, SystemOrder system, long entityId, long sequence, long jobId)
        {
            DueTime = dueTime;
            Class = cls;
            System = system;
            EntityId = entityId;
            Sequence = sequence;
            JobId = jobId;
        }

        public int CompareTo(ScheduledItem other)
        {
            int c = DueTime.CompareTo(other.DueTime);
            if (c != 0) return c;
            c = ((int)Class).CompareTo((int)other.Class);
            if (c != 0) return c;
            c = ((int)System).CompareTo((int)other.System);
            if (c != 0) return c;
            c = EntityId.CompareTo(other.EntityId);
            if (c != 0) return c;
            return Sequence.CompareTo(other.Sequence);
        }
    }

    /// <summary>A job due within a horizon, with its wall-clock due time for notifications (spec 001 §3.7).</summary>
    public readonly struct UpcomingJob
    {
        public long JobId { get; }
        public string Type { get; }
        public long TargetId { get; }
        public long DueTime { get; }

        /// <summary>lastWallUtc + (dueTime − simTime), Unix seconds UTC.</summary>
        public long WallDueUtc { get; }

        public UpcomingJob(long jobId, string type, long targetId, long dueTime, long wallDueUtc)
        {
            JobId = jobId;
            Type = type;
            TargetId = targetId;
            DueTime = dueTime;
            WallDueUtc = wallDueUtc;
        }
    }
}
