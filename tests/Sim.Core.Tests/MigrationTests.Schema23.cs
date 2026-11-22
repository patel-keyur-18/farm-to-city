using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
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
            sim.DrainEvents();
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
            Assert.That(inv.Open.Any(i => i.BuyerRival == "grocer") && inv.Open.Any(i => i.Buyer != 0), Is.True, "a village shop's and a business's");
            Worker boss = r.State.People.Workers.Single(w => w.IsManager);
            Assert.That((boss.Mgr.Style, boss.Mgr.Target, boss.Mgr.ReportPeriod != 0), Is.EqualTo((1, ManagerTarget.Growth, true)), "his style, his target and his report");
            Assert.That(r.State.People.WorkerAt(boss.Place)?.IsManager ?? false, Is.False);
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
