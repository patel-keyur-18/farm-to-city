using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sim.Core.Commands;
using Sim.Core.Time;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    /// <summary>A replay file: the commands of one session and, for golden tests, the expected final hash.</summary>
    public sealed class ReplayFile
    {
        public string Description { get; set; } = "";
        public bool DevCommandsEnabled { get; set; }
        public List<Command> Commands { get; } = new List<Command>();

        /// <summary>The expected <c>GameState.ComputeHash()</c> after replaying, or null.</summary>
        public ulong? ExpectedHash { get; set; }
    }

    /// <summary>
    /// Reads and writes replay files (spec 001 AC-13). Hand-written mapping per command type
    /// (ADR 0018 rule 5). Unknown command types fail with the field path.
    /// </summary>
    public static class ReplayJson
    {
        public const int Format = 1;

        public static ReplayFile Load(string json)
        {
            const string root = "replay";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "format", "description", "devCommandsEnabled", "commands", "expectedHash");
            int format = Int(Required(o, root, "format"), root + ".format");
            if (format != Format) throw new ContentException(root + ".format", "unsupported format " + format);

            var file = new ReplayFile();
            if (o["description"] != null) file.Description = String(o["description"], root + ".description");
            if (o["devCommandsEnabled"] != null) file.DevCommandsEnabled = Bool(o["devCommandsEnabled"], root + ".devCommandsEnabled");
            if (o["expectedHash"] != null)
            {
                string hex = String(o["expectedHash"], root + ".expectedHash");
                if (!hex.StartsWith("0x", StringComparison.Ordinal) ||
                    !ulong.TryParse(hex.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ulong hash))
                    throw new ContentException(root + ".expectedHash", "expected a hex string like 0x0123456789ABCDEF");
                file.ExpectedHash = hash;
            }

            List<JToken> commands = Array(Required(o, root, "commands"), root + ".commands");
            for (int i = 0; i < commands.Count; i++)
                file.Commands.Add(ReadCommand(commands[i], Index(root + ".commands", i)));
            return file;
        }

        public static string Write(ReplayFile file)
        {
            var sw = new StringWriter(CultureInfo.InvariantCulture);
            using (var w = new JsonTextWriter(sw) { Formatting = Formatting.Indented, Indentation = 2 })
            {
                w.WriteStartObject();
                w.WritePropertyName("format"); w.WriteValue(Format);
                w.WritePropertyName("description"); w.WriteValue(file.Description ?? "");
                w.WritePropertyName("devCommandsEnabled"); w.WriteValue(file.DevCommandsEnabled);
                w.WritePropertyName("commands");
                w.WriteStartArray();
                foreach (Command c in file.Commands) WriteCommand(w, c);
                w.WriteEndArray();
                if (file.ExpectedHash.HasValue)
                {
                    w.WritePropertyName("expectedHash");
                    w.WriteValue("0x" + file.ExpectedHash.Value.ToString("X16", CultureInfo.InvariantCulture));
                }
                w.WriteEndObject();
            }
            return sw.ToString().Replace("\r\n", "\n") + "\n";
        }

        private static Command ReadCommand(JToken token, string path)
        {
            JObject c = Object(token, path);
            string type = String(Required(c, path, "type"), path + ".type");
            switch (type)
            {
                case "NewGame":
                    OnlyFields(c, path, "type", "seed", "startWallUtc", "localSecondsSinceMidnight");
                    return new NewGame(ULong(Required(c, path, "seed"), path + ".seed"),
                        Long(Required(c, path, "startWallUtc"), path + ".startWallUtc"),
                        Long(Required(c, path, "localSecondsSinceMidnight"), path + ".localSecondsSinceMidnight"));
                case "AdvanceTo":
                    OnlyFields(c, path, "type", "target", "mode");
                    return new AdvanceTo(Long(Required(c, path, "target"), path + ".target"),
                        Mode(Required(c, path, "mode"), path + ".mode"));
                case "Sync":
                    OnlyFields(c, path, "type", "nowWallUtc");
                    return new Sync(Long(Required(c, path, "nowWallUtc"), path + ".nowWallUtc"));
                case "DebugAdvance":
                    OnlyFields(c, path, "type", "seconds", "mode");
                    return new DebugAdvance(Long(Required(c, path, "seconds"), path + ".seconds"),
                        Mode(Required(c, path, "mode"), path + ".mode"));
                default:
                    throw new ContentException(path + ".type", "unknown command type '" + type + "'");
            }
        }

        private static void WriteCommand(JsonTextWriter w, Command command)
        {
            w.WriteStartObject();
            switch (command)
            {
                case NewGame n:
                    Prop(w, "type", "NewGame");
                    w.WritePropertyName("seed"); w.WriteValue(n.Seed);
                    w.WritePropertyName("startWallUtc"); w.WriteValue(n.StartWallUtc);
                    w.WritePropertyName("localSecondsSinceMidnight"); w.WriteValue(n.LocalSecondsSinceMidnight);
                    break;
                case AdvanceTo a:
                    Prop(w, "type", "AdvanceTo");
                    w.WritePropertyName("target"); w.WriteValue(a.Target);
                    Prop(w, "mode", ModeName(a.Mode));
                    break;
                case Sync sync:
                    Prop(w, "type", "Sync");
                    w.WritePropertyName("nowWallUtc"); w.WriteValue(sync.NowWallUtc);
                    break;
                case DebugAdvance d:
                    Prop(w, "type", "DebugAdvance");
                    w.WritePropertyName("seconds"); w.WriteValue(d.Seconds);
                    Prop(w, "mode", ModeName(d.Mode));
                    break;
                default:
                    throw new ArgumentException("Command type " + command.GetType().Name + " can't be written to a replay file.");
            }
            w.WriteEndObject();
        }

        private static void Prop(JsonTextWriter w, string name, string value)
        {
            w.WritePropertyName(name);
            w.WriteValue(value);
        }

        private static long Long(JToken t, string path)
        {
            if (t.Type != JTokenType.Integer || ((JValue)t).Value is BigInteger) throw new ContentException(path, "expected a whole number");
            return t.Value<long>();
        }

        private static ulong ULong(JToken t, string path)
        {
            if (t.Type != JTokenType.Integer) throw new ContentException(path, "expected a whole number");
            object v = ((JValue)t).Value;
            if (v is BigInteger big)
            {
                if (big < 0 || big > ulong.MaxValue) throw new ContentException(path, "number out of range");
                return (ulong)big;
            }
            long l = t.Value<long>();
            if (l < 0) throw new ContentException(path, "must not be negative");
            return (ulong)l;
        }

        private static AdvanceMode Mode(JToken t, string path)
        {
            string s = String(t, path);
            if (s == "live") return AdvanceMode.Live;
            if (s == "away") return AdvanceMode.Away;
            throw new ContentException(path, "expected live or away");
        }

        private static string ModeName(AdvanceMode m) { return m == AdvanceMode.Away ? "away" : "live"; }
    }
}
