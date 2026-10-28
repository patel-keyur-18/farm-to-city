using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.People;

namespace Sim.Core.Business
{
    /// <summary>
    /// A business's own books (spec 016 §3.2): the founder's cash put in and taken out, the season's report and close to retained
    /// earnings, and its equity in the founder's net worth.
    /// </summary>
    public static class BusinessBooks
    {
        /// <summary>Closed seasons kept on a business: enough for plan vs actual (§3.3).</summary>
        public const int KeptReports = 4;

        /// <summary>Cash in (§3.2): founder Dr 1800 Investment in businesses, Cr Cash; business Dr Cash, Cr Owner's capital.</summary>
        internal static CommandResult PutCash(Simulation sim, long id, long amount)
        {
            Business b = sim.State.Businesses?.Find(id);
            RejectReason r = Check(b, amount);
            if (r == RejectReason.None && !sim.State.Ledger.CanAfford(amount)) r = RejectReason.InsufficientFunds;
            if (r != RejectReason.None) return CommandResult.Reject(r);
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + id;
            sim.State.Ledger.Post(now, sim.Period, "ledger.putCash", reference, new Posting(Account.InvestmentInBusinesses, amount), new Posting(Account.Cash, -amount));
            b.Ledger.Post(now, sim.Period, "ledger.putCash", reference, new Posting(Account.Cash, amount), new Posting(Account.OwnersCapital, -amount));
            Sweep(sim, b);
            return CommandResult.Ok;
        }

        /// <summary>
        /// Drawings (§3.2): business Dr Owner's capital, Cr Cash; founder Dr Cash, Cr 1800 down to what this business still holds of
        /// your capital, the rest Cr 4300 Business drawings income.
        /// </summary>
        internal static CommandResult TakeCash(Simulation sim, long id, long amount)
        {
            Business b = sim.State.Businesses?.Find(id);
            RejectReason r = Check(b, amount);
            if (r == RejectReason.None && !b.Ledger.CanAfford(amount)) r = RejectReason.InsufficientFunds;
            if (r != RejectReason.None) return CommandResult.Reject(r);
            Withdraw(sim, b, amount);
            return CommandResult.Ok;
        }

        /// <summary>The entries of drawings: business Dr Owner's capital, Cr Cash; you Dr Cash, Cr 1800 down to your capital in it, the rest Cr 4300.</summary>
        private static void Withdraw(Simulation sim, Business b, long amount)
        {
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            long atCost = System.Math.Min(amount, System.Math.Max(0, CapitalIn(b)));
            b.Ledger.Post(now, sim.Period, "ledger.takeCash", reference, new Posting(Account.OwnersCapital, amount), new Posting(Account.Cash, -amount));
            var postings = new List<Posting> { new Posting(Account.Cash, amount) };
            if (atCost > 0) postings.Add(new Posting(Account.InvestmentInBusinesses, -atCost));
            if (amount > atCost) postings.Add(new Posting(Account.BusinessDrawingsIncome, atCost - amount));
            sim.State.Ledger.Post(now, sim.Period, "ledger.takeCash", reference, postings.ToArray());
        }

        private static RejectReason Check(Business b, long amount)
        {
            if (b == null) return RejectReason.UnknownBusiness;
            if (b.Status == BusinessStatus.Closed) return RejectReason.BusinessClosed;
            return amount <= 0 ? RejectReason.InvalidArgument : RejectReason.None;
        }

        /// <summary>Your capital still in the business: put in less taken out (its Owner's capital).</summary>
        public static long CapitalIn(Business b) { return -b.Ledger.Balance(Account.OwnersCapital); }

        /// <summary>Assets − liabilities on the business's books (§3.2); net worth counts it never below 0.</summary>
        public static long Equity(Business b) { return b.Ledger.TotalOf(AccountGroup.Assets) - b.Ledger.TotalOf(AccountGroup.Liabilities); }

        /// <summary>Every business at its equity, each never below 0 (§3.2, AC-5).</summary>
        public static long AtEquity(Simulation sim)
        {
            long sum = 0;
            if (sim.State.Businesses != null)
                foreach (Business b in sim.State.Businesses.Businesses) sum += System.Math.Max(0, Equity(b));
            return sum;
        }

        /// <summary>A season's report from the business's balances and its entries of <paramref name="period"/>: the closed season's, or this one so far.</summary>
        public static BusinessReport Report(Business b, long period)
        {
            var balances = new List<KeyValuePair<Account, long>>();
            foreach (Account a in Accounts.All)
                if (b.Ledger.Balance(a) != 0) balances.Add(new KeyValuePair<Account, long>(a, b.Ledger.Balance(a)));
            long inflows = 0, outflows = 0;
            foreach (JournalEntry e in b.Ledger.Entries)
            {
                if (e.Period != period) continue;
                foreach (Posting p in e.Postings)
                {
                    if (p.Account != Account.Cash) continue;
                    if (p.Amount > 0) inflows += p.Amount;
                    else outflows -= p.Amount;
                }
            }
            return new BusinessReport
            {
                Period = period, Balances = balances.AsReadOnly(), Opening = b.SeasonOpeningCash, Inflows = inflows, Outflows = outflows,
                AreaGrams = b.AreaSeason, OwnGrams = b.OwnSeason,
            };
        }

