using NUnit.Framework;
using Sim.Content;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public class PriceTests
    {
        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        /// <summary>
        /// Prices are products of decimal factors that aren't exact in binary (0.85, 1.10), so they match the spec's
        /// decimal examples to within a few units in the last place (2⁻³²), far below 1/100 coin on any quantity.
        /// </summary>
        private static void AssertPrice(Fixed actual, string expected)
        {
            Assert.That(System.Math.Abs(actual.Raw - Fixed.Parse(expected).Raw), Is.LessThanOrEqualTo(16), actual + " vs " + expected);
        }

        // Spec 004 AC-2.
        [Test]
        public void Regional_Band()
        {
            Simulation sim = TestSim.NewFarm(); // t = 0: Spring
            int potato = Good(sim, "potato");
            AssertPrice(MarketRules.RegionalPrice(sim, potato), "6.80");
            AssertPrice(MarketRules.Floor(sim, potato), "5.30");
            AssertPrice(MarketRules.Ceiling(sim, potato), "8.30");

            sim.AdvanceTo(3 * sim.Clock.SecondsPerSeason); // Winter
            int wheat = Good(sim, "wheat");
            AssertPrice(MarketRules.RegionalPrice(sim, wheat), "27.50");
            AssertPrice(MarketRules.Floor(sim, wheat), "26.00");
            AssertPrice(MarketRules.Ceiling(sim, wheat), "29.00");
        }

        // Spec 004 §3.1: inputs cost P_regional; seed is 10% dearer in its sowing season.
        [Test]
        public void InputPrices_Seasonal()
        {
            Simulation sim = TestSim.NewFarm();
            AssertPrice(MarketRules.InputPrice(sim, Good(sim, "seed_potato")), "22");     // Spring: sown
            AssertPrice(MarketRules.InputPrice(sim, Good(sim, "seed_wheat")), "42.75");   // Spring: not sown
            AssertPrice(MarketRules.InputPrice(sim, Good(sim, "fertiliser_chemical")), "31.5");
            AssertPrice(MarketRules.InputPrice(sim, Good(sim, "sapling_mango")), "400");
        }

        // Spec 004 §3: Value(q, P) = round(q × P × 100 ÷ unitSize), rounded once.
        [Test]
        public void Value_RoundsOnce()
        {
            Assert.That(Money.Value(1_000_000, Fixed.Parse("5.30"), 1000), Is.EqualTo(5_300_00)); // 1,000 kg potato
            Assert.That(Money.Value(1, Fixed.Parse("5.30"), 1000), Is.EqualTo(1));                 // 1 g: 0.0053 coin → 0.01
            Assert.That(Money.Value(1, Fixed.Parse("400"), 1), Is.EqualTo(400_00));                // one sapling
            Assert.That(Money.Value(1_687_500, Fixed.Parse("42.75"), 1000), Is.EqualTo(7_214_063)); // 1,687.5 kg × 42.75 = 72,140.625 coins
        }

        // Spec 004 §3.3: the trader's daily capacity resets at each daily pass.
        [Test]
        public void TraderCapacity_ResetsDaily()
        {
            Simulation sim = TestSim.NewFarm();
            int potato = Good(sim, "potato");
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(3_000_000));
            sim.State.Market.TraderBoughtToday[potato] = 2_500_000;
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(500_000));
            sim.AdvanceTo(TestSim.Day);
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(3_000_000));
        }
    }

    public partial class ContentTests
    {
        private static ContentException MarketError(string file, string from, string to)
        {
            return Assert.Throws<ContentException>(() => TestContent.Game((f, j) => f == file ? j.Replace(from, to) : j));
        }

        // Spec 004 AC-1 (the goods and region checks; the market checks join in Sprint 3).
        [Test]
        public void Market_Validation()
        {
            Assert.That(MarketError("goods.json", "\"elasticity\": 1.2, \"spoilagePerDay\": 0.25,", "\"elasticity\": 0, \"spoilagePerDay\": 0.25,").FieldPath,
                Is.EqualTo("goods.goods[0].elasticity"));
            Assert.That(MarketError("region.json", "\"radish\":    [1.00, 1.15", "\"radish\":    [2.50, 1.15").FieldPath,
                Is.EqualTo("region.seasonalFactor.radish[0]"));
            Assert.That(MarketError("region.json", "\"wheat\":     [0.95", "\"wheet\":     [0.95").FieldPath,
                Is.EqualTo("region.seasonalFactor.wheet"));
            Assert.That(MarketError("region.json", "\"mango\": 400, ", "").Message, Does.Contain("missing produce good 'mango'"));
            Assert.That(MarketError("region.json", "\"roadTier\": \"track\"", "\"roadTier\": \"rail\"").FieldPath, Is.EqualTo("region.roadTier"));
            Assert.That(MarketError("goods.json", "\"unit\": \"litre\"", "\"unit\": \"gallon\"").FieldPath, Is.EqualTo("goods.goods[20].unit"));
            Assert.That(MarketError("goods.json", "\"basePrice\": 150, \"weightKg", "\"basePrice\": 0, \"weightKg").FieldPath, Is.EqualTo("goods.goods[21].basePrice"));
        }
    }
}
