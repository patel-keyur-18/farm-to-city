using System;
using NUnit.Framework;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public partial class FixedTests
    {
        private const double Ulp = 1.0 / 4294967296.0;

        private static double D(Fixed f) { return f.Raw / 4294967296.0; }

        /// <summary>
        /// Relative error within <paramref name="rel"/>, or within one unit in the last place: a Q31.32 result
        /// below 2⁻⁴ has fewer significant bits than the relative bound asks for (spec 004 §10).
        /// </summary>
        private static void AssertClose(Fixed actual, double exact, double rel, string what)
        {
            double err = Math.Abs(D(actual) - exact);
            Assert.That(err <= Math.Max(rel * Math.Abs(exact), Ulp), what + ": " + D(actual) + " vs " + exact + " (err " + err + ")");
        }

        // Spec 004 AC-4: error bounds at 200+ points per function, invalid arguments, exact results bit for bit.
        [Test]
        public void Pow_Exp_Log()
        {
            var rnd = new Random(7); // test inputs only; the sim never uses System.Random
            for (int i = 0; i < 300; i++)
            {
                // Log2: x across [2^-31, 2^31), log-spaced raw values.
                long raw = (long)Math.Pow(2, rnd.NextDouble() * 62) + 1;
                Fixed x = Fixed.FromRaw(raw);
                double lx = Math.Log2(D(x));
                Assert.That(Math.Abs(D(Fixed.Log2(x)) - lx), Is.LessThanOrEqualTo(Math.Pow(2, -28)), "Log2(" + D(x) + ")");

                // Exp2: y in [-40, 30].
                Fixed y = Fixed.FromRaw((long)((rnd.NextDouble() * 70 - 40) * 4294967296.0));
                AssertClose(Fixed.Exp2(y), Math.Pow(2, D(y)), Math.Pow(2, -28), "Exp2(" + D(y) + ")");

                // Pow: x in [2^-10, 2^10], |y| ≤ 4, results in range.
                Fixed px = Fixed.FromRaw((long)(Math.Pow(2, rnd.NextDouble() * 20 - 10) * 4294967296.0));
                Fixed py = Fixed.FromRaw((long)((rnd.NextDouble() * 8 - 4) * 4294967296.0));
                double exact = Math.Pow(D(px), D(py));
                if (exact >= 1u << 30) continue;
                AssertClose(Fixed.Pow(px, py), exact, Math.Pow(2, -20), "Pow(" + D(px) + ", " + D(py) + ")");
            }

            // Exact cases, bit for bit.
            Assert.That(Fixed.Log2(Fixed.One), Is.EqualTo(Fixed.Zero));
            Assert.That(Fixed.Log2(Fixed.FromInt(8)), Is.EqualTo(Fixed.FromInt(3)));
            Assert.That(Fixed.Log2(Fixed.FromRatio(1, 1024)), Is.EqualTo(Fixed.FromInt(-10)));
            Assert.That(Fixed.Exp2(Fixed.Zero), Is.EqualTo(Fixed.One));
            Assert.That(Fixed.Exp2(Fixed.FromInt(30)), Is.EqualTo(Fixed.FromInt(1 << 30)));
            Assert.That(Fixed.Exp2(Fixed.FromInt(-32)).Raw, Is.EqualTo(1));
            Assert.That(Fixed.Exp2(Fixed.FromInt(-40)), Is.EqualTo(Fixed.Zero));
            Assert.That(Fixed.Pow(Fixed.FromInt(4), Fixed.FromRatio(1, 2)), Is.EqualTo(Fixed.FromInt(2)));
            Assert.That(Fixed.Pow(Fixed.FromInt(2), Fixed.FromInt(10)), Is.EqualTo(Fixed.FromInt(1024)));
            Assert.That(Fixed.Pow(Fixed.Zero, Fixed.FromInt(2)), Is.EqualTo(Fixed.Zero));
            // Pinned results of the integer algorithm (any change on any platform fails here).
            Assert.That(Fixed.Exp2(Fixed.FromRatio(1, 2)).Raw, Is.EqualTo(6074001000L));
            Assert.That(Fixed.Log2(Fixed.FromInt(3)).Raw, Is.EqualTo(6807362106L));
            Assert.That(Fixed.Pow(Fixed.Parse("0.675"), Fixed.Parse("1.6666666667")).Raw, Is.EqualTo(2230828321L));

            // Invalid arguments.
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Log2(Fixed.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Log2(-Fixed.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Exp2(Fixed.Parse("30.5")));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Pow(-Fixed.One, Fixed.One));
            Assert.Throws<ArgumentOutOfRangeException>(() => Fixed.Pow(Fixed.Zero, Fixed.Zero));
        }
    }
}
