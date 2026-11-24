using Newtonsoft.Json.Linq;
using Sim.Core.Definitions;
using Sim.Core.Maths;
using static Sim.Content.JsonFields;

namespace Sim.Content
{
    public static partial class ContentLoader
    {
        /// <summary><c>workplace.json</c> (spec 028 §3.9): supervisors, accidents, conflicts and associations.</summary>
        public static WorkplaceDef LoadWorkplace(string json)
        {
            const string root = "workplace";
            JObject o = Object(Parse(json, root), root);
            OnlyFields(o, root, "supervisorWage", "supervisorMinWorkers", "supervisorSpan", "outputPerStar", "riskHalf", "expiryHours",
                "accidentChance", "nightFactor", "overtimeFactor", "skippedFactor", "outHours", "clinicOutHours", "severeShare", "severeHours", "bill", "severeBill",
                "payMood", "payReputation", "unpaidMood", "unpaidReputation",
                "conflictMinWorkers", "conflictChance", "conflictFatigue", "talkHours", "talkWindowHours", "talkResolves", "moveMood", "letMood", "letResigns",
                "assocMinWorkers", "assocPayUnder", "assocFatigue", "assocCloses", "assocReputation", "assocForms", "assocRaise", "assocNegotiateRaise", "assocAgrees",
                "assocQuietCloses", "refuseMood", "refuseOutput", "stoppageHours");
            Fixed F(string n) { return Number(Required(o, root, n), Join(root, n)); }
            int I(string n) { return Int(Required(o, root, n), Join(root, n)); }
            long M(string n) { return Money(Required(o, root, n), Join(root, n)); }
            var d = new WorkplaceDef
            {
                SupervisorWage = M("supervisorWage"), SupervisorMinWorkers = I("supervisorMinWorkers"), SupervisorSpan = I("supervisorSpan"), OutputPerStar = F("outputPerStar"),
                RiskHalf = F("riskHalf"), ExpiryHours = I("expiryHours"),
                AccidentChance = F("accidentChance"), NightFactor = F("nightFactor"), OvertimeFactor = F("overtimeFactor"), SkippedFactor = F("skippedFactor"),
                OutHours = I("outHours"), ClinicOutHours = I("clinicOutHours"), SevereShare = F("severeShare"), SevereHours = I("severeHours"), Bill = M("bill"), SevereBill = M("severeBill"),
                PayMood = F("payMood"), PayReputation = I("payReputation"), UnpaidMood = F("unpaidMood"), UnpaidReputation = I("unpaidReputation"),
                ConflictMinWorkers = I("conflictMinWorkers"), ConflictChance = F("conflictChance"), ConflictFatigue = F("conflictFatigue"), TalkHours = I("talkHours"),
                TalkWindowHours = I("talkWindowHours"), TalkResolves = F("talkResolves"), MoveMood = F("moveMood"), LetMood = F("letMood"), LetResigns = F("letResigns"),
                AssocMinWorkers = I("assocMinWorkers"), AssocPayUnder = F("assocPayUnder"), AssocFatigue = F("assocFatigue"), AssocCloses = I("assocCloses"),
                AssocReputation = I("assocReputation"), AssocForms = F("assocForms"), AssocRaise = F("assocRaise"), AssocNegotiateRaise = F("assocNegotiateRaise"),
                AssocAgrees = F("assocAgrees"), AssocQuietCloses = I("assocQuietCloses"), RefuseMood = F("refuseMood"), RefuseOutput = F("refuseOutput"), StoppageHours = I("stoppageHours"),
            };
            if (d.SupervisorWage <= 0) throw new ContentException(Join(root, "supervisorWage"), "not above 0");
            Range(d.SupervisorMinWorkers, 2, 10, Join(root, "supervisorMinWorkers"));
            Range(d.SupervisorSpan, d.SupervisorMinWorkers, 12, Join(root, "supervisorSpan"));
            Range(d.OutputPerStar, Fixed.Zero, Fixed.FromRatio(1, 10), Join(root, "outputPerStar"));
            Range(d.RiskHalf, Fixed.Zero, Fixed.One, Join(root, "riskHalf"));
            Range(d.ExpiryHours, 1, 72, Join(root, "expiryHours"));
            Range(d.AccidentChance, Fixed.Zero, Fixed.FromRatio(1, 10), Join(root, "accidentChance"));
            foreach (string c in new[] { "nightFactor", "overtimeFactor", "skippedFactor" }) Range(F(c), Fixed.One, Fixed.FromInt(10), Join(root, c));
            Range(d.OutHours, 1, 96, Join(root, "outHours"));
            Range(d.ClinicOutHours, 0, d.OutHours, Join(root, "clinicOutHours"));
            Range(d.SevereShare, Fixed.Zero, Fixed.One, Join(root, "severeShare"));
            Range(d.SevereHours, d.OutHours, 96, Join(root, "severeHours"));
            if (d.Bill < 0 || d.SevereBill < d.Bill) throw new ContentException(Join(root, "severeBill"), "not below the bill");
            foreach (string c in new[] { "payMood", "unpaidMood", "moveMood", "letMood", "refuseMood" }) Range(F(c), Fixed.FromRatio(-1, 2), Fixed.FromRatio(1, 2), Join(root, c));
            Range(d.ConflictMinWorkers, 2, 10, Join(root, "conflictMinWorkers"));
            Range(d.ConflictChance, Fixed.Zero, Fixed.One, Join(root, "conflictChance"));
            Range(d.ConflictFatigue, Fixed.Zero, Fixed.FromInt(10), Join(root, "conflictFatigue"));
            Range(d.TalkWindowHours, d.TalkHours, 48, Join(root, "talkWindowHours"));
            Range(d.TalkResolves, Fixed.Zero, Fixed.One, Join(root, "talkResolves"));
            Range(d.LetResigns, Fixed.Zero, Fixed.One, Join(root, "letResigns"));
            Range(d.AssocMinWorkers, d.ConflictMinWorkers, 12, Join(root, "assocMinWorkers"));
            Range(d.AssocPayUnder, Fixed.Zero, Fixed.One, Join(root, "assocPayUnder"));
            Range(d.AssocFatigue, Fixed.Zero, Fixed.One, Join(root, "assocFatigue"));
            Range(d.AssocCloses, 1, 8, Join(root, "assocCloses"));
            Range(d.AssocReputation, 0, 100, Join(root, "assocReputation"));
            Range(d.AssocForms, Fixed.Zero, Fixed.One, Join(root, "assocForms"));
            Range(d.AssocRaise, Fixed.Zero, Fixed.One, Join(root, "assocRaise"));
            Range(d.AssocNegotiateRaise, Fixed.Zero, d.AssocRaise, Join(root, "assocNegotiateRaise"));
            Range(d.AssocAgrees, Fixed.Zero, Fixed.One, Join(root, "assocAgrees"));
            Range(d.AssocQuietCloses, 1, 16, Join(root, "assocQuietCloses"));
            Range(d.RefuseOutput, Fixed.FromRatio(1, 2), Fixed.One, Join(root, "refuseOutput"));
            Range(d.StoppageHours, 1, 48, Join(root, "stoppageHours"));
            return d;
        }
    }
}
