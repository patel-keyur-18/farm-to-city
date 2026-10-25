using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.Time;

namespace Sim.View.Text
{
    /// <summary>Indian grouping with lakh and crore (1,20,000), or Western grouping (120,000) (ADR 0024).</summary>
    public enum NumberFormat
    {
        Indian = 0,
        Western = 1,
    }

    /// <summary>
    /// The locale formatter (spec 008 §3.11): every number the player reads goes through here. Money is whole coins on screen
    /// (rounded half away from zero), quantities are in kg, durations show the two largest units. Unit words come from the
    /// string table so they translate.
    /// </summary>
    public sealed class Formatter
    {
        public const char Minus = '−'; // a true minus sign

        private readonly StringTable _strings;

        public NumberFormat Format { get; }

        public Formatter(StringTable strings, NumberFormat format)
        {
            _strings = strings;
            Format = format;
        }

        /// <summary>The device's region picks the format: India → Indian, elsewhere Western (spec 008 §3.11).</summary>
        public static NumberFormat FormatForRegion(string regionName)
        {
            return string.Equals(regionName, "IN", StringComparison.OrdinalIgnoreCase) ? NumberFormat.Indian : NumberFormat.Western;
        }

        // ---- integers ----

        /// <summary>12,34,567 (Indian) or 1,234,567 (Western); a negative number has a true minus.</summary>
        public string Integer(long n)
        {
            string digits = (n < 0 ? -(BigInteger)n : n).ToString(CultureInfo.InvariantCulture);
            return (n < 0 ? Minus.ToString() : "") + Group(digits);
        }

        private string Group(string digits)
        {
            if (digits.Length <= 3) return digits;
            var sb = new StringBuilder();
            if (Format == NumberFormat.Western)
            {
                int first = digits.Length % 3;
                if (first > 0) sb.Append(digits, 0, first);
                for (int i = first; i < digits.Length; i += 3)
                {
                    if (sb.Length > 0) sb.Append(',');
                    sb.Append(digits, i, 3);
                }
                return sb.ToString();
            }
            // Indian: the last three digits, then pairs.
            string last3 = digits.Substring(digits.Length - 3), rest = digits.Substring(0, digits.Length - 3);
            int head = rest.Length % 2;
            if (head > 0) sb.Append(rest, 0, head);
            for (int i = head; i < rest.Length; i += 2)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(rest, i, 2);
            }
            return sb.Append(',').Append(last3).ToString();
        }

        // ---- money ----

        /// <summary>Whole coins from money (1/100 coin), rounded half away from zero.</summary>
        public static long WholeCoins(long money) { return Money.RoundToCoin(money) / Money.Coin; }

        /// <summary>1,20,000 (no unit word), for tables and ledgers.</summary>
        public string Amount(long money) { return Integer(WholeCoins(money)); }

        /// <summary>1,20,000 coins (the unit word, singular for one).</summary>
        public string Coins(long money)
        {
            long coins = WholeCoins(money);
            return _strings.Plural("unit.coins", Math.Abs(coins), "amount", Integer(coins));
        }

        /// <summary>The ledger's detail rows: 1,20,000.40 (two decimals, paise).</summary>
        public string Paise(long money)
        {
            BigInteger abs = money < 0 ? -(BigInteger)money : money;
            string whole = Group((abs / Money.Coin).ToString(CultureInfo.InvariantCulture));
            string frac = ((int)(abs % Money.Coin)).ToString("00", CultureInfo.InvariantCulture);
            return (money < 0 ? Minus.ToString() : "") + whole + "." + frac;
        }

        /// <summary>
        /// The HUD's short money: from 1 lakh (Indian) or 1 million (Western) as 1.2 L, 3.45 Cr or 12.34M, up to 2 decimals with
        /// trailing zeros trimmed, rounded down so the HUD never overstates cash. Below that, the full number.
        /// </summary>
        public string ShortCoins(long money)
        {
            long coins = WholeCoins(money);
            long abs = Math.Abs(coins);
            string sign = coins < 0 ? Minus.ToString() : "";
            if (Format == NumberFormat.Indian)
            {
                if (abs >= 10_000_000) return sign + _strings.Get("unit.short.crore", "amount", Scaled(abs, 10_000_000));
                if (abs >= 100_000) return sign + _strings.Get("unit.short.lakh", "amount", Scaled(abs, 100_000));
            }
            else if (abs >= 1_000_000) return sign + _strings.Get("unit.short.million", "amount", Scaled(abs, 1_000_000));
            return Integer(coins);
        }

        private static string Scaled(long value, long unit)
        {
            long hundredths = value * 100 / unit; // rounded down
            string s = (hundredths / 100).ToString(CultureInfo.InvariantCulture);
            long frac = hundredths % 100;
            if (frac == 0) return s;
            return s + "." + (frac % 10 == 0 ? (frac / 10).ToString(CultureInfo.InvariantCulture) : frac.ToString("00", CultureInfo.InvariantCulture));
        }

