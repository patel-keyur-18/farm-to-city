using System.Collections.Generic;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Scheduling;

namespace Sim.Core.People
{
    /// <summary>Why a household left the town (spec 014 §3.10, §3.13).</summary>
    public enum LeaveReason
    {
        LandTaken = 0,
        Unhappy = 1,
        RentBehind = 2,

        /// <summary>An elder living alone, behind on rent, gone to live with family (§3.13).</summary>
        ToFamily = 3,
    }

    /// <summary>The family house (spec 014 §3.4, §3.13): where it may stand, building it, rent, and the land being taken.</summary>
    public static class HouseRules
    {
        public const string Job = "house.build";

        public static bool IsHouse(Simulation sim, Building b) { return sim.Config.Game.Population != null && b.Def == sim.Config.Game.Population.House; }

        /// <summary>Family houses on a tile, standing or being built.</summary>
        public static int HousesOn(Simulation sim, int tileId)
        {
            int n = 0;
            if (sim.State.Citizens == null) return 0;
            foreach (Building b in sim.State.Land.Buildings) if (IsHouse(sim, b) && b.TileId == tileId) n++;
            foreach (Construction c in sim.State.Citizens.Constructions) if (c.TileId == tileId) n++;
            return n;
        }

        /// <summary>The era from which houses may be built (§3.13): the Hamlet.</summary>
        public static bool Unlocked(Simulation sim)
        {
            var onboarding = sim.Config.Game.Onboarding;
            if (sim.State.Era == null || onboarding == null) return true;
            for (int i = 0; i < onboarding.Eras.Count; i++)
                if (onboarding.Eras[i].Id == sim.Config.Game.Population.HouseUnlockEra) return sim.State.Era.Current >= i;
            return false;
        }

        /// <summary>
        /// Whether a family house may go on this tile (§3.4, §3.13): from the Hamlet, on an owned farm acre with nothing growing and fewer
        /// than <c>perAcre</c> houses, or on a free place in the labour quarters.
        /// </summary>
        public static RejectReason Place(Simulation sim, int tileId)
        {
            if (sim.State.Citizens == null || tileId < 0 || tileId >= sim.State.Land.Tiles.Count) return RejectReason.UnknownTile;
            if (!Unlocked(sim)) return RejectReason.EraLocked;
            Tile tile = sim.State.Land.Tiles[tileId];
            if (!sim.State.Land.Parcels[tile.ParcelId].Owned) return RejectReason.NotOwned;
            if (tile.Terrain == Terrain.Quarters)
                return PeopleRules.HutsOn(sim, tileId) + HousesOn(sim, tileId) >= sim.Config.Game.People.HutPlacesPerAcre ? RejectReason.NoRoom : RejectReason.None;
            if (tile.Terrain != Terrain.Farm) return RejectReason.TerrainNotAllowed;
            if (sim.State.Farm.CropOn(tileId) != null) return RejectReason.TileOccupied;
            return HousesOn(sim, tileId) >= sim.Config.Game.Population.HousesPerAcre ? RejectReason.NoRoom : RejectReason.None;
        }

        /// <summary>The building is finished (§3.4).</summary>
        internal sealed class Build : IJobHandler
        {
            public string JobType { get { return Job; } }
            public SystemOrder System { get { return SystemOrder.Population; } }

            public void OnDue(Simulation sim, Scheduling.Job job)
            {
                CitizensState s = sim.State.Citizens;
                Construction c = s.Constructions.Find(x => x.Id == job.TargetId);
                LandState land = sim.State.Land;
                var b = new Building { Id = land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = c.TileId, Cost = c.Cost };
                land.Buildings.Add(b);
                s.Constructions.Remove(c);
                sim.CompleteJob(job);
                sim.Emit(new FamilyHouseBuilt(sim.State.Clock.SimTime, b.Id, c.TileId));
            }
        }

