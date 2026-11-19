using System;
using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;

namespace Sim.Core.Town
{
    /// <summary>Why a family house is or is not offered the pucca upgrade, in the order the rules are checked (spec 024 §3.7).</summary>
    public enum UpgradeWhy
    {
        Offered = 0,

        /// <summary>Before the Township: land value and construction begin with it.</summary>
        Closed = 1,

        /// <summary>Not a family house.</summary>
        NotHouse = 2,
        Pucca = 3,

        /// <summary>The upgrade is being built.</summary>
        Underway = 4,

        /// <summary>The acre is listed for sale or leased out.</summary>
        Locked = 5,
        LandValue = 6,
        Water = 7,

        /// <summary>The pucca rent is over what the family's income can bear.</summary>
        Rent = 8,
    }

    /// <summary>The numbers behind an offer (or its refusal): the sheet and the card say each in words.</summary>
    public sealed class UpgradeCheck
    {
        public UpgradeWhy Why { get; internal set; }
        public bool Offered { get { return Why == UpgradeWhy.Offered; } }
        public Fixed LandValue { get; internal set; }
        public Fixed Needs { get; internal set; }

        /// <summary>The pucca rent of the acre today and what the house lets for now (market, kutcha).</summary>
        public long PuccaRent { get; internal set; }
        public long KutchaRent { get; internal set; }

        /// <summary>The household in it (null when empty), whether it is a hand's (it pays no rent), and its income and what of it the rent may take.</summary>
        public Household Family { get; internal set; }
        public bool Hand { get; internal set; }
        public long Income { get; internal set; }
        public long Bearable { get; internal set; }
    }

    /// <summary>
    /// Making a kutcha family house pucca (spec 024 §3.7, US-152, ADR 0131). An upgrade is a build: the difference to a pucca house in bricks, cement,
    /// timber and mason-days (<c>pucca_upgrade</c> in the construction kinds), paid at the start; the family stays; the house lets at the pucca rent
    /// from the next renewal on, by the usual capped step. The builders upgrade one of their homes a season by the same rules.
    /// </summary>
    public static class UpgradeRules
    {
        public static UpgradeDef Def(Simulation sim) { return sim.Config.Game?.Township?.Upgrade; }

        public static bool On(Simulation sim) { return Def(sim) != null && ConstructionRules.On(sim) && LandValueRules.On(sim); }

        /// <summary>A pucca house's rent on an acre today: the pucca base × the place × prices, to a whole coin.</summary>
        public static long PuccaRent(Simulation sim, int tileId) { return LeaseRules.MarketRent(sim, tileId, true); }

        /// <summary>The build making this house pucca, or null.</summary>
        public static BuildProject Underway(Simulation sim, long buildingId)
        {
            TownshipState s = sim.State.Township;
            if (s == null) return null;
            foreach (BuildProject p in s.Projects) if (p.Upgrade && p.House == buildingId) return p;
            return null;
        }

        /// <summary>The household living in a house, or null.</summary>
        public static Household FamilyIn(Simulation sim, Building b)
        {
            return sim.State.Citizens?.Households.Find(h => h.HomeId == b.Id);
        }

        /// <summary>Whether, and why not (spec 024 §3.7): land value at the bar, clean water near, and a family able to pay the pucca rent (an empty or hand's house has no rent to bear).</summary>
        public static UpgradeCheck Check(Simulation sim, Building b)
        {
            UpgradeDef d = Def(sim);
            var c = new UpgradeCheck();
            if (!On(sim)) { c.Why = UpgradeWhy.Closed; return c; }
            if (!HouseRules.IsHouse(sim, b)) { c.Why = UpgradeWhy.NotHouse; return c; }
            c.LandValue = LandValueRules.Of(sim, b.TileId);
            c.Needs = d.PuccaMinLandValue;
            c.PuccaRent = PuccaRent(sim, b.TileId);
            c.KutchaRent = LeaseRules.MarketRent(sim, b.TileId);
            c.Family = FamilyIn(sim, b);
            if (c.Family != null)
            {
                c.Hand = HouseholdRules.HasHand(sim, c.Family);
                c.Income = c.Family.Income;
                c.Bearable = Money.MulRate(c.Income, sim.Config.Game.Population.BurdenLimit) / Money.Coin * Money.Coin;
            }
            if (b.Pucca) c.Why = UpgradeWhy.Pucca;
            else if (Underway(sim, b.Id) != null) c.Why = UpgradeWhy.Underway;
            else if (sim.State.Land.Parcels[sim.State.Land.Tiles[b.TileId].ParcelId].Locked) c.Why = UpgradeWhy.Locked;
            else if (c.LandValue < d.PuccaMinLandValue) c.Why = UpgradeWhy.LandValue;
            else if (!LandValueRules.HasWater(sim, b.TileId)) c.Why = UpgradeWhy.Water;
            else if (c.Family != null && !c.Hand && c.Income > 0 && c.PuccaRent > c.Bearable) c.Why = UpgradeWhy.Rent;
            return c;
        }

