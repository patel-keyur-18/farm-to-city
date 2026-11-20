using System.Linq;
using NUnit.Framework;
using Sim.Core.Maths;
using Tools.Balancer;

namespace Sim.Core.Tests
{
    /// <summary>US-151 (spec 026 §9, ADR 0127): the town life of a Township the competent bot runs, two game years after the unlock.</summary>
    [Category("Balance")]
    public class TownLifeGateTests
    {
        private static double Median(System.Collections.Generic.IEnumerable<double> xs) { var l = xs.Where(x => !double.IsNaN(x)).OrderBy(x => x).ToList(); return l.Count == 0 ? double.NaN : l[l.Count / 2]; }

        private static int Seeds { get { string v = System.Environment.GetEnvironmentVariable("FTC_SEEDS"); return v == null ? 12 : int.Parse(v); } }

        private static Gate.TownshipResult Run(int seeds, int closes = 8)
        {
            TestContent.WithCohort = true;
            try
            {
                var config = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game());
                return Gate.Township(config, () => new TownshipBot(false), seeds, 80, closes);
            }
            finally { TestContent.WithCohort = false; }
        }

        [Test]
        public void Measure()
        {
            Gate.TownshipResult r = Run(Seeds);
            TestContext.Out.WriteLine("TOWNLIFE crime " + string.Join(" ", r.Crime.Select(x => x.ToString("0.0"))) + "; unemployed% " + string.Join(" ", r.Unemployed.Select(x => x.ToString("0.0")))
                + "; ratio " + string.Join(" ", r.Ratio.Select(x => x.ToString("0.0"))) + "; reputation mean " + string.Join(" ", r.RepMean.Select(x => x.ToString("0.0")))
                + "; approval " + string.Join(" ", r.Approval.Select(x => x.ToString("0"))) + "; pull " + string.Join(" ", r.Pull.Select(x => x.ToString("0"))) + "; happiness " + string.Join(" ", r.Happiness.Select(x => x.ToString("0.0")))
                + "; households " + string.Join(" ", r.HouseholdsAfter));
            foreach (string i in r.Incomes) TestContext.Out.WriteLine("INCOMES " + i);
            Assert.That(r.Broken, Is.EqualTo(0));
        }
    }
}
