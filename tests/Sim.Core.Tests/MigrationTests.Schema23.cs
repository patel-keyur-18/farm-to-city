using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        private static Equipment.Machine EquipmentRules_Tempo(Simulation sim) { return Equipment.TempoRules.Of(sim); }

        private static void ToHour(Simulation sim, int hour)
        {
            long t = sim.State.Clock.SimTime / 86400 * 86400 + hour * 3600;
            if (t <= sim.State.Clock.SimTime) t += 86400;
            sim.AdvanceTo(t);
        }

        private static string Growth23Fixture { get { return Path.Combine(Dir, "schema23-growth.ftc"); } }

        private static void Pin23(string file, Simulation sim)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(file, SaveCodec.Write(sim.State, SaveTests.Config(), Wall0));
            var pin = new Pinned { Schema = 23, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(file, ".json"), JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        /// <summary>
        /// Sprint 25 (spec 028, ADR 0155): a Township with a mill and a bakery trading on terms, one pair on a season's term, the village shops invoiced, an invoice late.
        /// Written while schema 23 is the current one; grows with each Sprint 25 story and is frozen when the sprint merges: <c>FTC_UPDATE_SCHEMA23=1</c>.
        /// </summary>
        private static void WriteGrowth23Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(23), "this fixture is the schema-23 one");
            Simulation sim = InvoiceTests.MillAndBakery(out Business.Business mill, out Business.Business bakery);
            Ok(sim, new SetTerms(sim.State.Clock.SimTime, bakery.Id, 0, "village_tea", "season"));
            InvoiceTests.ToHour(sim, 14, 2);
            Worker boss = ManagerTests.Manage(sim, bakery, 1); // spec 028 §3.2 (US-124): a growth-chaser runs the bakery, one close behind him
            boss.Mgr.Target = ManagerTarget.Growth;
            sim.AdvanceTo(BusinessTests.NextClose(sim) + 2 * 3600);
            InvoiceTests.ToHour(sim, 14, 1);
            ExitTests.Seasons(bakery, 50_000, 50_000, 50_000, 50_000);
            for (long p = 9_000; bakery.Stakes.Count == 0; p++) // US-247: an investor put money into the bakery
            {
                sim.State.Township.Exit.Offers.Clear();
                OwnerRules.AtClose(sim, p);
                DealOffer o = sim.State.Township.Exit.Offers.Find(x => x.Kind == DealKind.Investor && x.BusinessId == bakery.Id);
                if (o != null) Ok(sim, new AcceptDeal(sim.State.Clock.SimTime, o.Id));
                Assert.That(p, Is.LessThan(9_400), "an investor came");
            }
            sim.State.Township.Exit.Offers.Clear();
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, bakery.Id, Benefit.Meal, true)); // US-258: a meal for the bakery's people, a night shift on the grid
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, bakery.Id, Benefit.Leave, true));
            Ok(sim, new SetBenefit(sim.State.Clock.SimTime, 0, Benefit.Meal, true));
            sim.State.Township.UtilitiesBuilt |= 1 << Definitions.UtilityDef.Grid;
            bakery.ShortDays = 0b111;
            Ok(sim, new SetNightShift(sim.State.Clock.SimTime, bakery.Id, true));
            Business.Business centre = InvoiceTests.Found(sim, "tuition", mill.TileId, bakery.TileId); // US-244: a tuition centre, a family's children enrolled, one with seasons sat
            Household kids = AddFamily(sim, 0, 36, 33, 9, 12, 15);
            kids.Savings = 1_000_000 * Coin;
            kids.Schooling = 3 * TuitionRules.FeeAt(sim, sim.Config.Game.Businesses.Types[centre.Type].Tuition, Fixed.One);
            centre.Stars = Fixed.FromInt(5);
            TuitionRules.Enrol(sim, centre);
            sim.State.Citizens.Citizen(centre.Pupils[0]).TuitionSeasons = 3;
            ToHour(sim, 15);
            ToHour(sim, 19);
            sim.State.Township.Exit.Offers.Clear();
            foreach (Household h in sim.State.Citizens.Households) { h.Income = 40_000 * Coin; h.Leisure = 5_700 * Coin; } // US-246: a restaurant on a pitch, a lunch served, its thali switched off
            Business.Business diner = RestaurantTests.Open(sim);
            ToHour(sim, 14);
            Ok(sim, new SetDish(sim.State.Clock.SimTime, diner.Id, 0, false));
            sim.State.Township.Exit.Offers.Clear();
            EquipmentTests.Give(sim, 1_000_000); // US-245: a tempo, let out, with a hand to drive it, one send of two legs
            HireHand(sim);
            Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "tempo"));
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, EquipmentRules_Tempo(sim).Id, true));
            sim.State.Market.Stock[sim.Config.Game.Market.IndexOf("potato")] += 5_000_000;
            Ok(sim, new GoToMandi(sim.State.Clock.SimTime, new[] { new Market.MandiLoad("potato", 1_500_000) }));
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, mill.Id, true)); // US-125: the mill sold in a day, the bakery listed and a buyer still to come
            Ok(sim, new ListBusiness(sim.State.Clock.SimTime, bakery.Id, false));
            sim.AdvanceTo(sim.State.Clock.SimTime + 26 * 3600);
            sim.DrainEvents();
            long at = sim.State.Clock.SimTime;
            sim.State.Township.Exit.Offers.Add(new DealOffer { Id = sim.State.Township.Exit.NextOfferId++, Kind = DealKind.BuyOut, BusinessId = bakery.Id, Who = 2, Share = Fixed.FromRatio(104, 100), Cash = 340_000 * Coin, RaisedAt = at, ExpiresAt = at + 24 * 3600 }); // and a rival's offer waiting
            Pin23(Growth23Fixture, sim);
        }

        // Sprint 25: the growth of schema 23 (invoices, the terms) loads in every later build, round-trips to the same hash and carries on through two closes.
        [Test]
        public void Schema23_Growth()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_SCHEMA23") == "1") WriteGrowth23Fixture();
            Assert.That(File.Exists(Growth23Fixture), Is.True, "run once with FTC_UPDATE_SCHEMA23=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Growth23Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(23));
            InvoicesState inv = r.State.Township.Invoices;
            Assert.That(inv.Open.Count, Is.GreaterThan(0), "invoices stand");
            Assert.That(inv.Terms.Count, Is.EqualTo(1), "a term was set");
            Assert.That(inv.Open.Any(i => i.BuyerRival == "grocer"), Is.True, "a village shop's (the mill's own were collected when it was sold)");
            Worker boss = r.State.People.Workers.Single(w => w.IsManager);
            Assert.That((boss.Mgr.Style, boss.Mgr.Target, boss.Mgr.ReportPeriod != 0), Is.EqualTo((1, ManagerTarget.Growth, true)), "his style, his target and his report");
            Assert.That(r.State.People.WorkerAt(boss.Place)?.IsManager ?? false, Is.False);
            Assert.That(r.State.Township.Exit.Sales.Count, Is.EqualTo(1), "a sale is kept");
            Assert.That(r.State.Businesses.Businesses.Count(b => b.Listed), Is.EqualTo(1), "a listing waits for its buyer");
            Business.Business owned = r.State.Businesses.Businesses.Single(b => b.Stakes.Count > 0);
            Assert.That((owned.Stakes[0].Kind, OwnerRules.Liability(owned) > 0), Is.EqualTo((StakeKind.Investor, true)), "an investor's share, owed on the books");
            Assert.That(r.State.Township.Exit.Offers.Single().Kind, Is.EqualTo(DealKind.BuyOut), "a rival's offer waits");
            Assert.That((owned.Benefits, r.State.Township.Exit.FarmBenefits), Is.EqualTo(((int)(Benefit.Meal | Benefit.Leave), (int)Benefit.Meal)), "benefits are kept");
            Assert.That(r.State.People.NightAt(owned.Id)?.Night ?? false, Is.True, "a night worker is kept");
            Business.Business school = r.State.Businesses.Businesses.Single(b => config.Game.Businesses.Types[b.Type].IsTuition);
            Assert.That((school.Pupils.Count > 0, school.TuitionFees > 0, r.State.Citizens.Citizens.Max(c => c.TuitionSeasons) >= 3), Is.EqualTo((true, true, true)), "a tuition centre's pupils, its fees and a child's seasons are kept");
            Business.Business diner = r.State.Businesses.Businesses.Single(b => config.Game.Businesses.Types[b.Type].IsMeals);
            Assert.That((diner.DishesOff, diner.SeasonServed > 0 || diner.ServedToday > 0), Is.EqualTo((1, true)), "a restaurant's menu switch and its diners are kept");
            Equipment.Machine tempo = r.State.Equipment.Machines.Single(x => config.Game.Equipment.Machines[x.Kind].IsTempo);
            Assert.That((tempo.OwnLegs, tempo.HireOut), Is.EqualTo((2, true)), "a tempo's legs and its let-out switch are kept");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in r.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // An older save loads with every internal sale cash and no invoices, so a town is unchanged until it trades on terms.
        [Test]
        public void Schema22_Loads_Without_Invoices()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Path.Combine(Dir, "schema22-townlife.ftc")), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Township.Invoices.IsDefault, Is.True);
        }
    }
}
