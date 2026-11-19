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
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 3 * Hour);
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
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            MapDef map = config.Game.Map;
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rw1a")), Is.False, "the ring is open");
            Assert.That(LandRules.Locked(sim, map.IndexOfParcel("rh1")), Is.True, "the hills are not");
            Assert.That(LaneRules.Built(sim, map.TileAt(2, 6)), Is.True);
            Assert.That(LaneRules.Built(sim, map.TileAt(2, 12)), Is.False);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
        }
    }
}
