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

        /// <summary>Your savings at the co-op bank (spec 019 §3.5, ADR 0101 §2): never collateral, never seized.</summary>
        BankDeposit = 1020,
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

        /// <summary>Tax billed and not yet paid, to the town (spec 022 §3.1, §3.2): paid first at the next close. The founder's and each business's own.</summary>
        TaxOwed = 2400,

        OpeningEquity = 3000,
        RetainedEarnings = 3100,

        /// <summary>A business's owner's capital: what the founder put in, less drawings (spec 016 §3.2).</summary>
        OwnersCapital = 3200,

        CropSales = 4000,
        FruitSales = 4010,
        MilkSales = 4020,
        LivestockSales = 4030,

        /// <summary>What neighbours pay for your tractor's days on their fields (US-256, ADR 0137): farm income, matching the diesel and repairs it costs. Schema 21.</summary>
        MachineHire = 4040,

        /// <summary>What crop insurance paid when luck took more than the excess (spec 027 §3.5, schema 22): farm income, never passive.</summary>
        InsuranceClaims = 4045,
        DayLabourEarnings = 4100,
        RentReceived = 4200,

        /// <summary>The rent of land you lease out (spec 024 §3.6): passive income like a house's rent, outside farm profit. Schema 20.</summary>
        LandRentReceived = 4210,

        /// <summary>Drawings beyond what the founder put into businesses (spec 016 §3.2).</summary>
        BusinessDrawingsIncome = 4300,

        /// <summary>The deposit's interest, added at each close (spec 019 §3.5). Other money, not farm profit.</summary>
        InterestOnSavings = 4400,
        Sales = 4500,
        ServiceIncome = 4510,
        GainOnAssetSales = 4800,

        /// <summary>A regional grant paid at a close after you applied (spec 027 §3.2, schema 22): other income, never farm profit.</summary>
        RegionalGrants = 4350,

        /// <summary>What property and business insurance paid after a fire or a pause (spec 027 §3.6, schema 22): other income, outside farm profit.</summary>
        InsuranceClaimsReceived = 4355,

        /// <summary>A kept calf's and a growing heifer's rise in worth (spec 025 §3.8, ADR 0135): not cash, outside farm profit. Schema 21.</summary>
        HerdGrowth = 4810,
        DebtWrittenOff = 4900,

        SeedsAndPlanting = 5000,
        FertiliserAndSpraying = 5010,
        AnimalFeed = 5020,
        VetAndBreeding = 5030,

        /// <summary>Crop insurance's premiums, charged at each sowing (spec 027 §3.5, schema 22): a farm cost.</summary>
        CropInsurancePremiums = 5035,
        HiredLabour = 5100,
        LivingCosts = 5200,
        HealthCosts = 5250,

        /// <summary>The house tax you pay as the owner of the town's houses (spec 022 §3.1). Not farm profit.</summary>
        HouseTax = 5270,

        /// <summary>A fire's repair bill (spec 027 §3.4, schema 22): outside farm profit.</summary>
        DisasterLoss = 5280,

        /// <summary>What you paid to soften an event: tankers, drains, a fine, a second supplier (spec 027 §3.2, schema 22): outside farm profit.</summary>
        EventCosts = 5285,

        /// <summary>Property and business insurance's premiums, charged at the close (spec 027 §3.6, schema 22): outside farm profit.</summary>
        InsurancePremiums = 5287,

        /// <summary>The founder's gifts to the village: the school, its upkeep, adult classes (spec 018, ADR 0095 §5). Outside farm profit.</summary>
        GiftsToVillage = 5260,
        Rent = 5300,
        Interest = 5400,
        PenaltyInterest = 5410,
        Depreciation = 5500,
        LossOnAssetSales = 5600,
        TransportAndSelling = 5700,
        CostOfGoodsSold = 5800,
        Power = 5810,
        Repairs = 5820,

        /// <summary>A business's tax on its season profit (spec 022 §3.2), on its own books.</summary>
        BusinessTax = 5830,

        // The town treasury's accounts (spec 021 §3.2, ADR 0108; save schema 19): its own ledger, never the founder's.
        /// <summary>The district's one-off start-up grant at the Township's opening.</summary>
        StartUpGrant = 4600,
        GrantUntied = 4610,

        /// <summary>The per-resident grant tied to the clinic being staffed.</summary>
        GrantTied = 4620,
        PitchRents = 4630,
        HouseTaxIncome = 4640,
        BusinessTaxIncome = 4650,

        /// <summary>Permit fees (spec 024).</summary>
        Fees = 4660,
        GiftsFromFounder = 4670,

        // One expense account a department, in the order they are paid (spec 021 §3.4; the seventh, Policies, is spec 022 §3.5's).
        DeptOffice = 5910,
        DeptHealth = 5920,
        DeptEducation = 5930,
        DeptRoads = 5940,
        DeptSafety = 5950,
        DeptUtilities = 5960,
        DeptPolicies = 5970,

        /// <summary>A festival the town paid for (spec 026 §3.1, schema 22): spent at once to suppliers outside the town, outside any department's budget.</summary>
        Festivals = 5980,
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
            Account.Cash, Account.BankDeposit, Account.PrepaidLivingCosts, Account.PrepaidWages, Account.PrepaidRent, Account.InputsInStore, Account.BusinessStock, Account.OwedByBuyers, Account.Land,
            Account.Buildings, Account.Equipment, Account.AccumulatedDepreciation, Account.Trees, Account.Livestock, Account.InvestmentInBusinesses,
            Account.MoneylenderLoans, Account.Overdraft, Account.BankLoans, Account.InterestPayable,
            Account.OpeningEquity, Account.RetainedEarnings, Account.OwnersCapital,
            Account.CropSales, Account.FruitSales, Account.MilkSales, Account.LivestockSales, Account.DayLabourEarnings, Account.RentReceived, Account.BusinessDrawingsIncome, Account.InterestOnSavings, Account.LandRentReceived,
            Account.Sales, Account.ServiceIncome, Account.GainOnAssetSales, Account.DebtWrittenOff, Account.HerdGrowth, Account.MachineHire,
            Account.SeedsAndPlanting, Account.FertiliserAndSpraying, Account.AnimalFeed, Account.VetAndBreeding, Account.HiredLabour, Account.LivingCosts, Account.HealthCosts,
            Account.GiftsToVillage, Account.Rent, Account.Interest, Account.PenaltyInterest, Account.Depreciation, Account.LossOnAssetSales,
            Account.TransportAndSelling, Account.CostOfGoodsSold, Account.Power, Account.Repairs,
            Account.StartUpGrant, Account.GrantUntied, Account.GrantTied, Account.PitchRents, Account.HouseTaxIncome, Account.BusinessTaxIncome, Account.Fees, Account.GiftsFromFounder,
            Account.DeptOffice, Account.DeptHealth, Account.DeptEducation, Account.DeptRoads, Account.DeptSafety, Account.DeptUtilities, Account.DeptPolicies,
            Account.TaxOwed, Account.HouseTax, Account.BusinessTax, // spec 022 (save schema 19, after the treasury's)
            Account.Festivals, // spec 026 (save schema 22)
            Account.RegionalGrants, Account.DisasterLoss, Account.EventCosts, // spec 027 (save schema 22)
            Account.InsuranceClaims, Account.InsuranceClaimsReceived, Account.CropInsurancePremiums, Account.InsurancePremiums,
        };

        /// <summary>
        /// The save schema each account arrived in (spec 011: 1300 in schema 6; spec 012: the livestock accounts in 7; spec 014: 4200 Rent received in 9;
        /// spec 016: the business accounts in 10; spec 018: 5260 Gifts to the village in 15; spec 019: 1020 Bank deposit and 4400 Interest on savings in 16; spec 021: the treasury's accounts in 19); 0 for the rest.
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
                case Account.GiftsToVillage: return 15;
                case Account.BankDeposit: case Account.InterestOnSavings: return 16;
                case Account.LandRentReceived: return 20;
                case Account.HerdGrowth: case Account.MachineHire: return 21;
                case Account.StartUpGrant: case Account.GrantUntied: case Account.GrantTied: case Account.PitchRents: case Account.HouseTaxIncome: case Account.BusinessTaxIncome:
                case Account.Fees: case Account.GiftsFromFounder: case Account.DeptOffice: case Account.DeptHealth: case Account.DeptEducation: case Account.DeptRoads:
                case Account.DeptSafety: case Account.DeptUtilities: case Account.DeptPolicies: case Account.TaxOwed: case Account.HouseTax: case Account.BusinessTax: return 19;
                case Account.Festivals: case Account.RegionalGrants: case Account.DisasterLoss: case Account.EventCosts:
                case Account.InsuranceClaims: case Account.InsuranceClaimsReceived: case Account.CropInsurancePremiums: case Account.InsurancePremiums: return 22;
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