        public static BuildQuote Quote(Simulation sim, int owner) { return ConstructionRules.Quote(sim, UpgradeDef.Kind, owner, 1); }

        /// <summary>Seasons the upgrade takes to come back: its cost over the rise in rent once the pucca rent is reached; 0 when nothing comes back (an empty or hand's house).</summary>
        public static long PaybackSeasons(Simulation sim, UpgradeCheck c, long cost)
        {
            if (c.Family == null || c.Hand) return 0;
            long rise = c.PuccaRent - c.KutchaRent;
            return rise <= 0 ? 0 : (cost + rise - 1) / rise;
        }

        /// <summary>The player's order (<see cref="UpgradeHouse"/>): paid at the start, capitalised into the house's book value.</summary>
        internal static CommandResult Order(Simulation sim, long buildingId)
        {
            if (!On(sim)) return CommandResult.Reject(RejectReason.NoTownship);
            Building b = sim.State.Land.Buildings.Find(x => x.Id == buildingId);
            if (b == null) return CommandResult.Reject(RejectReason.UnknownBuilding);
            if (b.Builders || !Check(sim, b).Offered) return CommandResult.Reject(RejectReason.NotOffered);
            BuildQuote q = Quote(sim, BuildProject.Yours);
            long cost = q.Total;
            if (!sim.State.Ledger.CanAfford(cost)) return CommandResult.Reject(RejectReason.InsufficientFunds);
            long t = sim.State.Clock.SimTime;
            sim.State.Ledger.Post(t, sim.Period, "ledger.upgradeHouse", "house:" + b.Id, new Posting(Account.Buildings, cost), new Posting(Account.Cash, -cost));
            b.Cost += cost;
            sim.Emit(new HouseUpgradeStarted(t, b.Id, b.TileId, b.Owner));
            ConstructionRules.Start(sim, UpgradeDef.Kind, BuildProject.Yours, b.TileId, 1, -1, q, cost, b.Id);
            return CommandResult.Ok;
        }

        /// <summary>The build is done: the house is pucca (its rent moves at the family's next renewal).</summary>
        internal static void OnBuilt(Simulation sim, BuildProject p)
        {
            Building b = sim.State.Land.Buildings.Find(x => x.Id == p.House);
            if (b == null) return;
            b.Pucca = true;
            sim.Emit(new HouseUpgraded(sim.State.Clock.SimTime, b.Id, b.TileId, b.Owner));
        }

        /// <summary>A house taken with its land: the upgrade under way goes (its masons are free, their pay was never paid out).</summary>
        internal static void Cancel(Simulation sim, long buildingId)
        {
            BuildProject p = Underway(sim, buildingId);
            if (p == null) return;
            if (p.Working) sim.CompleteJob(sim.State.Jobs.Find(p.JobId));
            sim.State.Township.Projects.Remove(p);
        }

        /// <summary>Builders' homes that are pucca, and all of them.</summary>
        public static int BuildersPucca(Simulation sim)
        {
            int n = 0;
            foreach (Building b in sim.State.Land.Buildings) if (b.Builders && b.Pucca) n++;
            return n;
        }

        /// <summary>The close: the builders make one of their homes pucca (the first in building order that is offered), unless one is being made already.</summary>
        internal static void Close(Simulation sim)
        {
            if (!On(sim)) return;
            TownshipState s = sim.State.Township;
            foreach (BuildProject p in s.Projects) if (p.Upgrade && p.Owner == BuildProject.Builders) return;
            foreach (Building b in sim.State.Land.Buildings)
            {
                if (!b.Builders || !Check(sim, b).Offered) continue;
                ConstructionRules.Start(sim, UpgradeDef.Kind, BuildProject.Builders, b.TileId, 1, -1, Quote(sim, BuildProject.Builders), 0, b.Id);
                sim.Emit(new HouseUpgradeStarted(sim.State.Clock.SimTime, b.Id, b.TileId, b.Owner));
                return;
            }
        }
    }
}
