using System;
using System.Collections.Generic;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Definitions
{
    /// <summary>One weather type from <c>content/weather.json</c> (spec 001 §4).</summary>
    public sealed class WeatherTypeDef
    {
        public string Id { get; }
        public string NameKey { get; }

        /// <summary>Read by later specs (crop health, away rules).</summary>
        public bool IsSevere { get; }

        public WeatherTypeDef(string id, string nameKey, bool isSevere)
        {
            Id = id;
            NameKey = nameKey;
            IsSevere = isSevere;
        }
    }

    /// <summary>A season's climate table (spec 001 §3.5).</summary>
    public sealed class ClimateDef
    {
        public Season Season { get; }

        /// <summary>The long-run shares, indexed like <see cref="WeatherSet.Types"/>: the declared order of the weather types.</summary>
        public IReadOnlyList<int> Weights { get { return _weights; } }

        /// <summary>Per weather: the chance the next hour keeps this hour's weather (0–0.98; ADR 0042).</summary>
        public IReadOnlyList<Fixed> Persistence { get { return _persistence; } }

        /// <summary>The weights a fresh draw uses: shareᵢ × round(1000 × (1 − pᵢ)), so the long-run share of each weather equals its share.</summary>
        public IReadOnlyList<int> DrawWeights { get { return _draw; } }

        private readonly int[] _weights;
        private readonly Fixed[] _persistence;
        private readonly int[] _draw;

        public ClimateDef(Season season, int[] weights, Fixed[] persistence)
        {
            Season = season;
            _weights = (int[])(weights ?? throw new ArgumentNullException(nameof(weights))).Clone();
            _persistence = (Fixed[])(persistence ?? throw new ArgumentNullException(nameof(persistence))).Clone();
            if (_persistence.Length != _weights.Length) throw new ArgumentException("One persistence per weather is required.", nameof(persistence));
            _draw = new int[_weights.Length];
            for (int i = 0; i < _weights.Length; i++)
                _draw[i] = checked(_weights[i] * (int)(Fixed.One - _persistence[i]).MulRatio(1000, 1).RoundToLong());
        }

        internal int[] WeightsArray { get { return _weights; } }

        internal int[] DrawWeightsArray { get { return _draw; } }
    }

    /// <summary>All weather types and the four climate tables.</summary>
    public sealed class WeatherSet
    {
        /// <summary>Weather types in declared order. A weather's index in this list is its id in the state.</summary>
        public IReadOnlyList<WeatherTypeDef> Types { get; }

        private readonly ClimateDef[] _climates;

        /// <param name="climates">Exactly one per season, indexed by <see cref="Season"/>.</param>
        public WeatherSet(IReadOnlyList<WeatherTypeDef> types, ClimateDef[] climates)
        {
            Types = new List<WeatherTypeDef>(types).AsReadOnly();
            if (climates == null || climates.Length != ClockDef.SeasonsPerYear)
                throw new ArgumentException("One climate per season is required.", nameof(climates));
            _climates = new ClimateDef[ClockDef.SeasonsPerYear];
            foreach (ClimateDef c in climates)
            {
                if (c.Weights.Count != Types.Count)
                    throw new ArgumentException("Climate " + c.Season + " has " + c.Weights.Count + " weights for " + Types.Count + " types.");
                _climates[(int)c.Season] = c;
            }
            for (int i = 0; i < _climates.Length; i++)
                if (_climates[i] == null) throw new ArgumentException("Missing climate for " + (Season)i + ".", nameof(climates));
        }

        public ClimateDef ClimateFor(Season season) { return _climates[(int)season]; }

        /// <summary>The index of a weather id, or −1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < Types.Count; i++)
                if (Types[i].Id == id) return i;
            return -1;
        }
    }
}
