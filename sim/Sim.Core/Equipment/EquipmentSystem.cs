using Sim.Core.Scheduling;

namespace Sim.Core.Equipment
{
    /// <summary>The equipment ladder's daily pass (spec 013 §3.5): breakdowns and repairs, machines in id order; old sowing records pruned.</summary>
    internal sealed class EquipmentSystem : ISimSystem
    {
        public SystemOrder Order { get { return SystemOrder.Agriculture; } }

        public void OnPass(Simulation sim, ItemClass pass)
        {
            if (pass != ItemClass.Daily) return;
            EquipmentRules.Daily(sim);
            sim.State.Equipment.SownTools.RemoveAll(k => sim.State.Farm.Crops.Find(c => c.Id == k.Key) == null); // cleared, harvested or seized
        }
    }
}
