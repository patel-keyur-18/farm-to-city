using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Scheduling;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Helpers for the land tests.</summary>
    public static class LandTest
    {
        public static Fixed F(string s) { return Fixed.Parse(s); }

        public static int Weather(Simulation sim, string id) { return sim.Weather.IndexOf(id); }

        /// <summary>The first unowned farm tile of the given soil and drainage on the default map.</summary>
        public static Tile TileOf(Simulation sim, string soil, string drainage)
        {
            GameDefs d = sim.Config.Game;
            return sim.State.Land.Tiles.First(t => d.Soils[t.Soil].Id == soil && d.Drainages[t.Drainage].Id == drainage
                && t.Terrain == Terrain.Farm && !sim.State.Land.Parcels[t.ParcelId].Owned);
        }

        /// <summary>Weather content where every season has only the given weathers (equal weights).</summary>
        public static GameDefs GameWithWeather(out WeatherSet weather, params string[] only)
        {
            string json = TestContent.ReadContent("weather.json");
            string[] ids = { "sun", "cloudy", "rain", "heavy_rain", "heatwave", "frost", "storm" };
            string weights = string.Join(", ", ids.Select(id => "\"" + id + "\": " + (Array.IndexOf(only, id) >= 0 ? "10" : "0")));
            string persistence = string.Join(", ", ids.Select(id => "\"" + id + "\": 0.5"));
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"weights\": \\{[^}]*\\}", "\"weights\": { " + weights + " }");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"persistence\": \\{[^}]*\\}", "\"persistence\": { " + persistence + " }");
            weather = Sim.Content.ContentLoader.LoadWeather(json);
            WeatherSet w = weather;
            return TestContent.Game((f, j) => f == "start.json" ? j.Replace("20000", "5000000") : j, w);
        }

        public static Simulation NewFarmWithWeather(params string[] only)
        {
            GameDefs game = GameWithWeather(out WeatherSet weather, only);
            var config = new SimConfig(TestContent.Clock(), weather, game: game);
            return Simulation.NewGame(config, new Commands.NewGame(5, Wall0, 0));
        }
    }

    public class MoistureTests
    {
        // Spec 002 AC-3.
        [Test]
        public void HourlyUpdate_Exact()
        {
            Simulation sim = NewFarm();
            Tile tile = LandTest.TileOf(sim, "loam", "moderate");

            tile.Moisture = Fixed.FromInt(50);
            LandSystem.UpdateMoisture(sim, tile, LandTest.Weather(sim, "rain"));
            Assert.That(tile.Moisture, Is.EqualTo(LandTest.F("53.0")));

            tile.Moisture = Fixed.FromInt(80);
            LandSystem.UpdateMoisture(sim, tile, LandTest.Weather(sim, "sun"));
            // 80 − 0.9 = 79.1, then 79.1 − 9.1 × 0.15 = 77.735 to content precision; 0.9 and 0.15 aren't exact in binary, so
            // Q31.32 lands within a few ulps of Fixed.Parse("77.735"). The raw value is pinned for determinism (spec 002 §10, note 1).
            Assert.That(Math.Abs(tile.Moisture.Raw - LandTest.F("77.735").Raw), Is.LessThanOrEqualTo(8));
            Assert.That(tile.Moisture.Raw, Is.EqualTo(333_869_282_759L));
        }

        // Spec 002 AC-17 (ADR 0085): below dryingSlowsBelow (50) drying is × m ÷ 50; rain is unchanged; compost still multiplies.
        [Test]
        public void DrySoil_DriesSlower()
        {
            Simulation sim = NewFarm();
            Tile tile = LandTest.TileOf(sim, "loam", "moderate");
            int sun = LandTest.Weather(sim, "sun");

            tile.Moisture = Fixed.FromInt(50); // at the level: the full 0.9
            LandSystem.UpdateMoisture(sim, tile, sun);
            FarmTest.AssertNear(tile.Moisture, "49.1", 8);

            tile.Moisture = Fixed.FromInt(25); // half as fast: 25 − 0.9 × 25 ÷ 50
            LandSystem.UpdateMoisture(sim, tile, sun);
            FarmTest.AssertNear(tile.Moisture, "24.55", 8);

            tile.Moisture = Fixed.FromInt(10); // a fifth: 10 − 0.18
            LandSystem.UpdateMoisture(sim, tile, sun);
            FarmTest.AssertNear(tile.Moisture, "9.82", 8);

            tile.Moisture = Fixed.Zero; // bone dry stays at 0
            LandSystem.UpdateMoisture(sim, tile, sun);
            Assert.That(tile.Moisture, Is.EqualTo(Fixed.Zero));

            tile.Moisture = Fixed.FromInt(10); // rain soaks in as before
            LandSystem.UpdateMoisture(sim, tile, LandTest.Weather(sim, "rain"));
            Assert.That(tile.Moisture, Is.EqualTo(Fixed.FromInt(13)));

            // Sandy at 20 in a heatwave: 1.6 × 1.5 × 20 ÷ 50 = 0.96.
            Tile sandy = LandTest.TileOf(sim, "sandy", "good");
            sandy.Moisture = Fixed.FromInt(20);
            LandSystem.UpdateMoisture(sim, sandy, LandTest.Weather(sim, "heatwave"));
            FarmTest.AssertNear(sandy.Moisture, "19.04", 8);

            // Off (0, as in content before ADR 0085): the full drying at any moisture.
            GameDefs off = TestContent.Game((f, j) => f == "land.json" ? j.Replace("\"dryingSlowsBelow\": 50,", "") : j);
            Simulation old = NewFarm(game: off);
            Tile t2 = LandTest.TileOf(old, "loam", "moderate");
            t2.Moisture = Fixed.FromInt(25);
            LandSystem.UpdateMoisture(old, t2, LandTest.Weather(old, "sun"));
            FarmTest.AssertNear(t2.Moisture, "24.1", 8);
        }

        // Spec 002 §9 moisture hook (US-014.T6): an unwatered loam acre's mean moisture by season, over 100 years after
        // a warm-up year, is inside the realistic Indian ranges. Deterministic (fixed seed), so the check isn't flaky.
        [Test]
        public void SeasonalMeans_Realistic()
        {
            Simulation sim = NewFarm(seed: 1);
            var sum = new double[4];
            var n = new long[4];
            long year = sim.Clock.SecondsPerYear;
            for (long t = Hour; t <= 101 * year; t += Hour)
            {
                sim.AdvanceTo(t);
                if (t <= year) continue;
                int season = (int)Time.Calendar.SeasonAt(t - 1, sim.Clock);
                foreach (Tile tile in sim.State.Land.Tiles)
                {
                    if (tile.Terrain != Terrain.Farm || sim.State.Land.Parcels[tile.ParcelId].Owned) continue;
                    if (sim.Config.Game.Soils[tile.Soil].Id != "loam") continue;
                    sum[season] += tile.Moisture.Raw / 4294967296.0;
                    n[season]++;
                }
            }
            double[] mean = sum.Select((x, i) => x / n[i]).ToArray();
            TestContext.Out.WriteLine("Loam means (Spring, Summer, Monsoon, Winter): " + string.Join(", ", mean.Select(m => m.ToString("0.0"))));
            Assert.That(mean[0], Is.InRange(30, 50), "Spring");
            Assert.That(mean[1], Is.InRange(15, 35), "Summer");
            Assert.That(mean[2], Is.InRange(65, 85), "Monsoon");
            Assert.That(mean[3], Is.InRange(40, 60), "Winter");
            Assert.That(mean[2] > mean[3] && mean[3] > mean[0] && mean[0] > mean[1], Is.True, "Monsoon > Winter > Spring > Summer");
        }

        // Spec 002 §9 dry-season hook (ADR 0085, AC-18): on an unwatered loam acre, okra's water factor averaged over each Spring and
        // each Summer, over 100 years after a warm-up year (fixed seed). Measured: Spring mean 0.84 and worst season in 10 0.63
        // (0.80 and 0.51 before ADR 0085), Summer 0.81 and 0.59 (0.75 and 0.46). The thresholds sit between, so the check fails if
        // the rule is off. Monsoon (okra) and Winter (wheat) don't fall: 0.82 and 0.95, as before.
        [Test]
        public void DrySeasons_WorstSeason()
        {
            Simulation sim = NewFarm(seed: 1);
            GameDefs d = sim.Config.Game;
            CropDef okra = d.Farm.Crops.First(c => c.Id == "okra"), wheat = d.Farm.Crops.First(c => c.Id == "wheat");
            Tile tile = sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && !sim.State.Land.Parcels[t.ParcelId].Owned
                && d.Soils[t.Soil].Id == "loam" && d.Drainages[t.Drainage].Id == "moderate");
            long year = sim.Clock.SecondsPerYear;
            var seasons = Enumerable.Range(0, 4).Select(_ => new List<double>()).ToArray();
            double sum = 0; int n = 0, current = -1;
            for (long t = Hour; t <= 101 * year; t += Hour)
            {
                sim.AdvanceTo(t);
                if (t <= year) continue;
                int season = (int)Time.Calendar.SeasonAt(t - 1, sim.Clock);
                if (season != current && n > 0) { seasons[current].Add(sum / n); sum = 0; n = 0; }
                current = season;
                sum += Farm.FarmRules.WaterFactor(d.Farm.Farm, season == 3 ? wheat : okra, tile.Moisture).Raw / 4294967296.0;
                n++;
            }
            double Worst(int s) { return seasons[s].OrderBy(x => x).ElementAt(seasons[s].Count / 10); }
            string[] names = { "Spring", "Summer", "Monsoon", "Winter" };
            for (int s = 0; s < 4; s++)
                TestContext.Out.WriteLine(names[s] + ": mean " + seasons[s].Average().ToString("0.00") + ", worst in 10 " + Worst(s).ToString("0.00"));
            Assert.That(Worst(0), Is.GreaterThanOrEqualTo(0.60), "Spring worst season in 10");
            Assert.That(seasons[0].Average(), Is.GreaterThanOrEqualTo(0.82), "Spring mean");
            Assert.That(Worst(1), Is.GreaterThanOrEqualTo(0.55), "Summer worst season in 10");
            Assert.That(seasons[1].Average(), Is.GreaterThanOrEqualTo(0.78), "Summer mean");
            Assert.That(seasons[2].Average(), Is.GreaterThanOrEqualTo(0.80), "Monsoon");
            Assert.That(seasons[3].Average(), Is.GreaterThanOrEqualTo(0.93), "Winter");
        }

        // Spec 002 AC-4.
        [Test]
        public void Drainage_Order()
        {
            Simulation sim = NewFarm();
            Tile[] tiles = { LandTest.TileOf(sim, "clay", "poor"), LandTest.TileOf(sim, "clay", "moderate"), LandTest.TileOf(sim, "loam", "good") };
            // Same soil for a fair comparison: give all three loam.
            int loam = sim.Config.Game.Soils.ToList().FindIndex(s => s.Id == "loam");
            foreach (Tile t in tiles) { t.Soil = loam; t.Moisture = Fixed.FromInt(95); }
            for (int h = 0; h < 24; h++)
                foreach (Tile t in tiles) LandSystem.UpdateMoisture(sim, t, LandTest.Weather(sim, "sun"));
            Assert.That(tiles[0].Moisture, Is.GreaterThan(tiles[1].Moisture));
            Assert.That(tiles[1].Moisture, Is.GreaterThan(tiles[2].Moisture));
        }

        // Spec 002 AC-5.
        [Test]
        public void StaysInBounds()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                Simulation sim = NewFarm(seed: seed);
                long end = sim.State.Clock.SimTime + 3 * sim.Clock.SecondsPerYear;
                for (long t = sim.State.Clock.SimTime + Hour; t <= end; t += Hour)
                {
                    sim.AdvanceTo(t);
                    foreach (Tile tile in sim.State.Land.Tiles)
                    {
                        Assert.That(tile.Moisture, Is.InRange(Fixed.Zero, Fixed.FromInt(100)), "moisture, seed " + seed);
                        Assert.That(tile.Fertility, Is.InRange(Fixed.Zero, Fixed.FromInt(100)), "fertility, seed " + seed);
                    }
                }
            }
        }

        // Spec 002 AC-6 (the well acres only, ADR 0032).
        [Test]
        public void WellFloor_WellAcresOnly()
        {
            Simulation sim = LandTest.NewFarmWithWeather("sun", "heatwave");
            Assert.That(sim.Execute(new Commands.BuyParcel(sim.State.Clock.SimTime, "w1")).Accepted, Is.True);
            Tile[] well = sim.Config.Game.Land.WellTiles.Select(t => sim.State.Land.Tiles[t]).ToArray();
            Assert.That(well.Select(t => t.Id), Is.EqualTo(new[] { 86, 102 }));
            Tile rainFed = sim.State.Land.Tiles[85]; // inherited, loam, not a well acre
            Tile bought = sim.State.Land.Tiles.First(t => sim.Config.Game.Map.Parcels[t.ParcelId].Id == "w1");
            Assert.That(sim.Config.Game.Soils[bought.Soil].Id, Is.EqualTo("loam"));
            Assert.That(sim.Config.Game.Soils[rainFed.Soil].Id, Is.EqualTo("loam"));

            Fixed floor = sim.Config.Game.Land.WellMoistureFloor;
            Fixed lowestBought = bought.Moisture, lowestRainFed = rainFed.Moisture;
            bool lifted = false;
            long end = sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason;
            for (long t = Hour; t <= end; t += Hour)
            {
                sim.AdvanceTo(t);
                foreach (Tile tile in well)
                {
                    Assert.That(tile.Moisture, Is.GreaterThanOrEqualTo(floor));
                    lifted |= tile.WellLift > Fixed.Zero;
                }
                Assert.That(rainFed.WellLift, Is.EqualTo(Fixed.Zero));
                if (bought.Moisture < lowestBought) lowestBought = bought.Moisture;
                if (rainFed.Moisture < lowestRainFed) lowestRainFed = rainFed.Moisture;
            }
            Assert.That(lowestBought, Is.LessThan(floor));
            Assert.That(lowestRainFed, Is.LessThan(floor));
            Assert.That(lifted, Is.True, "the well lifted moisture (bucket labour, spec 003)");

            var e = Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) =>
                f == "land.json" ? j.Replace("\"irrigatedTiles\": [86, 102]", "\"irrigatedTiles\": [86, 103]") : j));
            Assert.That(e.FieldPath, Is.EqualTo("land.well.irrigatedTiles[1]"));
            e = Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) =>
                f == "land.json" ? j.Replace("\"irrigatedTiles\": [86, 102]", "\"irrigatedTiles\": [87]") : j));
            Assert.That(e.Message, Does.Contain("not an inherited farm tile")); // the yard
        }
    }

    public class FertilityTests
    {
        // Spec 002 AC-7.
        [Test]
        public void Fallow_RecoversProRata()
        {
            Simulation sim = NewFarm();
            Tile fallow = sim.State.Land.Tiles[85], busy = sim.State.Land.Tiles[86], capped = sim.State.Land.Tiles[100]; // inherited loam
            Tile unowned = LandTest.TileOf(sim, "loam", "moderate");
            Fixed unownedStart = unowned.Fertility;
            fallow.Fertility = Fixed.FromInt(45);
            busy.Fertility = Fixed.FromInt(45);
            capped.Fertility = Fixed.FromInt(75);

            // busy: occupied for an hour in the middle of every day.
            for (int d = 0; d < 4; d++)
            {
                sim.AdvanceTo(d * Day + 10 * Hour);
                LandSystem.SetOccupied(sim, busy.Id, true);
                sim.AdvanceTo(d * Day + 11 * Hour);
                LandSystem.SetOccupied(sim, busy.Id, false);
            }
            sim.AdvanceTo(4 * Day);

            Assert.That(fallow.Fertility, Is.EqualTo(Fixed.FromInt(51)));
            Assert.That(busy.Fertility, Is.EqualTo(Fixed.FromInt(45)));
            Assert.That(capped.Fertility, Is.EqualTo(Fixed.FromInt(75)));
            Assert.That(unowned.Fertility, Is.EqualTo(unownedStart), "neighbours farm unowned land: no fallow recovery (owner decision 2026-10-17)");
        }

        // Spec 002 AC-8.
        [Test]
        public void Adjust_ClampsAndRecordsReasons()
        {
            Simulation sim = NewFarm(localSeconds: 1 * Hour);
            Tile tile = LandTest.TileOf(sim, "loam", "moderate");
            tile.Fertility = Fixed.FromInt(95);
            LandSystem.SetOccupied(sim, tile.Id, true); // no fallow gains to muddle the totals

            LandSystem.AdjustFertility(sim, tile.Id, Fixed.FromInt(10), FertilityReason.Compost);
            Assert.That(tile.Fertility, Is.EqualTo(Fixed.FromInt(100)));
            Assert.That(tile.FertilityChange(FertilityReason.Compost), Is.EqualTo(Fixed.FromInt(5)));

            LandSystem.AdjustFertility(sim, tile.Id, LandTest.F("-3.5"), FertilityReason.CropUse);
            LandSystem.AdjustFertility(sim, tile.Id, Fixed.FromInt(-200), FertilityReason.Fertiliser);
            Assert.That(tile.Fertility, Is.EqualTo(Fixed.Zero));
            Assert.That(tile.FertilityChange(FertilityReason.CropUse), Is.EqualTo(LandTest.F("-3.5")));
            Assert.That(tile.FertilityChange(FertilityReason.Fertiliser), Is.EqualTo(LandTest.F("-96.5")));

            sim.AdvanceTo(sim.Clock.SecondsPerSeason);
            for (int r = 0; r < Tile.ReasonCount; r++)
                Assert.That(tile.FertilityChange((FertilityReason)r), Is.EqualTo(Fixed.Zero));
            Assert.That(tile.Fertility, Is.EqualTo(Fixed.Zero));
        }
    }

    public partial class SoilFactorTests
    {
        // Spec 002 AC-9.
        [Test]
        public void PiecewiseLinear()
        {
            Simulation sim = NewFarm();
            Tile loam = LandTest.TileOf(sim, "loam", "moderate");
            var expected = new Dictionary<int, string> { { 30, "0.75" }, { 45, "0.875" }, { 60, "1.00" }, { 80, "1.075" } };
            foreach (int f in new[] { 30, 45, 60, 80 })
            {
                loam.Fertility = Fixed.FromInt(f);
                Assert.That(LandRules.SoilFactor(sim, loam), Is.EqualTo(LandTest.F(expected[f])), "fertility " + f);
            }
            Tile sandy = LandTest.TileOf(sim, "sandy", "good");
            sandy.Fertility = Fixed.FromInt(60);
            Assert.That(LandRules.SoilFactor(sim, sandy), Is.EqualTo(LandTest.F("0.85")));
        }
    }

    public partial class SoilFactorTests
    {
        // Spec 002 AC-10.
        [Test]
        public void Shade_MatureTreesOnly()
        {
            Simulation sim = NewFarm();
            Tile tile = sim.State.Land.Tiles[84]; // inherited loam acre
            tile.Fertility = Fixed.FromInt(60);   // fertility factor 1.00, so the Soil factor is the shade factor
            TreeSlot[] slots = sim.State.Land.Slots.Where(x => x.TileId == 84 && x.Kind == SlotKind.FieldEdge).ToArray();
            Assert.That(slots.Length, Is.EqualTo(2));

            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(Fixed.One));
            LandSystem.SetTree(sim, slots[0].Id, 1);
            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(Fixed.One), "a sapling casts no shade");
            LandSystem.SetTreeMature(sim, slots[0].Id, LandTest.F("0.03")); // a small tree (banana, papaya)
            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(LandTest.F("0.97")));
            LandSystem.SetTree(sim, slots[1].Id, 2);
            LandSystem.SetTreeMature(sim, slots[1].Id, LandTest.F("0.08")); // a large tree (mango, guava), ADR 0037
            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(LandTest.F("0.89")));
            Assert.That(LandRules.SoilFactor(sim, tile), Is.EqualTo(LandTest.F("0.89")));
            Assert.That(LandRules.ShadeFactor(sim, sim.State.Land.Tiles[85]), Is.EqualTo(Fixed.One), "shade stays on its own tile");

            // Removing a tree empties the slot and its shade.
            LandSystem.SetTree(sim, slots[1].Id, 0);
            Assert.That(slots[1].TreeMature, Is.False);
            Assert.That(LandRules.ShadeFactor(sim, tile), Is.EqualTo(LandTest.F("0.97")));

            // The floor: 4 slots per acre and minShadeFactor 0.90 in test content (4 × 0.03 would give 0.88).
            Simulation four = NewFarm(game: TestContent.Game((f, j) => f != "land.json" ? j
                : j.Replace("\"fieldEdgeSlotsPerAcre\": 2", "\"fieldEdgeSlotsPerAcre\": 4").Replace("\"minShadeFactor\": 0.80", "\"minShadeFactor\": 0.90")));
            foreach (TreeSlot slot in four.State.Land.Slots.Where(x => x.TileId == 84 && x.Kind == SlotKind.FieldEdge))
            {
                LandSystem.SetTree(four, slot.Id, slot.Id);
                LandSystem.SetTreeMature(four, slot.Id, LandTest.F("0.03"));
            }
            Assert.That(LandRules.ShadeFactor(four, four.State.Land.Tiles[84]), Is.EqualTo(LandTest.F("0.90")), "the floor, not 0.88");
        }
    }

    public class TreeSlotTests
    {
        [Test]
        public void SlotIds_AreNeverReused()
        {
            Simulation sim = NewFarm(game: ParcelTests.Rich());
            sim.Execute(new Commands.BuyParcel(0, "w1"));
            long[] first = sim.State.Land.Slots.Where(x => x.TileId == 82 || x.TileId == 83).Select(x => x.Id).ToArray();
            sim.Execute(new Commands.SellParcel(0, "w1"));
            sim.Execute(new Commands.BuyParcel(0, "w1"));
            long[] second = sim.State.Land.Slots.Where(x => x.TileId == 82 || x.TileId == 83).Select(x => x.Id).ToArray();
            Assert.That(second.Length, Is.EqualTo(4));
            Assert.That(second.Min(), Is.GreaterThan(first.Max()));
            Assert.That(sim.State.Land.Slots.Select(x => x.Id), Is.Ordered.And.Unique);
        }
    }

    /// <summary>Occupies and vacates a few tiles on a fixed rhythm, like crops being sown and harvested.</summary>
    public sealed class FallowScheduleSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Production; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass != ItemClass.Hourly) return;
            long hour = sim.State.Clock.SimTime / 3600;
            foreach (int tile in new[] { 84, 85, 100, 102 })
                if ((hour + tile) % 37 == 0) LandSystem.SetOccupied(sim, tile, !sim.State.Land.Tiles[tile].Occupied);
        }
    }

}
