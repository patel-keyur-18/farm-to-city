using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Maths;
using Sim.Core.Saves;

namespace Sim.Core.Finance
{
    /// <summary>The savings deposit's own state (spec 019 §3.5, ADR 0101 §2): the standing choice, a yes for one close, and whether the warning was given.</summary>
    public sealed class SavingsState
    {
        /// <summary>The standing choice <em>If cash runs short at the close</em>: <c>false</c> is <em>Ask me</em> (the default), <c>true</c> is <em>Always pay from my savings</em>.</summary>
        public bool Always { get; internal set; }

        /// <summary>The season a yes covers, plus one (0: no yes). A yes lasts one close.</summary>
        public long ConsentFor { get; internal set; }

        /// <summary>The warning for the coming close has been given and the close is still short. Only the notification reads it, so the hash leaves it out.</summary>
        public bool Warned { get; internal set; }

        internal bool IsDefault { get { return !Always && ConsentFor == 0; } }

        internal SavingsState Clone() { return new SavingsState { Always = Always, ConsentFor = ConsentFor, Warned = Warned }; }

        internal void WriteTo(IStateWriter h)
        {
            h.WriteBool(Always);
            h.WriteLong(ConsentFor);
            if (!h.Hashing) h.WriteBool(Warned);
        }

        internal static SavingsState ReadFrom(SaveReader r) { return new SavingsState { Always = r.ReadBool(), ConsentFor = r.ReadLong(), Warned = r.ReadBool() }; }
    }

    /// <summary>
    /// The co-op bank's savings deposit (spec 019 §3.5, ADR 0101 §2): put in and take out any time, interest at each close before the
    /// statements, never collateral and never seized. Nothing leaves it for the close without the player's yes (a yes for that close, or the
    /// standing <em>Always pay from my savings</em>).
    /// </summary>
    public static class SavingsRules
    {
        public static SavingsState State(Simulation sim) { return sim.State.Finance?.Savings; }

        public static long Balance(Simulation sim) { return sim.State.Ledger.Balance(Account.BankDeposit); }

        /// <summary>What the next close adds to the deposit.</summary>
        public static long NextInterest(Simulation sim) { return Money.MulRate(Balance(sim), sim.Config.Game.Ledger.SavingsInterest); }

        public static bool Always(Simulation sim) { return State(sim) != null && State(sim).Always; }

        /// <summary>A yes was given for the close that ends the season in progress.</summary>
        public static bool Consented(Simulation sim) { return State(sim) != null && State(sim).ConsentFor == sim.Period + 1; }

        /// <summary>Whether the savings will cover a shortfall at the close: the standing choice, or a yes for this close.</summary>
        public static bool WillCover(Simulation sim) { return Always(sim) || Consented(sim); }

        /// <summary>
        /// What the next close takes from your cash beyond what you have: the sum of its charges less your cash (never under 0), and the
        /// first charge your cash can't meet in the close's order (null when cash covers all).
        /// </summary>
        public static long Shortfall(Simulation sim, out CloseDue firstUnpaid, bool atClose = false)
        {
            long cash = sim.State.Ledger.Cash, left = cash, total = 0;
            firstUnpaid = null;
            foreach (CloseDue due in CloseDues.Next(sim, atClose))
            {
                total += due.Amount;
                if (firstUnpaid == null && due.Amount > left) firstUnpaid = due;
                left -= System.Math.Min(left, due.Amount);
            }
            return System.Math.Max(0, total - cash);
        }

