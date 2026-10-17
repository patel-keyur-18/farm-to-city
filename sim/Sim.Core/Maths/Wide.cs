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

        /// <summary>The high 64 bits of a × b.</summary>
        public static ulong MultiplyHigh(ulong a, ulong b)
        {
            Multiply(a, b, out ulong hi, out _);
            return hi;
        }
    }
}
