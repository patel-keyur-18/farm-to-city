using System;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public partial class MarketTests
    {
        private static void Rejects(Simulation sim, Command cmd, RejectReason reason)
        {
            ulong before = sim.State.ComputeHash();
            Assert.That(sim.Execute(cmd).Reason, Is.EqualTo(reason), cmd.GetType().Name);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(before), reason + " changed the state");
        }

        // Spec 004 AC-7: random offer and withdraw sequences never leave the rail, and replay identically.
        [Test]
        public void Rail_SupplyNeverZero_Deterministic()
        {
            MarketDef def = TestContent.Game().Market.Market;
            string[] produce = { "radish", "wheat", "rice", "potato", "lentils", "banana", "papaya", "guava", "mango", "spinach", "coriander" };
            ulong RunSequence(ulong seed, bool check)
            {
                Simulation sim = TestSim.NewFarm(seed, game: TestSim.Quiet());
                foreach (string id in produce) sim.State.Market.Stock[Good(sim, id)] += 1_000_000; // 1 t of each to play with
                for (int step = 0; step < 24; step++)
                {
                    long t = (step + 1) * H;
                    ulong r = Rng.Next(seed, (RngStream)901, 0, (ulong)step);
                    string id = produce[(int)Rng.Below(r, (ulong)produce.Length)];
                    long q = (long)Rng.Below(r >> 7, 400_000) + 1;
                    Fixed? min = (r & 3) == 0 ? (Fixed?)MarketRules.RegionalPrice(sim, Good(sim, id)) : null;
                    if ((r & 4) == 0) sim.Execute(new Offer(sim.State.Clock.SimTime, id, q, min));
                    else sim.Execute(new Withdraw(sim.State.Clock.SimTime, id, Math.Min(q, sim.State.Market.OnOffer(Good(sim, id)))));
                    sim.AdvanceTo(t);
                    if (!check) continue;
                    foreach (string g in produce)
                    {
                        int i = Good(sim, g);
                        Fixed reg = MarketRules.RegionalPrice(sim, i), p = sim.State.Market.MarketPrice(i);
                        Assert.That(p >= def.RailMin * reg && p <= def.RailMax * reg, g + " left the rail: " + p);
                    }
                }
                return sim.State.ComputeHash();
            }
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                ulong a = RunSequence(seed, true);
                if (seed % 50 == 0) Assert.That(RunSequence(seed, false), Is.EqualTo(a), "same seed, same prices and sales");
            }
        }

        // Spec 004 AC-10.
        [Test]
        public void SellToTrader()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            int potato = Good(sim, "potato");
            sim.State.Market.Stock[potato] = 3_500_000;
            Assert.That(sim.Execute(new Offer(0, "potato", 1_000_000)).Accepted, Is.True);
            long cash = sim.State.Ledger.Cash;

            Rejects(sim, new SellToTrader(0, "potato", 2_600_000), RejectReason.InsufficientStock); // only 2,500 kg isn't on offer
            Rejects(sim, new SellToTrader(0, "potatoes", 1), RejectReason.UnknownGood);
            Rejects(sim, new SellToTrader(0, "seed_potato", 1), RejectReason.NotProduce);
            Rejects(sim, new SellToTrader(0, "potato", 0), RejectReason.InvalidQuantity);

            Assert.That(sim.Execute(new SellToTrader(0, "potato", 2_000_000)).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(Money.Value(2_000_000, MarketRules.Floor(sim, potato), 1000)));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(10_600_00)); // 2,000 kg × 5.30
            Assert.That(MarketRules.TraderCapacityLeft(sim, potato), Is.EqualTo(1_000_000));
            Assert.That(sim.State.Market.OnOffer(potato), Is.EqualTo(1_000_000), "the offer is untouched");
            Rejects(sim, new SellToTrader(0, "potato", 1_000_001), RejectReason.InsufficientStock);
            Assert.That(sim.Execute(new Withdraw(0, "potato", 1_000_000)).Accepted, Is.True);
            Rejects(sim, new SellToTrader(0, "potato", 1_000_001), RejectReason.TraderFull);
        }

        // Spec 004 AC-11: saplings only at planting; Buy rejects saplings and produce.
        [Test]
        public void Saplings_AtPlantingOnly()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.RichGame());
            Rejects(sim, new Buy(0, "sapling_mango", 1), RejectReason.NotAnInput);
            Rejects(sim, new Buy(0, "potato", 1_000), RejectReason.NotAnInput);
            long cash = sim.State.Ledger.Cash;
            long slot = sim.State.Land.Slots.First(s => s.TreeId == 0).Id;
            Assert.That(sim.Execute(new PlantTree(0, slot, "mango")).Accepted, Is.True);
            int sapling = Good(sim, "sapling_mango");
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.Value(1, MarketRules.RegionalPrice(sim, sapling), 1)));
            Assert.That(sim.State.Ledger.Balance(Account.Trees), Is.EqualTo(350_00));
        }

        // Spec 004 §5: Offer and Withdraw validation.
        [Test]
        public void Offer_Withdraw_Rejections()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            Rejects(sim, new Offer(0, "wheat", 75_001), RejectReason.InsufficientStock); // 75 kg inherited
            Rejects(sim, new Offer(0, "seed_wheat", 1), RejectReason.NotProduce);
            Rejects(sim, new Offer(0, "wheat", -5), RejectReason.InvalidQuantity);
            Rejects(sim, new Offer(0, "wheat", 5, Fixed.Zero), RejectReason.InvalidArgument);
            Assert.That(sim.Execute(new Offer(0, "wheat", 50_000, Fixed.FromInt(30))).Accepted, Is.True);
            Assert.That(sim.State.Market.OfferMinPrice(Good(sim, "wheat")), Is.EqualTo(Fixed.FromInt(30)));
            Rejects(sim, new Offer(0, "wheat", 25_001), RejectReason.InsufficientStock);
            Rejects(sim, new Withdraw(0, "wheat", 50_001), RejectReason.NotOffered);
            Assert.That(sim.Execute(new Withdraw(0, "wheat", 50_000)).Accepted, Is.True);
            Assert.That(sim.State.Market.OnOffer(Good(sim, "wheat")), Is.EqualTo(0));
            Assert.That(sim.State.Market.OfferMinPrice(Good(sim, "wheat")), Is.Null);
        }
    }
}

