using System;
using System.Collections.Generic;
using System.Linq;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Town;

namespace Tools.Balancer
{
    /// <summary>
    /// US-100 (spec 023 §3.4, §9, ADR 0107 §5): the competent founder going for the Township after the Village. The gate asks for 20 households of
    /// yours (the doctor's counts), the clinic with its doctor, the paved link road, three businesses each with a profitable season and literacy of
    /// 60%. So the bot, on top of the Village bot (homes, a mill, the school): buys parcels and builds family houses on them to 21 homes, founds
    /// a bakery and a vegetable stall beside the mill, pays the clinic's share and builds the doctor's quarters, petitions for the road at 10
    /// households and runs the school's adult class whenever there are learners. The saver variant borrows nothing. It never touches taxes,
    /// policies or civic buildings: a town left alone must reach the Township.
    /// </summary>
    public sealed class TownshipBot : VillageBot
    {
        /// <summary>Homes wanted: 15 households and the doctor's.</summary>
        public const int Homes = 16;

        /// <summary>Houses being built at once.</summary>
        public int Concurrent { get; set; } = 1;

        /// <summary>What the bot does with land it has no use for (spec 024 §9, the land stress bots): nothing, lease it out, zone it for builders, or buy and hold it for the town's growth.</summary>
        public enum LandUse { None, Lease, Zone, Hold }

        public LandUse Land { get; set; }

        /// <summary>A policy to enact as soon as the town can (spec 022: the policy-affordability measurement, US-263 item 8), or null.</summary>
        public PolicyId? Policy { get; set; }

        /// <summary>The risk exploit bots (specs 026 and 027 §9, AC-19): every policy on, every forward contract accepted, every festival held with the treasury paying.</summary>
        public enum RiskUse { None, Insure, Contracts, Festivals }

        public RiskUse Risk { get; set; }

        /// <summary>Sprint 25's managed-against-hired measurement (spec 028 §3.2, §9): from the Township every business gets a manager of this style (0 cost-cutter, 1 growth-chaser, 2 quality-first), or null for hired workers as before.</summary>
        public int? ManagerStyle { get; set; }

        /// <summary>US-248 (spec 028 AC-31): found, run a season, sell and found again (a business sold is a business the base bot founds anew).</summary>
        public bool FoundAndSell { get; set; }

        /// <summary>US-248: the fair landlord answers every lease's renewal with the rent kept; the default (no answer) is the market rise to the cap.</summary>
        public bool FairRent { get; set; }

        private readonly List<int> _houseAcres = new List<int>();

        public TownshipBot(bool saver = false) : base(saver, "HMS", "flour_mill", "tea_stall", "veg_stall") { }

        public override string Name { get { return (Saver ? "township-saver" : "township") + (Land == LandUse.None ? "" : "-" + Land.ToString().ToLowerInvariant()) + (Policy == null ? "" : "-" + Policy.Value.ToString().ToLowerInvariant()) + (Risk == RiskUse.None ? "" : "-" + Risk.ToString().ToLowerInvariant()) + (ManagerStyle == null ? "" : "-managed" + ManagerStyle.Value) + (FoundAndSell ? "-sells" : "") + (FairRent ? "-fairrent" : ""); } }

        /// <summary>A bought acre kept for houses is never sown.</summary>
        protected override bool KeepsClear(Simulation sim, int tile) { return _houseAcres.Contains(tile) || base.KeepsClear(sim, tile); }

        public override void Play(Simulation sim, long t)
        {
            base.Play(sim, t);
            if (sim.State.Citizens == null || sim.State.Treasury == null) return;
            if (sim.State.Era.Current < 2) return; // the Village (hamlet 1, village 2)
            Clinic(sim, t);
            Road(sim, t);
            Class(sim, t);
            MoreHomes(sim, t);
            if (Policy != null && sim.State.Treasury.Opened && PolicyRules.CanEnact(sim, Policy.Value) == RejectReason.None && !PolicyRules.Of(sim, Policy.Value).Repealing) Ok(sim, new EnactPolicy(t, Policy.Value));
            if (Land != LandUse.None && sim.State.Township != null && sim.State.Township.Cohort.Joined) UseLand(sim, t);
            if (Risk != RiskUse.None && sim.State.Township != null && TownshipRules.Reached(sim)) UseRisk(sim, t);
            if (ManagerStyle != null && sim.State.Township != null && TownshipRules.Reached(sim)) UseManagers(sim, t);
            if (FoundAndSell && sim.State.Township != null && TownshipRules.Reached(sim)) SellBusinesses(sim, t);
            if (FairRent && sim.State.Citizens != null && t % 86_400 / 3600 == 8) foreach (long id in LeaseRules.Due(sim)) Ok(sim, new RenewLease(t, id, LeaseChoice.Keep));
        }

