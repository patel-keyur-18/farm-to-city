using System;
using System.Collections.Generic;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// The five utilities (spec 025 §3.1, US-144, ADR 0130): a water tower, a diesel generator, the grid, drains and a collection post, each a work
    /// the treasury commissions on a fixed plot. They share the Utilities department's condition and the one coverage rule: <c>e = condition × covered
    /// × scale</c>, and under <c>minCondition</c> a utility covers nobody. What each does is what its absence costs: no water takes health from the homes
    /// beyond it and stops builders there, no drains (once the town has <c>townSize</c> residents) caps Health, health and land value, no collection takes
    /// Belonging, and power gives leisure and evenings at the usual wage. The generator smokes (<see cref="SmokeRules"/>) until the grid replaces it.
    /// </summary>
    public static class UtilityRules
    {
        /// <summary>The Utilities department's place in the treasury's payment order (spec 021 §3.4).</summary>
        private const int UtilitiesDept = 5;

        public static UtilitiesDef Defs(Simulation sim) { return sim.Config.Game?.Township?.Utilities; }

        public static UtilityDef Def(Simulation sim, int i) { return Defs(sim).Steps[i]; }

        public static bool On(Simulation sim) { return Defs(sim) != null && sim.State.Township != null && TownshipRules.Reached(sim); }

        public static bool Built(Simulation sim, int i) { return sim.State.Township != null && (sim.State.Township.UtilitiesBuilt & (1 << i)) != 0; }

        /// <summary>The generator stands but the grid has replaced it: it is switched off.</summary>
        public static bool SwitchedOff(Simulation sim, int i) { return i == UtilityDef.Generator && Built(sim, i) && Built(sim, UtilityDef.Grid); }

        /// <summary>Built and not switched off: its lines are due and (the generator) it smokes.</summary>
        public static bool Active(Simulation sim, int i) { return Built(sim, i) && !SwitchedOff(sim, i); }

        /// <summary>The Utilities department's condition (1 with no treasury).</summary>
        public static Fixed Condition(Simulation sim) { return TreasuryRules.IsOpen(sim) ? TreasuryRules.State(sim).Condition[UtilitiesDept] : Fixed.One; }

        /// <summary>Active, and the department's condition is at or over the floor.</summary>
        public static bool Working(Simulation sim, int i) { return On(sim) && Active(sim, i) && Condition(sim) >= Defs(sim).MinCondition; }

        /// <summary>The utility whose work this is, or -1.</summary>
        public static int OfWork(Simulation sim, string workId)
        {
            if (Defs(sim) == null) return -1;
            for (int i = 0; i < UtilityDef.Count; i++) if (Def(sim, i).Work == workId) return i;
            return -1;
        }

        /// <summary>The utility whose plot this place kind is (<c>tower_plot</c>…), or -1.</summary>
        public static int OfPlot(Simulation sim, string kind)
        {
            if (Defs(sim) == null) return -1;
            for (int i = 0; i < UtilityDef.Count; i++) if (Def(sim, i).Id + "_plot" == kind) return i;
            return -1;
        }

        /// <summary>A treasury line is due: its utility stands and is not switched off.</summary>
        public static bool LineOn(Simulation sim, int utility) { return utility >= 0 && Active(sim, utility); }

        // ---- cost and upkeep ----

        public static long Cost(Simulation sim, int i)
        {
            UtilityDef d = Def(sim, i);
            if (ConstructionRules.On(sim)) { BuildKindDef k = ConstructionRules.Def(sim).KindOfWork(d.Work); if (k != null) return ConstructionRules.Quote(sim, k.Id, BuildProject.Town, 1).Total; }
            foreach (TownWorkDef w in TreasuryRules.Def(sim).Works) if (w.Id == d.Work) return Market.MarketRules.Indexed(sim, w.Cost);
            return 0;
        }

        /// <summary>Whether the work takes bricks, cement, timber and masons (the tower, drains and the post), else it is fitted the same day.</summary>
        public static bool TakesMaterials(Simulation sim, int i) { return ConstructionRules.On(sim) && ConstructionRules.Def(sim).KindOfWork(Def(sim, i).Work) != null; }

        public static long Upkeep(Simulation sim, int i) { return TreasuryRules.LineAmount(sim, TreasuryRules.Def(sim).Lines[Def(sim, i).UpkeepLine]); }

        /// <summary>The generator's fuel or the collector's wage a season (0 for the others).</summary>
        public static long Extra(Simulation sim, int i)
        {
            UtilityDef d = Def(sim, i);
            return d.ExtraLine < 0 ? 0 : TreasuryRules.LineAmount(sim, TreasuryRules.Def(sim).Lines[d.ExtraLine]);
        }

        /// <summary>What a built utility costs the town a season: its upkeep and its extra line.</summary>
        public static long Runs(Simulation sim, int i) { return Upkeep(sim, i) + Extra(sim, i); }

        /// <summary>What the utilities the town runs now cost a season.</summary>
        public static long RunningCost(Simulation sim)
        {
            long n = 0;
            for (int i = 0; i < UtilityDef.Count; i++) if (Active(sim, i)) n += Runs(sim, i);
            return n;
        }

        // ---- coverage ----

        /// <summary>People the utilities serve: the town's residents.</summary>
        public static int Served(Simulation sim) { return TreasuryRules.Residents(sim); }

        /// <summary><c>min(1, capacity ÷ people served)</c>.</summary>
        public static Fixed Scale(Simulation sim, int i)
        {
            int served = Served(sim), capacity = Def(sim, i).Capacity;
            return served <= capacity ? Fixed.One : Fixed.FromRatio(capacity, served);
        }

        /// <summary>A home's tile is within the utility's reach and it works.</summary>
        public static bool Covers(Simulation sim, int i, int tile) { return Working(sim, i) && CoverageRules.Within(sim, tile, Def(sim, i).Tile, Def(sim, i).Reach); }

        /// <summary>The effect at a tile: <c>condition × scale</c> when it covers, else 0.</summary>
        public static Fixed Protection(Simulation sim, int i, int tile) { return Covers(sim, i, tile) ? Condition(sim) * Scale(sim, i) : Fixed.Zero; }

        /// <summary>The effect for the old village and the founder's side (the centre lies inside every reach).</summary>
        public static Fixed ProtectionTown(Simulation sim, int i) { return Working(sim, i) ? Condition(sim) * Scale(sim, i) : Fixed.Zero; }

        /// <summary>The town is big enough to miss drains and collection.</summary>
        public static bool Big(Simulation sim) { return On(sim) && Served(sim) >= Defs(sim).TownSize; }

        private static Fixed Max(Fixed a, Fixed b) { return a > b ? a : b; }

        // ---- water ----

        /// <summary>Clean water at a tile: 1 within reach of the pond or a well, else the tower's effect.</summary>
        public static Fixed Water(Simulation sim, int tile) { return LandValueRules.PondOrWell(sim, tile) ? Fixed.One : Protection(sim, UtilityDef.Tower, tile); }

        /// <summary>The tile has the pond, a well or a working tower in reach (what builders and land value ask).</summary>
        public static bool HasWater(Simulation sim, int tile) { return LandValueRules.PondOrWell(sim, tile) || Covers(sim, UtilityDef.Tower, tile); }

        // ---- what going without costs ----

        /// <summary>The health a home's people lose a season for want of clean water and, in a big town, drains.</summary>
        public static Fixed Strain(Simulation sim, Household x)
        {
            if (!On(sim)) return Fixed.Zero;
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            if (home == null || home.TileId < 0) return Fixed.Zero;
            UtilitiesDef d = Defs(sim);
            Fixed loss = d.WaterHealth * (Fixed.One - Water(sim, home.TileId));
            if (Big(sim)) loss += d.DrainsHealth * (Fixed.One - Protection(sim, UtilityDef.Drains, home.TileId));
            return loss;
        }

        /// <summary>Health's condition can't pass this: <c>drainsHealthCap</c> in a big town with no working drains, else 1.</summary>
        public static Fixed HealthCap(Simulation sim)
        {
            return Big(sim) && !Working(sim, UtilityDef.Drains) ? Defs(sim).DrainsHealthCap : Fixed.One;
        }

        /// <summary>An acre with a home standing, in a big town, beyond working drains: its land value is lower (spec 025 §3.1).</summary>
        public static bool DrainsMissing(Simulation sim, int tile)
        {
            if (!Big(sim) || Covers(sim, UtilityDef.Drains, tile)) return false;
            foreach (Building b in sim.State.Land.Buildings) if (b.TileId == tile && HouseRules.IsHouse(sim, b)) return true;
            return false;
        }

        /// <summary>The Belonging need after the town's waste: <c>wasteBelonging</c> lower in a big town beyond a working collection.</summary>
        public static Fixed Belonging(Simulation sim, Household x, Fixed value)
        {
            if (!Big(sim)) return value;
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            Fixed lack = home == null || home.TileId < 0 ? Fixed.One : Fixed.One - Protection(sim, UtilityDef.Collection, home.TileId);
            Fixed v = value - Defs(sim).WasteBelonging * lack;
            return v < Fixed.Zero ? Fixed.Zero : v;
        }

        public static Fixed BelongingTown(Simulation sim, Fixed value)
        {
            if (!Big(sim)) return value;
            Fixed v = value - Defs(sim).WasteBelonging * (Fixed.One - ProtectionTown(sim, UtilityDef.Collection));
            return v < Fixed.Zero ? Fixed.Zero : v;
        }

        // ---- power ----

        /// <summary>Power at a tile: the generator's or the grid's effect there (the generator is off once the grid stands).</summary>
        public static Fixed Power(Simulation sim, int tile)
        {
            return On(sim) ? Max(Protection(sim, UtilityDef.Generator, tile), Protection(sim, UtilityDef.Grid, tile)) : Fixed.Zero;
        }

        public static Fixed PowerTown(Simulation sim)
        {
            return On(sim) ? Max(ProtectionTown(sim, UtilityDef.Generator), ProtectionTown(sim, UtilityDef.Grid)) : Fixed.Zero;
        }

        /// <summary>A household's Leisure need with power: <c>powerLeisure</c> more at full strength.</summary>
        public static Fixed Leisure(Simulation sim, Household x, Fixed own)
        {
            if (own <= Fixed.Zero || !On(sim)) return own;
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            if (home == null || home.TileId < 0) return own;
            Fixed v = own + own * Defs(sim).PowerLeisure * Power(sim, home.TileId);
            return v > Fixed.One ? Fixed.One : v;
        }

        public static Fixed LeisureTown(Simulation sim, Fixed own)
        {
            if (own <= Fixed.Zero || !On(sim)) return own;
            Fixed v = own + own * Defs(sim).PowerLeisure * PowerTown(sim);
            return v > Fixed.One ? Fixed.One : v;
        }

        /// <summary>Hours past its usual close a business keeps at the usual wage, with power in reach.</summary>
        public static int EveningHours(Simulation sim, Business.Business b)
        {
            return b.TileId >= 0 && On(sim) && Power(sim, b.TileId) > Fixed.Zero ? Defs(sim).EveningHours : 0;
        }

        // ---- commissioning (spec 021 §3.7) ----

        /// <summary>Why <paramref name="workId"/> can't be commissioned as a utility now, or None.</summary>
        public static Commands.RejectReason CanCommission(Simulation sim, string workId)
        {
            int i = OfWork(sim, workId);
            if (i < 0) return Commands.RejectReason.None;
            if (!TownshipRules.Reached(sim)) return Commands.RejectReason.NoTownship;
            if (Built(sim, i)) return Commands.RejectReason.AlreadyBuilt;
            return ConstructionRules.Pending(sim, workId) ? Commands.RejectReason.PlotTaken : Commands.RejectReason.None;
        }

        /// <summary>The utility stands (the same day, or when its building is finished).</summary>
        internal static void OnBuilt(Simulation sim, string workId)
        {
            int i = OfWork(sim, workId);
            if (i < 0) return;
            long t = sim.State.Clock.SimTime;
            sim.State.Township.UtilitiesBuilt |= 1 << i;
            sim.Emit(new UtilityBuilt(t, i));
            if (i == UtilityDef.Grid && Built(sim, UtilityDef.Generator)) sim.Emit(new GridConnected(t));
        }

        /// <summary>
        /// The budget guard on the commission card: what the utility adds a season to Utilities (less the generator's, for the grid) and by how much the
        /// town would be short at today's income once it runs.
        /// </summary>
        public static void Guard(Simulation sim, string workId, out long added, out long shortBy)
        {
            int i = OfWork(sim, workId);
            added = i < 0 ? 0 : Runs(sim, i) - (i == UtilityDef.Grid && Active(sim, UtilityDef.Generator) ? Runs(sim, UtilityDef.Generator) : 0);
            long needs = 0;
            for (int d = 0; d < TreasuryState.Departments; d++) needs += TreasuryRules.Needs(sim, d);
            shortBy = Math.Max(0, needs + added - TreasuryRules.ExpectedIncome(sim));
        }

        /// <summary>What the grid saves a season over the running generator (0 with none), and the seasons its extra cost takes to come back (0 with none).</summary>
        public static long GridSaves(Simulation sim)
        {
            return Active(sim, UtilityDef.Generator) ? Runs(sim, UtilityDef.Generator) - Runs(sim, UtilityDef.Grid) : 0;
        }

        public static long GridPaybackSeasons(Simulation sim)
        {
            long saves = GridSaves(sim);
            long extra = Cost(sim, UtilityDef.Grid) - Cost(sim, UtilityDef.Generator);
            return saves <= 0 || extra <= 0 ? 0 : (extra + saves - 1) / saves;
        }

        /// <summary>The gift that tops the town's cash up to a cost: the shortfall rounded up to <c>giftRound</c> (0 when the town can pay).</summary>
        public static long TopUp(Simulation sim, long cost)
        {
            long short_ = cost - TreasuryRules.State(sim).Cash;
            if (short_ <= 0) return 0;
            long round = Math.Max(1, Defs(sim).GiftRound);
            return (short_ + round - 1) / round * round;
        }

        /// <summary>The close has paid the lines: the collector's wage reaches the old village as income.</summary>
        internal static void NotePaid(Simulation sim, long[] paid)
        {
            if (!On(sim) || !Active(sim, UtilityDef.Collection)) return;
            int line = Def(sim, UtilityDef.Collection).ExtraLine;
            if (line >= 0 && paid[line] > 0) CohortRules.Earn(sim, paid[line]);
        }

        // ---- the counts the sheets show ----

        /// <summary>Of your households and the homes of the town: how many stand within the utility's reach.</summary>
        public static int HomesCovered(Simulation sim, int i, out int homes)
        {
            homes = 0;
            int n = 0;
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                if (b == null || b.TileId < 0) continue;
                homes++;
                if (CoverageRules.Within(sim, b.TileId, Def(sim, i).Tile, Def(sim, i).Reach)) n++;
            }
            return n;
        }

        /// <summary>Households' homes the tower waters that the pond and wells don't reach.</summary>
        public static int TowerOnly(Simulation sim)
        {
            int n = 0;
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                if (b != null && b.TileId >= 0 && !LandValueRules.PondOrWell(sim, b.TileId) && Covers(sim, UtilityDef.Tower, b.TileId)) n++;
            }
            return n;
        }

        /// <summary>Households' homes with no clean water within reach.</summary>
        public static int HomesWithoutWater(Simulation sim, out int homes)
        {
            homes = 0;
            int n = 0;
            foreach (Household x in sim.State.Citizens.Households)
            {
                Building b = sim.State.Land.Buildings.Find(y => y.Id == x.HomeId);
                if (b == null || b.TileId < 0) continue;
                homes++;
                if (!HasWater(sim, b.TileId)) n++;
            }
            return n;
        }
    }
}
