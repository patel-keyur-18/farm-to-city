using System.Numerics;
using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Market
{
    /// <summary>One produce good's hour at the local market (spec 004 §3.2): what goes in.</summary>
    internal struct ClearingInput
    {
        public Fixed Price;          // P at the start of the hour (after following the region)
        public Fixed Regional;       // P_regional now
        public Fixed Floor;
        public Fixed Ceiling;
        public Fixed Elasticity;
        public long DemandPerDay;    // D_ref, grams a day
        public long OtherSupplyPerDay; // the valley's produce plus the trader's imports, grams a day
        public long Eligible;        // the founder's offer this hour (0 if its minimum price isn't met), grams
        public bool TraderMayTake;   // the offer's minimum price allows the floor
        public long TraderCapacityLeft; // grams
    }

    /// <summary>One produce good's hour at the local market: what comes out.</summary>
    internal struct ClearingResult
    {
        public Fixed Price;          // P after the damped step
        public Fixed Target;
        public long Lot;             // S_founder: what reached the stall this hour, grams
        public long Sold;            // the founder's local sales, grams
        public long ToTrader;        // bought by the trader at the floor, grams
        public Fixed UnmetKg;        // unmet demand this hour, kg
        public bool RailHit;
    }

    /// <summary>
    /// The damped hourly clearing of spec 004 §3.2 (ADR 0013, 0034), a pure function. Supply and demand are summed in
    /// whole grams a day, so a neutral market (nothing offered, valley supply + imports = demand) gives a ratio of
    /// exactly 1 and the price stays exactly at P_regional.
    /// </summary>
    internal static class MarketClearing
    {
        public static ClearingResult Clear(MarketDef def, ClearingInput a)
        {
            var r = new ClearingResult();

            // S_founder = min(eligible, max(eligible ÷ spread, minLot)), whole grams an hour.
            long lot = 0;
            if (a.Eligible > 0)
            {
                lot = System.Math.Max(a.Eligible / def.OfferSpreadHours, def.MinLotGrams);
                if (lot > a.Eligible) lot = a.Eligible;
            }
            r.Lot = lot;
            long supplyPerDay = lot * 24 + a.OtherSupplyPerDay; // > 0: imports are > 0 for every produce good

            // P_target = P_regional × (D_ref ÷ S)^(1 ÷ e), then the trader's floor, the import ceiling and the rail.
            Fixed ratio = Fixed.FromRatio(a.DemandPerDay, supplyPerDay);
            Fixed target = a.Regional * Fixed.Pow(ratio, Fixed.One / a.Elasticity);
            bool raised = false;
            if (target < a.Floor && a.TraderCapacityLeft > 0) { target = a.Floor; raised = true; }
            if (target > a.Ceiling) target = a.Ceiling;
            Fixed lo = def.RailMin * a.Regional, hi = def.RailMax * a.Regional;
            if (target < lo) { target = lo; r.RailHit = true; }
            if (target > hi) { target = hi; r.RailHit = true; }
            r.Target = target;

            Fixed p = a.Price + def.AdjustmentRate * (target - a.Price);
            if (p < lo) { p = lo; r.RailHit = true; }
            if (p > hi) { p = hi; r.RailHit = true; }
            r.Price = p;

            // D = D_ref × (P ÷ P_regional)^(−e), grams a day as a Fixed factor on D_ref.
            Fixed demandFactor = Fixed.Pow(p / a.Regional, -a.Elasticity);
            if (lot > 0)
            {
                // sold = min(lot, D × lot ÷ S), rounded down to the gram: D_ref × factor × lot ÷ S (both a day).
                BigInteger num = new BigInteger(a.DemandPerDay) * demandFactor.Raw * lot;
                BigInteger den = new BigInteger(supplyPerDay) << Fixed.FractionBits;
                long sold = (long)BigInteger.Min(num / den, lot);
                r.Sold = sold;
                if (raised && a.TraderMayTake) r.ToTrader = System.Math.Min(lot - sold, a.TraderCapacityLeft);
            }

            // unmet = max(0, D − S), kg an hour.
            Fixed demandKgPerHour = Fixed.FromRatio(a.DemandPerDay, 24_000) * demandFactor;
            Fixed supplyKgPerHour = Fixed.FromRatio(supplyPerDay, 24_000);
            r.UnmetKg = demandKgPerHour > supplyKgPerHour ? demandKgPerHour - supplyKgPerHour : Fixed.Zero;
            return r;
        }
    }
}
