using System.Numerics;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;

namespace Sim.Core.Finance
{
    /// <summary>A lender's credit limit and the terms behind it, for the loan sheet and its Why? (spec 005 §3.6).</summary>
    public sealed class CreditLimit
    {
        public long Collateral { get; internal set; }
        public long AverageFarmProfit { get; internal set; }
        public bool ScoreTooLow { get; internal set; }
        public Fixed ScoreFactor { get; internal set; }
        public long RawLimit { get; internal set; }
        public long EraCap { get; internal set; }
        public long Owed { get; internal set; }

        /// <summary>What can still be borrowed, rounded down to a whole 100 coins.</summary>
        public long Limit { get; internal set; }
    }

    /// <summary>Spec 005's pure credit formulas: rates, the credit limit, and interest.</summary>
    public static class CreditRules
    {
        private const long HundredCoins = 10_000;

        /// <summary>The rate fixed when a loan is taken: <c>baseRate + slope × max(0, refScore − score) ÷ 100</c> (spec 005 §3.6).</summary>
        public static Fixed Rate(LenderDef lender, int score)
        {
            int below = lender.RefScore - score;
            return below <= 0 ? lender.BaseRate : lender.BaseRate + lender.RateSlopePer100.MulRatio(below, 100);
        }

        /// <summary>Σ market value of owned non-protected land plus book value of non-protected buildings, plus <paramref name="extra"/> (a parcel being bought).</summary>
        public static long Collateral(Simulation sim, long extra = 0, int excludeParcel = -1)
        {
            long sum = extra;
            foreach (Parcel p in sim.State.Land.Parcels)
                if (p.Owned && !p.Protected && p.Id != excludeParcel) sum += LandRules.ParcelPrice(sim, p);
            foreach (Building b in sim.State.Land.Buildings)
                if (!sim.State.Land.Parcels[sim.State.Land.Tiles[b.TileId].ParcelId].Protected) sum += b.Cost - b.Depreciated;
            return sum;
        }

        /// <summary>max(0, mean farm profit of the last up to 4 closed seasons); 0 before the first close.</summary>
        public static long AverageFarmProfit(Simulation sim)
        {
            var reports = sim.State.Finance.Reports;
            int n = System.Math.Min(4, reports.Count);
            if (n == 0) return 0;
            long sum = 0;
            for (int i = reports.Count - n; i < reports.Count; i++) sum += reports[i].FarmProfit;
            return System.Math.Max(0, sum / n);
        }

        /// <summary>The principal outstanding to a lender (arrears included).</summary>
        public static long Owed(Simulation sim, int lender)
        {
            long sum = 0;
            foreach (Loan l in sim.State.Credit.Loans)
                if (l.IsOpen && l.Lender == lender) sum += l.Principal;
            return sum;
        }

        /// <summary>
        /// <c>limit = max(0, min(eraCap, (base + ltv × collateral + incomeMultiple × avgIncome) × scoreFactor) − owed)</c>, rounded down
        /// to a whole 100 coins (spec 005 §3.6). <paramref name="extraCollateral"/> is the parcel a land-purchase loan buys (ADR 0028).
        /// </summary>
        public static CreditLimit Limit(Simulation sim, int lender, long extraCollateral = 0)
        {
            CreditDefs credit = sim.Config.Game.Credit;
            LenderDef def = credit.Lenders[lender];
            int score = sim.State.Credit.Score;
            var r = new CreditLimit
            {
                Collateral = def.Secured ? Collateral(sim, extraCollateral) : 0,
                AverageFarmProfit = AverageFarmProfit(sim),
                EraCap = def.EraCaps[LenderDef.HomesteadEra], // the Village cap joins with the era unlock (F09.4)
                Owed = Owed(sim, lender),
            };
            if (score < def.MinScore) { r.ScoreTooLow = true; r.ScoreFactor = Fixed.Zero; return r; }
            r.ScoreFactor = !def.ScoreScaledLimit || credit.Score.FullScore <= def.MinScore ? Fixed.One
                : Fixed.Parse("0.5") + Fixed.Parse("0.5") * Min1(Fixed.FromInt(score - def.MinScore) / Fixed.FromInt(credit.Score.FullScore - def.MinScore));
            r.RawLimit = def.BaseLimit + Money.MulRate(r.Collateral, def.CollateralLtv) + Money.MulRate(r.AverageFarmProfit, def.IncomeMultiple);
            long scaled = Money.MulRate(r.RawLimit, r.ScoreFactor);
            long room = System.Math.Min(r.EraCap, scaled) - r.Owed;
            r.Limit = room <= 0 ? 0 : room - room % HundredCoins;
            return r;
        }

