using Sim.Core.Business;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Founds a business on an acre you own (spec 016 §3.3): the capital moves from your cash into its books, it buys its building and
    /// equipment, and it opens when built (24 h for the mill), run by you or by a worker you hire on the board (spec 015).
    /// </summary>
    public sealed class FoundBusiness : TimedCommand
    {
        public string TypeId { get; }
        public int TileId { get; }
        public long Capital { get; }
        public bool RunYourself { get; }

        /// <summary>A business loan from the co-op bank (spec 016 §3.8), or null.</summary>
        public Financing Financing { get; }

        public FoundBusiness(long at, string typeId, int tileId, long capital, bool runYourself, Financing financing = null) : base(at)
        {
            TypeId = typeId;
            TileId = tileId;
            Capital = capital;
            RunYourself = runYourself;
            Financing = financing;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return BusinessRules.Found(sim, TypeId, TileId, Capital, RunYourself, Financing); }
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
            var t = BusinessRules.TypeOf(sim, b);
            if (OpenHour < 0 || CloseHour > 24 || OpenHour >= CloseHour || Fee < Maths.Fixed.Zero || Fee > Maths.Fixed.FromInt(100) || OwnPerDay < 0
                || CloseHour - OpenHour > t.CloseHour - t.OpenHour + sim.Config.Game.People.Work.LongDayHours) // a long day at most (spec 015 §3.4)
                return CommandResult.Reject(RejectReason.InvalidArgument);
            b.OpenHour = OpenHour;
            b.CloseHour = CloseHour;
            b.Fee = Fee;
            b.OwnPerDay = OwnPerDay;
            b.BuyFromMarket = BuyFromMarket;
            return CommandResult.Ok;
        }
    }

    /// <summary>The day's mix (spec 016 §3.9c): grams a day to make of one of a bakery's or dairy's products.</summary>
    public sealed class SetMix : TimedCommand
    {
        public long BusinessId { get; }
        public string Good { get; }
        public long GramsPerDay { get; }

        public SetMix(long at, long businessId, string good, long gramsPerDay) : base(at)
        {
            BusinessId = businessId;
            Good = good;
            GramsPerDay = gramsPerDay;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            int g = sim.Config.Game.Market.IndexOf(Good);
            if (g < 0 || !Business.BusinessRules.TypeOf(sim, b).Makes(g) || GramsPerDay < 0 || GramsPerDay > 100_000_000)
                return CommandResult.Reject(RejectReason.InvalidArgument);
            b.Mix[g] = GramsPerDay;
            return CommandResult.Ok;
        }
    }

    /// <summary>Puts more of your cash into a business (spec 016 §3.2): the same entries as founding.</summary>
    public sealed class PutCash : TimedCommand
    {
        public long BusinessId { get; }
        public long Amount { get; }

        public PutCash(long at, long businessId, long amount) : base(at)
        {
            BusinessId = businessId;
            Amount = amount;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return BusinessBooks.PutCash(sim, BusinessId, Amount); }
    }

    /// <summary>Takes cash out of a business (drawings, spec 016 §3.2): your capital back first, the rest your income.</summary>
    public sealed class TakeCash : TimedCommand
    {
        public long BusinessId { get; }
        public long Amount { get; }

        public TakeCash(long at, long businessId, long amount) : base(at)
        {
            BusinessId = businessId;
            Amount = amount;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return BusinessBooks.TakeCash(sim, BusinessId, Amount); }
    }

    /// <summary>Closes a business (spec 016 §3.10): everything sold, its debts repaid, what it still owes your loan, the rest to you.</summary>
    public sealed class CloseBusiness : TimedCommand
    {
        public long BusinessId { get; }

        public CloseBusiness(long at, long businessId) : base(at) { BusinessId = businessId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            BusinessBooks.CloseDown(sim, b);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Lets a business's worker go (spec 015 §3.9): they keep the season's pay; after their first season, severance of 10% of the
    /// wage from the business's cash. When you run it yourself, you stop. Either way it waits for a hire, or for you.
    /// </summary>
    public sealed class LetWorkerGo : TimedCommand
    {
        public long BusinessId { get; }

        public LetWorkerGo(long at, long businessId) : base(at) { BusinessId = businessId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Worker == WorkerKind.None) return CommandResult.Reject(RejectReason.NoWorker);
            People.Worker w = sim.State.People?.WorkerAt(b.Id);
            long severance = 0;
            if (w != null && w.SeasonsWorked > 0)
            {
                severance = System.Math.Min(Finance.Money.MulRate(w.Wage, sim.Config.Game.People.Hand.SeveranceShare), BusinessBooks.CanSpend(sim, b));
                if (severance > 0 && BusinessBooks.Draw(sim, b, severance))
                    b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.severance", "business:" + b.Id,
                        new Finance.Posting(Finance.Account.HiredLabour, severance), new Finance.Posting(Finance.Account.Cash, -severance));
            }
            Farm.FarmSystem.Accrue(sim);
            BusinessRules.LoseWorker(sim, b);
            b.Worker = WorkerKind.None;
            Farm.FarmSystem.Refresh(sim); // your hours back on the farm
            if (w != null) sim.Emit(new Events.WorkerLeft(sim.State.Clock.SimTime, b.Id, w.Id, severance));
            return CommandResult.Ok;
        }
    }

    /// <summary>You run a business with nobody hired yourself (spec 016 §3.6): no wage, its hours come off your farm work.</summary>
    public sealed class RunBusinessYourself : TimedCommand
    {
        public long BusinessId { get; }

        public RunBusinessYourself(long at, long businessId) : base(at) { BusinessId = businessId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Worker != WorkerKind.None) return CommandResult.Reject(RejectReason.AlreadyStaffed);
            Farm.FarmSystem.Accrue(sim);
            b.Worker = WorkerKind.Founder;
            Farm.FarmSystem.Refresh(sim);
            return CommandResult.Ok;
        }
    }
}
