using System.Collections.Generic;
using System.Numerics;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Market;

namespace Sim.Core.Finance
{
    /// <summary>One point of net worth's trend: as counted at the close, and in today's coins (spec 019 §3.2).</summary>
    public sealed class TrendPoint
    {
        public long Period { get; internal set; }
        public long AsCounted { get; internal set; }
        public long InTodaysCoins { get; internal set; }

        /// <summary>How much prices rose from that close to now (≥ 1).</summary>
        public Fixed PricesSince { get; internal set; }
    }

    public enum DebtBand { None, Comfortable, Stretched, Risky }

    /// <summary>
    /// The finance hub's numbers (spec 019 §3.2–§3.4, ADR 0101 §6): net worth's trend in today's coins, debt to assets, the emergency fund
    /// and passive income. Views of state: nothing here changes it. Percents and seasons are rounded down, so a number is never bigger than it is.
    /// </summary>
    public static class Hub
    {
        /// <summary>
        /// The price index at the close of a season, from the town's seed: the start index stepped by each season's seeded draw (as
        /// <c>MarketSystem</c> steps it), so older towns' trends are exact with nothing stored (ADR 0101 §6).
        /// </summary>
        public static Fixed IndexAtClose(Simulation sim, long period)
        {
            Fixed index = sim.Config.Game.Market.Economy.StartIndex;
            for (long q = 1; q <= period + CloseStepsAhead; q++) index = index * (Fixed.One + MarketSystem.Inflation(sim, q));
            return index;
        }

        /// <summary>Whether the index has stepped into the new season when the close's report is built (0: not yet).</summary>
        private const int CloseStepsAhead = 0;

        /// <summary>The last <paramref name="count"/> closes in today's coins, oldest first. Net worth now is the last point of the trend the caller adds.</summary>
        public static List<TrendPoint> Trend(Simulation sim, int count)
        {
            var list = new List<TrendPoint>();
            List<SeasonReport> reports = sim.State.Finance.Reports;
            Fixed now = sim.State.Market.PriceIndex;
            for (int i = System.Math.Max(0, reports.Count - count); i < reports.Count; i++)
            {
                SeasonReport r = reports[i];
                Fixed then = IndexAtClose(sim, r.Period);
                list.Add(new TrendPoint
                {
                    Period = r.Period, AsCounted = r.NetWorth.Total, InTodaysCoins = InTodaysCoins(r.NetWorth.Total, then, now),
                    PricesSince = then.Raw == 0 ? Fixed.One : Fixed.FromRaw((long)(new BigInteger(now.Raw) * Fixed.One.Raw / then.Raw)),
                });
            }
            return list;
        }

        /// <summary>How much prices rose over the last year (four seasons, or as many as have passed): whole percent rounded down, never bigger than it was. The savings sheet puts it beside the deposit's rate (spec 019 §3.5).</summary>
        public static int PricesRose(Simulation sim, out int seasons)
        {
            seasons = (int)System.Math.Min(4, sim.Period);
            if (seasons == 0) return 0;
            Fixed then = IndexAtClose(sim, sim.Period - seasons), now = sim.State.Market.PriceIndex;
            return then.Raw <= 0 ? 0 : (int)System.Math.Max(0, (long)((new BigInteger(now.Raw) - then.Raw) * 100 / then.Raw));
        }

        /// <summary>A past amount in today's coins: × now ÷ then, rounded to the coin's hundredth.</summary>
        public static long InTodaysCoins(long amount, Fixed then, Fixed now)
        {
            return then.Raw == 0 ? amount : (long)(new BigInteger(amount) * now.Raw / then.Raw);
        }

        /// <summary>What you own: net worth plus what you owe.</summary>
        public static long Owned(NetWorthSnapshot n) { return n.Total + n.LoanPrincipal + n.InterestPayable; }
        public static long Owed(NetWorthSnapshot n) { return n.LoanPrincipal + n.InterestPayable; }

        /// <summary>What you owe over what you own, whole percent rounded down; 0 with nothing owned.</summary>
        public static int DebtPercent(NetWorthSnapshot n)
        {
            long own = Owned(n);
            return own <= 0 ? 0 : (int)(new BigInteger(Owed(n)) * 100 / own);
        }

