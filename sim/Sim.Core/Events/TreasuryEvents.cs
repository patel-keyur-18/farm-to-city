using Sim.Core.Maths;

namespace Sim.Core.Events
{
    /// <summary>The Township arrived and the treasury opened with the district's start-up grant (spec 021 §3.1).</summary>
    public sealed class TreasuryOpened : SimEvent
    {
        public long Grant { get; }

        public TreasuryOpened(long simTime, long grant) : base(simTime) { Grant = grant; }

        protected override string Describe() { return "TreasuryOpened(grant=" + Grant + ")"; }
    }

    /// <summary>A close's grants (spec 021 §3.3): what was paid untied and tied, and the tied part withheld (no staffed clinic).</summary>
    public sealed class GrantPaid : SimEvent
    {
        public long Untied { get; }
        public long Tied { get; }
        public long TiedWithheld { get; }

        public GrantPaid(long simTime, long untied, long tied, long tiedWithheld) : base(simTime)
        {
            Untied = untied;
            Tied = tied;
            TiedWithheld = tiedWithheld;
        }

        protected override string Describe() { return "GrantPaid(untied=" + Untied + ", tied=" + Tied + ", withheld=" + TiedWithheld + ")"; }
    }

    /// <summary>A department was paid at the close (spec 021 §3.4); <see cref="InPass2"/> when some of it came after every department's budget.</summary>
    public sealed class DepartmentPaid : SimEvent
    {
        public int Department { get; }
        public long Paid { get; }
        public long Needs { get; }
        public bool InPass2 { get; }

        public DepartmentPaid(long simTime, int department, long paid, long needs, bool inPass2) : base(simTime)
        {
            Department = department;
            Paid = paid;
            Needs = needs;
            InPass2 = inPass2;
        }

        protected override string Describe() { return "DepartmentPaid(dept=" + Department + ", paid=" + Paid + ", needs=" + Needs + ", pass2=" + InPass2 + ")"; }
    }

    /// <summary>A department couldn't be paid in full at the close; the shortfall is not carried (spec 021 §3.4).</summary>
    public sealed class DepartmentUnpaid : SimEvent
    {
        public int Department { get; }
        public long Amount { get; }

        public DepartmentUnpaid(long simTime, int department, long amount) : base(simTime)
        {
            Department = department;
            Amount = amount;
        }

        protected override string Describe() { return "DepartmentUnpaid(dept=" + Department + ", amount=" + Amount + ")"; }
    }

    /// <summary>A department's service moved at the close (spec 021 §3.5).</summary>
    public sealed class ConditionChanged : SimEvent
    {
        public int Department { get; }
        public Fixed From { get; }
        public Fixed To { get; }

        public ConditionChanged(long simTime, int department, Fixed from, Fixed to) : base(simTime)
        {
            Department = department;
            From = from;
            To = to;
        }

        protected override string Describe() { return "ConditionChanged(dept=" + Department + ", from=" + From + ", to=" + To + ")"; }
    }

    /// <summary>The founder gave to the town (spec 021 §3.8).</summary>
    public sealed class GiftGiven : SimEvent
    {
        public long Amount { get; }

        public GiftGiven(long simTime, long amount) : base(simTime) { Amount = amount; }

        protected override string Describe() { return "GiftGiven(amount=" + Amount + ")"; }
    }

    /// <summary>The treasury commissioned a capital work (spec 021 §3.7).</summary>
    public sealed class WorksCommissioned : SimEvent
    {
        public string WorkId { get; }
        public long Cost { get; }

        public WorksCommissioned(long simTime, string workId, long cost) : base(simTime)
        {
            WorkId = workId;
            Cost = cost;
        }

        protected override string Describe() { return "WorksCommissioned(work=" + WorkId + ", cost=" + Cost + ")"; }
    }

    /// <summary>A budget window opened (spec 021 §3.6): the set window at the opening and each Spring close, the revision at Monsoon.</summary>
    public sealed class BudgetWindowOpened : SimEvent
    {
        public bool Revision { get; }

        public BudgetWindowOpened(long simTime, bool revision) : base(simTime) { Revision = revision; }

        protected override string Describe() { return "BudgetWindowOpened(revision=" + Revision + ")"; }
    }

    /// <summary>The player changed a department's budget in an open window (spec 021 §3.6).</summary>
    public sealed class BudgetSet : SimEvent
    {
        public int Department { get; }
        public long Amount { get; }

        public BudgetSet(long simTime, int department, long amount) : base(simTime)
        {
            Department = department;
            Amount = amount;
        }

        protected override string Describe() { return "BudgetSet(dept=" + Department + ", amount=" + Amount + ")"; }
    }

