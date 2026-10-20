using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Scheduling;

namespace Sim.Core.Finance
{
    /// <summary>The founder's books (spec 005): the opening balance sheet and the season close.</summary>
    internal sealed class FinanceSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.BusinessFinance; } }

        internal const string OpeningKey = "ledger.opening";

        /// <summary>
        /// The opening balance sheet (spec 005 §3.1): Dr Cash, Land and Buildings; Cr Opening equity.
        /// Runs after the land is laid out.
        /// </summary>
        public static void Initialise(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            long land = 0;
            foreach (int p in defs.Start.InheritedParcels) land += sim.State.Land.Parcels[p].CostBasis;
            long buildings = 0;
            foreach (Building b in sim.State.Land.Buildings) buildings += b.Cost;
            // The inherited inputs at their start-time input price (ADR 0034).
            long inputs = 0;
            Market.MarketState m = sim.State.Market;
            for (int g = 0; g < defs.Market.Goods.Count; g++)
            {
                if (defs.Market.Goods[g].Kind != GoodKind.Input || m.Stock[g] == 0) continue;
                m.StockCost[g] = Market.MarketRules.Value(sim, g, m.Stock[g], Market.MarketRules.InputPrice(sim, g));
                inputs += m.StockCost[g];
            }
            long cash = defs.Start.StartingCash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, OpeningKey, null,
                new Posting(Account.Cash, cash),
                new Posting(Account.InputsInStore, inputs),
                new Posting(Account.Land, land),
                new Posting(Account.Buildings, buildings),
                new Posting(Account.OpeningEquity, -(cash + inputs + land + buildings)));
            sim.State.Finance.SeasonOpeningCash = cash;
        }

        /// <summary>
        /// Daily: prepaid costs expensed to now, in the period of the day that ended. Seasonal: the season close and
        /// settlement (spec 005 §3.4). Interest, loan payments and credit outcomes (steps 2, 4 and 8) join with F05.2.
        /// </summary>
        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Daily) Accrual.ExpenseAll(sim, sim.AccrualPeriod);
            if (pass == ItemClass.Hourly) CreditSystem.Hourly(sim);
            if (pass != ItemClass.Seasonal) return;

            Ledger ledger = sim.State.Ledger;
            long closing = sim.Period - 1;

            // Close the season (accruals; no cash moves).
            Depreciate(sim, closing);
            if (sim.State.Credit != null) CreditSystem.AccrueAll(sim, closing); // step 2: interest to the boundary, in the closing season
            SeasonReport report = Reporting.Build(sim, closing);
            CloseToRetainedEarnings(sim, closing);
            string failure = ledger.Reconcile();
            if (failure != null)
            {
                if (sim.Config.Options.StrictChecks) throw new System.InvalidOperationException("Ledger reconciliation failed: " + failure);
                sim.Emit(new LedgerReconciliationFailed(sim.State.Clock.SimTime, failure));
            }
            sim.State.Finance.Reports.Add(report);
            sim.State.Finance.SeasonOpeningCash = ledger.Cash;

            // Settle and pay upfront (these entries belong to the new season).
            SettlementStats loans = sim.State.Credit == null ? null : CreditSystem.Settle(sim); // step 4: loan payments come first
            Farm.FounderSystem.SettleLiving(sim); // step 5
            if (sim.State.People != null) People.PeopleSystem.SettleContracts(sim); // step 6: hands' wages, then skill growth
            if (loans != null) report.Credit = CreditSystem.Outcomes(sim, loans); // step 8: the score, defaults and the final notice

            sim.Emit(new SeasonClosed(sim.State.Clock.SimTime, closing, report));
            Progress.EraRules.OnSeasonClosed(sim, report.FarmProfit, closing); // after the books (spec 009 §3.6)
            ledger.DropEntriesBefore(closing - sim.Config.Game.Ledger.RetainSeasons + 1);
        }

        /// <summary>Straight-line depreciation (spec 005 §3.3): cost ÷ life a season, the remainder in the last season.</summary>
        private static void Depreciate(Simulation sim, long closing)
        {
            foreach (Building b in sim.State.Land.Buildings)
            {
                int life = sim.Config.Game.Buildings[b.Def].UsefulLifeSeasons;
                if (b.SeasonsDepreciated >= life) continue;
                long amount = b.SeasonsDepreciated + 1 == life ? b.Cost - b.Depreciated : b.Cost / life;
                b.SeasonsDepreciated++;
                if (amount == 0) continue;
                b.Depreciated += amount;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, closing, "ledger.depreciation", "building:" + b.Id,
                    new Posting(Account.Depreciation, amount), new Posting(Account.AccumulatedDepreciation, -amount));
            }
        }

        /// <summary>Moves every income and expense balance to Retained earnings (one entry in the closing season).</summary>
        private static void CloseToRetainedEarnings(Simulation sim, long closing)
        {
            Ledger ledger = sim.State.Ledger;
            var postings = new System.Collections.Generic.List<Posting>();
            long retained = 0;
            foreach (Account a in Accounts.All)
            {
                AccountGroup g = Accounts.GroupOf(a);
                long b = ledger.Balance(a);
                if ((g != AccountGroup.Income && g != AccountGroup.Expenses) || b == 0) continue;
                postings.Add(new Posting(a, -b));
                retained += b;
            }
            if (postings.Count == 0) return;
            postings.Add(new Posting(Account.RetainedEarnings, retained));
            ledger.Post(sim.State.Clock.SimTime, closing, "ledger.closeSeason", null, postings.ToArray());
        }
    }
}
