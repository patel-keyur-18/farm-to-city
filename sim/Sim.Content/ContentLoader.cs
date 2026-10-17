using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    /// <summary>
    /// Loads and validates content from JSON text (ADR 0020). The host decides where the text comes from:
    /// tests read <c>content/</c>, Unity reads its bundled copy. Errors are <see cref="ContentException"/>s
    /// whose message starts with the field path.
    /// </summary>
    public static class ContentLoader
    {
        /// <summary>Loads <c>clock.json</c> and checks the bounds in spec 001 §4.</summary>
        public static ClockDef LoadClock(string json)
        {
            const string root = "clock";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "daysPerSeason", "weatherSpellHours", "lifeYearsPerSeason", "awayCapHours",
                "awayThresholdSeconds", "forecastDays", "forecastAccuracy");

            int daysPerSeason = Int(Required(o, root, "daysPerSeason"), root + ".daysPerSeason");
            Range(daysPerSeason, 1, 15, root + ".daysPerSeason");

            int spellHours = Int(Required(o, root, "weatherSpellHours"), root + ".weatherSpellHours");
            if (spellHours < 1 || 24 % spellHours != 0)
                throw new ContentException(root + ".weatherSpellHours", "must divide 24 (was " + spellHours + ")");

            Fixed lifeYears = Number(Required(o, root, "lifeYearsPerSeason"), root + ".lifeYearsPerSeason");
            Range(lifeYears, Fixed.FromRatio(1, 4), Fixed.FromInt(4), root + ".lifeYearsPerSeason");

            int capHours = Int(Required(o, root, "awayCapHours"), root + ".awayCapHours");
            Range(capHours, 24, 168, root + ".awayCapHours");

            int threshold = Int(Required(o, root, "awayThresholdSeconds"), root + ".awayThresholdSeconds");
            Range(threshold, 5, 600, root + ".awayThresholdSeconds");

            int forecastDays = Int(Required(o, root, "forecastDays"), root + ".forecastDays");
            Range(forecastDays, 1, 3, root + ".forecastDays");

            string accPath = root + ".forecastAccuracy";
            List<JToken> accTokens = Array(Required(o, root, "forecastAccuracy"), accPath);
            if (accTokens.Count != forecastDays)
                throw new ContentException(accPath, "needs one value per forecast day (" + forecastDays + "), has " + accTokens.Count);
            var accuracy = new Fixed[accTokens.Count];
            for (int i = 0; i < accuracy.Length; i++)
            {
                string p = Index(accPath, i);
                accuracy[i] = Number(accTokens[i], p);
                Range(accuracy[i], Fixed.Zero, Fixed.One, p);
                if (i > 0 && accuracy[i] > accuracy[i - 1])
                    throw new ContentException(p, "accuracies must not increase (" + accuracy[i] + " > " + accuracy[i - 1] + ")");
            }

            return new ClockDef(daysPerSeason, spellHours, lifeYears, capHours, threshold, forecastDays, accuracy);
        }

        /// <summary>Loads <c>weather.json</c>: the weather types and one climate table per season.</summary>
        public static WeatherSet LoadWeather(string json)
        {
            const string root = "weather";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "weatherTypes", "climates");

            string typesPath = root + ".weatherTypes";
            List<JToken> typeTokens = Array(Required(o, root, "weatherTypes"), typesPath);
            if (typeTokens.Count == 0) throw new ContentException(typesPath, "must not be empty");
            var types = new List<WeatherTypeDef>();
            var ids = new List<string>();
            for (int i = 0; i < typeTokens.Count; i++)
            {
                string p = Index(typesPath, i);
                JObject t = Object(typeTokens[i], p);
                OnlyFields(t, p, "id", "nameKey", "isSevere");
                string id = String(Required(t, p, "id"), p + ".id");
                if (ids.Contains(id)) throw new ContentException(p + ".id", "duplicate weather id '" + id + "'");
                ids.Add(id);
                types.Add(new WeatherTypeDef(id, String(Required(t, p, "nameKey"), p + ".nameKey"),
                    Bool(Required(t, p, "isSevere"), p + ".isSevere")));
            }

            string climatesPath = root + ".climates";
            List<JToken> climateTokens = Array(Required(o, root, "climates"), climatesPath);
            var climates = new ClimateDef[ClockDef.SeasonsPerYear];
            for (int i = 0; i < climateTokens.Count; i++)
            {
                string p = Index(climatesPath, i);
                JObject c = Object(climateTokens[i], p);
                OnlyFields(c, p, "season", "persistence", "weights");

                Season season = ParseSeason(String(Required(c, p, "season"), p + ".season"), p + ".season");
                if (climates[(int)season] != null)
                    throw new ContentException(p + ".season", "duplicate climate for " + season);

                Fixed persistence = Number(Required(c, p, "persistence"), p + ".persistence");
                Range(persistence, Fixed.Zero, Fixed.FromRatio(95, 100), p + ".persistence");

                // Weights are stored in the declared order of weatherTypes, never in JSON object order.
                string wPath = p + ".weights";
                JObject w = Object(Required(c, p, "weights"), wPath);
                var weights = new int[types.Count];
                long total = 0;
                foreach (JProperty prop in w.Properties())
                {
                    int index = ids.IndexOf(prop.Name);
                    if (index < 0) throw new ContentException(Join(wPath, prop.Name), "unknown weather id");
                    int weight = Int(prop.Value, Join(wPath, prop.Name));
                    if (weight < 0) throw new ContentException(Join(wPath, prop.Name), "must not be negative (was " + weight + ")");
                    weights[index] = weight;
                    total += weight;
                }
                if (total <= 0) throw new ContentException(wPath, "the weights must add up to more than 0");

                climates[(int)season] = new ClimateDef(season, weights, persistence);
            }
            for (int s = 0; s < climates.Length; s++)
                if (climates[s] == null)
                    throw new ContentException(climatesPath, "missing the climate for " + SeasonName((Season)s));

            return new WeatherSet(types, climates);
        }

        private static Season ParseSeason(string name, string path)
        {
            for (int s = 0; s < ClockDef.SeasonsPerYear; s++)
                if (SeasonName((Season)s) == name) return (Season)s;
            throw new ContentException(path, "unknown season '" + name + "' (expected spring, summer, monsoon or winter)");
        }

        private static string SeasonName(Season s)
        {
            switch (s)
            {
                case Season.Spring: return "spring";
                case Season.Summer: return "summer";
                case Season.Monsoon: return "monsoon";
                default: return "winter";
            }
        }
    }
}
