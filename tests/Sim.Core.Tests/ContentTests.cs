using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Tests
{
    public class ContentTests
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
            Assert.That(c.WeatherSpellHours, Is.EqualTo(6));
            Assert.That(c.LifeYearsPerSeason, Is.EqualTo(Fixed.One));
            Assert.That(c.AwayCapHours, Is.EqualTo(72));
            Assert.That(c.AwayThresholdSeconds, Is.EqualTo(60));
            Assert.That(c.ForecastDays, Is.EqualTo(3));
            Assert.That(c.ForecastAccuracy, Is.EqualTo(d.ForecastAccuracy));
            Assert.That(c.ForecastSpells, Is.EqualTo(12));
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
            Assert.That(w.ClimateFor(Season.Spring).Weights, Is.EqualTo(new[] { 45, 30, 20, 3, 0, 2, 0 }));
            Assert.That(w.ClimateFor(Season.Summer).Weights, Is.EqualTo(new[] { 50, 15, 5, 0, 25, 0, 5 }));
            Assert.That(w.ClimateFor(Season.Monsoon).Weights, Is.EqualTo(new[] { 10, 25, 35, 20, 0, 0, 10 }));
            Assert.That(w.ClimateFor(Season.Winter).Weights, Is.EqualTo(new[] { 45, 30, 5, 0, 0, 20, 0 }));
            Assert.That(w.ClimateFor(Season.Spring).Persistence, Is.EqualTo(Fixed.Parse("0.50")));
            Assert.That(w.ClimateFor(Season.Summer).Persistence, Is.EqualTo(Fixed.Parse("0.55")));
            Assert.That(w.ClimateFor(Season.Monsoon).Persistence, Is.EqualTo(Fixed.Parse("0.60")));
            Assert.That(w.ClimateFor(Season.Winter).Persistence, Is.EqualTo(Fixed.Parse("0.50")));
        }

        [Test]
        public void WeatherJson_Validation()
        {
            string types = "\"weatherTypes\": [{\"id\":\"sun\",\"nameKey\":\"k\",\"isSevere\":false},{\"id\":\"rain\",\"nameKey\":\"k\",\"isSevere\":false}]";
            string Climate(string season, string weights, string p = "0.5")
            {
                return "{\"season\":\"" + season + "\",\"persistence\":" + p + ",\"weights\":{" + weights + "}}";
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
            Assert.That(Err(Json(Climate("spring", ok, "0.96"), Climate("summer", ok), Climate("monsoon", ok), Climate("winter", ok))).FieldPath,
                Is.EqualTo("weather.climates[0].persistence"));
            Assert.That(Err(Json(Climate("spring", ok), Climate("spring", ok), Climate("monsoon", ok), Climate("winter", ok))).FieldPath,
                Is.EqualTo("weather.climates[1].season"));
            Assert.That(Err("{\"weatherTypes\": [{\"id\":\"sun\",\"nameKey\":\"k\",\"isSevere\":false},{\"id\":\"sun\",\"nameKey\":\"k\",\"isSevere\":false}], \"climates\": []}").FieldPath,
                Is.EqualTo("weather.weatherTypes[1].id"));
        }
    }
}
