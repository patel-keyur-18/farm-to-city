using System;
using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Scheduling
{
    /// <summary>
    /// A binary min-heap of <see cref="ScheduledItem"/>s (spec 001 §3.3). Like a
    /// <c>PriorityQueue</c> in Java, but with a total, deterministic order.
    /// </summary>
    public sealed class Scheduler
    {
        private readonly List<ScheduledItem> _heap = new List<ScheduledItem>();

        /// <summary>The sequence number the next scheduled item gets.</summary>
        public long NextSequence { get; private set; }

        public int Count { get { return _heap.Count; } }

        internal ScheduledItem Schedule(long dueTime, ItemClass cls, SystemOrder system, long entityId, long jobId)
        {
            var item = new ScheduledItem(dueTime, cls, system, entityId, NextSequence++, jobId);
            _heap.Add(item);
            SiftUp(_heap.Count - 1);
            return item;
        }

        internal ScheduledItem Peek()
        {
            if (_heap.Count == 0) throw new InvalidOperationException("The scheduler is empty.");
            return _heap[0];
        }

        internal ScheduledItem Pop()
        {
            ScheduledItem top = Peek();
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            if (_heap.Count > 0) SiftDown(0);
            return top;
        }

        /// <summary>All items in processing order (a sorted copy; the heap is unchanged).</summary>
        public List<ScheduledItem> InOrder()
        {
            var copy = new List<ScheduledItem>(_heap);
            copy.Sort();
            return copy;
        }

        internal Scheduler Clone()
        {
            var c = new Scheduler { NextSequence = NextSequence };
            c._heap.AddRange(_heap);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(NextSequence);
            List<ScheduledItem> items = InOrder();
            h.WriteInt(items.Count);
            foreach (ScheduledItem i in items)
            {
                h.WriteLong(i.DueTime);
                h.WriteInt((int)i.Class);
                h.WriteInt((int)i.System);
                h.WriteLong(i.EntityId);
                h.WriteLong(i.Sequence);
                h.WriteLong(i.JobId);
            }
        }

        internal static Scheduler ReadFrom(SaveReader r)
        {
            var s = new Scheduler { NextSequence = r.ReadLong() };
            int n = r.ReadCount();
            for (int i = 0; i < n; i++)
            {
                long due = r.ReadLong();
                var cls = (ItemClass)r.ReadInt();
                var system = (SystemOrder)r.ReadInt();
                long entity = r.ReadLong();
                long seq = r.ReadLong();
                s._heap.Add(new ScheduledItem(due, cls, system, entity, seq, r.ReadLong()));
            }
            // Written in processing order, and a sorted list is already a valid min-heap.
            for (int i = 1; i < s._heap.Count; i++)
                if (s._heap[i].CompareTo(s._heap[i - 1]) <= 0) throw new SaveFormatException("scheduler items out of order");
            return s;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (_heap[i].CompareTo(_heap[parent]) >= 0) break;
                Swap(i, parent);
                i = parent;
            }
        }

        private void SiftDown(int i)
        {
            int n = _heap.Count;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, smallest = i;
                if (left < n && _heap[left].CompareTo(_heap[smallest]) < 0) smallest = left;
                if (right < n && _heap[right].CompareTo(_heap[smallest]) < 0) smallest = right;
                if (smallest == i) return;
                Swap(i, smallest);
                i = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            ScheduledItem t = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = t;
        }
    }
}
