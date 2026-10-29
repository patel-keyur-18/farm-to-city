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
            OnlyFields(o, root, "labourPool", "hand", "jobPost", "hiring", "work", "hrEvents", "reputation", "huts", "traits", "names");
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

            string gp = root + ".hiring";
            JObject hj = Object(Required(o, root, "hiring"), gp);
            OnlyFields(hj, gp, "referenceCost", "referenceHours", "referenceHalfWidth", "lowFactor", "highFactor", "lowAccept", "highAccept", "townLowAccept", "reputationStart");
            var hiring = new HiringDef
            {
                ReferenceCost = Money(Required(hj, gp, "referenceCost"), Join(gp, "referenceCost")),
                ReferenceHours = BoundedInt(hj, gp, "referenceHours", 1, 72),
                ReferenceHalfWidth = BoundedInt(hj, gp, "referenceHalfWidth", 0, 50),
                LowFactor = Bounded(hj, gp, "lowFactor", "0.5", "1"),
                HighFactor = Bounded(hj, gp, "highFactor", "1", "2"),
                LowAccept = Bounded(hj, gp, "lowAccept", "0", "1"),
                HighAccept = Bounded(hj, gp, "highAccept", "0", "1"),
                TownLowAccept = Bounded(hj, gp, "townLowAccept", "0", "1"),
                ReputationStart = BoundedInt(hj, gp, "reputationStart", 0, 100),
            };
            if (hiring.ReferenceCost < 0) throw new ContentException(Join(gp, "referenceCost"), "must not be negative");

            string wp = root + ".work";
            JObject wj = Object(Required(o, root, "work"), wp);
            OnlyFields(wj, wp, "motivationMin", "motivationMax", "fairAbove", "fairBelow", "homeBelow", "homePerPoint", "fatigueUp", "fatigueDown",
                "fatigueMax", "longDayHours", "overtimeFactor", "courseCost", "courseDays", "courseGain");
            var work = new WorkDef
            {
                MotivationMin = Bounded(wj, wp, "motivationMin", "0", "1"),
                MotivationMax = Bounded(wj, wp, "motivationMax", "1", "2"),
                FairAbove = Bounded(wj, wp, "fairAbove", "0", "0.5"),
                FairBelow = Bounded(wj, wp, "fairBelow", "0", "0.5"),
                HomeBelow = BoundedInt(wj, wp, "homeBelow", 0, 100),
                HomePerPoint = Bounded(wj, wp, "homePerPoint", "0", "0.05"),
                FatigueUp = Bounded(wj, wp, "fatigueUp", "0", "1"),
                FatigueDown = Bounded(wj, wp, "fatigueDown", "0", "1"),
                FatigueMax = Bounded(wj, wp, "fatigueMax", "0", "0.9"),
                LongDayHours = BoundedInt(wj, wp, "longDayHours", 0, 6),
                OvertimeFactor = Bounded(wj, wp, "overtimeFactor", "1", "3"),
                CourseCost = Money(Required(wj, wp, "courseCost"), Join(wp, "courseCost")),
                CourseDays = BoundedInt(wj, wp, "courseDays", 1, 30),
                CourseGain = BoundedInt(wj, wp, "courseGain", 0, 50),
            };

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
                OnlyFields(t, p, "id", "nameKey", "hoursFactor", "keepsWage", "skillGainFactor", "renewalWageFactor", "careless", "saleFactor", "costFactor",
                    "savesLeisure", "colleagueMotivation", "noBadBatch", "opposite");
                string id = UniqueId(t, p, traits.ConvertAll(x => x.Id));
                traits.Add(new TraitDef(id, String(Required(t, p, "nameKey"), Join(p, "nameKey")),
                    OptionalFactor(t, p, "hoursFactor"), t["keepsWage"] != null && Bool(t["keepsWage"], Join(p, "keepsWage")),
                    OptionalFactor(t, p, "skillGainFactor"), OptionalFactor(t, p, "renewalWageFactor"),
                    t["careless"] != null && Bool(t["careless"], Join(p, "careless")))
                {
                    SaleFactor = OptionalFactor(t, p, "saleFactor"),
                    CostFactor = OptionalFactor(t, p, "costFactor"),
                    SavesLeisure = t["savesLeisure"] != null && Bool(t["savesLeisure"], Join(p, "savesLeisure")),
                    ColleagueMotivation = t["colleagueMotivation"] == null ? Fixed.Zero : Bounded(t, p, "colleagueMotivation", "0", "0.5"),
                    NoBadBatch = t["noBadBatch"] != null && Bool(t["noBadBatch"], Join(p, "noBadBatch")),
                });
            }
            if (traits.Count < 2) throw new ContentException(root + ".traits", "needs at least 2 traits (an applicant has 2 distinct ones)");
            for (int i = 0; i < tt.Count; i++)
            {
                JToken op = ((JObject)tt[i])["opposite"];
                if (op == null) continue;
                string path = Join(Index(root + ".traits", i), "opposite");
                int at = traits.FindIndex(x => x.Id == String(op, path));
                if (at < 0 || at == i) throw new ContentException(path, "must name another trait");
                traits[i].Opposite = at;
            }

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
            string ep = root + ".hrEvents";
            JObject ej = Object(Required(o, root, "hrEvents"), ep);
            OnlyFields(ej, ep, "raiseChance", "raiseGap", "poachChance", "poachSkill", "poachUnderpaidFactor", "poachRefusedFactor", "poachMatch", "sickChance", "sickTiredAbove",
                "sickTiredFactor", "sickDays", "emergencyChance", "advanceShare", "badBatchChance", "badBatchSellShare", "raiseRefusedMood", "matchedMood", "sickPaidMood",
                "sickUnpaidMood", "advanceMood", "noAdvanceMood");
            var hr = new HrDef
            {
                RaiseChance = Bounded(ej, ep, "raiseChance", "0", "1"),
                RaiseGap = Bounded(ej, ep, "raiseGap", "0", "0.5"),
                PoachChance = Bounded(ej, ep, "poachChance", "0", "1"),
                PoachSkill = BoundedInt(ej, ep, "poachSkill", 0, 100),
                PoachUnderpaidFactor = BoundedInt(ej, ep, "poachUnderpaidFactor", 1, 10),
                PoachRefusedFactor = BoundedInt(ej, ep, "poachRefusedFactor", 1, 10),
                PoachMatch = Bounded(ej, ep, "poachMatch", "0", "1"),
                SickChance = Bounded(ej, ep, "sickChance", "0", "1"),
                SickTiredAbove = Bounded(ej, ep, "sickTiredAbove", "0", "1"),
                SickTiredFactor = BoundedInt(ej, ep, "sickTiredFactor", 1, 10),
                SickDays = BoundedInt(ej, ep, "sickDays", 1, 10),
                EmergencyChance = Bounded(ej, ep, "emergencyChance", "0", "1"),
                AdvanceShare = Bounded(ej, ep, "advanceShare", "0", "1"),
                BadBatchChance = Bounded(ej, ep, "badBatchChance", "0", "1"),
                BadBatchSellShare = Bounded(ej, ep, "badBatchSellShare", "0", "1"),
                RaiseRefusedMood = Bounded(ej, ep, "raiseRefusedMood", "-0.5", "0"),
                MatchedMood = Bounded(ej, ep, "matchedMood", "0", "0.5"),
                SickPaidMood = Bounded(ej, ep, "sickPaidMood", "0", "0.5"),
                SickUnpaidMood = Bounded(ej, ep, "sickUnpaidMood", "-0.5", "0"),
                AdvanceMood = Bounded(ej, ep, "advanceMood", "0", "0.5"),
                NoAdvanceMood = Bounded(ej, ep, "noAdvanceMood", "-0.5", "0"),
            };

            string rp = root + ".reputation";
            JObject rj = Object(Required(o, root, "reputation"), rp);
            OnlyFields(rj, rp, "fairRenewal", "sickPaid", "advance", "severance", "lowHire", "raiseRefused", "probationLetGo", "lapse", "badBatchSold", "high", "low",
                "acceptShift", "highSkillBonus", "lowPace");
            var rep = new ReputationDef
            {
                FairRenewal = BoundedInt(rj, rp, "fairRenewal", 0, 20),
                SickPaid = BoundedInt(rj, rp, "sickPaid", 0, 20),
                Advance = BoundedInt(rj, rp, "advance", 0, 20),
                Severance = BoundedInt(rj, rp, "severance", 0, 20),
                LowHire = BoundedInt(rj, rp, "lowHire", -20, 0),
                RaiseRefused = BoundedInt(rj, rp, "raiseRefused", -20, 0),
                ProbationLetGo = BoundedInt(rj, rp, "probationLetGo", -20, 0),
                Lapse = BoundedInt(rj, rp, "lapse", -20, 0),
                BadBatchSold = BoundedInt(rj, rp, "badBatchSold", -20, 0),
                High = BoundedInt(rj, rp, "high", 0, 100),
                Low = BoundedInt(rj, rp, "low", 0, 100),
                AcceptShift = Bounded(rj, rp, "acceptShift", "0", "0.5"),
                HighSkillBonus = BoundedInt(rj, rp, "highSkillBonus", 0, 50),
                LowPace = Bounded(rj, rp, "lowPace", "0.1", "1"),
            };
            if (rep.Low >= rep.High) throw new ContentException(Join(rp, "low"), "must be below high");
            return new PeopleDefs(pool, hand, post, hiring, work, traits, names, places, handHut) { Hr = hr, Reputation = rep };
        }

        private static Fixed OptionalFactor(JObject o, string path, string name)
        {
            return o[name] == null ? Fixed.One : Bounded(o, path, name, "0.5", "2");
        }
    }
}
