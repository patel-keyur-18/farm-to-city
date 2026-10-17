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
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.opening", null,
                new Posting(Account.Cash, cash),
                new Posting(Account.Land, land),
                new Posting(Account.Buildings, buildings),
                new Posting(Account.OpeningEquity, -(cash + land + buildings)));
        }

        /// <summary>
        /// The season close. So far: the reconciliation check and entry retention (US-059). Depreciation, interest,
        /// the P&amp;L and settlement (spec 005 §3.4) arrive with US-060 to US-062 and F05.2.
        /// </summary>
        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass != ItemClass.Seasonal) return;
            Ledger ledger = sim.State.Ledger;
            string failure = ledger.Reconcile();
            if (failure != null)
            {
                if (sim.Config.Options.StrictChecks) throw new System.InvalidOperationException("Ledger reconciliation failed: " + failure);
                sim.Emit(new LedgerReconciliationFailed(sim.State.Clock.SimTime, failure));
            }
            long closing = sim.Period - 1;
            ledger.DropEntriesBefore(closing - sim.Config.Game.Ledger.RetainSeasons + 1);
        }
    }
}
