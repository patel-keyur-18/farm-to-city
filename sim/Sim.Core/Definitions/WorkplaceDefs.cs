using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>
    /// Supervisors, accidents, conflicts and associations (spec 028 §3.9, <c>content/workplace.json</c>, ADR 0166 gaps 3 to 5). The accident chances are daily rolls and its hours real
    /// hours of the 4-day season (rescaled from the spec's 90-day figures); money is a coin amount at year-0 prices.
    /// </summary>
    public sealed class WorkplaceDef
    {
        /// <summary>A supervisor: the wage he asks at skill 50, the workers a place needs before one can be hired, how many he covers, the output each of his stars adds, and the factor on accident and association chances.</summary>
        public long SupervisorWage { get; internal set; }
        public int SupervisorMinWorkers { get; internal set; }
        public int SupervisorSpan { get; internal set; }
        public Fixed OutputPerStar { get; internal set; }

        /// <summary>What a hand's work is worth the farm in a season at year-0 prices (measured, `GateTests.HandWorth`: he nets about 18,500 after his wage, so about 48,000): the sum a supervisor's stars are worth a share of.</summary>
        public long HandOutput { get; internal set; }
        public Fixed RiskHalf { get; internal set; }

        /// <summary>Hours a new accident, conflict or association card waits before its default answers it.</summary>
        public int ExpiryHours { get; internal set; }

        /// <summary>Accidents: the chance a worker at work has one each day, the factors on it, the hours out (with the clinic, severe), the share that are severe, the bills, and what each answer does.</summary>
        public Fixed AccidentChance { get; internal set; }
        public Fixed NightFactor { get; internal set; }
        public Fixed OvertimeFactor { get; internal set; }
        public Fixed SkippedFactor { get; internal set; }
        public int OutHours { get; internal set; }
        public int ClinicOutHours { get; internal set; }
        public Fixed SevereShare { get; internal set; }
        public int SevereHours { get; internal set; }
        public long Bill { get; internal set; }
        public long SevereBill { get; internal set; }
        public Fixed PayMood { get; internal set; }
        public int PayReputation { get; internal set; }
        public Fixed UnpaidMood { get; internal set; }
        public int UnpaidReputation { get; internal set; }

        /// <summary>Conflicts: workers a place needs, the chance a season, how much tiredness raises it, your hours when you talk (spent over this window), the chance talking settles it, the moods, the chance one resigns when let be.</summary>
        public int ConflictMinWorkers { get; internal set; }
        public Fixed ConflictChance { get; internal set; }
        public Fixed ConflictFatigue { get; internal set; }
        public int TalkHours { get; internal set; }
        public int TalkWindowHours { get; internal set; }
        public Fixed TalkResolves { get; internal set; }
        public Fixed MoveMood { get; internal set; }
        public Fixed LetMood { get; internal set; }
        public Fixed LetResigns { get; internal set; }

        /// <summary>Associations: workers a place needs; pay under the reference, average fatigue for this many closes, or a reputation under this, makes one likely; the chance it forms at a close; its raise and the negotiated raise; the chance it agrees; closes it stays quiet after a yes; the refusal's mood and output; the stoppage after a second refusal.</summary>
        public int AssocMinWorkers { get; internal set; }
        public Fixed AssocPayUnder { get; internal set; }
        public Fixed AssocFatigue { get; internal set; }
        public int AssocCloses { get; internal set; }
        public int AssocReputation { get; internal set; }
        public Fixed AssocForms { get; internal set; }
        public Fixed AssocRaise { get; internal set; }
        public Fixed AssocNegotiateRaise { get; internal set; }
        public Fixed AssocAgrees { get; internal set; }
        public int AssocQuietCloses { get; internal set; }
        public Fixed RefuseMood { get; internal set; }
        public Fixed RefuseOutput { get; internal set; }
        public int StoppageHours { get; internal set; }
    }
}
