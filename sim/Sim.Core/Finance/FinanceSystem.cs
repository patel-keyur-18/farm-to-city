using Sim.Core.Definitions;
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
            long land = sim.State.Land.Parcels[defs.Start.InheritedParcel].CostBasis;
            long buildings = 0;
            foreach (Building b in sim.State.Land.Buildings) buildings += defs.Buildings[b.Def].Value;
            long cash = defs.Start.StartingCash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.opening", null,
                new Posting(Account.Cash, cash),
                new Posting(Account.Land, land),
                new Posting(Account.Buildings, buildings),
                new Posting(Account.OpeningEquity, -(cash + land + buildings)));
        }

        public void OnPass(Simulation sim, ItemClass pass)
        {
        }
    }
}
