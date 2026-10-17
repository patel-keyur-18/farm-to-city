using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Scheduling
{
    /// <summary>A timed piece of work (spec 001 §4). Concrete job types come from later specs.</summary>
    public sealed class Job
    {
        public long Id { get; internal set; }
        public string Type { get; internal set; }
        public long TargetId { get; internal set; }
        public long StartTime { get; internal set; }
        public long DueTime { get; internal set; }
        public JobState State { get; internal set; }

        internal Job Clone()
        {
            return new Job { Id = Id, Type = Type, TargetId = TargetId, StartTime = StartTime, DueTime = DueTime, State = State };
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(Id);
            h.WriteString(Type);
            h.WriteLong(TargetId);
            h.WriteLong(StartTime);
            h.WriteLong(DueTime);
            h.WriteInt((int)State);
        }
    }

    /// <summary>
    /// Runs a job type when it falls due. Like a Spring <c>@Component</c> registered per job type: the
    /// simulation looks the handler up by <see cref="JobType"/>.
    /// </summary>
    public interface IJobHandler
    {
        string JobType { get; }

        /// <summary>The system this job type belongs to; it orders same-time jobs (spec 001 §3.3).</summary>
        SystemOrder System { get; }

        /// <summary>
        /// Called once when the job is due. The job is already <see cref="JobState.Ready"/>; call
        /// <c>Simulation.CompleteJob</c> to finish it.
        /// </summary>
        void OnDue(Simulation sim, Job job);
    }

    /// <summary>Jobs kept in id order (never hash order; CLAUDE.md determinism rules).</summary>
    public sealed class JobTable
    {
        private readonly List<Job> _jobs = new List<Job>();

        public long NextId { get; private set; } = 1;

        /// <summary>Live jobs (Running or Ready) in id order.</summary>
        public IReadOnlyList<Job> All { get { return _jobs; } }

        internal Job Add(string type, long targetId, long startTime, long dueTime)
        {
            var job = new Job { Id = NextId++, Type = type, TargetId = targetId, StartTime = startTime, DueTime = dueTime, State = JobState.Running };
            _jobs.Add(job); // ids only grow, so appending keeps id order
            return job;
        }

        public Job Find(long id)
        {
            int i = IndexOf(id);
            return i < 0 ? null : _jobs[i];
        }

        internal void Remove(long id)
        {
            int i = IndexOf(id);
            if (i >= 0) _jobs.RemoveAt(i);
        }

        internal JobTable Clone()
        {
            var c = new JobTable { NextId = NextId };
            foreach (Job j in _jobs) c._jobs.Add(j.Clone());
            return c;
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteLong(NextId);
            h.WriteInt(_jobs.Count);
            foreach (Job j in _jobs) j.WriteTo(h);
        }

        private int IndexOf(long id)
        {
            int lo = 0, hi = _jobs.Count - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                long midId = _jobs[mid].Id;
                if (midId == id) return mid;
                if (midId < id) lo = mid + 1; else hi = mid - 1;
            }
            return -1;
        }
    }

    /// <summary>
    /// A system that takes part in the settlement passes. Systems run in <see cref="SystemOrder"/>
    /// within each pass (spec 001 §3.3).
    /// </summary>
    public interface ISimSystem
    {
        SystemOrder Order { get; }

        /// <summary>Called once per pass (Hourly, Daily, Seasonal or Yearly) at the boundary's sim time.</summary>
        void OnPass(Simulation sim, ItemClass pass);
    }
}
