using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-240 (spec 014 §3.14): rent by the kind of house, its place and a yearly lease.</summary>
    public class RentTests
    {
        private const int DistrictTile = 161; // row 10: the district road (row 11) is below it
        private const int LaneTile = 153; // row 9: the village lane (column 10) is beside it

        private static Building House(Simulation sim, int tile = HomeTile)
        {
            if (sim.State.Era != null)
            {
                var eras = sim.Config.Game.Onboarding.Eras;
                for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
            }
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildFamilyHouse(t, tile));
            sim.AdvanceTo(t + 48 * Hour);
            return sim.State.Land.Buildings.Last(b => HouseRules.IsHouse(sim, b));
        }

        private static long Season(Simulation sim) { return LeaseRules.Season(sim); }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        /// <summary>A tenant family with a lease at <paramref name="rent"/>, in the last season of it unless told otherwise.</summary>
        private static Household Tenants(Simulation sim, long rent, bool lastSeason = true, int tile = HomeTile)
        {
            Household x = AddFamily(sim, House(sim, tile).Id, 32, 30);
            HouseholdRules.SetBudget(sim, x);
            x.LeaseRent = rent;
            x.Rent = rent;
            x.LeaseStart = Season(sim) - (lastSeason ? sim.Config.Game.Population.LeaseSeasons - 1 : 0);
            return x;
        }

        // AC-18: the market rent is the kind's base × the place × the index, to a whole coin; the place is the best road the acre touches.
        [Test]
        public void KindLocationIndex()
        {
            Simulation sim = Start();
            Assert.That(LeaseRules.Place(sim, DistrictTile), Is.EqualTo(Fixed.Parse("1.2")), "the district road");
            Assert.That(LeaseRules.Place(sim, LaneTile), Is.EqualTo(Fixed.One), "a lane");
            Assert.That(LeaseRules.Place(sim, HomeTile), Is.EqualTo(Fixed.Parse("0.85")), "inner");
            Assert.That(LeaseRules.Place(sim, 169), Is.EqualTo(Fixed.Parse("1.2")), "the district road beats a lane");
            Assert.That(LeaseRules.MarketRent(sim, DistrictTile), Is.EqualTo(3_000_00L));
            Assert.That(LeaseRules.MarketRent(sim, LaneTile), Is.EqualTo(2_500_00L));
            Assert.That(LeaseRules.MarketRent(sim, HomeTile), Is.EqualTo(2_125_00L));
            sim.State.Market.PriceIndex = Fixed.Parse("1.04");
            Assert.That(LeaseRules.MarketRent(sim, DistrictTile), Is.EqualTo(3_120_00L), "2,500 × 1.20 × 1.04");
        }

        // AC-19: a lease holds its rent for 4 seasons; at its close the rent moves toward the market, at most 10% up.
        [Test]
        public void YearlyLease()
        {
            Simulation sim = Start();
            Household x = AddFamily(sim, House(sim).Id, 32, 30);
            HouseholdRules.SetBudget(sim, x);
            Assert.That((x.LeaseRent, x.Rent, x.LeaseStart), Is.EqualTo((2_125_00L, 2_125_00L, Season(sim))), "a lease at the market rent from move-in");
            x.LeaseRent = x.Rent = 1_900_00; // an older, cheaper lease: the market is now above it
            for (int i = 1; i <= 3; i++)
            {
                sim.AdvanceTo(NextClose(sim));
                Assert.That(x.LeaseRent, Is.EqualTo(1_900_00L), "fixed inside the lease, season " + i);
            }
            Assert.That(LeaseRules.InLastSeason(sim, x), Is.True);
            LeaseOptions o = LeaseRules.Options(sim, x);
            Assert.That(o.Allowed, Is.EqualTo(2_090_00L), "the 10% cap is below the market's 2,125");
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            LeaseRenewed e = sim.PendingEvents.OfType<LeaseRenewed>().Single();
            Assert.That((e.OldRent, e.NewRent, e.Choice, e.Answered), Is.EqualTo((1_900_00L, 2_090_00L, LeaseChoice.Market, false)), "no answer: the market rise");
            Assert.That(x.LeaseRent, Is.EqualTo(2_090_00L));
            Assert.That(x.LeaseStart, Is.EqualTo(Season(sim)), "a new lease");
            // Above the market: it renews at the market with no card.
            x.LeaseRent = x.Rent = 6_000_00;
            x.LeaseStart = Season(sim) - 3;
            Assert.That(LeaseRules.Options(sim, x).AboveMarket && !LeaseRules.Options(sim, x).CardDue, Is.True);
            sim.AdvanceTo(NextClose(sim));
            Assert.That(x.LeaseRent, Is.EqualTo(2_125_00L), "a rent above the market comes down to it");
        }

        // AC-19: a raise never goes above what 30% of the household's income allows, never below the current rent; lowering is offered over the limit.
        [Test]
        public void AffordableCap()
        {
            Simulation sim = Start();
            Household x = Tenants(sim, 1_900_00);
            x.Income = 6_800_00; // 30% = 2,040
            LeaseOptions o = LeaseRules.Options(sim, x);
            Assert.That((o.Bearable, o.Allowed, o.Lowered), Is.EqualTo((2_040_00L, 2_040_00L, 0L)), "the burden limit binds before the cap and the market");
            Assert.That(o.Offers(LeaseChoice.Market) && o.Offers(LeaseChoice.Smaller) && o.Offers(LeaseChoice.Keep) && !o.Offers(LeaseChoice.Lower), Is.True);
            Assert.That(o.Smaller, Is.EqualTo(1_970_00L), "half the rise");
            x.Income = 5_000_00; // 30% = 1,500: they already pay more than they can bear
            o = LeaseRules.Options(sim, x);
            Assert.That(o.Allowed, Is.EqualTo(1_900_00L), "never below the current rent: no raise now");
            Assert.That(o.Lowered, Is.EqualTo(1_805_00L), "lowering 5% is offered");
            Assert.That((o.CardDue, o.Default), Is.EqualTo((true, LeaseChoice.Keep)));
            x.Income = 20_000_00;
            o = LeaseRules.Options(sim, x);
            Assert.That(o.Allowed, Is.EqualTo(2_090_00L), "plenty of income: the 10% cap");
        }

        // AC-20: an answer is kept in the lease's last season and applied at its close; with none the market rise applies; refusals name why.
        [Test]
        public void RenewalChoice()
        {
            Simulation sim = Start();
            sim.AdvanceTo(12 * sim.Clock.SecondsPerSeason); // a lease can't start before the game does: be a few years in
            Household early = Tenants(sim, 1_900_00, lastSeason: false);
            Assert.That(sim.Execute(new RenewLease(sim.State.Clock.SimTime, early.Id, LeaseChoice.Keep)).Reason, Is.EqualTo(RejectReason.LeaseNotDue));
            Assert.That(sim.Execute(new RenewLease(sim.State.Clock.SimTime, 9999, LeaseChoice.Keep)).Reason, Is.EqualTo(RejectReason.UnknownHousehold));
            Hand hand = HireHand(sim);
            Household inHut = sim.State.Citizens.Household(sim.State.Citizens.OfHand(hand.Id).HouseholdId);
            Assert.That(sim.Execute(new RenewLease(sim.State.Clock.SimTime, inHut.Id, LeaseChoice.Keep)).Reason, Is.EqualTo(RejectReason.NoLease), "a hut has no lease");

            Household x = Tenants(sim, 1_900_00);
            x.Income = 20_000_00;
            Assert.That(sim.Execute(new RenewLease(sim.State.Clock.SimTime, x.Id, LeaseChoice.Lower)).Reason, Is.EqualTo(RejectReason.NotOffered), "lowering isn't offered to a family that can bear its rent");
            Ok(sim, new RenewLease(sim.State.Clock.SimTime, x.Id, LeaseChoice.Smaller));
            long happy = x.Happiness.RoundToLong();
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            LeaseRenewed e = sim.PendingEvents.OfType<LeaseRenewed>().Single(r => r.HouseholdId == x.Id);
            Assert.That((e.NewRent, e.Choice, e.Answered), Is.EqualTo((1_995_00L, LeaseChoice.Smaller, true)));
            Assert.That(x.LeaseRent, Is.EqualTo(1_995_00L));
            Assert.That(LeaseRules.GoodwillNow(sim, x), Is.EqualTo(sim.Config.Game.Population.GoodwillSmaller), "a smaller rise is remembered for the lease");
            Assert.That(x.Renewal, Is.EqualTo(LeaseChoice.None), "the answer is spent");
            // Keeping the rent when a raise was possible earns more goodwill.
            Household y = Tenants(sim, 1_900_00, true, 85);
            y.Income = 20_000_00;
            Ok(sim, new RenewLease(sim.State.Clock.SimTime, y.Id, LeaseChoice.Keep));
            sim.AdvanceTo(NextClose(sim));
            Assert.That((y.LeaseRent, LeaseRules.GoodwillNow(sim, y)), Is.EqualTo((1_900_00L, sim.Config.Game.Population.GoodwillKept)));
        }

        // AC-21: a hand's house in his pay is the house's market rent; a household that stops being a hand's starts a lease at the market rent.
        [Test]
        public void HandHouse()
        {
            Simulation sim = Start();
            Household x = AddFamily(sim, House(sim).Id, 32, 30);
            HouseholdRules.SetBudget(sim, x);
            Assert.That(x.LeaseRent, Is.EqualTo(2_125_00L), "an inner acre");
            Assert.That(LeaseRules.MarketRentFor(sim, x), Is.EqualTo(2_125_00L));
            LeaseRules.End(x);
            x.Rent = 0;
            Assert.That(LeaseRules.Applies(sim, x), Is.False, "no lease while the house is a hand's pay");
            HouseholdRules.SetBudget(sim, x); // the hand is gone: the family pays rent again
            Assert.That((x.LeaseRent, x.LeaseStart), Is.EqualTo((2_125_00L, Season(sim))), "a new lease at the market rent");
        }

        // AC-22: a settled family is patient: leaveSeasons plus a year for each year lived here, at most settledMax.
        [Test]
        public void SettledFamilies()
        {
            Simulation sim = Start();
            sim.AdvanceTo(40 * sim.Clock.SecondsPerSeason); // ten years in, so a household can have lived here for years
            Household x = AddFamily(sim, House(sim).Id, 32, 30);
            HouseholdRules.SetBudget(sim, x);
            var p = sim.Config.Game.Population;
            long now = Season(sim);
            Assert.That(LeaseRules.Patience(sim, x), Is.EqualTo(p.LeaveSeasons), "a new family");
            x.SinceSeason = now - 4;
            Assert.That(LeaseRules.Patience(sim, x), Is.EqualTo(p.LeaveSeasons + 1), "a year here");
            x.SinceSeason = now - 12;
            Assert.That(LeaseRules.Patience(sim, x), Is.EqualTo(p.LeaveSeasons + 3));
            x.SinceSeason = now - 36;
            Assert.That(LeaseRules.Patience(sim, x), Is.EqualTo(p.SettledMax), "at most settledMax");

            // Unhappy for fewer closes than its patience: stays. (Settled two years: patience 4.)
            x.SinceSeason = now - 8;
            x.Happiness = Fixed.FromInt(10);
            x.LowSeasons = 2;
            MigrationRules.MoveOut(sim);
            Assert.That((x.LowSeasons, sim.State.Citizens.Households.Contains(x)), Is.EqualTo((3, true)), "3 bad closes of a patient 4");
        }
    }
}
