using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.Market;
using Sim.Core.Saves;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 020: the crop ladder's rules, from the real content.</summary>
    internal static class Ladder
    {
        internal static readonly string[] NewCrops = { "methi", "amaranth", "okra", "peas", "maize", "mustard" };
    }

    public partial class ContentTests
    {
        // Spec 020 AC-1: every field crop, old and new, is whole in every file it needs.
        [Test]
        public void CropLadder_Complete()
        {
            GameDefs game = TestContent.Game();
            using JsonDocument region = JsonDocument.Parse(TestContent.ReadContent("region.json"));
            using JsonDocument farming = JsonDocument.Parse(TestContent.ReadContent("farming.json"));
            using JsonDocument strings = JsonDocument.Parse(TestContent.ReadContent("strings/en.json"));
            bool Has(JsonElement e, string key) { return e.TryGetProperty(key, out _); }
            foreach (CropDef c in game.Farm.Crops)
            {
                Assert.That(Has(farming.RootElement.GetProperty("pests").GetProperty("baseRisk"), c.Id), Is.True, c.Id + ": pest risk");
                Assert.That(Has(strings.RootElement, "crop." + c.Id), Is.True, c.Id + ": name");
                Assert.That(Has(strings.RootElement, "crop." + c.Id + ".note"), Is.True, c.Id + ": note");
                Assert.That(Has(strings.RootElement, "good." + game.Market.Goods[c.SeedGood].Id), Is.True, c.Id + ": seed's name");
                Assert.That(game.Market.Goods[c.SeedGood].Kind, Is.EqualTo(GoodKind.Input), c.Id + ": seed is an input");
                Assert.That(Has(region.RootElement.GetProperty("seasonalFactor"), game.Market.Goods[c.SeedGood].Id), Is.True, c.Id + ": seed's price");
                if (c.Fodder) continue;
                GoodDef g = game.Market.Goods[c.OutputGood];
                Assert.That(g.Kind, Is.EqualTo(GoodKind.Produce), c.Id);
                Assert.That(Has(strings.RootElement, "good." + g.Id), Is.True, c.Id + ": good's name");
                foreach (string map in new[] { "seasonalFactor", "traderBuyPerDay", "demandPerDay", "valleySupplyPerDay", "importCapacityPerDay" })
                    Assert.That(Has(region.RootElement.GetProperty(map), g.Id), Is.True, c.Id + ": " + map);
            }
            foreach (string id in Ladder.NewCrops) Assert.That(game.Farm.IndexOfCrop(id), Is.GreaterThanOrEqualTo(0), id);
        }

        // Spec 020 AC-18: every crop's art is found by its name alone, so a new crop needs no code.
        [Test]
        public void EveryCropHasSprites()
        {
            string file = Path.Combine(TestContent.RepoRoot, "unity", "FarmToCity", "Assets", "Resources", "Art", "Blender", "sprites.json");
            using JsonDocument sprites = JsonDocument.Parse(File.ReadAllText(file));
            var names = sprites.RootElement.GetProperty("sprites").EnumerateArray().Select(s => s.GetProperty("name").GetString()).ToHashSet();
            var missing = TestContent.Game().Farm.Crops
                .SelectMany(c => new[] { "crop_" + c.Id + "_g1", "crop_" + c.Id + "_g2", "crop_" + c.Id + "_g3", "crop_" + c.Id + "_ready", "icon_good_" + c.Id })
                .Where(n => !names.Contains(n)).ToList();
            Assert.That(missing, Is.Empty);
        }

        // Spec 020 AC-2: a sowable field crop on every rung of every season.
        [Test]
        public void CropLadder_EverySeason()
        {
            GameDefs game = TestContent.Game();
            (long lo, long hi)[] rungs = { (1, 60), (61, 180), (181, 300), (301, 540), (720, long.MaxValue) };
            for (int s = 0; s < 4; s++)
                foreach ((long lo, long hi) in rungs)
                    Assert.That(game.Farm.Crops.Any(c => !c.Fodder && c.SowSeasons[s] && c.GrowthSeconds / 60 >= lo && c.GrowthSeconds / 60 <= hi), Is.True,
                        "season " + s + ", " + lo + " to " + hi + " minutes");
        }
    }

    public partial class CropTests
    {
        // Spec 020 AC-3: a season of back-to-back cycles harvests the season's yield and uses its seed, to within one cycle's rounding.
        [Test]
        public void CropLadder_SeasonTotals()
        {
            Simulation sim = NewFarm(game: TestContent.Game());
            foreach (string id in Ladder.NewCrops)
            {
                CropDef c = sim.Config.Game.Farm.Crops[sim.Config.Game.Farm.IndexOfCrop(id)];
                long cycles = sim.Clock.SecondsPerSeason / c.GrowthSeconds;
                Assert.That(cycles * c.GrowthSeconds, Is.EqualTo(sim.Clock.SecondsPerSeason), id + ": whole cycles");
                long harvest = cycles * FarmRules.CycleGrams(sim.Clock, c, c.SeasonYieldPerAcre);
                long seed = cycles * FarmRules.CycleGrams(sim.Clock, c, c.SeedPerAcrePerSeason);
                Assert.That(harvest, Is.EqualTo(c.SeasonYieldPerAcre.RoundToLong() * 1000).Within(cycles), id);
                Assert.That(seed, Is.EqualTo(c.SeedPerAcrePerSeason.MulRatio(1000, 1).RoundToLong()).Within(cycles), id);
            }
        }

        // Spec 020 AC-4: each new crop sows only in its seasons; otherwise OutOfSeason, the state unchanged.
        [Test]
        public void CropLadder_SowWindows()
        {
            GameDefs game = RichGame();
            for (int s = 0; s < 4; s++)
                foreach (string id in Ladder.NewCrops)
                {
                    Simulation sim = NewFarm(game: game);
                    long t = s * sim.Clock.SecondsPerSeason;
                    sim.AdvanceTo(t);
                    bool inSeason = game.Farm.Crops[game.Farm.IndexOfCrop(id)].SowSeasons[s];
                    ulong before = sim.State.ComputeHash();
                    CommandResult r = sim.Execute(new Sow(t, 84, id, SeedSource.Certified, "none"));
                    Assert.That(r.Accepted, Is.EqualTo(inSeason), id + " in season " + s + ": " + r);
                    if (inSeason) continue;
                    Assert.That(r.Reason, Is.EqualTo(RejectReason.OutOfSeason), id);
                    Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), id);
                }
        }

        // Spec 020 AC-6: legumes feed the soil under the same reason; maize takes from it.
        [Test]
        public void CropLadder_Fertility()
        {
            Simulation sim = FarmTest.Farm();
            Tile peas = sim.State.Land.Tiles[84], methi = sim.State.Land.Tiles[85], maize = sim.State.Land.Tiles[100];
            Fixed p0 = peas.Fertility, m0 = methi.Fertility, z0 = maize.Fertility;
            foreach ((int tile, string crop) in new[] { (84, "peas"), (85, "methi"), (100, "maize") })
                Assert.That(sim.Execute(new Sow(0, tile, crop, SeedSource.Certified, "none")).Accepted, Is.True, crop);
            sim.AdvanceTo(6 * Hour);
            Fixed Share(int hours) { return Fixed.FromRatio(hours * Hour, sim.Clock.SecondsPerSeason); }
            Assert.That(peas.Fertility - p0, Is.EqualTo(Fixed.FromInt(3) * Share(4)));
            Assert.That(peas.FertilityChange(FertilityReason.Lentils), Is.EqualTo(Fixed.FromInt(3) * Share(4)));
            Assert.That(methi.Fertility - m0, Is.EqualTo(Share(1)));
            Assert.That(methi.FertilityChange(FertilityReason.Lentils), Is.EqualTo(Share(1)));
            Assert.That(maize.Fertility - z0, Is.EqualTo(Fixed.FromInt(-5) * Share(6)));
            Assert.That(maize.FertilityChange(FertilityReason.CropUse), Is.EqualTo(Fixed.FromInt(-5) * Share(6)));
        }
    }

    public partial class SeedTests
    {
        // Spec 020 AC-5: peas and mustard can be saved and age by spec 003's rule; the others can't.
        [Test]
        public void CropLadder_SavedSeed()
        {
            foreach ((string id, int hours) in new[] { ("peas", 4), ("mustard", 8) })
            {
                Simulation sim = FarmTest.Farm();
                int c = sim.Config.Game.Farm.IndexOfCrop(id);
                Assert.That(sim.Execute(new Sow(0, 84, id, SeedSource.Certified, "none")).Accepted, Is.True, id);
                long t = hours * Hour;
                Assert.That(sim.Execute(new Harvest(t, 84, Overflow.SellRestToTrader)).Accepted, Is.True, id);
                Fixed share = Fixed.FromRatio(hours * Hour, sim.Clock.SecondsPerSeason);
                Assert.That(sim.State.Farm.SavedSeedAgeOf(c), Is.EqualTo(share), id + ": aged by a cycle's share");
                Assert.That(sim.Execute(new Sow(t, 84, id, SeedSource.Saved, "none")).Accepted, Is.True, id + ": from saved seed");
            }
            foreach (string id in new[] { "methi", "amaranth", "okra", "maize" })
            {
                Simulation sim = FarmTest.Farm(TestContent.Game((f, j) => f == "crops.json" ? j.Replace("\"sowSeasons\": [\"summer\", \"monsoon\"]", "\"sowSeasons\": [\"spring\"]") : j));
                Assert.That(sim.Execute(new Sow(0, 84, id, SeedSource.Saved, "none")).Reason, Is.EqualTo(RejectReason.SeedCannotBeSaved), id);
            }
        }
    }

    public partial class FounderTests
    {
        // Spec 020 AC-7: the household eats maize, never mustard seed.
        [Test]
        public void CropLadder_MustardNotEaten()
        {
            Simulation sim = NewFarm(game: RichGame((f, j) => NoTruck(f, NoSpoilage(f, j))));
            int mustard = FarmTest.Good(sim, "mustard"), maize = FarmTest.Good(sim, "maize");
            foreach (GoodDef g in sim.Config.Game.Market.Goods) sim.State.Market.Stock[FarmTest.Good(sim, g.Id)] = g.Kind == GoodKind.Produce ? 0 : sim.State.Market.Stock[FarmTest.Good(sim, g.Id)];
            sim.State.Market.Stock[mustard] = 50_000;
            sim.State.Market.Stock[maize] = 50_000;
            sim.AdvanceTo(Day);
            Assert.That(sim.State.Market.InStore(mustard), Is.EqualTo(50_000), "mustard stays");
            Assert.That(sim.State.Market.InStore(maize), Is.LessThan(50_000), "maize is eaten");
        }
    }

    public partial class OrderTests
    {
        // Spec 020 AC-11: the Monsoon's eligible crops; the greens order stays garden-only.
        [Test]
        public void CropLadder_Eligible()
        {
            Simulation sim = Skipped(Quiet());
            MarketDefs market = sim.Config.Game.Market;
            sim.AdvanceTo(2 * sim.Clock.SecondsPerSeason);
            sim.DrainEvents();
            var crops = new System.Collections.Generic.HashSet<string>();
            string[] eligible = { "radish", "rice", "amaranth", "okra", "maize" };
            for (long d = 1; d <= 3; d++)
            {
                sim.AdvanceTo(2 * sim.Clock.SecondsPerSeason + (d - 1) * Day + 7 * Hour);
                foreach (OrdersPosted p in sim.DrainEvents().OfType<OrdersPosted>())
                    foreach (SupermarketOrder o in p.Orders)
                    {
                        string id = market.Goods[o.Good].Id;
                        if (id == "spinach" || id == "coriander") continue;
                        Assert.That(eligible, Does.Contain(id));
                        crops.Add(id);
                    }
            }
            Assert.That(crops.OrderBy(x => x, System.StringComparer.Ordinal), Is.EqualTo(eligible.OrderBy(x => x, System.StringComparer.Ordinal)), "each comes up");
        }
    }

    public partial class PeopleTests
    {
        // Spec 020 AC-12: the hand's pick considers each new crop by the same rule: a rich enough order makes it win.
        [Test]
        public void BestCrop_ConsidersNewCrops()
        {
            foreach ((string id, int season) in new[] { ("methi", 0), ("amaranth", 1), ("okra", 0), ("peas", 3), ("maize", 0), ("mustard", 3) })
            {
                Simulation sim = NewFarm(game: Quiet((f, j) => f == "market.json" ? j.Replace("\"perDay\": 4", "\"perDay\": 0").Replace("\"greensPerDay\": 1", "\"greensPerDay\": 0") : j));
                Assert.That(sim.Execute(new SkipFirstSession(0)).Accepted, Is.True);
                sim.AdvanceTo(season * sim.Clock.SecondsPerSeason + 7 * Hour);
                MarketState m = sim.State.Market;
                int good = FarmTest.Good(sim, id);
                m.Orders.Add(new SupermarketOrder { Id = m.NextOrderId++, Good = good, Amount = 10_000, Price = Fixed.FromInt(100_000), Premium = Fixed.Zero, PostedAt = sim.State.Clock.SimTime });
                FarmDefs farm = sim.Config.Game.Farm;
                Assert.That(farm.Crops[FarmRules.BestCrop(sim, sim.State.Land.Tiles[84], farm.Fertilisers[0])].Id, Is.EqualTo(id));
            }
        }
    }

    public partial class MigrationTests
    {
        // Spec 020 AC-13: a save written before the crop ladder loads: the new goods empty, at their price; play goes on with them.
        [Test]
        public void CropLadder_OldSaveLoads()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(12)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Simulation sim = Simulation.FromState(config, r.State);
            MarketDefs market = config.Game.Market;
            foreach (string id in Ladder.NewCrops)
            {
                int g = market.IndexOf(id);
                Assert.That(r.State.Market.InStore(g), Is.EqualTo(0), id);
                Assert.That(r.State.Market.DailyFactorOf(g), Is.EqualTo(Fixed.One), id);
                Assert.That(MarketRules.RegionalPrice(sim, g), Is.EqualTo(market.Goods[g].BasePrice * r.State.Market.PriceIndex * market.Region.SeasonalFactor(g, (int)Calendar.SeasonAt(r.State.Clock.SimTime, sim.Clock))), id);
            }
            LoadResult again = SaveCodec.Read(SaveCodec.Write(sim.State, config, TestSim.Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()), "saved and loaded again");

            sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Hour);
            int amaranth = market.IndexOf("amaranth");
            Assert.That(sim.State.Market.MarketPrice(amaranth), Is.GreaterThan(Fixed.Zero), "the market has its price after an hour");
            string crop = config.Game.Farm.Crops.First(c => !c.Fodder && c.SowSeasons[(int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock)] && System.Array.IndexOf(Ladder.NewCrops, c.Id) >= 0).Id;
            int tile = Tools.Balancer.Home.Acres(config.Game).First();
            if (sim.State.Farm.CropOn(tile) != null) Assert.That(sim.Execute(new ClearField(sim.State.Clock.SimTime, tile)).Accepted, Is.True);
            CommandResult sow = sim.Execute(new Sow(sim.State.Clock.SimTime, tile, crop, SeedSource.Certified, "none"));
            Assert.That(sow.Accepted, Is.True, sow.ToString());
        }

        // Spec 020 §3.10 (schema 13): a town below it becomes its own replay base, because its old history can't replay with new crops.
        [Test]
        public void Schema12_ReplayBase()
        {
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(BotFixture(12)), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.CommandLog.Base, Is.Not.Null, "its own replay base");
            Assert.That(r.State.CommandLog.Entries, Is.Empty);
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(r.State.Clock.SimTime + TestSim.Hour);
            byte[] bytes = SaveCodec.Write(sim.State, config, TestSim.Wall0);
            LoadResult again = SaveCodec.Read(bytes, config, MinSchema);
            Assert.That(SaveCodec.Replay(again, config).State.ComputeHash(), Is.EqualTo(again.State.Clone().ComputeHash()), "the history replays");
        }
    }
}
