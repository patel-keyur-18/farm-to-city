using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        private static string Township20Fixture { get { return Path.Combine(Dir, "schema20-township.ftc"); } }

        /// <summary>
        /// Sprint 22 (spec 023): a schema-20 Township with everything of it: the paved road, the clinic and quarters with the doctor in post, the
        /// gate's goals met, the old village joined, a policy and tax owed by you. Written while schema 20 is the current one:
        /// <c>FTC_UPDATE_TOWNSHIP=1</c>. Once schema 21 exists it is frozen.
        /// </summary>
        private static void WriteTownship20Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(20), "this fixture is the schema-20 one");
            TestContent.WithCohort = true;
            Simulation sim;
            try { sim = NewFarm(game: TestContent.Game(), options: new SimOptions { DevCommandsEnabled = true }); }
            finally { TestContent.WithCohort = false; }
            Gift(sim, 5_000_000 * Coin);
            TreasuryTests.Era(sim, "village");
            TownshipTests.Families(sim, 20);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildClinic(t));
            Ok(sim, new BuildDoctorQuarters(t));
            Ok(sim, new PetitionRoad(t));
            sim.AdvanceTo(t + 4 * Day);
            TreasuryTests.Township(sim);
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
            string bought = sim.Config.Game.Map.Parcels.First(p => p.ForSale && !sim.State.Land.Parcels[sim.Config.Game.Map.IndexOfParcel(p.Id)].Owned && Sim.Core.Land.LandSystem.IsAdjacentToOwned(sim, sim.Config.Game.Map.IndexOfParcel(p.Id))).Id;
            Ok(sim, new BuyParcel(sim.State.Clock.SimTime, bought)); // a parcel listed for a slow sale, with the land values read (US-143)
            sim.AdvanceTo(TreasuryTests.NextClose(sim) - 3600);
            long cash = sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.LivingCosts, cash - 10 * Coin), new Posting(Account.Cash, -(cash - 10 * Coin)));
            Ok(sim, new ListParcel(sim.State.Clock.SimTime, bought));
            int idle = sim.Config.Game.Start.InheritedParcels.First(p => Sim.Core.Town.LandLeaseRules.CanLease(sim, p) == RejectReason.None);
            Ok(sim, new LeaseOutLand(sim.State.Clock.SimTime, sim.Config.Game.Map.Parcels[idle].Id)); // an inherited acre leased out: running by the close (US-261)
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 3 * Hour);
            sim.DrainEvents();
            Assert.That(CohortRules.Joined(sim), Is.True);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            Assert.That(RoadRules.IsBuilt(sim), Is.True);
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Township20Fixture, SaveCodec.Write(sim.State, SaveTests.Config(), Wall0));
            var pin = new Pinned { Schema = 20, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(Township20Fixture, ".json"), JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        // Sprint 22: the whole Township loads in every later build, round-trips to the same hash and carries on through two closes.
        [Test]
        public void Schema20_Township()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_TOWNSHIP") == "1") WriteTownship20Fixture();
            Assert.That(File.Exists(Township20Fixture), Is.True, "run once with FTC_UPDATE_TOWNSHIP=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Township20Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            TownshipState t = r.State.Township;
            Assert.That(t.Road.Status, Is.EqualTo(RoadStatus.Built));
            Assert.That(t.Clinic.Status, Is.EqualTo(ClinicStatus.Open));
            Assert.That(t.Clinic.Quarters, Is.EqualTo(QuartersStatus.Built));
            Assert.That(t.Clinic.Doctor, Is.EqualTo(DoctorStatus.InPost));
            Assert.That(t.GateMet & (TownshipGoal.Clinic | TownshipGoal.Road | TownshipGoal.Households | TownshipGoal.Literacy), Is.EqualTo(TownshipGoal.Clinic | TownshipGoal.Road | TownshipGoal.Households | TownshipGoal.Literacy));
            Assert.That((t.Cohort.Joined, t.Cohort.Households), Is.EqualTo((true, 30)));
            Assert.That(r.State.Treasury.Opened, Is.True);
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            Assert.That(t.LandValues.Length, Is.EqualTo(192), "land values read, on the 16 × 12 map: the next close reads the ring too (schema 21)");
            Assert.That(r.State.Land.Parcels.Count(p => p.Listed), Is.EqualTo(1), "a parcel listed for a slow sale");
            Assert.That(r.State.Land.Parcels.Count(p => p.Lease == (int)Sim.Core.Land.LeaseStatus.Active), Is.EqualTo(1), "a parcel leased out and paying");
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Assert.That(again.State.Township.Cohort.Happiness, Is.EqualTo(t.Cohort.Happiness));
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
            Assert.That(sim.State.Township.Cohort.Households, Is.GreaterThan(0));
        }
    }
}
