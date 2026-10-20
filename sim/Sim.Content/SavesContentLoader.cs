using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>Loads <c>saves.json</c> and checks the bounds in spec 007 §4.</summary>
        public static SavesDefs LoadSaves(string json)
        {
            const string root = "saves";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "save", "notify", "playLog");

            string sp = root + ".save";
            JObject s = Object(Required(o, root, "save"), sp);
            OnlyFields(s, sp, "minSchema", "hourlyBackups", "dailyBackups", "autosaveMinutes", "commandDebounceSeconds", "maxLogEntries");
            int Get(JObject x, string path, string f, int min, int max) { int v = Int(Required(x, path, f), Join(path, f)); Range(v, min, max, Join(path, f)); return v; }
            var save = new SaveDef(Get(s, sp, "minSchema", 1, int.MaxValue), Get(s, sp, "hourlyBackups", 0, 10), Get(s, sp, "dailyBackups", 0, 10),
                Get(s, sp, "autosaveMinutes", 5, 240), Get(s, sp, "commandDebounceSeconds", 1, 60), Get(s, sp, "maxLogEntries", 1_000, 1_000_000));

            string np = root + ".notify";
            JObject n = Object(Required(o, root, "notify"), np);
            OnlyFields(n, np, "maxPending", "groupWindowMinutes", "infoGroupMinutes", "quietFrom", "quietTo");
            var notify = new NotifyDef(Get(n, np, "maxPending", 1, 64), Get(n, np, "groupWindowMinutes", 0, 240), Get(n, np, "infoGroupMinutes", 0, 1_440),
                Minute(n, np, "quietFrom"), Minute(n, np, "quietTo"));

            string lp = root + ".playLog";
            JObject l = Object(Required(o, root, "playLog"), lp);
            OnlyFields(l, lp, "maxBytes");
            return new SavesDefs(save, notify, new PlayLogDef(Get(l, lp, "maxBytes", 10_000, int.MaxValue)));
        }

        /// <summary>"hh:mm" as minutes after midnight.</summary>
        private static int Minute(JObject o, string path, string field)
        {
            string p = Join(path, field);
            string v = String(Required(o, path, field), p);
            string[] parts = v.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m) || h < 0 || h > 23 || m < 0 || m > 59 || parts[1].Length != 2)
                throw new ContentException(p, "expected a time like 22:00, was '" + v + "'");
            return h * 60 + m;
        }
    }
}
