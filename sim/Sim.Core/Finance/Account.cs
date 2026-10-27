using System;

namespace Sim.Core.Finance
{
    /// <summary>
    /// The chart of accounts (spec 005 §3.1). The value is the account id. A business's own ledger (spec 016 §3.2) uses the same chart:
    /// Cash, Stock, Buildings, Equipment, Overdraft, Bank loans, Owner's capital, Sales, Service income, Cost of goods sold, Wages
    /// (5100), Power, Repairs, Depreciation, Interest.
    /// </summary>
    public enum Account
    {
        Cash = 1000,
        PrepaidLivingCosts = 1100,
        PrepaidWages = 1110,
        PrepaidRent = 1120,
        InputsInStore = 1200,

        /// <summary>A business's stock of inputs and goods at cost (spec 016).</summary>
        BusinessStock = 1210,
        OwedByBuyers = 1300,
        Land = 1500,
        Buildings = 1600,
        AccumulatedDepreciation = 1650,
        Trees = 1700,
        Livestock = 1750,

        /// <summary>The founder's money put into businesses, at cost (spec 016 §3.2).</summary>
        InvestmentInBusinesses = 1800,
        Equipment = 1610,

        MoneylenderLoans = 2100,
        Overdraft = 2150,
        BankLoans = 2200,
        InterestPayable = 2300,

        OpeningEquity = 3000,
        RetainedEarnings = 3100,

        /// <summary>A business's owner's capital: what the founder put in, less drawings (spec 016 §3.2).</summary>
        OwnersCapital = 3200,

        CropSales = 4000,
        FruitSales = 4010,
        MilkSales = 4020,
        LivestockSales = 4030,
        DayLabourEarnings = 4100,
        RentReceived = 4200,

        /// <summary>Drawings beyond what the founder put into businesses (spec 016 §3.2).</summary>
        BusinessDrawingsIncome = 4300,
        Sales = 4500,
        ServiceIncome = 4510,
        GainOnAssetSales = 4800,
        DebtWrittenOff = 4900,

        SeedsAndPlanting = 5000,
        FertiliserAndSpraying = 5010,
        AnimalFeed = 5020,
        VetAndBreeding = 5030,
        HiredLabour = 5100,
        LivingCosts = 5200,
        HealthCosts = 5250,
        Rent = 5300,
        Interest = 5400,
        PenaltyInterest = 5410,
        Depreciation = 5500,
        LossOnAssetSales = 5600,
        TransportAndSelling = 5700,
        CostOfGoodsSold = 5800,
        Power = 5810,
        Repairs = 5820,
    }

    public enum AccountGroup
    {
        Assets = 1,
        Liabilities = 2,
        Equity = 3,
        Income = 4,
        Expenses = 5,
    }

    public static class Accounts
    {
        /// <summary>Every account in id order: the order balances are kept, hashed and reported in.</summary>
        public static readonly Account[] All =
        {
            Account.Cash, Account.PrepaidLivingCosts, Account.PrepaidWages, Account.PrepaidRent, Account.InputsInStore, Account.BusinessStock, Account.OwedByBuyers, Account.Land,
            Account.Buildings, Account.Equipment, Account.AccumulatedDepreciation, Account.Trees, Account.Livestock, Account.InvestmentInBusinesses,
            Account.MoneylenderLoans, Account.Overdraft, Account.BankLoans, Account.InterestPayable,
            Account.OpeningEquity, Account.RetainedEarnings, Account.OwnersCapital,
            Account.CropSales, Account.FruitSales, Account.MilkSales, Account.LivestockSales, Account.DayLabourEarnings, Account.RentReceived, Account.BusinessDrawingsIncome,
            Account.Sales, Account.ServiceIncome, Account.GainOnAssetSales, Account.DebtWrittenOff,
            Account.SeedsAndPlanting, Account.FertiliserAndSpraying, Account.AnimalFeed, Account.VetAndBreeding, Account.HiredLabour, Account.LivingCosts, Account.HealthCosts,
            Account.Rent, Account.Interest, Account.PenaltyInterest, Account.Depreciation, Account.LossOnAssetSales,
            Account.TransportAndSelling, Account.CostOfGoodsSold, Account.Power, Account.Repairs,
        };

        /// <summary>
        /// The save schema each account arrived in (spec 011: 1300 in schema 6; spec 012: the livestock accounts in 7; spec 014: 4200 Rent received in 9;
        /// spec 016: the business accounts in 10); 0 for the rest.
        /// A late account hashes only once used, so the golden replays stay as they were, and an older save reads without it.
        /// </summary>
        internal static int SinceSchema(Account a)
        {
            switch (a)
            {
                case Account.OwedByBuyers: return 6;
                case Account.Livestock: case Account.MilkSales: case Account.LivestockSales: case Account.AnimalFeed: case Account.VetAndBreeding: return 7;
                case Account.RentReceived: return 9;
                case Account.BusinessStock: case Account.Equipment: case Account.InvestmentInBusinesses: case Account.Overdraft: case Account.OwnersCapital:
                case Account.BusinessDrawingsIncome: case Account.Sales: case Account.ServiceIncome: case Account.CostOfGoodsSold: case Account.Power: case Account.Repairs:
                    return 10;
                default: return 0;
            }
        }

        /// <summary>The group is the id's thousands digit.</summary>
        public static AccountGroup GroupOf(Account a) { return (AccountGroup)((int)a / 1000); }

        /// <summary>The account's position in <see cref="All"/>.</summary>
        internal static int IndexOf(Account a)
        {
            int i = Array.IndexOf(All, a);
            if (i < 0) throw new ArgumentOutOfRangeException(nameof(a), "Unknown account " + (int)a + ".");
            return i;
        }
    }
}
