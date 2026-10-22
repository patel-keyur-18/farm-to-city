using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    /// <summary>Spec 011 §3.1b and §3.2: the daily price and the stall closed at night.</summary>
    public partial class MarketTests
    {
        // Spec 011 AC-4b.
        [Test]
        public void DailyPrice_BoundedAndNeutral()
        {
            Fixed[,] Run(ulong seed)
            {
                Simulation sim = TestSim.NewFarm(seed, game: TestSim.Quiet());
                var goods = sim.Config.Game.Market.Goods;
                var f = new Fixed[1000, goods.Count];
                for (int day = 0; day < 1000; day++)
                {
                    sim.State.Clock.SimTime = day * TestSim.Day + 6 * TestSim.Hour;
                    MarketSystem.DrawDailyFactors(sim);
                    for (int g = 0; g < goods.Count; g++) f[day, g] = sim.State.Market.DailyFactorOf(g);
                }
                return f;
            }

            Fixed[,] a = Run(7);
            Simulation probe = TestSim.NewFarm(7, game: TestSim.Quiet());
            var defs = probe.Config.Game.Market.Goods;
            for (int g = 0; g < defs.Count; g++)
            {
                GoodDef good = defs[g];
                bool moves = good.Kind == GoodKind.Produce && !good.PriceControlled;
                long sum = 0;
                for (int day = 0; day < 1000; day++)
                {
                    Fixed f = a[day, g];
                    if (!moves) { Assert.That(f, Is.EqualTo(Fixed.One), good.Id + " doesn't move"); continue; }
                    Assert.That(f, Is.InRange(Fixed.One - good.DailyBand, Fixed.One + good.DailyBand), good.Id + " day " + day);
                    sum += f.Raw;
                }
                if (!moves) continue;
                Assert.That(good.DailyVolatility, Is.GreaterThan(Fixed.Zero), good.Id + " has a daily price");
                double mean = sum / 1000.0 / Fixed.One.Raw;
                Assert.That(mean, Is.InRange(0.99, 1.01), good.Id + ": neutral on average");
            }
            Assert.That(Run(7), Is.EqualTo(a), "the same seed gives the same factors");
            Assert.That(Run(8), Is.Not.EqualTo(a));

            // The regional price carries today's factor; inputs keep their seasonal price.
            int potato = probe.Config.Game.Market.IndexOf("potato"), seed = probe.Config.Game.Market.IndexOf("seed_potato");
            Fixed before = MarketRules.RegionalPrice(probe, potato), input = MarketRules.InputPrice(probe, seed);
            probe.AdvanceTo(6 * TestSim.Hour);
            Assert.That(MarketRules.RegionalPrice(probe, potato), Is.EqualTo(before * probe.State.Market.DailyFactorOf(potato)));
            Assert.That(MarketRules.InputPrice(probe, seed), Is.EqualTo(input));
        }

        // Spec 011 AC-6.
        [Test]
        public void Stall_PausedAtNight()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet(TestSim.NoSpoilage));
            Assert.That(sim.Execute(new SkipFirstSession(0)).Accepted, Is.True);
            int potato = Good(sim, "potato");
            sim.AdvanceTo(12 * TestSim.Hour);
            PutOnOffer(sim, potato, 3_000_000); // a glut: the price falls under the regional price
            sim.AdvanceTo(TestSim.Day);
            Fixed Gap() { return Fixed.One - sim.State.Market.MarketPrice(potato) / MarketRules.RegionalPrice(sim, potato); }
            Fixed atMidnight = Gap();
            Assert.That(atMidnight, Is.GreaterThan(Fixed.FromRatio(1, 20)), "glutted");
            long offered = sim.State.Market.OnOffer(potato);
            sim.DrainEvents();

            for (int h = 1; h <= 6; h++)
            {
                sim.AdvanceTo(TestSim.Day + h * TestSim.Hour);
                Assert.That(sim.PendingEvents.OfType<MarketSales>().Where(e => e.GoodId == "potato"), Is.Empty, "nothing sells at the " + h + ":00 pass");
                Assert.That(MarketRules.StallClosed(sim), Is.EqualTo(h < 6));
            }
            Assert.That(sim.State.Market.OnOffer(potato), Is.EqualTo(offered), "the offer stays in the store");
            Assert.That(Gap(), Is.LessThan(atMidnight.MulRatio(1, 2)), "the price recovers towards the regional price overnight");
            Assert.That(sim.State.Market.TraderBought(potato), Is.EqualTo(0));

            sim.AdvanceTo(TestSim.Day + 7 * TestSim.Hour);
            Assert.That(sim.PendingEvents.OfType<MarketSales>().Single(e => e.GoodId == "potato").LocalQuantity, Is.GreaterThan(0), "it sells again from the 7:00 pass");
        }

        // Spec 011 §3.1b: the good's sheet compares today's truck price with yesterday's, also across a season's start.
        [Test]
        public void TruckPrice_TodayAndYesterday()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            Assert.That(sim.Execute(new SkipFirstSession(0)).Accepted, Is.True);
            int rice = Good(sim, "rice");
            long lastDay = sim.Clock.SecondsPerSeason / TestSim.Day - 1;
            sim.AdvanceTo(lastDay * TestSim.Day + 7 * TestSim.Hour); // the season's last day
            Fixed before = MarketRules.Floor(sim, rice);
            Assert.That(sim.State.Market.TruckPrice[rice], Is.EqualTo(before));
            sim.AdvanceTo((lastDay + 1) * TestSim.Day + 7 * TestSim.Hour); // the next season's first morning
            Assert.That(sim.State.Market.TruckPriceYesterdayOf(rice), Is.EqualTo(before), "yesterday's price, not today's season");
            Assert.That(sim.State.Market.TruckPrice[rice], Is.EqualTo(MarketRules.Floor(sim, rice)));
            Assert.That(TestSim.NewFarm(game: TestSim.Quiet()).State.Market.TruckPriceYesterdayOf(rice), Is.Null, "a new game has no yesterday");
        }
    }
}
