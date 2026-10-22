using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 011 §3.3: the city supermarket's daily orders (ADR 0055).</summary>
    public partial class OrderTests
    {
        /// <summary>A content edit keeping the order rules but posting none, so a test places its own order.</summary>
        private static string NonePosted(string file, string json)
        {
            return file == "market.json" ? json.Replace("\"perDay\": 2", "\"perDay\": 0").Replace("\"greensPerDay\": 1", "\"greensPerDay\": 0") : json;
        }

        private static SupermarketOrder Place(Simulation sim, string good, long grams, string price)
        {
            MarketState m = sim.State.Market;
            var o = new SupermarketOrder { Id = m.NextOrderId++, Good = FarmTest.Good(sim, good), Amount = grams, Price = Fixed.Parse(price), PostedAt = sim.State.Clock.SimTime };
            m.Orders.Add(o);
            return o;
        }

        private static Simulation Skipped(GameDefs game, long localSeconds = 0, ulong seed = 42)
        {
            Simulation sim = NewFarm(seed, localSeconds, game);
            Ok(sim, new SkipFirstSession(localSeconds));
            return sim;
        }

        // Spec 011 AC-7.
        [Test]
        public void Posting()
        {
            List<string> Days(ulong seed, out int checkedOrders)
            {
                Simulation sim = Skipped(Quiet(), 0, seed);
                FarmDefs farm = sim.Config.Game.Farm;
                OrderDef def = sim.Config.Game.Market.Orders;
                var log = new List<string>();
                checkedOrders = 0;
                for (long day = 0; day < 2 * sim.Clock.SecondsPerYear / Day; day++)
                {
                    sim.AdvanceTo(day * Day + 6 * Hour);
                    int season = (int)Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock);
                    var eligible = Enumerable.Range(0, farm.Crops.Count).Where(i => farm.Crops[i].SowSeasons[season] && farm.Crops[i].GrowthSeconds <= 18 * Hour)
                        .Select(i => farm.Crops[i].OutputGood).ToList();
                    OrdersPosted posted = sim.DrainEvents().OfType<OrdersPosted>().Single();
                    log.Add(posted.ToString());
                    var crops = posted.Orders.Where(o => farm.Crops.Any(c => c.OutputGood == o.Good)).ToList();
                    var greens = posted.Orders.Except(crops).ToList();
                    Assert.That(crops.Count, Is.EqualTo(eligible.Count == 0 ? 0 : def.PerDay));
                    if (eligible.Count >= 2) Assert.That(crops.Select(o => o.Good).Distinct().Count(), Is.EqualTo(crops.Count), "distinct crops");
                    foreach (SupermarketOrder o in posted.Orders)
                    {
                        Assert.That(o.Premium, Is.InRange(def.PremiumMin, def.PremiumMax));
                        Assert.That(o.Price, Is.EqualTo(MarketRules.RegionalPrice(sim, o.Good) * (Fixed.One + o.Premium)), "priced at posting");
                    }
                    foreach (SupermarketOrder o in crops)
                    {
                        Assert.That(eligible, Does.Contain(o.Good));
                        long baseSowing = Supermarket.BaseSowing(sim, farm.Crops.First(c => c.OutputGood == o.Good));
                        Assert.That(o.Amount % 10_000, Is.EqualTo(0), "10 kg lots");
                        Assert.That(o.Amount, Is.InRange(System.Math.Max(10_000, baseSowing / 2 - 10_000), baseSowing), "half to one acre's sowing (ADR 0058)");
                        checkedOrders++;
                    }
                    Assert.That(greens.Count, Is.EqualTo(1));
                    GardenCropDef g = farm.GardenCrops.Single(x => x.Good == greens[0].Good);
                    Assert.That(new[] { "spinach", "coriander" }, Does.Contain(sim.Config.Game.Market.Goods[g.Good].Id));
                    Assert.That(greens[0].Amount % 500, Is.EqualTo(0), "0.5 kg lots");
                    Assert.That(greens[0].Amount, Is.InRange(2 * g.HarvestGrams - 500, 4 * g.HarvestGrams), "2–4 pickings");
                }
                return log;
            }

            List<string> a = Days(5, out int n);
            Assert.That(n, Is.GreaterThan(50)); // 2 years: 32 days
            Assert.That(Days(5, out _), Is.EqualTo(a), "the same seed, the same orders");

            // No crop ripens within the day: only the greens order.
            Simulation slow = Skipped(Quiet((f, j) => f == "crops.json" ? j.Replace("\"growthMinutes\": 120,", "\"growthMinutes\": 1200,").Replace("\"growthMinutes\": 900", "\"growthMinutes\": 1200") : j));
            int s = (int)Calendar.SeasonAt(0, slow.Clock);
            Assert.That(slow.Config.Game.Farm.Crops.Where(c => c.SowSeasons[s]).All(c => c.GrowthSeconds > 18 * Hour), "Spring has no quick crop in this content");
            slow.AdvanceTo(6 * Hour);
            Assert.That(slow.PendingEvents.OfType<OrdersPosted>().Single().Orders.Count, Is.EqualTo(1));
        }

        // Spec 011 AC-8.
        [Test]
        public void FreshToday()
        {
            Simulation sim = Skipped(FarmTest.AnySeason((f, j) => NoSpoilage(f, NonePosted(f, j))));
            int radish = FarmTest.Good(sim, "radish");
            sim.AdvanceTo(6 * Hour);
            sim.State.Market.Stock[radish] = 50_000; // old stock, not fresh
            Ok(sim, new Sow(6 * Hour, HomeTile, "radish", SeedSource.Certified, "none"));
            long ready = sim.State.Farm.CropOn(HomeTile).ReadyAt;
            sim.AdvanceTo(ready);
            Ok(sim, new Harvest(ready, HomeTile, Overflow.StoreOnly));
            long harvested = sim.State.Market.Stock[radish] - 50_000;
            Assert.That(Supermarket.FreshStock(sim, radish), Is.EqualTo(harvested));

            Ok(sim, new SellToTrader(ready, "radish", 50_000)); // older stock leaves first: the fresh amount is unchanged
            Assert.That(Supermarket.FreshStock(sim, radish), Is.EqualTo(harvested));
            SupermarketOrder o = Place(sim, "radish", 10 * harvested, "10");
            Assert.That(sim.Execute(new DeliverOrder(ready, o.Id, harvested + 1)).Reason, Is.EqualTo(RejectReason.InsufficientStock));
            Ok(sim, new DeliverOrder(ready, o.Id, harvested));

            sim.State.Market.FreshToday[radish] = 0; // nothing harvested since 6:00
            sim.State.Market.Stock[radish] = 20_000;
            Assert.That(sim.Execute(new DeliverOrder(ready, o.Id, 1_000)).Reason, Is.EqualTo(RejectReason.InsufficientStock), "old stock doesn't count");
        }

        // Spec 011 AC-9.
        [Test]
        public void DeliverAndLapse()
        {
            Simulation sim = Skipped(Quiet((f, j) => NoSpoilage(f, NonePosted(f, j))));
            int radish = FarmTest.Good(sim, "radish");
            sim.AdvanceTo(8 * Hour);
            SupermarketOrder o = Place(sim, "radish", 100_000, "10");
            sim.State.Market.Stock[radish] += 120_000;
            sim.State.Market.FreshToday[radish] += 120_000;
            long cash = sim.State.Ledger.Cash, worth = Reporting.NetWorth(sim).Total;

            Ok(sim, new DeliverOrder(8 * Hour, o.Id, 30_000));
            Ok(sim, new DeliverOrder(8 * Hour, o.Id, 20_000));
            Assert.That(sim.Execute(new DeliverOrder(8 * Hour, o.Id, 60_000)).Reason, Is.EqualTo(RejectReason.OrderFilled));
            Assert.That(sim.State.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(500_00));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash), "the cash comes at 6:00");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(Reporting.NetWorth(sim).OwedByBuyers, Is.EqualTo(500_00));
            Assert.That(sim.PendingEvents.OfType<OrderDelivered>().Sum(e => e.Amount), Is.EqualTo(500_00));
            Assert.That(sim.State.Ledger.Balance(Account.CropSales), Is.LessThanOrEqualTo(-500_00), "booked as a sale at delivery");

            sim.AdvanceTo(Day + 30 * 60);
            Assert.That(sim.Execute(new DeliverOrder(Day + 30 * 60, o.Id, 1_000)).Reason, Is.EqualTo(RejectReason.OrderClosed), "after midnight");
            sim.DrainEvents();
            long before = sim.State.Ledger.Cash;
            sim.AdvanceTo(Day + 6 * Hour);
            Assert.That(sim.PendingEvents.OfType<OrderPaid>().Single().Amount, Is.EqualTo(500_00));
            OrderLapsed lapsed = sim.PendingEvents.OfType<OrderLapsed>().Single();
            Assert.That((lapsed.OrderId, lapsed.Left), Is.EqualTo((o.Id, 50_000L)));
            Assert.That(sim.State.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(0));
            Assert.That(sim.State.Ledger.Cash - before, Is.GreaterThanOrEqualTo(500_00 - 1000_00), "paid (living costs may also move cash)");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.Execute(new DeliverOrder(Day + 6 * Hour, o.Id, 1_000)).Reason, Is.EqualTo(RejectReason.OrderClosed), "a lapsed order");
            Assert.That(sim.Execute(new DeliverOrder(Day + 6 * Hour, 999, 1_000)).Reason, Is.EqualTo(RejectReason.UnknownOrder));
        }

        // Spec 011 AC-10.
        [Test]
        public void HandDeliversFirst()
        {
            (Simulation, OrderExecuted) Run(SellRule rule, bool careless)
            {
                Simulation sim = Start(NoPests((f, j) => NoSpoilage(f, NonePosted(f, j))));
                Ok(sim, new SkipFirstSession(sim.State.Clock.SimTime));
                Hand h = SteadyHand(sim);
                if (careless) h.Trait1 = sim.Config.Game.People.IndexOfTrait("careless");
                long t = (sim.State.Clock.SimTime / Day + 1) * Day + 7 * Hour; // sow at 7:00: ripe within the day
                sim.AdvanceTo(t);
                Ok(sim, new Sow(t, HomeTile, "wheat", SeedSource.Certified, "none"));
                Ok(sim, new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(HomeTile, "wheat", SeedSource.Certified, "none", rule, rule == SellRule.Offer ? Fixed.One : Fixed.Zero)));
                long harvestAt = (sim.State.Farm.CropOn(HomeTile).ReadyAt + Hour - 1) / Hour * Hour;
                sim.AdvanceTo(harvestAt - 1);
                Place(sim, "wheat", 20_000, "40");
                sim.DrainEvents();
                sim.AdvanceTo(harvestAt);
                return (sim, sim.PendingEvents.OfType<OrderExecuted>().First(e => e.Action == OrderAction.Harvested));
            }

            foreach (SellRule rule in new[] { SellRule.Trader, SellRule.Offer })
            {
                (Simulation sim, OrderExecuted ex) = Run(rule, false);
                OrderDelivered d = sim.PendingEvents.OfType<OrderDelivered>().Single();
                Assert.That((d.Quantity, d.ByHand), Is.EqualTo((20_000L, true)), rule + ": the order first");
                Assert.That(ex.Quantity, Is.GreaterThan(20_000));
                int wheat = FarmTest.Good(sim, "wheat");
                if (rule == SellRule.Trader) Assert.That(sim.State.Market.TraderBought(wheat), Is.EqualTo(ex.Quantity - 20_000), "the rest to the trader");
                else Assert.That(sim.State.Market.OnOffer(wheat), Is.EqualTo(ex.Quantity - 20_000), "the rest on the stall");
            }
            (Simulation store, _) = Run(SellRule.Store, false);
            Assert.That(store.PendingEvents.OfType<OrderDelivered>(), Is.Empty, "STORE keeps it in store");
            (Simulation careless, _) = Run(SellRule.Store, true);
            Assert.That(careless.PendingEvents.OfType<OrderDelivered>().Single().ByHand, Is.True, "a careless hand delivers first too");
        }

        // Spec 011 AC-11.
        [Test]
        public void BestCrop_CountsOrderPrice()
        {
            Simulation sim = Skipped(FarmTest.AnySeason(NonePosted));
            sim.AdvanceTo(7 * Hour);
            Land.Tile tile = sim.State.Land.Tiles[HomeTile];
            FertiliserDef none = sim.Config.Game.Farm.Fertilisers.First(f => f.Good < 0);
            int usual = FarmRules.BestCrop(sim, tile, none);
            CropDef other = sim.Config.Game.Farm.Crops.First(c => c != sim.Config.Game.Farm.Crops[usual] && c.GrowthSeconds <= 15 * Hour && c.SowSeasons[0]);
            SupermarketOrder o = Place(sim, sim.Config.Game.Market.Goods[other.OutputGood].Id, 10_000, "1000");

            long grams = FarmRules.Estimate(sim, tile, other, none).HarvestGrams;
            long ready = sim.State.Clock.SimTime + other.GrowthSeconds;
            long q = System.Math.Min(grams, o.Left);
            Assert.That(Supermarket.HarvestValue(sim, other.OutputGood, grams, ready),
                Is.EqualTo(MarketRules.Value(sim, other.OutputGood, q, o.Price) + MarketRules.Value(sim, other.OutputGood, grams - q, MarketRules.Floor(sim, other.OutputGood))));
            Assert.That(sim.Config.Game.Farm.Crops[FarmRules.BestCrop(sim, tile, none)], Is.EqualTo(other), "the order's price wins");

            sim.AdvanceTo(Day + 30 * 60);
            Assert.That(Supermarket.HarvestValue(sim, other.OutputGood, grams, sim.State.Clock.SimTime + other.GrowthSeconds),
                Is.EqualTo(MarketRules.Value(sim, other.OutputGood, grams, MarketRules.Floor(sim, other.OutputGood))), "after midnight: the floor");
        }

        // Spec 011 AC-17 (orders' start).
        [Test]
        public void StartAfterFirstSession()
        {
            Simulation sim = NewFarm(game: Quiet());
            sim.AdvanceTo(6 * Hour);
            Assert.That(sim.PendingEvents.OfType<OrdersPosted>(), Is.Empty, "not during the first session");
            sim.State.Guidance.Step = sim.Config.Game.Onboarding.Steps.Count; // comeBack done
            sim.AdvanceTo(Day + 6 * Hour - 1);
            Assert.That(sim.PendingEvents.OfType<OrdersPosted>(), Is.Empty);
            sim.AdvanceTo(Day + 6 * Hour);
            Assert.That(sim.PendingEvents.OfType<OrdersPosted>().Count(), Is.EqualTo(1), "the first 6:00 after it");
        }
    }
}
