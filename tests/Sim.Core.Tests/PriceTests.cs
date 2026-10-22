using System.Linq;
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
            Simulation sim = TestSim.NewFarm(game: TestSim.NoInflation(TestSim.NoTruck)); // t = 0: Spring; index 1.00 throughout, no daily move
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
            AssertPrice(MarketRules.InputPrice(sim, Good(sim, "sapling_mango")), "350");
        }

        // Spec 004 §3: Value(q, P) = round(q × P × 100 ÷ unitSize), rounded once.
        [Test]
        public void Value_RoundsOnce()
        {
            Assert.That(Money.Value(1_000_000, Fixed.Parse("5.30"), 1000), Is.EqualTo(5_300_00)); // 1,000 kg potato
            Assert.That(Money.Value(1, Fixed.Parse("5.30"), 1000), Is.EqualTo(1));                 // 1 g: 0.0053 coin → 0.01
            Assert.That(Money.Value(1, Fixed.Parse("350"), 1), Is.EqualTo(350_00));                // one sapling
            Assert.That(Money.Value(1_687_500, Fixed.Parse("42.75"), 1000), Is.EqualTo(7_214_063)); // 1,687.5 kg × 42.75 = 72,140.625 coins
        }

        // Spec 004 AC-3: the index rises every season by 0.5–1.5%, seeded.
        [Test]
        public void Index_SeasonalInflation()
        {
            Fixed[] Path(ulong seed)
            {
                Simulation sim = TestSim.NewFarm(seed, game: TestSim.Quiet());
                var path = new Fixed[40];
                for (int s = 1; s <= 40; s++)
                {
                    sim.AdvanceTo(s * sim.Clock.SecondsPerSeason);
                    path[s - 1] = sim.State.Market.PriceIndex;
                }
                return path;
            }
            Fixed[] a = Path(7), b = Path(7), c = Path(8);
            Fixed prev = Fixed.One;
            foreach (Fixed index in a)
            {
                Fixed step = index / prev;
                Assert.That(step, Is.GreaterThanOrEqualTo(Fixed.Parse("1.005")).And.LessThanOrEqualTo(Fixed.Parse("1.015")), "step " + step);
                prev = index;
            }
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a, Is.Not.EqualTo(c));
            Assert.That(a[39], Is.GreaterThanOrEqualTo(Fixed.Parse("1.22")).And.LessThanOrEqualTo(Fixed.Parse("1.79"))); // spec 004 §9
        }

        // Spec 004 AC-20: chemical fertiliser ignores the index; seed follows it.
        [Test]
        public void PriceControlled_IgnoresIndex()
        {
            Simulation sim = TestSim.NewFarm(3, game: TestSim.Quiet());
            int fert = Good(sim, "fertiliser_chemical"), seed = Good(sim, "seed_wheat");
            Fixed fert0 = MarketRules.InputPrice(sim, fert), seed0 = MarketRules.InputPrice(sim, seed);
            sim.AdvanceTo(40 * sim.Clock.SecondsPerSeason); // 10 years later, Spring again
            Assert.That(sim.State.Market.PriceIndex, Is.GreaterThan(Fixed.Parse("1.2")));
            Assert.That(MarketRules.InputPrice(sim, fert), Is.EqualTo(fert0));
            AssertPrice(MarketRules.InputPrice(sim, seed), (seed0 * sim.State.Market.PriceIndex).ToString());
        }

        // Spec 004 AC-24: every indexed coin amount is charged at MulRate(amount, PriceIndex).
        [Test]
        public void Indexed_CoinAmounts()
        {
            Simulation sim = TestSim.NewFarm(5, game: TestSim.RichGame());
            sim.AdvanceTo(8 * sim.Clock.SecondsPerSeason); // two years of inflation
            Fixed index = sim.State.Market.PriceIndex;
            Assert.That(index, Is.GreaterThan(Fixed.One));
            Assert.That(MarketRules.Indexed(sim, 18_000_00), Is.EqualTo(Money.MulRate(18_000_00, index)));

            // Land: acreBasePrice × index × the tile's factors.
            var tile = sim.State.Land.Tiles[84];
            GameDefs defs = sim.Config.Game;
            Fixed factor = defs.Soils[tile.Soil].PriceFactor * defs.Drainages[tile.Drainage].PriceFactor
                * Sim.Core.Land.LandRules.FertilityPriceFactor(defs.Land, tile.Fertility) * defs.Map.Parcels[tile.ParcelId].LocationFactor;
            Assert.That(Sim.Core.Land.LandRules.TilePrice(sim, tile),
                Is.EqualTo(Money.RoundToCoin(Money.MulRate(Money.MulRate(defs.Land.AcreBasePrice, index), factor))));

            // Living costs: the season's basic plan paid at the close at the index then.
            long paid = 0;
            foreach (var p in sim.State.Finance.Prepayments) if (p.Start == sim.State.Clock.SimTime) paid += p.Total;
            Assert.That(paid, Is.EqualTo(Money.MulRate(18_000_00, index)));

            // Day labour: 100% for one day at the indexed wage.
            sim.Execute(new Sim.Core.Commands.SetLabourSplit(sim.State.Clock.SimTime, 100));
            Fixed hours = Sim.Core.Farm.FounderRules.LabourRate(sim);
            long cash = sim.State.Ledger.Cash;
            var events = sim.DrainEvents();
            sim.AdvanceTo(sim.State.Clock.SimTime + TestSim.Day);
            var pay = sim.DrainEvents().OfType<Sim.Core.Events.DayLabourPaid>().Single();
            Assert.That(pay.Amount, Is.EqualTo(Money.MulRate(Money.MulRate(defs.Farm.Founder.DayLabourWagePerHour, index), pay.Hours)));

            // Medicine and the mandi's cart hire.
            index = sim.State.Market.PriceIndex;
            cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new Sim.Core.Commands.BuyMedicine(sim.State.Clock.SimTime)).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.MulRate(defs.Farm.Founder.MedicineCost, index)));
            sim.State.Market.Stock[Good(sim, "wheat")] += 10_000;
            cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new Sim.Core.Commands.GoToMandi(sim.State.Clock.SimTime, new[] { new MandiLoad("wheat", 10_000) })).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.MulRate(defs.Market.Mandi.CartHirePerTrip, index)));
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

        // Spec 004 AC-1.
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
            Assert.That(MarketError("goods.json", "\"unit\": \"litre\"", "\"unit\": \"gallon\"").FieldPath, Is.EqualTo("goods.goods[22].unit")); // spray, after straw and cattle feed (spec 012)
            Assert.That(MarketError("goods.json", "\"basePrice\": 60, \"weightKg", "\"basePrice\": 0, \"weightKg").FieldPath, Is.EqualTo("goods.goods[23].basePrice"));
            Assert.That(MarketError("region.json", "\"importCapacityPerDay\": { \"radish\": 15", "\"importCapacityPerDay\": { \"radish\": 0").FieldPath,
                Is.EqualTo("region.importCapacityPerDay.radish"));
            Assert.That(MarketError("market.json", "\"railMin\": 0.5", "\"railMin\": 1").FieldPath, Is.EqualTo("market.market.railMin"));
            Assert.That(MarketError("market.json", "\"inflationMaxPerYear\": 0.06", "\"inflationMaxPerYear\": 0.01").FieldPath,
                Is.EqualTo("market.economy.inflationMaxPerYear"));
        }
    }
}
