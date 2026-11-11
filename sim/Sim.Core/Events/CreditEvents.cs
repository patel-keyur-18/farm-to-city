using System.Collections.Generic;
using Sim.Core.Maths;

namespace Sim.Core.Events
{
    /// <summary>A loan was taken (spec 005 §5).</summary>
    public sealed class LoanTaken : SimEvent
    {
        public long LoanId { get; }
        public string LenderId { get; }
        public long Amount { get; }
        public Fixed Rate { get; }
        public int Term { get; }

        public LoanTaken(long simTime, long loanId, string lenderId, long amount, Fixed rate, int term) : base(simTime)
        {
            LoanId = loanId;
            LenderId = lenderId;
            Amount = amount;
            Rate = rate;
            Term = term;
        }

        protected override string Describe() { return "LoanTaken(loan=" + LoanId + ", " + LenderId + ", amount=" + Amount + ", rate=" + Rate + ", term=" + Term + ")"; }
    }

    /// <summary>A scheduled or early payment: interest, arrears and principal paid (spec 005 §5).</summary>
    public sealed class LoanRepayment : SimEvent
    {
        public long LoanId { get; }
        public long Interest { get; }
        public long Arrears { get; }
        public long Principal { get; }

        public LoanRepayment(long simTime, long loanId, long interest, long arrears, long principal) : base(simTime)
        {
            LoanId = loanId;
            Interest = interest;
            Arrears = arrears;
            Principal = principal;
        }

        protected override string Describe() { return "LoanRepayment(loan=" + LoanId + ", interest=" + Interest + ", arrears=" + Arrears + ", principal=" + Principal + ")"; }
    }

    /// <summary>A season-close payment fell short (spec 005 §3.8).</summary>
    public sealed class LoanPaymentMissed : SimEvent
    {
        public long LoanId { get; }
        public long Unpaid { get; }
        public int ArrearsCloses { get; }

        public LoanPaymentMissed(long simTime, long loanId, long unpaid, int arrearsCloses) : base(simTime)
        {
            LoanId = loanId;
            Unpaid = unpaid;
            ArrearsCloses = arrearsCloses;
        }

        protected override string Describe() { return "LoanPaymentMissed(loan=" + LoanId + ", unpaid=" + Unpaid + ", closes=" + ArrearsCloses + ")"; }
    }

    public sealed class LoanRestructured : SimEvent
    {
        public long LoanId { get; }
        public int NewTerm { get; }
        public long NewPrincipal { get; }

        public LoanRestructured(long simTime, long loanId, int newTerm, long newPrincipal) : base(simTime)
        {
            LoanId = loanId;
            NewTerm = newTerm;
            NewPrincipal = newPrincipal;
        }

        protected override string Describe() { return "LoanRestructured(loan=" + LoanId + ", term=" + NewTerm + ", principal=" + NewPrincipal + ")"; }
    }

    public sealed class LoanDefaulted : SimEvent
    {
        public long LoanId { get; }

        public LoanDefaulted(long simTime, long loanId) : base(simTime) { LoanId = loanId; }

        protected override string Describe() { return "LoanDefaulted(loan=" + LoanId + ")"; }
    }

    /// <summary>A default has been noticed: foreclosure follows after <see cref="LiveHoursLeft"/> hours of play unless cured (ADR 0038).</summary>
    public sealed class FinalNotice : SimEvent
    {
        public IReadOnlyList<long> LoanIds { get; }
        public int LiveHoursLeft { get; }

        public FinalNotice(long simTime, IReadOnlyList<long> loanIds, int liveHoursLeft) : base(simTime)
        {
            LoanIds = new List<long>(loanIds).AsReadOnly();
            LiveHoursLeft = liveHoursLeft;
        }

        protected override string Describe() { return "FinalNotice(loans=" + string.Join(",", LoanIds) + ", hoursLeft=" + LiveHoursLeft + ")"; }
    }

    /// <summary>A bought parcel was taken by foreclosure (spec 002 §5).</summary>
    public sealed class ParcelSeized : SimEvent
    {
        public string ParcelId { get; }
        public long QuickSaleValue { get; }

