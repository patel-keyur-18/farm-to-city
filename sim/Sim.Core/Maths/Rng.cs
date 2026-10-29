using System;

namespace Sim.Core.Maths
{
    /// <summary>
    /// Fixed stream ids, one per system (spec 001 §3.1). Values never change once assigned, so adding a
    /// system never changes another system's random sequence.
    /// </summary>
    public enum RngStream : ulong
    {
        Weather = 1,
        Forecast = 2,
        Inflation = 3,
        Pest = 4,
        FounderSick = 5,
        People = 6,
        DayLabourPool = 7,
        Applicants = 8,
        DailyPrice = 9,
        Orders = 10,
        Livestock = 11,
        Citizens = 12,
        Births = 13,
        Deaths = 14,
        Migration = 15,

        /// <summary>HR events (spec 015 §3.7): key = the worker (a hand's id, or a business worker's id + 2^32), counter = day × 8 + kind.</summary>
        HrEvents = 16,
    }

    /// <summary>
    /// Counter-based random numbers (spec 001 §3.1): every draw is a pure function of
    /// (seed, stream, key, counter), so results never depend on call order. Never use <c>System.Random</c>.
    /// </summary>
    public static class Rng
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;

        /// <summary>
        /// SplitMix64 mixing of the four inputs:
        /// h = Mix(seed + γ); then h = Mix(h ⊕ Mix(x + kγ)) for stream (k = 2), key (k = 3) and counter (k = 4).
        /// </summary>
        public static ulong Next(ulong seed, RngStream stream, ulong key, ulong counter)
        {
            unchecked
            {
                ulong h = Mix(seed + Gamma);
                h = Mix(h ^ Mix((ulong)stream + 2 * Gamma));
                h = Mix(h ^ Mix(key + 3 * Gamma));
                return Mix(h ^ Mix(counter + 4 * Gamma));
            }
        }

        /// <summary>Uniform integer in [0, n) from a 64-bit draw: (r × n) >> 64 (Lemire's multiply-high).</summary>
        public static ulong Below(ulong r, ulong n)
        {
            if (n == 0) throw new ArgumentOutOfRangeException(nameof(n), "n must be greater than 0.");
            return Wide.MultiplyHigh(r, n);
        }

        /// <summary>
        /// True with probability p (0–1), using the draw's top 32 bits: (r >> 32) &lt; p × 2^32.
        /// Exact at p = 0 (never) and p = 1 (always).
        /// </summary>
        public static bool Chance(ulong r, Fixed p)
        {
            return (long)(r >> 32) < p.Raw;
        }

        /// <summary>
        /// Weighted pick: draws u in [0, ΣWeights), then walks the weights in declared order.
        /// Weights are non-negative and their sum must be greater than 0.
        /// </summary>
        public static int WeightedPick(ulong r, int[] weights)
        {
            long total = 0;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] < 0) throw new ArgumentException("Weights must be non-negative.", nameof(weights));
                total += weights[i];
            }
            if (total <= 0) throw new ArgumentException("The sum of weights must be greater than 0.", nameof(weights));

            long u = (long)Below(r, (ulong)total);
            for (int i = 0; i < weights.Length; i++)
            {
                u -= weights[i];
                if (u < 0) return i;
            }
            throw new InvalidOperationException("Unreachable: weighted pick walked past the total.");
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
