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
    /// <summary>The zones of a parcel (spec 024 §3.3). Agricultural is the default and stays farmland; Commercial (a newcomer's shop on the acre) is cut for now.</summary>
    public enum Zone
    {
        Agricultural = 0,
        Residential = 1,
    }

    /// <summary>
    /// Zoning and the builders (US-142, spec 024 §3.3, ADR 0119). The migration draw counts families that found no home; at the close, when
    /// enough are waiting, builders buy one parcel (yours if you zoned it residential, else a neighbour's for sale by the road, nearest the
    /// square first), build the homes, and the town's purse gets a permit fee. A builder's families are residents, not your households.
    /// </summary>
    public static class ZoningRules
    {
        public static ZoningDef Def(Simulation sim) { return sim.Config.Game?.Township?.Zoning; }

        /// <summary>From the Township on, with the content.</summary>
        public static bool On(Simulation sim) { return Def(sim) != null && LandValueRules.On(sim); }

        public static Zone ZoneOf(Parcel p) { return (Zone)p.Zone; }

        /// <summary>The acres of a parcel that can hold homes (farm acres).</summary>
        public static List<int> HomeTiles(Simulation sim, int parcelId)
        {
            var list = new List<int>();
            foreach (int t in sim.Config.Game.Map.Parcels[parcelId].Tiles)
                if (sim.State.Land.Tiles[t].Terrain == Terrain.Farm) list.Add(t);
            return list;
        }

        /// <summary>Families a builder needs to have been turned away for this parcel: <c>familiesPerAcre</c> for each acre.</summary>
        public static int Need(Simulation sim, int parcelId) { return Def(sim).FamiliesPerAcre * HomeTiles(sim, parcelId).Count; }

        /// <summary>Whether a parcel has the road and the water homes need (some acre of it touches the lane or the district road and some acre has water).</summary>
        public static bool Serviced(Simulation sim, int parcelId)
        {
            bool road = false, water = false;
            foreach (int t in HomeTiles(sim, parcelId))
            {
                if (LeaseRules.RoadAccess(sim, t) > 0) road = true;
                foreach (LandPart p in LandValueRules.Parts(sim, t)) if (p.Key == "water") water = true;
            }
            return road && water;
        }

        /// <summary>Why you can't zone a parcel, or None: from the Township, yours, not the inherited acres, not listed, nothing on it.</summary>
        public static RejectReason CanZone(Simulation sim, int parcelId)
        {
            if (!On(sim)) return RejectReason.NoTownship;
            Parcel p = sim.State.Land.Parcels[parcelId];
            if (!p.Owned) return RejectReason.NotOwned;
            if (p.Protected) return RejectReason.Protected;
            if (p.Locked) return p.LockReason;
            if (HomeTiles(sim, parcelId).Count == 0) return RejectReason.NotFarmTile;
            if (LandSystem.InUse(sim, parcelId)) return RejectReason.ParcelInUse;
            return RejectReason.None;
        }

        internal static RejectReason Set(Simulation sim, string parcelId, Zone zone)
        {
            if (sim.Config.Game == null) return RejectReason.UnknownParcel;
            int id = sim.Config.Game.Map.IndexOfParcel(parcelId);
            if (id < 0) return RejectReason.UnknownParcel;
            Parcel p = sim.State.Land.Parcels[id];
            if (zone == Zone.Agricultural)
            {
                if (!On(sim)) return RejectReason.NoTownship;
                if (!p.Owned) return RejectReason.NotOwned;
                if (p.Zone == 0) return RejectReason.NotZoned;
            }
            else
            {
                RejectReason r = CanZone(sim, id);
                if (r != RejectReason.None) return r;
                if (p.Zone == (int)zone) return RejectReason.AlreadyZoned;
            }
            p.Zone = (int)zone;
            sim.Emit(new AcreZoned(sim.State.Clock.SimTime, parcelId, (int)zone));
            return RejectReason.None;
        }

        // ---- the families turned away ----

        /// <summary>The hourly migration draw found no free house: one family is turned away, at the pace families would have come (spec 024 §3.3), from the Township.</summary>
        internal static void TurnAway(Simulation sim, long t)
        {
            if (!On(sim)) return;
            TownshipState s = TownshipRules.State(sim);
            if (s.NextTurnedAwayAt == 0) { s.NextTurnedAwayAt = t + MigrationRules.Wait(sim, t); return; }
            if (t < s.NextTurnedAwayAt) return;
            if (s.TurnedAway < Def(sim).TurnedAwayMax) s.TurnedAway++;
            s.NextTurnedAwayAt = 0;
        }

        /// <summary>A free house appeared: nobody is turned away now.</summary>
        internal static void HousesFree(Simulation sim)
        {
            TownshipState s = TownshipRules.State(sim);
            if (s != null) s.NextTurnedAwayAt = 0;
        }

        // ---- the builders at the close ----

        /// <summary>Parcels builders could buy, in the order they would: yours zoned residential first, then a neighbour's for sale by the road; nearest the square, then id.</summary>
        public static List<int> Candidates(Simulation sim)
        {
            GameDefs defs = sim.Config.Game;
            var mine = new List<int>();
            var theirs = new List<int>();
            var beside = new List<int>();
            for (int i = 0; i < defs.Map.Parcels.Count; i++)
            {
                Parcel p = sim.State.Land.Parcels[i];
                if (p.BuilderOwned || p.Protected || LandRules.Locked(sim, i)) continue;
                if (HomeTiles(sim, i).Count == 0 || !Serviced(sim, i)) continue;
                if (p.Owned)
                {
                    if (p.Zone != (int)Zone.Residential || p.Locked || LandSystem.InUse(sim, i) || LandSystem.WouldStrand(sim, i)) continue;
                    if (LandRules.ParcelPrice(sim, p) < Finance.CreditSystem.RequiredOnSale(sim, i)) continue;
                    mine.Add(i);
                }
                else if (defs.Map.Parcels[i].ForSale) (LandSystem.IsAdjacentToOwned(sim, i) ? beside : theirs).Add(i); // builders try the acres beside yours last (ADR 0126 §2, amended by ADR 0128)
            }
            Comparison<int> byDistance = (a, b) =>
            {
                int c = Nearest(sim, a).CompareTo(Nearest(sim, b));
                return c != 0 ? c : a.CompareTo(b);
            };
            mine.Sort(byDistance);
            theirs.Sort(byDistance);
            beside.Sort(byDistance);
            mine.AddRange(theirs);
            mine.AddRange(beside);
            return mine;
        }

        private static int Nearest(Simulation sim, int parcelId)
        {
            int square = LandValueRules.Def(sim).SquareTile, best = int.MaxValue;
            foreach (int t in sim.Config.Game.Map.Parcels[parcelId].Tiles) best = Math.Min(best, CoverageRules.Metres(sim, t, square));
            return best;
        }

        /// <summary>The close (spec 024 §3.9, after land value is read): at most one parcel, when enough families are waiting.</summary>
        internal static void Close(Simulation sim)
        {
            if (!On(sim)) return;
            TownshipState s = TownshipRules.State(sim);
            ZoningDef d = Def(sim);
            long season = LeaseRules.Season(sim);
            if (s.BuiltSeason == season) return;
            foreach (int id in Candidates(sim))
            {
                int need = Need(sim, id);
                if (s.TurnedAway < need) continue; // the nearest parcel may need more families than are waiting: the next may need fewer
                Parcel p = sim.State.Land.Parcels[id];
                string parcelId = sim.Config.Game.Map.Parcels[id].Id;
                long price = LandRules.ParcelPrice(sim, p);
                long t = sim.State.Clock.SimTime;
                bool yours = p.Owned;
                if (yours) LandSystem.Sell(sim, p, price);
                p.BuilderOwned = true;
                p.Zone = 0;
                s.TurnedAway -= need;
                s.BuiltSeason = season;
                s.BuilderParcels++;
                TreasuryRules.CollectFee(sim, "builder", Money.MulRate(price, d.PermitFee));
                sim.Emit(new AcreSoldToBuilder(t, parcelId, yours ? price : 0, yours));
                List<int> tiles = HomeTiles(sim, id);
                if (ConstructionRules.On(sim)) // from the Township the homes take materials and masons, after yours and the treasury's (spec 025 §3.2)
                {
                    int count = tiles.Count * d.HomesPerAcre;
                    ConstructionRules.Start(sim, "family_house", BuildProject.Builders, tiles[0], count, id, ConstructionRules.Quote(sim, "family_house", BuildProject.Builders, count), 0, 0);
                    return;
                }
                BuildHomes(sim, id, parcelId);
                return;
            }
        }

        /// <summary>The builders' homes stand (no materials before the Township, or when their materials and masons are done): two an acre, off your books.</summary>
        private static void BuildHomes(Simulation sim, int id, string parcelId)
        {
            TownshipState s = TownshipRules.State(sim);
            ZoningDef d = Def(sim);
            int homes = 0;
            LandState land = sim.State.Land;
            foreach (int tile in HomeTiles(sim, id))
                for (int k = 0; k < d.HomesPerAcre; k++)
                {
                    land.Buildings.Add(new Building { Id = land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = tile, Cost = 0, Owner = 1 });
                    homes++;
                }
            s.BuilderHomes += homes;
            sim.Emit(new HomesBuilt(sim.State.Clock.SimTime, parcelId, homes));
            CitizensState cs = sim.State.Citizens;
            if (cs != null) cs.NextFamilyAt = 0; // houses are free now: families come at the usual pace
        }

        /// <summary>The builders' materials and masons are done: their parcel's homes stand.</summary>
        internal static void FinishHomes(Simulation sim, BuildProject p)
        {
            BuildHomes(sim, p.Parcel, sim.Config.Game.Map.Parcels[p.Parcel].Id);
        }
    }
}