        // ---- quantities ----

        /// <summary>1,850 kg, 70.7 kg, 0.4 kg: whole kg from 100 kg, one decimal below (grams in).</summary>
        public string Kg(long grams) { return Mass(grams, "unit.kg"); }

        /// <summary>A litre good in millilitres: 1.5 L.</summary>
        public string Litres(long ml) { return Mass(ml, "unit.litre"); }

        /// <summary>A good's quantity in its own unit: kg, litres or a count.</summary>
        public string Quantity(Sim.Core.Definitions.GoodDef good, long q)
        {
            if (good.Unit == Sim.Core.Definitions.GoodUnit.Each) return Integer(q);
            return good.Unit == Sim.Core.Definitions.GoodUnit.Litre ? Litres(q) : Kg(q);
        }

        private string Mass(long grams, string key)
        {
            BigInteger abs = grams < 0 ? -(BigInteger)grams : grams;
            string sign = grams < 0 ? Minus.ToString() : "";
            string amount;
            if (abs >= 100_000) amount = Group(((abs + 500) / 1000).ToString(CultureInfo.InvariantCulture));
            else
            {
                BigInteger tenths = (abs + 50) / 100;
                amount = (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture);
            }
            return _strings.Get(key, "amount", sign + amount);
        }

        /// <summary>5.30 /kg: two decimals.</summary>
        public string PricePerKg(Fixed price) { return _strings.Get("unit.perKg", "amount", Decimals(price, 2, false)); }

        /// <summary>62%, 3.5%: whole from 10%, one decimal below (a fraction in, 0.0625 → 6.3%).</summary>
        /// <summary>One decimal, for hours a day ("3.4").</summary>
        public string Decimal1(Fixed v) { return Decimals(v, 1, false); }

        public string Percent(Fixed fraction)
        {
            Fixed pct = fraction * Fixed.FromInt(100);
            bool whole = pct.Raw == 0 || Math.Abs(pct.RoundToLong()) >= 10 && AbsGreaterOrEqual(pct, 10); // nothing is "0%", not "0.0%"
            return Decimals(pct, whole ? 0 : 1, false) + "%";
        }

        private static bool AbsGreaterOrEqual(Fixed v, long n) { return (v.Raw < 0 ? -(BigInteger)v.Raw : v.Raw) >= (BigInteger)n << Fixed.FractionBits; }

        /// <summary>0.875, 1.00, 1.10: three decimals, a zero third decimal trimmed to two.</summary>
        public string Factor(Fixed f)
        {
            string three = Decimals(f, 3, false);
            return three.EndsWith("0", StringComparison.Ordinal) ? three.Substring(0, three.Length - 1) : three;
        }

        private static string Decimals(Fixed v, int places, bool group)
        {
            BigInteger scale = BigInteger.Pow(10, places);
            BigInteger raw = v.Raw;
            bool negative = raw < 0;
            BigInteger abs = negative ? -raw : raw;
            BigInteger one = BigInteger.One << Fixed.FractionBits;
            BigInteger scaled = (abs * scale * 2 + one) / (one * 2); // rounded half up on the magnitude: ties away from zero
            string whole = (scaled / scale).ToString(CultureInfo.InvariantCulture);
            string s = places == 0 ? whole : whole + "." + (scaled % scale).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0');
            return (negative && scaled != 0 ? Minus.ToString() : "") + s;
        }

        // ---- time ----

        /// <summary>3 h 12 min, 2 d 4 h, 45 s: the two largest units (a day is 24 hours).</summary>
        public string Duration(long seconds)
        {
            long s = Math.Abs(seconds);
            string sign = seconds < 0 ? Minus.ToString() : "";
            long d = s / 86400, h = s % 86400 / 3600, m = s % 3600 / 60, sec = s % 60;
            string Unit(string key, long n) { return _strings.Get(key, "n", n.ToString(CultureInfo.InvariantCulture)); }
            if (d > 0) return sign + Unit("unit.d", d) + (h > 0 ? " " + Unit("unit.h", h) : "");
            if (h > 0) return sign + Unit("unit.h", h) + (m > 0 ? " " + Unit("unit.min", m) : "");
            if (m > 0) return sign + Unit("unit.min", m) + (sec > 0 ? " " + Unit("unit.s", sec) : "");
            return sign + Unit("unit.s", sec);
        }

        /// <summary>14:05.</summary>
        public static string Clock(CalendarDate d)
        {
            return d.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + d.Minute.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>Spring · Day 2 of 4 · Year 1.</summary>
        public string Date(CalendarDate d, int daysPerSeason)
        {
            return _strings.Get("date.full", "season", _strings.Get("season." + d.Season.ToString().ToLowerInvariant()),
                "day", d.DayOfSeason.ToString(CultureInfo.InvariantCulture), "days", daysPerSeason.ToString(CultureInfo.InvariantCulture),
                "year", d.Year.ToString(CultureInfo.InvariantCulture));
        }
    }
}
