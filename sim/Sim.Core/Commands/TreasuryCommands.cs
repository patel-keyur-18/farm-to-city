namespace Sim.Core.Commands
{
    /// <summary>
    /// A gift to the town (spec 021 §3.8): yours Dr 5260 Gifts to the village, Cr Cash, outside farm profit; the town's Dr Cash, Cr 4670.
    /// At least <c>giftMinimum</c> and no more than your cash. It is never repaid.
    /// </summary>
    public sealed class GiveToTreasury : TimedCommand
    {
        public long Amount { get; }

        public GiveToTreasury(long at, long amount) : base(at) { Amount = amount; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Town.TreasuryRules.Give(sim, Amount);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }

    /// <summary>
    /// The treasury commissions a capital work (spec 021 §3.7) from its own cash, to a supplier: never to you or your businesses. The asset
    /// is kept at cost and depreciated at each close.
    /// </summary>
    public sealed class CommissionWorks : TimedCommand
    {
        public string WorkId { get; }

        public CommissionWorks(long at, string workId) : base(at) { WorkId = workId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = Town.TreasuryRules.Commission(sim, WorkId);
            return r == RejectReason.None ? CommandResult.Ok : CommandResult.Reject(r);
        }
    }
}
