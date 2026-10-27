using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Definitions
{
    /// <summary>How a loan's principal is repaid (spec 005 §3.6).</summary>
    public enum LoanSchedule
    {
        /// <summary>Interest each season, all the principal at the last instalment (the moneylender).</summary>
        Bullet = 0,

        /// <summary>Equal principal instalments, plus that season's interest (the cooperative bank).</summary>
        EqualPrincipal = 1,
    }

    /// <summary>The founder's credit score rules, from <c>content/finance.json</c> (spec 005 §4, ADR 0038).</summary>
    public sealed class CreditScoreDef
    {
        public int Min { get; }
        public int Max { get; }
        public int StartScore { get; }

        /// <summary>The score at which a bank's score factor reaches 1 (spec 005 §3.6).</summary>
        public int FullScore { get; }
        public int OnTimeGain { get; }
        public int CleanSeasonGain { get; }
        public int MissedPenalty { get; }
        public int DefaultPenalty { get; }
        public int RestructurePenalty { get; }
        public int ForeclosureScore { get; }

        /// <summary>Hours of live play between a default's final notice and foreclosure (ADR 0038).</summary>
        public int FinalNoticeLiveHours { get; }

        public CreditScoreDef(int min, int max, int startScore, int fullScore, int onTimeGain, int cleanSeasonGain, int missedPenalty,
            int defaultPenalty, int restructurePenalty, int foreclosureScore, int finalNoticeLiveHours)
        {
            Min = min;
            Max = max;
            StartScore = startScore;
            FullScore = fullScore;
            OnTimeGain = onTimeGain;
            CleanSeasonGain = cleanSeasonGain;
            MissedPenalty = missedPenalty;
            DefaultPenalty = defaultPenalty;
            RestructurePenalty = restructurePenalty;
            ForeclosureScore = foreclosureScore;
            FinalNoticeLiveHours = finalNoticeLiveHours;
        }

        public int Clamp(int score) { return score < Min ? Min : score > Max ? Max : score; }
    }

    /// <summary>A lender from <c>content/lenders.json</c> (spec 005 §3.6, §4). Money is 1/100 coin.</summary>
    public sealed class LenderDef
    {
        public const string MoneylenderId = "moneylender";
        public const string BankId = "coop_bank";
        public const string HomesteadEra = "homestead";

        public string Id { get; }
        public string NameKey { get; }

        /// <summary>Rate a season at <see cref="RefScore"/> and above; the bank adds <see cref="RateSlopePer100"/> per 100 points below.</summary>
        public Fixed BaseRate { get; }
        public Fixed RateSlopePer100 { get; }
        public int RefScore { get; }
        public Fixed PenaltyRate { get; }
        public LoanSchedule Schedule { get; }
        public int MinTerm { get; }
        public int MaxTerm { get; }
        public long MinAmount { get; }
        public bool Secured { get; }
        public long BaseLimit { get; }
        public Fixed CollateralLtv { get; }

        /// <summary>The share of an animal's value a secured lender lends against (spec 012 §3.12; bank 0.75, a quarter down).</summary>
        public Fixed LivestockLtv { get; internal set; }

        /// <summary>The share of family houses' book value the lender counts (the home loan, spec 014 §3.4, ADR 0071); 0 if it doesn't.</summary>
        public Fixed HouseLtv { get; internal set; }
        public Fixed IncomeMultiple { get; }
        public int MinScore { get; }

        /// <summary>False for the moneylender: its limit does not scale with the score (spec 005 §3.6, "always 1").</summary>
        public bool ScoreScaledLimit { get; }
        public int GraceCloses { get; }
        public int RestructureExtraSeasons { get; }

        /// <summary>The era cap on the total owed to this lender, by era id (Homestead required), money.</summary>
        public IReadOnlyDictionary<string, long> EraCaps { get; }

        /// <summary>The loan account: 2100 for the moneylender, 2200 for the bank.</summary>
        public Finance.Account LoanAccount { get { return Id == MoneylenderId ? Finance.Account.MoneylenderLoans : Finance.Account.BankLoans; } }

        public LenderDef(string id, string nameKey, Fixed baseRate, Fixed rateSlopePer100, int refScore, Fixed penaltyRate,
            LoanSchedule schedule, int minTerm, int maxTerm, long minAmount, bool secured, long baseLimit, Fixed collateralLtv,
            Fixed incomeMultiple, int minScore, bool scoreScaledLimit, int graceCloses, int restructureExtraSeasons,
            IReadOnlyDictionary<string, long> eraCaps)
        {
            Id = id;
            NameKey = nameKey;
            BaseRate = baseRate;
            RateSlopePer100 = rateSlopePer100;
            RefScore = refScore;
            PenaltyRate = penaltyRate;
            Schedule = schedule;
            MinTerm = minTerm;
            MaxTerm = maxTerm;
            MinAmount = minAmount;
            Secured = secured;
            BaseLimit = baseLimit;
            CollateralLtv = collateralLtv;
            IncomeMultiple = incomeMultiple;
            MinScore = minScore;
            ScoreScaledLimit = scoreScaledLimit;
            GraceCloses = graceCloses;
            RestructureExtraSeasons = restructureExtraSeasons;
            EraCaps = new Dictionary<string, long>(eraCaps);
        }
    }

    /// <summary>The credit content: the score rules and the lenders in file order (moneylender, then bank). Optional (ADR 0038).</summary>
    public sealed class CreditDefs
    {
        public CreditScoreDef Score { get; }
        public IReadOnlyList<LenderDef> Lenders { get; }

        public CreditDefs(CreditScoreDef score, IReadOnlyList<LenderDef> lenders)
        {
            Score = score;
            Lenders = new List<LenderDef>(lenders).AsReadOnly();
        }

        public int IndexOfLender(string id)
        {
            for (int i = 0; i < Lenders.Count; i++)
                if (Lenders[i].Id == id) return i;
            return -1;
        }
    }
}
