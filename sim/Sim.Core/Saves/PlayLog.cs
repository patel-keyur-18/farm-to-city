using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Progress;

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

        /// <param name="tzMinutes">The device's UTC offset, so days are the player's calendar days (ADR 0047 §2).</param>
        /// <param name="fromNote">True when the app was opened by tapping a notification.</param>
        public void SessionStart(long wallUtc, long simTime, string build, string device, int tzMinutes, bool fromNote)
        {
            Write(wallUtc, simTime, "sessionStart", ",\"build\":" + Quote(build) + ",\"device\":" + Quote(device)
                + ",\"tz\":" + tzMinutes + ",\"fromNote\":" + (fromNote ? "true" : "false"));
        }

        public void SessionEnd(long wallUtc, long simTime, long secondsPlayed)
        {
            Write(wallUtc, simTime, "sessionEnd", ",\"seconds\":" + secondsPlayed);
        }

        public void Away(long wallUtc, long simTime, long requested, long applied, bool capped)
        {
            Write(wallUtc, simTime, "away", ",\"requested\":" + requested + ",\"applied\":" + applied + ",\"capped\":" + (capped ? "true" : "false"));
        }

        /// <summary>A milestone by name, once per town (the events below call it; the host may too).</summary>
        public void Milestone(long wallUtc, long simTime, string type)
        {
            if (!Enabled || _reached.Contains(type)) return;
            Write(wallUtc, simTime, type, null);
        }

        /// <summary>
        /// The first session's steps done so far, each once per town, then <c>firstSessionDone</c> when every step is done or
        /// the player skipped (ADR 0047 §2). Reads the state, so any path that completes a step is logged.
        /// </summary>
        public void Guidance(long wallUtc, long simTime, GuidanceState g, OnboardingDefs d)
        {
            if (!Enabled || g == null || d == null) return;
            for (int i = 0; i < g.Step && i < d.Steps.Count; i++)
                if (!_reached.Contains("step:" + d.Steps[i].Id)) Write(wallUtc, simTime, "step", ",\"id\":" + Quote(d.Steps[i].Id));
            if ((g.Skipped || g.Step >= d.Steps.Count) && !_reached.Contains("firstSessionDone"))
                Write(wallUtc, simTime, "firstSessionDone", ",\"skipped\":" + (g.Skipped ? "true" : "false"));
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
                    case LabourersHired _: Milestone(wallUtc, e.SimTime, "firstHire"); break;
                    case HandHired _:
                        Milestone(wallUtc, e.SimTime, "firstHire");
                        Milestone(wallUtc, e.SimTime, "firstHand");
                        break;
                    case LoanTaken _: Milestone(wallUtc, e.SimTime, "firstLoan"); break;
                    case SeasonClosed _: Milestone(wallUtc, e.SimTime, "firstSeasonClose"); break;
                    case TruckArrived _: Milestone(wallUtc, e.SimTime, "firstTruck"); break;
                    case OrdersPosted _: Milestone(wallUtc, e.SimTime, "firstOrders"); break;
                    case OrderDelivered _: Milestone(wallUtc, e.SimTime, "firstOrderDelivered"); break;
                    case OrderPaid _: Milestone(wallUtc, e.SimTime, "firstOrderPaid"); break;
                    case EraUnlocked u: Milestone(wallUtc, e.SimTime, u.EraId + "Unlocked"); break; // hamletUnlocked (spec 009 §3.6)
                    // The dairy (spec 012): the shed, the first animal, the first milk money, the first calf.
                    case ShedBuilt _: Milestone(wallUtc, e.SimTime, "shedBuilt"); break;
                    case AnimalBought _: Milestone(wallUtc, e.SimTime, "firstAnimal"); break;
                    case MilkCollected m when m.Amount > 0: Milestone(wallUtc, e.SimTime, "firstMilk"); break;
                    case CalfBorn _: Milestone(wallUtc, e.SimTime, "firstCalf"); break;
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
            else if (type == "step")
            {
                int id = line.IndexOf("\"id\":\"", at, System.StringComparison.Ordinal) + 6;
                if (id >= 6) _reached.Add("step:" + line.Substring(id, line.IndexOf('"', id) - id));
            }
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
