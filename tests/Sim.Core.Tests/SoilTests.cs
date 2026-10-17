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

        /// <summary>The first tile of the given soil and drainage on the default map that isn't on the well's parcel.</summary>
        public static Tile TileOf(Simulation sim, string soil, string drainage)
        {
            GameDefs d = sim.Config.Game;
            return sim.State.Land.Tiles.First(t => d.Soils[t.Soil].Id == soil && d.Drainages[t.Drainage].Id == drainage
                && t.Terrain == Terrain.Farm && t.ParcelId != d.Start.InheritedParcel);
        }

        /// <summary>Weather content where every season has only the given weathers (equal weights).</summary>
        public static GameDefs GameWithWeather(out WeatherSet weather, params string[] only)
        {
            string json = TestContent.ReadContent("weather.json");
            foreach (string id in new[] { "sun", "cloudy", "rain", "heavy_rain", "heatwave", "frost", "storm" })
            {
                string weight = Array.IndexOf(only, id) >= 0 ? "10" : "0";
                json = System.Text.RegularExpressions.Regex.Replace(json, "\"" + id + "\": \\d+", "\"" + id + "\": " + weight);
            }
            weather = Sim.Content.ContentLoader.LoadWeather(json);
            WeatherSet w = weather;
            return Sim.Content.ContentLoader.LoadGame(TestContent.ReadContent("land.json"), TestContent.ReadContent("map.json"),
                TestContent.ReadContent("buildings.json"), TestContent.ReadContent("start.json"),
                TestContent.ReadContent("finance.json"), w);
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
            Assert.That(tile.Moisture, Is.EqualTo(LandTest.F("51.2")));

            tile.Moisture = Fixed.FromInt(80);
            LandSystem.UpdateMoisture(sim, tile, LandTest.Weather(sim, "sun"));
            // 79.14 to content precision; 0.4 and 0.1 aren't exact in binary, so Q31.32 lands 1 ulp below
            // Fixed.Parse("79.14"). The raw value is pinned for determinism (spec 002 §10, implementation note).
            Assert.That(Math.Abs(tile.Moisture.Raw - LandTest.F("79.14").Raw), Is.LessThanOrEqualTo(1));
            Assert.That(tile.Moisture.Raw, Is.EqualTo(339_903_711_804L));
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

        // Spec 002 AC-6.
        [Test]
        public void WellFloor_InheritedOnly()
        {
            Simulation sim = LandTest.NewFarmWithWeather("sun", "heatwave");
            // US-040 adds: buy w1 first (spec 002 AC-6 says "a bought tile").
            int home = sim.Config.Game.Start.InheritedParcel;
            Tile[] inherited = sim.State.Land.Tiles.Where(t => t.ParcelId == home && t.Terrain == Terrain.Farm).ToArray();
            Tile bought = sim.State.Land.Tiles.First(t => sim.Config.Game.Map.Parcels[t.ParcelId].Id == "w1");
            Assert.That(sim.Config.Game.Soils[bought.Soil].Id, Is.EqualTo("loam"));

            Fixed floor = sim.Config.Game.Land.WellMoistureFloor;
            Fixed lowestBought = bought.Moisture;
            long end = sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason;
            for (long t = Hour; t <= end; t += Hour)
            {
                sim.AdvanceTo(t);
                foreach (Tile tile in inherited) Assert.That(tile.Moisture, Is.GreaterThanOrEqualTo(floor));
                if (bought.Moisture < lowestBought) lowestBought = bought.Moisture;
            }
            Assert.That(lowestBought, Is.LessThan(floor));
        }
    }

    public class FertilityTests
    {
        // Spec 002 AC-7.
        [Test]
        public void Fallow_RecoversProRata()
        {
            Simulation sim = NewFarm();
            Tile fallow = LandTest.TileOf(sim, "loam", "moderate");
            Tile busy = sim.State.Land.Tiles.First(t => t.Id != fallow.Id && sim.Config.Game.Soils[t.Soil].Id == "loam" && t.Terrain == Terrain.Farm && t.ParcelId != 0);
            Tile capped = sim.State.Land.Tiles.First(t => t.Id != fallow.Id && t.Id != busy.Id && sim.Config.Game.Soils[t.Soil].Id == "loam" && t.Terrain == Terrain.Farm);
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

    public class SoilFactorTests
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

    /// <summary>Occupies and vacates a few tiles on a fixed rhythm, like crops being sown and harvested.</summary>
    public sealed class FallowScheduleSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Production; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass != ItemClass.Hourly) return;
            long hour = sim.State.Clock.SimTime / 3600;
            foreach (int tile in new[] { 2, 8, 14, 21 })
                if ((hour + tile) % 37 == 0) LandSystem.SetOccupied(sim, tile, !sim.State.Land.Tiles[tile].Occupied);
        }
    }

}
