using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;

namespace Sim.Core.Business
{
    /// <summary>What a business is worth (spec 028 §3.3) and how it got there: a year's profit, the multiple and what moved it, and what it holds.</summary>
    public sealed class Worth
    {
        /// <summary>A year's profit: the kept seasons' (up to four) scaled to a year, at least 1 coin.</summary>
        public long Profit { get; internal set; }
        public Fixed Multiple { get; internal set; }

        /// <summary>1 growth, −1 a fall, 0 neither; whether risk took a step off; whether a manager and four profitable seasons added one.</summary>
        public int Growth { get; internal set; }
        public bool Risk { get; internal set; }
        public bool Manager { get; internal set; }

        /// <summary>Seasons of books it has (at most 4).</summary>
        public int Seasons { get; internal set; }

        /// <summary>What it holds: its assets less its debts, never below 0.</summary>
        public long Book { get; internal set; }

        /// <summary>The larger of what it earns (the multiple of a year's profit) and what it holds.</summary>
        public long Value { get; internal set; }
    }

    /// <summary>
    /// Valuing, selling and branching businesses (spec 028 §3.3, ADR 0158). A sale is the close-down path at the sale's price: its people go with severance,
    /// what it holds is paid to you, the gain over what it held is business income taxed as the season's, and the acre is yours again.
    /// </summary>
    public static class ExitRules
    {
        private const long Day = ClockDef.SecondsPerDay;

        public static ExitDef Defs(Simulation sim) { return sim.Config.Game.Exit; }

        /// <summary>Valuing and selling are on from the Township, with the content.</summary>
        public static bool On(Simulation sim) { return Defs(sim) != null && sim.State.Township != null && sim.State.Businesses != null && Town.TownshipRules.Reached(sim); }

        public static ExitState State(Simulation sim) { return sim.State.Township?.Exit; }

        public static long Book(Business b) { return System.Math.Max(0, BusinessBooks.Equity(b)); }

        // ---- the worth ----

        public static Worth Valuation(Simulation sim, Business b)
        {
            ExitDef d = Defs(sim);
            var profits = new List<long>();
            foreach (BusinessReport r in b.Reports) profits.Add(BusinessBooks.NetProfit(r));
            int n = profits.Count;
            long sum = 0;
            foreach (long p in profits) sum += p;
            long year = n == 0 ? b.Plan.Profit * 4 : sum * 4 / n;
            var w = new Worth { Seasons = n, Profit = System.Math.Max(1, year), Book = Book(b) };
            Fixed m = d.BaseMultiple;
            if (n >= 4)
            {
                long recent = profits[n - 1] + profits[n - 2], older = profits[n - 3] + profits[n - 4], band = Money.MulRate(System.Math.Abs(older), d.GrowthShare);
                if (recent > older && recent - older >= band) w.Growth = 1;
                else if (recent < older && older - recent >= band) w.Growth = -1;
                m += Fixed.FromInt(w.Growth * d.GrowthStep);
            }
            bool lost = profits.Exists(p => p < 0);
            w.Risk = lost || n < 4;
            if (w.Risk) m -= Fixed.FromInt(d.RiskStep);
            bool allProfitable = n >= d.ProfitableSeasons;
            for (int i = n - d.ProfitableSeasons; i < n && allProfitable; i++) if (i < 0 || profits[i] <= 0) allProfitable = false;
            w.Manager = allProfitable && ManagerRules.Of(sim, b.Id) != null;
            if (w.Manager) m += Fixed.FromInt(d.ManagerBonus);
            w.Multiple = m < d.MinMultiple ? d.MinMultiple : m > d.MaxMultiple ? d.MaxMultiple : m;
            w.Value = System.Math.Max(w.Book, Money.MulRate(w.Profit, w.Multiple));
            return w;
        }

        /// <summary>The price at a share of the worth: never under what the business holds.</summary>
        public static long PriceAt(Worth w, Fixed share) { return System.Math.Max(w.Book, Money.MulRate(w.Value, share)); }

        // ---- listing ----

        /// <summary>The most a business can be refused for listing: who is to blame (the same reasons the commands give).</summary>
        public static RejectReason CanList(Simulation sim, Business b)
        {
            if (!On(sim)) return RejectReason.NoTownship;
            if (b == null || b.Status == BusinessStatus.Closed) return RejectReason.UnknownBusiness;
            if (b.Status != BusinessStatus.Open) return RejectReason.NotOpen;
            if (b.Listed) return RejectReason.AlreadyListed;
            return RejectReason.None;
        }

