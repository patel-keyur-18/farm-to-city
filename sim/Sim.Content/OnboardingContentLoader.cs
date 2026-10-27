using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary>The events a goal or trigger can name (the view layer matches each, spec 009 §3.2–3.3).</summary>
        public static readonly string[] OnboardingEvents =
        {
            "GardenPicked", "SoldToTrader", "MarketSales", "MandiTripReturned", "AwayAdvanceCompleted", "CropHarvested",
            "PestWarning", "SeasonClosed", "FounderSick", "EraUnlocked(hamlet)", "OrdersPosted",
        };

        /// <summary>The commands a <c>rejection</c> trigger can name.</summary>
        public static readonly string[] OnboardingCommands = { "Sow", "BuildHandHut", "HireLabourers", "OfferContract", "BuyParcel", "SellToTrader", "Harvest", "PickGarden", "PickFruit" };

        private static readonly string[] GoalKinds = { "chapterClosed", "bedGrowing", "sheetVisited", "event", "cropSown" };
        private static readonly string[] TriggerKinds = { "step", "event", "storeAbovePercent", "traderFull", "seasonStart", "careBelowForHours", "rejection", "cashBelowHutCost", "awayCard", "storeTruckAway", "cattleAcre" };

        /// <summary>Loads <c>onboarding.json</c> and <c>eras.json</c> (spec 009 §4, AC-1) and checks every kind, id and reference.</summary>
        public static OnboardingDefs LoadOnboarding(string onboardingJson, string erasJson)
        {
            const string root = "onboarding";
            JObject o = Object(Parse(onboardingJson, root), root);
            OnlyFields(o, root, "steps", "chapters");

            // Chapters first: steps name them.
            List<JToken> chapterTokens = NonEmptyArray(o, root, "chapters");
            var ids = new List<string>();
            foreach (JToken t in chapterTokens)
            {
                string p = Index(root + ".chapters", ids.Count);
                ids.Add(UniqueId(Object(t, p), p, ids));
            }
            if (ids.Count > 64) throw new ContentException(root + ".chapters", "at most 64 chapters");
            var chapters = new List<ChapterDef>();
            for (int i = 0; i < chapterTokens.Count; i++)
            {
                string p = Index(root + ".chapters", i);
                JObject c = (JObject)chapterTokens[i];
                OnlyFields(c, p, "id", "triggers", "action");
                var triggers = new List<ConditionDef>();
                List<JToken> tt = NonEmptyArray(c, p, "triggers");
                for (int k = 0; k < tt.Count; k++) triggers.Add(Condition(Object(tt[k], Index(p + ".triggers", k)), Index(p + ".triggers", k), TriggerKinds, ids));
                chapters.Add(new ChapterDef(ids[i], triggers.AsReadOnly(), OptionalString(c, p, "action")));
            }

            var steps = new List<StepDef>();
            var stepIds = new List<string>();
            List<JToken> stepTokens = NonEmptyArray(o, root, "steps");
            for (int i = 0; i < stepTokens.Count; i++)
            {
                string p = Index(root + ".steps", i);
                JObject s = Object(stepTokens[i], p);
                OnlyFields(s, p, "id", "goal", "highlight", "chapter", "tour", "pageAfter");
                string id = UniqueId(s, p, stepIds);
                stepIds.Add(id);
                string chapterId = String(Required(s, p, "chapter"), Join(p, "chapter"));
                int chapter = ids.IndexOf(chapterId);
                if (chapter < 0) throw new ContentException(Join(p, "chapter"), "unknown chapter '" + chapterId + "'");
                if (chapters[chapter].Triggers[0].Trigger != TriggerKind.Step)
                    throw new ContentException(Join(p, "chapter"), "a step's chapter must have the trigger 'step'");
                ConditionDef goal = Condition(Object(Required(s, p, "goal"), Join(p, "goal")), Join(p, "goal"), GoalKinds, ids);
                var tour = new List<string>();
                if (s["tour"] != null)
                {
                    List<JToken> tt = Array(s["tour"], Join(p, "tour"));
                    for (int k = 0; k < tt.Count; k++) tour.Add(String(tt[k], Index(Join(p, "tour"), k)));
                }
                steps.Add(new StepDef(id, goal, OptionalString(s, p, "highlight"), chapter, tour.AsReadOnly(), s["pageAfter"] != null && Bool(s["pageAfter"], Join(p, "pageAfter"))));
            }

            const string er = "eras";
            JObject e = Object(Parse(erasJson, er), er);
            OnlyFields(e, er, "eras");
            var eras = new List<EraDef>();
            var eraIds = new List<string>();
            List<JToken> eraTokens = NonEmptyArray(e, er, "eras");
            for (int i = 0; i < eraTokens.Count; i++)
            {
                string p = Index(er + ".eras", i);
                JObject x = Object(eraTokens[i], p);
                OnlyFields(x, p, "id", "conditions", "manual");
                string id = UniqueId(x, p, eraIds);
                eraIds.Add(id);
                if (i == 0)
                {
                    if (x["conditions"] != null) throw new ContentException(Join(p, "conditions"), "the first era is the start and has no conditions");
                    eras.Add(new EraDef(id, 0, Fixed.Zero, 0, 0));
                    continue;
                }
                if (x["manual"] != null)
                {
                    if (!Bool(x["manual"], Join(p, "manual"))) throw new ContentException(Join(p, "manual"), "leave it out instead of false");
                    if (x["conditions"] != null) throw new ContentException(Join(p, "conditions"), "a manual era has no conditions yet");
                    eras.Add(new EraDef(id, 0, Fixed.Zero, 0, 0, true));
                    continue;
                }
                string cp = Join(p, "conditions");
                JObject c = Object(Required(x, p, "conditions"), cp);
                OnlyFields(c, cp, "acresHarvestedInSeason", "meanCareAtLeast", "handHutsAtLeast", "profitableSeasonsAtLeast");
                eras.Add(new EraDef(id, BoundedInt(c, cp, "acresHarvestedInSeason", 1, 1000), Bounded(c, cp, "meanCareAtLeast", "0", "2"),
                    BoundedInt(c, cp, "handHutsAtLeast", 0, 100), BoundedInt(c, cp, "profitableSeasonsAtLeast", 0, 100)));
            }
            return new OnboardingDefs(steps.AsReadOnly(), chapters.AsReadOnly(), eras.AsReadOnly());
        }

        /// <summary>A goal or trigger: its kind, then exactly the fields that kind takes.</summary>
        private static ConditionDef Condition(JObject c, string p, string[] kinds, List<string> chapterIds)
        {
            string kindName = String(Required(c, p, "kind"), Join(p, "kind"));
            int kind = System.Array.IndexOf(kinds, kindName);
            if (kind < 0) throw new ContentException(Join(p, "kind"), "unknown kind '" + kindName + "'");
            IReadOnlyList<string> names = new string[0];
            string value = null;
            int number = 0, after = -1;
            List<string> Names(string field, string[] known)
            {
                var list = new List<string>();
                List<JToken> tokens = NonEmptyArray(c, p, field);
                for (int i = 0; i < tokens.Count; i++)
                {
                    string n = String(tokens[i], Index(Join(p, field), i));
                    if (System.Array.IndexOf(known, n) < 0) throw new ContentException(Index(Join(p, field), i), "unknown name '" + n + "'");
                    list.Add(n);
                }
                return list;
            }
            switch (kindName)
            {
                case "event": OnlyFields(c, p, "kind", "events"); names = Names("events", OnboardingEvents); break;
                case "sheetVisited": OnlyFields(c, p, "kind", "sheet"); value = String(Required(c, p, "sheet"), Join(p, "sheet")); break;
                case "cropSown": OnlyFields(c, p, "kind", "minGrowthHours"); number = BoundedInt(c, p, "minGrowthHours", 0, 1000); break;
                case "storeAbovePercent": OnlyFields(c, p, "kind", "percent"); number = BoundedInt(c, p, "percent", 1, 100); break;
                case "careBelowForHours": OnlyFields(c, p, "kind", "hours"); number = BoundedInt(c, p, "hours", 1, 100); break;
                case "seasonStart":
                    OnlyFields(c, p, "kind", "season");
                    value = String(Required(c, p, "season"), Join(p, "season"));
                    if (System.Array.IndexOf(SeasonIds, value) < 0) throw new ContentException(Join(p, "season"), "unknown season '" + value + "'");
                    break;
                case "rejection":
                    OnlyFields(c, p, "kind", "reason", "commands");
                    value = String(Required(c, p, "reason"), Join(p, "reason"));
                    if (!System.Enum.TryParse(value, out Sim.Core.Commands.RejectReason _)) throw new ContentException(Join(p, "reason"), "unknown reason '" + value + "'");
                    names = Names("commands", OnboardingCommands);
                    break;
                case "cashBelowHutCost":
                    OnlyFields(c, p, "kind", "after");
                    string a = String(Required(c, p, "after"), Join(p, "after"));
                    after = chapterIds.IndexOf(a);
                    if (after < 0) throw new ContentException(Join(p, "after"), "unknown chapter '" + a + "'");
                    break;
                default: OnlyFields(c, p, "kind"); break;
            }
            return new ConditionDef(kind, names, value, number, after);
        }

        private static string OptionalString(JObject o, string path, string field)
        {
            JToken t = o[field];
            return t == null || t.Type == JTokenType.Null ? null : String(t, Join(path, field));
        }
    }
}
