using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        private static string TownLife22Fixture { get { return Path.Combine(Dir, "schema22-townlife.ftc"); } }

        private static string Risk22Fixture { get { return Path.Combine(Dir, "schema22-risk.ftc"); } }

        private static void Pin22(string file, Simulation sim)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(file, SaveCodec.Write(sim.State, SaveTests.Config(), Wall0));
            var pin = new Pinned { Schema = 22, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(file, ".json"), JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        /// <summary>Sprint 24 (spec 026, ADR 0127): a Township a few closes on with its town read, a petition standing and a festival held and over. Written while schema 22 is the current one: <c>FTC_UPDATE_SCHEMA22=1</c>.</summary>
        private static void WriteTownLife22Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(22), "this fixture is the schema-22 one");
            Simulation sim = TownLifeTests.Township(families: 12);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Finance.Posting(Finance.Account.Cash, 5_000_000 * Coin), new Finance.Posting(Finance.Account.GiftsFromFounder, -5_000_000 * Coin));
            sim.DrainEvents();
            Ok(sim, new ScheduleFestival(sim.State.Clock.SimTime, FestivalDef.Square, FestivalDef.Town, 1));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * sim.Clock.SecondsPerSeason);
            sim.DrainEvents();
            Pin22(TownLife22Fixture, sim);
        }

        /// <summary>Sprint 24 (spec 027, ADR 0141 to 0150): a Township with every policy on, an offer accepted, a card waiting and a season gone by. Written with <c>FTC_UPDATE_SCHEMA22=1</c>.</summary>
        private static void WriteRisk22Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(22), "this fixture is the schema-22 one");
            Simulation sim = ForwardKit.WithOffers();
            InsuranceKit.SetCash(sim, 5_000_000 * Coin);
            Ok(sim, new SetCropInsurance(InsuranceKit.Now(sim), true));
            InsuranceKit.SowWheat(sim);
            Business.Business shop = InsuranceKit.Shop(sim, 8_000);
            Ok(sim, new SetPropertyInsurance(InsuranceKit.Now(sim), true));
            Ok(sim, new SetBusinessInsurance(InsuranceKit.Now(sim), shop.Id, true));
            ForwardKit.Accept(sim, "wheat");
            EventEngineTests.Force(sim, "drought");
            InsuranceKit.Close(sim);
            sim.DrainEvents();
            Pin22(Risk22Fixture, sim);
        }

        // Sprint 24: the town life of schema 22 (reputation, crime, petitions, a festival) loads in every later build, round-trips to the same hash and carries on through two closes.
        [Test]
        public void Schema22_TownLife()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_SCHEMA22") == "1") WriteTownLife22Fixture();
            Assert.That(File.Exists(TownLife22Fixture), Is.True, "run once with FTC_UPDATE_SCHEMA22=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(TownLife22Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(22));
            TownLifeState life = r.State.Township.TownLife;
            Assert.That(life.Read, Is.True, "the town was read at a close");
            Assert.That(life.Closes, Is.GreaterThan(0));
            Assert.That(life.Festivals.Count, Is.GreaterThan(0), "the festival is on the record");
            Assert.That(life.Reputation.Any(x => x.Raw != 0), Is.True, "five scores");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            Assert.That(r.State.Treasury.Ledger.Reconcile(), Is.Null);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
        }

        // Sprint 24: the risk of schema 22 (policies, a contract, a waiting card, the books) loads, round-trips and carries on.
        [Test]
        public void Schema22_Risk()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_SCHEMA22") == "1") WriteRisk22Fixture();
            Assert.That(File.Exists(Risk22Fixture), Is.True, "run once with FTC_UPDATE_SCHEMA22=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Risk22Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(22));
            Assert.That(r.State.Township.Insurance.CropOn, Is.True, "crop insurance on");
            Assert.That(r.State.Township.Insurance.Property.On, Is.True, "property insurance on");
            Assert.That(r.State.Township.Insurance.Businesses.Any(p => p.On), Is.True, "a business policy on");
            Assert.That(r.State.Township.Contracts.Open.Count + r.State.Township.Contracts.History.Count, Is.GreaterThan(0), "the contract taken");
            Assert.That(r.State.Township.Events.Waiting.Count + r.State.Township.Events.History.Count, Is.GreaterThan(0), "the engine has had a card");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
        }
    }
}