        public static DebtBand Band(Simulation sim, NetWorthSnapshot n)
        {
            if (Owed(n) <= 0) return DebtBand.None;
            LedgerDef d = sim.Config.Game.Ledger;
            long own = Owned(n), debt = Owed(n);
            if (debt > d.DebtRiskyOver.MulRatio(own, 1).RoundToLong()) return DebtBand.Risky;
            return debt < d.DebtComfortableUnder.MulRatio(own, 1).RoundToLong() ? DebtBand.Comfortable : DebtBand.Stretched;
        }

        /// <summary>The family's living a season at the plan you're on, never under the basic plan, at today's prices (ADR 0101 §6).</summary>
        public static long LivingPerSeason(Simulation sim)
        {
            if (sim.State.Farm == null) return 0;
            IReadOnlyList<DietPlanDef> plans = sim.Config.Game.Farm.DietPlans;
            long basic = 0, plan = 0;
            int onPlan = sim.State.Farm.Founder.AutoSubsistence ? sim.State.Farm.Founder.PreviousPlan : sim.State.Farm.Founder.DietPlan;
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i].Id == "basic") basic = plans[i].CostPerSeason;
                if (i == onPlan) plan = plans[i].CostPerSeason;
            }
            return MarketRules.Indexed(sim, System.Math.Max(basic, plan));
        }

        /// <summary>Everything the next close takes from your cash, with living never under the basic plan (the emergency fund's divisor).</summary>
        public static long NextCloseTotal(Simulation sim)
        {
            long total = 0, living = 0;
            foreach (CloseDue d in CloseDues.Next(sim))
            {
                if (d.Kind == DueKind.Living) { living = d.Amount; continue; }
                total += d.Amount;
            }
            return total + System.Math.Max(living, LivingPerSeason(sim));
        }

        /// <summary>Seasons of bills your cash and savings cover, one decimal rounded down, in tenths (14 is "1.4"). −1 when nothing is due.</summary>
        public static long EmergencyTenths(Simulation sim)
        {
            long due = NextCloseTotal(sim);
            if (due <= 0) return -1;
            return (long)(new BigInteger(sim.State.Ledger.Cash + Deposit(sim)) * 10 / due);
        }

        /// <summary>The savings deposit (account 1020); none until the co-op bank takes deposits.</summary>
        public static long Deposit(Simulation sim) { return Reporting.NetWorth(sim).Deposit; }

        /// <summary>One source of passive income in the last closed season (a rent, a business's profit).</summary>
        public static long Passive(Simulation sim, out long rent, out long interest, out long business)
        {
            rent = interest = business = 0;
            List<SeasonReport> reports = sim.State.Finance.Reports;
            if (reports.Count == 0) return 0;
            SeasonReport last = reports[reports.Count - 1];
            foreach (KeyValuePair<Account, long> kv in last.ByAccount)
            {
                if (kv.Key == Account.RentReceived) rent = kv.Value;
                if (kv.Key == Account.InterestOnSavings) interest = kv.Value;
            }
            if (sim.State.Businesses != null)
                foreach (Business.Business b in sim.State.Businesses.Businesses)
                {
                    if (BusinessRules.HiredWorker(sim, b) == null) continue; // a business you run yourself is your labour
                    foreach (BusinessReport r in b.Reports) if (r.Period == last.Period) business += BusinessBooks.NetProfit(r);
                }
            return rent + interest + business;
        }

        /// <summary>Passive income over the family's living, whole percent rounded down, never under 0.</summary>
        public static int PassivePercent(Simulation sim)
        {
            return PercentOf(Passive(sim, out _, out _, out _), LivingPerSeason(sim));
        }

        /// <summary>Whole percent rounded down, never under 0 (a business's loss can take passive income below nothing).</summary>
        public static int PercentOf(long passive, long living)
        {
            return living <= 0 || passive <= 0 ? 0 : (int)(new BigInteger(passive) * 100 / living);
        }
    }
}
