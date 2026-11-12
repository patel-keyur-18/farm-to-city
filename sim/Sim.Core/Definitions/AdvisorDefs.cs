using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// An advisor's note (spec 018 §3.4, ADR 0095 §8): who says it, whether it can be the season report's line, the alerts it attaches to
    /// (event names), and its threshold where its condition takes one. Its id is its topic and names its condition, which the view layer
    /// knows; its words are in the string table (<c>advisor.note.&lt;id&gt;</c>). The notes' order is their priority.
    /// </summary>
    public sealed class AdvisorNoteDef
    {
        public string Id { get; internal set; }

        /// <summary>Index into <see cref="AdvisorDefs.Advisors"/>.</summary>
        public int Advisor { get; internal set; }

        public bool Report { get; internal set; }
        public IReadOnlyList<string> Alerts { get; internal set; }
        public Fixed Threshold { get; internal set; }
    }

    /// <summary><c>advisors.json</c>: the advisors (id, portrait), their notes in priority order, and how long before a topic speaks again.</summary>
    public sealed class AdvisorDefs
    {
        /// <summary>The notes the view layer has a condition for (ADR 0095 §8's table).</summary>
        public static readonly string[] NoteIds =
        {
            "missedPayment", "debt", "overdraft", "profitNotCash", "pests", "feed", "soil", "dry", "raise", "tired", "children", "adultClass",
        };

        /// <summary>Notes whose condition takes a threshold.</summary>
        public static readonly string[] WithThreshold = { "overdraft", "soil", "dry" };

        /// <summary>The alerts a note can attach to (spec 018 §3.4).</summary>
        public static readonly string[] AlertKinds = { "LoanPaymentMissed", "FinalNotice", "WagesMissed", "PestWarning", "FeedLow", "HrEventRaised" };

        public IReadOnlyList<string> Advisors { get; internal set; }
        public IReadOnlyList<string> Portraits { get; internal set; }
        public IReadOnlyList<AdvisorNoteDef> Notes { get; internal set; }

        /// <summary>A topic speaks at most once in this many hours (a game day).</summary>
        public int TopicHours { get; internal set; }

        public int IndexOf(string id)
        {
            for (int i = 0; i < Notes.Count; i++) if (Notes[i].Id == id) return i;
            return -1;
        }
    }
}
