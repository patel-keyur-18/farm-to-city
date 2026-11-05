using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;

namespace Sim.Core.Commands
{
    /// <summary>
    /// Plants an empty field you own as an orchard of one tree at its real density (spec 013 §3.3): the saplings with planting are
    /// capitalised (Dr 1700 Trees) and the trees grow by spec 003's rules.
    /// </summary>
    public sealed class PlantOrchard : TimedCommand
    {
        public int TileId { get; }
        public string TreeId { get; }

        public PlantOrchard(long at, int tileId, string treeId) : base(at)
        {
            TileId = tileId;
            TreeId = treeId;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            RejectReason r = OrchardRules.Place(sim, TileId);
            if (r != RejectReason.None) return CommandResult.Reject(r);
            int d = sim.Config.Game.Farm.IndexOfTree(TreeId);
            if (d < 0 || OrchardRules.TreesPerAcre(sim, d) == 0) return CommandResult.Reject(RejectReason.UnknownTree);
            TreeDef def = sim.Config.Game.Farm.Trees[d];
            if (def.HillOnly) return CommandResult.Reject(RejectReason.TerrainNotAllowed);
            long cost = OrchardRules.Price(sim, d);
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);

            FarmSystem.Accrue(sim);
            OrchardState s = sim.State.Orchards;
            long t = sim.State.Clock.SimTime;
            var o = new Orchard { Id = s.NextOrchardId++, TileId = TileId, Def = d, Trees = OrchardRules.TreesPerAcre(sim, d), PlantedAt = t, State = TreeState.Growing, Cost = cost, SumsFrom = t };
            sim.State.Ledger.Post(t, sim.Period, "ledger.orchardPlanted", "orchard:" + o.Id, new Posting(Account.Trees, cost), new Posting(Account.Cash, -cost));
            o.JobId = sim.ScheduleJob(FarmSystem.OrchardJob, o.Id, t + def.GrowthSeconds).Id;
            s.Orchards.Add(o);
            FarmSystem.Refresh(sim);
            sim.Emit(new OrchardPlanted(t, TileId, def.Id, o.Trees, cost));
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Picks what can be taken of an orchard's ripe batch into the store, the rest of it to the trader with SellRestToTrader (spec 003
    /// §3.12, ADR 0090 §4); what neither can take waits on the trees. Banana and papaya are spent after their last harvest.
    /// </summary>
    public sealed class PickOrchard : TimedCommand
    {
        public int TileId { get; }
        public Overflow Overflow { get; }

        public PickOrchard(long at, int tileId, Overflow overflow = Overflow.StoreOnly) : base(at)
        {
            TileId = tileId;
            Overflow = overflow;
        }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Orchard o = OrchardRules.Of(sim, TileId);
            if (o == null) return CommandResult.Reject(RejectReason.NoOrchard);
            if (o.RipeGrams == 0) return CommandResult.Reject(RejectReason.NoRipeFruit);
            TreeDef def = sim.Config.Game.Farm.Trees[o.Def];
            long grams = OrchardRules.Pickable(sim, o, Overflow);
            if (grams == 0)
                return CommandResult.Reject(Overflow == Overflow.StoreOnly ? RejectReason.StoreFull : !MarketRules.TraderBuys(sim) ? RejectReason.TraderAway : RejectReason.TraderFull);
            long value = MarketRules.Value(sim, def.FruitGood, grams, MarketRules.Floor(sim, def.FruitGood));
            RejectReason r = Store.Add(sim, def.FruitGood, grams, Overflow, "orchard:" + o.Id);
            if (r != RejectReason.None) return CommandResult.Reject(r);

            o.RipeGrams -= grams;
            sim.Emit(new OrchardPicked(sim.State.Clock.SimTime, TileId, def.Id, grams, value));
            OrchardRules.Earned(sim, o, value);
            return CommandResult.Ok;
        }
    }

    /// <summary>
    /// Sells what is left of an orchard's ripe batch on the trees to a fruit contractor, who picks and carries it himself (ADR 0090 §4):
    /// the contractor's share of the trader's price, any amount, at any hour. Dr Cash, Cr 4010 Fruit sales.
    /// </summary>
    public sealed class SellOrchardOnTrees : TimedCommand
    {
        public int TileId { get; }

        public SellOrchardOnTrees(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Orchard o = OrchardRules.Of(sim, TileId);
            if (o == null) return CommandResult.Reject(RejectReason.NoOrchard);
            if (o.RipeGrams == 0) return CommandResult.Reject(RejectReason.NoRipeFruit);
            TreeDef def = sim.Config.Game.Farm.Trees[o.Def];
            long t = sim.State.Clock.SimTime, grams = o.RipeGrams, amount = OrchardRules.ContractorPrice(sim, o);
            if (amount > 0)
                sim.State.Ledger.Post(t, sim.Period, "ledger.soldOnTrees", "orchard:" + o.Id,
                    new Posting(Account.Cash, amount), new Posting(Store.SalesAccount(sim, def.FruitGood), -amount));
            sim.State.Finance.Sales.Add(sim, def.FruitGood, SoldTo.Contractor, amount);
            o.RipeGrams = 0;
            sim.Emit(new OrchardSoldOnTrees(t, TileId, def.Id, grams, amount));
            OrchardRules.Earned(sim, o, amount);
            return CommandResult.Ok;
        }
    }

    /// <summary>Clears an orchard at any time: its book value is lost (Dr 5600) and unpicked fruit with it; the field is free again.</summary>
    public sealed class ClearOrchard : TimedCommand
    {
        public int TileId { get; }

        public ClearOrchard(long at, int tileId) : base(at) { TileId = tileId; }

        protected override CommandResult ApplyAt(Simulation sim)
        {
            Orchard o = OrchardRules.Of(sim, TileId);
            if (o == null) return CommandResult.Reject(RejectReason.NoOrchard);
            FarmSystem.Accrue(sim);
            OrchardRules.Dispose(sim, o, OrchardEnd.Cleared);
            return CommandResult.Ok;
        }
    }
}
