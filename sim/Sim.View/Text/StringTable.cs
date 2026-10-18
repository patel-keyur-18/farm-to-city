using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Sim.Content;

namespace Sim.View.Text
{
    /// <summary>
    /// A string table (spec 008 §3.11, ADR 0024): a flat object of dotted key → text, with named arguments in braces
    /// (<c>"Wheat can be sown in {seasons} only"</c>) and plural forms (<c>alert.ready.one</c> and <c>alert.ready.other</c>).
    /// Like a Spring <c>MessageSource</c>: every visible string comes from here, so Hindi is a translation job.
    /// </summary>
    public sealed class StringTable
    {
        private readonly Dictionary<string, string> _texts;

        /// <summary>Tests and development: a missing key throws. Players: it renders <c>⟦key⟧</c> so a gap is visible, not fatal.</summary>
        public bool ThrowOnMissing { get; set; }

        private StringTable(Dictionary<string, string> texts) { _texts = texts; }

        /// <summary>
        /// Loads and validates a table: duplicate keys, malformed arguments (an empty name, an unclosed brace, a character that
        /// isn't a letter or digit) and a plural without <c>.other</c> fail naming the key.
        /// </summary>
        public static StringTable Load(string json)
        {
            if (json == null) throw new ContentException("strings", "no JSON text");
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            using (var reader = new JsonTextReader(new StringReader(json)))
            {
                if (!reader.Read() || reader.TokenType != JsonToken.StartObject) throw new ContentException("strings", "expected an object of key → text");
                while (reader.Read() && reader.TokenType != JsonToken.EndObject)
                {
                    if (reader.TokenType != JsonToken.PropertyName) throw new ContentException("strings", "unexpected content");
                    string key = (string)reader.Value;
                    if (!reader.Read() || reader.TokenType != JsonToken.String) throw new ContentException(key, "expected a text");
                    if (texts.ContainsKey(key)) throw new ContentException(key, "duplicate key");
                    CheckArguments(key, (string)reader.Value);
                    texts.Add(key, (string)reader.Value);
                }
            }
            foreach (string key in texts.Keys)
                if (key.EndsWith(".one", StringComparison.Ordinal) && !texts.ContainsKey(key.Substring(0, key.Length - 4) + ".other"))
                    throw new ContentException(key, "a plural needs a '.other' form");
            return new StringTable(texts);
        }

        private static void CheckArguments(string key, string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '}') throw new ContentException(key, "unmatched '}'");
                if (text[i] != '{') continue;
                int close = text.IndexOf('}', i + 1);
                if (close < 0) throw new ContentException(key, "unclosed '{'");
                string name = text.Substring(i + 1, close - i - 1);
                if (name.Length == 0 || !char.IsLetter(name[0])) throw new ContentException(key, "unknown argument '{" + name + "}'");
                foreach (char c in name)
                    if (!char.IsLetterOrDigit(c)) throw new ContentException(key, "unknown argument '{" + name + "}'");
                i = close;
            }
        }

        public bool Has(string key) { return _texts.ContainsKey(key); }

        public IEnumerable<string> Keys { get { return _texts.Keys; } }

        public int Count { get { return _texts.Count; } }

        /// <summary>The text for a key with its arguments filled in (name, value pairs). A missing key or argument: see <see cref="ThrowOnMissing"/>.</summary>
        public string Get(string key, params string[] args)
        {
            if (!_texts.TryGetValue(key, out string text))
            {
                if (ThrowOnMissing) throw new KeyNotFoundException("Missing string '" + key + "'.");
                return "⟦" + key + "⟧";
            }
            if (text.IndexOf('{') < 0) return text;
            var sb = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '{') { sb.Append(text[i]); continue; }
                int close = text.IndexOf('}', i + 1);
                string name = text.Substring(i + 1, close - i - 1);
                string value = Find(args, name);
                if (value == null)
                {
                    if (ThrowOnMissing) throw new ArgumentException("String '" + key + "' needs the argument '" + name + "'.");
                    value = "{" + name + "}";
                }
                sb.Append(value);
                i = close;
            }
            return sb.ToString();
        }

        private static string Find(string[] args, string name)
        {
            for (int i = 0; i + 1 < args.Length; i += 2)
                if (args[i] == name) return args[i + 1];
            return null;
        }

        /// <summary>
        /// The plural form for a count: <c>key.one</c> when n is 1, else <c>key.other</c> (English rules). The count is available as
        /// <c>{n}</c> unless given.
        /// </summary>
        public string Plural(string key, long n, params string[] args)
        {
            string form = key + (n == 1 ? ".one" : ".other");
            var all = new List<string>(args);
            all.Add("n");
            all.Add(n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Get(form, all.ToArray());
        }
    }
}
