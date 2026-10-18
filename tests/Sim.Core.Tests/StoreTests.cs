using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;

namespace Sim.Core.Tests
{
    public class StoreTests
    {
        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        // Spec 004 AC-23.
        [Test]
        public void InheritedStore()
        {
            Simulation sim = TestSim.NewFarm();
            Assert.That(sim.State.Market.InStore(Good(sim, "seed_potato")), Is.EqualTo(600_000));
            Assert.That(sim.State.Market.InStore(Good(sim, "wheat")), Is.EqualTo(75_000));
            Assert.That(sim.State.Market.InStore(Good(sim, "lentils")), Is.EqualTo(75_000));
            foreach (string seed in new[] { "seed_radish", "seed_spinach", "seed_coriander" })
                Assert.That(sim.State.Market.InStore(Good(sim, seed)), Is.EqualTo(100));
            Assert.That(sim.State.Market.InStore(Good(sim, "potato")), Is.EqualTo(0));
            Assert.That(Store.UsedGrams(sim), Is.EqualTo(750_300));
            Assert.That(Store.CapacityGrams(sim), Is.EqualTo(2_000_000));

            var e = Assert.Throws<ContentException>(() => TestContent.Game((f, j) =>
                f == "start.json" ? j.Replace("\"quantity\": 600", "\"quantity\": 1950") : j));
            Assert.That(e.FieldPath, Is.EqualTo("start.store"));
            Assert.That(e.Message, Does.Contain("more than the store's 2000 kg"));
        }

        // Spec 004 AC-12.
        [Test]
        public void Capacity_AndOverflow()
        {
            Simulation sim = TestSim.NewFarm();
            int potato = Good(sim, "potato");
            long free = 2_000_000 - 750_300;
            ulong before = sim.State.ComputeHash();

            Assert.That(Store.Add(sim, potato, free + 1, Overflow.StoreOnly, "test"), Is.EqualTo(RejectReason.StoreFull));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(before));

            // 3,300 kg with room for 1,249.7 kg: the rest (2,050.3 kg) is sold at the Spring floor 5.30.
            long cash = sim.State.Ledger.Cash;
            sim.DrainEvents();
            Assert.That(Store.Add(sim, potato, 3_300_000, Overflow.SellRestToTrader, "test"), Is.EqualTo(RejectReason.None));
            Assert.That(sim.State.Market.InStore(potato), Is.EqualTo(free));
            Assert.That(Store.UsedGrams(sim), Is.EqualTo(2_000_000));
            long sold = 3_300_000 - free;
            long expected = Money.Value(sold, MarketRules.Floor(sim, potato), 1000);
            Assert.That(expected, Is.EqualTo(10_866_59)); // 2,050.3 kg × 5.30 = 10,866.59
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(expected));
            Assert.That(sim.State.Ledger.Balance(Account.CropSales), Is.EqualTo(-expected));
            Assert.That(sim.State.Market.TraderBought(potato), Is.EqualTo(sold));
            Assert.That(sim.DrainEvents().OfType<SoldToTrader>().Single().Quantity, Is.EqualTo(sold));

            // The trader's capacity is 3,000 kg a day: 949.7 kg left today.
            before = sim.State.ComputeHash();
            Assert.That(Store.Add(sim, potato, 949_701, Overflow.SellRestToTrader, "test"), Is.EqualTo(RejectReason.TraderFull));
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(before));
            Assert.That(Store.Add(sim, potato, 949_700, Overflow.SellRestToTrader, "test"), Is.EqualTo(RejectReason.None));

            // Fruit overflow goes to Fruit sales.
            sim.AdvanceTo(TestSim.Day);
            int banana = Good(sim, "banana");
            Assert.That(Store.Add(sim, banana, 10_000, Overflow.SellRestToTrader, "test"), Is.EqualTo(RejectReason.None));
            Assert.That(sim.State.Ledger.Balance(Account.FruitSales), Is.LessThan(0));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // Spec 004 AC-14 (inputs at today's input price, ADR 0032).
        [Test]
        public void Valuation_AtTraderPrice()
        {
            Simulation sim = TestSim.NewFarm();
            int potato = Good(sim, "potato");
            Store.Add(sim, potato, 100_000, Overflow.StoreOnly, "test");
            long expected = 0;
            for (int g = 0; g < sim.Config.Game.Market.Goods.Count; g++)
            {
                long q = sim.State.Market.InStore(g);
                if (q == 0) continue;
                bool produce = sim.Config.Game.Market.Goods[g].Kind == Definitions.GoodKind.Produce;
                expected += MarketRules.Value(sim, g, q, produce ? MarketRules.Floor(sim, g) : MarketRules.InputPrice(sim, g));
            }
            Assert.That(Store.Value(sim), Is.EqualTo(expected));
            // Spring: 600 kg seed potato × 22 = 13,200; wheat 75 × 22.25 = 1,668.75; lentils 75 × 68.50 = 5,137.50;
            // potato 100 × 5.30 = 530; garden seed 0.1 kg each × 300, 200, 150 = 65.
            Assert.That(expected, Is.EqualTo(13_200_00 + 1_668_75 + 5_137_50 + 530_00 + 65_00));
        }

        // Spec 004 §3.4 UseInput (the AC-19 test adds Buy in Sprint 3).
        [Test]
        public void UseInput_StockFirst()
        {
            Simulation sim = TestSim.NewFarm();
            int seed = Good(sim, "seed_potato");
            long cash = sim.State.Ledger.Cash;

            Assert.That(Store.ShortfallCost(sim, seed, 175_000), Is.EqualTo(0));
            Store.UseInput(sim, seed, 175_000, Account.SeedsAndPlanting, "test");
            Assert.That(sim.State.Market.InStore(seed), Is.EqualTo(425_000));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash));

            // 500 kg needed, 425 kg in store: the 75 kg shortfall costs 75 × 22 = 1,650 (Spring: potato seed season).
            Assert.That(Store.ShortfallCost(sim, seed, 500_000), Is.EqualTo(1_650_00));
            sim.DrainEvents();
            Store.UseInput(sim, seed, 500_000, Account.SeedsAndPlanting, "test");
            Assert.That(sim.State.Market.InStore(seed), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - 1_650_00));
            Assert.That(sim.State.Ledger.Balance(Account.SeedsAndPlanting), Is.EqualTo(1_650_00));
            Assert.That(sim.DrainEvents().OfType<InputsBought>().Single().Quantity, Is.EqualTo(75_000));
        }
    }
}
