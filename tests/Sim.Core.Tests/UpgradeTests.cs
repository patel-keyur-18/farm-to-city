using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-152 (spec 024 §3.7, ADR 0131): making a kutcha family house pucca, a build with materials and masons; the rent follows at the renewal; builders do it too.</summary>
    public class UpgradeTests
    {
        private static Simulation Township()
        {
            Simulation sim = TownshipTests.Start(cohort: true);
            TreasuryTests.Township(sim);
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 5_000_000 * Coin), new Posting(Account.OpeningEquity, -5_000_000 * Coin));
            sim.DrainEvents();
            return sim;
        }

        /// <summary>A farm acre with clean water near (the pond is within reach), with a land value set to <paramref name="lv"/>.</summary>
        private static int WaterTile(Simulation sim, string lv = "1.20")
        {
            foreach (Tile t in sim.State.Land.Tiles)
                if (t.Terrain == Terrain.Farm && LandValueRules.HasWater(sim, t.Id) && sim.State.Land.Parcels[t.ParcelId].Owned)
                {
                    sim.State.Township.LandValues[t.Id] = Fixed.Parse(lv);
                    return t.Id;
                }
            Assert.Fail("no owned acre with water near");
            return -1;
        }

        private static Building House(Simulation sim, int tile, int owner = 0)
        {
            LandState land = sim.State.Land;
            var b = new Building { Id = land.NextBuildingId++, Def = sim.Config.Game.Population.House, TileId = tile, Cost = 6_000_000, Owner = owner };
            land.Buildings.Add(b);
            return b;
        }

        private static Household Family(Simulation sim, Building b, long income)
        {
            Household x = AddFamily(sim, b.Id, 30, 28);
            x.Income = income;
            x.LeaseStart = LeaseRules.Season(sim);
            x.LeaseRent = LeaseRules.MarketRentFor(sim, x);
            return x;
        }

        private static void Finish(Simulation sim, Building b)
        {
            for (int i = 0; i < 400 && !b.Pucca; i++) sim.AdvanceTo(sim.State.Clock.SimTime + 6 * Hour);
            Assert.That(b.Pucca, Is.True, "the upgrade is done");
        }

        // AC-10: offered at land value 1.15, with clean water near; the refusals say which.
        [Test]
        public void Offered_Needs_LandValue_And_Water()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            Assert.That(UpgradeRules.Check(sim, b).Why, Is.EqualTo(UpgradeWhy.Offered));
            sim.State.Township.LandValues[tile] = Fixed.Parse("1.14");
            UpgradeCheck low = UpgradeRules.Check(sim, b);
            Assert.That(low.Why, Is.EqualTo(UpgradeWhy.LandValue));
            Assert.That(low.Needs, Is.EqualTo(Fixed.Parse("1.15")));
            sim.State.Township.LandValues[tile] = Fixed.Parse("1.15");
            Assert.That(UpgradeRules.Check(sim, b).Offered, Is.True, "1.15 is enough");
            int dry = sim.State.Land.Tiles.First(t => t.Terrain == Terrain.Farm && !LandValueRules.HasWater(sim, t.Id)).Id;
            sim.State.Township.LandValues[dry] = Fixed.Parse("1.30");
            Assert.That(UpgradeRules.Check(sim, House(sim, dry)).Why, Is.EqualTo(UpgradeWhy.Water), "no clean water near");
        }

        // AC-10: the family must be able to pay the pucca rent (30% of its income); an empty house, and a hand's, need no such test.
        [Test]
        public void Offered_Needs_A_Family_That_Can_Pay()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            long pucca = UpgradeRules.PuccaRent(sim, tile);
            Assert.That(pucca, Is.GreaterThan(LeaseRules.MarketRent(sim, tile)), "a pucca house lets for more");
            Household x = Family(sim, b, pucca * 100 / 30 - 10 * Coin); // a little too poor
            Assert.That(UpgradeRules.Check(sim, b).Why, Is.EqualTo(UpgradeWhy.Rent));
            x.Income = pucca * 100 / 30 + 10 * Coin;
            Assert.That(UpgradeRules.Check(sim, b).Offered, Is.True);
            x.Income = 1;
            sim.State.Citizens.Households.Remove(x);
            Assert.That(UpgradeRules.Check(sim, b).Offered, Is.True, "empty: no rent to bear");
        }

        // AC-10: the order is paid at the start from your cash, takes the trader's materials and masons, the family stays; the house is pucca when it is done.
        [Test]
        public void Order_Is_A_Build()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            Household x = Family(sim, b, 1_000_000 * Coin);
            long cash = sim.State.Ledger.Cash, book = b.Cost;
            BuildQuote q = UpgradeRules.Quote(sim, BuildProject.Yours);
            Assert.That((q.Bricks, q.Cement, q.Timber, q.MasonDays), Is.EqualTo((8_000L, 80L, 24L, 55)));
            Ok(sim, new UpgradeHouse(sim.State.Clock.SimTime, b.Id));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash - q.Total), "paid at the start");
            Assert.That(b.Cost, Is.EqualTo(book + q.Total), "capitalised");
            Assert.That(b.Pucca, Is.False);
            BuildProject p = UpgradeRules.Underway(sim, b.Id);
            Assert.That(p, Is.Not.Null);
            Assert.That(p.Working, Is.False, "8,000 bricks are more than the trader's day: it waits");
            Assert.That(UpgradeRules.Check(sim, b).Why, Is.EqualTo(UpgradeWhy.Underway));
            Assert.That(sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.NotOffered), "once");
            Assert.That(x.HomeId, Is.EqualTo(b.Id), "the family stays");
            Finish(sim, b);
            Assert.That(sim.PendingEvents.OfType<HouseUpgraded>().Single().BuildingId, Is.EqualTo(b.Id));
            Assert.That(UpgradeRules.Underway(sim, b.Id), Is.Null);
            Assert.That(UpgradeRules.Check(sim, b).Why, Is.EqualTo(UpgradeWhy.Pucca));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }

        // AC-10: the rent follows the house: the market rent is pucca's now, the lease keeps its rent until it ends, then steps up by the cap.
        [Test]
        public void Rent_Follows_At_The_Renewal()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            Household x = Family(sim, b, 1_000_000 * Coin);
            long kutcha = x.LeaseRent;
            Assert.That(LeaseRules.MarketRentFor(sim, x), Is.EqualTo(kutcha));
            Ok(sim, new UpgradeHouse(sim.State.Clock.SimTime, b.Id));
            Finish(sim, b);
            Assert.That(x.LeaseRent, Is.EqualTo(kutcha), "the lease keeps its rent");
            Assert.That(LeaseRules.MarketRentFor(sim, x), Is.EqualTo(UpgradeRules.PuccaRent(sim, tile)));
            LeaseOptions o = LeaseRules.Options(sim, x);
            Assert.That(o.Allowed, Is.GreaterThan(kutcha).And.LessThanOrEqualTo(kutcha + kutcha / 10 + Coin), "a rise of at most the cap");
            Assert.That(o.Allowed, Is.LessThan(o.Market), "the rest comes at later renewals");
        }

        // AC-10: the rejections.
        [Test]
        public void Rejections()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            Assert.That(sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, 9999)).Reason, Is.EqualTo(RejectReason.UnknownBuilding));
            Building builders = House(sim, tile, 1);
            Assert.That(sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, builders.Id)).Reason, Is.EqualTo(RejectReason.NotOffered), "a builder's house is the builder's");
            sim.State.Township.LandValues[tile] = Fixed.Parse("1.0");
            Assert.That(sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.NotOffered));
            sim.State.Township.LandValues[tile] = Fixed.Parse("1.2");
            long cash = sim.State.Ledger.Cash;
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -(cash - 1_000 * Coin)), new Posting(Account.OpeningEquity, cash - 1_000 * Coin));
            Assert.That(sim.Execute(new UpgradeHouse(sim.State.Clock.SimTime, b.Id)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(UpgradeRules.Underway(sim, b.Id), Is.Null, "nothing moved");
        }

        // AC-10: builders upgrade one of their homes a close, where it is offered; their upgrade draws bricks after yours.
        [Test]
        public void Builders_Upgrade_One_A_Close()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building a = House(sim, tile, 1), c = House(sim, tile, 1);
            UpgradeRules.Close(sim);
            Assert.That(sim.State.Township.Projects.Count(p => p.Upgrade && p.Owner == BuildProject.Builders), Is.EqualTo(1));
            UpgradeRules.Close(sim);
            Assert.That(sim.State.Township.Projects.Count(p => p.Upgrade), Is.EqualTo(1), "one at a time");
            Finish(sim, a);
            Assert.That(UpgradeRules.BuildersPucca(sim), Is.EqualTo(1));
            Assert.That(c.Pucca, Is.False);
            sim.State.Township.LandValues[tile] = Fixed.Parse("1.20"); // a close read the land again while the masons worked
            UpgradeRules.Close(sim);
            Assert.That(UpgradeRules.Underway(sim, c.Id), Is.Not.Null, "the next close, the next house");
            Assert.That(a.Cost, Is.EqualTo(6_000_000), "builders' books are theirs: nothing on yours");
        }

        // AC-10: an acre taken in foreclosure takes its upgrade with it.
        [Test]
        public void Seized_House_Drops_Its_Upgrade()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            Ok(sim, new UpgradeHouse(sim.State.Clock.SimTime, b.Id));
            HouseRules.Seize(sim, sim.State.Land.Tiles[tile].ParcelId, sim.Period);
            Assert.That(UpgradeRules.Underway(sim, b.Id), Is.Null);
            Assert.That(sim.State.Township.Projects.Any(p => p.Upgrade), Is.False);
        }

        // AC-10: the pucca flag is saved from schema 21, and the town reads back the same.
        [Test]
        public void Pucca_Survives_A_Save()
        {
            Simulation sim = Township();
            int tile = WaterTile(sim);
            Building b = House(sim, tile);
            Ok(sim, new UpgradeHouse(sim.State.Clock.SimTime, b.Id));
            Finish(sim, b);
            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, 0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.Land.Buildings.Find(x => x.Id == b.Id).Pucca, Is.True);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