namespace Sim.Core.Tests
{
    public partial class MarketTests
    {
        // Spec 004 AC-19 (ADR 0034): Buy into the store at today's input price, as an asset (1200); use at average cost.
        [Test]
        public void Buy_AheadAndAtUse()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet((f, j) => TestSim.NoSpoilage(f, j)));
            int seed = Good(sim, "seed_wheat");
            long cash = sim.State.Ledger.Cash, assets = sim.State.Ledger.Balance(Account.InputsInStore);

            // Spring: wheat isn't sown, so its seed is at 0.95 (42.75); in Winter it's 1.10 (49.50), 15.8% dearer.
            Assert.That(sim.Execute(new Buy(0, "seed_wheat", 100_000)).Accepted, Is.True);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(4_275_00));
            Assert.That(sim.State.Ledger.Balance(Account.InputsInStore) - assets, Is.EqualTo(4_275_00), "an asset, not an expense");
            Assert.That(sim.State.Ledger.Balance(Account.SeedsAndPlanting), Is.EqualTo(0));
            Assert.That(sim.State.Market.InStore(seed), Is.EqualTo(100_000));
            sim.AdvanceTo(3 * FarmTest.S); // Winter
            Assert.That(sim.Execute(new Buy(sim.State.Clock.SimTime, "seed_wheat", 100_000)).Accepted, Is.True);
            long winterCost = Money.Value(100_000, MarketRules.InputPrice(sim, seed), 1000);
            Assert.That((double)winterCost / 4_275_00, Is.GreaterThan(1.15)); // 1.10 ÷ 0.95, plus the index
            Assert.That(sim.State.Market.StockCostOf(seed), Is.EqualTo(4_275_00 + winterCost));

            // Use takes stock first at average cost, then buys exactly the shortfall.
            long book = sim.State.Market.StockCostOf(seed);
            cash = sim.State.Ledger.Cash;
            Store.UseInput(sim, seed, 250_000, Account.SeedsAndPlanting, "test");
            long shortfall = Money.Value(50_000, MarketRules.InputPrice(sim, seed), 1000);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(shortfall));
            Assert.That(sim.State.Ledger.Balance(Account.SeedsAndPlanting), Is.EqualTo(book + shortfall));
            Assert.That(sim.State.Market.StockCostOf(seed), Is.EqualTo(0));

            // Capacity and cash.
            Rejects(sim, new Buy(sim.State.Clock.SimTime, "compost", 1_500_000), RejectReason.StoreFull); // 1,250 kg free
            Assert.That(sim.Execute(new Buy(sim.State.Clock.SimTime, "compost", 1_000_000)).Accepted, Is.True);
            Simulation broke = TestSim.NewFarm(game: TestContent.Game());
            Rejects(broke, new Buy(0, "spray", 100_000), RejectReason.InsufficientFunds); // 100 L × 525 = 52,500
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // Spec 004 AC-18: a minimum price keeps the offer off the stall while the price is below it; never to the trader below it.
        [Test]
        public void Offer_MinPrice()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet(TestSim.NoSpoilage));
            int potato = Good(sim, "potato");
            sim.State.Market.Stock[potato] = 240_000;
            Assert.That(sim.Execute(new Offer(0, "potato", 240_000, Fixed.Parse("6.50"))).Accepted, Is.True); // regional 6.80, floor 5.30

            sim.AdvanceTo(H); // P = 6.80 ≥ 6.50: on the stall; a glut, but the trader may not take it at 5.30
            MarketSales first = sim.DrainEvents().OfType<MarketSales>().Single();
            Assert.That(first.LocalQuantity, Is.GreaterThan(0));
            Assert.That(first.TraderQuantity, Is.EqualTo(0));
            Assert.That(sim.State.Market.MarketPrice(potato), Is.LessThan(Fixed.Parse("6.50")));

            sim.AdvanceTo(2 * H); // P < 6.50: off the stall, nothing sells, supply is neutral so P recovers
            Assert.That(sim.DrainEvents().OfType<MarketSales>(), Is.Empty);
            Assert.That(sim.State.Market.MarketPrice(potato), Is.GreaterThanOrEqualTo(Fixed.Parse("6.50")));
            sim.AdvanceTo(3 * H);
            Assert.That(sim.DrainEvents().OfType<MarketSales>().Single().Price, Is.LessThan(Fixed.Parse("6.80")));

            // Over a day the trader never takes any, and a little sells each hour the price allows.
            sim.AdvanceTo(TestSim.Day);
            var day = sim.DrainEvents().OfType<MarketSales>().ToList();
            Assert.That(day.All(e => e.TraderQuantity == 0));
            Assert.That(sim.State.Market.OnOffer(potato), Is.LessThan(240_000).And.GreaterThan(0), "a little sells each hour");
        }
    }
}
