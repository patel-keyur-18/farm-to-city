using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Weather
{
    /// <summary>
    /// The weather and forecast formulas (spec 001 §3.5–3.6) as pure functions of the seed, so they can be
    /// tested on their own and the system that calls them stays thin.
    /// </summary>
    public static class WeatherRules
    {
        /// <summary>The forecast's alternative pick uses counter <c>AlternativeCounterOffset + bucket</c> (owner decision, 2026-10-17).</summary>
        public const ulong AlternativeCounterOffset = 16;

        /// <summary>
        /// The weather of spell <paramref name="spell"/> (spec 001 §3.5):
        /// <list type="number">
        /// <item>No previous spell, or the previous weather has weight 0 in this climate: weighted pick.</item>
        /// <item>Otherwise keep the previous weather with probability <c>persistence</c>, else a weighted pick.</item>
        /// </list>
        /// Stream Weather, key = spell; counter 0 is the persistence roll, counter 1 the weighted pick.
        /// </summary>
        /// <param name="previous">The previous spell's weather index, or −1 for spell 0.</param>
        public static int SpellWeather(ulong seed, long spell, int previous, ClimateDef climate)
        {
            ulong key = (ulong)spell;
            if (previous >= 0 && climate.Weights[previous] > 0 &&
                Rng.Chance(Rng.Next(seed, RngStream.Weather, key, 0), climate.Persistence))
                return previous;
            return Rng.WeightedPick(Rng.Next(seed, RngStream.Weather, key, 1), climate.WeightsArray);
        }

        /// <summary>The bucket of a spell <paramref name="spellsAhead"/> (≥ 1) spells ahead: (k − 1) / spellsPerDay.</summary>
        public static int Bucket(long spellsAhead, ClockDef clock)
        {
            return (int)((spellsAhead - 1) / clock.SpellsPerDay);
        }

        /// <summary>
        /// What the forecast shows for <paramref name="spell"/> in <paramref name="bucket"/> (spec 001 §3.6):
        /// the actual weather with probability <c>forecastAccuracy[bucket]</c>, otherwise an alternative
        /// picked from the season's weights with the actual weather removed (the actual one if nothing is left).
        /// Stream Forecast, key = spell; counter = bucket for the accuracy roll,
        /// <see cref="AlternativeCounterOffset"/> + bucket for the alternative.
        /// </summary>
        public static int ForecastWeather(ulong seed, long spell, int bucket, int actual, ClimateDef climate, ClockDef clock)
        {
            ulong key = (ulong)spell;
            if (Rng.Chance(Rng.Next(seed, RngStream.Forecast, key, (ulong)bucket), clock.ForecastAccuracy[bucket]))
                return actual;

            int[] weights = (int[])climate.WeightsArray.Clone();
            weights[actual] = 0;
            long total = 0;
            foreach (int w in weights) total += w;
            if (total == 0) return actual;
            return Rng.WeightedPick(Rng.Next(seed, RngStream.Forecast, key, AlternativeCounterOffset + (ulong)bucket), weights);
        }
    }
}
