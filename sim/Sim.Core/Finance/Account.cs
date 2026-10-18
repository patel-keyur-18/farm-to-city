using System;

namespace Sim.Core.Finance
{
    /// <summary>The founder's fixed chart of accounts (spec 005 §3.1). The value is the account id.</summary>
    public enum Account
    {
        Cash = 1000,
        PrepaidLivingCosts = 1100,
        PrepaidWages = 1110,
        PrepaidRent = 1120,
        Land = 1500,
        Buildings = 1600,
        AccumulatedDepreciation = 1650,
        Trees = 1700,

        MoneylenderLoans = 2100,
        BankLoans = 2200,
        InterestPayable = 2300,

        OpeningEquity = 3000,
        RetainedEarnings = 3100,

        CropSales = 4000,
        FruitSales = 4010,
        DayLabourEarnings = 4100,
        GainOnAssetSales = 4800,
        DebtWrittenOff = 4900,

        SeedsAndPlanting = 5000,
        FertiliserAndSpraying = 5010,
        HiredLabour = 5100,
        LivingCosts = 5200,
        Rent = 5300,
        Interest = 5400,
        PenaltyInterest = 5410,
        Depreciation = 5500,
        LossOnAssetSales = 5600,
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
            Account.Cash, Account.PrepaidLivingCosts, Account.PrepaidWages, Account.PrepaidRent, Account.Land,
            Account.Buildings, Account.AccumulatedDepreciation, Account.Trees,
            Account.MoneylenderLoans, Account.BankLoans, Account.InterestPayable,
            Account.OpeningEquity, Account.RetainedEarnings,
            Account.CropSales, Account.FruitSales, Account.DayLabourEarnings, Account.GainOnAssetSales, Account.DebtWrittenOff,
            Account.SeedsAndPlanting, Account.FertiliserAndSpraying, Account.HiredLabour, Account.LivingCosts, Account.Rent,
            Account.Interest, Account.PenaltyInterest, Account.Depreciation, Account.LossOnAssetSales,
        };

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
