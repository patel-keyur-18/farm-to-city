using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sim.Core.Definitions;

namespace Sim.Core.Saves
{
    /// <summary>
    /// A notification as it will be shown, with the town it is from (spec 031 §3.1). The words are made when the note is planned, from that town's
    /// own view, so the town needn't be loaded when they go out.
    /// </summary>
    public sealed class KeptNote
    {
        public long WallUtc { get; }
        public NotifyKind Kind { get; }
        public string Title { get; }
        public string Body { get; }

        /// <summary>The slot of the town it is about (1 or 2).</summary>
        public int Town { get; }

        public KeptNote(long wallUtc, NotifyKind kind, string title, string body, int town)
        {
            WallUtc = wallUtc;
            Kind = kind;
            Title = title;
            Body = body;
            Town = town;
        }

        public NotifyImportance Importance { get { return NotificationPlanner.ImportanceOf(Kind); } }
    }

    /// <summary>
    /// The other town's notifications, kept in <c>notes.json</c> beside its save from the moment you left it (spec 031 §3.1), and the merge with the open
    /// town's at each backgrounding. Nothing changes the town you are not in, so the kept plan stays right until you open it.
    /// </summary>
    public static class KeptNotes
    {
        public const string FileName = "notes.json";

        /// <summary>One JSON object a line inside an array, so the file is valid JSON and a damaged line costs one note.</summary>
        public static string ToJson(IReadOnlyList<KeptNote> notes)
        {
            var sb = new StringBuilder("[\n");
            for (int i = 0; i < notes.Count; i++)
            {
                KeptNote n = notes[i];
                sb.Append("{\"t\":").Append(n.WallUtc.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"k\":").Append(((int)n.Kind).ToString(CultureInfo.InvariantCulture))
                  .Append(",\"town\":").Append(n.Town.ToString(CultureInfo.InvariantCulture))
                  .Append(",\"title\":").Append(Quote(n.Title)).Append(",\"body\":").Append(Quote(n.Body)).Append('}')
                  .Append(i + 1 < notes.Count ? ",\n" : "\n");
            }
            return sb.Append("]\n").ToString();
        }

        /// <summary>Reads what <see cref="ToJson"/> wrote; a line that isn't a note is skipped.</summary>
        public static List<KeptNote> Parse(string json)
        {
            var list = new List<KeptNote>();
            if (string.IsNullOrEmpty(json)) return list;
            foreach (string raw in json.Split('\n'))
            {
                string line = raw.Trim().TrimEnd(',');
                if (!line.StartsWith("{", StringComparison.Ordinal) || !Number(line, "t", out long t) || !Number(line, "k", out long k) || !Number(line, "town", out long town)) continue;
                string title = Text(line, "title"), body = Text(line, "body");
                if (title == null || body == null || !Enum.IsDefined(typeof(NotifyKind), (int)k)) continue;
                list.Add(new KeptNote(t, (NotifyKind)(int)k, title, body, (int)town));
            }
            return list;
        }

        /// <summary>
        /// What to schedule when the app goes to the background (spec 031 §3.1, owner 2026-11-27): the open town's notes and the other town's, over the
        /// platform's pending limit the most important first (urgent, needs you, information; soonest first within each), scheduled in time order.
        /// The other town's notes that came due while you played this one go out once, a minute from now and after quiet hours, if they are urgent or need
        /// you; information ones are dropped. <paramref name="stillKept"/> is what the other town keeps: its future notes (the ones sent or dropped leave).
        /// </summary>
        public static List<KeptNote> Merge(IReadOnlyList<KeptNote> open, IReadOnlyList<KeptNote> other, long nowWallUtc, NotifyDef def, int utcOffsetMinutes, out List<KeptNote> stillKept)
        {
            stillKept = new List<KeptNote>();
            var all = new List<KeptNote>(open);
            long late = NotificationPlanner.OutOfQuiet(nowWallUtc + 60, def, utcOffsetMinutes);
            foreach (KeptNote n in other)
            {
                if (n.WallUtc > nowWallUtc) { stillKept.Add(n); all.Add(n); }
                else if (n.Importance != NotifyImportance.Information) all.Add(new KeptNote(late, n.Kind, n.Title, n.Body, n.Town));
            }
            all.Sort((a, b) => a.Importance != b.Importance ? a.Importance.CompareTo(b.Importance) : a.WallUtc != b.WallUtc ? a.WallUtc.CompareTo(b.WallUtc) : a.Kind.CompareTo(b.Kind));
            if (all.Count > def.MaxPending) all.RemoveRange(def.MaxPending, all.Count - def.MaxPending);
            all.Sort((a, b) => a.WallUtc != b.WallUtc ? a.WallUtc.CompareTo(b.WallUtc) : a.Town != b.Town ? a.Town.CompareTo(b.Town) : a.Kind.CompareTo(b.Kind));
            return all;
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        private static bool Number(string line, string key, out long value)
        {
            value = 0;
            int at = line.IndexOf("\"" + key + "\":", StringComparison.Ordinal);
            if (at < 0) return false;
            int i = at + key.Length + 3, j = i;
            while (j < line.Length && (line[j] == '-' || char.IsDigit(line[j]))) j++;
            return j > i && long.TryParse(line.Substring(i, j - i), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string Text(string line, string key)
        {
            int at = line.IndexOf("\"" + key + "\":\"", StringComparison.Ordinal);
            if (at < 0) return null;
            var sb = new StringBuilder();
            for (int i = at + key.Length + 4; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') return sb.ToString();
                if (c == '\\' && i + 1 < line.Length)
                {
                    char e = line[++i];
                    sb.Append(e == 'n' ? '\n' : e == 'r' ? '\r' : e);
                }
                else sb.Append(c);
            }
            return null;
        }
    }
}