        /// <summary>Net profit on a report's P&amp;L: income less expenses (§3.2; a profitable season is one above 0).</summary>
        public static long NetProfit(BusinessReport r)
        {
            long sum = 0;
            foreach (KeyValuePair<Account, long> kv in r.Balances)
            {
                AccountGroup g = Accounts.GroupOf(kv.Key);
                if (g == AccountGroup.Income || g == AccountGroup.Expenses) sum -= kv.Value;
            }
            return sum;
        }

        /// <summary>The season's report, then income and expenses to retained earnings (spec 005 §3.4 steps 1–3), kept and checked.</summary>
        internal static void CloseSeason(Simulation sim, Business b, long closing)
        {
            BusinessReport report = Report(b, closing);
            FinanceSystem.CloseToRetainedEarnings(b.Ledger, sim.State.Clock.SimTime, closing);
            string failure = b.Ledger.Reconcile();
            if (failure != null)
            {
                if (sim.Config.Options.StrictChecks) throw new System.InvalidOperationException("Business " + b.Id + " books failed to reconcile: " + failure);
                sim.Emit(new LedgerReconciliationFailed(sim.State.Clock.SimTime, "business " + b.Id + ": " + failure));
            }
            b.Reports.Add(report);
            if (b.Reports.Count > KeptReports) b.Reports.RemoveAt(0);
            b.SeasonOpeningCash = b.Ledger.Cash;
            b.AreaSeason = 0;
            b.OwnSeason = 0;
        }

        // ---- the overdraft (§3.8, §3.10) ----

        /// <summary>The overdraft's limit: <c>overdraftLimit</c> × a season's running costs at its settings and today's prices, to 100 coins.</summary>
        public static long OverdraftLimit(Simulation sim, Business b)
        {
            BusinessRules.Projection p = BusinessRules.Projected(sim, BusinessRules.TypeOf(sim, b), b.Worker == WorkerKind.Founder, b.OwnPerDay, b.Fee);
            long limit = Money.MulRate(p.Costs - p.Depreciation, BusinessRules.Defs(sim).OverdraftLimit);
            return limit - limit % (100 * Money.Coin);
        }

        /// <summary>What the business owes on its overdraft now.</summary>
        public static long Overdrawn(Business b) { return -b.Ledger.Balance(Account.Overdraft); }

        /// <summary>What it can pay now: its cash and what's left of its overdraft.</summary>
        public static long CanSpend(Simulation sim, Business b) { return b.Ledger.Cash + System.Math.Max(0, OverdraftLimit(sim, b) - Overdrawn(b)); }

        /// <summary>Draws on the overdraft for what its cash doesn't cover of <paramref name="amount"/>, up to the limit; true when it's all in cash.</summary>
        internal static bool Draw(Simulation sim, Business b, long amount, long period = -1)
        {
            long need = amount - b.Ledger.Cash;
            if (need <= 0) return true;
            long draw = System.Math.Min(need, OverdraftLimit(sim, b) - Overdrawn(b));
            if (draw > 0)
                b.Ledger.Post(sim.State.Clock.SimTime, period < 0 ? sim.Period : period, "ledger.overdraftDrawn", "business:" + b.Id,
                    new Posting(Account.Cash, draw), new Posting(Account.Overdraft, -draw));
            return b.Ledger.Cash >= amount;
        }

        /// <summary>Cash coming in pays the overdraft off first, as a bank's overdraft account does.</summary>
        internal static void Sweep(Simulation sim, Business b)
        {
            long repay = System.Math.Min(b.Ledger.Cash, Overdrawn(b));
            if (repay > 0)
                b.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.overdraftRepaid", "business:" + b.Id,
                    new Posting(Account.Overdraft, repay), new Posting(Account.Cash, -repay));
        }

        /// <summary>
        /// In trouble (§3.10): open with its overdraft used to the limit (inputs stop), or with no worker since a wage went unpaid.
        /// </summary>
        public static bool Short(Simulation sim, Business b)
        {
            if (b.Status != BusinessStatus.Open) return false;
            return b.Worker == WorkerKind.None || (Overdrawn(b) > 0 && Overdrawn(b) >= OverdraftLimit(sim, b));
        }

        // ---- the business loan (§3.8) ----

        /// <summary>The business's bank loan, or null.</summary>
        public static Loan LoanOf(Simulation sim, Business b) { return b.LoanId == 0 || sim.State.Credit == null ? null : sim.State.Credit.Find(b.LoanId); }

        /// <summary>The most a business loan can be: <c>businessLtv</c> of its building and equipment at today's prices, to 1,000 coins.</summary>
        public static long MaxLoan(Simulation sim, BusinessTypeDef t)
        {
            long max = Money.MulRate(BusinessRules.Cost(sim, t), BusinessRules.Defs(sim).BusinessLtv);
            return max - max % (1000 * Money.Coin);
        }

