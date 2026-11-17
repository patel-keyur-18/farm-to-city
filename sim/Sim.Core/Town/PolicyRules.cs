using System;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>
    /// The five launch policies (spec 022 §3.5, §3.6, ADR 0109, ADR 0118): each costs a line in a department, starts after its lag, and has one
    /// effect. At most <c>maxActive</c> at a time; enacting waits for the next close to be paid; a policy the close can't pay in full is paused
    /// (no effect, nothing charged), never repealed by itself; a repealed one waits a season. None of the money goes to you.
    /// </summary>
    public static class PolicyRules
    {
        public static PolicyDefs Def(Simulation sim) { return sim.Config.Game?.Treasury?.Policies; }
        private static TreasuryState T(Simulation sim) { return sim.State.Treasury; }
        public static PolicyState Of(Simulation sim, PolicyId p) { return T(sim).Policies[(int)p]; }

        /// <summary>Taking a slot: enacted, active or paused, and not already repealed.</summary>
        public static bool Holds(PolicyState s) { return s.Status != PolicyStatus.None && !s.Repealing; }

        public static int Used(Simulation sim)
        {
            int n = 0;
            foreach (PolicyState s in T(sim).Policies) if (Holds(s)) n++;
            return n;
        }

        /// <summary>Its effect is on now: paid at the last close, and its lag has passed.</summary>
        public static bool Effective(Simulation sim, PolicyId p)
        {
            if (!TreasuryRules.IsOpen(sim)) return false;
            PolicyState s = Of(sim, p);
            return s.Status == PolicyStatus.Active && s.PaidCloses > Def(sim).Lag[(int)p];
        }

        /// <summary>Seasons left before the effect starts (0 once it has), for "Setting up".</summary>
        public static int SettingUp(Simulation sim, PolicyId p)
        {
            PolicyState s = Of(sim, p);
            if (s.Status == PolicyStatus.None || s.Status == PolicyStatus.Paused) return 0;
            return Math.Max(0, Def(sim).Lag[(int)p] + 1 - s.PaidCloses);
        }

        /// <summary>Seasons before a repealed policy can be enacted again, 0 when it can.</summary>
        public static long Wait(Simulation sim, PolicyId p)
        {
            PolicyState s = Of(sim, p);
            if (s.RepealedPeriod < 0) return 0;
            return Math.Max(0, s.RepealedPeriod + Def(sim).ReenactAfterSeasons - sim.Period);
        }

        /// <summary>Why a policy can't be enacted now, or None.</summary>
        public static RejectReason CanEnact(Simulation sim, PolicyId p)
        {
            if (!TreasuryRules.IsOpen(sim)) return RejectReason.NoTreasury;
            PolicyState s = Of(sim, p);
            if (Holds(s)) return RejectReason.AlreadyActive;
            if (Wait(sim, p) > 0) return RejectReason.WaitAfterRepeal;
            if (Used(sim) >= Def(sim).MaxActive) return RejectReason.SlotsFull;
            if (p == PolicyId.Meals && !SchoolRules.IsOpen(sim)) return RejectReason.NeedsBuilding;
            return RejectReason.None;
        }

        internal static RejectReason Enact(Simulation sim, PolicyId p)
        {
            RejectReason r = CanEnact(sim, p);
            if (r != RejectReason.None) return r;
            PolicyState s = Of(sim, p);
            s.Status = PolicyStatus.Enacted;
            s.EnactedPeriod = sim.Period;
            s.PaidCloses = 0;
            s.Repealing = false;
            s.Boost = false;
            sim.Emit(new PolicyEnacted(sim.State.Clock.SimTime, (int)p));
            return RejectReason.None;
        }

        /// <summary>A policy never yet paid ends at once; a running one ends at the next close (spec 022 §3.6).</summary>
        internal static RejectReason Repeal(Simulation sim, PolicyId p)
        {
            if (!TreasuryRules.IsOpen(sim)) return RejectReason.NoTreasury;
            PolicyState s = Of(sim, p);
            if (!Holds(s)) return RejectReason.NotActive;
            if (s.PaidCloses == 0) { Reset(sim, s, false); sim.Emit(new PolicyRepealed(sim.State.Clock.SimTime, (int)p)); return RejectReason.None; }
            s.Repealing = true;
            sim.Emit(new PolicyRepealed(sim.State.Clock.SimTime, (int)p));
            return RejectReason.None;
        }

        private static void Reset(Simulation sim, PolicyState s, bool wait)
        {
            s.Status = PolicyStatus.None;
            s.Repealing = false;
            s.PaidCloses = 0;
            s.Boost = false;
            s.RepealedPeriod = wait ? sim.Period : -1;
        }

        // ---- the line a policy pays ----

        /// <summary>The line is charged at the next close: enacted, active or paused, not repealed.</summary>
        public static bool LineActive(Simulation sim, int policy) { return Holds(T(sim).Policies[policy]); }

        /// <summary>A policy's cost a season at today's prices (spec 022 §3.5).</summary>
        public static long Cost(Simulation sim, PolicyId p)
        {
            PolicyDefs d = Def(sim);
            switch (p)
            {
                case PolicyId.Meals: return MarketRules.Indexed(sim, d.MealPerPupil) * Pupils(sim);
                case PolicyId.Subsidy: return MarketRules.Indexed(sim, d.SubsidyPerAcre) * NeighbourAcres(sim);
                case PolicyId.Grant: return Residents(sim) > 0 ? MarketRules.Indexed(sim, d.GrantAmount) : 0;
                case PolicyId.MinWage: return MarketRules.Indexed(sim, d.InspectorCost);
                default: return MarketRules.Indexed(sim, d.RentOfficerCost);
            }
        }

        /// <summary>The children of school age who can't read yet (spec 018): the school's pupils.</summary>
        public static int Pupils(Simulation sim) { return SchoolRules.Pupils(sim).Count; }
        private static int Residents(Simulation sim) { return sim.State.Citizens?.Population ?? 0; }

        /// <summary>The acres your neighbours farm: farm land on the map that isn't yours (spec 022 §3.5).</summary>
        public static int NeighbourAcres(Simulation sim)
        {
            var land = sim.State.Land;
            int n = 0;
            foreach (Land.Tile t in land.Tiles)
                if (t.Terrain == Terrain.Farm && t.ParcelId >= 0 && !land.Parcels[t.ParcelId].Owned) n++;
            return n;
        }

        /// <summary>
        /// After the close's two passes: a paid policy is active (the effect after its lag), an unpaid one is paused, a repealed one ends. The
        /// grant rolls its 40% waste. <paramref name="fullyPaid"/> says, by policy, whether its line was paid in full.
        /// </summary>
        internal static void AtClose(Simulation sim, bool[] fullyPaid)
        {
            PolicyDefs d = Def(sim);
            long now = sim.State.Clock.SimTime;
            for (int i = 0; i < PolicyState.Count; i++)
            {
                PolicyState s = T(sim).Policies[i];
                if (s.Hangover > 0) s.Hangover--;
                if (s.Status == PolicyStatus.None) continue;
                var id = (PolicyId)i;
                if (s.Repealing)
                {
                    bool was = s.Status == PolicyStatus.Active && s.PaidCloses > d.Lag[i];
                    Reset(sim, s, true);
                    if (id == PolicyId.Subsidy && was) s.Hangover = d.SubsidyHangoverSeasons;
                    continue;
                }
                if (!fullyPaid[i])
                {
                    if (s.Status != PolicyStatus.Paused) sim.Emit(new PolicyPaused(now, i));
                    s.Status = PolicyStatus.Paused;
                    s.Boost = false;
                    continue;
                }
                bool starting = s.Status != PolicyStatus.Active && s.PaidCloses + 1 > d.Lag[i];
                s.Status = PolicyStatus.Active;
                s.PaidCloses++;
                if (starting && s.PaidCloses > d.Lag[i]) sim.Emit(new PolicyStarted(now, i));
                if (id == PolicyId.Grant && Effective(sim, id)) RollGrant(sim, s);
            }
        }

        /// <summary>The grant, once it is running: 40% come to nothing (spent, no shop), the rest open a door for a newcomer (spec 022 §3.5).</summary>
        private static void RollGrant(Simulation sim, PolicyState s)
        {
            ulong r = Rng.Next(sim.State.Seed, RngStream.Grants, 0, (ulong)sim.Period);
            if (Rng.Chance(r, Def(sim).GrantWasted)) { s.Boost = false; sim.Emit(new GrantWasted(sim.State.Clock.SimTime)); return; }
            s.Boost = true;
        }

        // ---- the effects ----

        /// <summary>
        /// The minimum wage's floor on a wage a season: the market wage +10% (spec 022 §3.5). Applied to every ask and every renewal, so a hand,
        /// a business's worker and the treasury's clerk never go below it; a wage already paid stays until its renewal.
        /// </summary>
        public static long WageFloor(Simulation sim, long wage, long marketWage)
        {
            if (!Effective(sim, PolicyId.MinWage)) return wage;
            long floor = Money.RoundToCoin(MarketRules.Indexed(sim, Money.MulRate(marketWage, Fixed.One + Def(sim).MinWageAbove)));
            return Math.Max(wage, floor);
        }

        /// <summary>A labourer's day wage under the minimum wage (from the next day: it is read when labour is hired).</summary>
        public static long DayWageFloor(Simulation sim, long wage, long marketDay)
        {
            if (!Effective(sim, PolicyId.MinWage)) return wage;
            return Math.Max(wage, MarketRules.Indexed(sim, Money.MulRate(marketDay, Fixed.One + Def(sim).MinWageAbove)));
        }

        /// <summary>The most a lease's rent may rise at a renewal: rent control's 3%, else the content's usual cap (spec 022 §3.5).</summary>
        public static Fixed RentCap(Simulation sim, Fixed usual)
        {
            return Effective(sim, PolicyId.RentControl) ? (usual < Def(sim).RentControlCap ? usual : Def(sim).RentControlCap) : usual;
        }

        /// <summary>What the valley supplies of each crop, as a factor: the subsidy's 1.05, then its hangover's 0.97 (spec 022 §3.5).</summary>
        public static Fixed SupplyFactor(Simulation sim)
        {
            if (!TreasuryRules.IsOpen(sim)) return Fixed.One;
            if (Effective(sim, PolicyId.Subsidy)) return Def(sim).SubsidySupply;
            return Of(sim, PolicyId.Subsidy).Hangover > 0 ? Def(sim).SubsidyHangover : Fixed.One;
        }

        /// <summary>A pupil's household needs this share less of a child's food (free school meals).</summary>
        public static Fixed MealShare(Simulation sim)
        {
            return Effective(sim, PolicyId.Meals) ? Def(sim).MealShare : Fixed.Zero;
        }

        /// <summary>The children's extra health a season from the meals.</summary>
        public static Fixed MealHealth(Simulation sim)
        {
            return Effective(sim, PolicyId.Meals) ? Def(sim).MealHealth : Fixed.Zero;
        }

        /// <summary>A newcomer's chance is multiplied while the grant has opened a door; the door shuts at the close it is used or not (spec 022 §3.5).</summary>
        public static bool GrantBoost(Simulation sim) { return TreasuryRules.IsOpen(sim) && Of(sim, PolicyId.Grant).Boost; }

        internal static void UseGrantBoost(Simulation sim, bool opened)
        {
            Of(sim, PolicyId.Grant).Boost = false;
            if (opened) sim.Emit(new GrantOpenedShop(sim.State.Clock.SimTime));
        }
    }
}