        private static Fixed Min1(Fixed v) { return v > Fixed.One ? Fixed.One : v; }

        /// <summary>
        /// Whether a loan could be taken (shared by TakeLoan and the financing of BuyParcel): the rejection reason, or None.
        /// <paramref name="lender"/> is the lender's index, or −1 for an unknown lender.
        /// </summary>
        internal static RejectReason Check(Simulation sim, string lenderId, long amount, int term, long extraCollateral, out int lender)
        {
            lender = -1;
            CreditDefs credit = sim.Config.Game == null ? null : sim.Config.Game.Credit;
            if (credit == null || sim.State.Credit == null) return RejectReason.UnknownLender;
            lender = credit.IndexOfLender(lenderId);
            if (lender < 0) return RejectReason.UnknownLender;
            LenderDef def = credit.Lenders[lender];
            if (amount <= 0 || amount % Money.Coin != 0) return RejectReason.InvalidAmount;
            if (amount < def.MinAmount) return RejectReason.BelowMinimum;
            if (term < def.MinTerm || term > def.MaxTerm) return RejectReason.InvalidTerm;
            CreditLimit limit = Limit(sim, lender, extraCollateral);
            if (limit.ScoreTooLow) return RejectReason.ScoreTooLow;
            if (amount > limit.Limit) return RejectReason.OverLimit;
            return RejectReason.None;
        }

        /// <summary><c>round(money × rate × seconds ÷ S)</c>, one exact product rounded once, ties away from zero (spec 005 §3.6).</summary>
        internal static long Interest(long money, Fixed rate, long seconds, long seasonSeconds)
        {
            if (money <= 0 || seconds <= 0 || rate.Raw == 0) return 0;
            BigInteger num = new BigInteger(money) * rate.Raw * seconds;
            BigInteger den = new BigInteger(seasonSeconds) << Fixed.FractionBits;
            BigInteger q = BigInteger.DivRem(num, den, out BigInteger r);
            if (r * 2 >= den) q += 1;
            return (long)q;
        }

        /// <summary>
        /// Interest and penalty interest accrued on a loan from its segment start to <paramref name="now"/> (not yet posted).
        /// Scheduled principal accrues at the rate; arrears (unpaid principal and interest) at rate + penalty rate, the penalty
        /// part being penalty interest (spec 005 §3.6).
        /// </summary>
        public static void AccruedSince(Simulation sim, Loan loan, long now, out long interest, out long penalty)
        {
            long S = sim.Clock.SecondsPerSeason, secs = now - loan.SegmentStart;
            long arrears = loan.ArrearsPrincipal + loan.ArrearsInterest;
            interest = Interest(loan.Principal - loan.ArrearsPrincipal, loan.Rate, secs, S) + Interest(arrears, loan.Rate, secs, S);
            penalty = Interest(arrears, loan.PenaltyRate, secs, S);
        }

        /// <summary>Interest and penalty accrued but not yet posted, over all open loans: part of what is owed now (net worth, ADR 0038).</summary>
        public static long AccruedUnposted(Simulation sim)
        {
            if (sim.State.Credit == null) return 0;
            long sum = 0, now = sim.State.Clock.SimTime;
            foreach (Loan l in sim.State.Credit.Loans)
            {
                if (!l.IsOpen) continue;
                AccruedSince(sim, l, now, out long i, out long p);
                sum += i + p;
            }
            return sum;
        }

        /// <summary>Everything owed on a loan now: principal, arrears interest, interest accrued and not yet posted.</summary>
        public static long OwedNow(Simulation sim, Loan loan)
        {
            if (!loan.IsOpen) return 0;
            AccruedSince(sim, loan, sim.State.Clock.SimTime, out long i, out long p);
            return loan.Principal + loan.ArrearsInterest + loan.InterestAccrued + i + p;
        }
    }
}
