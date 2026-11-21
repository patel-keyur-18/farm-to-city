using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        private static string Ring21Fixture { get { return Path.Combine(Dir, "schema21-ring.ftc"); } }

        /// <summary>
        /// Sprint 23 (spec 025 §3.4, ADR 0125): a schema-21 Township with the ring open, three lane sections built (two west, one east) and its
        /// treasury paying for them. Written while schema 21 is the current one: <c>FTC_UPDATE_RING=1</c>. Once schema 22 exists it is frozen.
        /// </summary>
        private static void WriteRing21Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(21), "this fixture is the schema-21 one");
            Simulation sim = TownshipTests.Start(cohort: true);
            TreasuryTests.Era(sim, "village");
            TreasuryTests.Township(sim);
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 600_000 * Coin));
            foreach (string lane in new[] { "lane_w1", "lane_w2", "lane_e1" }) Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, lane));
            foreach (string work in new[] { "diesel_generator", "water_tower" }) Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, work)); // US-144: the generator fitted, the tower under way
            // Every other part of Sprint 23 in the same town (the independent review, 2026-11-20): the kiln with its yard, the cold store, a shed with a
            // heifer, the silage pit and the chaff cutter, a hired-out tractor, a pump with its solar panel. Each is read behind its own schema gate.
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Finance.Posting(Finance.Account.Cash, 5_000_000 * Coin), new Finance.Posting(Finance.Account.OpeningEquity, -5_000_000 * Coin)); // posted straight to the books: a test's cash is no command in the save
            int kilnTile = sim.State.Land.Tiles.First(t => Business.BusinessRules.Place(sim, t.Id) == RejectReason.None && People.HouseRules.Place(sim, t.Id) == RejectReason.None).Id;
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "brick_kiln", kilnTile, 400_000 * Coin, false));
            Business.Business kiln = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, kiln);
            Ok(sim, new RentColdStore(sim.State.Clock.SimTime, 3));
            Ok(sim, new BuildShed(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 25 * Hour);
            Ok(sim, new SetKeepHeifers(sim.State.Clock.SimTime, true));
            Ok(sim, new BuildSilagePit(sim.State.Clock.SimTime));
            Ok(sim, new BuyChaffCutter(sim.State.Clock.SimTime));
            HireHand(sim);
            Ok(sim, new BuyMachine(sim.State.Clock.SimTime, "tractor"));
            Ok(sim, new SetHireOut(sim.State.Clock.SimTime, Equipment.EquipmentRules.OfKind(sim, sim.Config.Game.Equipment.IndexOf("tractor")).Id, true));
            Ok(sim, new InstallPump(sim.State.Clock.SimTime));
            Ok(sim, new FitSolarPanel(sim.State.Clock.SimTime, 1));
            sim.AdvanceTo(Math.Max(kiln.ReadyAt, TreasuryTests.NextClose(sim)) + 3 * Hour);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Day);
            sim.DrainEvents();
            Assert.That(sim.State.Township.LanesBuilt, Is.EqualTo(0b10011));
            Assert.That(sim.State.Township.UtilitiesBuilt & 0b10, Is.EqualTo(0b10), "the generator stands");
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Ring21Fixture, SaveCodec.Write(sim.State, SaveTests.Config(), Wall0));
            var pin = new Pinned { Schema = 21, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(Ring21Fixture, ".json"), JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        // Sprint 23: the open ring with its built lanes loads in every later build, round-trips to the same hash and carries on through two closes.
        [Test]
        public void Schema21_Ring()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_RING") == "1") WriteRing21Fixture();
            Assert.That(File.Exists(Ring21Fixture), Is.True, "run once with FTC_UPDATE_RING=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Ring21Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(21));
            Assert.That(r.State.Land.Tiles.Count, Is.EqualTo(432));
            Assert.That(r.State.Township.LanesBuilt, Is.EqualTo(0b10011), "two west sections and the first east one");
            Assert.That(r.State.Township.LandValues.Length, Is.EqualTo(432), "the ring is read too");
            Assert.That(r.State.Township.UtilitiesBuilt & 0b10, Is.EqualTo(0b10), "the generator (US-144) stands");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            // Sprint 24 (spec 027 AC-15, spec 026 AC-12): a town saved before schema 22 loads with the engine idle, no policy, no offers and no town life read.
            Assert.That((r.State.Township.Events.StartedAt, r.State.Township.Events.Waiting.Count, r.State.Township.Events.IsDefault), Is.EqualTo((-1L, 0, true)), "the event engine is idle");
            Assert.That((r.State.Township.Insurance.CropOn, r.State.Township.Insurance.Property.On, r.State.Township.Insurance.IsDefault), Is.EqualTo((false, false, true)), "no policy");
            Assert.That((r.State.Township.Contracts.Offers.Count, r.State.Township.Contracts.Open.Count), Is.EqualTo((0, 0)), "no offers");
            Assert.That((r.State.Township.TownLife.Read, r.State.Township.TownLife.Petitions.Count), Is.EqualTo((false, 0)), "no town life read yet");
            Assert.That(r.State.Farm.Crops.All(c => c.EventHealth == Sim.Core.Maths.Fixed.One && !c.Insured), Is.True, "every crop whole and uninsured");
            // the other parts of the sprint (US-243, US-242, US-254, US-255, US-256, US-257)
            Business.Business kiln = r.State.Businesses.Businesses.Single(x => config.Game.Businesses.Types[x.Type].IsKiln);
            Assert.That(kiln.KilnMade, Is.GreaterThanOrEqualTo(0), "the kiln and its counts");
            Assert.That(r.State.Godown.ColdRentedTonnes + r.State.Godown.ColdRentNextTonnes, Is.GreaterThan(0), "the cold store's rented tonnes");
            Assert.That(r.State.Livestock.KeepHeifers, Is.True, "the heifer switch");
            Assert.That((r.State.Livestock.SilagePit, r.State.Livestock.ChaffCutter), Is.EqualTo((true, true)), "the silage pit and the chaff cutter");
            Assert.That(r.State.Equipment.Machines.Any(m => m.HireOut), Is.True, "the tractor let out");
            Assert.That(r.State.Irrigation.PumpSolar, Is.GreaterThan(0), "the pump's solar panel");
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            MapDef map = config.Game.Map;
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rw1a")), Is.False, "the ring is open");
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rh1")), Is.False, "the hills open with the ring (US-147)");
            Assert.That(LaneRules.Built(sim, map.TileAt(2, 6)), Is.True);
            Assert.That(LaneRules.Built(sim, map.TileAt(2, 12)), Is.False);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
        }
    }
}
