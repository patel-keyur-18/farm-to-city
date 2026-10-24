using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    /// <summary>
    /// Save schema 5 (spec 010 §3.4): a schema-4 town on the 8 × 6 map moves into the 16 × 12 map. The case fixture
    /// <c>saves/cases/schema4-s5.ftc</c> owns <c>s5</c> with a growing crop, a tree and a bank loan secured on it; its
    /// <c>.json</c> pins the old town's values. It was written once, while the writer's format still equalled schema 4
    /// (<see cref="WriteFarmLaneCase"/>, kept for the record); never regenerate it.
    /// </summary>
    public class MapMigrationTests
    {
        private static string Dir { get { return Path.Combine(TestContent.TestFile("saves"), "cases"); } }
        private static string CaseFile { get { return Path.Combine(Dir, "schema4-s5.ftc"); } }

        public sealed class Pins
        {
            public long Cash { get; set; }
            public long S5Price { get; set; }
            public long LoanRequired { get; set; }
            public long TreeCost { get; set; }
            public List<long[]> Tiles { get; set; } // fertility raw, moisture raw, vacantSince
            public List<int> CropTiles { get; set; }
            public List<int> BuildingTiles { get; set; }
            public List<int> SlotTiles { get; set; }
        }

        private static LoadResult Load(out Pins pins)
        {
            pins = JsonSerializer.Deserialize<Pins>(File.ReadAllText(Path.ChangeExtension(CaseFile, ".json")));
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(CaseFile), SaveTests.Config(), 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(4));
            Assert.That(r.MigratedMap, Is.True);
            return r;
        }

        // Spec 010 AC-6.
        [Test]
        public void Schema5_RemapsTiles()
        {
            LoadResult r = Load(out Pins pins);
            GameDefs defs = SaveTests.Config().Game;
            LandState land = r.State.Land;
            Assert.That(land.Tiles.Count, Is.EqualTo(192));
            var old = new HashSet<int>();
            for (int i = 0; i < 48; i++)
            {
                int at = MapMigration.Remap(i);
                old.Add(at);
                Tile t = land.Tiles[at];
                Assert.That(t.Id, Is.EqualTo(at));
                Assert.That(t.Terrain, Is.EqualTo(defs.Map.Tiles[at].Terrain), "terrain from the content (tile 45 becomes the farm lane)");
                if (i == 45 || i == 30) continue; // the farm lane's acre and the cattle acre (ADR 0064): what stood there was paid for and cleared
                Assert.That(new[] { t.Fertility.Raw, t.Moisture.Raw, t.VacantSince }, Is.EqualTo(pins.Tiles[i]), "old tile " + i);
            }
            for (int i = 0; i < 192; i++)
            {
                if (old.Contains(i)) continue;
                Assert.That((land.Tiles[i].Fertility, land.Tiles[i].Moisture), Is.EqualTo((defs.Map.Tiles[i].Fertility, defs.Map.Tiles[i].Moisture)), "new tile " + i);
                Assert.That(land.Parcels[land.Tiles[i].ParcelId].Owned, Is.False, "new land starts unowned");
            }
            Assert.That(r.State.Farm.Crops.Select(c => c.TileId), Is.EquivalentTo(pins.CropTiles.Where(t => t != 45 && t != 30).Select(MapMigration.Remap)));
            Assert.That(land.Buildings.Select(b => b.TileId), Is.EqualTo(pins.BuildingTiles.Select(MapMigration.Remap)));
            Assert.That(land.Slots.Select(s => s.TileId), Is.EqualTo(pins.SlotTiles.Where(t => t != 45 && t != 30).Select(MapMigration.Remap)));

            // It advances a game day and round-trips byte for byte.
            SimConfig config = SaveTests.Config();
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Day);
            byte[] bytes = SaveCodec.Write(sim.State, config, TestSim.Wall0);
            LoadResult again = SaveCodec.Read(bytes, config);
            Assert.That(again.Error, Is.EqualTo(SaveError.None), again.Detail);
            Assert.That(again.MigratedMap, Is.False);
            Assert.That(SaveCodec.Write(again.State, config, TestSim.Wall0), Is.EqualTo(bytes));
        }

        // Spec 010 AC-7.
        [Test]
        public void Schema5_FarmLaneParcel()
        {
            LoadResult r = Load(out Pins pins);
            SimConfig config = SaveTests.Config();
            GameDefs defs = config.Game;
            Simulation sim = Simulation.FromState(config, r.State);
            int s5 = defs.Map.IndexOfParcel("s5");
            Assert.That(r.State.Land.Parcels[s5].Owned, Is.False);
            Assert.That(r.State.Land.Tiles[135].Terrain, Is.EqualTo(Terrain.Road));
            Assert.That(r.State.Farm.CropOn(135), Is.Null);
            Assert.That(r.State.Land.Slots.Any(s => s.TileId == 135), Is.False);

            CropDef potato = defs.Farm.Crops[defs.Farm.IndexOfCrop("potato")];
            long crop = MarketRules.Value(sim, potato.OutputGood, FarmRules.CycleGrams(sim.Clock, potato, potato.SeasonYieldPerAcre),
                MarketRules.Floor(sim, potato.OutputGood));
            RoadTake take = r.RoadTake;
            Assert.That(take, Is.Not.Null);
            Assert.That(take.Compensation, Is.EqualTo(2 * pins.S5Price));
            Assert.That(take.OnIt, Is.EqualTo(crop + pins.TreeCost));
            Assert.That(take.LoanRepaid, Is.EqualTo(pins.LoanRequired));
            Assert.That(pins.LoanRequired, Is.GreaterThan(0), "the case has a loan secured on s5");
            Assert.That(r.State.Ledger.Cash, Is.EqualTo(pins.Cash + take.Compensation + take.OnIt - take.LoanRepaid));
            Assert.That(r.State.Credit.Loans.Count(l => l.IsOpen), Is.EqualTo(0), "the loan s5 secured, and no other land to secure it, is repaid");
            Assert.That(r.State.Ledger.Balance(Account.Land), Is.EqualTo(r.State.Land.Parcels.Where(p => p.Owned).Sum(p => p.CostBasis)), "land at cost");

            // The migrated town is its own replay base: the old history can't replay on the new map (spec 007 AC-6).
            Assert.That(r.State.CommandLog.Entries, Is.Empty);
            Assert.That(SaveCodec.Replay(r, config).State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()));
        }

        /// <summary>
        /// Wrote the case fixture on 2026-10-21, before spec 011 changed the state, so the bytes are a true schema-4 save.
        /// Run only to see how it was made; with a newer writer it would write a broken fixture.
        /// </summary>
        [Test, Explicit("writes the frozen case fixture; valid only while the writer's format equals schema 4")]
        public void WriteFarmLaneCase()
        {
            string old = File.ReadAllText(TestContent.TestFile("saves/map-8x6.json"));
            GameDefs game = TestContent.Game((f, j) =>
                f == "map.json" ? old
                : f == "land.json" ? j.Replace("[86, 102]", "[20, 28]")
                : f == "start.json" ? j.Replace("\"tile\": 87", "\"tile\": 21").Replace("\"startingCash\": 20000", "\"startingCash\": 2000000")
                : f == "onboarding.json" ? j.Replace("field:102", "field:28").Replace("tile:86", "tile:20").Replace("field:86", "field:20")
                : j);
            SimConfig config = TestSim.Config(game: game);
            Simulation sim = Simulation.NewGame(config, new NewGame(7, TestSim.Wall0, 8 * 3600));
            long t = sim.State.Clock.SimTime;
            int s5 = game.Map.IndexOfParcel("s5");
            long price = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[s5]);
            long loan = System.Math.Min(price / 2, CreditRules.Limit(sim, 1, price).Limit);
            loan -= loan % Money.Coin;
            Assert.That(sim.Execute(new BuyParcel(t, "s5", new Financing("coop_bank", loan, 16))).Reason, Is.EqualTo(RejectReason.None), "s5 with a bank loan");
            Assert.That(sim.Execute(new Sow(t, 45, "potato", SeedSource.Certified, "none")).Accepted, Is.True);
            Assert.That(sim.Execute(new Sow(t, 20, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
            TreeSlot slot = sim.State.Land.Slots.First(s => s.TileId == 45);
            Assert.That(sim.Execute(new PlantTree(t, slot.Id, "mango")).Accepted, Is.True);
            sim.AdvanceTo(t + 3600);
            sim.DrainEvents();
            var pins = new Pins
            {
                Cash = sim.State.Ledger.Cash,
                S5Price = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[s5]),
                LoanRequired = CreditSystem.RequiredOnSale(sim, s5),
                TreeCost = sim.State.Farm.Trees.Single(x => x.SlotId == slot.Id).Cost,
                Tiles = sim.State.Land.Tiles.Select(x => new[] { x.Fertility.Raw, x.Moisture.Raw, x.VacantSince }).ToList(),
                CropTiles = sim.State.Farm.Crops.Select(c => c.TileId).ToList(),
                BuildingTiles = sim.State.Land.Buildings.Select(b => b.TileId).ToList(),
                SlotTiles = sim.State.Land.Slots.Select(s => s.TileId).ToList(),
            };
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(CaseFile, SaveCodec.Write(sim.State, config, TestSim.Wall0, schema: 4));
            File.WriteAllText(Path.ChangeExtension(CaseFile, ".json"), JsonSerializer.Serialize(pins, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }
    }
}