        internal static CommandResult List(Simulation sim, long businessId, bool rushed)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            RejectReason r = CanList(sim, b);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            ExitDef d = Defs(sim);
            long now = sim.State.Clock.SimTime, day = now / Day;
            ulong seed = sim.State.Seed, key = (ulong)b.Id;
            b.ListBuyer = (int)Rng.Below(Rng.Next(seed, RngStream.Exit, key, (ulong)day * 8 + 1), (ulong)d.Buyers.Count);
            int days = rushed ? d.RushedDays : d.SaleDaysMin + (int)Rng.Below(Rng.Next(seed, RngStream.Exit, key, (ulong)day * 8 + 2), (ulong)(d.SaleDaysMax - d.SaleDaysMin + 1));
            Fixed span = d.BuyerHigh - d.BuyerLow;
            b.ListShare = rushed ? d.RushedShare : d.BuyerLow + span * Fixed.FromRatio((long)Rng.Below(Rng.Next(seed, RngStream.Exit, key, (ulong)day * 8 + 3), 1001), 1000);
            b.Rushed = rushed;
            b.ListedAt = now;
            b.CompletesAt = now + days * Day;
            sim.Emit(new BusinessListed(now, b.Id, b.CompletesAt, rushed));
            return CommandResult.Ok;
        }

        internal static CommandResult Cancel(Simulation sim, long businessId)
        {
            Business b = sim.State.Businesses?.Find(businessId);
            if (b == null || b.Status == BusinessStatus.Closed) return CommandResult.Reject(RejectReason.UnknownBusiness);
            if (!b.Listed) return CommandResult.Reject(RejectReason.NotListedForSale);
            b.CompletesAt = 0;
            sim.Emit(new BusinessSaleCancelled(sim.State.Clock.SimTime, b.Id));
            return CommandResult.Ok;
        }

        /// <summary>Hourly, live or away: a listing whose buyer's day has come completes, in business order.</summary>
        internal static void Hourly(Simulation sim)
        {
            if (!On(sim)) return;
            long now = sim.State.Clock.SimTime;
            foreach (Business b in sim.State.Businesses.Businesses.ToArray())
                if (b.Status == BusinessStatus.Open && b.Listed && now >= b.CompletesAt) Complete(sim, b);
        }

        /// <summary>The business is sold at its worth then, at the buyer's share, never under what it holds.</summary>
        private static void Complete(Simulation sim, Business b)
        {
            long severance = Release(sim, b); // first: the buyer pays for what the business holds after its people are paid off
            Worth w = Valuation(sim, b);
            long price = PriceAt(w, b.ListShare);
            BusinessBooks.CloseDown(sim, b, new SaleTerms { Price = price, Share = b.ListShare, Worth = w.Value, Buyer = b.ListBuyer, Severance = severance });
        }

        /// <summary>Its worker and manager are released with severance (10% of the wage after their first season) from the business's cash; returns what was paid.</summary>
        private static long Release(Simulation sim, Business b)
        {
            long total = 0;
            foreach (Worker w in new[] { sim.State.People?.WorkerAt(b.Id), sim.State.People?.ManagerAt(b.Id) })
            {
                if (w == null || w.SeasonsWorked <= 0) continue;
                long sev = System.Math.Min(Money.MulRate(w.Wage, sim.Config.Game.People.Hand.SeveranceShare), BusinessBooks.CanSpend(sim, b));
                if (sev <= 0 || !BusinessBooks.Draw(sim, b, sev)) continue;
                b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.severance", "business:" + b.Id, new Posting(Account.HiredLabour, sev), new Posting(Account.Cash, -sev));
                total += sev;
            }
            return total;
        }

        // ---- branches ----

        /// <summary>The first business of a kind you still have (the one its branches are of), or null.</summary>
        public static Business FirstOf(Simulation sim, int type)
        {
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status != BusinessStatus.Closed && b.Type == type) return b;
            return null;
        }

        /// <summary>The branches of a kind you have: its businesses besides the first.</summary>
        public static int Branches(Simulation sim, int type)
        {
            int n = 0;
            foreach (Business b in sim.State.Businesses.Businesses) if (b.Status != BusinessStatus.Closed && b.Type == type) n++;
            return System.Math.Max(0, n - 1);
        }

        /// <summary>A business of a kind you already have is a branch (up to <c>maxBranches</c>): it starts as the first one's settings, its own books and manager.</summary>
        internal static RejectReason Branch(Simulation sim, int type, out Business first)
        {
            first = null;
            if (!On(sim)) return RejectReason.None;
            first = FirstOf(sim, type);
            if (first == null) return RejectReason.None;
            return Branches(sim, type) >= Defs(sim).MaxBranches ? RejectReason.TooManyBranches : RejectReason.None;
        }

        internal static void StartAs(Business b, Business first)
        {
            b.BranchOf = first.Id;
            b.OpenHour = first.OpenHour; b.CloseHour = first.CloseHour; b.Fee = first.Fee; b.OwnPerDay = first.OwnPerDay; b.BuyFromMarket = first.BuyFromMarket;
            b.PriceFactor = first.PriceFactor; b.Quality = first.Quality;
            b.Mix = (long[])first.Mix.Clone(); b.PutOut = (long[])first.PutOut.Clone();
        }
    }

    /// <summary>What a sale is (see <see cref="ExitRules"/>): the price, its share of the worth, the worth that day, the buyer's kind and the severance already paid.</summary>
    internal sealed class SaleTerms
    {
        public long Price;
        public Fixed Share;
        public long Worth;
        public int Buyer;
        public long Severance;
    }
}
