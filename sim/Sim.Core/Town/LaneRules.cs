using Sim.Core.Definitions;
using Sim.Core.Events;

namespace Sim.Core.Town
{
    /// <summary>
    /// The ring's gravel lanes (spec 025 §3.4): a lane is a few sections, each a work of the same id the treasury commissions on a
    /// confirm card. Until a section is built its acres are a track; after it they are a road, so the acres beside it have road
    /// access for land value, rent and builders. A lane does not wait for anything else.
    /// </summary>
    public static class LaneRules
    {
        /// <summary>The index of the lane section a work builds, or −1 when the work is not a lane.</summary>
        public static int OfWork(Simulation sim, string workId)
        {
            var lanes = sim.Config.Game?.Map.Lanes;
            if (lanes == null) return -1;
            for (int i = 0; i < lanes.Count; i++) if (lanes[i].Id == workId) return i;
            return -1;
        }

        /// <summary>The section has been built.</summary>
        public static bool BuiltSection(Simulation sim, int section)
        {
            TownshipState s = sim.State.Township;
            return s != null && (s.LanesBuilt & (1 << section)) != 0;
        }

        /// <summary>The acre is a lane acre whose section has been built.</summary>
        public static bool BuiltLane(Simulation sim, int tile)
        {
            var lanes = sim.Config.Game?.Map.Lanes;
            if (lanes == null || sim.State.Township == null) return false;
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!BuiltSection(sim, i)) continue;
                foreach (int t in lanes[i].Tiles) if (t == tile) return true;
            }
            return false;
        }

        /// <summary>The acre is a road acre or a lane acre built (the village's and the ring's roads).</summary>
        public static bool Built(Simulation sim, int tile)
        {
            return sim.State.Land.Tiles[tile].Terrain == Terrain.Road || BuiltLane(sim, tile);
        }


        /// <summary>The section a lane acre belongs to, or −1.</summary>
        public static int OfTile(Simulation sim, int tile)
        {
            var lanes = sim.Config.Game?.Map.Lanes;
            if (lanes == null) return -1;
            for (int i = 0; i < lanes.Count; i++) foreach (int t in lanes[i].Tiles) if (t == tile) return i;
            return -1;
        }

        /// <summary>A section's cost today: its work's price moved by the price index.</summary>
        public static long Cost(Simulation sim, int section)
        {
            string id = sim.Config.Game.Map.Lanes[section].Id;
            foreach (Definitions.TownWorkDef w in TreasuryRules.Def(sim).Works) if (w.Id == id) return Market.MarketRules.Indexed(sim, w.Cost);
            return 0;
        }

        /// <summary>A section's work price before prices.</summary>
        public static long BaseCost(Simulation sim, int section)
        {
            string id = sim.Config.Game.Map.Lanes[section].Id;
            foreach (Definitions.TownWorkDef w in TreasuryRules.Def(sim).Works) if (w.Id == id) return w.Cost;
            return 0;
        }

        /// <summary>How many sections the section's lane has (the west lane's four, the east lane's three, told by the id without its number) and how many are built.</summary>
        public static void Progress(Simulation sim, int section, out int built, out int total)
        {
            var lanes = sim.Config.Game.Map.Lanes;
            string stem = lanes[section].Id.Substring(0, lanes[section].Id.Length - 1);
            built = 0; total = 0;
            for (int i = 0; i < lanes.Count; i++)
            {
                if (!lanes[i].Id.StartsWith(stem)) continue;
                total++;
                if (BuiltSection(sim, i)) built++;
            }
        }

        internal static Commands.RejectReason CanCommission(Simulation sim, int section)
        {
            if (!TownshipRules.Reached(sim)) return Commands.RejectReason.NoTownship;
            return BuiltSection(sim, section) ? Commands.RejectReason.AlreadyBuilt : Commands.RejectReason.None;
        }

        internal static void OnCommissioned(Simulation sim, int section)
        {
            sim.State.Township.LanesBuilt |= 1 << section;
            sim.Emit(new LaneBuilt(sim.State.Clock.SimTime, section));
        }
    }
}