        /// <summary>Each open business that has run a season is put up for sale (an ordinary listing, 3 to 6 days); the base bot founds the next in its place.</summary>
        private void SellBusinesses(Simulation sim, long t)
        {
            if (t % 86_400 / 3600 != 13) return;
            foreach (Sim.Core.Business.Business b in sim.State.Businesses.Businesses)
                if (b.Status == Sim.Core.Business.BusinessStatus.Open && !b.Listed && t - b.ReadyAt >= sim.Clock.SecondsPerSeason) Ok(sim, new ListBusiness(t, b.Id, false));
        }

        /// <summary>A manager for each open business that has none: he works it himself, so its worker is let go first; his post is put up once and the applicant of the wanted style is hired at the asked wage when the business can pay the season's rest of it.</summary>
        private void UseManagers(Simulation sim, long t)
        {
            if (t % 86_400 / 3600 != 12) return;
            foreach (Sim.Core.Business.Business b in sim.State.Businesses.Businesses)
            {
                if (b.Status != Sim.Core.Business.BusinessStatus.Open || sim.State.People.ManagerAt(b.Id) != null) continue;
                if (sim.State.People.PostAt(HiringRules.ManagerPlace) == null) Ok(sim, new PostJob(t, HiringRules.ManagerPlace, false));
                // The style is the applicant's own (spec 028 §3.2): the bot picks the applicant of the style it wants, else the first.
                var applicants = sim.State.People.ApplicantsFor(HiringRules.ManagerPlace);
                Applicant a = applicants.Find(x => Sim.Core.Business.ManagerRules.StyleOf(sim, x) == ManagerStyle.Value) ?? applicants.FirstOrDefault();
                if (a == null) return;
                if (b.Worker == Sim.Core.Business.WorkerKind.Citizen || b.Worker == Sim.Core.Business.WorkerKind.Neighbour) Ok(sim, new LetWorkerGo(t, b.Id)); // he works it himself
                Ok(sim, new HireManager(t, a.Id, b.Id));
            }
        }

        /// <summary>The risk exploit bots: once a day at 10:00, whatever the Township offers of that kind is taken.</summary>
        private void UseRisk(Simulation sim, long t)
        {
            if (t % 86_400 / 3600 != 10) return;
            if (Risk == RiskUse.Insure)
            {
                Ok(sim, new SetCropInsurance(t, true));
                Ok(sim, new SetPropertyInsurance(t, true));
                foreach (Sim.Core.Business.Business b in sim.State.Businesses.Businesses) Ok(sim, new SetBusinessInsurance(t, b.Id, true));
            }
            else if (Risk == RiskUse.Contracts)
            {
                foreach (var offer in sim.State.Township.Contracts.Offers.ToList()) Ok(sim, new AcceptContract(t, offer.Id));
            }
            else if (!FestivalRules.DoneThisSeason(sim))
            {
                foreach (int venue in new[] { 0, 1 }) if (Ok(sim, new ScheduleFestival(t, venue, 0, 1))) return;
            }
        }

        /// <summary>The land stress bots: idle parcels leased or zoned; and each buys one serviced parcel a season to do it with (the holder sells it slowly once its value has risen a tenth).</summary>
        private void UseLand(Simulation sim, long t)
        {
            if (t % 86_400 / 3600 != 11) return;
            GameDefs defs = sim.Config.Game;
            foreach (Parcel p in sim.State.Land.Parcels)
            {
                if (!p.Owned) continue;
                string id = defs.Map.Parcels[p.Id].Id;
                if (Land == LandUse.Lease && LandLeaseRules.CanLease(sim, p.Id) == RejectReason.None) Ok(sim, new LeaseOutLand(t, id));
                else if (Land == LandUse.Zone && ZoningRules.CanZone(sim, p.Id) == RejectReason.None && ZoningRules.ZoneOf(p) == Zone.Agricultural) Ok(sim, new ZoneParcel(t, id));
                else if (Land == LandUse.Hold && !p.Protected && !p.Listed && p.CostBasis > 0 && LandRules.ParcelPrice(sim, p) * 10 >= p.CostBasis * 11) Ok(sim, new ListParcel(t, id));
            }
            if (sim.State.Clock.SimTime % sim.Clock.SecondsPerSeason >= 86_400) return; // a season's wait between buys
            foreach (Parcel p in sim.State.Land.Parcels)
            {
                if (p.Owned || !defs.Map.Parcels[p.Id].ForSale || !ZoningRules.Serviced(sim, p.Id) || !LandSystemAdjacent(sim, p.Id)) continue;
                long price = LandRules.ParcelPrice(sim, p);
                if (sim.State.Ledger.Cash < price + HomeReserve(sim)) continue; // the first season it can afford one
                if (Ok(sim, new BuyParcel(t, defs.Map.Parcels[p.Id].Id, null))) return;
            }
        }

