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
            foreach (Building b in sim.State.Land.Buildings) buildings += defs.Buildings[b.Def].Value;
            long cash = defs.Start.StartingCash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, OpeningKey, null,
                new Posting(Account.Cash, cash),
                new Posting(Account.Land, land),
                new Posting(Account.Buildings, buildings),
                new Posting(Account.OpeningEquity, -(cash + land + buildings)));
            sim.State.Finance.SeasonOpeningCash = cash;
        }

        /// <summary>
        /// Daily: prepaid costs expensed to now, in the period of the day that ended. Seasonal: the season close and
        /// settlement (spec 005 §3.4). Interest, loan payments and credit outcomes (steps 2, 4 and 8) join with F05.2.
        /// </summary>
        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Daily) Accrual.ExpenseAll(sim, sim.AccrualPeriod);
            if (pass != ItemClass.Seasonal) return;

            Ledger ledger = sim.State.Ledger;
            long closing = sim.Period - 1;

            // Close the season (accruals; no cash moves).
            Depreciate(sim, closing);
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
            // Step 5, living costs, arrives with the founder (US-051).

            sim.Emit(new SeasonClosed(sim.State.Clock.SimTime, closing, report));
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