        /// <summary>Whether a business loan can be made: the business lender, up to <see cref="MaxLoan"/>, a term up to <c>termSeasons</c>, your score.</summary>
        internal static RejectReason CheckLoan(Simulation sim, BusinessTypeDef t, Financing f, out int lender)
        {
            lender = -1;
            CreditDefs credit = sim.Config.Game.Credit;
            if (credit == null || sim.State.Credit == null || f.LenderId != BusinessRules.Defs(sim).LenderId) return RejectReason.UnknownLender;
            lender = credit.IndexOfLender(f.LenderId);
            if (lender < 0) return RejectReason.UnknownLender;
            LenderDef def = credit.Lenders[lender];
            if (f.Amount <= 0 || f.Amount % Money.Coin != 0) return RejectReason.InvalidAmount;
            if (f.Amount < def.MinAmount) return RejectReason.BelowMinimum;
            if (f.Amount > MaxLoan(sim, t)) return RejectReason.OverLimit;
            if (f.TermSeasons < def.MinTerm || f.TermSeasons > BusinessRules.Defs(sim).TermSeasons) return RejectReason.InvalidTerm;
            if (sim.State.Credit.Score < def.MinScore) return RejectReason.ScoreTooLow;
            return RejectReason.None;
        }

        // ---- closing (§3.10) ----

        /// <summary>
        /// Closes a business (§3.10): its worker leaves; its stock goes to the trader at today's price; its building and equipment sell at
        /// <c>closingSaleShare</c> of book value; its overdraft and loan are repaid; what it still owes becomes your bank loan over
        /// <c>guaranteeTermSeasons</c> (you guaranteed it); what's left comes to you; your capital not returned is your loss.
        /// </summary>
        internal static void CloseDown(Simulation sim, Business b)
        {
            BusinessDefs d = BusinessRules.Defs(sim);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            Ledger l = b.Ledger;
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            Loan loan = LoanOf(sim, b);
            if (loan != null && loan.IsOpen) CreditSystem.Accrue(sim, loan, sim.Period);
            BusinessRules.LoseWorker(sim, b);

            for (int g = 0; g < b.Stock.Length; g++)
                if (b.Stock[g] > 0) BusinessRules.Sold(sim, b, g, b.Stock[g], MarketRules.Value(sim, g, b.Stock[g], MarketRules.Floor(sim, g)));

            long building = l.Balance(Account.Buildings), equipment = l.Balance(Account.Equipment), worn = l.Balance(Account.AccumulatedDepreciation);
            long book = building + equipment + worn, sale = Money.RoundToCoin(Money.MulRate(book, d.ClosingSaleShare));
            if (building + equipment > 0)
                l.Post(now, sim.Period, "ledger.businessSold", reference, new Posting(Account.Cash, sale), new Posting(Account.AccumulatedDepreciation, -worn),
                    new Posting(Account.Buildings, -building), new Posting(Account.Equipment, -equipment), new Posting(Account.LossOnAssetSales, book - sale));

            long overdraft = Overdrawn(b), owed = loan != null && loan.IsOpen ? CreditRules.OwedNow(sim, loan) : 0;
            long guaranteed = 0;
            if (overdraft + owed > l.Cash)
            {
                long shortfall = overdraft + owed - l.Cash;
                guaranteed = (shortfall + Money.Coin - 1) / Money.Coin * Money.Coin;
                CreditSystem.Create(sim, sim.Config.Game.Credit.IndexOfLender(d.LenderId), guaranteed, d.GuaranteeTermSeasons);
                sim.State.Ledger.Post(now, sim.Period, "ledger.guarantee", reference, new Posting(Account.InvestmentInBusinesses, guaranteed), new Posting(Account.Cash, -guaranteed));
                l.Post(now, sim.Period, "ledger.guarantee", reference, new Posting(Account.Cash, guaranteed), new Posting(Account.OwnersCapital, -guaranteed));
            }
            if (overdraft > 0) l.Post(now, sim.Period, "ledger.overdraftRepaid", reference, new Posting(Account.Overdraft, overdraft), new Posting(Account.Cash, -overdraft));
            if (owed > 0) CreditSystem.Pay(sim, loan, owed, "ledger.loanPayment", out _, out _, out _);

            long toYou = l.Cash;
            if (toYou > 0) Withdraw(sim, b, toYou);
            long lost = CapitalIn(b);
            if (lost > 0)
                sim.State.Ledger.Post(now, sim.Period, "ledger.businessLost", reference, new Posting(Account.LossOnAssetSales, lost), new Posting(Account.InvestmentInBusinesses, -lost));

            b.Status = BusinessStatus.Closed;
            Land.LandSystem.SetOccupied(sim, b.TileId, false);
            sim.Emit(new BusinessClosed(now, b.Id, t.Id, toYou, guaranteed));
        }
    }
}