        public ParcelSeized(long simTime, string parcelId, long quickSaleValue) : base(simTime)
        {
            ParcelId = parcelId;
            QuickSaleValue = quickSaleValue;
        }

        protected override string Describe() { return "ParcelSeized(parcel=" + ParcelId + ", value=" + QuickSaleValue + ")"; }
    }

    /// <summary>Foreclosure ran: what was seized, what it raised, what was written off (spec 005 §3.9).</summary>
    public sealed class ForeclosureCompleted : SimEvent
    {
        public IReadOnlyList<string> Parcels { get; }

        /// <summary>The produce taken, as good ids with grams.</summary>
        public IReadOnlyList<KeyValuePair<string, long>> Produce { get; }
        public long Proceeds { get; }
        public long WrittenOff { get; }
        public int NewScore { get; }

        public ForeclosureCompleted(long simTime, IReadOnlyList<string> parcels, IReadOnlyList<KeyValuePair<string, long>> produce,
            long proceeds, long writtenOff, int newScore) : base(simTime)
        {
            Parcels = new List<string>(parcels).AsReadOnly();
            Produce = new List<KeyValuePair<string, long>>(produce).AsReadOnly();
            Proceeds = proceeds;
            WrittenOff = writtenOff;
            NewScore = newScore;
        }

        protected override string Describe()
        {
            var produce = new List<string>();
            foreach (KeyValuePair<string, long> p in Produce) produce.Add(p.Key + ":" + p.Value);
            return "ForeclosureCompleted(parcels=" + string.Join(",", Parcels) + ", produce=" + string.Join(",", produce)
                + ", proceeds=" + Proceeds + ", writtenOff=" + WrittenOff + ", score=" + NewScore + ")";
        }
    }

    public sealed class CreditScoreChanged : SimEvent
    {
        public int Old { get; }
        public int New { get; }
        public string Reason { get; }

        public CreditScoreChanged(long simTime, int old, int @new, string reason) : base(simTime)
        {
            Old = old;
            New = @new;
            Reason = reason;
        }

        protected override string Describe() { return "CreditScoreChanged(" + Old + "→" + New + ", " + Reason + ")"; }
    }

    /// <summary>The season's interest was added to your savings deposit (spec 019 §3.5).</summary>
    public sealed class InterestEarned : SimEvent
    {
        public long Amount { get; }

        public InterestEarned(long simTime, long amount) : base(simTime) { Amount = amount; }

        protected override string Describe() { return "InterestEarned(" + Amount + ")"; }
    }

    /// <summary>The close, on the season's last day, would take more than your cash holds: <see cref="Short"/> by this much, <see cref="FirstUnpaid"/> the first charge (its <c>CloseDue</c> key) that would go unpaid (spec 019 §3.5).</summary>
    public sealed class CloseShortAhead : SimEvent
    {
        public long Short { get; }
        public string FirstUnpaid { get; }

        public CloseShortAhead(long simTime, long @short, string firstUnpaid) : base(simTime)
        {
            Short = @short;
            FirstUnpaid = firstUnpaid;
        }

        protected override string Describe() { return "CloseShortAhead(short=" + Short + ", first=" + FirstUnpaid + ")"; }
    }

    /// <summary>With your yes, the savings paid what the close's charges needed beyond your cash (spec 019 §3.5).</summary>
    public sealed class CoveredFromSavings : SimEvent
    {
        public long Amount { get; }

        public CoveredFromSavings(long simTime, long amount) : base(simTime) { Amount = amount; }

        protected override string Describe() { return "CoveredFromSavings(" + Amount + ")"; }
    }

    /// <summary>A wealth milestone was reached (spec 019 §3.6), once each: <see cref="Period"/> is the season it happened in, <see cref="Past"/> when it was found from a report kept before the update.</summary>
    public sealed class MilestoneReached : SimEvent
    {
        public string Id { get; }
        public long Period { get; }
        public bool Past { get; }

        public MilestoneReached(long simTime, string id, long period, bool past) : base(simTime)
        {
            Id = id;
            Period = period;
            Past = past;
        }

        protected override string Describe() { return "MilestoneReached(" + Id + ", period=" + Period + (Past ? ", past" : "") + ")"; }
    }
}
