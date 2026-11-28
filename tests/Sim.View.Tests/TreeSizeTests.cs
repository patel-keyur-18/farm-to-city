using System.Collections.Generic;
using NUnit.Framework;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Repeated trees are drawn a little bigger or smaller by their place (US-275, ADR 0186).</summary>
    public class TreeSizeTests
    {
        // The same place always gets the same size, every size stays within 8% of the picture's own, the whole range is used and
        // an orchard's trees aren't all alike.
        [Test]
        public void FactorIsSteadyWithinEightPercentAndMixed()
        {
            var seen = new HashSet<float>();
            for (int tile = 0; tile < 24 * 18; tile++)
            {
                var acre = new HashSet<float>();
                for (int n = 0; n < 16; n++)
                {
                    float k = TreeSizes.Factor(tile, n);
                    Assert.That(k, Is.InRange(0.92f - 0.0001f, 1.08f + 0.0001f));
                    Assert.That(TreeSizes.Factor(tile, n), Is.EqualTo(k), "steady");
                    seen.Add(k);
                    acre.Add(k);
                }
                Assert.That(acre.Count, Is.GreaterThanOrEqualTo(4), "acre " + tile + " mixes sizes");
            }
            Assert.That(seen.Count, Is.EqualTo(17), "every step from 92% to 108%");
        }
    }
}
