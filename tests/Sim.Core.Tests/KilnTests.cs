using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Time;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-243 (spec 025 §3.3, ADR 0129): the brick kiln fires bricks, sells them to your builds at cost and to the trader, stops in the Monsoon and smokes.</summary>
    public class KilnTests
    {
        private static Simulation Township(int families = 2)
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            if (families > 0) TownshipTests.Families(sim, families);
            TreasuryTests.Township(sim);
            return sim;
        }

        private static int FreeTile(Simulation sim, int not = -1)
        {
            foreach (Tile t in sim.State.Land.Tiles) if (t.Id != not && BusinessRules.Place(sim, t.Id) == RejectReason.None && HouseRules.Place(sim, t.Id) == RejectReason.None) return t.Id;
            Assert.Fail("no tile for a kiln");
            return -1;
        }

        /// <summary>A kiln founded, built and staffed with a hand; the clock on the first morning it is open.</summary>
        private static Business.Business Kiln(Simulation sim, int tile = -1)
        {
            long give = 2_000_000 * Coin - sim.State.Ledger.Cash; // posted straight to the books: a test's cash is no command in the save
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Finance.Posting(Finance.Account.Cash, give), new Finance.Posting(Finance.Account.Sales, -give));
            Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "brick_kiln", tile < 0 ? FreeTile(sim) : tile, 400_000 * Coin, false));
            Business.Business b = sim.State.Businesses.Businesses.Last();
            BusinessTests.Staff(sim, b);
            sim.AdvanceTo(b.ReadyAt + 60);
            Assert.That(b.Status, Is.EqualTo(BusinessStatus.Open));
            return b;
        }

        /// <summary>Moves the clock to the start of a day of <paramref name="season"/> (the next one), at 0:00.</summary>
        private static void ToSeason(Simulation sim, Season season)
        {
            while (Calendar.SeasonAt(sim.State.Clock.SimTime + 1, sim.Clock) != season) sim.AdvanceTo((sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason);
        }

        private static void ToHour(Simulation sim, int hour)
        {
            long day = sim.State.Clock.SimTime / Day;
            long t = day * Day + hour * Hour;
            if (t <= sim.State.Clock.SimTime) t += Day;
            sim.AdvanceTo(t);
        }

        // AC-7: the kiln's type loads with its numbers; it is a Village business.
        [Test]
        public void Content()
        {
            Simulation sim = Township();
            BusinessTypeDef t = BusinessRules.Defs(sim).Types.First(x => x.Id == "brick_kiln");
            Assert.That(t.IsKiln && !t.ByRecipe && !t.IsShop, Is.True);
            Assert.That((t.Kiln.BricksPerHour, t.Kiln.YardMax, t.Kiln.TraderPerDay), Is.EqualTo((500L, 10_000L, 5_000L)));
            Assert.That(BusinessRules.Cost(sim, t), Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 300_000 * Coin)), "kiln and yard 3,00,000");
            Assert.That(sim.Config.Game.Township.Smoke.Source("kiln").Reach, Is.EqualTo(150));
        }

        // The review (2026-11-20): the kiln's plan pays back in about 7 seasons hired and about 4 run by you, counting the Monsoon (nothing fired, the
        // wage, wear and repairs still come): the targets the bots would have set.
        [Test]
        public void Plan_Payback_CountsTheMonsoon()
        {
            Simulation sim = Township();
            BusinessTypeDef t = BusinessRules.Defs(sim).Types.First(x => x.Id == "brick_kiln");
            foreach (bool you in new[] { false, true })
            {
                BusinessRules.Projection p = BusinessRules.Projected(sim, t, you, 0, Fixed.Zero);
                long dead = p.Wage + p.Repairs + p.Depreciation; // a Monsoon season
                double average = (3.0 * p.Profit - dead) / 4.0 / Coin, payback = (p.Cost / (double)Coin) / average;
                TestContext.Out.WriteLine((you ? "run by you" : "hired") + ": a firing season " + p.Profit / Coin + ", a year's average " + (long)average + ", payback " + payback.ToString("0.0") + " seasons");
                if (you) Assert.That(payback, Is.InRange(2.0, 4.5), "run by you");
                else Assert.That(payback, Is.InRange(4.0, 8.0), "hired");
            }
        }

        // AC-7: an open hour fires the hand's rate of bricks into the yard, fuel paid as it burns and kept in the bricks' cost; the books reconcile.
        [Test]
        public void Fires_Outside_The_Monsoon()
        {
            Simulation sim = Township();
            ToSeason(sim, Season.Winter);
            Business.Business b = Kiln(sim);
            ToHour(sim, 6);
            long cash = b.Ledger.Cash;
            ToHour(sim, 9);
            Assert.That(b.Yard, Is.EqualTo(500), "one open hour (8:00 to 9:00) of the hand at skill 50");
            Assert.That(b.YardCost, Is.GreaterThan(0));
            Assert.That(b.YardCost, Is.EqualTo(KilnRules.FuelCost(sim, b, KilnRules.Def(sim, b), 500)), "the fuel in the bricks: 0.4 kg a brick");
            Assert.That(b.Ledger.Cash, Is.LessThan(cash));
            ToHour(sim, 19);
            Assert.That(b.KilnMade, Is.EqualTo(5_000), "ten open hours");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-7: wet clay can't be fired: nothing is made in the Monsoon, the wage is still paid.
        [Test]
        public void Stops_In_The_Monsoon()
        {
            Simulation sim = Township();
            ToSeason(sim, Season.Monsoon);
            Business.Business b = Kiln(sim);
            Assert.That(KilnRules.Fires(sim), Is.False);
            ToHour(sim, 19);
            Assert.That(Calendar.SeasonAt(sim.State.Clock.SimTime, sim.Clock), Is.EqualTo(Season.Monsoon), "still the Monsoon");
            Assert.That(b.Yard + b.KilnMade, Is.EqualTo(0), "nothing fired in a whole open day");
        }

        // AC-7: each morning the kiln sells the trader up to what he takes at his price less a tenth, and his stock for the whole town grows by it.
        [Test]
        public void Trader_Buys_And_His_Stock_Grows()
        {
            Simulation sim = Township();
            ToSeason(sim, Season.Winter);
            Business.Business b = Kiln(sim);
            ToHour(sim, 19);
            ToHour(sim, 19); // a second day's firing: the yard holds what the first day made and the trader took none of it yet
            long yard = b.Yard;
            Assert.That(yard, Is.GreaterThan(0));
            long cash = b.Ledger.Cash;
            ToHour(sim, 7); // the trader's morning (6:00)
            long sold = b.KilnSold;
            Assert.That(sold, Is.EqualTo(System.Math.Min(yard, 5_000)));
            Assert.That(b.Ledger.Cash - cash, Is.EqualTo(KilnRules.PaysFor(sim, KilnRules.Def(sim, b), sold)).Within(2), "his price less a tenth");
            Assert.That(ConstructionRules.KilnToday(sim), Is.EqualTo(sold));
            Assert.That(ConstructionRules.Left(sim, 0), Is.EqualTo(ConstructionRules.Def(sim).DailyBricks + sold - sim.State.Township.TakenBricks), "his stock is his own 2,000 and the kiln's");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
        }

        // AC-7: your own build takes bricks from your yard first, at what they cost the kiln: cheaper than the trader's, no profit for the kiln.
        [Test]
        public void Own_Builds_Take_The_Yard_At_Cost()
        {
            Simulation sim = Township();
            ToSeason(sim, Season.Winter);
            Business.Business b = Kiln(sim);
            ToHour(sim, 19);
            long yard = b.Yard;
            Assert.That(yard, Is.GreaterThanOrEqualTo(2400));
            BuildQuote with = ConstructionRules.Quote(sim, "family_house", BuildProject.Yours, 1);
            Assert.That(with.KilnBricks, Is.EqualTo(2400));
            Simulation plain = Township();
            Assert.That(with.Materials, Is.LessThan(ConstructionRules.Quote(plain, "family_house", BuildProject.Yours, 1).Materials), "bricks at cost, not at the trader's price");
            long cost = KilnRules.CostOf(sim, 2400), cash = b.Ledger.Cash, mine = sim.State.Ledger.Cash;
            Ok(sim, new BuildFamilyHouse(sim.State.Clock.SimTime, FreeTile(sim, b.TileId)));
            Assert.That(b.Yard, Is.EqualTo(yard - 2400));
            Assert.That(b.KilnToBuilds, Is.EqualTo(2400));
            Assert.That(b.Ledger.Cash - cash, Is.EqualTo(cost), "your kiln is paid what the bricks cost it");
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(mine - (with.Materials + with.MasonPay)).Or.EqualTo(mine - with.Total));
            Assert.That(ConstructionRules.Projects(sim).Single(p => p.Owner == BuildProject.Yours).Bricks, Is.EqualTo(2400), "all its bricks in hand");
            Assert.That(b.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-7: smoke takes 0.04 of land value from the acre and the four beside it and 3% of health from the homes there; none farther away.
        [Test]
        public void Smoke()
        {
            Simulation sim = Township();
            Business.Business b = Kiln(sim);
            MapDef map = sim.Config.Game.Map;
            int x = map.X(b.TileId), y = map.Y(b.TileId);
            int beside = -1, far = -1;
            foreach (Tile t in sim.State.Land.Tiles)
            {
                int d = System.Math.Abs(map.X(t.Id) - x) + System.Math.Abs(map.Y(t.Id) - y);
                if (d == 1 && beside < 0) beside = t.Id;
                if (d >= 3 && far < 0) far = t.Id;
            }
            Assert.That(SmokeRules.LandValue(sim, b.TileId, out int n), Is.EqualTo(Fixed.Parse("0.04")));
            Assert.That(n, Is.EqualTo(1));
            Assert.That(SmokeRules.LandValue(sim, beside, out _), Is.EqualTo(Fixed.Parse("0.04")));
            Assert.That(SmokeRules.LandValue(sim, far, out int none), Is.EqualTo(Fixed.Zero));
            Assert.That(none, Is.EqualTo(0));
            Assert.That(LandValueRules.Parts(sim, beside).Any(p => p.Key == "smoke" && p.Value == Fixed.Parse("-0.04")), Is.True);
            Assert.That(LandValueRules.Parts(sim, far).Any(p => p.Key == "smoke"), Is.False);
        }

        // The yard, the day's counts and the trader's sales survive a save.
        [Test]
        public void Saved()
        {
            Simulation sim = Township();
            ToSeason(sim, Season.Winter);
            Business.Business b = Kiln(sim);
            ToHour(sim, 19);
            ToHour(sim, 7);
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Business.Business back = r.State.Businesses.Businesses.Last();
            Assert.That((back.Yard, back.YardCost, back.KilnSold, back.KilnMade), Is.EqualTo((b.Yard, b.YardCost, b.KilnSold, b.KilnMade)));
            Assert.That(r.State.Township.KilnBricks, Is.EqualTo(sim.State.Township.KilnBricks));
        }
    }
}
