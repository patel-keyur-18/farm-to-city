using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {

        /// <summary>Loads <c>content/people.json</c> (spec 006 §4) and checks its bounds.</summary>
        private static PeopleDefs LoadPeople(string json, int handHut, BuildingDef hut)
        {
            const string root = "people";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "labourPool", "hand", "jobPost", "huts", "traits", "names");
            if (hut.HousesHands < 1) throw new ContentException("buildings.hand_hut.housesHands", "a hand's hut must house at least one hand");

            string lp = root + ".labourPool";
            JObject l = Object(Required(o, root, "labourPool"), lp);
            OnlyFields(l, lp, "rosterSize", "availablePerDay", "dailyWage", "hoursPerDay", "regularThresholdDays");
            int roster = BoundedInt(l, lp, "rosterSize", 0, 30);
            string ap = Join(lp, "availablePerDay");
            JObject a = Object(Required(l, lp, "availablePerDay"), ap);
            OnlyFields(a, ap, SeasonIds);
            var available = new List<int>();
            foreach (string season in SeasonIds) available.Add(BoundedInt(a, ap, season, 0, roster));
            long wage = Money(Required(l, lp, "dailyWage"), Join(lp, "dailyWage"));
            if (wage <= 0) throw new ContentException(Join(lp, "dailyWage"), "must be more than 0");
            var pool = new LabourPoolDef(roster, available, wage, Bounded(l, lp, "hoursPerDay", "0", "14"), BoundedInt(l, lp, "regularThresholdDays", 1, 100));

            string hp = root + ".hand";
            JObject h = Object(Required(o, root, "hand"), hp);
            OnlyFields(h, hp, "hoursPerDay", "productivityBase", "productivityPerSkill", "marketWage", "wageBase", "wagePerSkill", "skillMin", "skillMax",
                "skillRangeHalfWidth", "skillGainPerSeason", "maxApplicants", "applicantEveryHours", "applicantJitterHours", "applicantWaitDays",
                "acceptChance", "severanceShare", "lapseGraceDays", "maxOrders", "maxHandHuts", "orderActionsPerHour");
            var hand = new HandDef
            {
                HoursPerDay = Bounded(h, hp, "hoursPerDay", "0", "14"),
                ProductivityBase = Bounded(h, hp, "productivityBase", "0", "2"),
                ProductivityPerSkill = Bounded(h, hp, "productivityPerSkill", "0", "2"),
                MarketWage = Money(Required(h, hp, "marketWage"), Join(hp, "marketWage")),
                WageBase = Bounded(h, hp, "wageBase", "0", "2"),
                WagePerSkill = Bounded(h, hp, "wagePerSkill", "0", "2"),
                SkillMin = BoundedInt(h, hp, "skillMin", 0, 100),
                SkillMax = BoundedInt(h, hp, "skillMax", 0, 100),
                SkillRangeHalfWidth = BoundedInt(h, hp, "skillRangeHalfWidth", 0, 100),
                SkillGainPerSeason = Bounded(h, hp, "skillGainPerSeason", "0", "20"),
                MaxApplicants = BoundedInt(h, hp, "maxApplicants", 1, 20),
                ApplicantEveryHours = BoundedInt(h, hp, "applicantEveryHours", 1, 24 * 14),
                ApplicantJitterHours = BoundedInt(h, hp, "applicantJitterHours", 0, 24 * 7),
                ApplicantWaitDays = BoundedInt(h, hp, "applicantWaitDays", 1, 14),
                AcceptChance = Bounded(h, hp, "acceptChance", "0", "1"),
                SeveranceShare = Bounded(h, hp, "severanceShare", "0", "1"),
                LapseGraceDays = BoundedInt(h, hp, "lapseGraceDays", 0, 8),
                MaxOrders = BoundedInt(h, hp, "maxOrders", 1, 20),
                MaxHandHuts = BoundedInt(h, hp, "maxHandHuts", 0, 4),
                OrderActionsPerHour = BoundedInt(h, hp, "orderActionsPerHour", 1, 6),
            };
            if (hand.MarketWage <= 0) throw new ContentException(Join(hp, "marketWage"), "must be more than 0");
            if (hand.SkillMin > hand.SkillMax) throw new ContentException(Join(hp, "skillMin"), "must not be above skillMax");
            if (hand.ApplicantJitterHours >= hand.ApplicantEveryHours) throw new ContentException(Join(hp, "applicantJitterHours"), "must be below applicantEveryHours");

            string jp = root + ".jobPost";
            JObject j = Object(Required(o, root, "jobPost"), jp);
            OnlyFields(j, jp, "cost", "days", "rateFactor", "aboveMarketFactor", "aboveMarketSkillBonus", "aboveMarketAccept");
            var post = new JobPostDef
            {
                Cost = Money(Required(j, jp, "cost"), Join(jp, "cost")),
                Days = BoundedInt(j, jp, "days", 1, 14),
                RateFactor = Bounded(j, jp, "rateFactor", "0.1", "1"),
                AboveMarketFactor = Bounded(j, jp, "aboveMarketFactor", "1", "2"),
                AboveMarketSkillBonus = BoundedInt(j, jp, "aboveMarketSkillBonus", 0, 50),
                AboveMarketAccept = Bounded(j, jp, "aboveMarketAccept", "0", "1"),
            };
            if (post.Cost <= 0) throw new ContentException(Join(jp, "cost"), "must be more than 0");

            string up = root + ".huts";
            JObject u = Object(Required(o, root, "huts"), up);
            OnlyFields(u, up, "placesPerAcre");
            int places = BoundedInt(u, up, "placesPerAcre", 0, 4);

            var traits = new List<TraitDef>();
            List<JToken> tt = NonEmptyArray(o, root, "traits");
            for (int i = 0; i < tt.Count; i++)
            {
                string p = Index(root + ".traits", i);
                JObject t = Object(tt[i], p);
                OnlyFields(t, p, "id", "nameKey", "hoursFactor", "keepsWage", "skillGainFactor", "renewalWageFactor", "careless");
                string id = UniqueId(t, p, traits.ConvertAll(x => x.Id));
                traits.Add(new TraitDef(id, String(Required(t, p, "nameKey"), Join(p, "nameKey")),
                    OptionalFactor(t, p, "hoursFactor"), t["keepsWage"] != null && Bool(t["keepsWage"], Join(p, "keepsWage")),
                    OptionalFactor(t, p, "skillGainFactor"), OptionalFactor(t, p, "renewalWageFactor"),
                    t["careless"] != null && Bool(t["careless"], Join(p, "careless"))));
            }
            if (traits.Count < 2) throw new ContentException(root + ".traits", "needs at least 2 traits (an applicant has 2 distinct ones)");

            var names = new List<NameDef>();
            List<JToken> nt = NonEmptyArray(o, root, "names");
            for (int i = 0; i < nt.Count; i++)
            {
                string p = Index(root + ".names", i);
                JObject n = Object(nt[i], p);
                OnlyFields(n, p, "id", "gender");
                string id = UniqueId(n, p, names.ConvertAll(x => x.Id));
                string g = String(Required(n, p, "gender"), Join(p, "gender"));
                if (g != "m" && g != "f") throw new ContentException(Join(p, "gender"), "expected m or f");
                names.Add(new NameDef(id, "name." + id, g == "f"));
            }
            if (names.Count < roster) throw new ContentException(root + ".names", "needs at least rosterSize names");
            return new PeopleDefs(pool, hand, post, traits, names, places, handHut);
        }

        private static Fixed OptionalFactor(JObject o, string path, string name)
        {
            return o[name] == null ? Fixed.One : Bounded(o, path, name, "0.5", "2");
        }
    }
}
