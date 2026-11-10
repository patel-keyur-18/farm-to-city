using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>
        /// <c>advisors.json</c> (spec 018 §3.4, ADR 0095 §8): the advisors (id, portrait) and their notes in priority order (id, advisor,
        /// the season report's line and/or the alerts it attaches to, a threshold where its condition takes one). Each note id is known
        /// and used once; a note speaks somewhere.
        /// </summary>
        public static AdvisorDefs LoadAdvisors(string json)
        {
            const string root = "advisors";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "topicHours", "advisors", "notes");
            int hours = Int(Required(o, root, "topicHours"), Join(root, "topicHours"));
            if (hours < 1 || hours > 96) throw new ContentException(Join(root, "topicHours"), "must be 1-96");
            var advisors = new List<string>();
            var portraits = new List<string>();
            List<JToken> a = NonEmptyArray(o, root, "advisors");
            for (int i = 0; i < a.Count; i++)
            {
                string p = Index(root + ".advisors", i);
                JObject x = Object(a[i], p);
                OnlyFields(x, p, "id", "portrait");
                advisors.Add(UniqueId(x, p, advisors));
                portraits.Add(String(Required(x, p, "portrait"), Join(p, "portrait")));
            }
            var notes = new List<AdvisorNoteDef>();
            var ids = new List<string>();
            List<JToken> n = NonEmptyArray(o, root, "notes");
            for (int i = 0; i < n.Count; i++)
            {
                string p = Index(root + ".notes", i);
                JObject x = Object(n[i], p);
                OnlyFields(x, p, "id", "advisor", "report", "alerts", "threshold");
                string id = UniqueId(x, p, ids);
                ids.Add(id);
                if (System.Array.IndexOf(AdvisorDefs.NoteIds, id) < 0) throw new ContentException(Join(p, "id"), "no condition for note '" + id + "'");
                string who = String(Required(x, p, "advisor"), Join(p, "advisor"));
                int ai = advisors.IndexOf(who);
                if (ai < 0) throw new ContentException(Join(p, "advisor"), "unknown advisor '" + who + "'");
                bool report = x["report"] != null && Bool(x["report"], Join(p, "report"));
                var alerts = new List<string>();
                if (x["alerts"] != null)
                {
                    List<JToken> l = NonEmptyArray(x, p, "alerts");
                    for (int k = 0; k < l.Count; k++)
                    {
                        string ap = Index(Join(p, "alerts"), k), kind = String(l[k], ap);
                        if (System.Array.IndexOf(AdvisorDefs.AlertKinds, kind) < 0) throw new ContentException(ap, "unknown alert '" + kind + "'");
                        alerts.Add(kind);
                    }
                }
                if (!report && alerts.Count == 0) throw new ContentException(p, "a note speaks on the season report, on alerts or both");
                bool needs = System.Array.IndexOf(AdvisorDefs.WithThreshold, id) >= 0;
                if (needs != (x["threshold"] != null)) throw new ContentException(Join(p, "threshold"), needs ? "this note needs a threshold" : "this note takes no threshold");
                Fixed threshold = needs ? Bounded(x, p, "threshold", "0", "100") : Fixed.Zero;
                notes.Add(new AdvisorNoteDef { Id = id, Advisor = ai, Report = report, Alerts = alerts.AsReadOnly(), Threshold = threshold });
            }
            return new AdvisorDefs { Advisors = advisors.AsReadOnly(), Portraits = portraits.AsReadOnly(), Notes = notes.AsReadOnly(), TopicHours = hours };
        }
    }
}
