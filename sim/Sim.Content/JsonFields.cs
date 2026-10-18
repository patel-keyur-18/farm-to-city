using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sim.Core.Maths;

namespace Sim.Content
{
    /// <summary>
    /// Hand-written JSON mapping helpers (ADR 0018 rule 5: no reflection-based deserialisation).
    /// Numbers are read as <c>decimal</c>, never <c>double</c> (spec 001 §3.1).
    /// </summary>
    internal static class JsonFields
    {
        /// <summary>The most decimal places a content number may have (spec 001 §3.1).</summary>
        public const int MaxDecimalPlaces = 9;

        public static JToken Parse(string json, string rootPath)
        {
            if (json == null) throw new ContentException(rootPath, "no JSON text");
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)))
                {
                    reader.FloatParseHandling = FloatParseHandling.Decimal;
                    reader.DateParseHandling = DateParseHandling.None;
                    JToken token = JToken.ReadFrom(reader);
                    while (reader.Read())
                        if (reader.TokenType != JsonToken.Comment)
                            throw new ContentException(rootPath, "unexpected content after the root value");
                    return token;
                }
            }
            catch (JsonReaderException e)
            {
                throw new ContentException(rootPath, "invalid JSON: " + e.Message);
            }
        }

        public static JObject Object(JToken token, string path)
        {
            if (!(token is JObject o)) throw new ContentException(path, "expected an object");
            return o;
        }

        /// <summary>Rejects any property not in <paramref name="allowed"/>, so typos fail loudly.</summary>
        public static void OnlyFields(JObject o, string path, params string[] allowed)
        {
            foreach (JProperty p in o.Properties())
            {
                if (System.Array.IndexOf(allowed, p.Name) < 0)
                    throw new ContentException(Join(path, p.Name), "unknown field");
            }
        }

        public static JToken Required(JObject o, string path, string name)
        {
            JToken t = o[name];
            if (t == null || t.Type == JTokenType.Null) throw new ContentException(Join(path, name), "required field is missing");
            return t;
        }

        public static int Int(JToken t, string path)
        {
            if (t.Type != JTokenType.Integer) throw new ContentException(path, "expected a whole number");
            long v = t.Value<long>();
            if (v < int.MinValue || v > int.MaxValue) throw new ContentException(path, "number out of range");
            return (int)v;
        }

        public static Fixed Number(JToken t, string path)
        {
            if (t.Type == JTokenType.Integer)
            {
                long v = t.Value<long>();
                if (v < int.MinValue || v > int.MaxValue) throw new ContentException(path, "number out of range");
                return Sim.Core.Maths.Fixed.FromInt(v);
            }
            if (t.Type != JTokenType.Float || !(((JValue)t).Value is decimal d))
                throw new ContentException(path, "expected a number");
            int scale = (decimal.GetBits(d)[3] >> 16) & 0xFF;
            if (scale > MaxDecimalPlaces)
                throw new ContentException(path, "more than " + MaxDecimalPlaces + " decimal places");
            try
            {
                return Sim.Core.Maths.Fixed.FromDecimal(d);
            }
            catch (System.OverflowException)
            {
                throw new ContentException(path, "number out of range");
            }
        }

        /// <summary>A coin amount with at most 2 decimal places, as money (1/100 coin). Never negative.</summary>
        public static long Money(JToken t, string path)
        {
            decimal d;
            if (t.Type == JTokenType.Integer) d = t.Value<long>();
            else if (t.Type == JTokenType.Float && ((JValue)t).Value is decimal v) d = v;
            else throw new ContentException(path, "expected a coin amount");
            decimal hundredths = d * 100;
            if (hundredths != decimal.Truncate(hundredths)) throw new ContentException(path, "more than 2 decimal places");
            if (hundredths < 0) throw new ContentException(path, "must not be negative (was " + d + ")");
            if (hundredths > long.MaxValue / 1000) throw new ContentException(path, "amount out of range");
            return (long)hundredths;
        }

        public static bool Bool(JToken t, string path)
        {
            if (t.Type != JTokenType.Boolean) throw new ContentException(path, "expected true or false");
            return t.Value<bool>();
        }

        public static string String(JToken t, string path)
        {
            if (t.Type != JTokenType.String) throw new ContentException(path, "expected a string");
            string s = t.Value<string>();
            if (string.IsNullOrEmpty(s)) throw new ContentException(path, "must not be empty");
            return s;
        }

        public static List<JToken> Array(JToken t, string path)
        {
            if (!(t is JArray a)) throw new ContentException(path, "expected an array");
            return new List<JToken>(a);
        }

        public static void Range(int v, int min, int max, string path)
        {
            if (v < min || v > max) throw new ContentException(path, "must be between " + min + " and " + max + " (was " + v + ")");
        }

        public static void Range(Fixed v, Fixed min, Fixed max, string path)
        {
            if (v < min || v > max) throw new ContentException(path, "must be between " + min + " and " + max + " (was " + v + ")");
        }

        public static string Join(string path, string name) { return path + "." + name; }

        public static string Index(string path, int i) { return path + "[" + i + "]"; }
    }
}
