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

        private readonly List<int> _houseAcres = new List<int>();

        public TownshipBot(bool saver = false) : base(saver, "HMS", "flour_mill", "tea_stall", "veg_stall") { }

        public override string Name { get { return Saver ? "township-saver" : "township"; } }

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
