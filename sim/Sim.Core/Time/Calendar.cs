using System;
using Sim.Core.Definitions;
using Sim.Core.Maths;

namespace Sim.Core.Time
{
    /// <summary>A calendar reading of a sim time (spec 001 §3.2).</summary>
    public readonly struct CalendarDate
    {
        /// <summary>Days since t = 0 (day index, from 0).</summary>
        public long Day { get; }
        public Season Season { get; }
        /// <summary>1 to daysPerSeason.</summary>
        public int DayOfSeason { get; }
        /// <summary>From 1.</summary>
        public long Year { get; }
        public int Hour { get; }
        public int Minute { get; }
        public int Second { get; }

        public CalendarDate(long day, Season season, int dayOfSeason, long year, int hour, int minute, int second)
        {
            Day = day;
            Season = season;
            DayOfSeason = dayOfSeason;
            Year = year;
            Hour = hour;
            Minute = minute;
            Second = second;
        }

        /// <summary>For logs and tests, e.g. <c>Y1 Spring D2 14:05:00</c>. Player-facing text uses string tables (spec 008).</summary>
        public override string ToString()
        {
            return "Y" + Year + " " + Season + " D" + DayOfSeason + " " + Hour.ToString("00") + ":" + Minute.ToString("00") + ":" + Second.ToString("00");
        }
    }

    /// <summary>Calendar arithmetic. Season length comes from data; a day is always 86 400 s (real time).</summary>
    public static class Calendar
    {
        public static CalendarDate From(long t, ClockDef clock)
        {
            if (t < 0) throw new ArgumentOutOfRangeException(nameof(t), "Sim time is never negative.");
            const long d = ClockDef.SecondsPerDay;
            long s = clock.SecondsPerSeason;
            long secondOfDay = t % d;
            return new CalendarDate(
                day: t / d,
                season: (Season)((t / s) % ClockDef.SeasonsPerYear),
                dayOfSeason: (int)((t % s) / d) + 1,
                year: t / (ClockDef.SeasonsPerYear * s) + 1,
                hour: (int)(secondOfDay / 3600),
                minute: (int)(secondOfDay % 3600 / 60),
                second: (int)(secondOfDay % 60));
        }

        public static Season SeasonAt(long t, ClockDef clock)
        {
            return (Season)((t / clock.SecondsPerSeason) % ClockDef.SeasonsPerYear);
        }

        /// <summary>Spell index: t / W.</summary>
        public static long SpellAt(long t, ClockDef clock) { return t / clock.SpellSeconds; }

        /// <summary>
        /// Life-clock age: (t − birthTime) × lifeYearsPerSeason / S, rounded once to the nearest Q31.32 value.
        /// </summary>
        public static Fixed AgeLifeYears(long birthTime, long t, ClockDef clock)
        {
            return clock.LifeYearsPerSeason.MulRatio(t - birthTime, clock.SecondsPerSeason);
        }
    }
}
