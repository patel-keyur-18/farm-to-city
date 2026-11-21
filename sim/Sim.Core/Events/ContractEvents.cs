using Sim.Core.Maths;

namespace Sim.Core.Events
{
    /// <summary>The trader made an offer at a close (spec 027 §3.7): the crop, the quantity, the price, the expected price it is set under, and the delivery season.</summary>
    public sealed class ContractOffered : SimEvent
    {
        public long Id { get; }
        public string GoodId { get; }
        public long Quantity { get; }
        public Fixed Price { get; }
        public Fixed Expected { get; }
        public long Delivery { get; }

        public ContractOffered(long simTime, long id, string goodId, long quantity, Fixed price, Fixed expected, long delivery) : base(simTime)
        {
            Id = id; GoodId = goodId; Quantity = quantity; Price = price; Expected = expected; Delivery = delivery;
        }

        protected override string Describe() { return "ContractOffered(" + Id + ", " + GoodId + ", " + Quantity + " at " + Price + ")"; }
    }

    /// <summary>An offer was accepted and is now a contract.</summary>
    public sealed class ContractAccepted : SimEvent
    {
        public long Id { get; }
        public string GoodId { get; }
        public long Quantity { get; }
        public Fixed Price { get; }
        public long Delivery { get; }

        public ContractAccepted(long simTime, long id, string goodId, long quantity, Fixed price, long delivery) : base(simTime)
        {
            Id = id; GoodId = goodId; Quantity = quantity; Price = price; Delivery = delivery;
        }

        protected override string Describe() { return "ContractAccepted(" + Id + ", " + GoodId + ", " + Quantity + ")"; }
    }

    /// <summary>A sale to the trader filled some of a contract at the contract price; <see cref="Gain"/> is what that paid above (or below, negative) the market floor.</summary>
    public sealed class ContractFilled : SimEvent
    {
        public long Id { get; }
        public string GoodId { get; }
        public long Taken { get; }
        public long Filled { get; }
        public long Quantity { get; }
        public Fixed Price { get; }
        public long Gain { get; }

        public ContractFilled(long simTime, long id, string goodId, long taken, long filled, long quantity, Fixed price, long gain) : base(simTime)
        {
            Id = id; GoodId = goodId; Taken = taken; Filled = filled; Quantity = quantity; Price = price; Gain = gain;
        }

        protected override string Describe() { return "ContractFilled(" + Id + ", " + GoodId + ", " + Filled + "/" + Quantity + ")"; }
    }

    /// <summary>A contract's delivery season closed: what was delivered, the shortfall charged, and what the whole came to against the market (a gain, or a loss when negative).</summary>
    public sealed class ContractSettled : SimEvent
    {
        public long Id { get; }
        public string GoodId { get; }
        public long Quantity { get; }
        public long Filled { get; }
        public long Shortfall { get; }
        public long Against { get; }

        public ContractSettled(long simTime, long id, string goodId, long quantity, long filled, long shortfall, long against) : base(simTime)
        {
            Id = id; GoodId = goodId; Quantity = quantity; Filled = filled; Shortfall = shortfall; Against = against;
        }

        protected override string Describe() { return "ContractSettled(" + Id + ", " + GoodId + ", short " + Shortfall + ")"; }
    }
}
