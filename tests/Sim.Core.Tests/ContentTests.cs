using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Tests
{
    public partial class ContentTests
    {
        private static string ClockJson(string field, string value)
        {
            var fields = new[]
            {
                ("daysPerSeason", "4"), ("weatherSpellHours", "6"), ("lifeYearsPerSeason", "1.0"),
                ("awayCapHours", "72"), ("awayThresholdSeconds", "60"), ("forecastDays", "3"),
                ("forecastAccuracy", "[1.0, 0.80, 0.65]"),
            };
            return "{" + string.Join(",", fields.Select(f => "\"" + f.Item1 + "\": " + (f.Item1 == field ? value : f.Item2))) + "}";
        }

        private static ContentException ClockError(string field, string value)
        {
            return Assert.Throws<ContentException>(() => ContentLoader.LoadClock(ClockJson(field, value)));
        }

        // Spec 001 AC-29.
        [Test]
        public void ClockDef_Validation()
        {
            ContentException e = ClockError("weatherSpellHours", "5");
            Assert.That(e.FieldPath, Is.EqualTo("clock.weatherSpellHours"));
            Assert.That(e.Message, Does.Contain("weatherSpellHours").And.Contain("divide 24"));

            e = ClockError("forecastAccuracy", "[1.0, 0.65, 0.80]");
            Assert.That(e.FieldPath, Is.EqualTo("clock.forecastAccuracy[2]"));
            Assert.That(e.Message, Does.Contain("forecastAccuracy").And.Contain("must not increase"));
        }

        [Test]
        public void ClockDef_BoundsAndShape()
        {
            Assert.That(ClockError("daysPerSeason", "0").FieldPath, Is.EqualTo("clock.daysPerSeason"));
            Assert.That(ClockError("daysPerSeason", "16").FieldPath, Is.EqualTo("clock.daysPerSeason"));
            Assert.That(ClockError("daysPerSeason", "4.5").FieldPath, Is.EqualTo("clock.daysPerSeason"));
            Assert.That(ClockError("lifeYearsPerSeason", "0.2").FieldPath, Is.EqualTo("clock.lifeYearsPerSeason"));
            Assert.That(ClockError("awayCapHours", "200").FieldPath, Is.EqualTo("clock.awayCapHours"));
            Assert.That(ClockError("awayThresholdSeconds", "4").FieldPath, Is.EqualTo("clock.awayThresholdSeconds"));
            Assert.That(ClockError("forecastDays", "4").FieldPath, Is.EqualTo("clock.forecastDays"));
            Assert.That(ClockError("forecastAccuracy", "[1.0, 0.8]").FieldPath, Is.EqualTo("clock.forecastAccuracy"));
            Assert.That(ClockError("forecastAccuracy", "[1.0, 1.2, 0.5]").FieldPath, Is.EqualTo("clock.forecastAccuracy[1]"));
            Assert.That(ClockError("forecastAccuracy", "[1.0, 0.8, 0.6500000001]").Message, Does.Contain("decimal places"));

            var unknown = Assert.Throws<ContentException>(() => ContentLoader.LoadClock("{\"daysPerSeasn\": 4}"));
            Assert.That(unknown.FieldPath, Is.EqualTo("clock.daysPerSeasn"));
            var missing = Assert.Throws<ContentException>(() => ContentLoader.LoadClock("{}"));
            Assert.That(missing.FieldPath, Is.EqualTo("clock.daysPerSeason"));
            Assert.Throws<ContentException>(() => ContentLoader.LoadClock("{ not json"));
        }

        [Test]
        public void ClockJson_LoadsSpecDefaults()
        {
            ClockDef c = TestContent.Clock();
            ClockDef d = ClockDef.Default();
            Assert.That(c.DaysPerSeason, Is.EqualTo(4).And.EqualTo(d.DaysPerSeason));
            Assert.That(c.WeatherSpellHours, Is.EqualTo(1));
            Assert.That(c.LifeYearsPerSeason, Is.EqualTo(Fixed.One));
            Assert.That(c.AwayCapHours, Is.EqualTo(72));
            Assert.That(c.AwayThresholdSeconds, Is.EqualTo(60));
            Assert.That(c.ForecastDays, Is.EqualTo(3));
            Assert.That(c.ForecastAccuracy, Is.EqualTo(d.ForecastAccuracy));
            Assert.That(c.ForecastSpells, Is.EqualTo(72));
        }

        // Spec 001 AC-2, loader path: JSON 0.65 → decimal → Fixed, never double.
        [Test]
        public void JsonDecimal_IsExact()
        {
            ClockDef c = ContentLoader.LoadClock(ClockJson("forecastAccuracy", "[1.0, 0.80, 0.65]"));
            Assert.That(c.ForecastAccuracy[2], Is.EqualTo(Fixed.FromRatio(65, 100)));
            Assert.That(c.ForecastAccuracy[1], Is.EqualTo(Fixed.FromRatio(80, 100)));
        }

        [Test]
        public void WeatherJson_MatchesSpecTables()
        {
            WeatherSet w = TestContent.Weather();
            Assert.That(w.Types.Select(t => t.Id),
                Is.EqualTo(new[] { "sun", "cloudy", "rain", "heavy_rain", "heatwave", "frost", "storm" }));

            // Spec 001 §4, starting climate tables: Sun, Cloudy, Rain, Heavy rain, Heatwave, Frost, Storm.
            Assert.That(w.ClimateFor(Season.Spring).Weights, Is.EqualTo(new[] { 47, 28, 15, 2, 5, 3, 0 }));
            Assert.That(w.ClimateFor(Season.Summer).Weights, Is.EqualTo(new[] { 53, 10, 5, 0, 25, 0, 7 }));
            Assert.That(w.ClimateFor(Season.Monsoon).Weights, Is.EqualTo(new[] { 10, 25, 35, 20, 0, 0, 10 }));
            Assert.That(w.ClimateFor(Season.Winter).Weights, Is.EqualTo(new[] { 58, 24, 4, 0, 0, 14, 0 }));

            // Spec 001 §4, persistence per weather (ADR 0043); a weather with share 0 has none (0).
            Fixed[] P(params string[] v) { return v.Select(Fixed.Parse).ToArray(); }
            Assert.That(w.ClimateFor(Season.Spring).Persistence, Is.EqualTo(P("0.96", "0.85", "0.77", "0.24", "0.82", "0.64", "0")));
            Assert.That(w.ClimateFor(Season.Summer).Persistence, Is.EqualTo(P("0.97", "0.81", "0.73", "0", "0.83", "0", "0")));
            Assert.That(w.ClimateFor(Season.Monsoon).Persistence, Is.EqualTo(P("0.82", "0.86", "0.94", "0", "0", "0", "0.12")));
            Assert.That(w.ClimateFor(Season.Winter).Persistence, Is.EqualTo(P("0.97", "0.90", "0.83", "0", "0", "0", "0")));

            // Spec 001 AC-16: the derived draw weights, shareᵢ × round(1000 × (1 − pᵢ)).
            Assert.That(w.ClimateFor(Season.Spring).DrawWeights, Is.EqualTo(new[] { 1880, 4200, 3450, 1520, 900, 1080, 0 }));
            Assert.That(w.ClimateFor(Season.Summer).DrawWeights, Is.EqualTo(new[] { 1590, 1900, 1350, 0, 4250, 0, 7000 }));
            Assert.That(w.ClimateFor(Season.Monsoon).DrawWeights, Is.EqualTo(new[] { 1800, 3500, 2100, 20000, 0, 0, 8800 }));
            Assert.That(w.ClimateFor(Season.Winter).DrawWeights, Is.EqualTo(new[] { 1740, 2400, 680, 0, 0, 14000, 0 }));
        }

        [Test]
        public void WeatherJson_Validation()
        {
            string types = "\"weatherTypes\": [{\"id\":\"sun\",\"nameKey\":\"k\",\"isSevere\":false},{\"id\":\"rain\",\"nameKey\":\"k\",\"isSevere\":false}]";
            string Climate(string season, string weights, string p = "\"sun\":0.5")
            {
                return "{\"season\":\"" + season + "\",\"persistence\":{" + p + "},\"weights\":{" + weights + "}}";
            }
            string Json(params string[] climates) { return "{" + types + ",\"climates\":[" + string.Join(",", climates) + "]}"; }
            string ok = "\"sun\":1";

            Assert.That(ContentLoader.LoadWeather(Json(Climate("spring", ok), Climate("summer", ok), Climate("monsoon", ok), Climate("winter", ok)))
                .ClimateFor(Season.Winter).Weights, Is.EqualTo(new[] { 1, 0 }));

            ContentException Err(string json) { return Assert.Throws<ContentException>(() => ContentLoader.LoadWeather(json)); }
            Assert.That(Err(Json(Climate("spring", ok), Climate("summer", ok), Climate("monsoon", ok))).Message, Does.Contain("winter"));
            Assert.That(Err(Json(Climate("spring", "\"snow\":1"), Climate("summer", ok), Climate("monsoon", ok), Climate("winter", ok))).FieldPath,
                Is.EqualTo("weather.climates[0].weights.snow"));
            Assert.That(Err(Json(Climate("spring", "\"sun\":0"), Climate("summer", ok), Climate("monsoon", ok), Climate("winter", ok))).FieldPath,
                Is.EqualTo("weather.climates[0].weights"));
            Assert.That(Err(Json(Climate("spring", "\"sun\":-1,\"rain\":2"), Climate("summer", ok), Climate("monsoon", ok), Climate("winter", ok))).FieldPath,
                Is.EqualTo("weather.climates[0].weights.sun"));
            // Spec 001 AC-29: persistence per weather, 0–0.98, at most 3 decimals, required for a weather with a share.
            string Spring(string p) { return Json(Climate("spring", ok, p), Climate("summer", ok), Climate("monsoon", ok), Climate("winter", ok)); }
            Assert.That(Err(Spring("\"sun\":0.99")).FieldPath, Is.EqualTo("weather.climates[0].persistence.sun"));
            Assert.That(Err(Spring("\"sun\":0.9512")).FieldPath, Is.EqualTo("weather.climates[0].persistence.sun"));
            Assert.That(Err(Spring("\"snow\":0.5")).FieldPath, Is.EqualTo("weather.climates[0].persistence.snow"));
            Assert.That(Err(Spring("")).FieldPath, Is.EqualTo("weather.climates[0].persistence.sun"));
            Assert.That(ContentLoader.LoadWeather(Spring("\"sun\":0.98")).ClimateFor(Season.Spring).DrawWeights, Is.EqualTo(new[] { 20, 0 }));
            Assert.That(Err(Json(Climate("spring", ok), Climate("spring", ok), Climate("monsoon", ok), Climate("winter", ok))).FieldPath,
                Is.EqualTo("weather.climates[1].season"));
            Assert.That(Err("{\"weatherTypes\": [{\"id\":\"sun\",\"nameKey\":\"k\",\"isSevere\":false},{\"id\":\"sun\",\"nameKey\":\"k\",\"isSevere\":false}], \"climates\": []}").FieldPath,
                Is.EqualTo("weather.weatherTypes[1].id"));
        }
    }
}

