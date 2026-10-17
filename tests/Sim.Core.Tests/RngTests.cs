using System;
using NUnit.Framework;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public class RngTests
    {
        // Spec 001 AC-3. Golden values were computed by an independent Python implementation
        // of the same definition (Rng.Next's doc comment), not by this code.
        [Test]
        public void CounterBased_GoldenValues_AndStreamIndependence()
        {
            ulong[] golden =
            {
                0x9E396DF9DB385D18UL,
                0x59228549847635E6UL,
                0x9E61C8801B0FBD4CUL,
                0x0F4B0FD4994B2C18UL,
                0xDFF4E7D6494D9A69UL,
            };
            for (ulong c = 0; c < 5; c++)
                Assert.That(Rng.Next(42, RngStream.Weather, 0, c), Is.EqualTo(golden[c]), "counter " + c);

            Assert.That(Rng.Next(42, RngStream.Forecast, 0, 0), Is.EqualTo(0x98718FAEEFBA8041UL));

            // Different streams with the same seed, key and counter differ.
            for (ulong key = 0; key < 100; key++)
                Assert.That(Rng.Next(42, RngStream.Weather, key, 0), Is.Not.EqualTo(Rng.Next(42, RngStream.Forecast, key, 0)));

            // Order independence: drawing in a different order gives the same values.
            Assert.That(Rng.Next(42, RngStream.Weather, 0, 4), Is.EqualTo(golden[4]));
        }

        // Spec 001 AC-4.
        [Test]
        public void Uniform_IsUnbiased()
        {
            const int n = 10, draws = 100000;
            var counts = new int[n];
            for (int i = 0; i < draws; i++)
                counts[Rng.Below(Rng.Next(7, RngStream.Weather, (ulong)i, 0), n)]++;

            double expected = draws / (double)n, chi2 = 0;
            foreach (int c in counts) chi2 += (c - expected) * (c - expected) / expected;
            // Critical value of chi-square with 9 degrees of freedom at p = 0.001.
            Assert.That(chi2, Is.LessThan(27.877), "chi2 = " + chi2);
        }

        [Test]
        public void Below_UsesMultiplyHigh()
        {
            Assert.That(Rng.Below(0, 10), Is.EqualTo(0UL));
            Assert.That(Rng.Below(ulong.MaxValue, 10), Is.EqualTo(9UL));
            Assert.That(Rng.Below(1UL << 63, 10), Is.EqualTo(5UL));
            // Python reference: (Next × 10) >> 64 for counters 0–4.
            ulong[] expected = { 6, 3, 6, 0, 8 };
            for (ulong c = 0; c < 5; c++)
                Assert.That(Rng.Below(Rng.Next(42, RngStream.Weather, 0, c), 10), Is.EqualTo(expected[c]));
        }

        [Test]
        public void WeightedPick_WalksDeclaredOrder()
        {
            int[] w = { 0, 3, 0, 1 };
            // u = Below(r, 4): r = 0 → u 0 → index 1; top of range → u 3 → index 3.
            Assert.That(Rng.WeightedPick(0, w), Is.EqualTo(1));
            Assert.That(Rng.WeightedPick(ulong.MaxValue, w), Is.EqualTo(3));
            Assert.That(Rng.WeightedPick((3UL << 62) - 1, w), Is.EqualTo(1)); // u = 2
            Assert.That(Rng.WeightedPick(3UL << 62, w), Is.EqualTo(3));       // u = 3

            Assert.That(() => Rng.WeightedPick(0, new[] { 0, 0 }), Throws.ArgumentException);
            Assert.That(() => Rng.WeightedPick(0, new[] { 1, -1 }), Throws.ArgumentException);
        }

        [Test]
        public void WeightedPick_ZeroWeightNeverDrawn()
        {
            int[] w = { 5, 0, 5 };
            for (ulong i = 0; i < 10000; i++)
                Assert.That(Rng.WeightedPick(Rng.Next(1, RngStream.Weather, i, 0), w), Is.Not.EqualTo(1));
        }

        [Test]
        public void Chance_IsExactAtBounds_AndMatchesRate()
        {
            Assert.That(Rng.Chance(ulong.MaxValue, Fixed.One), Is.True);
            Assert.That(Rng.Chance(0, Fixed.Zero), Is.False);
            int hits = 0;
            Fixed p = Fixed.FromRatio(80, 100);
            for (ulong i = 0; i < 100000; i++)
                if (Rng.Chance(Rng.Next(3, RngStream.Forecast, i, 1), p)) hits++;
            Assert.That(hits, Is.InRange(79500, 80500));
        }
    }
}
