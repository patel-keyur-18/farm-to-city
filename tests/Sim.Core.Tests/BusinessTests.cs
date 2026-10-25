using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 016: founding a business and its books, the mill's hour, running it yourself.</summary>
    public class BusinessTests
    {
        internal static void Hamlet(Simulation sim)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
        }

        internal static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        /// <summary>A rich Hamlet town with a flour mill founded on <see cref="PeopleKit.HomeTile"/> (capital 1,50,000), not yet built.</summary>
        internal static Business.Business Mill(Simulation sim, bool runYourself = false, long capital = 150_000_00)
        {
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "flour_mill", HomeTile, capital, runYourself));
            return sim.State.Businesses.Businesses.Last();
        }

        internal static void GiveWheat(Simulation sim, long grams)
        {
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            sim.State.Market.Stock[wheat] += grams;
        }

        // AC-1: founding moves the founder's cash into the business (1800 / Owner's capital); the business buys and capitalises its
        // building and equipment; refusals in order; both ledgers reconcile.
        [Test]
        public void Founding_Books()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.EraLocked), "the Hamlet's");
            Hamlet(sim);
            CashTo(sim, 500_000_00);
            Assert.That(sim.Execute(new FoundBusiness(t, "bakery", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.UnknownBusiness));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", QuartersTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.TerrainNotAllowed));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, 50_000_00, false)).Reason, Is.EqualTo(RejectReason.CapitalTooLow), "the building, equipment and a season's wage");
            Ok(sim, new Sow(t, HomeTile + 1, "radish", Farm.SeedSource.Certified, "none"));
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile + 1, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.TileOccupied));

            long cash = sim.State.Ledger.Cash;
            Ok(sim, new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            long cost = MarketRules.Indexed(sim, 100_000_00);
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(150_000_00));
            Assert.That(sim.State.Ledger.Balance(Account.InvestmentInBusinesses), Is.EqualTo(150_000_00));
            Assert.That(b.Ledger.Balance(Account.OwnersCapital), Is.EqualTo(-150_000_00));
            Assert.That(b.Ledger.Balance(Account.Buildings) + b.Ledger.Balance(Account.Equipment), Is.EqualTo(cost), "capitalised");
            Assert.That(b.Ledger.Cash, Is.EqualTo(150_000_00 - cost));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
            Assert.That(sim.Execute(new Sow(t, HomeTile, "radish", Farm.SeedSource.Certified, "none")).Reason, Is.EqualTo(RejectReason.TileOccupied), "the acre stops farming");
            Assert.That(sim.Execute(new BuildFamilyHouse(t, HomeTile)).Reason, Is.EqualTo(RejectReason.TileOccupied), "no house on a business's acre");
            Assert.That(sim.Execute(new FoundBusiness(t, "flour_mill", HomeTile, 150_000_00, false)).Reason, Is.EqualTo(RejectReason.NoRoom), "one business an acre");
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Building));
            sim.AdvanceTo(b.ReadyAt);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open));
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.Neighbour), "nobody in town: a daily worker from a neighbouring village");
            Assert.That(b.Ledger.Balance(Account.HiredLabour), Is.EqualTo(PeopleRules.RestOfSeason(sim, MarketRules.Indexed(sim, 27_000_00))).Within(100), "the rest of the season's wage, upfront");
        }

        // AC-3: the founder's wheat goes to the mill at today's market price: Crop sales on the farm, Stock on the mill.
        [Test]
        public void InternalSale()
        {
            Simulation sim = Start();
            Business.Business b = Mill(sim);
            GiveWheat(sim, 3_000_000); // the household eats from the store too
            sim.AdvanceTo(b.ReadyAt);
            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400 + 8 * 3600);
            long sales = sim.State.Ledger.Balance(Account.CropSales), cash = sim.State.Ledger.Cash;
            int wheat = sim.Config.Game.Market.IndexOf("wheat");
            Fixed price = MarketRules.Floor(sim, wheat);
            sim.AdvanceTo(day * 86_400 + 18 * 3600);
            long value = MarketRules.Value(sim, wheat, 200_000, price);
            Assert.That(b.OwnToday, Is.EqualTo(200_000), "200 kg a day by default");
            Assert.That(sales - sim.State.Ledger.Balance(Account.CropSales), Is.EqualTo(value).Within(value / 50), "Crop sales on the farm (prices move a little in a day)");
            Assert.That(sim.State.Ledger.Cash - cash, Is.EqualTo(value).Within(value / 50));
            Assert.That((sim.State.Ledger.Reconcile(), b.Ledger.Reconcile()), Is.EqualTo(((string)null, (string)null)));
        }

        // AC-11: run by you, the mill works only in your hours there, which come off your farm work; no wage is paid.
        [Test]
        public void RunItYourself()
        {
            Simulation sim = Start();
            Fixed before = Farm.FounderRules.LabourRate(sim);
            Business.Business b = Mill(sim, runYourself: true, capital: 120_000_00);
            sim.AdvanceTo(b.ReadyAt);
            Assert.That(b.Worker, Is.EqualTo(WorkerKind.Founder));
            Fixed hours = sim.State.Farm.Founder.TodayHours;
            Fixed cut = (hours < Fixed.FromInt(10) ? hours : Fixed.FromInt(10)) * Farm.FounderRules.Nutrition(sim);
            Assert.That(before - Farm.FounderRules.LabourRate(sim), Is.EqualTo(cut), "a 10-hour day at the mill takes the founder's day");
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 12, b.Fee, b.OwnPerDay, false));
            Fixed four = Fixed.FromInt(4) * Farm.FounderRules.Nutrition(sim);
            Assert.That(before - Farm.FounderRules.LabourRate(sim), Is.EqualTo(four < cut ? four : cut), "4 hours open, 4 hours off the farm");
            sim.AdvanceTo(NextClose(sim) + 3600);
            Assert.That(b.Ledger.Balance(Account.HiredLabour), Is.EqualTo(0), "no wage");
            Assert.That(b.Ledger.Balance(Account.ServiceIncome), Is.LessThan(0), "it ground for the area");
        }

        // AC-9: the same seed and commands give the same town with a business, in one chunk or many, live or away.
        [Test]
        public void ChunkingInvariance()
        {
            ulong Run(System.Action<Simulation, long> advance)
            {
                Simulation sim = Start(seed: 5);
                Business.Business b = Mill(sim);
                GiveWheat(sim, 2_000_000);
                long end = sim.State.Clock.SimTime + 6 * 86_400;
                advance(sim, end);
                Assert.That(b.Ledger.Balance(Account.Sales), Is.LessThan(0), "it sold atta");
                return sim.State.ComputeHash();
            }
            ulong one = Run((sim, end) => sim.AdvanceTo(end));
            ulong many = Run((sim, end) => { while (sim.State.Clock.SimTime < end) sim.AdvanceTo(System.Math.Min(end, sim.State.Clock.SimTime + 7_777)); });
            ulong away = Run((sim, end) => { long d = end - sim.State.Clock.SimTime; sim.AdvanceAway(d, d, false); });
            Assert.That(many, Is.EqualTo(one));
            Assert.That(away, Is.EqualTo(one));

            Simulation s = CreditKit.NewSim(cash: 500_000);
            Hamlet(s);
            Ok(s, new FoundBusiness(s.State.Clock.SimTime, "flour_mill", HomeTile, 150_000_00, false));
            s.AdvanceTo(s.State.Clock.SimTime + 3 * 86_400);
            LoadResult r = SaveTests.RoundTrip(s);
            Assert.That(r.Error, Is.EqualTo(SaveError.None));
            Assert.That(r.State.ComputeHash(), Is.EqualTo(s.State.ComputeHash()), "a business round-trips");
        }
    }

    /// <summary>Spec 016 §3.4, §3.5: the mill's rate, order of work and yields.</summary>
    public class ProductionTests
    {
        // AC-2: open 8–18 it grinds at most 380 kg a real hour, the area's grain first (catchment + town × the grain a person), then your
        // wheat up to the daily amount; 0.97 atta and 0.03 bran; grain not ground in the day is lost; bran goes to the feed room first.
        [Test]
        public void Mill_Rate_Order_Yield()
        {
            Simulation sim = Start();
            Business.Business b = BusinessTests.Mill(sim);
            BusinessTests.GiveWheat(sim, 5_000_000);
            Ok(sim, new BuildShed(sim.State.Clock.SimTime));
            sim.AdvanceTo(System.Math.Max(b.ReadyAt, sim.State.Clock.SimTime + 25 * 3600));
            Ok(sim, new BuyAnimal(sim.State.Clock.SimTime, "buffalo"));
            int feed = sim.Config.Game.Livestock.Feed, atta = sim.Config.Game.Market.IndexOf("atta"), bran = sim.Config.Game.Market.IndexOf("bran");
            long day = sim.State.Clock.SimTime / 86_400 + 1;
            sim.AdvanceTo(day * 86_400);
            sim.DrainEvents();
            sim.AdvanceTo(day * 86_400 + 7 * 3600); // after the animals' 6:00 feed
            long feedBefore = sim.State.Market.Stock[feed];
            sim.AdvanceTo(day * 86_400 + 19 * 3600);
            long attaSold = sim.State.Market.TraderBoughtToday[atta], branSold = sim.State.Market.TraderBoughtToday[bran], toFeed = sim.State.Market.Stock[feed] - feedBefore;
            sim.AdvanceTo((day + 1) * 86_400);
            var worked = sim.DrainEvents().OfType<BusinessWorked>().ToList();
            long area = BusinessRules.AreaGramsPerDay(sim);
            Assert.That(worked.Select(w => (w.SimTime % 86_400) / 3600 - 1), Is.EqualTo(Enumerable.Range(8, 10)), "the hours 8:00–18:00 only");
            Assert.That(worked.All(w => w.AreaGrams + w.OwnGrams <= 380_000), Is.True, "380 kg a real hour at most");
            Assert.That(worked.Sum(w => w.AreaGrams), Is.EqualTo(area / 10 * 10), "the area's grain first");
            Assert.That(worked.Sum(w => w.OwnGrams), Is.EqualTo(200_000), "then 200 kg of your wheat");
            Assert.That(worked.First().OwnGrams, Is.EqualTo(System.Math.Min(200_000, 380_000 - area / 10)), "your wheat fills what the area leaves");
            Assert.That(b.Stock[atta] + attaSold, Is.EqualTo(194_000).Within(10), "0.97 atta (rounded an hour at a time)");
            Assert.That(toFeed, Is.GreaterThan(0), "bran to the feed room first");
            Assert.That(b.Stock[bran] + branSold + toFeed, Is.EqualTo(6_000).Within(10), "0.03 bran");

            // Open only 8–12: the area's afternoon grain goes to the next village.
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 12, b.Fee, b.OwnPerDay, false));
            sim.AdvanceTo((day + 2) * 86_400);
            Assert.That(b.AreaToday, Is.EqualTo(area / 10 * 4), "4 hours of it");

            // A fee more than 1 coin above the next village's: the area goes there.
            Ok(sim, new SetBusiness(sim.State.Clock.SimTime, b.Id, 8, 18, Fixed.FromInt(6), b.OwnPerDay, false));
            sim.AdvanceTo((day + 3) * 86_400);
            Assert.That(b.AreaToday, Is.EqualTo(0));
            Assert.That(b.OwnToday, Is.EqualTo(200_000), "your own wheat still");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }
    }
}
