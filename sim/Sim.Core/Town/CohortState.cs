using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Town
{
    /// <summary>The Township gate's goals (spec 023 §3.4), as bits of <see cref="TownshipState.GateMet"/>.</summary>
    public static class TownshipGoal
    {
        public const int Households = 1, Clinic = 2, Road = 4, Businesses = 8, Literacy = 16, All = 31;
    }

    /// <summary>
    /// The old village as one aggregate record (spec 023 §3.5, save schema 20): it joins at the Township's unlock. Never citizens: no cards,
    /// no portraits. Its happiness follows the town's real coverage; a household leaves after <c>leaveCloses</c> closes under <c>leaveBelow</c>.
    /// </summary>
    public sealed class CohortState
    {
        public bool Joined { get; internal set; }
        public long JoinedAt { get; internal set; }

        /// <summary>Households now (30 at the join, fewer as they leave).</summary>
        public int Households { get; internal set; }

        /// <summary>The households that have left.</summary>
        public int Left { get; internal set; }

        public Fixed Happiness { get; internal set; }

        /// <summary>Season closes in a row under the leaving line.</summary>
        public int LowCloses { get; internal set; }

        /// <summary>The house tax it paid at the last two closes, from leisure and savings before food, and what it kept in savings.</summary>
        public long TaxThis { get; internal set; }
        public long TaxLast { get; internal set; }
        public long Savings { get; internal set; }

        /// <summary>The needs met at the last close (survival, security, belonging, growth), for the Town sheet's *Why?*.</summary>
        public Fixed Survival { get; internal set; }
        public Fixed Security { get; internal set; }
        public Fixed Belonging { get; internal set; }
        public Fixed Growth { get; internal set; }

        internal CohortState Clone() { return (CohortState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(Joined);
            h.WriteLong(JoinedAt);
            h.WriteInt(Households);
            h.WriteInt(Left);
            h.WriteFixed(Happiness);
            h.WriteInt(LowCloses);
            h.WriteLong(TaxThis);
            h.WriteLong(TaxLast);
            h.WriteLong(Savings);
            h.WriteFixed(Survival); h.WriteFixed(Security); h.WriteFixed(Belonging); h.WriteFixed(Growth);
        }

        internal static CohortState ReadFrom(SaveReader r)
        {
            return new CohortState
            {
                Joined = r.ReadBool(), JoinedAt = r.ReadLong(), Households = r.ReadInt(), Left = r.ReadInt(), Happiness = r.ReadFixed(), LowCloses = r.ReadInt(),
                TaxThis = r.ReadLong(), TaxLast = r.ReadLong(), Savings = r.ReadLong(),
                Survival = r.ReadFixed(), Security = r.ReadFixed(), Belonging = r.ReadFixed(), Growth = r.ReadFixed(),
            };
        }
    }
}
