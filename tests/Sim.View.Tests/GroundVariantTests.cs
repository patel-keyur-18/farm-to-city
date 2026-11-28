using NUnit.Framework;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>The ground's three variants are picked per acre by a fixed hash (US-273, ADR 0184).</summary>
    public class GroundVariantTests
    {
        // The same acre always gets the same variant, every variant is used about a third of the time over the map, and no acre has
        // the same variant as all four of its neighbours more often than chance allows (no stripes or blocks).
        [Test]
        public void PickIsSteadyEvenAndNotStriped()
        {
            var counts = new int[GroundVariants.Count];
            int same = 0, cells = 0;
            for (int y = 0; y < 18; y++)
                for (int x = 0; x < 24; x++)
                {
                    int v = GroundVariants.Pick(x, y);
                    Assert.That(v, Is.InRange(0, GroundVariants.Count - 1));
                    Assert.That(GroundVariants.Pick(x, y), Is.EqualTo(v), "steady");
                    counts[v]++;
                    cells++;
                    if (x > 0 && y > 0 && v == GroundVariants.Pick(x - 1, y) && v == GroundVariants.Pick(x, y - 1) && v == GroundVariants.Pick(x - 1, y - 1)) same++;
                }
            foreach (int c in counts) Assert.That(c / (double)cells, Is.InRange(0.25, 0.42), "each variant about a third");
            Assert.That(same / (double)cells, Is.LessThan(0.08), "no blocks of one variant (chance is 1 in 27)");
        }
    }
}