        /// <summary>
        /// Rent at the close (§3.4), for the season that ended: a household in a family house with nobody working as your hand pays
        /// <c>rentPerSeason</c> × index from what its budget set aside, then from savings; short of both it falls a season behind.
        /// </summary>
        internal static void CollectRent(Simulation sim, Household x)
        {
            if (x.Rent <= 0) return;
            long t = sim.State.Clock.SimTime;
            long setAside = System.Math.Max(0, System.Math.Min(x.Rent, x.Income - x.Food - x.Clothing - x.HealthCare - x.Schooling));
            long fromSavings = x.Rent - setAside;
            if (fromSavings > x.Savings)
            {
                x.RentBehind++;
                sim.Emit(new RentMissed(t, x.Id, x.Rent, x.RentBehind));
                return;
            }
            x.Savings -= fromSavings;
            x.RentBehind = 0;
            sim.State.Ledger.Post(t, sim.Period, "ledger.rent", "household:" + x.Id, new Posting(Account.Cash, x.Rent), new Posting(Account.RentReceived, -x.Rent));
            sim.Emit(new RentPaid(t, x.Id, x.Rent));
        }

        /// <summary>
        /// Foreclosure takes bought land (spec 005; §3.13): a family house on it goes with the land at its book value (one being built at
        /// its cost) toward the debt, and the household living there leaves. Returns the value raised.
        /// </summary>
        internal static long Seize(Simulation sim, int parcelId, long period)
        {
            if (sim.State.Citizens == null) return 0;
            LandState land = sim.State.Land;
            long t = sim.State.Clock.SimTime, raised = 0;
            foreach (Building b in land.Buildings.ToArray())
            {
                if (!IsHouse(sim, b) || land.Tiles[b.TileId].ParcelId != parcelId) continue;
                long book = b.Cost - b.Depreciated;
                sim.State.Ledger.Post(t, period, "ledger.houseSeized", "building:" + b.Id, new Posting(Account.Cash, book),
                    new Posting(Account.AccumulatedDepreciation, b.Depreciated), new Posting(Account.Buildings, -b.Cost));
                raised += book;
                Household x = sim.State.Citizens.Households.Find(h => h.HomeId == b.Id);
                if (x != null) Evict(sim, x, LeaveReason.LandTaken);
                land.Buildings.Remove(b);
                LandSystem.SetOccupied(sim, b.TileId, false);
            }
            foreach (Construction c in sim.State.Citizens.Constructions.ToArray())
            {
                if (land.Tiles[c.TileId].ParcelId != parcelId) continue;
                sim.State.Ledger.Post(t, period, "ledger.houseSeized", "construction:" + c.Id, new Posting(Account.Cash, c.Cost), new Posting(Account.Buildings, -c.Cost));
                raised += c.Cost;
                sim.CompleteJob(sim.State.Jobs.Find(c.JobId));
                sim.State.Citizens.Constructions.Remove(c);
                LandSystem.SetOccupied(sim, c.TileId, false);
            }
            return raised;
        }

        /// <summary>A household leaves (§3.10): a hand among them ends his contract (no severance; the season's unused wages come back).</summary>
        internal static void Evict(Simulation sim, Household x, LeaveReason reason)
        {
            int people = x.Members.Count, name = sim.State.Citizens.Citizen(x.Members[0]).Name; // a hand alone in his hut is gone once he leaves
            foreach (long id in new List<long>(x.Members))
            {
                Citizen c = sim.State.Citizens.Citizen(id);
                if (c.Job != CitizenJob.Hand) continue;
                Hand h = sim.State.People.Hand(c.HandId);
                if (h == null) continue;
                if (h.PrepaymentId != 0 && sim.State.Finance.FindPrepayment(h.PrepaymentId) != null) Accrual.Close(sim, h.PrepaymentId);
                PeopleSystem.Leave(sim, h);
                sim.Emit(new HandLeft(sim.State.Clock.SimTime, h.Id));
            }
            sim.Emit(new HouseholdLeft(sim.State.Clock.SimTime, x.Id, people, reason, name));
            if (sim.State.Citizens.Households.Contains(x)) CitizenRules.Leave(sim, x);
        }
    }
}
