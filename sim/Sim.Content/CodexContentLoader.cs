using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        private static readonly string[] CodexNumberNames = { "coins", "count", "percent", "factor", "decimal1" };

        /// <summary>
        /// <c>codex.json</c> (spec 018 §3.3, §4): the groups (id, icon) and the cards (id, group, trigger kind with its threshold where it
        /// takes one, the numbers' formats). Each trigger kind is used once.
        /// </summary>
        public static CodexDefs LoadCodex(string json)
        {
            const string root = "codex";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "groups", "cards");
            var groups = new List<string>();
            var icons = new List<string>();
            List<JToken> g = NonEmptyArray(o, root, "groups");
            for (int i = 0; i < g.Count; i++)
            {
                string p = Index(root + ".groups", i);
                JObject x = Object(g[i], p);
                OnlyFields(x, p, "id", "icon");
                groups.Add(UniqueId(x, p, groups));
                icons.Add(String(Required(x, p, "icon"), Join(p, "icon")));
            }
            var cards = new List<CodexCardDef>();
            var ids = new List<string>();
            var kinds = new List<string>();
            List<JToken> c = NonEmptyArray(o, root, "cards");
            for (int i = 0; i < c.Count; i++)
            {
                string p = Index(root + ".cards", i);
                JObject x = Object(c[i], p);
                OnlyFields(x, p, "id", "group", "trigger", "threshold", "numbers");
                string id = UniqueId(x, p, ids);
                ids.Add(id);
                string group = String(Required(x, p, "group"), Join(p, "group"));
                int gi = groups.IndexOf(group);
                if (gi < 0) throw new ContentException(Join(p, "group"), "unknown group '" + group + "'");
                string kind = x["trigger"] == null ? id : String(x["trigger"], Join(p, "trigger"));
                if (System.Array.IndexOf(CodexDefs.TriggerKinds, kind) < 0) throw new ContentException(Join(p, "trigger"), "unknown trigger '" + kind + "'");
                if (kinds.Contains(kind)) throw new ContentException(Join(p, "trigger"), "trigger '" + kind + "' is already a card's");
                kinds.Add(kind);
                bool needs = System.Array.IndexOf(CodexDefs.WithThreshold, kind) >= 0;
                if (needs != (x["threshold"] != null)) throw new ContentException(Join(p, "threshold"), needs ? "this trigger needs a threshold" : "this trigger takes no threshold");
                Fixed threshold = needs ? Bounded(x, p, "threshold", "0", "100") : Fixed.Zero;
                var numbers = new List<CodexNumber>();
                List<JToken> n = NonEmptyArray(x, p, "numbers");
                for (int k = 0; k < n.Count; k++)
                {
                    string np = Index(Join(p, "numbers"), k), name = String(n[k], np);
                    int f = System.Array.IndexOf(CodexNumberNames, name);
                    if (f < 0) throw new ContentException(np, "unknown number format '" + name + "'");
                    numbers.Add((CodexNumber)f);
                }
                cards.Add(new CodexCardDef { Id = id, Group = gi, Trigger = kind, Threshold = threshold, Numbers = numbers.AsReadOnly() });
            }
            return new CodexDefs { Groups = groups.AsReadOnly(), GroupIcons = icons.AsReadOnly(), Cards = cards.AsReadOnly() };
        }
    }
}
