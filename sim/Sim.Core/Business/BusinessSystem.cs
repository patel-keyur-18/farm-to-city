using Sim.Core.Scheduling;

namespace Sim.Core.Business
{
    /// <summary>Businesses (spec 016 §3.11). Hourly, after the market: each open business works the hour. The close runs with settlement.</summary>
    internal sealed class BusinessSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.BusinessFinance; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass == ItemClass.Hourly && sim.State.Businesses != null) BusinessRules.Hourly(sim);
        }
    }
}
