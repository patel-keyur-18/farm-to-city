using NUnit.Framework;
using Sim.Core.Maths;
using Sim.Core.Time;
using Sim.View.Text;

namespace Sim.View.Tests
{
    /// <summary>Spec 008 AC-4: the locale formatter gives exactly §3.11's examples.</summary>
    public class FormatTests
    {
        private static long Coins(decimal c) { return (long)(c * 100); }

        [Test]
        public void Examples()
        {
            Formatter @in = ViewKit.Indian(), west = ViewKit.Western();
            Assert.That(@in.Integer(1234567), Is.EqualTo("12,34,567"));
            Assert.That(west.Integer(1234567), Is.EqualTo("1,234,567"));
            Assert.That(@in.Coins(Coins(120000.40m)), Is.EqualTo("1,20,000 coins"));
            Assert.That(west.Coins(Coins(120000.40m)), Is.EqualTo("120,000 coins"));
            Assert.That(@in.ShortCoins(Coins(12345678)), Is.EqualTo("1.23 Cr"));
            Assert.That(west.ShortCoins(Coins(12345678)), Is.EqualTo("12.34M"));
            Assert.That(@in.Coins(-Coins(3200)), Is.EqualTo("−3,200 coins"), "a true minus sign");
            Assert.That(@in.Kg(70_744), Is.EqualTo("70.7 kg"));
            Assert.That(@in.PricePerKg(Fixed.Parse("5.3")), Is.EqualTo("5.30 /kg"));
            Assert.That(@in.Percent(Fixed.Parse("0.0625")), Is.EqualTo("6.3%"));
            Assert.That(@in.Duration(11_520), Is.EqualTo("3 h 12 min"));
        }

        [Test]
        public void MoreExamples()
        {
            Formatter f = ViewKit.Indian();
            Assert.That(f.Coins(Coins(1)), Is.EqualTo("1 coin"));
            Assert.That(f.Coins(Coins(0)), Is.EqualTo("0 coins"));
            Assert.That(f.Coins(50), Is.EqualTo("1 coin"), "0.50 rounds half away from zero");
            Assert.That(f.Coins(-50), Is.EqualTo("−1 coin"));
            Assert.That(f.Coins(49), Is.EqualTo("0 coins"));
            Assert.That(f.Paise(Coins(120000.40m)), Is.EqualTo("1,20,000.40"));
            Assert.That(f.Paise(-5), Is.EqualTo("−0.05"));
            Assert.That(f.Integer(0), Is.EqualTo("0"));
            Assert.That(f.Integer(999), Is.EqualTo("999"));
            Assert.That(f.Integer(1000), Is.EqualTo("1,000"));
            Assert.That(f.Integer(100000), Is.EqualTo("1,00,000"));
            Assert.That(f.Integer(10000000), Is.EqualTo("1,00,00,000"));
            Assert.That(f.Integer(-1234567), Is.EqualTo("−12,34,567"));
            Assert.That(ViewKit.Western().Integer(1234), Is.EqualTo("1,234"));
            Assert.That(ViewKit.Western().Integer(100000), Is.EqualTo("100,000"));

            // Short money: full numbers below a lakh, rounded down above, trailing zeros trimmed.
            Assert.That(f.ShortCoins(Coins(99999)), Is.EqualTo("99,999"));
            Assert.That(f.ShortCoins(Coins(100000)), Is.EqualTo("1 L"));
            Assert.That(f.ShortCoins(Coins(120000)), Is.EqualTo("1.2 L"));
            Assert.That(f.ShortCoins(Coins(129999)), Is.EqualTo("1.29 L"), "rounded down: the HUD never overstates cash");
            Assert.That(f.ShortCoins(Coins(10000000)), Is.EqualTo("1 Cr"));
            Assert.That(f.ShortCoins(Coins(34500000)), Is.EqualTo("3.45 Cr"));
            Assert.That(ViewKit.Western().ShortCoins(Coins(120000)), Is.EqualTo("120,000"), "a Western HUD shows full numbers below a million");
            Assert.That(ViewKit.Western().ShortCoins(Coins(1200000)), Is.EqualTo("1.2M"));

            // Quantities.
            Assert.That(f.Kg(1_850_000), Is.EqualTo("1,850 kg"));
            Assert.That(f.Kg(100_000), Is.EqualTo("100 kg"), "whole kg from 100 kg");
            Assert.That(f.Kg(99_949), Is.EqualTo("99.9 kg"));
            Assert.That(f.Kg(400), Is.EqualTo("0.4 kg"));
            Assert.That(f.Kg(0), Is.EqualTo("0.0 kg"));
            Assert.That(f.Kg(12_345_678), Is.EqualTo("12,346 kg"));
            Assert.That(ViewKit.Western().Kg(12_345_678), Is.EqualTo("12,346 kg"));

            // Percent and factor.
            Assert.That(f.Percent(Fixed.Parse("0.62")), Is.EqualTo("62%"));
            Assert.That(f.Percent(Fixed.Parse("0.035")), Is.EqualTo("3.5%"));
            Assert.That(f.Percent(Fixed.Parse("0.1")), Is.EqualTo("10%"));
            Assert.That(f.Percent(Fixed.Parse("-0.035")), Is.EqualTo("−3.5%"));
            Assert.That(f.Factor(Fixed.Parse("0.875")), Is.EqualTo("0.875"));
            Assert.That(f.Factor(Fixed.One), Is.EqualTo("1.00"));
            Assert.That(f.Factor(Fixed.Parse("1.1")), Is.EqualTo("1.10"));
            Assert.That(f.Factor(Fixed.Parse("0.9")), Is.EqualTo("0.90"));
            Assert.That(f.Factor(Fixed.Parse("1.0625")), Is.EqualTo("1.063"), "three decimals, ties away from zero");
            Assert.That(f.PricePerKg(Fixed.Parse("6.8")), Is.EqualTo("6.80 /kg"));

            // Durations: the two largest units.
            Assert.That(f.Duration(45), Is.EqualTo("45 s"));
            Assert.That(f.Duration(3600), Is.EqualTo("1 h"));
            Assert.That(f.Duration(3660), Is.EqualTo("1 h 1 min"));
            Assert.That(f.Duration(2 * 86400 + 4 * 3600 + 120), Is.EqualTo("2 d 4 h"));
            Assert.That(f.Duration(86400), Is.EqualTo("1 d"));
            Assert.That(f.Duration(125), Is.EqualTo("2 min 5 s"));
            Assert.That(f.Duration(0), Is.EqualTo("0 s"));
        }

        [Test]
        public void DateAndClock()
        {
            Formatter f = ViewKit.Indian();
            var d = new CalendarDate(1, Season.Spring, 2, 1, 14, 5, 0);
            Assert.That(f.Date(d, 4), Is.EqualTo("Spring · Day 2 of 4 · Year 1"));
            Assert.That(Formatter.Clock(d), Is.EqualTo("14:05"));
        }

        [Test]
        public void Region_PicksTheFormat()
        {
            Assert.That(Formatter.FormatForRegion("IN"), Is.EqualTo(NumberFormat.Indian));
            Assert.That(Formatter.FormatForRegion("in"), Is.EqualTo(NumberFormat.Indian));
            Assert.That(Formatter.FormatForRegion("US"), Is.EqualTo(NumberFormat.Western));
            Assert.That(Formatter.FormatForRegion(""), Is.EqualTo(NumberFormat.Western));
        }
    }
}