namespace Sim.Core.Tests
{
    using System;
    using NUnit.Framework;
    using Sim.Content;

    public partial class ContentTests
    {
        private static ContentException Error(string file, Func<string, string> edit)
        {
            return Assert.Throws<ContentException>(() => TestContent.Game((f, json) => f == file ? edit(json) : json));
        }

        // Spec 002 AC-2.
        [Test]
        public void Land_Validation()
        {
            // Tile 85 (inherited) also in w1.
            ContentException e = Error("map.json", j => j.Replace("\"tiles\": [82, 83]", "\"tiles\": [82, 83, 85]"));
            Assert.That(e.FieldPath, Is.EqualTo("map.parcels[9].tiles[2]"));
            Assert.That(e.Message, Does.Contain("tile 85").And.Contain("two parcels"));

            // Tile 83 in no parcel.
            e = Error("map.json", j => j.Replace("\"tiles\": [82, 83]", "\"tiles\": [82]"));
            Assert.That(e.FieldPath, Is.EqualTo("map.tiles[83]"));
            Assert.That(e.Message, Does.Contain("no parcel"));

            // A tile names an unknown soil.
            e = Error("map.json", j => j.Replace("\"soil\": \"loam\"", "\"soil\": \"peat\""));
            Assert.That(e.FieldPath, Is.EqualTo("map.tiles[0].soil"));

            // moistureByWeather misses a weather id.
            e = Error("land.json", j => j.Replace("\"frost\": -0.1, ", ""));
            Assert.That(e.FieldPath, Is.EqualTo("land.land.moistureByWeather"));
            Assert.That(e.Message, Does.Contain("frost"));

            // Bounds and cross-file checks.
            Assert.That(Error("land.json", j => j.Replace("\"quickSaleFactor\": 0.80", "\"quickSaleFactor\": 1.5")).FieldPath,
                Is.EqualTo("land.land.quickSaleFactor"));
            Assert.That(Error("start.json", j => j.Replace("[\"home_a\", \"home_b\", \"home_c\"]", "[\"w1\"]")).FieldPath,
                Is.EqualTo("start.inheritedParcels"));
            Assert.That(Error("start.json", j => j.Replace("[\"home_a\", \"home_b\", \"home_c\"]", "[\"home_a\", \"home_a\"]")).FieldPath,
                Is.EqualTo("start.inheritedParcels[1]"));
            Assert.That(Error("start.json", j => j.Replace("\"tile\": 87 },\n    { \"building\": \"well\"", "\"tile\": 86 },\n    { \"building\": \"well\"")).FieldPath,
                Is.EqualTo("start.buildings[0].tile"));
            Assert.That(Error("finance.json", j => j.Replace("\"retainSeasons\": 4", "\"retainSeasons\": 0")).FieldPath, Is.EqualTo("finance.ledger.retainSeasons"));
        }

