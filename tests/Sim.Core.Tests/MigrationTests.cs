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
                // Through the save points too, which report the old schema so the app keeps the town as it was (owner, Sprint 12).
                var store = new MemoryStore();
                store.Files[SavePoints.Latest] = bytes;
                LoadOutcome o = new SavePoints(store, Sim.Content.ContentLoader.LoadSaves(TestContent.ReadContent("saves.json")).Save).LoadNewestValid(config);
                Assert.That((o.Error, o.Schema), Is.EqualTo((SaveError.None, r.Header.Schema)), name + " through the save points");
                string pinPath = Path.ChangeExtension(file, ".json");
                if (Update && r.Header.Schema == SaveCodec.SchemaVersion) Pin(pinPath, r.State);
                Assert.That(File.Exists(pinPath), Is.True, name + " has its pinned facts");
                Pinned pin = JsonSerializer.Deserialize<Pinned>(File.ReadAllText(pinPath));

                Assert.That(pin.Schema, Is.EqualTo(r.Header.Schema), name);
                if (r.Header.Schema == SaveCodec.SchemaVersion)
                    Assert.That("0x" + r.State.ComputeHash().ToString("X16"), Is.EqualTo(pin.Hash), name + ": the current schema loads exactly");
                // After a migration the hash changes with the new fields; the old facts may not. Each migration step adds its
                // own asserts here that the new fields hold their documented defaults.
                SortedDictionary<string, long> facts = Facts(r.State);
                if (r.Header.Schema < 8)
                {
                    // ADR 0064: the cattle acre is inherited, owned and protected, barren, with nothing on it; it is the one new parcel.
                    int home = config.Game.Map.IndexOfParcel("home_d");
                    Assert.That((r.State.Land.Parcels[home].Owned, r.State.Land.Parcels[home].Protected), Is.EqualTo((true, true)), name);
                    Assert.That(r.State.Land.Tiles[104].Terrain, Is.EqualTo(Definitions.Terrain.Cattle), name);
                    Assert.That(r.State.Farm.CropOn(104), Is.Null, name);
                    Assert.That(r.State.Land.Slots.Any(s => s.TileId == 104), Is.False, name);
                    Assert.That(r.State.Ledger.Balance(Finance.Account.Land), Is.EqualTo(r.State.Land.Parcels.Where(x => x.Owned).Sum(x => x.CostBasis)), name + ": land at cost");
                    facts["parcelsOwned"] -= 1;
                }
                Assert.That(facts, Is.EqualTo(pin.Facts), name + ": plain facts");
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

                if (r.Header.Schema < 9)
                {
                    // Spec 014 §3.11 (AC-9): each hand a citizen, 22–45, in a household of one in his hut; nobody else.
                    People.CitizensState c = r.State.Citizens;
                    Simulation at = Simulation.FromState(config, r.State.Clone());
                    Assert.That(c.Population, Is.EqualTo(r.State.People.Hands.Count), name);
                    foreach (People.Hand h in r.State.People.Hands)
                    {
                        People.Citizen ci = c.OfHand(h.Id);
                        Assert.That(ci.Female, Is.EqualTo(config.Game.People.Names[h.Name].Female), name);
                        Assert.That(People.CitizenRules.Age(at, ci), Is.InRange(22, 45), name);
                        Assert.That(c.Household(ci.HouseholdId).Members, Is.EqualTo(new[] { ci.Id }), name);
                        Assert.That(c.Household(ci.HouseholdId).HomeId, Is.EqualTo(h.HutId), name);
                    }
                    Assert.That(r.State.CommandLog.Base, Is.Not.Null, name + ": the migrated town is its own replay base");
                }

                if (r.Header.Schema < 10)
                {
                    // Spec 014 §3.6 (schema 10): no hand's wage has housing in kind yet; the town is its own replay base (families move in now).
                    Assert.That(r.State.People.Hands.All(h => h.HousingInKind == 0), Is.True, name);
                    Assert.That(r.State.Businesses.Businesses, Is.Empty, name + ": no businesses yet (spec 016 AC-10)");
                    Assert.That(r.State.Citizens.Citizens.All(c => c.Employer == 0), Is.True, name);
                    if (r.State.Citizens != null) Assert.That(r.State.CommandLog.Base, Is.Not.Null, name);
                }

                if (r.Header.Schema < 11)
                {
                    // Spec 016 §3.9c (schema 11): no business has a day's mix or anything made today yet.
                    Assert.That(r.State.Businesses.Businesses.All(x => x.Mix.All(v => v == 0) && x.MadeToday.All(v => v == 0)), Is.True, name);
                }

                if (r.Header.Schema < 7)
                {
                    // Spec 012 §4 (AC-16): no shed, no animals, buying green fodder on, no livestock money.
                    Livestock.LivestockState l = r.State.Livestock;
                    Assert.That(l.Animals, Is.Empty, name);
                    Assert.That(l.ShedReadyAt, Is.EqualTo(0), name);
                    Assert.That(l.BuyGreenFodder, Is.True, name);
                    Assert.That(r.State.Ledger.Balance(Finance.Account.Livestock), Is.EqualTo(0), name);
                }

                Simulation sim = Simulation.FromState(config, r.State);
                sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
                sim.DrainEvents();
                LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
                Assert.That(again.Error, Is.EqualTo(SaveError.None), name + " after a day: " + again.Detail);
                Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), name + ": a day later it round-trips");
            }
        }

        // Spec 016 AC-10: a schema-9 save loads into schema 10 with no businesses, founds one, and round-trips.
        [Test]
        public void Schema9_Businesses()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(9)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Businesses.Businesses, Is.Empty);
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // Spec 016 §3.9c (ADR 0076): a mill saved at schema 10 loads into 11 with its stock and no mix, works on and round-trips.
        [Test]
        public void Schema10_Mix()
        {
            SimConfig config = SaveTests.Config();
            Simulation s = CreditKit.NewSim(cash: 500_000);
            BusinessTests.Hamlet(s);
            Commands.CommandResult found = s.Execute(new Commands.FoundBusiness(s.State.Clock.SimTime, "flour_mill", PeopleKit.HomeTile, 150_000_00, false));
            Assert.That(found.Accepted, Is.True, found.ToString());
            s.AdvanceTo(s.State.Clock.SimTime + 2 * TestSim.Day);
            LoadResult r = SaveCodec.Read(SaveCodec.Write(s.State, config, TestSim.Wall0, 0, SaveCodec.DefaultMaxLogEntries, 10), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Business.Business b = r.State.Businesses.Businesses.Single();
            Assert.That(b.Stock, Is.EqualTo(s.State.Businesses.Businesses[0].Stock));
            Assert.That(b.Mix.All(v => v == 0), Is.True);
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // Spec 014 AC-9: the schema-8 fixture's hands become citizens; the town advances a season (ageing, closes) and round-trips.
        [Test]
        public void Schema8_Citizens()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(8)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.People.Hands, Is.Not.Empty, "the fixture has a hand");
            Assert.That(r.State.Citizens.Population, Is.EqualTo(r.State.People.Hands.Count));
            Simulation sim = Simulation.FromState(config, r.State);
            People.Citizen c = sim.State.Citizens.Citizens[0];
            int age = People.CitizenRules.Age(sim, c);
            sim.AdvanceTo(r.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            if (sim.State.Citizens.Citizen(c.Id) != null) Assert.That(People.CitizenRules.Age(sim, c), Is.EqualTo(age + 1), "a year a season");
            byte[] bytes = SaveCodec.Write(sim.State, config, TestSim.Wall0);
            LoadResult again = SaveCodec.Read(bytes, config, MinSchema);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(SaveCodec.Replay(again, config).State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "the history replays");
        }

        // ADR 0064: a schema-7 town that bought e1 and sowed tile 104 gets the cattle acre: the crop is paid for at the trader's
        // floor and cleared, e1 keeps its other acres, the land stays at cost, and the town round-trips.
        [Test]
        public void Schema7_CattleAcre()
        {
            SimConfig config = SaveTests.Config();
            Definitions.GameDefs defs = config.Game;
            Simulation sim = Simulation.NewGame(config, new NewGame(42, TestSim.Wall0, 0));
            int e1 = defs.Map.IndexOfParcel("e1"), home = defs.Map.IndexOfParcel("home_d");
            // Undo the cattle acre to make the town as schema 7 left it: tile 104 a farm acre of e1, home_d not inherited.
            Land.Parcel h = sim.State.Land.Parcels[home];
            sim.State.Ledger.Post(0, sim.Period, "ledger.opening", "test", new Finance.Posting(Finance.Account.Land, -h.CostBasis), new Finance.Posting(Finance.Account.OpeningEquity, h.CostBasis));
            h.Owned = false; h.Protected = false; h.CostBasis = 0;
            Land.Tile old = Land.LandSystem.NewTile(defs.Map, 104);
            old.Terrain = Definitions.Terrain.Farm;
            old.ParcelId = e1;
            sim.State.Land.Tiles[104] = old;
            sim.State.Ledger.Post(0, sim.Period, "ledger.opening", "test", new Finance.Posting(Finance.Account.Cash, 10_00_000_00), new Finance.Posting(Finance.Account.OpeningEquity, -10_00_000_00));
            Assert.That(sim.Execute(new BuyParcel(0, "e1")).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(0, 104, "radish", Farm.SeedSource.Certified, "none")).Accepted, Is.True);
            long cash = sim.State.Ledger.Cash;
            sim.State.Citizens = null; // schema 7 had no citizens (spec 014)

            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0, schema: 7), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Land.LandState land = r.State.Land;
            Assert.That(land.Tiles[104].Terrain, Is.EqualTo(Definitions.Terrain.Cattle));
            Assert.That(land.Tiles[104].ParcelId, Is.EqualTo(home));
            Assert.That((land.Parcels[home].Owned, land.Parcels[home].Protected, land.Parcels[e1].Owned), Is.EqualTo((true, true, true)));
            Assert.That(r.State.Farm.CropOn(104), Is.Null);
            Simulation after = Simulation.FromState(config, r.State);
            Definitions.CropDef radish = defs.Farm.Crops[defs.Farm.IndexOfCrop("radish")];
            long paid = Market.MarketRules.Value(after, radish.OutputGood, Farm.FarmRules.CycleGrams(after.Clock, radish, radish.SeasonYieldPerAcre), Market.MarketRules.Floor(after, radish.OutputGood));
            Assert.That(r.State.Ledger.Cash - cash, Is.EqualTo(paid));
            Assert.That(r.State.Ledger.Balance(Finance.Account.Land), Is.EqualTo(land.Parcels.Where(x => x.Owned).Sum(x => x.CostBasis)), "land at cost");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            Assert.That(r.State.CommandLog.Entries, Is.Empty, "the migrated town is its own replay base");
            after.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
            byte[] bytes = SaveCodec.Write(after.State, config, TestSim.Wall0);
            LoadResult again = SaveCodec.Read(bytes, config, MinSchema);
            Assert.That(SaveCodec.Write(again.State, config, TestSim.Wall0), Is.EqualTo(bytes));
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
                Assert.That(again.Header.Schema, Is.EqualTo(SaveCodec.SchemaVersion));
                Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), name + " round-trips");
            }
        }

        // Spec 012 AC-16: every schema-6 or older fixture loads at schema 7 with no cattle, becomes its own replay base, advances a
        // day and round-trips.
        [Test]
        public void Schema6_Livestock()
        {
            SimConfig config = SaveTests.Config();
            string[] older = Directory.GetFiles(Dir, "*.ftc", SearchOption.AllDirectories).Where(f => SaveCodec.Read(File.ReadAllBytes(f), config, MinSchema).Header.Schema < 7).ToArray();
            Assert.That(older.Length, Is.GreaterThanOrEqualTo(4), "schema 3 to 6 fixtures");
            foreach (string file in older)
            {
                string name = Path.GetFileName(file);
                LoadResult r = SaveCodec.Read(File.ReadAllBytes(file), config, MinSchema);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), name + ": " + r.Detail);
                Assert.That(r.State.Livestock, Is.Not.Null, name);
                Assert.That(r.State.Livestock.Animals, Is.Empty, name);
                Assert.That(r.State.CommandLog.Base, Is.Not.Null, name + ": the migrated town is its own replay base");
                Simulation sim = Simulation.FromState(config, r.State);
                sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
                LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
                Assert.That(again.Error, Is.EqualTo(SaveError.None), name);
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
