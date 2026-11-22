using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>
    /// Seeded Phase 1 situations for the views' sweeping tests (spec 008 AC-1, AC-15): a new game, a growing farm with people and
    /// trees, the same farm ready to harvest with goods on offer, and a later game with loans and a closed season.
    /// </summary>
    public static class Situations
    {
        private const long Hour = 3600, Day = 86400;

        public static Simulation Rich(ulong seed = 42)
        {
            return ViewKit.NewGame(seed: seed, edit: (f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j);
        }

        private static void Ok(Simulation sim, Command c) { Assert.That(sim.Execute(c).Accepted, Is.True, c.GetType().Name + " " + sim.Preview(c).Reason); }

        private static long T(Simulation sim) { return sim.State.Clock.SimTime; }

        /// <summary>Potato on the well acres, a garden bed, a mango, a hut and a hand with an order.</summary>
        public static Simulation Growing()
        {
            Simulation sim = Rich();
            Ok(sim, new Sow(T(sim), 86, "potato", SeedSource.Certified, "compost"));
            Ok(sim, new Sow(T(sim), 102, "radish", SeedSource.Certified, "none"));
            GardenBed bed = sim.State.Farm.Beds[0];
            Ok(sim, new SowGarden(T(sim), bed.Id, "garden_spinach"));
            TreeSlot slot = sim.State.Land.Slots.First(s => s.Kind == SlotKind.Yard && s.TreeId == 0);
            Ok(sim, new PlantTree(T(sim), slot.Id, "mango"));
            Ok(sim, new BuildHandHut(T(sim), 119));
            for (int i = 0; i < 24 * 10 && sim.State.People.Hands.Count == 0; i++)
            {
                sim.AdvanceTo(T(sim) + Hour);
                if (sim.State.People.Applicants.Count > 0) Ok(sim, new OfferContract(T(sim), sim.State.People.Applicants[0].Id));
            }
            Assert.That(sim.State.People.Hands.Count, Is.EqualTo(1));
            Ok(sim, new SetLabourSplit(T(sim), 25));
            sim.AdvanceTo(T(sim) + 2 * Hour);
            return sim;
        }

        /// <summary>The growing farm a day and a half later: ready crops and goods on offer.</summary>
        public static Simulation Ready()
        {
            Simulation sim = Growing();
            sim.AdvanceTo(T(sim) + 30 * Hour);
            foreach (Crop c in sim.State.Farm.Crops.Where(c => c.State == CropState.Ready && c.TileId == 102).ToList())
                Ok(sim, new Harvest(T(sim), c.TileId));
            int radish = sim.Config.Game.Market.Goods.ToList().FindIndex(g => g.Id == "radish");
            if (sim.State.Market.InStore(radish) > 0) Ok(sim, new Offer(T(sim), "radish", sim.State.Market.InStore(radish) / 2));
            return sim;
        }

        /// <summary>A moneylender loan and a closed season (a report), with the events of the last away advance.</summary>
        public static Simulation Later(out IReadOnlyList<SimEvent> away)
        {
            Simulation sim = Ready();
            Ok(sim, new TakeLoan(T(sim), "moneylender", 20_000 * 100, 4));
            sim.DrainEvents();
            long next = (T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(next + 2 * Hour, AdvanceMode.Away);
            away = sim.DrainEvents();
            Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThan(0));
            return sim;
        }

        /// <summary>
        /// The Hamlet with a family house (spec 014 §7): the hand alone in his hut, and Manoj's family of five in the house (his mother,
        /// his wife from another village, a son and a daughter) with his late father kept in the tree.
        /// </summary>
        public static Simulation Town()
        {
            Simulation sim = Ready();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
            Ok(sim, new BuildFamilyHouse(T(sim), HouseTile(sim)));
            sim.AdvanceTo(T(sim) + 48 * Hour);
            Building house = sim.State.Land.Buildings.Single(b => HouseRules.IsHouse(sim, b));
            CitizensState town = sim.State.Citizens;
            var x = new Household { Id = town.NextHouseholdId++, HomeId = house.Id };
            Citizen Add(string name, int age, long father = 0, long mother = 0)
            {
                int n = sim.Config.Game.People.Names.ToList().FindIndex(d => d.Id == name);
                var c = new Citizen { Id = town.NextCitizenId++, Name = n, Female = sim.Config.Game.People.Names[n].Female, BornSeason = CitizenRules.BornFor(sim, age),
                    HouseholdId = x.Id, Health = Fixed.One, Father = father, Mother = mother, Skill = Fixed.FromInt(40), Village = 1 };
                town.Citizens.Add(c);
                return c;
            }
            Citizen grandfather = Add("mohan", 74), grandmother = Add("kamala", 66);
            grandfather.Gone = CitizenGone.Died;
            grandfather.GoneAt = T(sim);
            grandfather.Spouse = grandmother.Id;
            grandmother.Spouse = grandfather.Id;
            Citizen manoj = Add("manoj", 38, grandfather.Id, grandmother.Id), sita = Add("usha", 35);
            manoj.Spouse = sita.Id;
            sita.Spouse = manoj.Id;
            Citizen ravi = Add("ravi", 12, manoj.Id, sita.Id), asha = Add("asha", 9, manoj.Id, sita.Id);
            foreach (Citizen c in new[] { manoj, sita, ravi, asha, grandmother }) x.Members.Add(c.Id);
            town.Households.Add(x);
            HouseholdRules.SetBudget(sim, x);
            return sim;
        }

        /// <summary>
        /// The ready farm as a Village with irrigation (spec 013 §3.1): the pump on the well's acres and one more, channels on 85 and 101,
        /// a borewell on 100, drip on a well acre, and a season closed so the running costs are paid.
        /// </summary>
        public static Simulation Irrigated()
        {
            Simulation sim = Ready();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            Ok(sim, new SetCashForTest(T(sim), 5_00_000_00));
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new SetPumpAcres(T(sim), new[] { 86, 102, 84 }));
            Ok(sim, new BuildChannel(T(sim), 85));
            Ok(sim, new BuildChannel(T(sim), 101));
            Ok(sim, new DrillBorewell(T(sim), 100));
            Ok(sim, new LayDrip(T(sim), 86));
            sim.AdvanceTo((T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + Hour);
            return sim;
        }

        /// <summary>The Village with both sheds, the ox plough and a tractor bought (maintenance skipped), past a close (spec 013 §3.2).</summary>
        public static Simulation Equipped()
        {
            Simulation sim = Ready();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            Ok(sim, new SetCashForTest(T(sim), 20_00_000_00));
            Ok(sim, new BuildShed(T(sim)));
            sim.AdvanceTo(T(sim) + 25 * Hour);
            int field = System.Linq.Enumerable.First(System.Linq.Enumerable.Range(0, sim.State.Land.Tiles.Count),
                t => Sim.Core.Livestock.LivestockRules.PlaceSecond(sim, t) == RejectReason.None);
            Ok(sim, new BuildShed(T(sim), field));
            sim.AdvanceTo(T(sim) + 25 * Hour);
            Ok(sim, new BuyMachine(T(sim), "ox_plough"));
            Ok(sim, new BuyMachine(T(sim), "tractor"));
            Ok(sim, new SetMaintenance(T(sim), sim.State.Equipment.Machines[1].Id, false));
            sim.AdvanceTo((T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + Hour);
            return sim;
        }

        /// <summary>
        /// The sprint-23 farm: the Equipped farm with its tractor let out to neighbours, heifers kept, a silage pit and a chaff cutter, and the
        /// pump with its solar panel (US-254 to US-257), so the Why? sweep reaches their sheets.
        /// </summary>
        public static Simulation Promises()
        {
            Simulation sim = Equipped();
            Ok(sim, new SetCashForTest(T(sim), 20_00_000_00));
            Ok(sim, new SetKeepHeifers(T(sim), true));
            Ok(sim, new BuildSilagePit(T(sim)));
            Ok(sim, new BuyChaffCutter(T(sim)));
            Ok(sim, new SetHireOut(T(sim), Sim.Core.Equipment.EquipmentRules.OfKind(sim, sim.Config.Game.Equipment.IndexOf("tractor")).Id, true));
            Ok(sim, new InstallPump(T(sim)));
            Ok(sim, new FitSolarPanel(T(sim), 1));
            sim.AdvanceTo(T(sim) + 2 * 86_400);
            sim.DrainEvents();
            return sim;
        }

        /// <summary>Your cash set to <paramref name="amount"/> (1/100 coin) with a balanced test entry.</summary>
        public static void SetCash(Simulation sim, long amount) { sim.Execute(new SetCashForTest(T(sim), amount)); }

        /// <summary>Sets cash with a balanced test entry against opening equity.</summary>
        private sealed class SetCashForTest : TimedCommand
        {
            private readonly long _amount;
            public SetCashForTest(long at, long amount) : base(at) { _amount = amount; }
            protected override CommandResult ApplyAt(Simulation sim)
            {
                long d = _amount - sim.State.Ledger.Cash;
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "ledger.opening", "test",
                    new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.Cash, d), new Sim.Core.Finance.Posting(Sim.Core.Finance.Account.OpeningEquity, -d));
                return CommandResult.Ok;
            }
        }

        /// <summary>An owned farm acre with nothing growing, for a family house.</summary>
        public static int HouseTile(Simulation sim)
        {
            return sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && sim.State.Land.Parcels[t.ParcelId].Owned && sim.State.Farm.CropOn(t.Id) == null).Id;
        }

        /// <summary>The town with a flour mill founded on an empty acre (hired worker), built, and a working day gone by (spec 016).</summary>
        public static Simulation Mill()
        {
            Simulation sim = Town();
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "flour_mill", tile, 150_000_00, false));
            Hire(sim);
            sim.AdvanceTo((T(sim) / 86_400 + 2) * 86_400 + 12 * 3600);
            return sim;
        }

        /// <summary>
        /// The newest business's worker from the board (spec 015): word sent, the most skilled townsperson hired at what they ask; with
        /// nobody in town, a commuter at skill 50 (as spec 016's stopgap took them).
        /// </summary>
        private static void Hire(Simulation sim)
        {
            var b = sim.State.Businesses.Businesses.Last();
            Ok(sim, new PostJob(T(sim), b.Id, false));
            Applicant a = sim.State.People.ApplicantsFor(b.Id).FirstOrDefault(x => x.CitizenId != 0);
            if (a != null)
            {
                Ok(sim, new OfferContract(T(sim), a.Id));
                return;
            }
            int steady = sim.Config.Game.People.IndexOfTrait("steady");
            var commuter = new Applicant { Id = sim.State.People.NextApplicantId++, Skill = 50, Trait1 = steady, Trait2 = steady, Place = b.Id };
            HiringRules.HireWorker(sim, commuter, HiringRules.OfferWage(sim, commuter, OfferLevel.Asked), 0);
        }

        /// <summary>
        /// The hiring board busy (spec 015 §7): the mill's worker let go and word sent for another (townspeople asking, then a migrant
        /// a reference was asked about), and word out for a farm hand.
        /// </summary>
        public static Simulation Board()
        {
            Simulation sim = Mill();
            var mill = sim.State.Businesses.Businesses[0];
            Ok(sim, new LetWorkerGo(T(sim), mill.Id));
            Ok(sim, new PostJob(T(sim), mill.Id, true));
            sim.AdvanceTo(T(sim) + 20 * Hour);
            Applicant migrant = sim.State.People.ApplicantsFor(mill.Id).FirstOrDefault(x => x.CitizenId == 0);
            if (migrant != null) Ok(sim, new CheckReference(T(sim), migrant.Id));
            sim.Execute(new PostJob(T(sim), 0, false));
            return sim;
        }

        /// <summary>The Township with a brick kiln founded on a free acre, a hand hired and built (US-243), the clock at the first moment it is open.</summary>
        public static Simulation Kiln()
        {
            Simulation sim = Township();
            SetCash(sim, 2_000_000 * 100);
            int tile = -1;
            foreach (Tile t in sim.State.Land.Tiles) if (tile < 0 && BusinessRules.Place(sim, t.Id) == RejectReason.None && HouseRules.Place(sim, t.Id) == RejectReason.None) tile = t.Id;
            Ok(sim, new FoundBusiness(T(sim), "brick_kiln", tile, 400_000 * 100, false));
            Hire(sim);
            var b = sim.State.Businesses.Businesses.Last();
            sim.AdvanceTo(b.ReadyAt + 60);
            return sim;
        }

        /// <summary>The Village with the mill and a bakery beside it (hired baker), built, a morning round gone by (spec 016 §3.9c).</summary>
        public static Simulation Bakery()
        {
            Simulation sim = Mill();
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "village") sim.State.Era.Current = i;
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "bakery", tile, 200_000_00, false));
            Hire(sim);
            sim.AdvanceTo((T(sim) / 86_400 + 3) * 86_400 + 12 * 3600);
            return sim;
        }

        /// <summary>The bakery's town with a cattle shed (a buffalo and a cow) and a dairy, a morning round gone by (spec 016 §3.9b).</summary>
        public static Simulation Dairy()
        {
            Simulation sim = Bakery();
            Ok(sim, new BuildShed(T(sim)));
            sim.AdvanceTo(T(sim) + 25 * Hour);
            Ok(sim, new BuyAnimal(T(sim), "buffalo"));
            Ok(sim, new BuyAnimal(T(sim), "cow"));
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "dairy", tile, 300_000_00, false));
            Hire(sim);
            sim.AdvanceTo((T(sim) / 86_400 + 3) * 86_400 + 12 * 3600);
            return sim;
        }

        /// <summary>
        /// The dairy's town with three shops (spec 017): a tea stall and a vegetable stall on the square's pitches, a sweet shop on an
        /// acre, all hired, a day of customers gone by.
        /// </summary>
        public static Simulation Shops()
        {
            Simulation sim = Dairy();
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += 500_000;
            Ok(sim, new FoundBusiness(T(sim), "tea_stall", 0, 100_000_00, false, null, 0));
            Hire(sim);
            Ok(sim, new FoundBusiness(T(sim), "veg_stall", 0, 100_000_00, false, null, 1));
            Hire(sim);
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "sweet_shop", tile, 200_000_00, false));
            Hire(sim);
            sim.AdvanceTo((T(sim) / 86_400 + 3) * 86_400 + 12 * 3600);
            return sim;
        }

        /// <summary>A Township (spec 021): the treasury open, a family in town, a school open, a season closed and a gift given.</summary>
        public static Simulation Township()
        {
            Simulation sim = Town();
            var eras = sim.Config.Game.Onboarding.Eras;
            sim.State.Era.Current = eras.ToList().FindIndex(e => e.Id == "village");
            Assert.That(Sim.Core.Progress.EraRules.Unlock(sim), Is.True);
            Ok(sim, new GiveToTreasury(T(sim), 5_000 * 100));
            long next = (T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(next + 2 * Hour);
            sim.DrainEvents();
            Assert.That(sim.State.Treasury.Reports.Count, Is.GreaterThan(0));
            return sim;
        }

        /// <summary>A Township with a flour mill and a bakery trading on terms (spec 028 §3.1, US-123): invoices standing between them and to the village shops, one gone overdue.</summary>
        public static Simulation Invoicing()
        {
            Simulation sim = Township();
            SetCash(sim, 3_000_000_00);
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "flour_mill", tile, 300_000_00, false));
            Hire(sim);
            int tile2 = sim.State.Land.Tiles.First(t => t.Id != tile && Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "bakery", tile2, 300_000_00, false));
            Hire(sim);
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("wheat")] += 20_000_000;
            sim.AdvanceTo((T(sim) / 86_400 + 3) * 86_400 + 14 * 3600);
            var open = sim.State.Township.Invoices.Open;
            Assert.That(open.Count, Is.GreaterThan(0));
            open.First(i => i.Buyer != 0).Stage = Sim.Core.Business.InvoiceStage.Overdue; // the bakery's atta, long unpaid
            return sim;
        }

        /// <summary>The first applicant for the manager's post: a townsperson at once, else the first migrant to come.</summary>
        internal static Applicant ManagerApplicant(Simulation sim)
        {
            for (int i = 0; i < 24 * 10 && sim.State.People.ApplicantsFor(HiringRules.ManagerPlace).Count == 0; i++) sim.AdvanceTo(T(sim) + Hour);
            Assert.That(sim.State.People.ApplicantsFor(HiringRules.ManagerPlace), Is.Not.Empty, "nobody applied for the manager's post");
            return sim.State.People.ApplicantsFor(HiringRules.ManagerPlace).First();
        }

        /// <summary>A Township with a bakery run by a manager (spec 028 §3.2, US-124): hired, a season closed with a report, and a mill beside it that has none.</summary>
        public static Simulation Managed()
        {
            Simulation sim = Township();
            SetCash(sim, 3_000_000_00);
            int tile = sim.State.Land.Tiles.First(t => Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "bakery", tile, 300_000_00, false));
            Hire(sim);
            long bakery = sim.State.Businesses.Businesses.Last().Id;
            int tile2 = sim.State.Land.Tiles.First(t => t.Id != tile && Sim.Core.Business.BusinessRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(T(sim), "flour_mill", tile2, 300_000_00, false));
            Hire(sim);
            Ok(sim, new PostJob(T(sim), HiringRules.ManagerPlace, false));
            Applicant a = ManagerApplicant(sim);
            Ok(sim, new HireManager(T(sim), a.Id, bakery, OfferLevel.High));
            sim.AdvanceTo((T(sim) / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason + 2 * Hour);
            Assert.That(sim.State.People.Workers.Any(w => w.IsManager && w.Mgr.ReportPeriod != 0), Is.True, "he reported at the close");
            return sim;
        }

        /// <summary>A Township whose bakery has an investor (a share he can buy back) and whose mill has offers waiting: an investor's, a partner's and a rival's (spec 028 §3.4, US-247).</summary>
        public static Simulation Owned()
        {
            Simulation sim = Managed();
            Sim.Core.Business.Business bakery = sim.State.Businesses.Businesses.First(b => Sim.Core.Business.BusinessRules.TypeOf(sim, b).Id == "bakery");
            Sim.Core.Business.Business mill = sim.State.Businesses.Businesses.First(b => Sim.Core.Business.BusinessRules.TypeOf(sim, b).Id == "flour_mill");
            Sim.Core.Business.ExitState x = sim.State.Township.Exit;
            Sim.Core.Business.DealOffer Add(Sim.Core.Business.DealKind kind, Sim.Core.Business.Business b, int pct, long coins, long who)
            {
                var o = new Sim.Core.Business.DealOffer { Id = x.NextOfferId++, Kind = kind, BusinessId = b.Id, Who = who, Share = Sim.Core.Maths.Fixed.FromRatio(pct, 100), Cash = coins * 100, RaisedAt = T(sim), ExpiresAt = T(sim) + 48 * 3600 };
                x.Offers.Add(o);
                return o;
            }
            Ok(sim, new AcceptDeal(T(sim), Add(Sim.Core.Business.DealKind.Investor, bakery, 15, 43_500, 0).Id));
            bakery.Stakes[0].Period -= 5; // five seasons ago: it can be bought back
            bakery.Stakes[0].Paid = 2_130 * 100;
            foreach (Sim.Core.People.Household h in sim.State.Citizens.Households) h.Savings = 500_000 * 100;
            Add(Sim.Core.Business.DealKind.Investor, mill, 20, 71_550, 2);
            Add(Sim.Core.Business.DealKind.Partner, mill, 20, 28_800, sim.State.Citizens.Households[0].Id);
            Add(Sim.Core.Business.DealKind.BuyOut, bakery, 104, 341_000, 1);
            return sim;
        }

        /// <summary>The Township with its works built and land in use: the clinic and quarters, the civic works, and three bought parcels leased, zoned and listed (spec 023, 024).</summary>
        /// <summary>The built Township with a card waiting for every kind of event, and some answered (US-154).</summary>
        public static Simulation Events()
        {
            Simulation sim = TownshipBuilt();
            Sim.Core.Town.EventsState st = Sim.Core.Town.EventRules.State(sim);
            Sim.Core.Definitions.EventsDef defs = sim.Config.Game.Events;
            long hh = sim.State.Citizens.Households[0].Id, hh2 = sim.State.Citizens.Households.Count > 1 ? sim.State.Citizens.Households[1].Id : hh;
            st.Waiting.Clear();
            for (int i = 0; i < defs.Events.Count; i++)
            {
                if (defs.Events[i].Disaster && i != defs.IndexOf("fire")) continue;
                Sim.Core.Town.EventRules.Raise(sim, st, i, defs.Events[i].Id == "wedding" || defs.Events[i].Id == "dispute" ? hh : 0, hh2, 0, sim.State.Clock.SimTime, defs.Events[i].Major);
            }
            st.History.Add(new Sim.Core.Town.EventRecord(defs.IndexOf("heatwave"), 1, false, sim.State.Clock.SimTime - 3 * Day, -24_000, 0));
            st.History.Add(new Sim.Core.Town.EventRecord(defs.IndexOf("fuel_spike"), 0, true, sim.State.Clock.SimTime - 9 * Day, 0, 0));
            return sim;
        }

        /// <summary>The Township with a hill parcel (rh6) bought beside the acres you own, an apple orchard bearing on one acre and a guava orchard on the other (US-147, Preview 15).</summary>
        public static Simulation Hills()
        {
            Simulation sim = TownshipBuilt();
            MapDef map = sim.Config.Game.Map;
            int parcel = map.IndexOfParcel("rh8");
            foreach (int t in map.Parcels[parcel].Tiles) // the acres beside it become yours, as a run of purchases would
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int n = map.TileAt(map.X(t) + dx, map.Y(t) + dy);
                    if (n >= 0 && map.Parcels[sim.State.Land.Tiles[n].ParcelId].Opens != "hills") sim.State.Land.Parcels[sim.State.Land.Tiles[n].ParcelId].Owned = true;
                }
            Ok(sim, new BuyParcel(T(sim), "rh8"));
            Ok(sim, new PlantOrchard(T(sim), map.Parcels[parcel].Tiles[0], "apple"));
            Ok(sim, new PlantOrchard(T(sim), map.Parcels[parcel].Tiles[1], "guava"));
            return sim;
        }

        /// <summary>The Township with crop insurance on, the property and a tea stall's policies covering, and the last claims (spec 027 §7): every insurance sheet and Why? in one place.</summary>
        public static Simulation Insured()
        {
            Simulation sim = TownshipBuilt();
            Sim.Core.Town.InsuranceState st = Sim.Core.Town.InsuranceRules.State(sim);
            string tea = Sim.Core.Business.BusinessRules.Defs(sim).Types.First(t => t.Kind == Sim.Core.Definitions.ShopKind.Chai).Id;
            Ok(sim, new FoundBusiness(T(sim), tea, 0, 60_000 * 100, true, null, 0));
            Sim.Core.Business.Business b = sim.State.Businesses.Businesses.Last();
            b.Status = Sim.Core.Business.BusinessStatus.Open;
            b.ReadyAt = T(sim);
            Ok(sim, new Sim.Core.Commands.SetCropInsurance(T(sim), true));
            st.Property.On = true;
            st.Property.PaidSeason = sim.Period;
            Sim.Core.Town.Policy bp = st.BusinessPolicyOrNew(b.Id);
            bp.On = true;
            bp.PaidSeason = sim.Period;
            int fire = sim.Config.Game.Events.IndexOf("fire") + 1, hail = sim.Config.Game.Events.IndexOf("hailstorm") + 1, strike = sim.Config.Game.Events.IndexOf("supplier_strike") + 1;
            long house = sim.State.Land.Buildings.First(x => x.Owner == 0 && x.TileId >= 0 && x.Cost > 0).Id;
            int wheat = sim.Config.Game.Farm.IndexOfCrop("wheat");
            st.Claims.Add(new Sim.Core.Town.Claim(Sim.Core.Town.Claim.CropPolicy, hail, wheat, 4_896_00, T(sim) - 3 * Day, 0, 28_800_00, 63));
            st.Claims.Add(new Sim.Core.Town.Claim(Sim.Core.Town.Claim.PropertyPolicy, fire, house, 12_600_00, T(sim) - 2 * Day, 0, 14_000_00, 90));
            st.Claims.Add(new Sim.Core.Town.Claim(Sim.Core.Town.Claim.BusinessPolicy, fire, b.Id, 16_100_00, T(sim) - Day, 2, 14_000_00, 90));
            st.Claims.Add(new Sim.Core.Town.Claim(Sim.Core.Town.Claim.BusinessPolicy, strike, b.Id, 6_000_00, T(sim) - Day / 2, 3, 35_000_00, 80));
            return sim;
        }

        /// <summary>The Township with the trader's offers (wheat, maize), a contract running (rice, 300 of 800 kg in, the price risen), one waiting and one settled (spec 027 §7): every contract sheet and Why? in one place.</summary>
        public static Simulation Contracted()
        {
            Simulation sim = TownshipBuilt();
            Sim.Core.Town.ContractsState st = Sim.Core.Town.ContractRules.State(sim);
            int next = (int)((sim.Period + 1) % 4);
            Sim.Core.Town.Contract Make(string good, long kg, long delivery)
            {
                int g = sim.Config.Game.Market.IndexOf(good);
                Sim.Core.Maths.Fixed expected = Sim.Core.Town.ContractRules.Expected(sim, g, (int)(delivery % 4));
                return new Sim.Core.Town.Contract { Id = st.NextId++, Good = good, Quantity = kg * 1000, Expected = expected, Price = Sim.Core.Town.ContractRules.OfferPrice(sim, expected), Delivery = delivery };
            }
            Sim.Core.Town.ContractsState.Add(st.HarvestLast, "wheat", 1_000_000);
            Sim.Core.Town.ContractsState.Add(st.HarvestLast, "maize", 800_000);
            st.Offers.Add(Make("wheat", 400, sim.Period + 1));
            st.Offers.Add(Make("maize", 300, sim.Period + 1));
            Sim.Core.Town.Contract run = Make("rice", 800, sim.Period);
            run.Filled = 300_000;
            run.Gain = -1_200_00;
            run.Samples = 3;
            run.FloorSum = (run.Price + Sim.Core.Maths.Fixed.FromInt(2)) * Sim.Core.Maths.Fixed.FromInt(3);
            st.Open.Add(run);
            st.Open.Add(Make("potato", 500, sim.Period + 1));
            st.History.Add(new Sim.Core.Town.ContractRecord("wheat", 400_000, 400_000, run.Price, sim.Period - 1, 0, 1_800_00));
            st.History.Add(new Sim.Core.Town.ContractRecord("mustard", 300_000, 100_000, run.Price, sim.Period - 2, 2_400_00, -1_000_00));
            return sim;
        }

        public static Simulation TownshipBuilt()
        {
            Simulation sim = Township();
            Ok(sim, new SetCashForTest(T(sim), 600_000_000_00));
            Ok(sim, new GiveToTreasury(T(sim), 5_000_000 * 100));
            typeof(Sim.Core.Town.RoadState).GetProperty("Status").GetSetMethod(true).Invoke(sim.State.Township.Road, new object[] { Sim.Core.Town.RoadStatus.Built }); // the petition needs households a Situation doesn't have
            Ok(sim, new BuildClinic(T(sim)));
            Ok(sim, new BuildDoctorQuarters(T(sim)));
            sim.AdvanceTo(T(sim) + 72 * Hour);
            for (int civic = 0; civic < 3; civic++) sim.Execute(new CommissionWorks(T(sim), Sim.Core.Town.CivicRules.Def(sim, civic).Work));
            sim.AdvanceTo(T(sim) + 72 * Hour);
            var bought = new List<string>();
            for (int pass = 0; pass < 3 && bought.Count < 3; pass++) // each purchase can make the next acre adjacent
                foreach (ParcelDef p in sim.Config.Game.Map.Parcels)
                {
                    int id = sim.Config.Game.Map.IndexOfParcel(p.Id);
                    if (bought.Count == 3 || !p.ForSale || sim.State.Land.Parcels[id].Owned || !Sim.Core.Town.ZoningRules.Serviced(sim, id) || !LandSystem.IsAdjacentToOwned(sim, id)) continue;
                    if (sim.Execute(new BuyParcel(T(sim), p.Id)).Accepted) bought.Add(p.Id);
                }
            Assert.That(bought.Count, Is.EqualTo(3));
            Ok(sim, new LeaseOutLand(T(sim), bought[0]));
            Ok(sim, new ZoneParcel(T(sim), bought[1]));
            Ok(sim, new ListParcel(T(sim), bought[2]));
            sim.AdvanceTo(T(sim) + 3 * Day);
            sim.DrainEvents();
            Assert.That(sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(bought[0])].Leased, Is.True);
            return sim;
        }

        public static IEnumerable<Simulation> All()
        {
            yield return Rich();
            yield return Growing();
            yield return Ready();
            yield return Later(out _);
            yield return Town();
            yield return Mill();
            yield return Bakery();
            yield return Dairy();
            yield return Shops();
            yield return Board();
            yield return Irrigated();
            yield return Equipped();
            yield return Township();
            yield return Invoicing();
            yield return Managed();
            yield return Owned();
            yield return TownshipBuilt();
            yield return Kiln();
            yield return Promises();
            yield return Events();
            yield return Insured();
            yield return Contracted();
        }
    }
}

