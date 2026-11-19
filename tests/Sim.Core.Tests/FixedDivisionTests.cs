using System;
using System.Numerics;
using NUnit.Framework;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    /// <summary>
    /// The allocation-free division behind <c>Fixed /</c>, <c>FromRatio</c>, <c>MulRatio</c> and <c>RoundToLong</c> gives exactly what the
    /// BigInteger arithmetic it replaced gave (round to nearest, ties away from zero, overflow throws), for edge values and a seeded fuzz.
    /// </summary>
    public class FixedDivisionTests
    {
        /// <summary>The reference: numerator ÷ denominator rounded half away from zero; null when the result does not fit a long.</summary>
        private static long? Reference(BigInteger numerator, BigInteger denominator)
        {
            bool negative = (numerator.Sign < 0) != (denominator.Sign < 0);
            BigInteger n = BigInteger.Abs(numerator), d = BigInteger.Abs(denominator);
            BigInteger q = BigInteger.DivRem(n, d, out BigInteger r);
            if (r * 2 >= d) q += 1;
            if (negative) q = -q;
            if (q > long.MaxValue || q < long.MinValue) return null;
            return (long)q;
        }

        private static long? Run(Func<Fixed> f)
        {
            try { return f().Raw; }
            catch (OverflowException) { return null; }
        }

        private static readonly long[] Edges =
        {
            0, 1, -1, 2, -2, 3, 7, -7, 1L << 31, -(1L << 31), (1L << 32) - 1, 1L << 32, (1L << 32) + 1, 1L << 33, 0x7FFFFFFFL, 0xFFFFFFFFL,
            1_000_000_007, -1_000_000_007, long.MaxValue, long.MinValue, long.MaxValue - 1, long.MinValue + 1, 1L << 62, -(1L << 62), 123456789012345L, -98765432109876L,
        };

        private static void Check(long a, long b)
        {
            if (b == 0) return;
            Assert.That(Run(() => Fixed.FromRaw(a) / Fixed.FromRaw(b)), Is.EqualTo(Reference(new BigInteger(a) << 32, b)), a + " / " + b);
            Assert.That(Run(() => Fixed.FromRatio(a, b)), Is.EqualTo(Reference(new BigInteger(a) << 32, b)), "FromRatio " + a + ", " + b);
            foreach (long m in new[] { 1L, -1L, 7L, 1000L, 86_400L, long.MaxValue, long.MinValue, 123456789L })
                Assert.That(Run(() => Fixed.FromRaw(a).MulRatio(m, b)), Is.EqualTo(Reference(new BigInteger(a) * m, b)), a + " × " + m + " ÷ " + b);
        }

        [Test]
        public void Edge_Values()
        {
            foreach (long a in Edges) foreach (long b in Edges) Check(a, b);
            foreach (long a in Edges)
                Assert.That(Fixed.FromRaw(a).RoundToLong(), Is.EqualTo(Reference(a, 1L << 32).Value), "RoundToLong " + a);
        }

        [Test]
        public void Fuzz_Matches_BigInteger()
        {
            var rng = new Random(20261119);
            long Next(int bits)
            {
                long v = (long)(((ulong)rng.NextInt64() >> (63 - bits)) & ((1UL << bits) - 1));
                return rng.Next(2) == 0 ? v : -v;
            }
            for (int i = 0; i < 200_000; i++)
            {
                long a = Next(rng.Next(1, 63)), b = Next(rng.Next(1, 63));
                Check(a, b);
                Assert.That(Fixed.FromRaw(a).RoundToLong(), Is.EqualTo(Reference(a, 1L << 32).Value));
            }
        }

        // Pow remembers its answers (the same powers come round every hour): a remembered answer is the computed one, for colliding keys too.
        [Test]
        public void Pow_Memory_Gives_The_Computed_Answer()
        {
            var rng = new Random(7);
            for (int i = 0; i < 20_000; i++)
            {
                Fixed x = Fixed.FromRaw((long)(rng.NextInt64(1, 1L << 36))), y = Fixed.FromRaw(rng.NextInt64(-(1L << 33), 1L << 33));
                Fixed expected;
                try { expected = Fixed.Exp2(y * Fixed.Log2(x)); }
                catch (OverflowException) { Assert.Throws<OverflowException>(() => Fixed.Pow(x, y)); continue; }
                catch (ArgumentOutOfRangeException) { Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Pow(x, y)); continue; }
                Assert.That(Fixed.Pow(x, y), Is.EqualTo(expected));
                Assert.That(Fixed.Pow(x, y), Is.EqualTo(expected), "again, from memory");
            }
        }
    }
}
