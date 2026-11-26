using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    public partial class MigrationTests
    {
        /// <summary>The vegetable stall's second good after potato (its id), for the fixture's fixed cap.</summary>
        private static string StallSecond(SimConfig c)
        {
            return c.Game.Market.Goods[c.Game.Businesses.Types.First(x => x.Id == "veg_stall").Goods.First(g => c.Game.Market.Goods[g].Id != "potato")].Id;
        }

        private static string Supply25Fixture { get { return Path.Combine(Dir, "schema25-supply.ftc"); } }

        /// <summary>
        /// Sprint 28 (spec 030, US-268): a vegetable stall on Manual with a half cap on potato, a fixed 10 kg cap on onion and a day of buying kept, and a bakery on Manual
        /// with none of your mill's atta. Written while schema 25 is the current one: <c>FTC_UPDATE_SCHEMA25=1</c>.
        /// </summary>
        private static void WriteSupply25Fixture()
        {
            Assert.That(SaveCodec.SchemaVersion, Is.EqualTo(25), "this fixture is the schema-25 one");
            Simulation sim = Start();
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 1_000_000_00), new Posting(Account.Sales, -1_000_000_00)); // a balanced entry, not a command: the file must stay saveable
            Business.Business mill = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 2_000_000);
            Business.Business stall = ShopTests.Shop(sim, "veg_stall", 0, HomeTile, false);
            Business.Business bakery = RecipeTests.Found(sim, "bakery", HomeTile + 1, 200_000_00);
            long t = sim.State.Clock.SimTime;
            int potato = sim.Config.Game.Market.IndexOf("potato");
            foreach (int g in BusinessRules.TypeOf(sim, stall).Goods) Ok(sim, new SetStallGoods(t, stall.Id, sim.Config.Game.Market.Goods[g].Id, 0));
            Ok(sim, new SetStallGoods(t, stall.Id, "potato", 3_000));
            Ok(sim, new SetStallGoods(t, stall.Id, StallSecond(sim.Config), 8_000));
            sim.State.Market.Stock[potato] += 2_000_000;
            Ok(sim, new SetSupplyMode(t, stall.Id, true));
            Ok(sim, new SetSupplyCap(t, stall.Id, "potato", 50, 0));
            Ok(sim, new SetSupplyCap(t, stall.Id, StallSecond(sim.Config), 100, 10_000));
            Ok(sim, new SetSupplyMode(t, bakery.Id, true));
            Ok(sim, new SetSupplyCap(t, bakery.Id, "atta", 0, 0));
            sim.AdvanceTo((sim.State.Clock.SimTime / 86_400 + 3) * 86_400 + 8 * 3600);
            Assert.That(mill.Status, Is.EqualTo(BusinessStatus.Open));
            Directory.CreateDirectory(Dir);
            File.WriteAllBytes(Supply25Fixture, SaveCodec.Write(sim.State, SaveTests.Config(), Wall0, 0, 0)); // the log restarts here: its test-only SetCash helper can't be saved
            var pin = new Pinned { Schema = 25, Hash = "0x" + sim.State.ComputeHash().ToString("X16"), Facts = Facts(sim.State) };
            File.WriteAllText(Path.ChangeExtension(Supply25Fixture, ".json"), JsonSerializer.Serialize(pin, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        }

        // Sprint 28: schema 25's supply (Manual, the caps, the day's counts, the days bought) loads in every later build, round-trips to the same hash and carries on.
        [Test]
        public void Schema25_Supply()
        {
            if (Environment.GetEnvironmentVariable("FTC_UPDATE_SCHEMA25") == "1") WriteSupply25Fixture();
            Assert.That(File.Exists(Supply25Fixture), Is.True, "run once with FTC_UPDATE_SCHEMA25=1");
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(File.ReadAllBytes(Supply25Fixture), config, MinSchema);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.Header.Schema, Is.EqualTo(25));
            int potato = config.Game.Market.IndexOf("potato"), onion = config.Game.Market.IndexOf(StallSecond(config)), atta = config.Game.Market.IndexOf("atta");
            Business.Business stall = r.State.Businesses.Businesses.Single(b => config.Game.Businesses.Types[b.Type].Id == "veg_stall");
            Business.Business bakery = r.State.Businesses.Businesses.Single(b => config.Game.Businesses.Types[b.Type].Id == "bakery");
            Assert.That((stall.SupplyManual, stall.SupplyUsed, bakery.SupplyManual), Is.EqualTo((true, true, true)));
            Assert.That((stall.CapShare[potato], stall.CapGrams[potato], stall.CapGrams[onion]), Is.EqualTo((50, 0L, 10_000L)), "the caps are kept");
            Assert.That(bakery.CapShare[atta], Is.EqualTo(0));
            Assert.That(stall.FromYouToday.Sum() + stall.BoughtToday.Sum() + stall.BoughtDays.Sum(d => d.Sum()), Is.GreaterThan(0), "the counts are kept");
            Assert.That(r.State.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in r.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null);
            LoadResult again = SaveCodec.Read(SaveCodec.Write(r.State, config, Wall0), config, MinSchema);
            Assert.That(again.State.ComputeHash(), Is.EqualTo(r.State.ComputeHash()), "round-trips");
            Simulation sim = Simulation.FromState(config, r.State);
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * 86_400);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null, "three days on");
            Assert.That(SupplyRules.CapOf(sim, stall.Id == 0 ? stall : sim.State.Businesses.Find(stall.Id), potato), Is.EqualTo(SupplyRules.DayAmount(sim, sim.State.Businesses.Find(stall.Id), potato) / 2), "half of a day's potato");
        }

        // An older save loads with every business on Automatic, nothing counted, and the same behaviour as before (schema 25 adds state only).
        [Test]
        public void Schema24_LoadsAutomatic()
        {
            SimConfig config = SaveTests.Config();
            foreach (string file in Directory.GetFiles(Dir, "schema24*.ftc").Concat(Directory.GetFiles(Dir, "schema23*.ftc")))
            {
                LoadResult r = SaveCodec.Read(File.ReadAllBytes(file), config, MinSchema);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), file + ": " + r.Detail);
                Assert.That(r.State.Businesses.Businesses.All(b => !b.SupplyManual && !b.SupplyUsed), Is.True, file + ": Automatic");
                Assert.That(r.State.People == null || r.State.People.Orders.All(o => o.ForBusiness == 0), Is.True, file + ": no stall orders");
                Simulation sim = Simulation.FromState(config, r.State);
                sim.AdvanceTo(sim.State.Clock.SimTime + 2 * 86_400);
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null, file);
            }
        }
    }
}
