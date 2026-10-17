using System;
using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Clock settings from <c>content/clock.json</c> (spec 001 §4). Bounds are checked by the
    /// content loader in Sim.Content.
    /// </summary>
    public sealed class ClockDef
    {
        public const long SecondsPerDay = 86400;
        public const int SeasonsPerYear = 4;

        public int DaysPerSeason { get; }
        public int WeatherSpellHours { get; }
        public Fixed LifeYearsPerSeason { get; }
        public int AwayCapHours { get; }
        public int AwayThresholdSeconds { get; }
        public int ForecastDays { get; }

        /// <summary>Chance the forecast shows the actual weather, indexed by bucket (0 = day 1).</summary>
        public IReadOnlyList<Fixed> ForecastAccuracy { get; }

        public ClockDef(int daysPerSeason, int weatherSpellHours, Fixed lifeYearsPerSeason, int awayCapHours,
            int awayThresholdSeconds, int forecastDays, IReadOnlyList<Fixed> forecastAccuracy)
        {
            DaysPerSeason = daysPerSeason;
            WeatherSpellHours = weatherSpellHours;
            LifeYearsPerSeason = lifeYearsPerSeason;
            AwayCapHours = awayCapHours;
            AwayThresholdSeconds = awayThresholdSeconds;
            ForecastDays = forecastDays;
            ForecastAccuracy = new List<Fixed>(forecastAccuracy ?? throw new ArgumentNullException(nameof(forecastAccuracy))).AsReadOnly();
        }

        /// <summary>The spec 001 defaults, matching <c>content/clock.json</c>.</summary>
        public static ClockDef Default()
        {
            return new ClockDef(4, 6, Fixed.One, 72, 60, 3,
                new[] { Fixed.One, Fixed.FromRatio(80, 100), Fixed.FromRatio(65, 100) });
        }

        /// <summary>S = D × daysPerSeason.</summary>
        public long SecondsPerSeason { get { return SecondsPerDay * DaysPerSeason; } }

        public long SecondsPerYear { get { return SecondsPerSeason * SeasonsPerYear; } }

        /// <summary>W = weatherSpellHours × 3600.</summary>
        public long SpellSeconds { get { return WeatherSpellHours * 3600L; } }

        public int SpellsPerDay { get { return 24 / WeatherSpellHours; } }

        /// <summary>Spells covered by the forecast: forecastDays × 24 / weatherSpellHours.</summary>
        public int ForecastSpells { get { return ForecastDays * SpellsPerDay; } }

        /// <summary>C = awayCapHours × 3600.</summary>
        public long AwayCapSeconds { get { return AwayCapHours * 3600L; } }
    }
}
