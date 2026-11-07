using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Market
{
    /// <summary>Why your godown wasn't let at a close (spec 013 §3.4, ADR 0087).</summary>
    public enum LetOutcome
    {
        /// <summary>Not set to let, or let.</summary>
        None = 0,

        /// <summary>No trader took it this season (the seeded chance).</summary>
        NoTenant = 1,

        /// <summary>Your goods needed it: they didn't fit the rented space.</summary>
        YourGoods = 2,
    }

    /// <summary>
    /// The godown in the mandi town (spec 013 §3.4, ADR 0087). Owning it is a building with no tile (the books and depreciation
    /// are the buildings'); what each place holds is worked out from the stock (<see cref="GodownRules.Dry"/>), so only the
    /// space rented and the letting are kept here.
    /// </summary>
    public sealed class GodownState
    {
        /// <summary>Whole tonnes rented in the warehouse this season, and from the next close.</summary>
        public int RentedTonnes { get; internal set; }
        public int RentNextTonnes { get; internal set; }

        /// <summary>Let your godown from the next close (renewed each close until switched off), and whether it is let this season.</summary>
        public bool LetNext { get; internal set; }
        public bool LetNow { get; internal set; }

        /// <summary>Why it wasn't let at the last close, when it was set to let.</summary>
        public LetOutcome LastOutcome { get; internal set; }

        internal GodownState Clone() { return (GodownState)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt(RentedTonnes);
            h.WriteInt(RentNextTonnes);
            h.WriteBool(LetNext);
            h.WriteBool(LetNow);
            h.WriteInt((int)LastOutcome);
        }

        internal static GodownState ReadFrom(SaveReader r)
        {
            var s = new GodownState { RentedTonnes = r.ReadInt(), RentNextTonnes = r.ReadInt(), LetNext = r.ReadBool(), LetNow = r.ReadBool() };
            int outcome = r.ReadInt();
            if (outcome < 0 || outcome > 2) throw new SaveFormatException("a let outcome of " + outcome);
            s.LastOutcome = (LetOutcome)outcome;
            if (s.RentedTonnes < 0 || s.RentNextTonnes < 0) throw new SaveFormatException("negative tonnes rented");
            return s;
        }
    }
}