        // Spec 005 AC-29: lenders.json and the credit block of finance.json fail validation naming the field.
        [Test]
        public void Lender_Validation()
        {
            System.Func<string, string, System.Func<string, string>, ContentException> err = (file, _, edit) =>
                Assert.Throws<ContentException>(() => TestContent.Game((f, j) => f == file ? edit(j) : j));

            Assert.That(err("lenders.json", "", j => j.Replace("\"baseRate\": 0.06", "\"baseRate\": 0.3")).FieldPath, Is.EqualTo("lenders.lenders[0].baseRate"));
            Assert.That(err("lenders.json", "", j => j.Replace("\"minTerm\": 2, \"maxTerm\": 16", "\"minTerm\": 9, \"maxTerm\": 3")).FieldPath, Is.EqualTo("lenders.lenders[1].minTerm"));
            Assert.That(err("lenders.json", "", j => j.Replace("\"eraCaps\": { \"homestead\": 50000 }", "\"eraCaps\": { \"village\": 50000 }")).FieldPath, Is.EqualTo("lenders.lenders[0].eraCaps"));
            Assert.That(err("lenders.json", "", j => j.Replace("\"graceCloses\": 1", "\"graceCloses\": 0")).FieldPath, Is.EqualTo("lenders.lenders[0].graceCloses"));
            Assert.That(err("lenders.json", "", j => j.Replace("\"schedule\": \"bullet\"", "\"schedule\": \"annuity\"")).FieldPath, Is.EqualTo("lenders.lenders[0].schedule"));
            Assert.That(err("finance.json", "", j => j.Replace("\"foreclosureScore\": 350", "\"foreclosureScore\": 100")).FieldPath, Is.EqualTo("finance.credit.foreclosureScore"));
            Assert.That(err("finance.json", "", j => j.Replace("\"finalNoticeLiveHours\": 24", "\"finalNoticeLiveHours\": 0")).FieldPath, Is.EqualTo("finance.credit.finalNoticeLiveHours"));

            // Credit is optional: a finance.json without it needs no lenders.json (ADR 0038).
            GameDefs none = TestContent.Game((f, j) => f == "finance.json" ? "{ \"ledger\": { \"retainSeasons\": 4 } }" : f == "lenders.json" ? "not json" : j);
            Assert.That(none.Credit, Is.Null);
            Assert.That(TestContent.Game().Credit.Lenders.Select(l => l.Id), Is.EqualTo(new[] { "moneylender", "coop_bank" }));
        }
    }
    public partial class ContentTests
    {
        // Spec 006 AC-1.
        [Test]
        public void People_Validation()
        {
            ContentException e = Error("people.json", j => j.Replace("\"spring\": 2", "\"spring\": 9"));
            Assert.That(e.FieldPath, Is.EqualTo("people.labourPool.availablePerDay.spring"));
            e = Error("people.json", j => j.Replace("\"skillMin\": 20", "\"skillMin\": 80"));
            Assert.That(e.FieldPath, Is.EqualTo("people.hand.skillMin"));
            e = Error("people.json", j => j.Replace("\"id\": \"steady\"", "\"id\": \"steady\", \"creative\": 1.2"));
            Assert.That(e.FieldPath, Is.EqualTo("people.traits[5].creative"));
            e = Error("buildings.json", j => j.Replace("\"housesHands\": 1", "\"housesHands\": 0"));
            Assert.That(e.FieldPath, Is.EqualTo("buildings.hand_hut.housesHands"));
            // Without a hand's hut there are no people: the frozen golden content loads as before.
            GameDefs none = TestContent.Game((f, j) => f == "buildings.json" ? System.Text.RegularExpressions.Regex.Replace(j, ",\\s*\\{ \"id\": \"hand_hut\"[^}]*\\}", "") : j);
            Assert.That(none.People, Is.Null);
        }
        [Test]
        public void Livestock()
        {
            GameDefs g = TestContent.Game();
            LivestockDefs d = g.Livestock;
            Assert.That(d, Is.Not.Null);
            Assert.That(g.Buildings[d.Shed].Animals, Is.EqualTo(4));
            Assert.That(g.Map.Tiles[d.Livestock.ShedTile].Terrain, Is.EqualTo(Terrain.Yard));
            Assert.That(g.Onboarding.Eras[d.UnlockEra].Id, Is.EqualTo("hamlet"));
            foreach (AnimalDef a in d.Animals)
            {
                // Spec 012 §3.2: the curve's mean over the milk days is 1.00 ± 1%.
                Sim.Core.Maths.Fixed sum = Sim.Core.Maths.Fixed.Zero;
                for (int day = 0; day < a.MilkDays; day++) sum += Sim.Core.Livestock.LivestockRules.Lactation(d.Livestock, day);
                double mean = double.Parse(sum.MulRatio(1, a.MilkDays).ToString(), System.Globalization.CultureInfo.InvariantCulture);
                Assert.That(mean, Is.EqualTo(1.0).Within(0.01), a.Id);
            }
            Assert.That((d.Livestock.GreenWeight + d.Livestock.StrawWeight + d.Livestock.FeedWeight - Sim.Core.Maths.Fixed.One).Raw, Is.InRange(-10, 10));
            Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) => f == "livestock.json" ? j.Replace("\"shedTile\": 103", "\"shedTile\": 84") : j));
            Assert.Throws<Sim.Content.ContentException>(() => TestContent.Game((f, j) => f == "livestock.json" ? j.Replace("\"green\": 0.4,", "\"green\": 0.5,") : j));
        }
    }
}

