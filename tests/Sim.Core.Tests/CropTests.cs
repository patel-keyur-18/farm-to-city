using System;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Helpers for spec 003 tests. Inherited farm tiles: 18, 19, 26, 27 rain-fed; 20, 28 the well acres.</summary>
    public static class FarmTest
    {
        public const long S = 4 * Day;

        /// <summary>Rich content where every crop can be sown in every season (so tests don't wait for Winter).</summary>
        public static GameDefs AnySeason(Func<string, string, string> edit = null)
        {
            return RichGame((f, j) =>
            {
                if (f == "crops.json")
                    foreach (string s in new[] { "[\"winter\"]", "[\"summer\", \"monsoon\"]", "[\"spring\", \"winter\"]", "[\"summer\", \"winter\"]" })
                        j = j.Replace("\"sowSeasons\": " + s, "\"sowSeasons\": [\"spring\", \"summer\", \"monsoon\", \"winter\"]");
                return edit == null ? j : edit(f, j);
            });
        }

        public static Simulation Farm(GameDefs game = null, ulong seed = 42) { return NewFarm(seed: seed, game: game ?? AnySeason()); }

        public static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        public static int Weather(Simulation sim, string id) { return sim.Weather.IndexOf(id); }

        /// <summary>Sets every generated spell from <paramref name="fromSpell"/> to the given weather.</summary>
        public static void SetWeather(Simulation sim, long fromSpell, string id)
        {
            for (long s = fromSpell; s <= sim.State.Weather.LastGeneratedSpell; s++) sim.State.Weather.SetSpell(s, Weather(sim, id));
            // The land system captured the current hour's weather when the hour began; keep it consistent.
            if (Calendar.SpellAt(sim.State.Clock.SimTime, sim.Clock) >= fromSpell) sim.State.Land.HourWeather = Weather(sim, id);
            Sim.Core.Farm.FarmSystem.Refresh(sim);
        }

        public static void AssertNear(Fixed actual, string expected, int ulps = 64)
        {
            Assert.That(Math.Abs(actual.Raw - Fixed.Parse(expected).Raw), Is.LessThanOrEqualTo(ulps), actual + " vs " + expected);
        }

        public static Crop Crop(Simulation sim, int tile) { return sim.State.Farm.CropOn(tile); }
    }

    public partial class CropTests
    {
        private const long S = FarmTest.S;

        // Spec 003 AC-2.
        [Test]
        public void CycleShare_PerSeasonRealism()
        {
            Simulation sim = FarmTest.Farm();
            FarmDefs f = sim.Config.Game.Farm;
            CropDef wheat = f.Crops[f.IndexOfCrop("wheat")], rice = f.Crops[f.IndexOfCrop("rice")];
            Assert.That(FarmRules.CycleShare(sim.Clock, wheat), Is.EqualTo(Fixed.Parse("0.0625")));
            Assert.That(FarmRules.CycleShare(sim.Clock, rice), Is.EqualTo(Fixed.Parse("0.25")));
            Assert.That(wheat.SeasonYieldPerAcre * FarmRules.CycleShare(sim.Clock, wheat), Is.EqualTo(Fixed.FromInt(75)));
            Assert.That(wheat.SeedPerAcrePerSeason * FarmRules.CycleShare(sim.Clock, wheat), Is.EqualTo(Fixed.Parse("1.6875")));
        }

        // Spec 003 AC-3.
        [Test]
        public void Sow_RulesAndCosts()
        {
            Simulation sim = FarmTest.Farm();
            long cash = sim.State.Ledger.Cash;
            int seed = FarmTest.Good(sim, "seed_radish"), chem = FarmTest.Good(sim, "fertiliser_chemical");
            // Radish: 8 kg × 0.0208333 = 167 g of seed (100 g in store, booked at 30.00; 67 g bought at 300 = 20.10); chemical
            // 150 kg × 0.0208333 = 3,125 g at 31.50 (Spring rush) = 98.44.
            long expected = Store.ShortfallCost(sim, seed, 167) + Store.ShortfallCost(sim, chem, 3125);
            Assert.That(expected, Is.EqualTo(20_10 + 98_44));

            Assert.That(sim.Execute(new Sow(60, 19, "radish", SeedSource.Certified, "chemical")).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(expected));
            Assert.That(sim.State.Ledger.Balance(Account.SeedsAndPlanting), Is.EqualTo(30_00 + 20_10)); // stock at cost, then the shortfall
            Assert.That(sim.State.Ledger.Balance(Account.FertiliserAndSpraying), Is.EqualTo(98_44));
            Assert.That(sim.State.Land.Tiles[19].Occupied, Is.True);
            Assert.That(sim.State.Market.InStore(seed), Is.EqualTo(0));
            Crop c = FarmTest.Crop(sim, 19);
            Assert.That(c.ReadyAt, Is.EqualTo(60 + 120 * 60));
            Assert.That(sim.UpcomingJobs(Day).Single(j => j.Type == FarmSystem.CropJob).DueTime, Is.EqualTo(60 + 7200));

            // Every rejection leaves the state unchanged.
            Simulation spring = NewFarm(game: RichGame());
            Assert.That(spring.Execute(new Sow(0, 19, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            void Rejects(Command cmd, RejectReason reason)
            {
                ulong before = spring.State.ComputeHash();
                Assert.That(spring.Execute(cmd).Reason, Is.EqualTo(reason));
                Assert.That(spring.State.ComputeHash(), Is.EqualTo(before), reason.ToString());
            }
            Rejects(new Sow(0, 0, "radish", SeedSource.Certified, "none"), RejectReason.NotOwned);
            Rejects(new Sow(0, 21, "radish", SeedSource.Certified, "none"), RejectReason.NotFarmTile);
            Rejects(new Sow(0, 19, "radish", SeedSource.Certified, "none"), RejectReason.TileOccupied);
            Rejects(new Sow(0, 18, "wheat", SeedSource.Certified, "none"), RejectReason.OutOfSeason);
            Rejects(new Sow(0, 18, "potato", SeedSource.Saved, "none"), RejectReason.NoSavedSeed);
            Rejects(new Sow(0, 18, "radish", SeedSource.Saved, "none"), RejectReason.SeedCannotBeSaved);
            Rejects(new Sow(0, 18, "maize", SeedSource.Certified, "none"), RejectReason.UnknownCrop);
            Rejects(new Sow(0, 18, "potato", SeedSource.Certified, "manure"), RejectReason.UnknownFertiliser);
            Rejects(new Sow(0, 99, "potato", SeedSource.Certified, "none"), RejectReason.UnknownTile);

            Simulation broke = NewFarm(game: TestContent.Game((f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 0") : j));
            ulong h = broke.State.ComputeHash();
            Assert.That(broke.Execute(new Sow(0, 18, "potato", SeedSource.Certified, "compost")).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(broke.State.ComputeHash(), Is.EqualTo(h));
            Assert.That(broke.Execute(new Sow(0, 18, "potato", SeedSource.Certified, "none")).Accepted, Is.True, "stock only: free");
        }

        // Spec 003 AC-31.
        [Test]
        public void Sow_UsesStockFirst()
        {
            Simulation sim = FarmTest.Farm();
            int seed = FarmTest.Good(sim, "seed_potato");
            long cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new Sow(0, 18, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash), "175 kg from the 600 kg inherited");
            Assert.That(sim.State.Market.InStore(seed), Is.EqualTo(425_000));

            // Compost: none in store, so 500 kg is bought at 2 × 1.05 = 2.10: 1,050.
            Assert.That(sim.Execute(new Sow(0, 19, "potato", SeedSource.Certified, "compost")).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(1_050_00));
            Assert.That(sim.State.Ledger.Balance(Account.FertiliserAndSpraying), Is.EqualTo(1_050_00));

            // The third and fourth acres take the last 250 kg of stock, then buy 100 kg at 22.
            Assert.That(sim.Execute(new Sow(0, 26, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            cash = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new Sow(0, 27, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(100 * 22_00));
            Assert.That(sim.State.Market.InStore(seed), Is.EqualTo(0));
        }

        // Spec 003 AC-4.
        [Test]
        public void ReadyWaits_HarvestToStore()
        {
            Simulation sim = FarmTest.Farm();
            Assert.That(sim.Execute(new Sow(0, 19, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(2 * Hour - 1);
            Assert.That(FarmTest.Crop(sim, 19).State, Is.EqualTo(CropState.Growing));
            sim.DrainEvents();
            sim.AdvanceTo(2 * Hour);
            Crop c = FarmTest.Crop(sim, 19);
            Assert.That(c.State, Is.EqualTo(CropState.Ready));
            CropReady ready = sim.DrainEvents().OfType<CropReady>().Single();
            Assert.That((ready.SimTime, ready.TileId, ready.Grams), Is.EqualTo((2 * Hour, 19, c.Breakdown.Grams)));
            long grams = c.Breakdown.Grams;
            Assert.That(grams, Is.InRange(50_000, 100_000)); // about 83 kg × the factors

            sim.AdvanceTo(2 * Hour + 72 * Hour); // waits the whole grace (ADR 0034) without loss
            Assert.That(FarmTest.Crop(sim, 19).HarvestGrams, Is.EqualTo(grams));
            int radish = FarmTest.Good(sim, "radish");
            long before = sim.State.Market.InStore(radish);
            Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 19)).Accepted, Is.True);
            Assert.That(sim.State.Market.InStore(radish) - before, Is.EqualTo(grams));
            Assert.That(sim.State.Land.Tiles[19].Occupied, Is.False);
            Assert.That(FarmTest.Crop(sim, 19), Is.Null);
            Assert.That(sim.Execute(new Harvest(3 * S, 19)).Reason, Is.EqualTo(RejectReason.NoCrop));

            Assert.That(sim.Execute(new Sow(3 * S, 19, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.Execute(new Harvest(3 * S, 19)).Reason, Is.EqualTo(RejectReason.NotReady));
            Assert.That(sim.Execute(new ClearField(3 * S + 60, 19)).Accepted, Is.True);
            Assert.That(sim.State.Land.Tiles[19].Occupied, Is.False);
            Assert.That(sim.UpcomingJobs(4 * S).Any(j => j.Type == FarmSystem.CropJob), Is.False);
            sim.AdvanceTo(4 * S); // the cancelled growth job doesn't fire
        }

        // Spec 003 AC-11.
        [Test]
        public void ShortCrop_UsesCurrentRates()
        {
            Simulation sim = FarmTest.Farm();
            sim.AdvanceTo(1800);
            Assert.That(sim.Execute(new Sow(1800, 19, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            Crop c = FarmTest.Crop(sim, 19);
            Fixed waterAtSowing = c.WaterRate, weatherAtSowing = c.WeatherRate, care = sim.State.Farm.CareRatio;
            sim.AdvanceTo(Hour);
            Assert.That(c.WaterSum, Is.EqualTo(waterAtSowing.MulRatio(1800, 1)));
            Assert.That(c.WeatherSum, Is.EqualTo(weatherAtSowing.MulRatio(1800, 1)));
            Assert.That(c.CareSum, Is.EqualTo(care.MulRatio(1800, 1)));
            sim.AdvanceTo(1800 + 7200 - 1);
            Assert.That(c.State, Is.EqualTo(CropState.Growing));
            sim.AdvanceTo(1800 + 7200);
            Assert.That(c.State, Is.EqualTo(CropState.Ready));
        }

        // Spec 003 AC-12.
        [Test]
        public void Fertility_FromCropsAndInputs()
        {
            Simulation sim = FarmTest.Farm();
            Tile wheat = sim.State.Land.Tiles[18], lentils = sim.State.Land.Tiles[19], compost = sim.State.Land.Tiles[26];
            Fixed w0 = wheat.Fertility, l0 = lentils.Fertility, c0 = compost.Fertility;
            Assert.That(sim.Execute(new Sow(0, 18, "wheat", SeedSource.Certified, "chemical")).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(0, 19, "lentils", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(0, 26, "wheat", SeedSource.Certified, "compost")).Accepted, Is.True);
            sim.AdvanceTo(8 * Hour);

            Assert.That(wheat.Fertility - w0, Is.EqualTo(Fixed.Parse("-0.4375")));
            Assert.That(wheat.FertilityChange(FertilityReason.CropUse), Is.EqualTo(Fixed.Parse("-0.25")));
            Assert.That(wheat.FertilityChange(FertilityReason.Fertiliser), Is.EqualTo(Fixed.Parse("-0.1875")));
            Fixed lentilShare = Fixed.FromRatio(8 * Hour, S);
            Assert.That(lentils.Fertility - l0, Is.EqualTo(Fixed.FromInt(4) * lentilShare));
            Assert.That(lentils.FertilityChange(FertilityReason.Lentils), Is.EqualTo(Fixed.FromInt(4) * lentilShare));
            Assert.That(compost.FertilityChange(FertilityReason.Compost), Is.EqualTo(Fixed.Parse("0.375")));
            Assert.That(compost.Fertility - c0, Is.EqualTo(Fixed.Parse("0.125"))); // −4 × 0.0625 + 6 × 0.0625: compost builds the soil (ADR 0051)
        }

        // Spec 003 AC-27 (ADR 0051): soil under a growing composted crop dries 20% more slowly; rain, chemical and bare soil are unchanged.
        [Test]
        public void Compost_HoldsWater()
        {
            Simulation sim = FarmTest.Farm();
            Tile compost = sim.State.Land.Tiles[26], chemical = sim.State.Land.Tiles[18];
            int sun = LandTest.Weather(sim, "sun"), rain = LandTest.Weather(sim, "rain");
            Fixed Change(Tile t, int weather)
            {
                t.Moisture = Fixed.FromInt(50);
                LandSystem.UpdateMoisture(sim, t, weather);
                return t.Moisture - Fixed.FromInt(50);
            }
            Fixed bareSun = Change(compost, sun), bareRain = Change(compost, rain), chemBare = Change(chemical, sun);
            Assert.That(sim.Execute(new Sow(0, 26, "wheat", SeedSource.Certified, "compost")).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(0, 18, "wheat", SeedSource.Certified, "chemical")).Accepted, Is.True);
            Assert.That(LandSystem.DryingFactor(sim, 26), Is.EqualTo(Fixed.Parse("0.8")));
            Assert.That(Math.Abs((Change(compost, sun) - bareSun * Fixed.Parse("0.8")).Raw), Is.LessThanOrEqualTo(8), "dries 20% more slowly");
            Assert.That(Change(compost, rain), Is.EqualTo(bareRain), "rain soaks in as before");
            Assert.That(Change(chemical, sun), Is.EqualTo(chemBare), "chemical fertiliser holds no water");
            sim.AdvanceTo(6 * Hour);
            Assert.That(LandSystem.DryingFactor(sim, 26), Is.EqualTo(Fixed.One), "only while the crop grows");
        }

        // Spec 003 AC-13.
        [Test]
        public void Chemical_FourSeasons()
        {
            Simulation sim = FarmTest.Farm();
            Tile tile = sim.State.Land.Tiles[18];
            Fixed fromFertiliser = Fixed.Zero;
            long t = Hour; // off the 6-hour grid, so no harvest falls on a season boundary
            for (int k = 0; k < 64; k++)
            {
                Assert.That(sim.Execute(new Sow(t, 18, "wheat", SeedSource.Certified, "chemical")).Accepted, Is.True, "cycle " + k);
                t += 6 * Hour;
                sim.AdvanceTo(t - 1);
                Fixed before = tile.FertilityChange(FertilityReason.Fertiliser);
                sim.AdvanceTo(t);
                fromFertiliser += tile.FertilityChange(FertilityReason.Fertiliser) - before;
                Assert.That(sim.Execute(new Harvest(t, 18, Overflow.SellRestToTrader)).Accepted, Is.True);
            }
            Assert.That(fromFertiliser, Is.EqualTo(Fixed.FromInt(-12)));
        }
    }

    public class YieldTests
    {
        // Spec 003 AC-5.
        [Test]
        public void Formula_AndCap()
        {
            Simulation sim = FarmTest.Farm();
            FarmDef farm = sim.Config.Game.Farm.Farm;
            YieldBreakdown b = FarmRules.Yield(farm, Fixed.FromInt(75), Fixed.Parse("0.875"), Fixed.One, Fixed.Parse("1.10"), Fixed.One,
                Fixed.Parse("0.98"), Fixed.One, Fixed.One);
            FarmTest.AssertNear(b.Uncapped, "70.74375");
            Assert.That(b.Grams, Is.EqualTo(70_744));
            Assert.That(b.Capped, Is.False);

            b = FarmRules.Yield(farm, Fixed.FromInt(75), Fixed.Parse("1.38"), Fixed.One, farm.CareCap, Fixed.One, Fixed.Parse("1.2"),
                Fixed.Parse("1.25"), Fixed.One);
            Assert.That(b.Capped, Is.True);
            Assert.That(b.Uncapped, Is.GreaterThan(farm.YieldCap * Fixed.FromInt(75)));
            Assert.That(b.Grams, Is.EqualTo((farm.YieldCap * Fixed.FromInt(75)).MulRatio(1000, 1).RoundToLong()));
            Assert.That(b.Grams, Is.EqualTo(120_000));
        }

        // Spec 003 AC-6.
        [Test]
        public void WaterFactor_Piecewise()
        {
            Simulation sim = FarmTest.Farm();
            FarmDefs f = sim.Config.Game.Farm;
            CropDef wheat = f.Crops[f.IndexOfCrop("wheat")];
            FarmTest.AssertNear(FarmRules.WaterFactor(f.Farm, wheat, Fixed.FromInt(20)), "0.742857142857", 8);
            Assert.That(FarmRules.WaterFactor(f.Farm, wheat, Fixed.FromInt(35)), Is.EqualTo(Fixed.One));
            Assert.That(FarmRules.WaterFactor(f.Farm, wheat, Fixed.FromInt(50)), Is.EqualTo(Fixed.One));
            Assert.That(FarmRules.WaterFactor(f.Farm, wheat, Fixed.FromInt(65)), Is.EqualTo(Fixed.One));
            Assert.That(FarmRules.WaterFactor(f.Farm, wheat, Fixed.Zero), Is.EqualTo(f.Farm.WaterDryFloor));
            Assert.That(FarmRules.WaterFactor(f.Farm, wheat, Fixed.FromInt(100)), Is.EqualTo(f.Farm.WaterWetFloor));
        }

        // Spec 003 AC-7.
        [Test]
        public void Weather_TimeWeighted()
        {
            Simulation sim = FarmTest.Farm();
            FarmTest.SetWeather(sim, 0, "sun");
            FarmTest.SetWeather(sim, 6, "heatwave"); // hourly spells: heatwave from 06:00
            sim.AdvanceTo(4 * Hour);
            Assert.That(sim.Execute(new Sow(4 * Hour, 18, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            sim.AdvanceTo(10 * Hour);
            YieldBreakdown b = FarmTest.Crop(sim, 18).Breakdown;
            FarmTest.AssertNear(b.Weather, "0.90", 16); // (2 × 1.00 + 4 × 0.85) ÷ 6
        }
    }

    public class CareTests
    {
        // Spec 003 AC-9.
        [Test]
        public void SharedPool_TimeWeighted()
        {
            Simulation sim = FarmTest.Farm();
            FarmTest.AssertNear(FounderRules.FieldHours(sim), "7.2", 8);
            Assert.That(sim.Execute(new Sow(0, 18, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(0, 19, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.State.Farm.CareRatio, Is.EqualTo(Fixed.Parse("1.1")), "7.2 ÷ 6 = 1.2, capped");
            Assert.That(sim.Execute(new Sow(0, 26, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            FarmTest.AssertNear(sim.State.Farm.CareRatio, "0.8", 8); // 7.2 ÷ 9

            // Watering demand on a well acre in a heatwave lowers the ratio.
            Simulation hot = FarmTest.Farm();
            FarmTest.SetWeather(hot, 0, "heatwave");
            hot.State.Land.Tiles[20].Moisture = Fixed.FromInt(30); // at the well floor
            Assert.That(hot.Execute(new Sow(0, 18, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(hot.Execute(new Sow(0, 20, "wheat", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(hot.State.Farm.CareRatio, Is.EqualTo(Fixed.Parse("1.1")));
            hot.AdvanceTo(Hour);
            Fixed lift = hot.State.Land.Tiles[20].WellLift;
            Assert.That(lift, Is.EqualTo(Fixed.Parse("1.2"))); // a heatwave dries loam 1.2 an hour
            Fixed demand = Fixed.FromInt(6) + lift.MulRatio(24, 1) * Fixed.Parse("0.1"); // 6 + 2.88
            Assert.That(FarmSystem.LabourDemand(hot), Is.EqualTo(demand));
            FarmTest.AssertNear(hot.State.Farm.CareRatio, "0.810810810", 16); // 7.2 ÷ 8.88

            // The crop's Care factor is the time-weighted mean across the changes.
            Crop c = FarmTest.Crop(hot, 18);
            Fixed watering = hot.State.Farm.CareRatio;
            hot.AdvanceTo(6 * Hour);
            Fixed expected = (Fixed.Parse("1.1").MulRatio(Hour, 1) + watering.MulRatio(5 * Hour, 1)).MulRatio(1, 6 * Hour);
            FarmTest.AssertNear(c.Breakdown.Care, expected.ToString(), 16);
        }
    }

    public partial class ContentTests
    {
        private static ContentException FarmError(string file, string from, string to)
        {
            return Assert.Throws<ContentException>(() => TestContent.Game((f, j) => f == file ? Replace(j, from, to) : j));
        }

        private static string Replace(string j, string from, string to)
        {
            Assert.That(j, Does.Contain(from), "test fixture text");
            return j.Replace(from, to);
        }

        // Spec 003 AC-1.
        [Test]
        public void Crops_Validation()
        {
            Assert.That(FarmError("crops.json", "\"outputGood\": \"radish\"", "\"outputGood\": \"turnip\"").FieldPath, Is.EqualTo("crops.crops[0].outputGood"));
            Assert.That(FarmError("crops.json", "\"sowSeasons\": [\"winter\"]", "\"sowSeasons\": [\"autumn\"]").FieldPath, Is.EqualTo("crops.crops[1].sowSeasons[0]"));
            Assert.That(FarmError("crops.json", "\"moistureMin\": 35, \"moistureMax\": 65", "\"moistureMin\": 65, \"moistureMax\": 65").FieldPath,
                Is.EqualTo("crops.crops[1].moistureMin"));
            Assert.That(FarmError("founder.json", "\"id\": \"subsistence\"", "\"id\": \"starving\"").Message, Does.Contain("needs 'subsistence'"));
            Assert.That(FarmError("founder.json", "\"nutrition\": 1.05", "\"nutrition\": 1.10").FieldPath, Is.EqualTo("founder.dietPlans[3].nutrition"));
            Assert.That(FarmError("farming.json", "\"storm\": 0.85 }", "\"stormy\": 0.85 }").FieldPath, Is.EqualTo("farming.farm.weatherYieldFactor.stormy"));
            Assert.That(FarmError("farming.json", ", \"storm\": 0.85 }", " }").Message, Does.Contain("missing weather id 'storm'"));
            Assert.That(FarmError("crops.json", "\"seedGood\": \"seed_wheat\"", "\"seedGood\": \"wheat\"").Message, Does.Contain("must be an input good"));
            Assert.That(FarmError("start.json", "\"wheat\": 2.0", "\"radish\": 2.0").Message, Does.Contain("can't be saved"));
        }
    }
}
