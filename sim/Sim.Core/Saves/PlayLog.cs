using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sim.Core.Definitions;
using Sim.Core.Events;

namespace Sim.Core.Saves
{
    /// <summary>
    /// The opt-in local play log (spec 007 §3.6, ADR 0024): one JSON object per line, sessions, away time and the Phase 1
    /// milestones, each milestone once per town. It stays on the phone; the host writes <see cref="Text"/> to
    /// <c>playlog.jsonl</c> and exports it on request. The sim never knows about it: milestones come from its events.
    /// </summary>
    public sealed class PlayLog
    {
        private readonly PlayLogDef _def;
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _reached = new List<string>();
        private int _bytes;

        /// <summary>Off by default; nothing is written while off.</summary>
        public bool Enabled { get; set; }

        /// <param name="existing">The file's text so far (milestones already logged for this town aren't logged again).</param>
        public PlayLog(PlayLogDef def, string existing)
        {
            _def = def;
            foreach (string line in (existing ?? "").Split('\n'))
                if (line.Length > 0) Append(line);
        }

        public IReadOnlyList<string> Lines { get { return _lines; } }

        public string Text
        {
            get
            {
                var sb = new StringBuilder(_bytes);
                foreach (string l in _lines) sb.Append(l).Append('\n');
                return sb.ToString();
            }
        }

        public void NewGame(long wallUtc, long simTime) { Write(wallUtc, simTime, "newGame", null); }

        public void SessionStart(long wallUtc, long simTime, string build, string device)
        {
            Write(wallUtc, simTime, "sessionStart", ",\"build\":" + Quote(build) + ",\"device\":" + Quote(device));
        }

        public void SessionEnd(long wallUtc, long simTime, long secondsPlayed)
        {
            Write(wallUtc, simTime, "sessionEnd", ",\"seconds\":" + secondsPlayed);
        }

        public void Away(long wallUtc, long simTime, long requested, long applied, bool capped)
        {
            Write(wallUtc, simTime, "away", ",\"requested\":" + requested + ",\"applied\":" + applied + ",\"capped\":" + (capped ? "true" : "false"));
        }

        /// <summary>A milestone the sim's events don't show (the Hamlet unlock, spec 009), once per town.</summary>
        public void Milestone(long wallUtc, long simTime, string type)
        {
            if (!Enabled || _reached.Contains(type)) return;
            Write(wallUtc, simTime, type, null);
        }

        /// <summary>Logs the Phase 1 milestones these events reach for the first time in this town.</summary>
        public void OnEvents(IEnumerable<SimEvent> events, long wallUtc)
        {
            if (!Enabled) return;
            foreach (SimEvent e in events)
            {
                switch (e)
                {
                    case CropHarvested _: Milestone(wallUtc, e.SimTime, "firstHarvest"); break;
                    case GardenPicked _: Milestone(wallUtc, e.SimTime, "firstGardenPick"); break;
                    case SoldToTrader _: case MarketSales _: case MandiTripReturned _: Milestone(wallUtc, e.SimTime, "firstSale"); break;
                    case DayLabourersHired _: Milestone(wallUtc, e.SimTime, "firstHire"); break;
                    case HandHired _:
                        Milestone(wallUtc, e.SimTime, "firstHire");
                        Milestone(wallUtc, e.SimTime, "firstHand");
                        break;
                    case LoanTaken _: Milestone(wallUtc, e.SimTime, "firstLoan"); break;
                    case SeasonClosed _: Milestone(wallUtc, e.SimTime, "firstSeasonClose"); break;
                }
            }
        }

        private void Write(long wallUtc, long simTime, string type, string rest)
        {
            if (!Enabled) return;
            Append("{\"t\":" + wallUtc + ",\"sim\":" + simTime + ",\"type\":\"" + type + "\"" + rest + "}");
            while (_bytes > _def.MaxBytes && _lines.Count > 1)
            {
                _bytes -= Encoding.UTF8.GetByteCount(_lines[0]) + 1;
                _lines.RemoveAt(0);
            }
        }

        private void Append(string line)
        {
            _lines.Add(line);
            _bytes += Encoding.UTF8.GetByteCount(line) + 1;
            int at = line.IndexOf("\"type\":\"", System.StringComparison.Ordinal);
            if (at < 0) return;
            string type = line.Substring(at + 8, line.IndexOf('"', at + 8) - at - 8);
            if (type == "newGame") _reached.Clear();
            else if (type.StartsWith("first", System.StringComparison.Ordinal) || type == "hamletUnlocked") _reached.Add(type);
        }

        private static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