    /// <summary>The player took the proposal as it stands, closing the window (spec 021 §3.6).</summary>
    public sealed class BudgetAccepted : SimEvent
    {
        public BudgetAccepted(long simTime) : base(simTime) { }

        protected override string Describe() { return "BudgetAccepted"; }
    }

    /// <summary>On a season's last day the close would leave a department unpaid (spec 021 §3.11).</summary>
    public sealed class TreasuryShortAhead : SimEvent
    {
        public long Short { get; }
        public int FirstUnpaid { get; }

        public TreasuryShortAhead(long simTime, long @short, int firstUnpaid) : base(simTime)
        {
            Short = @short;
            FirstUnpaid = firstUnpaid;
        }

        protected override string Describe() { return "TreasuryShortAhead(short=" + Short + ", first=" + FirstUnpaid + ")"; }
    }

    /// <summary>A tax was billed at the close (spec 022 §3.8): what was billed and what the office collected (and the payer paid or owes).</summary>
    public sealed class TaxBilled : SimEvent
    {
        public int Tax { get; }
        public string Payer { get; }
        public long Billed { get; }
        public long Collected { get; }

        public TaxBilled(long simTime, int tax, string payer, long billed, long collected) : base(simTime)
        {
            Tax = tax;
            Payer = payer;
            Billed = billed;
            Collected = collected;
        }

        protected override string Describe() { return "TaxBilled(tax=" + Tax + ", payer=" + Payer + ", billed=" + Billed + ", collected=" + Collected + ")"; }
    }

    /// <summary>A payer couldn't pay all it owed the town at the close: the rest is carried and paid first at the next one (spec 022 §3.1).</summary>
    public sealed class TaxOwed : SimEvent
    {
        public string Payer { get; }
        public long Amount { get; }

        public TaxOwed(long simTime, string payer, long amount) : base(simTime)
        {
            Payer = payer;
            Amount = amount;
        }

        protected override string Describe() { return "TaxOwed(payer=" + Payer + ", amount=" + Amount + ")"; }
    }

    /// <summary>A tax rate was set for the next season, or the pending one undone (spec 022 §3.4).</summary>
    public sealed class TaxRateSet : SimEvent
    {
        public int Tax { get; }
        public Fixed From { get; }
        public Fixed To { get; }

        public TaxRateSet(long simTime, int tax, Fixed from, Fixed to) : base(simTime)
        {
            Tax = tax;
            From = from;
            To = to;
        }

        protected override string Describe() { return "TaxRateSet(tax=" + Tax + ", from=" + From + ", to=" + To + ")"; }
    }

    /// <summary>A policy was enacted (spec 022 §3.6).</summary>
    public sealed class PolicyEnacted : SimEvent
    {
        public int Policy { get; }
        public PolicyEnacted(long simTime, int policy) : base(simTime) { Policy = policy; }
        protected override string Describe() { return "PolicyEnacted(policy=" + Policy + ")"; }
    }

    /// <summary>A policy was repealed: it ends at the next close (spec 022 §3.6).</summary>
    public sealed class PolicyRepealed : SimEvent
    {
        public int Policy { get; }
        public PolicyRepealed(long simTime, int policy) : base(simTime) { Policy = policy; }
        protected override string Describe() { return "PolicyRepealed(policy=" + Policy + ")"; }
    }

    /// <summary>A policy's lag is over and its effect has begun (spec 022 §3.6).</summary>
    public sealed class PolicyStarted : SimEvent
    {
        public int Policy { get; }
        public PolicyStarted(long simTime, int policy) : base(simTime) { Policy = policy; }
        protected override string Describe() { return "PolicyStarted(policy=" + Policy + ")"; }
    }

    /// <summary>The close couldn't pay a policy in full: it is paused for the season (spec 022 §3.6).</summary>
    public sealed class PolicyPaused : SimEvent
    {
        public int Policy { get; }
        public PolicyPaused(long simTime, int policy) : base(simTime) { Policy = policy; }
        protected override string Describe() { return "PolicyPaused(policy=" + Policy + ")"; }
    }

    /// <summary>A small business grant came to nothing: spent, no shop (spec 022 §3.5).</summary>
    public sealed class GrantWasted : SimEvent
    {
        public GrantWasted(long simTime) : base(simTime) { }
        protected override string Describe() { return "GrantWasted()"; }
    }

    /// <summary>A small business grant helped a newcomer open a shop (spec 022 §3.5).</summary>
    public sealed class GrantOpenedShop : SimEvent
    {
        public GrantOpenedShop(long simTime) : base(simTime) { }
        protected override string Describe() { return "GrantOpenedShop()"; }
    }
}
