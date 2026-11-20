using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Scheduling;
using Sim.Core.Time;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-149 (spec 026 §3.1, ADR 0127): one festival a season, its cost and payer, the day's lift and the shops' rise, the hall's caretaker, the record.</summary>
    public class FestivalTests
    {
        private static Simulation Township()
        {
            Simulation sim = TownLifeTests.Township(families: 12);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 5_000_000 * Coin), new Posting(Account.GiftsFromFounder, -5_000_000 * Coin));
            sim.DrainEvents();
            return sim;
        }

        private static CommandResult Hold(Simulation sim, int venue = FestivalDef.Square, int payer = FestivalDef.Town, int days = 1) { return sim.Execute(new ScheduleFestival(sim.State.Clock.SimTime, venue, payer, days)); }

        private static void StaffHall(Simulation sim)
        {
            CivicPost p = CivicRules.Post(sim, CivicDef.Hall);
            p.Built = true;
            p.Status = PostStatus.InPost;
        }

        // AC-1: one a season, costing perPerson × people × the index, from the payer's cash only; a gift is outside farm profit; nothing flows to you.
        [Test]
        public void Cost_Payer_Wall()
        {
            Simulation sim = Township();
            FestivalDef d = FestivalRules.Def(sim);
            int kind = FestivalRules.KindNow(sim);
            int people = FestivalRules.People(sim);
            Assert.That(people, Is.GreaterThan(0));
            long cost = FestivalRules.Cost(sim, kind);
            Assert.That(cost, Is.EqualTo(Money.MulRate(d.Kinds[kind].PerPerson * Coin * people, sim.State.Market.PriceIndex)));

            long town = sim.State.Treasury.Cash, mine = sim.State.Ledger.Cash;
            Assert.That(Hold(sim).Accepted, Is.True);
            Assert.That(sim.State.Treasury.Cash, Is.EqualTo(town - cost), "the treasury paid");
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(mine), "not you");
            Assert.That(sim.State.Treasury.Ledger.Balance(Account.Festivals), Is.EqualTo(cost));
            Assert.That(Hold(sim).Reason, Is.EqualTo(RejectReason.FestivalThisSeason), "one a season");
            Assert.That(sim.PendingEvents.OfType<FestivalScheduled>().Single().Cost, Is.EqualTo(cost));

            // Your gift (next season): your cash, booked to Gifts to the village; the treasury's is untouched.
            sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            long town2 = sim.State.Treasury.Cash, mine2 = sim.State.Ledger.Cash, gifts = sim.State.Ledger.Balance(Account.GiftsToVillage);
            long cost2 = FestivalRules.Cost(sim, FestivalRules.KindNow(sim));
            Assert.That(FestivalRules.KindNow(sim), Is.Not.EqualTo(kind), "the kind follows the season");
            Assert.That(Hold(sim, payer: FestivalDef.You).Accepted, Is.True);
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(mine2 - cost2));
            Assert.That(sim.State.Ledger.Balance(Account.GiftsToVillage), Is.EqualTo(gifts + cost2));
            Assert.That(sim.State.Treasury.Cash, Is.EqualTo(town2), "the town paid nothing");

            // The wall: the treasury never goes below zero, and your cash never does.
            sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason);
            long all = sim.State.Treasury.Cash;
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Festivals, all - 1), new Posting(Account.Cash, -(all - 1)));
            Assert.That(Hold(sim).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(sim.State.Treasury.Cash, Is.GreaterThanOrEqualTo(0));
            Assert.That(Hold(sim, days: 4).Reason, Is.EqualTo(RejectReason.InvalidArgument), "1 to 3 days ahead");
            Assert.That(Hold(sim, days: 0).Reason, Is.EqualTo(RejectReason.InvalidArgument));
        }

        // AC-2: on the day households in reach get +8 and the rest +3, the shops' demand rises; the next day neither.
        [Test]
        public void Day_Lift_Shops()
        {
            Simulation sim = Township();
            FestivalDef d = FestivalRules.Def(sim);
            Assert.That(Hold(sim, days: 1).Accepted, Is.True);
            Household x = TownLifeRules.Yours(sim)[0];
            Assert.That(FestivalRules.IsDay(sim), Is.False, "scheduled for tomorrow");
            Assert.That(FestivalRules.Lift(sim, x), Is.EqualTo(0));
            long plain = ShopRules.DemandThisHour(sim, ShopKind.Chai, -1, ShopGroup.Village, 12);

            long day = FestivalRules.Today(sim) + 1;
            sim.AdvanceTo(day * Day + 2 * Hour);
            Assert.That(FestivalRules.IsDay(sim), Is.True);
            // Put one home at the venue and one far away.
            int venueTile = FestivalRules.Tile(sim, FestivalDef.Square);
            Household near = TownLifeRules.Yours(sim)[0], far = TownLifeRules.Yours(sim)[1];
            var pop = sim.Config.Game.Population;
            near.HomeId = sim.State.Land.NextBuildingId++;
            sim.State.Land.Buildings.Add(new Sim.Core.Land.Building { Id = near.HomeId, Def = pop.House, TileId = venueTile });
            far.HomeId = sim.State.Land.NextBuildingId++;
            sim.State.Land.Buildings.Add(new Sim.Core.Land.Building { Id = far.HomeId, Def = pop.House, TileId = venueTile + 24 * 10 }); // ten rows down: past 500 m
            Assert.That(FestivalRules.Lift(sim, near), Is.EqualTo(d.InReach));
            Assert.That(FestivalRules.Lift(sim, far), Is.EqualTo(d.Outside));
            near.Happiness = Fixed.FromInt(50);
            HappinessRules.Update(sim, near);
            Fixed lifted = near.Happiness;
            Assert.That(lifted, Is.GreaterThan(Fixed.Zero));
            long rise = ShopRules.DemandThisHour(sim, ShopKind.Chai, -1, ShopGroup.Village, 12);
            Assert.That(rise, Is.EqualTo(plain + (Fixed.FromInt((int)plain) * d.ShopChai).RoundToLong()), "chai sells 30% more");
            Assert.That(ShopRules.DemandThisHour(sim, ShopKind.Veg, 0, ShopGroup.Village, 12) >= 0, Is.True);
            Assert.That(FestivalRules.ShopRise(sim, ShopKind.Veg), Is.EqualTo(d.ShopVeg));

            // The next day: gone.
            sim.AdvanceTo((day + 1) * Day + 2 * Hour);
            Assert.That(FestivalRules.IsDay(sim), Is.False);
            Assert.That(FestivalRules.Lift(sim, near), Is.EqualTo(0));
            Assert.That(ShopRules.DemandThisHour(sim, ShopKind.Chai, -1, ShopGroup.Village, 12), Is.EqualTo(plain));
            HappinessRules.Update(sim, near);
            Assert.That(near.Happiness, Is.LessThan(lifted).Or.EqualTo(Fixed.FromInt(100)), "the lift is gone");
        }

        // AC-2: the hall needs its caretaker; the square is always there.
        [Test]
        public void HallNeedsKeeper()
        {
            Simulation sim = Township();
            Assert.That(Hold(sim, FestivalDef.Hall).Reason, Is.EqualTo(RejectReason.VenueNotReady), "no hall");
            StaffHall(sim);
            Assert.That(FestivalRules.VenueReady(sim, FestivalDef.Hall), Is.True);
            Assert.That(Hold(sim, FestivalDef.Hall).Accepted, Is.True);
            Assert.That(FestivalRules.Reach(sim, FestivalDef.Hall), Is.EqualTo(CivicRules.Def(sim, CivicDef.Hall).Reach));
            Assert.That(FestivalRules.Reach(sim, FestivalDef.Square), Is.EqualTo(FestivalRules.Def(sim).SquareReach));

            Simulation village = TownshipTests.Start(cohort: true);
            Assert.That(village.Execute(new ScheduleFestival(village.State.Clock.SimTime, FestivalDef.Square, FestivalDef.You, 1)).Reason, Is.EqualTo(RejectReason.NoTownship), "no festival before the Township");
        }

        // AC-3: a festival held is recorded and announced; a 72 h absence doesn't skip a scheduled day.
        [Test]
        public void Record_Away()
        {
            Simulation sim = Township();
            Assert.That(Hold(sim, days: 2).Accepted, Is.True);
            TownLifeState s = TownLifeRules.State(sim);
            long cost = s.FestivalCost, day = s.FestivalDay;
            sim.DrainEvents();
            Assert.That(s.Festivals, Is.Empty);
            sim.AdvanceTo(sim.State.Clock.SimTime + 4 * Day, AdvanceMode.Away); // away across the day and beyond
            Assert.That(s.FestivalOn, Is.False);
            Assert.That(s.Festivals.Count, Is.EqualTo(1));
            FestivalRecord r = s.Festivals[0];
            Assert.That((r.Day, r.Cost, r.Payer, r.Venue), Is.EqualTo((day, cost, FestivalDef.Town, FestivalDef.Square)));
            Assert.That(r.People, Is.EqualTo(s.FestivalPeople));
            Assert.That(sim.PendingEvents.OfType<FestivalHeld>().Count(), Is.EqualTo(1), "announced once");

            // The record keeps the last few.
            for (int i = 0; i < FestivalRules.Def(sim).Keep + 3; i++)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + sim.Clock.SecondsPerSeason, AdvanceMode.Away);
                sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 1_000_000 * Coin), new Posting(Account.GiftsFromFounder, -1_000_000 * Coin));
                Hold(sim);
            }
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Day, AdvanceMode.Away);
            Assert.That(s.Festivals.Count, Is.EqualTo(FestivalRules.Def(sim).Keep));
        }

        [Test]
        public void SaveRoundTrips()
        {
            Simulation sim = Township();
            Hold(sim, days: 3);
            var loaded = SaveTests.RoundTrip(sim);
            TownLifeState a = TownLifeRules.State(sim), b = loaded.State.Township.TownLife;
            Assert.That((b.FestivalOn, b.FestivalDay, b.FestivalCost, b.FestivalSeason, b.FestivalJobId), Is.EqualTo((a.FestivalOn, a.FestivalDay, a.FestivalCost, a.FestivalSeason, a.FestivalJobId)));
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }
    }
}
