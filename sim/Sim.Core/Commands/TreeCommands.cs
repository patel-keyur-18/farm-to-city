using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;

namespace Sim.Core.Commands
{
    /// <summary>Buys a sapling and plants it in an empty slot of owned land; the sapling is capitalised (spec 003 §3.12).</summary>
    public sealed class PlantTree : TimedCommand
    {
        public long SlotId { get; }
        public string TreeId { get; }

        public PlantTree(long at, long slotId, string treeId) : base(at)
        {
            SlotId = slotId;
            TreeId = treeId;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            TreeSlot slot = sim.State.Land?.Slots.Find(s => s.Id == SlotId);
            if (slot == null) return CommandResult.Reject(RejectReason.UnknownSlot); // slots exist only on owned land
            if (slot.TreeId != 0) return CommandResult.Reject(RejectReason.SlotOccupied);
            FarmDefs farm = sim.Config.Game.Farm;
            int d = farm.IndexOfTree(TreeId);
            if (d < 0) return CommandResult.Reject(RejectReason.UnknownTree);
            TreeDef def = farm.Trees[d];
            if (def.HillOnly) return CommandResult.Reject(RejectReason.TerrainNotAllowed); // no hill parcels in Phase 1
            long cost = MarketRules.Value(sim, def.SaplingGood, 1, MarketRules.InputPrice(sim, def.SaplingGood));
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            FarmState state = sim.State.Farm;
            long t = sim.State.Clock.SimTime;
            var tree = new Tree { Id = state.NextTreeId++, SlotId = SlotId, Def = d, PlantedAt = t, State = TreeState.Growing, Cost = cost };
            sim.State.Ledger.Post(t, sim.Period, "ledger.treePlanted", "tree:" + tree.Id,
                new Posting(Account.Trees, cost), new Posting(Account.Cash, -cost));
            tree.JobId = sim.ScheduleJob(FarmSystem.TreeJob, tree.Id, t + def.GrowthSeconds).Id;
            state.Trees.Add(tree);
            LandSystem.SetTree(sim, SlotId, tree.Id);
            sim.Emit(new TreePlanted(t, SlotId, def.Id, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>Picks a tree's ripe batch into the store; the tree is spent after its last batch (spec 003 §3.12).</summary>
    public sealed class PickFruit : TimedCommand
    {
        public long SlotId { get; }
        public Overflow Overflow { get; }

        public PickFruit(long at, long slotId, Overflow overflow = Overflow.StoreOnly) : base(at)
        {
            SlotId = slotId;
            Overflow = overflow;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Tree tree = sim.State.Farm?.TreeIn(SlotId);
            if (tree == null) return CommandResult.Reject(RejectReason.NoTree);
            if (tree.RipeGrams == 0) return CommandResult.Reject(RejectReason.NoRipeFruit);
            TreeDef def = sim.Config.Game.Farm.Trees[tree.Def];
            long value = MarketRules.Value(sim, def.FruitGood, tree.RipeGrams, MarketRules.Floor(sim, def.FruitGood));
            RejectReason r = Store.Add(sim, def.FruitGood, tree.RipeGrams, Overflow, "tree:" + tree.Id);
            if (r != RejectReason.None) return CommandResult.Reject(r);

            long t = sim.State.Clock.SimTime;
            long grams = tree.RipeGrams;
            tree.RipeGrams = 0;
            tree.Batches++;
            tree.Returns += value;
            sim.Emit(new FruitPicked(t, SlotId, def.Id, grams, value));
            if (def.HarvestLimit > 0 && tree.Batches >= def.HarvestLimit)
            {
                FarmSystem.Dispose(sim, tree);
                sim.Emit(new TreeSpent(t, SlotId, def.Id, tree.Cost));
            }
            return CommandResult.Ok;
        }
    }

    /// <summary>Removes a tree at any time: its book value is lost and unpicked fruit with it (spec 003 §3.12).</summary>
    public sealed class RemoveTree : TimedCommand
    {
        public long SlotId { get; }

        public RemoveTree(long at, long slotId) : base(at) { SlotId = slotId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Tree tree = sim.State.Farm?.TreeIn(SlotId);
            if (tree == null) return CommandResult.Reject(RejectReason.NoTree);
            FarmSystem.Dispose(sim, tree);
            sim.Emit(new TreeRemoved(sim.State.Clock.SimTime, SlotId, sim.Config.Game.Farm.Trees[tree.Def].Id, tree.Cost));
            return CommandResult.Ok;
        }
    }
}
