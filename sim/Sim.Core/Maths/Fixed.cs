using System;
using System.Numerics;
using System.Text;

namespace Sim.Core.Maths
{
    /// <summary>
    /// Signed Q31.32 fixed-point number stored in a <c>long</c> (spec 001 §3.1).
    /// Used for every rate and factor in the simulation instead of floats, so results are
    /// identical on CoreCLR, Mono and IL2CPP.
    /// <list type="bullet">
    /// <item>Add and subtract are exact.</item>
    /// <item>Multiply and divide round to nearest, ties away from zero.</item>
    /// <item>Overflow always throws <see cref="OverflowException"/>, in every build configuration.</item>
    /// </list>
    /// </summary>
    public readonly struct Fixed : IEquatable<Fixed>, IComparable<Fixed>
    {
        public const int FractionBits = 32;
        private const long OneRaw = 1L << FractionBits;
        private const ulong HalfUlp = 1UL << (FractionBits - 1);

        /// <summary>The raw Q31.32 value: the number × 2^32.</summary>
        public readonly long Raw;

        private Fixed(long raw) { Raw = raw; }

        public static readonly Fixed Zero = new Fixed(0);
        public static readonly Fixed One = new Fixed(OneRaw);

        public static Fixed FromRaw(long raw) { return new Fixed(raw); }

        /// <summary>An integer value. Throws if it's outside about ±2.1 × 10⁹.</summary>
        public static Fixed FromInt(long value)
        {
            if (value > int.MaxValue || value < int.MinValue)
                throw new OverflowException("Fixed.FromInt: " + value + " is out of range.");
            return new Fixed(value << FractionBits);
        }

        /// <summary>numerator ÷ denominator, rounded to nearest with ties away from zero.</summary>
        public static Fixed FromRatio(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("Fixed.FromRatio: denominator is zero.");
            return FromBigRatio(new BigInteger(numerator) << FractionBits, denominator);
        }

        /// <summary>Exact conversion from a decimal (used by the content loader; never via double).</summary>
        public static Fixed FromDecimal(decimal value)
        {
            int[] bits = decimal.GetBits(value);
            var mantissa = new BigInteger((uint)bits[0])
                | (new BigInteger((uint)bits[1]) << 32)
                | (new BigInteger((uint)bits[2]) << 64);
            int scale = (bits[3] >> 16) & 0xFF;
            if (bits[3] < 0) mantissa = -mantissa;
            return FromBigRatio(mantissa << FractionBits, BigInteger.Pow(10, scale));
        }

        /// <summary>
        /// Exact parse of a plain decimal string such as <c>-12.345</c> (invariant format, no exponent).
        /// </summary>
        public static Fixed Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) throw new FormatException("Fixed.Parse: empty string.");
            int i = 0;
            bool negative = false;
            if (text[0] == '-' || text[0] == '+') { negative = text[0] == '-'; i = 1; }
            BigInteger mantissa = BigInteger.Zero;
            int scale = 0, digits = 0;
            bool seenPoint = false;
            for (; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '.' && !seenPoint) { seenPoint = true; continue; }
                if (c < '0' || c > '9') throw new FormatException("Fixed.Parse: invalid number '" + text + "'.");
                mantissa = mantissa * 10 + (c - '0');
                digits++;
                if (seenPoint) scale++;
            }
            if (digits == 0) throw new FormatException("Fixed.Parse: invalid number '" + text + "'.");
            if (negative) mantissa = -mantissa;
            return FromBigRatio(mantissa << FractionBits, BigInteger.Pow(10, scale));
        }

        /// <summary>this × numerator ÷ denominator with a single rounding at the end.</summary>
        public Fixed MulRatio(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException("Fixed.MulRatio: denominator is zero.");
            return FromBigRatio(new BigInteger(Raw) * numerator, denominator);
        }

        /// <summary>The largest integer ≤ this value.</summary>
        public long FloorToLong() { return Raw >> FractionBits; }

        /// <summary>The nearest integer, ties away from zero.</summary>
        public long RoundToLong()
        {
            return (long)FromBigRatio(new BigInteger(Raw), OneRaw).Raw;
        }

        public static Fixed operator +(Fixed a, Fixed b) { return new Fixed(checked(a.Raw + b.Raw)); }
        public static Fixed operator -(Fixed a, Fixed b) { return new Fixed(checked(a.Raw - b.Raw)); }
        public static Fixed operator -(Fixed a) { return new Fixed(checked(-a.Raw)); }

        public static Fixed operator *(Fixed a, Fixed b)
        {
            bool negative = (a.Raw < 0) != (b.Raw < 0);
            Wide.Multiply(Magnitude(a.Raw), Magnitude(b.Raw), out ulong hi, out ulong lo);

            // Add half an ulp, then shift right by 32: rounds the magnitude half away from zero.
            ulong roundedLo = lo + HalfUlp;
            if (roundedLo < lo) hi++;
            if ((hi >> FractionBits) != 0) throw new OverflowException("Fixed multiply overflow.");
            ulong magnitude = (hi << FractionBits) | (roundedLo >> FractionBits);
            return new Fixed(ApplySign(magnitude, negative));
        }

        public static Fixed operator /(Fixed a, Fixed b)
        {
            if (b.Raw == 0) throw new DivideByZeroException("Fixed divide by zero.");
            return FromBigRatio(new BigInteger(a.Raw) << FractionBits, b.Raw);
        }

        public static bool operator ==(Fixed a, Fixed b) { return a.Raw == b.Raw; }
        public static bool operator !=(Fixed a, Fixed b) { return a.Raw != b.Raw; }
        public static bool operator <(Fixed a, Fixed b) { return a.Raw < b.Raw; }
        public static bool operator >(Fixed a, Fixed b) { return a.Raw > b.Raw; }
        public static bool operator <=(Fixed a, Fixed b) { return a.Raw <= b.Raw; }
        public static bool operator >=(Fixed a, Fixed b) { return a.Raw >= b.Raw; }

        public bool Equals(Fixed other) { return Raw == other.Raw; }
        public override bool Equals(object obj) { return obj is Fixed other && Raw == other.Raw; }
        public override int GetHashCode() { return Raw.GetHashCode(); }
        public int CompareTo(Fixed other) { return Raw.CompareTo(other.Raw); }

        /// <summary>The exact decimal value, for example <c>2.25</c> (every Q31.32 value has a finite decimal form).</summary>
        public override string ToString()
        {
            bool negative = Raw < 0;
            BigInteger magnitude = BigInteger.Abs(new BigInteger(Raw));
            BigInteger integer = magnitude >> FractionBits;
            BigInteger fraction = magnitude - (integer << FractionBits);
            var sb = new StringBuilder();
            if (negative) sb.Append('-');
            sb.Append(integer.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (!fraction.IsZero)
            {
                // fraction / 2^32 = fraction × 5^32 / 10^32
                string digits = (fraction * BigInteger.Pow(5, FractionBits))
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)
                    .PadLeft(FractionBits, '0')
                    .TrimEnd('0');
                sb.Append('.').Append(digits);
            }
            return sb.ToString();
        }

        /// <summary>Rounds numerator ÷ denominator to nearest (ties away from zero) as a raw value.</summary>
        private static Fixed FromBigRatio(BigInteger numerator, BigInteger denominator)
        {
            bool negative = (numerator.Sign < 0) != (denominator.Sign < 0);
            BigInteger n = BigInteger.Abs(numerator), d = BigInteger.Abs(denominator);
            BigInteger q = BigInteger.DivRem(n, d, out BigInteger r);
            if (r * 2 >= d) q += 1;
            if (negative) q = -q;
            if (q > long.MaxValue || q < long.MinValue) throw new OverflowException("Fixed result out of range.");
            return new Fixed((long)q);
        }

        private static ulong Magnitude(long v) { return v < 0 ? (ulong)(-(v + 1)) + 1 : (ulong)v; }

        private static long ApplySign(ulong magnitude, bool negative)
        {
            if (negative)
            {
                if (magnitude > (1UL << 63)) throw new OverflowException("Fixed multiply overflow.");
                return magnitude == (1UL << 63) ? long.MinValue : -(long)magnitude;
            }
            if (magnitude > long.MaxValue) throw new OverflowException("Fixed multiply overflow.");
            return (long)magnitude;
        }
    }
}
