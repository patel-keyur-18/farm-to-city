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
            if (r == RejectReason.None && (!b.Ledger.CanAfford(amount) || amount > Drawable(b))) r = RejectReason.InsufficientFunds;
            if (r != RejectReason.None) return CommandResult.Reject(r);
            Withdraw(sim, b, amount);
            return CommandResult.Ok;
        }

        /// <summary>
        /// The most you can draw: its cash less what outside owners have put in (ADR 0164). Their money is not yours to take; if the business loses money, they lose with it
        /// at the close, but they cannot be left with nothing by drawing their cash out first.
        /// </summary>
        public static long Drawable(Business b) { return System.Math.Max(0, b.Ledger.Cash - OwnerRules.Liability(b)); }

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
            if (NetProfit(report) > 0) { b.ProfitableSeasons++; History.ChronicleRules.Profit(sim, b); } // spec 029 §3.2: the first profit
            if (b.Reports.Count > KeptReports) b.Reports.RemoveAt(0);
            b.SeasonOpeningCash = b.Ledger.Cash;
            b.AreaSeason = 0;
            b.KilnSeason = 0;
            b.OwnSeason = 0;
            b.SeasonCustomers = 0;
            b.SeasonServed = 0;
            b.SeasonAtPrice = 0;
        }

        // ---- the overdraft (§3.8, §3.10) ----

        /// <summary>The overdraft's limit: <c>overdraftLimit</c> × a season's running costs at its settings and today's prices, to 100 coins.</summary>
        public static long OverdraftLimit(Simulation sim, Business b)
        {
            BusinessRules.Projection p = BusinessRules.Projected(sim, BusinessRules.TypeOf(sim, b), b.Worker == WorkerKind.Founder, b.OwnPerDay, b.Fee, 0, null, b.Pitch, b.TileId);
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
        internal static void CloseDown(Simulation sim, Business b, SaleTerms sale = null)
        {
            BusinessDefs d = BusinessRules.Defs(sim);
            BusinessTypeDef t = BusinessRules.TypeOf(sim, b);
            Ledger l = b.Ledger;
            long now = sim.State.Clock.SimTime;
            string reference = "business:" + b.Id;
            Loan loan = LoanOf(sim, b);
            if (loan != null && loan.IsOpen) CreditSystem.Accrue(sim, loan, sim.Period);
            Town.InsuranceRules.Drop(sim, b.Id); // a closed or sold business keeps no policy (ADR 0158)
            Town.EventRules.BusinessGone(sim, b.Id); // and the cards aimed at it end quietly
            OwnerRules.Gone(sim, b.Id); // and the offers for it
            People.Worker boss = ManagerRules.Of(sim, b.Id); // a closing business lets its manager go first: he works it himself, and keeps the season's pay
            if (boss != null)
            {
                ManagerRules.Remove(sim, boss);
                sim.Emit(new Events.ManagerLeft(now, b.Id, boss.Id, 0));
            }
            BusinessRules.LoseWorker(sim, b);
            InvoiceRules.CollectAll(sim, b); // what its customers owe it, now (spec 028 §3.1)
            KhataRules.Settle(sim, b); // and its households' khata (spec 028 §3.10)

            KilnRules.Clear(sim, b); // a closing kiln's yard goes to the trader
            TuitionRules.Release(b); // a closing tuition centre's pupils go
            long held = Equity(b) + OwnerRules.Liability(b); // what a sale's buyer takes over before the premium he pays, outside owners' money included (spec 028 §3.3)
            for (int g = 0; g < b.Stock.Length; g++)
            {
                if (b.Stock[g] <= 0) continue;
                if (sale == null) BusinessRules.Sold(sim, b, g, b.Stock[g], MarketRules.Value(sim, g, b.Stock[g], MarketRules.Floor(sim, g)));
                else
                {
                    long cost = b.StockCost[g]; // a sale takes its stock at cost
                    b.Stock[g] = 0;
                    b.StockCost[g] = 0;
                    if (cost > 0) l.Post(now, sim.Period, "ledger.stockToBuyer", reference, new Posting(Account.Cash, cost), new Posting(Account.BusinessStock, -cost));
                }
            }

            long building = l.Balance(Account.Buildings), equipment = l.Balance(Account.Equipment), worn = l.Balance(Account.AccumulatedDepreciation);
            long book = building + equipment + worn, proceeds = sale == null ? Money.RoundToCoin(Money.MulRate(book, d.ClosingSaleShare)) : book;
            if (building + equipment > 0)
            {
                var parts = new List<Posting> { new Posting(Account.Cash, proceeds), new Posting(Account.AccumulatedDepreciation, -worn), new Posting(Account.Buildings, -building), new Posting(Account.Equipment, -equipment) };
                if (book != proceeds) parts.Add(new Posting(Account.LossOnAssetSales, book - proceeds));
                l.Post(now, sim.Period, sale == null ? "ledger.businessSold" : "ledger.buildingsToBuyer", reference, parts.ToArray());
            }

            long overdraft = Overdrawn(b), owed = loan != null && loan.IsOpen ? CreditRules.OwedNow(sim, loan) : 0;
            long taxOwed = -l.Balance(Account.TaxOwed); // spec 022 §3.2: closing the business never wipes the tax it still owes
            long suppliers = InvoiceRules.Payables(b); // and what it owes other businesses on terms (spec 028 §3.1)
            long guaranteed = 0;
            if (overdraft + owed + taxOwed + suppliers > l.Cash)
            {
                long shortfall = overdraft + owed + taxOwed + suppliers - l.Cash;
                guaranteed = (shortfall + Money.Coin - 1) / Money.Coin * Money.Coin;
                CreditSystem.Create(sim, sim.Config.Game.Credit.IndexOfLender(d.LenderId), guaranteed, d.GuaranteeTermSeasons);
                sim.State.Ledger.Post(now, sim.Period, "ledger.guarantee", reference, new Posting(Account.InvestmentInBusinesses, guaranteed), new Posting(Account.Cash, -guaranteed));
                l.Post(now, sim.Period, "ledger.guarantee", reference, new Posting(Account.Cash, guaranteed), new Posting(Account.OwnersCapital, -guaranteed));
            }
            if (overdraft > 0) l.Post(now, sim.Period, "ledger.overdraftRepaid", reference, new Posting(Account.Overdraft, overdraft), new Posting(Account.Cash, -overdraft));
            if (owed > 0) CreditSystem.Pay(sim, loan, owed, "ledger.loanPayment", out _, out _, out _);
            if (taxOwed > 0) Town.TaxRules.PayOwed(sim, l, reference, Town.TaxKind.Business, now);
            if (suppliers > 0) InvoiceRules.PayAll(sim, b);

            long gain = 0, tax = 0, outside = 0;
            if (sale != null)
            {
                gain = System.Math.Max(0, sale.Price - held); // the buyer pays the price for what the business holds: the rest is the gain
                if (gain > 0) l.Post(now, sim.Period, "ledger.saleGain", reference, new Posting(Account.Cash, gain), new Posting(Account.GainOnAssetSales, -gain));
                outside = OwnerRules.Settle(sim, b); // investor and partner first, a part of what is left (spec 028 §3.4); the tax then falls on your gain alone
                long taxBefore = l.Balance(Account.BusinessTax);
                Town.TaxRules.BillBusiness(sim, b, sim.Period); // the season's profit with the gain, at the business tax (ADR 0109)
                tax = l.Balance(Account.BusinessTax) - taxBefore;
            }
            else outside = OwnerRules.Settle(sim, b); // a plain closing pays them their part of what is left too
            long toYou = l.Cash;
            if (toYou > 0) Withdraw(sim, b, toYou);
            long lost = CapitalIn(b);
            if (lost > 0)
                sim.State.Ledger.Post(now, sim.Period, "ledger.businessLost", reference, new Posting(Account.LossOnAssetSales, lost), new Posting(Account.InvestmentInBusinesses, -lost));

            b.Status = BusinessStatus.Closed;
            if (b.Pitch < 0) Land.LandSystem.SetOccupied(sim, b.TileId, false); // a pitch in the square is the village's
            if (sale == null)
            {
                History.AlmanacRules.Loss(sim, History.AlmanacLoss.BusinessClosed, lost, t.Id); // the money put in that did not come back (spec 029 §3.4)
                sim.Emit(new BusinessClosed(now, b.Id, t.Id, toYou, guaranteed));
                return;
            }
            ExitState x = sim.State.Township.Exit;
            var rec = new SaleRecord
            {
                Id = x.NextSaleId++, At = now, BusinessId = b.Id, TypeId = t.Id, Buyer = sale.Buyer, Price = sale.Price, Share = sale.Share, Worth = sale.Worth, Held = held, Gain = gain, Tax = tax,
                Severance = sale.Severance, ToYou = toYou, Outside = outside,
            };
            x.Sales.Add(rec);
            if (x.Sales.Count > ExitState.Kept) x.Sales.RemoveAt(0);
            sim.Emit(new Events.BusinessSold(now, b.Id, rec.Id, sale.Price, toYou));
        }
    }
}
