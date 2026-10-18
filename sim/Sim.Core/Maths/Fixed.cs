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

        // ---- Powers (spec 004 §3.7): integer arithmetic only, bit-identical on every platform ----

        /// <summary>2^(2^-k) for k = 1..33 in Q1.63 (computed to 80 digits and rounded).</summary>
        private static readonly ulong[] Exp2Steps =
        {
            0xB504F333F9DE6484UL, 0x9837F0518DB8A96FUL, 0x8B95C1E3EA8BD6E7UL, 0x85AAC367CC487B15UL,
            0x82CD8698AC2BA1D7UL, 0x8164D1F3BC030773UL, 0x80B1ED4FD999AB6CUL, 0x8058D7D2D5E5F6B1UL,
            0x802C6436D0E04F51UL, 0x8016302F17467628UL, 0x800B179C82028FD1UL, 0x80058BAF7FEE3B5DUL,
            0x8002C5D00FDCFCB7UL, 0x800162E61BED4A49UL, 0x8000B17292F702A4UL, 0x800058B92ABBAE02UL,
            0x80002C5C8DADE4D7UL, 0x8000162E44EAF636UL, 0x80000B1721FA7C19UL, 0x8000058B90DE7E4DUL,
            0x800002C5C8678F37UL, 0x80000162E431DBA0UL, 0x800000B1721872D1UL, 0x80000058B90C1AA9UL,
            0x8000002C5C8605A4UL, 0x800000162E4300E6UL, 0x8000000B17217FF8UL, 0x800000058B90BFDDUL,
            0x80000002C5C85FE7UL, 0x8000000162E42FF2UL, 0x80000000B17217F8UL, 0x8000000058B90BFCUL,
        };

        /// <summary>log₂(x) for x &gt; 0. Absolute error ≤ 2⁻³² (spec: 2⁻²⁸).</summary>
        public static Fixed Log2(Fixed x)
        {
            if (x.Raw <= 0) throw new ArgumentOutOfRangeException(nameof(x), "Log2 needs x > 0.");
            ulong raw = (ulong)x.Raw;
            int top = 63;
            while ((raw >> top) == 0) top--;
            long result = (long)(top - FractionBits) << FractionBits;
            ulong m = raw << (63 - top); // x ÷ 2^n in Q1.63, in [1, 2)

            // Each squaring of m doubles its log; a value ≥ 2 gives the next fraction bit. One extra bit to round.
            long bits = 0;
            for (int i = 0; i < FractionBits + 1; i++)
            {
                Wide.Multiply(m, m, out ulong hi, out ulong lo);
                bits <<= 1;
                if ((hi >> 63) != 0) { m = hi; bits |= 1; }
                else m = (hi << 1) | (lo >> 63);
            }
            return new Fixed(result + ((bits + 1) >> 1));
        }

        /// <summary>2^y for y ≤ 30; results that underflow give 0. Relative error ≤ 2⁻²⁸ (or one ulp for tiny results).</summary>
        public static Fixed Exp2(Fixed y)
        {
            if (y.Raw > (30L << FractionBits)) throw new ArgumentOutOfRangeException(nameof(y), "Exp2 needs y ≤ 30.");
            long n = y.Raw >> FractionBits;           // floor
            ulong f = (ulong)y.Raw & 0xFFFFFFFFUL;    // fraction bits
            ulong m = 1UL << 63;                      // 1.0 in Q1.63
            for (int k = 0; k < FractionBits; k++)
            {
                if (((f >> (FractionBits - 1 - k)) & 1) == 0) continue;
                Wide.Multiply(m, Exp2Steps[k], out ulong hi, out ulong lo);
                m = ((hi << 1) | (lo >> 63)) + ((lo >> 62) & 1); // Q2.126 → Q1.63, rounded
            }
            long shift = 31 - n; // Q1.63 × 2^n → Q31.32
            if (shift > 64) return Zero;
            if (shift == 64) return new Fixed((long)(m >> 63)); // rounds 2^-33 ≤ v < 2^-32 to one ulp
            return new Fixed((long)((m >> (int)shift) + ((m >> (int)(shift - 1)) & 1)));
        }

        /// <summary>x^y = Exp2(y × Log2(x)) for x &gt; 0, or x = 0 with y &gt; 0 (gives 0).</summary>
        public static Fixed Pow(Fixed x, Fixed y)
        {
            if (x.Raw < 0) throw new ArgumentOutOfRangeException(nameof(x), "Pow needs x ≥ 0.");
            if (x.Raw == 0)
            {
                if (y.Raw <= 0) throw new ArgumentOutOfRangeException(nameof(y), "Pow(0, y) needs y > 0.");
                return Zero;
            }
            return Exp2(y * Log2(x));
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
