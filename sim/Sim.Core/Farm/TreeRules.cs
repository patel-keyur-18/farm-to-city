using Sim.Core.Definitions;
using Sim.Core.Market;

namespace Sim.Core.Farm
{
    /// <summary>A tree's payback Why? (spec 003 §3.12, ADR 0021, 0034).</summary>
    public sealed class TreePayback
    {
        /// <summary>Book value (the sapling with planting) and returns to date (picked batches at the trader's price), 1/100 coin.</summary>
        public long Cost { get; internal set; }
        public long Returns { get; internal set; }
        public bool PaidBack { get; internal set; }

        /// <summary>The season index (since t = 0) it paid back or is expected to, or null if it never will (harvest limit).</summary>
        public long? Season { get; internal set; }
    }

    public static class TreeRules
    {
        /// <summary>
        /// Paid back: the season returns reached the cost. Otherwise ⌈(cost − returns) ÷ (batchYieldPerTree × today's buying
        /// price)⌉ batches more, counted from the first fruiting season with a batch still to come (from maturity for a young
        /// tree); null if the harvest limit ends the tree first.
        /// </summary>
        public static TreePayback Payback(Simulation sim, Tree tree)
        {
            TreeDef def = sim.Config.Game.Farm.Trees[tree.Def];
            var p = new TreePayback { Cost = tree.Cost, Returns = tree.Returns };
            if (tree.PaidBackPeriod >= 0) { p.PaidBack = true; p.Season = tree.PaidBackPeriod; return p; }

            long batchValue = MarketRules.Value(sim, def.FruitGood, def.BatchGrams, MarketRules.Floor(sim, def.FruitGood));
            if (batchValue <= 0) return p;
            long need = (tree.Cost - tree.Returns + batchValue - 1) / batchValue;
            if (def.HarvestLimit > 0 && need > def.HarvestLimit - tree.Batches) return p; // never: spent first

            // The first season with a batch still to come: this one if a batch is waiting, else the next fruiting season
            // after now (this season's batch is picked) or at or after maturity for a young tree.
            long S = sim.Clock.SecondsPerSeason;
            long period;
            if (tree.State == TreeState.Growing) period = (tree.PlantedAt + def.GrowthSeconds) / S;
            else if (tree.RipeGrams > 0) period = sim.Period;
            else period = sim.Period + 1;
            for (long found = 0; ; period++)
            {
                if (!def.FruitingSeasons[(int)(period % 4)]) continue;
                if (++found == need) { p.Season = period; return p; }
            }
        }
    }
}