        /// <summary>Cash → the deposit (<see cref="DepositSavings"/>).</summary>
        internal static CommandResult Put(Simulation sim, long amount)
        {
            Ledger l = sim.State.Ledger;
            if (amount < sim.Config.Game.Ledger.SavingsMinimum) return CommandResult.Reject(RejectReason.BelowSavingsMinimum);
            if (!l.CanAfford(amount)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            l.Post(sim.State.Clock.SimTime, sim.Period, "ledger.savingsIn", "savings", new Posting(Account.BankDeposit, amount), new Posting(Account.Cash, -amount));
            return CommandResult.Ok;
        }

        /// <summary>The deposit → cash (<see cref="WithdrawSavings"/>).</summary>
        internal static CommandResult Take(Simulation sim, long amount)
        {
            Ledger l = sim.State.Ledger;
            if (amount <= 0) return CommandResult.Reject(RejectReason.BelowSavingsMinimum);
            if (amount > Balance(sim)) return CommandResult.Reject(RejectReason.InsufficientDeposit);
            l.Post(sim.State.Clock.SimTime, sim.Period, "ledger.savingsOut", "savings", new Posting(Account.Cash, amount), new Posting(Account.BankDeposit, -amount));
            return CommandResult.Ok;
        }

        /// <summary>A yes for the coming close (<see cref="CoverFromSavings"/>): only while the close would come up short.</summary>
        internal static CommandResult Consent(Simulation sim, bool yes)
        {
            SavingsState s = State(sim);
            if (s == null) return CommandResult.Reject(RejectReason.NothingShort);
            if (yes)
            {
                if (Shortfall(sim, out _) == 0) return CommandResult.Reject(RejectReason.NothingShort);
                s.ConsentFor = sim.Period + 1;
            }
            else s.ConsentFor = 0;
            return CommandResult.Ok;
        }

        /// <summary>At the close, once the season's interest on loans is accrued and before the statements: the interest on the deposit, added to it (so it compounds).</summary>
        internal static void AddInterest(Simulation sim, long closing)
        {
            long interest = NextInterest(sim);
            if (interest <= 0) return;
            long t = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(t, closing, "ledger.savingsInterest", "savings", new Posting(Account.BankDeposit, interest), new Posting(Account.InterestOnSavings, -interest));
            sim.Emit(new InterestEarned(t, interest));
        }

        /// <summary>
        /// At the close, before the new season's charges are taken: with the player's yes (this close's, or the standing one), what the charges
        /// need beyond cash comes from the deposit, never more than it holds. Without a yes nothing moves and what can't be paid goes unpaid in
        /// its order, as before. The yes is spent either way.
        /// </summary>
        internal static void Cover(Simulation sim, long closing)
        {
            SavingsState s = State(sim);
            if (s == null) return;
            bool yes = s.Always || s.ConsentFor == closing + 1;
            s.ConsentFor = 0;
            s.Warned = false;
            long deposit = Balance(sim);
            if (!yes || deposit == 0) return;
            long take = System.Math.Min(deposit, Shortfall(sim, out _, atClose: true));
            if (take <= 0) return;
            long t = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(t, sim.Period, "ledger.savingsCover", "savings", new Posting(Account.Cash, take), new Posting(Account.BankDeposit, -take));
            sim.Emit(new CoveredFromSavings(t, take));
        }

        /// <summary>
        /// Hourly: from 00:00 of a season's last day, once the close would come up short even with what your savings will cover, one warning
        /// (<see cref="CloseShortAhead"/>); something done later that makes it short again warns again.
        /// </summary>
        internal static void Hourly(Simulation sim)
        {
            SavingsState s = State(sim);
            if (s == null || sim.Config.Game == null) return;
            long S = sim.Clock.SecondsPerSeason;
            if (S - sim.State.Clock.SimTime % S > Definitions.ClockDef.SecondsPerDay) return;
            long short1 = Shortfall(sim, out CloseDue first);
            long uncovered = short1 - (WillCover(sim) ? Balance(sim) : 0);
            if (uncovered <= 0) { s.Warned = false; return; }
            if (s.Warned) return;
            s.Warned = true;
            sim.Emit(new CloseShortAhead(sim.State.Clock.SimTime, short1, first == null ? "" : first.Key));
        }
    }
}
