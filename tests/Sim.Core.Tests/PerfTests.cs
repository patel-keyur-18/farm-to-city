using System;
using System.Diagnostics;
using NUnit.Framework;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>
    /// Catch-up must stay fast (spec 001 AC-28; the GDD budget is 2 s on device). CI runs these in a Release
    /// build. Each case warms up once (JIT), then takes the best of 5 runs to filter out noise on shared runners.
    /// </summary>
    [Category("Perf")]
    public partial class PerfTests
    {
        private static double BestMilliseconds(long seconds)
        {
            Advance(seconds); // warm-up
            double best = double.MaxValue;
            for (int run = 0; run < 5; run++)
                best = Math.Min(best, Advance(seconds));
            return best;
        }

        private static double Advance(long seconds)
        {
            Simulation sim = New(seed: 3, localSeconds: 8 * Hour);
            long target = sim.State.Clock.SimTime + seconds;
            var watch = Stopwatch.StartNew();
            sim.AdvanceTo(target, AdvanceMode.Away);
            watch.Stop();
            Assert.That(sim.State.Clock.SimTime, Is.EqualTo(target));
            return watch.Elapsed.TotalMilliseconds;
        }

        // Spec 001 AC-28.
        [Test]
        public void Advance72h_And1Year()
        {
            double h72 = BestMilliseconds(72 * Hour);
            double year = BestMilliseconds(TestContent.Clock().SecondsPerYear);
            TestContext.Out.WriteLine("72 h: " + h72.ToString("0.000") + " ms; 1 year: " + year.ToString("0.000") + " ms");
            Assert.That(h72, Is.LessThan(20), "72 h advance");
            Assert.That(year, Is.LessThan(100), "1 game year advance");
        }
    
        // Spec 002 AC-16.
        [Test]
        public void Advance72h_WithLand()
        {
            double best = double.MaxValue;
            for (int run = 0; run < 6; run++) // the first run warms up the JIT
            {
                Simulation sim = NewFarm(seed: 3, localSeconds: 8 * Hour);
                long target = sim.State.Clock.SimTime + 72 * Hour;
                var watch = Stopwatch.StartNew();
                sim.AdvanceTo(target, AdvanceMode.Away);
                watch.Stop();
                if (run > 0) best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            TestContext.Out.WriteLine("72 h with land: " + best.ToString("0.000") + " ms");
            Assert.That(best, Is.LessThan(20));
        }

        // Spec 004 AC-17: the market (11 produce goods, every one on offer), the store and the default farm.
        [Test]
        public void Advance72h_WithMarket()
        {
            double best = double.MaxValue;
            for (int run = 0; run < 6; run++) // the first run warms up the JIT
            {
                Simulation sim = NewFarm(seed: 3, localSeconds: 8 * Hour, game: RichGame());
                var goods = sim.Config.Game.Market.Goods;
                for (int g = 0; g < goods.Count; g++)
                    if (goods[g].Kind == Sim.Core.Definitions.GoodKind.Produce) { sim.State.Market.Stock[g] += 100_000; sim.State.Market.Offered[g] += 100_000; }
                long target = sim.State.Clock.SimTime + 72 * Hour;
                var watch = Stopwatch.StartNew();
                sim.AdvanceTo(target, AdvanceMode.Away);
                watch.Stop();
                if (run > 0) best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            TestContext.Out.WriteLine("72 h with the market: " + best.ToString("0.000") + " ms");
            Assert.That(best, Is.LessThan(20));
        }
    }
}
