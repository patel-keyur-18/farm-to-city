using System;
using NUnit.Framework;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public class FixedTests
    {
        private static Fixed F(string s) { return Fixed.Parse(s); }

        // Spec 001 AC-1.
        [Test]
        public void Mul_Div_RoundToNearestTiesAwayFromZero()
        {
            Assert.That(F("1.5") * F("1.5"), Is.EqualTo(F("2.25")));
            Assert.That((F("1.5") * F("1.5")).Raw, Is.EqualTo(9663676416L)); // 2.25 × 2^32
            Assert.That(F("-1.5") * F("1.5"), Is.EqualTo(F("-2.25")));

            // 1 ÷ 3: 2^32 / 3 = 1431655765.33…, nearest is 1431655765.
            Fixed third = Fixed.One / Fixed.FromInt(3);
            Assert.That(third.Raw, Is.EqualTo(1431655765L));
            // 2 ÷ 3: 2863311530.67…, nearest is 2863311531.
            Assert.That((Fixed.FromInt(2) / Fixed.FromInt(3)).Raw, Is.EqualTo(2863311531L));
            Assert.That((Fixed.FromInt(-2) / Fixed.FromInt(3)).Raw, Is.EqualTo(-2863311531L));

            // Exact ties round away from zero: 2^-32 × 0.5 = 2^-33.
            Fixed epsilon = Fixed.FromRaw(1);
            Fixed half = Fixed.FromRatio(1, 2);
            Assert.That((epsilon * half).Raw, Is.EqualTo(1L));
            Assert.That((-epsilon * half).Raw, Is.EqualTo(-1L));
            Assert.That((epsilon / Fixed.FromInt(2)).Raw, Is.EqualTo(1L));
            Assert.That((-epsilon / Fixed.FromInt(2)).Raw, Is.EqualTo(-1L));
            // Below the tie rounds toward zero: 2^-32 × 0.25.
            Assert.That((epsilon * Fixed.FromRatio(1, 4)).Raw, Is.EqualTo(0L));
        }

        [Test]
        public void AddSubtractCompare_AreExact()
        {
            Assert.That(F("0.1") + F("0.2") - F("0.3"), Is.EqualTo(Fixed.FromRaw(F("0.1").Raw + F("0.2").Raw - F("0.3").Raw)));
            Assert.That(F("2.5") > F("2.25"), Is.True);
            Assert.That(F("-1") < Fixed.Zero, Is.True);
            Assert.That(F("3").CompareTo(F("3.0")), Is.EqualTo(0));
            Assert.That(-F("1.75"), Is.EqualTo(F("-1.75")));
        }

        [Test]
        public void Overflow_AlwaysThrows()
        {
            Fixed max = Fixed.FromRaw(long.MaxValue);
            Assert.That(() => max + Fixed.FromRaw(1), Throws.TypeOf<OverflowException>());
            Assert.That(() => Fixed.FromRaw(long.MinValue) - Fixed.FromRaw(1), Throws.TypeOf<OverflowException>());
            Assert.That(() => Fixed.FromInt(100000) * Fixed.FromInt(100000), Throws.TypeOf<OverflowException>());
            Assert.That(() => Fixed.FromInt(2000000000) / F("0.5"), Throws.TypeOf<OverflowException>());
            Assert.That(() => Fixed.FromInt(3000000000L), Throws.TypeOf<OverflowException>());
            Assert.That(() => -Fixed.FromRaw(long.MinValue), Throws.TypeOf<OverflowException>());
        }

        [Test]
        public void DivideByZero_Throws()
        {
            Assert.That(() => Fixed.One / Fixed.Zero, Throws.TypeOf<DivideByZeroException>());
            Assert.That(() => Fixed.FromRatio(1, 0), Throws.TypeOf<DivideByZeroException>());
        }

        [Test]
        public void Mul_LargeAndNegativeOperands()
        {
            Assert.That(Fixed.FromInt(-40000) * Fixed.FromInt(50000), Is.EqualTo(Fixed.FromInt(-2000000000)));
            Assert.That(Fixed.FromInt(-3) * Fixed.FromInt(-7), Is.EqualTo(Fixed.FromInt(21)));
            Assert.That(Fixed.FromRaw(long.MinValue) * Fixed.One, Is.EqualTo(Fixed.FromRaw(long.MinValue)));
            Assert.That(Fixed.FromRaw(long.MaxValue) * Fixed.One, Is.EqualTo(Fixed.FromRaw(long.MaxValue)));
        }

        // Spec 001 AC-2.
        [Test]
        public void FromJsonDecimal_IsExact()
        {
            Assert.That(Fixed.FromDecimal(0.65m), Is.EqualTo(Fixed.FromRatio(65, 100)));
            Assert.That(F("0.65"), Is.EqualTo(Fixed.FromRatio(65, 100)));
            Assert.That(Fixed.FromDecimal(-0.65m), Is.EqualTo(Fixed.FromRatio(-65, 100)));
            Assert.That(Fixed.FromDecimal(0.123456789m), Is.EqualTo(Fixed.FromRatio(123456789, 1000000000)));
            Assert.That(Fixed.FromDecimal(1.0m), Is.EqualTo(Fixed.One));
            Assert.That(Fixed.FromDecimal(2147483647m), Is.EqualTo(Fixed.FromInt(2147483647)));
            // The loader path (JSON → decimal → Fixed) is covered in ContentTests.
        }

        [Test]
        public void FromRatio_RoundsHalfAwayFromZero()
        {
            Assert.That(Fixed.FromRatio(1, 3).Raw, Is.EqualTo(1431655765L));
            Assert.That(Fixed.FromRatio(-1, 3).Raw, Is.EqualTo(-1431655765L));
            Assert.That(Fixed.FromRatio(1, -3).Raw, Is.EqualTo(-1431655765L));
            Assert.That(Fixed.FromRatio(-2, -3).Raw, Is.EqualTo(2863311531L));
            Assert.That(Fixed.FromRatio(5, 1), Is.EqualTo(Fixed.FromInt(5)));
        }

        [Test]
        public void MulRatio_IsExactThenRounded()
        {
            // 18 seasons of 4 days at 1 life-year per season = 18.0 exactly.
            long season = 4 * 86400;
            Assert.That(Fixed.One.MulRatio(18 * season, season), Is.EqualTo(Fixed.FromInt(18)));
            Assert.That(Fixed.FromInt(2).MulRatio(1, 3).Raw, Is.EqualTo(2863311531L));
        }

        [Test]
        public void Parse_RejectsBadInput()
        {
            Assert.That(() => Fixed.Parse("abc"), Throws.TypeOf<FormatException>());
            Assert.That(() => Fixed.Parse("1e5"), Throws.TypeOf<FormatException>());
            Assert.That(() => Fixed.Parse(""), Throws.TypeOf<FormatException>());
        }

        [Test]
        public void Conversions()
        {
            Assert.That(F("2.75").FloorToLong(), Is.EqualTo(2L));
            Assert.That(F("-2.75").FloorToLong(), Is.EqualTo(-3L));
            Assert.That(F("2.5").RoundToLong(), Is.EqualTo(3L));
            Assert.That(F("-2.5").RoundToLong(), Is.EqualTo(-3L));
            Assert.That(F("2.25").ToString(), Is.EqualTo("2.25"));
            Assert.That(F("-0.5").ToString(), Is.EqualTo("-0.5"));
        }
    }
}
