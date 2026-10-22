using System.Numerics;
using Sim.Core.Maths;

namespace Sim.Core.Finance
{
    /// <summary>Money is <c>long</c> in 1/100 coin (CLAUDE.md). Helpers for money × rate (spec 005 §3).</summary>
    public static class Money
    {
        /// <summary>1 coin in money units.</summary>
        public const long Coin = 100;

        /// <summary>money × rate, computed exactly and rounded once to the nearest 1/100 coin, ties away from zero.</summary>
        public static long MulRate(long money, Fixed rate)
        {
            BigInteger product = new BigInteger(money) * rate.Raw;
            bool negative = product.Sign < 0;
            BigInteger q = BigInteger.DivRem(BigInteger.Abs(product), BigInteger.One << Fixed.FractionBits, out BigInteger r);
            if (r * 2 >= BigInteger.One << Fixed.FractionBits) q += 1;
            return (long)(negative ? -q : q);
        }

        /// <summary>
        /// Value(q, P) = round(q × P × 100 ÷ unitSize) (spec 004 §3): a quantity in grams, millilitres or units at a
        /// price in coins per kg, litre or unit, rounded once to 1/100 coin, ties away from zero.
        /// </summary>
        public static long Value(long quantity, Fixed price, long unitSize)
        {
            BigInteger n = new BigInteger(quantity) * price.Raw * Coin;
            BigInteger d = new BigInteger(unitSize) << Fixed.FractionBits;
            bool negative = n.Sign < 0;
            BigInteger q = BigInteger.DivRem(BigInteger.Abs(n), d, out BigInteger r);
            if (r * 2 >= d) q += 1;
            return (long)(negative ? -q : q);
        }

        /// <summary>
        /// The most of a good <paramref name="money"/> buys at <paramref name="price"/> (the inverse of <see cref="Value"/>, rounded down):
        /// q with Value(q, P) ≤ money. Exact for any amount (no fixed-point overflow); 0 for a price of 0 or less.
        /// </summary>
        public static long QuantityFor(long money, Fixed price, long unitSize)
        {
            if (money <= 0 || price.Raw <= 0) return 0;
            BigInteger q = (new BigInteger(money) * unitSize << Fixed.FractionBits) / (new BigInteger(price.Raw) * Coin);
            long r = q > long.MaxValue ? long.MaxValue : (long)q;
            while (r > 0 && Value(r, price, unitSize) > money) r--;
            while (r < long.MaxValue && Value(r + 1, price, unitSize) <= money) r++; // Value rounds to the nearest 1/100: a few more units may still fit
            return r;
        }

        /// <summary>Rounds to a whole coin, ties away from zero.</summary>
        public static long RoundToCoin(long money)
        {
            long whole = (System.Math.Abs(money) + Coin / 2) / Coin * Coin;
            return money < 0 ? -whole : whole;
        }
    }
}
