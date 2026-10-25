using Sim.Core.Business;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Founds a business on an acre you own (spec 016 §3.3): the capital moves from your cash into its books, it buys its building and
    /// equipment, and it opens when built (24 h for the mill), run by the town's best worker or by you.
    /// </summary>
    public sealed class FoundBusiness : TimedCommand
    {
        public string TypeId { get; }
        public int TileId { get; }
        public long Capital { get; }
        public bool RunYourself { get; }

        public FoundBusiness(long at, string typeId, int tileId, long capital, bool runYourself) : base(at)
        {
            TypeId = typeId;
            TileId = tileId;
            Capital = capital;
            RunYourself = runYourself;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return BusinessRules.Found(sim, TypeId, TileId, Capital, RunYourself); }
    }

    /// <summary>A business's settings (spec 016 §3.4, §3.5): open hours, the fee, your own input a day, buying at the market.</summary>
    public sealed class SetBusiness : TimedCommand
    {
        public long BusinessId { get; }
        public int OpenHour { get; }
        public int CloseHour { get; }
        public Maths.Fixed Fee { get; }
        public long OwnPerDay { get; }
        public bool BuyFromMarket { get; }

        public SetBusiness(long at, long businessId, int openHour, int closeHour, Maths.Fixed fee, long ownPerDay, bool buyFromMarket) : base(at)
        {
            BusinessId = businessId;
            OpenHour = openHour;
            CloseHour = closeHour;
            Fee = fee;
            OwnPerDay = ownPerDay;
            BuyFromMarket = buyFromMarket;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            if (OpenHour < 0 || CloseHour > 24 || OpenHour >= CloseHour || Fee < Maths.Fixed.Zero || Fee > Maths.Fixed.FromInt(100) || OwnPerDay < 0)
                return CommandResult.Reject(RejectReason.InvalidArgument);
            b.OpenHour = OpenHour;
            b.CloseHour = CloseHour;
            b.Fee = Fee;
            b.OwnPerDay = OwnPerDay;
            b.BuyFromMarket = BuyFromMarket;
            return CommandResult.Ok;
        }
    }
}
