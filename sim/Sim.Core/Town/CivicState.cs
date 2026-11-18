using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    public enum PostStatus
    {
        /// <summary>Nobody: the post wasn't advertised, was given up or he left.</summary>
        Vacant = 0,

        /// <summary>Advertised; a villager takes it at <see cref="CivicPost.ArrivesAt"/>.</summary>
        Coming = 1,
        InPost = 2,
    }

    /// <summary>One civic building and its post (spec 023 §3.7, save schema 20): commissioned by the treasury, staffed by an appointed villager of the old village.</summary>
    public sealed class CivicPost
    {
        public bool Built { get; internal set; }
        public long BuiltAt { get; internal set; }

        /// <summary>The cost, indexed when commissioned.</summary>
        public long Cost { get; internal set; }

        public PostStatus Status { get; internal set; }
        public long ArrivesAt { get; internal set; }
        public long JobId { get; internal set; }

        /// <summary>The villager's name (an index into the names) while he holds the post or after he left; and how many have held it.</summary>
        public int StaffName { get; internal set; }
        public int Hired { get; internal set; }
        public long SinceSeason { get; internal set; }

        /// <summary>Season closes in a row his wage went unpaid in full (spec 023 §3.7: he leaves at <c>leaveCloses</c>).</summary>
        public int UnpaidCloses { get; internal set; }

        /// <summary>The wage the old village received at the last close, and the one before.</summary>
        public long WageLast { get; internal set; }
        public long WageBefore { get; internal set; }

        internal bool IsDefault { get { return !Built && Status == PostStatus.Vacant && Hired == 0; } }

        internal CivicPost Clone() { return (CivicPost)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(Built);
            h.WriteLong(BuiltAt);
            h.WriteLong(Cost);
            h.WriteInt((int)Status);
            h.WriteLong(ArrivesAt);
            h.WriteLong(JobId);
            h.WriteInt(StaffName);
            h.WriteInt(Hired);
            h.WriteLong(SinceSeason);
            h.WriteInt(UnpaidCloses);
            h.WriteLong(WageLast);
            h.WriteLong(WageBefore);
        }

        internal static CivicPost ReadFrom(SaveReader r)
        {
            return new CivicPost
            {
                Built = r.ReadBool(), BuiltAt = r.ReadLong(), Cost = r.ReadLong(), Status = (PostStatus)r.ReadInt(), ArrivesAt = r.ReadLong(), JobId = r.ReadLong(),
                StaffName = r.ReadInt(), Hired = r.ReadInt(), SinceSeason = r.ReadLong(), UnpaidCloses = r.ReadInt(), WageLast = r.ReadLong(), WageBefore = r.ReadLong(),
            };
        }
    }
}
