namespace Sim.Core.Maths
{
    /// <summary>
    /// 128-bit helpers. netstandard2.1 has no <c>Int128</c> or <c>Math.BigMul(ulong, ulong)</c>,
    /// so the full product is built from 32-bit halves (spec 001 §3.1).
    /// </summary>
    internal static class Wide
    {
        /// <summary>Full 64 × 64 → 128-bit unsigned product.</summary>
        public static void Multiply(ulong a, ulong b, out ulong hi, out ulong lo)
        {
            ulong aLo = (uint)a, aHi = a >> 32;
            ulong bLo = (uint)b, bHi = b >> 32;

            ulong ll = aLo * bLo;
            ulong lh = aLo * bHi;
            ulong hl = aHi * bLo;
            ulong hh = aHi * bHi;

            ulong mid = (ll >> 32) + (uint)lh + (uint)hl;
            lo = (mid << 32) | (uint)ll;
            hi = hh + (lh >> 32) + (hl >> 32) + (mid >> 32);
        }

        /// <summary>The number of leading zero bits of a non-zero value (netstandard2.1 has no BitOperations).</summary>
        public static int LeadingZeros(ulong x)
        {
            int n = 0;
            if ((x >> 32) == 0) { n += 32; x <<= 32; }
            if ((x >> 48) == 0) { n += 16; x <<= 16; }
            if ((x >> 56) == 0) { n += 8; x <<= 8; }
            if ((x >> 60) == 0) { n += 4; x <<= 4; }
            if ((x >> 62) == 0) { n += 2; x <<= 2; }
            if ((x >> 63) == 0) n++;
            return n;
        }

        /// <summary>
        /// The 128-bit (hi:lo) ÷ d as a 64-bit quotient and its remainder, for hi &lt; d (so the quotient fits): schoolbook division on 32-bit
        /// halves (Hacker's Delight, divlu), integer only, so every platform gives the same bits.
        /// </summary>
        public static ulong Divide(ulong hi, ulong lo, ulong d, out ulong remainder)
        {
            const ulong b = 1UL << 32;
            if (hi == 0) { remainder = lo % d; return lo / d; } // the dividend fits 64 bits: the hardware divide
            int s = LeadingZeros(d); // normalise: the divisor's top bit set
            d <<= s;
            ulong vn1 = d >> 32, vn0 = d & 0xFFFFFFFFUL;
            ulong un32 = s == 0 ? hi : (hi << s) | (lo >> (64 - s));
            ulong un10 = lo << s;
            ulong un1 = un10 >> 32, un0 = un10 & 0xFFFFFFFFUL;

            ulong q1 = un32 / vn1, rhat = un32 - q1 * vn1;
            while (q1 >= b || q1 * vn0 > b * rhat + un1)
            {
                q1--;
                rhat += vn1;
                if (rhat >= b) break;
            }
            ulong un21 = un32 * b + un1 - q1 * d;
            ulong q0 = un21 / vn1;
            rhat = un21 - q0 * vn1;
            while (q0 >= b || q0 * vn0 > b * rhat + un0)
            {
                q0--;
                rhat += vn1;
                if (rhat >= b) break;
            }
            remainder = (un21 * b + un0 - q0 * d) >> s;
            return q1 * b + q0;
        }

        /// <summary>The high 64 bits of a × b.</summary>
        public static ulong MultiplyHigh(ulong a, ulong b)
        {
            Multiply(a, b, out ulong hi, out _);
            return hi;
        }
    }
}
