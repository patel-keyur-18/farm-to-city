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
        private static string OpenTreasuryFixture(int schema) { return Path.Combine(Dir, "schema" + schema + "-open.ftc"); }

        /// <summary>
        /// Sprint 22 item 1 (ADR 0118 §18–20): a schema-19 town whose treasury is open on the real content, with three policies enacted and a
        /// tax owed by you, frozen before schema 20 so the 19 → 20 migration is tried on a real open treasury. Written once, while the schema
        /// is still 19: <c>FTC_UPDATE_OPEN=1</c>. Never regenerate it at a later schema.
        /// </summary>
        private static void WriteOpenTreasuryFixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(19), "this fixture is the schema-19 one; it is never rewritten later");
            Simulation sim = NewFarm(game: TestContent.Game(), options: new SimOptions { DevCommandsEnabled = true });
            Gift(sim, 2_000_000 * Coin);
            TreasuryTests.Era(sim, "village");
            TreasuryTests.Family(sim);
            TreasuryTests.Township(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new EnactPolicy(t, PolicyId.Subsidy));
            Ok(sim, new EnactPolicy(t, PolicyId.MinWage));
            Ok(sim, new EnactPolicy(t, PolicyId.RentControl));
            // All but 10 coins an hour before the close, so the house tax is carried as owed (the founder's cash can't pay it).
            sim.AdvanceTo(TreasuryTests.NextClose(sim) - 3600);
            long cash = sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test",
                new Posting(Account.LivingCosts, cash - 10 * Coin), new Posting(Account.Cash, -(cash - 10 * Coin)));
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day);
            sim.DrainEvents();
            Assert.That(sim.State.Treasury.Opened, Is.True);
            Assert.That(TaxRules.YouOwe(sim), Is.GreaterThan(0), "owed tax is carried");
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(OpenTreasuryFixture(19), SaveCodec.Write(sim.State, SaveTests.Config(), Wall0));
            var pin = new Pinned { Schema = 19, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(OpenTreasuryFixture(19), ".json"),
                JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        /// <summary>Cash as a posting, not a command: <c>SetCash</c> is a test command the save's command log can't encode.</summary>
        private static void Gift(Simulation sim, long amount)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
        }

        // Sprint 22 item 1: the open schema-19 treasury loads in every later build with its cash, policies and owed tax intact, and round-trips.
        [Test]
        public void Schema19_OpenTreasury()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_OPEN") == "1") WriteOpenTreasuryFixture();
            Assert.That(File.Exists(OpenTreasuryFixture(19)), Is.True, "run once at schema 19 with FTC_UPDATE_OPEN=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(OpenTreasuryFixture(19)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            TreasuryState tr = r.State.Treasury;
            Assert.That(tr.Opened, Is.True, "the treasury is open");
            Assert.That(tr.Cash, Is.GreaterThan(0));
            Assert.That(tr.Policies.Count(p => p.Status != PolicyStatus.None), Is.EqualTo(3), "three policies enacted");
            Assert.That(tr.Reports.Count, Is.GreaterThan(0), "a closed season's statements");
            Assert.That(tr.Ledger.Reconcile(), Is.Null);
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            Assert.That(-r.State.Ledger.Balance(Account.TaxOwed), Is.GreaterThan(0), "owed tax");
            Assert.That(r.State.Township?.Road.Status, Is.EqualTo(RoadStatus.None), "schema 20 migrates it with nothing of the Township started");

            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Assert.That(again.State.Treasury.Cash, Is.EqualTo(tr.Cash));

            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * sim.Clock.SecondsPerSeason);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null, "the town carries on through two more closes");
        }
    }
}
