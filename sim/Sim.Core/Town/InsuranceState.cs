using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>
    /// One season policy (spec 027 §3.6): switched on (<see cref="On"/>), paid at a close for the season that begins (<see cref="PaidSeason"/>), and the time its cover has run
    /// unbroken from (<see cref="Since"/>: a fire signalled before it is not insured). It covers only while the season it was paid for is the one running.
    /// </summary>
    public sealed class Policy
    {
        public bool On { get; internal set; }

        /// <summary>The season (period) whose premium was paid, −1 if none.</summary>
        public long PaidSeason { get; internal set; } = -1;
        public long Since { get; internal set; }

        /// <summary>For a business policy, the business.</summary>
        public long BusinessId { get; internal set; }

        public bool Covers(long period) { return On && PaidSeason == period; }

        internal Policy Clone() { return (Policy)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(On);
            h.WriteLong(PaidSeason);
            h.WriteLong(Since);
            h.WriteLong(BusinessId);
        }

        internal static Policy ReadFrom(SaveReader r) { return new Policy { On = r.ReadBool(), PaidSeason = r.ReadLong(), Since = r.ReadLong(), BusinessId = r.ReadLong() }; }
    }

    /// <summary>A claim paid, kept for the Money page's last few (spec 027 §7): which policy, what hit (an event's definition + 1, 0 the season's weather, −1 pests), what, and how much.</summary>
    public readonly struct Claim
    {
        public const int CropPolicy = 0, PropertyPolicy = 1, BusinessPolicy = 2;

        public int Policy { get; }
        public int Cause { get; }

        /// <summary>The crop's definition, the building or the business.</summary>
        public long Target { get; }
        public long Amount { get; }
        public long At { get; }

        /// <summary>For a business claim, the days shut it paid for (0 for the repair share).</summary>
        public int Days { get; }

        public Claim(int policy, int cause, long target, long amount, long at, int days = 0) { Policy = policy; Cause = cause; Target = target; Amount = amount; At = at; Days = days; }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(Policy);
            h.WriteInt(Cause);
            h.WriteLong(Target);
            h.WriteLong(Amount);
            h.WriteLong(At);
            h.WriteInt(Days);
        }

        internal static Claim ReadFrom(SaveReader r) { return new Claim(r.ReadInt(), r.ReadInt(), r.ReadLong(), r.ReadLong(), r.ReadLong(), r.ReadInt()); }
    }

    /// <summary>
    /// Insurance (spec 027 §3.5–§3.6, save schema 22): crop insurance's switch, the property policy, each business's policy and the last claims. A town that never switched
    /// anything on has the default and hashes as before.
    /// </summary>
    public sealed class InsuranceState
    {
        public bool CropOn { get; internal set; }
        public Policy Property { get; internal set; } = new Policy();
        public List<Policy> Businesses { get; internal set; } = new List<Policy>();
        public List<Claim> Claims { get; internal set; } = new List<Claim>();

        /// <summary>Premiums paid and claims received, all time (the Money page's glance).</summary>
        public long PremiumsPaid { get; internal set; }
        public long ClaimsReceived { get; internal set; }

        internal bool IsDefault { get { return !CropOn && !Property.On && Property.PaidSeason < 0 && Businesses.Count == 0 && Claims.Count == 0 && PremiumsPaid == 0 && ClaimsReceived == 0; } }

        public Policy BusinessPolicy(long business) { return Businesses.Find(x => x.BusinessId == business); }

        internal Policy BusinessPolicyOrNew(long business)
        {
            Policy p = BusinessPolicy(business);
            if (p == null) { p = new Policy { BusinessId = business }; Businesses.Add(p); }
            return p;
        }

        internal InsuranceState Clone()
        {
            var c = (InsuranceState)MemberwiseClone();
            c.Property = Property.Clone();
            c.Businesses = Businesses.ConvertAll(x => x.Clone());
            c.Claims = new List<Claim>(Claims);
            return c;
        }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(CropOn);
            Property.WriteTo(h);
            h.WriteInt(Businesses.Count);
            foreach (Policy p in Businesses) p.WriteTo(h);
            h.WriteInt(Claims.Count);
            foreach (Claim c in Claims) c.WriteTo(h);
            h.WriteLong(PremiumsPaid);
            h.WriteLong(ClaimsReceived);
        }

        internal static InsuranceState ReadFrom(SaveReader r)
        {
            var s = new InsuranceState { CropOn = r.ReadBool(), Property = Policy.ReadFrom(r) };
            int n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Businesses.Add(Policy.ReadFrom(r));
            n = r.ReadInt();
            for (int i = 0; i < n; i++) s.Claims.Add(Claim.ReadFrom(r));
            s.PremiumsPaid = r.ReadLong();
            s.ClaimsReceived = r.ReadLong();
            return s;
        }
    }
}
