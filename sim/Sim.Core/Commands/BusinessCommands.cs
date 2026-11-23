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

        /// <summary>A shop's pitch in the village square (spec 017 §3.4, 0-based; <see cref="TileId"/> unused), or −1 for your acre.</summary>
        public int Pitch { get; }

        public FoundBusiness(long at, string typeId, int tileId, long capital, bool runYourself, Financing financing = null, int pitch = -1) : base(at)
        {
            TypeId = typeId;
            TileId = tileId;
            Capital = capital;
            RunYourself = runYourself;
            Financing = financing;
            Pitch = pitch;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return BusinessRules.Found(sim, TypeId, TileId, Capital, RunYourself, Financing, Pitch); }
    }

    /// <summary>A shop's price as a share of the reference (spec 017 §3.1): one of the presets (the reference, 10% below, 10% or 20% above).</summary>
    public sealed class SetShop : TimedCommand
    {
        public long BusinessId { get; }
        public Maths.Fixed PriceFactor { get; }

        /// <summary>1 standard, 2 good (§3.5, US-121).</summary>
        public int Quality { get; }

        public SetShop(long at, long businessId, Maths.Fixed priceFactor, int quality = 1) : base(at)
        {
            BusinessId = businessId;
            PriceFactor = priceFactor;
            Quality = quality;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null || !BusinessRules.TypeOf(sim, b).IsShop) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            bool preset = false;
            foreach (Maths.Fixed f in sim.Config.Game.Shops.PricePresets) if (f == PriceFactor) preset = true;
            if (!preset || Quality < 1 || Quality > 2) return CommandResult.Reject(RejectReason.InvalidArgument);
            ShopRules.SetLook(sim, b, PriceFactor, Quality);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// What a stall puts out a day of a good (spec 017 §5): each morning at 6:00 it takes up to that much, produce from your store, your
    /// products from your businesses; the tea stall's snacks the same way.
    /// </summary>
    public sealed class SetStallGoods : TimedCommand
    {
        public long BusinessId { get; }
        public string Good { get; }
        public long GramsPerDay { get; }

        public SetStallGoods(long at, long businessId, string good, long gramsPerDay) : base(at)
        {
            BusinessId = businessId;
            Good = good;
            GramsPerDay = gramsPerDay;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Business.Business b = sim.State.Businesses?.Find(BusinessId);
            if (b == null || !ShopRules.Stocked(BusinessRules.TypeOf(sim, b))) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.BusinessClosed);
            int g = sim.Config.Game.Market.IndexOf(Good);
            if (g < 0) return CommandResult.Reject(RejectReason.UnknownGood);
            if (!BusinessRules.TypeOf(sim, b).Sells(g)) return CommandResult.Reject(RejectReason.NotProduce);
            if (GramsPerDay < 0 || GramsPerDay > 100_000_000) return CommandResult.Reject(RejectReason.InvalidArgument);
            b.PutOut[g] = GramsPerDay;
            return CommandResult.Ok;
        }
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
            if (sim.State.People?.ManagerAt(b.Id) != null) return CommandResult.Reject(RejectReason.HasManager); // he works it: let him go instead
            People.Worker w = sim.State.People?.WorkerAt(b.Id);
            long severance = 0;
            if (w != null && w.SeasonsWorked > 0)
            {
                severance = System.Math.Min(Finance.Money.MulRate(w.Wage, sim.Config.Game.People.Hand.SeveranceShare), BusinessBooks.CanSpend(sim, b));
                if (severance > 0 && BusinessBooks.Draw(sim, b, severance))
                    b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.severance", "business:" + b.Id,
                        new Finance.Posting(Finance.Account.HiredLabour, severance), new Finance.Posting(Finance.Account.Cash, -severance));
            }
            if (w != null)
                People.HrRules.Repute(sim, w.SeasonsWorked == 0 ? sim.Config.Game.People.Reputation.ProbationLetGo : severance > 0 ? sim.Config.Game.People.Reputation.Severance : 0);
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
            if (sim.State.People?.ManagerAt(b.Id) != null) return CommandResult.Reject(RejectReason.HasManager); // his to run: let him go first
            Farm.FarmSystem.Accrue(sim);
            b.Worker = WorkerKind.Founder;
            Farm.FarmSystem.Refresh(sim);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// The payment term a seller offers one buyer (spec 028 §3.1): a business of yours (<see cref="BuyerId"/>) or a village shop (<see cref="BuyerRival"/>, its content id,
    /// with <see cref="BuyerId"/> 0). The term is one of <c>invoices.json</c>'s ids; it applies to sales from then on.
    /// </summary>
    public sealed class SetTerms : TimedCommand
    {
        public long SellerId { get; }
        public long BuyerId { get; }
        public string BuyerRival { get; }
        public string Term { get; }

        public SetTerms(long at, long sellerId, long buyerId, string buyerRival, string term) : base(at)
        {
            SellerId = sellerId;
            BuyerId = buyerId;
            BuyerRival = buyerRival ?? "";
            Term = term;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return InvoiceRules.SetTerms(sim, SellerId, BuyerId, BuyerRival, Term); }
    }
    /// <summary>
    /// Offers the manager's post to an applicant of the board's manager post, to run one of your businesses (spec 028 §3.2): the asked wage,
    /// 10% less or 10% more, the base wage for the rest of the season prepaid from the business's cash. One manager a business.
    /// </summary>
    public sealed class HireManager : TimedCommand
    {
        public long ApplicantId { get; }
        public long BusinessId { get; }
        public People.OfferLevel Level { get; }

        public HireManager(long at, long applicantId, long businessId, People.OfferLevel level = People.OfferLevel.Asked) : base(at)
        {
            ApplicantId = applicantId;
            BusinessId = businessId;
            Level = level;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            if (!ManagerRules.On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            People.Applicant a = sim.State.People.Applicant(ApplicantId);
            if (a == null || a.Place != People.HiringRules.ManagerPlace) return CommandResult.Reject(RejectReason.UnknownApplicant);
            Business.Business b = sim.State.Businesses.Find(BusinessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (sim.State.People.ManagerAt(b.Id) != null) return CommandResult.Reject(RejectReason.HasManager);
            if (b.Worker == WorkerKind.Citizen || b.Worker == WorkerKind.Neighbour) return CommandResult.Reject(RejectReason.AlreadyStaffed); // he works it himself: let your worker go first
            long wage = People.HiringRules.OfferWage(sim, a, Level, b.Id);
            if (BusinessBooks.CanSpend(sim, b) < People.PeopleRules.RestOfSeason(sim, wage)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            if (a.CitizenId != 0 && !People.HouseholdRules.WorksOutside(sim, sim.State.Citizens.Citizen(a.CitizenId))) return CommandResult.Reject(RejectReason.UnknownApplicant);
            People.HiringRules.Offer(sim, a, Level, wage, false, b);
            return CommandResult.Ok;
        }
    }

    /// <summary>Lets a business's manager go (spec 028 §3.2): he keeps the season's pay; after his first season, severance of 10% of his wage; you can run it yourself or hire another.</summary>
    public sealed class DismissManager : TimedCommand
    {
        public long BusinessId { get; }

        public DismissManager(long at, long businessId) : base(at) { BusinessId = businessId; }

        protected override CommandResult ApplyAt(Simulation sim) { return ManagerRules.Dismiss(sim, BusinessId); }
    }

    /// <summary>What you judge a manager on at each close (spec 028 §3.2): the season's profit, or its sales.</summary>
    public sealed class SetManagerTarget : TimedCommand
    {
        public long BusinessId { get; }
        public People.ManagerTarget Target { get; }

        public SetManagerTarget(long at, long businessId, People.ManagerTarget target) : base(at)
        {
            BusinessId = businessId;
            Target = target;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return ManagerRules.SetTarget(sim, BusinessId, Target); }
    }
    /// <summary>
    /// Lists a business for sale (spec 028 §3.3): a buyer completes in 3 to 6 days at 90 to 105% of its worth then, never under what it holds; rushed, in a day at 85%.
    /// It runs until the day it closes.
    /// </summary>
    public sealed class ListBusiness : TimedCommand
    {
        public long BusinessId { get; }
        public bool Rushed { get; }

        public ListBusiness(long at, long businessId, bool rushed) : base(at)
        {
            BusinessId = businessId;
            Rushed = rushed;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return ExitRules.List(sim, BusinessId, Rushed); }
    }

    /// <summary>Takes a business off the market before a buyer completes (spec 028 §3.3).</summary>
    public sealed class CancelBusinessSale : TimedCommand
    {
        public long BusinessId { get; }

        public CancelBusinessSale(long at, long businessId) : base(at) { BusinessId = businessId; }

        protected override CommandResult ApplyAt(Simulation sim) { return ExitRules.Cancel(sim, BusinessId); }
    }

    /// <summary>Takes an investor's cash or a partner's savings into a business for a share of it (spec 028 §3.4).</summary>
    public sealed class AcceptDeal : TimedCommand
    {
        public long OfferId { get; }

        public AcceptDeal(long at, long offerId) : base(at) { OfferId = offerId; }

        protected override CommandResult ApplyAt(Simulation sim) { return OwnerRules.Accept(sim, OfferId); }
    }

    /// <summary>Says no to an offer: it costs nothing and may come again (spec 028 §3.4).</summary>
    public sealed class DeclineDeal : TimedCommand
    {
        public long OfferId { get; }

        public DeclineDeal(long at, long offerId) : base(at) { OfferId = offerId; }

        protected override CommandResult ApplyAt(Simulation sim) { return OwnerRules.Decline(sim, OfferId); }
    }

    /// <summary>Sells the business today to the rival who offered for it (spec 028 §3.4): the outside shares are paid first, as in any sale (§3.3).</summary>
    public sealed class AcceptBuyOut : TimedCommand
    {
        public long OfferId { get; }

        public AcceptBuyOut(long at, long offerId) : base(at) { OfferId = offerId; }

        protected override CommandResult ApplyAt(Simulation sim) { return OwnerRules.AcceptBuyOut(sim, OfferId); }
    }

    /// <summary>Buys an outside owner's share back at the business's worth × the share (spec 028 §3.4): an investor's after four seasons, a partner's any time.</summary>
    public sealed class BuyBackShare : TimedCommand
    {
        public long BusinessId { get; }
        public StakeKind Kind { get; }

        public BuyBackShare(long at, long businessId, StakeKind kind) : base(at)
        {
            BusinessId = businessId;
            Kind = kind;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return OwnerRules.BuyBack(sim, BusinessId, Kind); }
    }

    /// <summary>Gives (or stops giving) one benefit to all of an employer's workers (spec 028 §3.8): a business's id, or 0 for your farm's hands.</summary>
    public sealed class SetBenefit : TimedCommand
    {
        public long Employer { get; }
        public People.Benefit Benefit { get; }
        public bool On { get; }

        public SetBenefit(long at, long employer, People.Benefit benefit, bool on) : base(at)
        {
            Employer = employer;
            Benefit = benefit;
            On = on;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return People.BenefitRules.Set(sim, Employer, Benefit, On); }
    }

    /// <summary>Starts or stops a business's night shift (spec 028 §3.8).</summary>
    public sealed class SetNightShift : TimedCommand
    {
        public long BusinessId { get; }
        public bool On { get; }

        public SetNightShift(long at, long businessId, bool on) : base(at)
        {
            BusinessId = businessId;
            On = on;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return On ? NightRules.Start(sim, BusinessId) : NightRules.Stop(sim, BusinessId); }
    }

    /// <summary>Sets a tuition centre's fee preset for the next enrolment (spec 028 §3.5): the pupils already in keep the fee they came at.</summary>
    public sealed class SetTuitionFee : TimedCommand
    {
        public long BusinessId { get; }
        public int Preset { get; }

        public SetTuitionFee(long at, long businessId, int preset) : base(at)
        {
            BusinessId = businessId;
            Preset = preset;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return TuitionRules.SetFee(sim, BusinessId, Preset); }
    }

    /// <summary>Switches one of a restaurant's dishes on or off (spec 028 §3.7): a dish off hands its diners to the rest, and the last cannot be switched off.</summary>
    public sealed class SetDish : TimedCommand
    {
        public long BusinessId { get; }
        public int Dish { get; }
        public bool On { get; }

        public SetDish(long at, long businessId, int dish, bool on) : base(at)
        {
            BusinessId = businessId;
            Dish = dish;
            On = on;
        }

        protected override CommandResult ApplyAt(Simulation sim) { return MealsRules.SetDish(sim, BusinessId, Dish, On); }
    }
}
