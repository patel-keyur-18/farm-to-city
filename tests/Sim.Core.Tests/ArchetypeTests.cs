using System;
using System.Linq;
using NUnit.Framework;
using Tools.Balancer;

namespace Sim.Core.Tests
{
    /// <summary>US-099 (ADR 0016, ADR 0107 §11): the archetype bots finish within 20% of each other; no stress bot beats the best archetype by over 20%.</summary>
    [Category("Balance")]
    public class ArchetypeTests
    {
        private static SimConfig Live() { return new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game()); }

        private static int Env(string name, int fallback) { string v = Environment.GetEnvironmentVariable(name); return v == null ? fallback : int.Parse(v); }

        // ADR 0016 on Phase 2's systems (US-099): over seeds to game year 8 (the full 1,000 seeds at year 12 at the Phase 4 gate; 24 here so CI stays under its time limit, `FTC_SEEDS=200` for ADR 0107 §11's run, about 10 minutes),
        // the agri-hub, merchant and education towns finish within 20% of each other on median net worth (at year-0 prices) and on median town
        // happiness; no stress bot (debt-fuelled expansion, cautious saver, random) is over 20% above the best archetype on either; random is below every
        // archetype on both. Every archetype plants the mango orchard: it is the best use of 30,000 coins in the game (+24% net worth on the competent
        // bot by year 8) and a player of any town would do it, so leaving it to one archetype made that one 30% ahead (ADR 0112).
        [Test]
        public void Archetypes_WithinTwentyPercent()
        {
            int seeds = Env("FTC_SEEDS", 24), seasons = Env("FTC_SEASONS", 32);
            Archetypes.Row[] arch = { Archetypes.Measure(Live(), () => new AgriHubBot(), seeds, seasons), Archetypes.Measure(Live(), () => new MerchantBot(), seeds, seasons), Archetypes.Measure(Live(), () => new EducationBot(), seeds, seasons) };
            Archetypes.Row[] stress = { Archetypes.Measure(Live(), () => new LeverageBot(), seeds, seasons), Archetypes.Measure(Live(), () => new CautiousSaverBot(), seeds, seasons), Archetypes.Measure(Live(), () => new RandomBot(), seeds, seasons) };
            foreach (Archetypes.Row r in arch.Concat(stress))
                TestContext.Out.WriteLine(r.Bot.PadRight(15) + " net worth median " + r.MedianWorth.ToString("0").PadLeft(9) + "  happiness median " + r.MedianHappiness.ToString("0.0") + "  (" + seeds + " seeds, " + seasons + " seasons)");
            double bestWorth = arch.Max(r => r.MedianWorth), worstWorth = arch.Min(r => r.MedianWorth), bestHappy = arch.Max(r => r.MedianHappiness), worstHappy = arch.Min(r => r.MedianHappiness);
            TestContext.Out.WriteLine("archetypes: net worth best/worst " + (bestWorth / worstWorth).ToString("0.000") + ", happiness best/worst " + (bestHappy / worstHappy).ToString("0.000"));
            Assert.That(bestWorth / worstWorth, Is.LessThanOrEqualTo(1.2), "the best archetype's median net worth is at most 20% above the worst's");
            Assert.That(bestHappy / worstHappy, Is.LessThanOrEqualTo(1.2), "and its median happiness");
            foreach (Archetypes.Row r in stress)
            {
                Assert.That(r.MedianWorth, Is.LessThanOrEqualTo(bestWorth * 1.2), r.Bot + ": net worth against the best archetype");
                Assert.That(r.MedianHappiness, Is.LessThanOrEqualTo(bestHappy * 1.2), r.Bot + ": happiness against the best archetype");
            }
            Archetypes.Row random = stress.Single(r => r.Bot == "random");
            foreach (Archetypes.Row a in arch)
            {
                Assert.That(random.MedianWorth, Is.LessThan(a.MedianWorth), "random finishes below " + a.Bot + " on net worth");
                Assert.That(random.MedianHappiness, Is.LessThan(a.MedianHappiness), "and on happiness");
            }
        }

        [Test, Explicit("measure")]
        public void Measure()
        {
            int seeds = Env("FTC_SEEDS", 8), seasons = Env("FTC_SEASONS", 32);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            foreach (Func<Bot> make in new Func<Bot>[] { () => new AgriHubBot(), () => new MerchantBot(), () => new EducationBot(), () => new FarmerBot(),  () => new LeverageBot(), () => new CautiousSaverBot(), () => new RandomBot() })
            {
                Archetypes.Row r = Archetypes.Measure(Live(), make, seeds, seasons);
                TestContext.Out.WriteLine(r.Bot.PadRight(14) + " net worth median " + r.MedianWorth.ToString("0").PadLeft(10) + "  happiness median " + r.MedianHappiness.ToString("0.0")
                    + "  (" + watch.Elapsed.TotalSeconds.ToString("0") + " s) worths " + string.Join(" ", r.NetWorth.Select(x => (x / 1000).ToString("0")).Take(12)) + " k");
            }
        }

        [Test, Explicit("measure")]
        public void Orchards()
        {
            foreach (Func<Bot> make in new Func<Bot>[] { () => new MerchantBot(true), () => new EducationBot(true), () => new AgriHubBot(false, "mango") })
            {
                Simulation sim = Economy.RunBot(Live(), make(), 32, 1, null).Sim;
                TestContext.Out.WriteLine(make().Name + ": orchards " + sim.State.Orchards?.Orchards.Count + " cash " + sim.State.Ledger.Cash / 100 + " worth " + Sim.Core.Finance.Reporting.NetWorth(sim).Total / 100
                    + " last acre " + Home.Acres(sim.Config.Game).Last() + " businesses " + sim.State.Businesses?.Businesses.Count);
            }
        }
    }
}
