using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public partial class MarketTests
    {
        private const long H = TestSim.Hour;

        private static int Good(Simulation sim, string id) { return sim.Config.Game.Market.IndexOf(id); }

        /// <summary>Puts q grams of a produce good in the store and on offer (the Offer command is US-056's).</summary>
        private static void PutOnOffer(Simulation sim, int good, long q, Fixed minPrice = default(Fixed))
        {
            sim.State.Market.Stock[good] += q;
            sim.State.Market.Offered[good] += q;
            sim.State.Market.MinPrice[good] = minPrice;
        }

        // Spec 004 AC-16 (with golden 004 in ReplayTests): a year of farming and trading, chunked or not, ends identical.
        [Test]
        public void ChunkingInvariance() { FarmPlayTests.AssertChunkingInvariant(11); }

        // Spec 004 AC-5: neutral when nothing is offered, across season changes and index steps.
        [Test]
        public void Neutral_WhenNothingOffered()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            var produce = Enumerable.Range(0, sim.Config.Game.Market.Goods.Count)
                .Where(g => sim.Config.Game.Market.Goods[g].Kind == GoodKind.Produce).ToList();
            for (long t = H; t <= 2 * sim.Clock.SecondsPerYear; t += H)
            {
                sim.AdvanceTo(t);
                foreach (int g in produce)
                    Assert.That(sim.State.Market.MarketPrice(g), Is.EqualTo(MarketRules.RegionalPrice(sim, g)), sim.Config.Game.Market.Goods[g].Id + " at " + t);
            }
            Assert.That(sim.State.Market.PriceIndex, Is.GreaterThan(Fixed.One), "the index stepped");
            Assert.That(produce.Sum(g => sim.State.Market.RailHitsOf(g)), Is.EqualTo(0));
        }

        // Spec 004 AC-6: under constant D_ref and S the price converges monotonically, within 1% after 20 hours.
        [Test]
        public void Converges_NoOvershoot()
        {
            MarketDef def = TestContent.Game().Market.Market;
            var rnd = new Random(11); // test inputs only
            for (int i = 0; i < 200; i++)
            {
                Fixed regional = Fixed.FromRatio(rnd.Next(500, 10_000), 100);
                Fixed transport = Fixed.FromRatio(rnd.Next(0, 300), 100);
                var a = new ClearingInput
                {
                    Price = regional * Fixed.FromRatio(rnd.Next(60, 180), 100), Regional = regional,
                    Floor = regional - transport, Ceiling = regional + transport, Elasticity = Fixed.FromRatio(rnd.Next(20, 200), 100),
                    DemandPerDay = rnd.Next(10_000, 500_000), OtherSupplyPerDay = rnd.Next(10_000, 600_000),
                    Eligible = rnd.Next(0, 3) == 0 ? 0 : rnd.Next(1_000, 2_000_000), TraderMayTake = true,
                    TraderCapacityLeft = rnd.Next(0, 2) * 10_000_000L,
                };
                Fixed target = MarketClearing.Clear(def, a).Target;
                Fixed gap0 = a.Price - target;
                for (int h = 1; h <= 20; h++)
                {
                    ClearingResult r = MarketClearing.Clear(def, a); // constant inputs: the offer isn't drawn down
                    Assert.That(r.Target, Is.EqualTo(target));
                    Fixed before = a.Price - target, after = r.Price - target;
                    Assert.That(after.Raw == 0 || Math.Sign(after.Raw) == Math.Sign(before.Raw), "no overshoot, case " + i);
                    Assert.That(Math.Abs(after.Raw) <= Math.Abs(before.Raw), "monotonic, case " + i);
                    a.Price = r.Price;
                }
                Assert.That(Math.Abs((a.Price - target).Raw), Is.LessThanOrEqualTo(Math.Abs((target * Fixed.FromRatio(1, 100)).Raw)), "within 1% after 20 h, case " + i);
            }
        }

        // Spec 004 AC-8 (ADR 0034): a glut drains hour by hour; the trader takes each hour's unsold share at the
        // floor up to its capacity; then the price can fall below the floor; the capacity resets daily; offers finish.
        [Test]
        public void TraderHoldsFloor_UntilCapacity()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet((f, j) =>
                f == "region.json" ? j.Replace("\"potato\": 3000,", "\"potato\": 200,") : j));
            int potato = Good(sim, "potato");
            PutOnOffer(sim, potato, 1_200_000); // 1,200 kg: 50 kg an hour on the stall
            Fixed floor = MarketRules.Floor(sim, potato);

            sim.AdvanceTo(H);
            MarketSales first = sim.DrainEvents().OfType<MarketSales>().Single();
            Assert.That(first.LocalQuantity + first.TraderQuantity, Is.EqualTo(50_000), "only this hour's lot leaves the offer");
            Assert.That(first.TraderQuantity, Is.GreaterThan(0));
            Assert.That(first.Floor, Is.EqualTo(floor));
            Assert.That(sim.State.Market.OnOffer(potato), Is.EqualTo(1_150_000));

            // The trader's 200 kg is used within the morning; after that nothing goes to it and the price falls under the floor.
            long t = H;
            while (MarketRules.TraderCapacityLeft(sim, potato) > 0) { t += H; sim.AdvanceTo(t); }
            Assert.That(t, Is.LessThan(TestSim.Day - 6 * H));
            Assert.That(sim.State.Market.TraderBought(potato), Is.EqualTo(200_000));
            sim.DrainEvents();
            sim.AdvanceTo(t + 6 * H);
            Assert.That(sim.DrainEvents().OfType<MarketSales>().Sum(e => e.TraderQuantity), Is.EqualTo(0));
            Assert.That(sim.State.Market.MarketPrice(potato), Is.LessThan(floor));

            // The daily pass resets the capacity: the trader buys again.
            sim.AdvanceTo(TestSim.Day + H);
            Assert.That(sim.DrainEvents().OfType<MarketSales>().Last().TraderQuantity, Is.GreaterThan(0));

            // With the minimum lot the offer always finishes: about a day per 2.4 kg left once the lot is down to 100 g.
            sim.AdvanceTo(8 * TestSim.Day);
            Assert.That(sim.State.Market.OnOffer(potato), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // Spec 004 AC-9: a small offer sells as the founder's pro-rata share, paid exactly Value(sold, P).
        [Test]
        public void Offer_SellsProRata()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet());
            int wheat = Good(sim, "wheat"), mango = Good(sim, "mango");
            PutOnOffer(sim, wheat, 4_800); // 200 g an hour: wheat's thin, inelastic market (e = 0.4) takes a little
            PutOnOffer(sim, mango, 1_200);  // 50 g an hour: the minimum lot of 100 g applies
            long cash = sim.State.Ledger.Cash, crop = sim.State.Ledger.Balance(Account.CropSales), fruit = sim.State.Ledger.Balance(Account.FruitSales);

            sim.AdvanceTo(H);
            var sales = sim.DrainEvents().OfType<MarketSales>().ToList();
            MarketSales w = sales.Single(e => e.GoodId == "wheat"), m = sales.Single(e => e.GoodId == "mango");
            Assert.That(w.TraderQuantity, Is.EqualTo(0), "a small offer stays above the floor");
            // The founder's share of what's bought is just under what they put on the stall.
            Assert.That(w.LocalQuantity, Is.InRange(190, 200));
            Assert.That(m.LocalQuantity, Is.InRange(95, 100));
            Assert.That(w.Amount, Is.EqualTo(Money.Value(w.LocalQuantity, w.Price, 1000)));
            Assert.That(m.Amount, Is.EqualTo(Money.Value(m.LocalQuantity, m.Price, 1000)));
            Assert.That(w.Price, Is.LessThan(MarketRules.RegionalPrice(sim, wheat)).And.GreaterThan(MarketRules.Floor(sim, wheat)));
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(w.Amount + m.Amount));
            Assert.That(sim.State.Ledger.Balance(Account.CropSales) - crop, Is.EqualTo(-w.Amount));
            Assert.That(sim.State.Ledger.Balance(Account.FruitSales) - fruit, Is.EqualTo(-m.Amount));
            Assert.That(sim.State.Market.InStore(wheat), Is.EqualTo(75_000 + 4_800 - w.LocalQuantity)); // with the inherited 75 kg
        }

        // Spec 004 AC-15: unmet demand per good per hour; only the last 96 hours are kept.
        [Test]
        public void UnmetDemand_Window()
        {
            Simulation sim = TestSim.NewFarm(game: TestSim.Quiet((f, j) =>
                f == "region.json" ? j.Replace("\"valleySupplyPerDay\": { \"radish\": 135", "\"valleySupplyPerDay\": { \"radish\": 60") : j));
            int radish = Good(sim, "radish"), wheat = Good(sim, "wheat");
            sim.AdvanceTo(100 * H);
            Assert.That(sim.State.Market.Unmet[radish].Length, Is.EqualTo(96));
            Assert.That(sim.State.Market.UnmetAt(radish, 0), Is.GreaterThan(Fixed.Zero), "short supply leaves demand unmet");
            Assert.That(sim.State.Market.UnmetAt(wheat, 0), Is.EqualTo(Fixed.Zero));
            // The price climbs to the import ceiling, where demand is still above supply.
            Assert.That(sim.State.Market.MarketPrice(radish), Is.GreaterThan(MarketRules.RegionalPrice(sim, radish)));
            Assert.That(sim.State.Market.MarketHours, Is.EqualTo(100));
        }
    }
}
