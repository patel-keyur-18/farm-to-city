using System.Collections.Generic;
using Sim.Core.Land;

namespace Sim.Core.Finance
{
    /// <summary>The season report (spec 005 §3.4 step 3, ADR 0032 grouping) and net worth (§3.5).</summary>
    public static class Reporting
    {
        private static readonly Account[] FarmIncome = { Account.CropSales, Account.FruitSales };
        private static readonly Account[] FarmCosts =
        {
            Account.SeedsAndPlanting, Account.FertiliserAndSpraying, Account.HiredLabour, Account.Rent,
            Account.Depreciation, Account.TransportAndSelling,
        };
        private static readonly Account[] OtherIncome = { Account.DayLabourEarnings, Account.GainOnAssetSales, Account.DebtWrittenOff };
        private static readonly Account[] Household = { Account.LivingCosts, Account.HealthCosts };
        private static readonly Account[] FinanceCosts = { Account.Interest, Account.PenaltyInterest };
        private static readonly Account[] Other = { Account.LossOnAssetSales };

        /// <summary>Net worth now (spec 005 §3.5).</summary>
        public static NetWorthSnapshot NetWorth(Simulation sim)
        {
            Ledger l = sim.State.Ledger;
            long land = 0;
            foreach (Parcel p in sim.State.Land.Parcels)
                if (p.Owned) land += LandRules.ParcelPrice(sim, p);
            return new NetWorthSnapshot
            {
                Cash = l.Cash,
                Prepaid = l.Balance(Account.PrepaidLivingCosts) + l.Balance(Account.PrepaidWages) + l.Balance(Account.PrepaidRent),
                LandAtMarket = land,
                BuildingsAtBook = l.Balance(Account.Buildings) + l.Balance(Account.AccumulatedDepreciation),
                TreesAtBook = l.Balance(Account.Trees),
                StoredGoods = Market.Store.Value(sim),
                LoanPrincipal = -(l.Balance(Account.MoneylenderLoans) + l.Balance(Account.BankLoans)),
                InterestPayable = -l.Balance(Account.InterestPayable) + CreditRules.AccruedUnposted(sim),
                BookEquity = l.TotalOf(AccountGroup.Assets) - l.TotalOf(AccountGroup.Liabilities),
            };
        }

        /// <summary>Builds the report of the closing season from the income and expense balances and its entries.</summary>
        internal static SeasonReport Build(Simulation sim, long period)
        {
            Ledger l = sim.State.Ledger;
            var byAccount = new List<KeyValuePair<Account, long>>();
            foreach (Account a in Accounts.All)
            {
                AccountGroup g = Accounts.GroupOf(a);
                if ((g == AccountGroup.Income || g == AccountGroup.Expenses) && l.Balance(a) != 0)
                    byAccount.Add(new KeyValuePair<Account, long>(a, -l.Balance(a)));
            }


            var cash = new CashFlowSummary { Opening = sim.State.Finance.SeasonOpeningCash, Closing = l.Cash };
            foreach (JournalEntry e in l.Entries)
            {
                if (e.Period != period || e.DescriptionKey == FinanceSystem.OpeningKey) continue; // the inheritance is opening cash
                foreach (Posting p in e.Postings)
                {
                    if (p.Account == Account.Cash)
                    {
                        if (p.Amount > 0) cash.Inflows += p.Amount;
                        else cash.Outflows -= p.Amount;
                    }
                    else if (IsPrepaid(p.Account) && p.Amount > 0 && Touches(e, Account.Cash)) cash.PaidUpfront += p.Amount;
                }
            }

            return new SeasonReport
            {
                Period = period,
                ByAccount = byAccount.AsReadOnly(),
                Lines = LinesFrom(byAccount),
                CashFlow = cash,
                NetWorth = NetWorth(sim),
                HomeUse = sim.State.Farm == null ? 0 : sim.State.Farm.Founder.HomeUseSeason,
                GardenHomeUse = sim.State.Farm == null ? 0 : sim.State.Farm.Founder.GardenHomeUseSeason,
            };
        }

        /// <summary>The report's lines (ADR 0032 grouping) from its per-account amounts: built at the close and again when a save loads.</summary>
        internal static IReadOnlyList<ReportLine> LinesFrom(IReadOnlyList<KeyValuePair<Account, long>> byAccount)
        {
            ReportLine Line(string key, Account[] accounts)
            {
                long sum = 0;
                foreach (Account a in accounts)
                    foreach (KeyValuePair<Account, long> kv in byAccount)
                        if (kv.Key == a) sum += kv.Value;
                return new ReportLine(key, sum, accounts);
            }
            ReportLine farmIncome = Line("farmIncome", FarmIncome);
            ReportLine farmCosts = Line("farmCosts", FarmCosts);
            var farmProfit = new ReportLine("farmProfit", farmIncome.Amount + farmCosts.Amount, Concat(FarmIncome, FarmCosts));
            ReportLine other = Line("otherIncome", OtherIncome);
            ReportLine household = Line("household", Household);
            ReportLine finance = Line("finance", FinanceCosts);
            ReportLine losses = Line("other", Other);
            var net = new ReportLine("netResult", farmProfit.Amount + other.Amount + household.Amount + finance.Amount + losses.Amount,
                new Account[0]);
            return new List<ReportLine> { farmIncome, farmCosts, farmProfit, other, household, finance, losses, net }.AsReadOnly();
        }

        private static Account[] Concat(Account[] a, Account[] b)
        {
            var r = new Account[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        private static bool IsPrepaid(Account a)
        {
            return a == Account.PrepaidLivingCosts || a == Account.PrepaidWages || a == Account.PrepaidRent;
        }

        private static bool Touches(JournalEntry e, Account a)
        {
            foreach (Posting p in e.Postings)
                if (p.Account == a) return true;
            return false;
        }
    }
}
