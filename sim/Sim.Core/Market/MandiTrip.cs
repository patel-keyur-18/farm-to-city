using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Market
{
    /// <summary>One good and quantity carted to the mandi (spec 004 §3.6).</summary>
    public readonly struct MandiLoad
    {
        public string GoodId { get; }
        public long Quantity { get; }

        public MandiLoad(string goodId, long quantity)
        {
            GoodId = goodId;
            Quantity = quantity;
        }
    }

    /// <summary>A trip to the town mandi under way (spec 004 §4): at most one.</summary>
    public sealed class MandiTrip
    {
        /// <summary>(good index, quantity) in the order given.</summary>
        internal List<KeyValuePair<int, long>> Loads { get; } = new List<KeyValuePair<int, long>>();
        public long DepartedAt { get; internal set; }
        public long ReturnAt { get; internal set; }
        public long JobId { get; internal set; }

        /// <summary>The cart hire paid at departure (1/100 coin).</summary>
        public long CartHire { get; internal set; }

        internal MandiTrip Clone()
        {
            var copy = new MandiTrip { DepartedAt = DepartedAt, ReturnAt = ReturnAt, JobId = JobId, CartHire = CartHire };
            copy.Loads.AddRange(Loads);
            return copy;
        }

        internal void WriteTo(StateHasher h)
        {
            h.WriteInt(Loads.Count);
            foreach (KeyValuePair<int, long> l in Loads) { h.WriteInt(l.Key); h.WriteLong(l.Value); }
            h.WriteLong(DepartedAt);
            h.WriteLong(ReturnAt);
            h.WriteLong(JobId);
            h.WriteLong(CartHire);
        }
    }
}