namespace Sim.View.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Sim.Core;
    using Sim.Core.Definitions;
    using Sim.Core.Land;
    using Sim.View.Views;

    /// <summary>The Sprint 7 panels (ADR 0043) for the sweeping tests; each new sheet joins here.</summary>
    public static class Panels
    {
        private static KeyValuePair<string, SheetVm> N(string name, SheetVm s) { return new KeyValuePair<string, SheetVm>(name, s); }

        public static IEnumerable<KeyValuePair<string, SheetVm>> All(ViewContext ctx)
        {
            Simulation sim = ctx.Sim;
            yield return N("menu", MenuSheet.Build(ctx));
            yield return N("lands", LandsSheet.Build(ctx));
            if (Sim.Core.Town.LandValueRules.On(sim))
                foreach (Parcel p in sim.State.Land.Parcels.Where(p => p.Owned && !p.Protected))
                    yield return N("sell land " + p.Id, SellLandSheet.Build(ctx, sim.Config.Game.Map.Parcels[p.Id].Tiles[0])); // spec 024 AC-11: every land-value number has a Why?
            if (Sim.Core.Market.ColdStoreRules.On(sim)) yield return N("cold store", ColdStoreSheet.Build(ctx)); // US-242: every number has a Why?
            if (Sim.Core.Town.ConstructionRules.On(sim)) yield return N("building", BuildingSheet.Build(ctx)); // US-145: every number has a Why?
            if (sim.State.Equipment != null)
            {
                yield return N("equipment", EquipmentSheet.Build(ctx));
                for (int m = 0; m < sim.Config.Game.Equipment.Machines.Count; m++) yield return N("machine loan " + m, MachineLoanSheet.Build(ctx, m));
            }
            if (sim.State.Livestock != null && Sim.Core.Livestock.LivestockRules.Shed(sim) != null) yield return N("shed", ShedSheet.Build(ctx)); // US-254, US-255
            if (Sim.Core.Town.TownLifeRules.On(sim)) yield return N("town life", TreasurySheet.Build(ctx, TreasurySheet.LifeTab)); // US-151: every town-life number has a Why?
            if (Sim.Core.Town.FestivalRules.On(sim)) { yield return N("festival", FestivalSheet.Build(ctx, null)); yield return N("festival you", FestivalSheet.Build(ctx, "1:1:2")); } // US-149: every festival number has a Why?
            if (Sim.Core.Town.EventRules.On(sim)) // US-154: every card's numbers have a Why?
            {
                foreach (Sim.Core.Town.PendingEvent p in Sim.Core.Town.EventRules.State(sim).Waiting) yield return N("event " + sim.Config.Game.Events.Events[p.Def].Id, EventSheet.Card(ctx, p.Id));
                yield return N("events", EventSheet.List(ctx));
            }
            yield return N("money", MoneySheet.Build(ctx));
            yield return N("money wealth", MoneySheet.Build(ctx, MoneySheet.Wealth));
            yield return N("money last", MoneySheet.Build(ctx, MoneySheet.LastSeason));
            foreach (var r in sim.State.Finance.Reports)
                foreach (string tab in new[] { StatementSheet.ProfitTab, StatementSheet.CashTab, StatementSheet.BalanceTab })
                    yield return N("report " + r.Period + " " + tab, ReportSheet.Build(ctx, r.Period, tab));
            yield return N("books", BooksSheet.Build(ctx));
            foreach (var r in sim.State.Finance.Reports.Where(x => YearSheet.EndsYear(x.Period))) yield return N("year " + r.Period, YearSheet.Build(ctx, r.Period));
            for (int i = 0; i < sim.Config.Game.Credit.Lenders.Count; i++)
            {
                SheetVm amounts = BorrowSheet.Build(ctx, i, null);
                yield return N("borrow " + i, amounts);
                foreach (LineVm l in amounts.Lines) yield return N("borrow " + i + " " + l.Label, BorrowSheet.Build(ctx, i, l.Action.Opens.Value.Arg));
            }
            foreach (var loan in sim.State.Credit.Loans) if (loan.IsOpen) yield return N("repay " + loan.Id, Repay.Build(ctx, loan.Id));
            Tile unowned = sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && !sim.State.Land.Parcels[t.ParcelId].Owned);
            yield return N("land loan", LandLoanSheet.Build(ctx, unowned.Id));
            yield return N("founder", FounderSheet.Build(ctx));
            yield return N("progress", ProgressSheet.Build(ctx));
            yield return N("letter", Letter.List(ctx));
            for (int g = 0; g < sim.Config.Game.Market.Goods.Count; g++)
            {
                if (sim.State.Market.InStore(g) == 0) continue;
                yield return N("good " + g, GoodSheet.Build(ctx, g));
                SheetVm offer = GoodSheet.Offer(ctx, g, null);
                yield return N("offer " + g, offer);
                foreach (LineVm l in offer.Lines) yield return N("offer " + g + " " + l.Label, GoodSheet.Offer(ctx, g, l.Action.Opens.Value.Arg));
            }
            SheetVm inputs = BuyInputsSheet.Build(ctx);
            yield return N("inputs", inputs);
            foreach (LineVm l in inputs.Lines) yield return N("input " + l.Label, BuyInputsSheet.One(ctx, (int)l.Action.Opens.Value.Id));
            SheetVm mandi = MandiSheet.Build(ctx, null);
            yield return N("mandi", mandi);
            foreach (LineVm l in mandi.Lines.Where(x => x.Amount != null))
                yield return N("mandi with " + l.Label, MandiSheet.Build(ctx, l.Amount.Then(l.Amount.Max).Arg));
            ActionVm fill = mandi.Lines.Select(x => x.Action).FirstOrDefault(x => x != null && x.Id == "replace:fill");
            if (fill != null && fill.Opens.HasValue) yield return N("mandi filled", MandiSheet.Build(ctx, fill.Opens.Value.Arg));
        }
    }
}
