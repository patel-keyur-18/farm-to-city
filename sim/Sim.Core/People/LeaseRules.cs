using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.People
{
    /// <summary>What the player answers to a lease's renewal (US-240, spec 014 §3.14). <c>None</c> is no answer: the market rise applies.</summary>
    public enum LeaseChoice
    {
        None = 0,
        Market = 1,
        Smaller = 2,
        Keep = 3,
        Lower = 4,
    }

    /// <summary>The rents a lease's renewal could take now, and what each choice means (§3.14). Pure: it reads state and changes none.</summary>
    public sealed class LeaseOptions
    {
        public long Current { get; internal set; }

        /// <summary>The house's market rent today (kind × place × index).</summary>
        public long Market { get; internal set; }

        /// <summary>The market rise (§3.14): the least of the market rent, the 10% cap and what 30% of the income allows, never below the current rent.</summary>
        public long Allowed { get; internal set; }

        public long Smaller { get; internal set; }

        /// <summary>The lowered rent, or 0 when lowering isn't offered (the family isn't over the burden limit).</summary>
        public long Lowered { get; internal set; }

        /// <summary>The rent at which the household would pay <c>burdenLimit</c> of its income.</summary>
        public long Bearable { get; internal set; }

        public long Income { get; internal set; }

        /// <summary>The lease rent is above the market's: it renews at the market, with no card.</summary>
        public bool AboveMarket { get; internal set; }

        /// <summary>A card is shown: a raise is possible or lowering is offered.</summary>
        public bool CardDue { get { return Allowed > Current || Lowered > 0; } }

        /// <summary>What happens with no answer: the market rise if there is one, else the rent as it is.</summary>
        public LeaseChoice Default { get { return Allowed > Current ? LeaseChoice.Market : LeaseChoice.Keep; } }

        public bool Offers(LeaseChoice c)
        {
            switch (c)
            {
                case LeaseChoice.Market: return Allowed > Current;
                case LeaseChoice.Smaller: return Smaller > Current && Smaller < Allowed;
                case LeaseChoice.Keep: return true;
                case LeaseChoice.Lower: return Lowered > 0;
                default: return false;
            }
        }

        /// <summary>The rent a choice gives.</summary>
        public long Rent(LeaseChoice c)
        {
            if (AboveMarket) return Market;
            switch (c)
            {
                case LeaseChoice.Market: return Allowed;
                case LeaseChoice.Smaller: return Smaller;
                case LeaseChoice.Lower: return Lowered;
                default: return Current;
            }
        }
    }

    /// <summary>
    /// Rent by the kind of house, its place and a yearly lease (US-240, spec 014 §3.14, ADR 0107 §8). A household's rent is fixed for
    /// <c>leaseSeasons</c> seasons; in the last one the player is asked how to renew it, and the close that ends the lease applies the answer
    /// (or the market rise).
    /// </summary>
    public static class LeaseRules
    {
        private static PopulationDefs P(Simulation sim) { return sim.Config.Game.Population; }

        public static long Season(Simulation sim) { return CitizenRules.SeasonOf(sim, sim.State.Clock.SimTime); }

        /// <summary>The place factor of an acre (§3.14): the best road it touches by an edge, district road, lane, or none (inner).</summary>
        public static Fixed Place(Simulation sim, int tileId)
        {
            PopulationDefs p = P(sim);
            MapDef map = sim.Config.Game.Map;
            int x = tileId % map.Width, y = tileId / map.Width;
            bool district = false, lane = false;
            for (int i = 0; i < 4; i++)
            {
                int nx = x + (i == 1 ? -1 : i == 2 ? 1 : 0), ny = y + (i == 0 ? -1 : i == 3 ? 1 : 0);
                if (nx < 0 || ny < 0 || nx >= map.Width || ny >= map.Height) continue;
                int n = ny * map.Width + nx;
                if (Contains(p.DistrictRoadTiles, n)) district = true;
                else if (Contains(p.LaneRoadTiles, n)) lane = true;
            }
            return district ? p.RentDistrict : lane ? p.RentLane : p.RentInner;
        }

        private static bool Contains(IReadOnlyList<int> list, int id)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == id) return true;
            return false;
        }

        /// <summary>The market rent of a house on a tile at today's prices: the base × the place × the index, to a whole coin.</summary>
        public static long MarketRent(Simulation sim, int tileId)
        {
            long rent = Money.MulRate(MarketRules.Indexed(sim, P(sim).BaseRent), Place(sim, tileId));
            return rent / Money.Coin * Money.Coin;
        }

        /// <summary>A typical house's market rent (a lane acre, ×1.00): "about" in screens that don't know the house yet (§3.14).</summary>
        public static long TypicalRent(Simulation sim)
        {
            return Money.MulRate(MarketRules.Indexed(sim, P(sim).BaseRent), P(sim).RentLane) / Money.Coin * Money.Coin;
        }

        /// <summary>The market rent of the house a household lives in, or 0 when it isn't in a family house.</summary>
        public static long MarketRentFor(Simulation sim, Household x)
        {
            Building home = sim.State.Land.Buildings.Find(b => b.Id == x.HomeId);
            return home == null || !HouseRules.IsHouse(sim, home) ? 0 : MarketRent(sim, home.TileId);
        }

        /// <summary>A household with a lease: in a family house with nobody working as your hand (the house is his pay otherwise).</summary>
        public static bool Applies(Simulation sim, Household x)
        {
            return x.LeaseRent > 0 && HouseholdRules.InFamilyHouse(sim, x) && !HouseholdRules.HasHand(sim, x);
        }

        /// <summary>The lease's last season: the one in which the renewal is asked.</summary>
        public static bool InLastSeason(Simulation sim, Household x) { return Season(sim) - x.LeaseStart >= P(sim).LeaseSeasons - 1; }

        /// <summary>Seasons left to the lease, counting this one (1 in its last).</summary>
        public static long SeasonsLeft(Simulation sim, Household x) { return System.Math.Max(0, P(sim).LeaseSeasons - (Season(sim) - x.LeaseStart)); }

        /// <summary>What a renewal could take now (§3.14).</summary>
        public static LeaseOptions Options(Simulation sim, Household x)
        {
            PopulationDefs p = P(sim);
            long current = x.LeaseRent, market = MarketRentFor(sim, x);
            var o = new LeaseOptions { Current = current, Market = market, Income = x.Income };
            o.Bearable = Money.MulRate(x.Income, p.BurdenLimit) / Money.Coin * Money.Coin;
            if (current > market)
            {
                o.AboveMarket = true;
                o.Allowed = market;
                return o;
            }
            long cap = current + Money.MulRate(current, p.MaxRise) / Money.Coin * Money.Coin;
            long allowed = System.Math.Min(market, System.Math.Min(cap, o.Bearable));
            o.Allowed = System.Math.Max(current, allowed);
            if (o.Allowed > current) o.Smaller = current + (o.Allowed - current) / 2 / Money.Coin * Money.Coin;
            if (x.Income > 0 && current > o.Bearable) o.Lowered = current - Money.MulRate(current, p.LowerStep) / Money.Coin * Money.Coin;
            return o;
        }

        /// <summary>A lease starts when a household begins to pay rent: at the market rent, for <c>leaseSeasons</c> (§3.14).</summary>
        internal static void Begin(Simulation sim, Household x)
        {
            if (x.LeaseRent > 0) return;
            x.LeaseStart = Season(sim);
            x.LeaseRent = MarketRentFor(sim, x);
            x.Renewal = LeaseChoice.None;
        }

        /// <summary>The house is a hand's pay now, or the household has no house: no lease.</summary>
        internal static void End(Household x)
        {
            x.LeaseStart = -1;
            x.LeaseRent = 0;
            x.Renewal = LeaseChoice.None;
        }

        /// <summary>The player's answer (§3.14, <see cref="RenewLease"/>).</summary>
        internal static CommandResult Answer(Simulation sim, long householdId, LeaseChoice choice)
        {
            Household x = sim.State.Citizens?.Household(householdId);
            if (x == null) return CommandResult.Reject(RejectReason.UnknownHousehold);
            if (!Applies(sim, x)) return CommandResult.Reject(RejectReason.NoLease);
            if (!InLastSeason(sim, x)) return CommandResult.Reject(RejectReason.LeaseNotDue);
            LeaseOptions o = Options(sim, x);
            if (!o.CardDue || !o.Offers(choice)) return CommandResult.Reject(RejectReason.NotOffered);
            x.Renewal = choice;
            return CommandResult.Ok;
        }

        /// <summary>
        /// The close that ends a lease (§3.14, before the new budgets): the answered rent, or the market rise, starts the next lease; a
        /// smaller rise or a kept rent earns goodwill.
        /// </summary>
        internal static void AtClose(Simulation sim, Household x)
        {
            if (!Applies(sim, x)) return;
            PopulationDefs p = P(sim);
            long season = Season(sim);
            if (season - x.LeaseStart < p.LeaseSeasons) return;
            LeaseOptions o = Options(sim, x);
            bool answered = x.Renewal != LeaseChoice.None && o.Offers(x.Renewal);
            LeaseChoice choice = answered ? x.Renewal : o.Default;
            long old = x.LeaseRent, rent = o.Rent(choice);
            int goodwill = o.AboveMarket ? 0 : choice == LeaseChoice.Smaller ? p.GoodwillSmaller : (choice == LeaseChoice.Keep && o.Allowed > o.Current) || choice == LeaseChoice.Lower ? p.GoodwillKept : 0;
            x.LeaseRent = rent;
            x.LeaseStart = season;
            x.Renewal = LeaseChoice.None;
            x.Goodwill = goodwill;
            x.GoodwillUntil = goodwill > 0 ? season + p.LeaseSeasons - 1 : -1;
            sim.Emit(new LeaseRenewed(sim.State.Clock.SimTime, x.Id, old, rent, choice, answered));
        }

        /// <summary>The leases whose card is due now: in their last season, with a raise possible or lowering offered, in household id order.</summary>
        public static List<long> Due(Simulation sim)
        {
            var due = new List<long>();
            CitizensState s = sim.State.Citizens;
            if (s == null) return due;
            foreach (Household x in s.Households)
                if (Applies(sim, x) && InLastSeason(sim, x) && x.Renewal == LeaseChoice.None && Options(sim, x).CardDue) due.Add(x.Id);
            return due;
        }

        /// <summary>After the new budgets: tell the player which leases have entered their last season (one event, nothing waits on the answer).</summary>
        internal static void Offer(Simulation sim)
        {
            long season = Season(sim);
            var ids = new List<long>();
            foreach (long id in Due(sim))
                if (season - sim.State.Citizens.Household(id).LeaseStart == P(sim).LeaseSeasons - 1) ids.Add(id); // once, as the last season begins
            if (ids.Count > 0) sim.Emit(new LeasesOffered(sim.State.Clock.SimTime, ids));
        }

        /// <summary>Happiness a kind renewal still gives (§3.14).</summary>
        public static int GoodwillNow(Simulation sim, Household x) { return x.Goodwill > 0 && Season(sim) <= x.GoodwillUntil ? x.Goodwill : 0; }

        /// <summary>Whole years a household has lived in the town (§3.10, §3.14).</summary>
        public static int YearsHere(Simulation sim, Household x)
        {
            if (x.SinceSeason < 0) return 0;
            return (int)((Season(sim) - x.SinceSeason) / ClockDef.SeasonsPerYear);
        }

        /// <summary>Season closes below <c>leaveBelow</c> before a household leaves unhappy: <c>leaveSeasons</c> and a year more for each year lived here, at most <c>settledMax</c> (§3.10).</summary>
        public static int Patience(Simulation sim, Household x)
        {
            PopulationDefs p = P(sim);
            return System.Math.Min(p.SettledMax, p.LeaveSeasons + YearsHere(sim, x) * p.SettledPerYear);
        }

        /// <summary>A schema-17 town (§3.14, AC-23): each tenant's lease starts now at its present rent; every household settles now.</summary>
        internal static bool Migrate(Simulation sim)
        {
            CitizensState s = sim.State.Citizens;
            if (s == null) return false;
            long season = Season(sim);
            foreach (Household x in s.Households)
            {
                x.SinceSeason = season;
                if (x.Rent > 0 && HouseholdRules.InFamilyHouse(sim, x) && !HouseholdRules.HasHand(sim, x)) { x.LeaseStart = season; x.LeaseRent = x.Rent; }
            }
            return true;
        }
    }
}