        private static bool LandSystemAdjacent(Simulation sim, int parcel)
        {
            foreach (int tile in sim.Config.Game.Map.Parcels[parcel].Tiles)
            {
                int x = tile % sim.Config.Game.Map.Width, y = tile / sim.Config.Game.Map.Width;
                foreach (int n in new[] { y > 0 ? tile - sim.Config.Game.Map.Width : -1, x > 0 ? tile - 1 : -1, x < sim.Config.Game.Map.Width - 1 ? tile + 1 : -1, y < sim.Config.Game.Map.Height - 1 ? tile + sim.Config.Game.Map.Width : -1 })
                    if (n >= 0 && sim.State.Land.Parcels[sim.State.Land.Tiles[n].ParcelId].Owned) return true;
            }
            return false;
        }

        private long Spare(Simulation sim) { return sim.State.Ledger.Cash - HomeReserve(sim); }

        /// <summary>
        /// The share first (spec 023 §3.1), then the quarters, on the co-op's loan unless a saver; each only with the cash after its down payment
        /// and the close's costs kept back.
        /// </summary>
        private void Clinic(Simulation sim, long t)
        {
            if (!ClinicRules.Unlocked(sim)) return;
            if (ClinicRules.CanBuildClinic(sim) == RejectReason.None) Buy(sim, t, ClinicRules.Price(sim), f => new BuildClinic(t, f));
            else if (ClinicRules.CanBuildQuarters(sim) == RejectReason.None) Buy(sim, t, ClinicRules.QuartersPrice(sim), f => new BuildDoctorQuarters(t, f));
        }

        /// <summary>The paved road at 10 households (spec 023 §3.3).</summary>
        private void Road(Simulation sim, long t)
        {
            if (RoadRules.CanPetition(sim) != RejectReason.None) return;
            Buy(sim, t, RoadRules.Price(sim), f => new PetitionRoad(t, f));
        }

        private void Buy(Simulation sim, long t, long price, Func<Financing, Command> make)
        {
            long loan = Saver ? 0 : Loan(sim, price);
            if (sim.State.Ledger.Cash < price - loan + HomeReserve(sim)) return;
            Ok(sim, make(loan > 0 ? new Financing("coop_bank", loan, 8) : null));
        }

        private static long Loan(Simulation sim, long price)
        {
            if (sim.State.Credit == null) return 0;
            int bank = sim.Config.Game.Credit.IndexOfLender("coop_bank");
            long loan = Math.Min(price, CreditRules.Limit(sim, bank).Limit);
            loan -= loan % (1_000 * Money.Coin);
            return loan < sim.Config.Game.Credit.Lenders[bank].MinAmount ? 0 : loan;
        }

        /// <summary>Adult classes run back to back while there are learners (the school's own rule limits who and when).</summary>
        private void Class(Simulation sim, long t)
        {
            if (!SchoolRules.IsOpen(sim) || SchoolRules.ClassRunning(sim)) return;
            if (SchoolRules.CanStartClass(sim) != RejectReason.None) return;
            if (sim.State.Ledger.Cash < SchoolRules.ClassCost(sim) + HomeReserve(sim)) return;
            Ok(sim, new StartAdultClass(t));
        }

        /// <summary>Family houses on bought acres up to <see cref="Homes"/>: the next free place, else the cheapest parcel for sale.</summary>
        private void MoreHomes(Simulation sim, long t)
        {
            CitizensState town = sim.State.Citizens;
            if (!HouseRules.Unlocked(sim) || Town.Homes(sim) < Town.Households || Town.Homes(sim) >= Homes || town.Constructions.Count >= Concurrent) return;
            long cost = Sim.Core.Market.MarketRules.Indexed(sim, sim.Config.Game.Buildings[sim.Config.Game.Population.House].Value);
            long loan = Saver ? 0 : Loan(sim, cost);
            foreach (int acre in _houseAcres)
                if (HouseRules.Place(sim, acre) == RejectReason.None)
                {
                    if (sim.State.Ledger.Cash >= cost - loan + LivingCosts(sim))
                        Ok(sim, new BuildFamilyHouse(t, acre, loan > 0 ? new Financing("coop_bank", loan, 8) : null));
                    return;
                }
            if (t % 86_400 / 3600 != 9) return; // one parcel a day at most
            foreach (Parcel p in sim.State.Land.Parcels)
            {
                if (p.Owned || !sim.Config.Game.Map.Parcels[p.Id].ForSale) continue;
                if (sim.Config.Game.Map.Parcels[p.Id].Tiles.Any(tl => sim.Config.Game.Map.Tiles[tl].Terrain != Terrain.Farm)) continue; // houses go on farm acres only
                long price = LandRules.ParcelPrice(sim, p);
                long plan = Saver ? 0 : Math.Min(price, Loan(sim, price));
                if (sim.State.Ledger.Cash < price - plan + cost - loan + LivingCosts(sim)) continue;
                if (Ok(sim, new BuyParcel(t, sim.Config.Game.Map.Parcels[p.Id].Id, plan > 0 ? new Financing("coop_bank", plan, 16) : null)))
                {
                    foreach (int tile in sim.Config.Game.Map.Parcels[p.Id].Tiles) _houseAcres.Add(tile);
                    return;
                }
            }
        }
    }
}
