using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.Core.Tests
{
    public class CalendarTests
    {
        private const long Day = 86400;
        private static readonly ClockDef Clock = TestContent.Clock();

        private static ClockDef WithDaysPerSeason(int days)
        {
            return new ClockDef(days, Clock.WeatherSpellHours, Clock.LifeYearsPerSeason, Clock.AwayCapHours,
                Clock.AwayThresholdSeconds, Clock.ForecastDays, Clock.ForecastAccuracy);
        }

        // Spec 001 AC-5.
        [Test]
        public void Epoch_IsSpringDay1Year1()
        {
            CalendarDate c = Calendar.From(0, Clock);
            Assert.That(c.Season, Is.EqualTo(Season.Spring));
            Assert.That(c.DayOfSeason, Is.EqualTo(1));
            Assert.That(Clock.DaysPerSeason, Is.EqualTo(4));
            Assert.That(c.Year, Is.EqualTo(1));
            Assert.That((c.Hour, c.Minute, c.Second), Is.EqualTo((0, 0, 0)));
            Assert.That(c.ToString(), Is.EqualTo("Y1 Spring D1 00:00:00"));
        }

        // Spec 001 AC-6.
        [Test]
        public void SeasonAndYearBoundaries()
        {
            CalendarDate summer = Calendar.From(4 * Day, Clock);
            Assert.That((summer.Season, summer.DayOfSeason, summer.Year), Is.EqualTo((Season.Summer, 1, 1L)));

            CalendarDate nextYear = Calendar.From(16 * Day, Clock);
            Assert.That((nextYear.Season, nextYear.DayOfSeason, nextYear.Year), Is.EqualTo((Season.Spring, 1, 2L)));

            CalendarDate last = Calendar.From(16 * Day - 1, Clock);
            Assert.That((last.Season, last.DayOfSeason, last.Year), Is.EqualTo((Season.Winter, 4, 1L)));
            Assert.That((last.Hour, last.Minute, last.Second), Is.EqualTo((23, 59, 59)));

            CalendarDate monsoon = Calendar.From(8 * Day + 14 * 3600 + 5 * 60, Clock);
            Assert.That(monsoon.ToString(), Is.EqualTo("Y1 Monsoon D1 14:05:00"));
            Assert.That(monsoon.Day, Is.EqualTo(8));
        }

        // Spec 001 AC-7.
        [Test]
        public void SeasonLength_IsDataDriven()
        {
            ClockDef five = WithDaysPerSeason(5);
            Assert.That(Calendar.From(4 * Day, five).Season, Is.EqualTo(Season.Spring));
            Assert.That(Calendar.From(4 * Day, five).DayOfSeason, Is.EqualTo(5));
            CalendarDate summer = Calendar.From(5 * Day, five);
            Assert.That((summer.Season, summer.DayOfSeason), Is.EqualTo((Season.Summer, 1)));
            Assert.That(Calendar.From(20 * Day, five).Year, Is.EqualTo(2));
        }

        // Spec 001 AC-8.
        [Test]
        public void LifeClock_Age()
        {
            long eighteenSeasons = 18 * Clock.SecondsPerSeason;
            Assert.That(Calendar.AgeLifeYears(0, eighteenSeasons, Clock), Is.EqualTo(Fixed.FromInt(18)));

            var fast = new ClockDef(4, 6, Fixed.FromInt(2), 72, 60, 3, Clock.ForecastAccuracy);
            Assert.That(Calendar.AgeLifeYears(0, eighteenSeasons, fast), Is.EqualTo(Fixed.FromInt(36)));

            // Born mid-way: half a season later is 0.5 life-years at the default rate.
            Assert.That(Calendar.AgeLifeYears(Day, Day + 2 * Day, Clock), Is.EqualTo(Fixed.FromRatio(1, 2)));
        }

        [Test]
        public void SpellIndex()
        {
            Assert.That(Calendar.SpellAt(0, Clock), Is.EqualTo(0));
            Assert.That(Calendar.SpellAt(6 * 3600 - 1, Clock), Is.EqualTo(0));
            Assert.That(Calendar.SpellAt(6 * 3600, Clock), Is.EqualTo(1));
            Assert.That(Calendar.SpellAt(Day, Clock), Is.EqualTo(4));
        }
    }
}
