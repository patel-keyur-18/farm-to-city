using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    /// <summary>
    /// Save migrations from the release candidate on (spec 007 AC-14, ADR 0047 §3). Every frozen save in <c>saves/</c> must
    /// load in every later build: while its schema is current its hash is the pinned one; after a migration its plain facts
    /// are unchanged. <c>FTC_UPDATE_SAVES=1</c> writes the bot fixture of the current schema and pins the facts of every
    /// current-schema fixture (a device town dropped in as <c>schemaN-device.ftc</c> included). Never re-pin an older schema.
    /// </summary>
    public class MigrationTests
    {
        private static bool Update { get { return Environment.GetEnvironmentVariable("FTC_UPDATE_SAVES") == "1"; } }

        private static string Dir { get { return TestContent.TestFile("saves"); } }

        private static int MinSchema { get { return ContentLoader.LoadSaves(TestContent.ReadContent("saves.json")).Save.MinSchema; } }

        private static string BotFixture(int schema) { return Path.Combine(Dir, "schema" + schema + ".ftc"); }

        [Test]
        public void MinSchemaFrozen()
        {
            Assert.That(MinSchema, Is.EqualTo(3),
                "refusing old saves again needs a new ADR (ADR 0047 §3)");
        }

        [Test]
        public void SchemaHasFixture()
        {
            if (Update) WriteBotFixture();
            Assert.That(File.Exists(BotFixture(SaveCodec.SchemaVersion)), Is.True,
                "schema " + SaveCodec.SchemaVersion + " needs a frozen fixture and a migration step: run with FTC_UPDATE_SAVES=1");
        }

        [Test]
        public void EverySchemaLoads()
        {
            if (Update) WriteBotFixture();
            SimConfig config = SaveTests.Config();
            string[] files = Directory.GetFiles(Dir, "*.ftc").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            Assert.That(files, Is.Not.Empty);
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                byte[] bytes = File.ReadAllBytes(file);
                LoadResult r = SaveCodec.Read(bytes, config, MinSchema);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), name + ": " + r.Detail);
                string pinPath = Path.ChangeExtension(file, ".json");
                if (Update && r.Header.Schema == SaveCodec.SchemaVersion) Pin(pinPath, r.State);
                Assert.That(File.Exists(pinPath), Is.True, name + " has its pinned facts");
                Pinned pin = JsonSerializer.Deserialize<Pinned>(File.ReadAllText(pinPath));

                Assert.That(pin.Schema, Is.EqualTo(r.Header.Schema), name);
                if (r.Header.Schema == SaveCodec.SchemaVersion)
                    Assert.That("0x" + r.State.ComputeHash().ToString("X16"), Is.EqualTo(pin.Hash), name + ": the current schema loads exactly");
                // After a migration the hash changes with the new fields; the old facts may not. Each migration step adds its
                // own asserts here that the new fields hold their documented defaults.
                Assert.That(Facts(r.State), Is.EqualTo(pin.Facts), name + ": plain facts");
                if (r.Header.Schema < 4)
                {
                    // ADR 0048 §4: a neighbour hired today stays to the daily pass they were paid to, on no field.
                    long pass = People.PeopleRules.NextDailyPass(r.State.Clock.SimTime);
                    Assert.That(r.State.People.Roster.All(n => n.Tile == -1 && (n.Until == 0 || n.Until == pass)), Is.True, name + ": labourers migrated");
                }

                if (r.Header.Schema < 6)
                {
                    // Spec 011 §4 (AC-14): the truck here by the save's hour, its load the trader's counts, no daily move, nothing held.
                    Market.MarketState m = r.State.Market;
                    int hour = (int)(r.State.Clock.SimTime % TestSim.Day / TestSim.Hour);
                    Assert.That(m.TruckHere, Is.EqualTo(hour >= 6), name + ": the truck by the hour");
                    Assert.That(m.TruckLoadToday, Is.EqualTo(m.TraderBoughtToday.Sum()), name + ": the load");
                    for (int g = 0; g < m.Stock.Length; g++)
                    {
                        Assert.That(m.DailyFactorOf(g), Is.EqualTo(Maths.Fixed.One), name + ": factor 1");
                        Assert.That(m.HeldForTruckOf(g), Is.EqualTo(0), name + ": nothing held");
                    }
                }

                Simulation sim = Simulation.FromState(config, r.State);
                sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
                sim.DrainEvents();
                LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
                Assert.That(again.Error, Is.EqualTo(SaveError.None), name + " after a day: " + again.Detail);
                Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), name + ": a day later it round-trips");
            }
        }

        // Spec 011 AC-14: every older fixture loads at schema 6 with the truck by its hour, its load the trader's counts, no
        // orders, nothing fresh, nothing owed; it advances a day and round-trips.
        [Test]
        public void Schema5_TruckAndOrders()
        {
            SimConfig config = SaveTests.Config();
            string[] older = Directory.GetFiles(Dir, "*.ftc", SearchOption.AllDirectories).Where(f => SaveCodec.Read(File.ReadAllBytes(f), config, MinSchema).Header.Schema < 6).ToArray();
            Assert.That(older.Length, Is.GreaterThanOrEqualTo(3), "schema 3, 4 and 5 fixtures");
            foreach (string file in older)
            {
                string name = Path.GetFileName(file);
                LoadResult r = SaveCodec.Read(File.ReadAllBytes(file), config, MinSchema);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), name + ": " + r.Detail);
                Market.MarketState m = r.State.Market;
                Assert.That(m.TruckHere, Is.EqualTo(r.State.Clock.SimTime % TestSim.Day >= 6 * TestSim.Hour), name);
                Assert.That(m.TruckLoadToday, Is.EqualTo(m.TraderBoughtToday.Sum()), name);
                Assert.That(m.SupermarketOrders, Is.Empty, name);
                Assert.That(m.FreshToday.All(q => q == 0), Is.True, name);
                Assert.That(r.State.Ledger.Balance(Finance.Account.OwedByBuyers), Is.EqualTo(0), name);
                Simulation sim = Simulation.FromState(config, r.State);
                sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
                LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
                Assert.That(again.Error, Is.EqualTo(SaveError.None), name);
                Assert.That(again.Header.Schema, Is.EqualTo(6));
                Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), name + " round-trips");
            }
        }

        /// <summary>
        /// The bot fixture: the competent bot's town after 2 seasons (a hand and hut, orders, trees, the garden, the era's
        /// progress), with a seed that took a loan, and the letter's first steps seen and done.
        /// </summary>
        private static void WriteBotFixture()
        {
            SimConfig config = SaveTests.Config();
            Simulation sim = null;
            for (ulong seed = 1; seed < 100; seed++)
            {
                sim = SaveTests.Town(seed, 2);
                if (sim.State.Credit.Loans.Count > 0 && sim.State.People.Hands.Count > 0 && sim.State.People.Orders.Count > 0) break;
            }
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new SeeChapter(t, "welcome")).Accepted, Is.True);
            // The bots play after the first session (they skip it, spec 011 §10 G6), so its first step is done only before that.
            if (!sim.State.Guidance.Skipped) Assert.That(sim.Execute(new CompleteStep(t, "welcome")).Accepted, Is.True);
            sim.DrainEvents();
            Assert.That(sim.State.Credit.Loans.Count, Is.GreaterThan(0), "a loan");
            Assert.That(sim.State.People.Hands.Count, Is.GreaterThan(0), "a hand");
            Assert.That(sim.State.Farm.Trees.Count, Is.GreaterThan(0), "trees");
            Assert.That(sim.State.Era.SeasonPeriod, Is.GreaterThanOrEqualTo(0), "the era's progress");
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(BotFixture(SaveCodec.SchemaVersion), SaveCodec.Write(sim.State, config, TestSim.Wall0));
        }

        private static void Pin(string path, GameState s)
        {
            var pin = new Pinned { Schema = SaveCodec.SchemaVersion, Hash = "0x" + s.ComputeHash().ToString("X16"), Facts = Facts(s) };
            File.WriteAllText(path, JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        /// <summary>What a player would notice if a migration lost it. Keep this list stable: add, never change.</summary>
        internal static SortedDictionary<string, long> Facts(GameState s)
        {
            return new SortedDictionary<string, long>(StringComparer.Ordinal)
            {
                ["simTime"] = s.Clock.SimTime,
                ["cash"] = s.Ledger.Cash,
                ["parcelsOwned"] = s.Land.Parcels.Count(p => p.Owned),
                ["buildings"] = s.Land.Buildings.Count,
                ["crops"] = s.Farm.Crops.Count,
                ["gardenBedsSown"] = s.Farm.Beds.Count(b => b.Crop >= 0),
                ["trees"] = s.Farm.Trees.Count,
                ["storeTotal"] = s.Market.Stock.Sum(),
                ["roster"] = s.People.Roster.Count,
                ["hands"] = s.People.Hands.Count,
                ["orders"] = s.People.Orders.Count,
                ["loansOpen"] = s.Credit.Loans.Count(l => l.IsOpen),
                ["seasonReports"] = s.Finance.Reports.Count,
                ["guidanceStep"] = s.Guidance.Step,
                ["chaptersSeen"] = s.Guidance.Seen.Count,
                ["era"] = s.Era.Current,
                ["profitableSeasons"] = s.Era.ProfitableSeasons,
            };
        }

        public sealed class Pinned
        {
            public int Schema { get; set; }
            public string Hash { get; set; }
            public SortedDictionary<string, long> Facts { get; set; }
        }
    }
}
