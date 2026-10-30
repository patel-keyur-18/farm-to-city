using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Livestock;
using Sim.Core.Market;
using Sim.Core.Definitions;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 012 §3.8b: green fodder fields, cut every morning.</summary>
    public class FodderTests
    {
        private static long StoreTotal(Simulation sim) { return sim.State.Market.Stock.Sum(); }

        // AC-8: cut daily from 6 h after sowing, through its cut seasons, ended at the close of the last; never in the store; spare fodder
        // (all of it with no animals) sold standing at 1 a kg; the seed is paid once.
        [Test]
        public void Berseem_CutDaily()
        {
            Simulation sim = LivestockKit.WithShed(Game(NoSpoilage)); // any-season content: berseem sown in Spring, cut in Spring only
            long t = sim.State.Clock.SimTime, cash = sim.State.Ledger.Cash;
            Ok(sim, new Sow(t, HomeTile, "berseem", SeedSource.Certified, "none"));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Money.Value(10_000, Maths.Fixed.FromInt(180), 1000)), "10 kg of seed, once");
            Crop c = sim.State.Farm.CropOn(HomeTile);
            Assert.That(c.ReadyAt, Is.EqualTo(sim.Clock.SecondsPerSeason), "it stands till the end of Spring");
            Assert.That(c.PestJobId, Is.EqualTo(0), "no pest check");

            long stored = StoreTotal(sim);
            LivestockKit.ToMorning(sim);
            FeedingReport r = LivestockKit.L(sim).LastFeeding;
            Assert.That(r.GreenSold, Is.GreaterThan(0), "18 hours of cutting since the first cut was due");
            Assert.That(r.GreenSold, Is.LessThan(3_000_000 * 16 / 10), "at most 18 h of 4 t a day × the 1.6 cap");
            Assert.That(r.GreenSoldAmount, Is.EqualTo(Money.Value(r.GreenSold, Maths.Fixed.One, 1000)), "1 coin a kg");
            long sold = -sim.State.Ledger.Entries.Where(e => e.DescriptionKey == "ledger.fodderSold").SelectMany(e => e.Postings).Where(p => p.Account == Account.CropSales).Sum(p => p.Amount);
            Assert.That(sold, Is.EqualTo(r.GreenSoldAmount), "booked as crop sales");
            Assert.That(sim.State.Finance.Sales.This.Single(x => x.To == SoldTo.Neighbours).Amount, Is.EqualTo(sold), "in the sales record as green fodder (spec 019 §3.9)");
            Assert.That(StoreTotal(sim) - stored, Is.LessThan(r.GreenSold), "never in the store (only the compost and nothing else moves)");

            // With a buffalo, the cut feeds her first and only the rest is sold.
            LivestockKit.Buy(sim);
            LivestockKit.ToMorning(sim);
            r = LivestockKit.L(sim).LastFeeding;
            Assert.That(r.GreenOwn, Is.GreaterThan(0));
            Assert.That(r.GreenOwn, Is.EqualTo(System.Math.Min(r.GreenNeed - r.GreenCommons, r.GreenOwn + r.GreenSold)));
            Assert.That(r.GreenBought, Is.EqualTo(0), "a whole acre feeds one buffalo with plenty to spare");

            sim.DrainEvents();
            sim.AdvanceTo(sim.Clock.SecondsPerSeason);
            Assert.That(sim.PendingEvents.OfType<FodderEnded>().Single().TileId, Is.EqualTo(HomeTile));
            Assert.That(sim.State.Farm.CropOn(HomeTile), Is.Null);
            Assert.That(sim.State.Land.Tiles[HomeTile].Occupied, Is.False);
        }

        [Test]
        public void Fodder_NeverAnOrderOrTheHandsPick()
        {
            Simulation sim = Start();
            Assert.That(sim.Config.Game.Farm.Crops[FarmRules.BestCrop(sim, sim.State.Land.Tiles[HomeTile], sim.Config.Game.Farm.Fertilisers[0])].Fodder, Is.False);
        }

        // A fodder order keeps green fodder all year: sorghum in Summer, berseem in Winter, the hand's pick in between.
        [Test]
        public void FodderOrder_AllYear()
        {
            Simulation sim = NewFarm(game: RichContent()); // the real crop calendar
            LivestockKit.Hamlet(sim);
            Assert.That(sim.Execute(new Sow(0, HomeTile + 1, "berseem", SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.OutOfSeason));
            Hand h = HireHand(sim);
            Ok(sim, new SetStandingOrder(sim.State.Clock.SimTime, h.Id, OrderSpec.KeepCrop(HomeTile, "berseem", SeedSource.Certified, "none", SellRule.Trader)));
            long s = sim.Clock.SecondsPerSeason;
            sim.AdvanceTo(s + 3 * Hour); // Summer
            Assert.That(CropId(sim), Is.EqualTo("sorghum"));
            sim.AdvanceTo(3 * s + 3 * Hour); // Winter: the sorghum stood through the Monsoon
            Assert.That(CropId(sim), Is.EqualTo("berseem"));
        }

        private static GameDefs RichContent() { return NoInflation((f, j) => f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000") : j); }

        private static string CropId(Simulation sim)
        {
            Crop c = sim.State.Farm.CropOn(HomeTile);
            return c == null ? null : sim.Config.Game.Farm.Crops[c.Def].Id;
        }
    }
}
