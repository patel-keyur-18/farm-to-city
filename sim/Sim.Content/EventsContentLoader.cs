using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        private static readonly string[] EventFamilies = { "weather", "economic", "business", "workforce", "social", "opportunity", "disaster" };
        private static readonly string[] EffectKinds = { "cash", "cropHealth", "priceShift", "costShift", "happiness", "offer", "flag", "downtime", "grant", "resow", "sellNow", "approval", "mediate" };

        /// <summary><c>events.json</c> (spec 027 §3.1–§3.4 and §4): the engine, the fire, the epidemic and the events with their choices and effects.</summary>
        public static EventsDef LoadEvents(string json)
        {
            const string root = "events";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "engine", "fire", "epidemic", "events");

            string ep = Join(root, "engine");
            JObject e = Object(Required(o, root, "engine"), ep);
            OnlyFields(e, ep, "minorChance", "majorChance", "maxWaiting", "minorHours", "majorHours", "warningHours", "repairHours", "slideDays", "graceDays", "cashShareCap", "baseWeight", "disasterWeight", "weightMin", "weightMax", "keepHistory");
            var engine = new EventEngineDef
            {
                MinorChance = Bounded(e, ep, "minorChance", "0", "1"), MajorChance = Bounded(e, ep, "majorChance", "0", "1"), MaxWaiting = BoundedInt(e, ep, "maxWaiting", 1, 10),
                MinorHours = BoundedInt(e, ep, "minorHours", 1, 168), MajorHours = BoundedInt(e, ep, "majorHours", 1, 168), WarningHours = BoundedInt(e, ep, "warningHours", 1, 168),
                RepairHours = BoundedInt(e, ep, "repairHours", 1, 168), SlideDays = BoundedInt(e, ep, "slideDays", 0, 10), GraceDays = BoundedInt(e, ep, "graceDays", 0, 30),
                CashShareCap = Bounded(e, ep, "cashShareCap", "0.05", "1"), BaseWeight = BoundedInt(e, ep, "baseWeight", 1, 100), DisasterWeight = BoundedInt(e, ep, "disasterWeight", 1, 100),
                WeightMin = Bounded(e, ep, "weightMin", "0.01", "1"), WeightMax = Bounded(e, ep, "weightMax", "1", "100"), KeepHistory = BoundedInt(e, ep, "keepHistory", 1, 50),
            };
            if (engine.WeightMin > engine.WeightMax) throw new ContentException(Join(ep, "weightMin"), "above weightMax");

            string fp = Join(root, "fire");
            JObject f = Object(Required(o, root, "fire"), fp);
            OnlyFields(f, fp, "damageShare", "towerFactor", "prepareFactor", "prepareCost", "downtimeDays", "slowRepairShare", "slowRepairDays");
            var fire = new FireDef
            {
                DamageShare = Bounded(f, fp, "damageShare", "0.05", "1"), TowerFactor = Bounded(f, fp, "towerFactor", "0", "1"), PrepareFactor = Bounded(f, fp, "prepareFactor", "0", "1"),
                PrepareCost = BoundedInt(f, fp, "prepareCost", 0, 100000), DowntimeDays = BoundedInt(f, fp, "downtimeDays", 0, 30), SlowRepairShare = Bounded(f, fp, "slowRepairShare", "0.1", "1"),
                SlowRepairDays = BoundedInt(f, fp, "slowRepairDays", 1, 60),
            };

            string pp = Join(root, "epidemic");
            JObject p = Object(Required(o, root, "epidemic"), pp);
            OnlyFields(p, pp, "days", "sickFactor", "clinicFactor", "campFactor", "closeFactor", "campPerPerson", "closeDays", "closeShopsDemand");
            var epidemic = new EpidemicDef
            {
                Days = BoundedInt(p, pp, "days", 1, 30), SickFactor = Bounded(p, pp, "sickFactor", "1", "10"), ClinicFactor = Bounded(p, pp, "clinicFactor", "0", "1"),
                CampFactor = Bounded(p, pp, "campFactor", "1", "10"), CloseFactor = Bounded(p, pp, "closeFactor", "1", "10"), CampPerPerson = BoundedInt(p, pp, "campPerPerson", 0, 1000),
                CloseDays = BoundedInt(p, pp, "closeDays", 1, 30), CloseShopsDemand = Bounded(p, pp, "closeShopsDemand", "0", "1")
            };

            string lp = Join(root, "events");
            var arr = Array(Required(o, root, "events"), lp);
            var events = new List<EventDef>();
            for (int i = 0; i < arr.Count; i++)
            {
                string ip = Index(lp, i);
                JObject j = Object(arr[i], ip);
                OnlyFields(j, ip, "id", "family", "major", "disaster", "rolled", "mods", "choices", "default");
                string id = String(Required(j, ip, "id"), Join(ip, "id"));
                foreach (EventDef x in events) if (x.Id == id) throw new ContentException(Join(ip, "id"), "two events called '" + id + "'");
                string fam = String(Required(j, ip, "family"), Join(ip, "family"));
                int family = System.Array.IndexOf(EventFamilies, fam);
                if (family < 0) throw new ContentException(Join(ip, "family"), "unknown family '" + fam + "'");
                var mods = new Dictionary<string, Fixed>();
                if (j["mods"] != null)
                {
                    JObject m = Object(j["mods"], Join(ip, "mods"));
                    foreach (KeyValuePair<string, JToken> kv in m) mods[kv.Key] = Number(kv.Value, Join(Join(ip, "mods"), kv.Key));
                }
                string cp = Join(ip, "choices");
                var carr = Array(Required(j, ip, "choices"), cp);
                if (carr.Count < 2 || carr.Count > 3) throw new ContentException(cp, "two or three choices");
                var choices = new List<EventChoiceDef>();
                for (int c = 0; c < carr.Count; c++)
                {
                    string chp = Index(cp, c);
                    JObject ch = Object(carr[c], chp);
                    OnlyFields(ch, chp, "id", "effects");
                    var effects = new List<EffectDef>();
                    string efp = Join(chp, "effects");
                    var earr = Array(Required(ch, chp, "effects"), efp);
                    for (int k = 0; k < earr.Count; k++) effects.Add(LoadEffect(Object(earr[k], Index(efp, k)), Index(efp, k)));
                    choices.Add(new EventChoiceDef { Id = String(Required(ch, chp, "id"), Join(chp, "id")), Effects = effects.AsReadOnly() });
                }
                int def = j["default"] == null ? 0 : Int(j["default"], Join(ip, "default"));
                Range(def, 0, choices.Count - 1, Join(ip, "default"));
                events.Add(new EventDef
                {
                    Id = id, Family = family, Major = j["major"] != null && Bool(j["major"], Join(ip, "major")), Disaster = j["disaster"] != null && Bool(j["disaster"], Join(ip, "disaster")),
                    Rolled = j["rolled"] == null || Bool(j["rolled"], Join(ip, "rolled")), Choices = choices.AsReadOnly(), Default = def, Mods = mods,
                });
            }
            var d = new EventsDef { Engine = engine, FireDef = fire, EpidemicDef = epidemic, Events = events.AsReadOnly() };
            if (d.IndexOf(EventsDef.Fire) < 0 || d.IndexOf(EventsDef.Epidemic) < 0 || d.IndexOf(EventsDef.FireRepair) < 0) throw new ContentException(lp, "the fire, the epidemic and the fire's repair card are required");
            return d;
        }

        private static EffectDef LoadEffect(JObject j, string p)
        {
            OnlyFields(j, p, "kind", "unit", "amount", "filter", "factor", "percent", "days", "target", "who", "points", "what", "name", "hours");
            string k = String(Required(j, p, "kind"), Join(p, "kind"));
            int kind = System.Array.IndexOf(EffectKinds, k);
            if (kind < 0) throw new ContentException(Join(p, "kind"), "unknown effect '" + k + "'");
            var e = new EffectDef { Kind = (EffectKind)kind };
            if (j["unit"] != null) e.Unit = String(j["unit"], Join(p, "unit"));
            if (j["amount"] != null) { e.Amount = Int(j["amount"], Join(p, "amount")); Range((int)e.Amount, 0, 1000000, Join(p, "amount")); }
            if (j["filter"] != null) e.Filter = String(j["filter"], Join(p, "filter"));
            if (j["factor"] != null) { e.Factor = Number(j["factor"], Join(p, "factor")); Range(e.Factor, Fixed.Parse("0.1"), Fixed.One, Join(p, "factor")); }
            if (j["percent"] != null) { e.Percent = Int(j["percent"], Join(p, "percent")); Range(e.Percent, -90, 300, Join(p, "percent")); }
            if (j["days"] != null) { e.Days = Int(j["days"], Join(p, "days")); Range(e.Days, 0, 400, Join(p, "days")); }
            if (j["target"] != null) e.Target = String(j["target"], Join(p, "target"));
            if (j["who"] != null) e.Who = String(j["who"], Join(p, "who"));
            if (j["points"] != null) { e.Points = Int(j["points"], Join(p, "points")); Range(e.Points, -20, 20, Join(p, "points")); }
            if (j["what"] != null) e.What = String(j["what"], Join(p, "what"));
            if (j["name"] != null) e.Name = String(j["name"], Join(p, "name"));
            if (j["hours"] != null) { e.Hours = Int(j["hours"], Join(p, "hours")); Range(e.Hours, 0, 720, Join(p, "hours")); }
            return e;
        }
    }
}
