using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Business
{
    /// <summary>Who holds a share of a business besides you (spec 028 §3.4): an investor who bought it, or a partner who joined for a share of the profit.</summary>
    public enum StakeKind
    {
        Investor = 0,
        Partner = 1,
    }

    /// <summary>
    /// An outside share in a business (spec 028 §3.4, save schema 23): the cash put in, the share of its worth and profit it is, and the season it joined. It is a liability of the
    /// business (<c>OutsideEquity</c>), paid a share of each season's profit, bought back at the worth × the share, and settled first when the business is sold.
    /// </summary>
    public sealed class Stake
    {
        public StakeKind Kind { get; internal set; }

        /// <summary>An investor: his index in <see cref="Definitions.ExitDef.Investors"/>; a partner: the id of the household she is from.</summary>
        public long Who { get; internal set; }

        /// <summary>The share of the business's worth, equity and profit this is.</summary>
        public Fixed Share { get; internal set; }

        /// <summary>The cash put in, and when (the season's period and the sim time).</summary>
        public long Put { get; internal set; }
        public long Period { get; internal set; }
        public long At { get; internal set; }

        /// <summary>What was paid to it at the last close.</summary>
        public long Paid { get; internal set; }

        internal Stake Clone() { return (Stake)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteInt((int)Kind); h.WriteLong(Who); h.WriteFixed(Share); h.WriteLong(Put); h.WriteLong(Period); h.WriteLong(At); h.WriteLong(Paid);
        }

        internal static Stake ReadFrom(SaveReader r)
        {
            return new Stake { Kind = (StakeKind)r.ReadInt(), Who = r.ReadLong(), Share = r.ReadFixed(), Put = r.ReadLong(), Period = r.ReadLong(), At = r.ReadLong(), Paid = r.ReadLong() };
        }
    }

    /// <summary>What an offer is: an investor's cash for a share, a partner joining, or a rival's offer to buy the business.</summary>
    public enum DealKind
    {
        Investor = 0,
        Partner = 1,
        BuyOut = 2,
    }

    /// <summary>An offer waiting for your answer (spec 028 §3.4, save schema 23): open until it lapses, when the answer is no.</summary>
    public sealed class DealOffer
    {
        public long Id { get; internal set; }
        public DealKind Kind { get; internal set; }
        public long BusinessId { get; internal set; }

        /// <summary>An investor: his index in the content's investors; a partner: the id of her household; a buy-out: the index of the buyer's kind.</summary>
        public long Who { get; internal set; }

        /// <summary>The share it is for (a buy-out's is the share of the worth it pays); the cash it brings (a buy-out's is the price).</summary>
        public Fixed Share { get; internal set; }
        public long Cash { get; internal set; }

        public long RaisedAt { get; internal set; }
        public long ExpiresAt { get; internal set; }

        internal DealOffer Clone() { return (DealOffer)MemberwiseClone(); }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteLong(Id); h.WriteInt((int)Kind); h.WriteLong(BusinessId); h.WriteLong(Who); h.WriteFixed(Share); h.WriteLong(Cash); h.WriteLong(RaisedAt); h.WriteLong(ExpiresAt);
        }

        internal static DealOffer ReadFrom(SaveReader r)
        {
            return new DealOffer { Id = r.ReadLong(), Kind = (DealKind)r.ReadInt(), BusinessId = r.ReadLong(), Who = r.ReadLong(), Share = r.ReadFixed(), Cash = r.ReadLong(), RaisedAt = r.ReadLong(), ExpiresAt = r.ReadLong() };
        }
    }
}
