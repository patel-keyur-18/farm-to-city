using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 014 §3.6, §3.10, §3.13: families moving in and out, a hand's family, housing in kind.</summary>
    public class MigrationTests2
    {
        private static void Hamlet(Simulation sim)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
        }

        /// <summary>Starts a family house on <paramref name="tile"/> and returns when it will be finished.</summary>
        private static long StartHouse(Simulation sim, int tile = HomeTile)
        {
            Hamlet(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildFamilyHouse(t, tile));
            return t + 48 * Hour;
        }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        private static Household In(Simulation sim, long houseId) { return sim.State.Citizens.Households.SingleOrDefault(x => x.HomeId == houseId); }

        // AC-3: with no hand living alone, an empty house brings a family of 2–5 within 18 ± 6 h, live or away: a couple, maybe an
        // elder, children younger than their mother less 20; they pay rent at the next close (4200).
        [Test]
        public void FamilyMovesIn()
        {
            var sizes = new HashSet<int>();
            int elders = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                Simulation sim = Start(seed: seed);
                long ready = StartHouse(sim);
                bool away = seed % 2 == 0;
                if (away) sim.AdvanceAway(ready + 30 * Hour - sim.State.Clock.SimTime, ready + 30 * Hour - sim.State.Clock.SimTime, false);
                else sim.AdvanceTo(ready + 30 * Hour);
                FamilyMovedIn e = sim.DrainEvents().OfType<FamilyMovedIn>().Single();
                long after = e.SimTime - ready;
                Assert.That(after, Is.InRange(12 * Hour, 25 * Hour), "seed " + seed + (away ? " away" : " live"));
                Household x = sim.State.Citizens.Household(e.HouseholdId);
                List<Citizen> people = x.Members.Select(id => sim.State.Citizens.Citizen(id)).ToList();
                sizes.Add(people.Count);
                Assert.That(people.Count, Is.InRange(2, 5));
                Citizen man = sim.State.Citizens.Citizen(people.First(c => !c.Female && c.Spouse != 0).Id), wife = sim.State.Citizens.Citizen(man.Spouse);
                Assert.That(wife.Female && wife.Spouse == man.Id, Is.True, "a couple");
                Assert.That(people.All(c => c.Village == man.Village), Is.True, "from one village");
                foreach (Citizen child in people.Where(c => c.Mother == wife.Id))
                    Assert.That(CitizenRules.Age(sim, child), Is.LessThanOrEqualTo(System.Math.Max(0, CitizenRules.Age(sim, wife) - 20)));
                if (man.Mother != 0) elders++;
                Assert.That(x.Income, Is.GreaterThan(0), "a budget at once: two adults working outside");

                sim.AdvanceTo(NextClose(sim));
                Assert.That(sim.DrainEvents().OfType<RentPaid>().Single().HouseholdId, Is.EqualTo(x.Id), "rent from the next close");
            }
            Assert.That(sizes, Is.EquivalentTo(new[] { 2, 3, 4, 5 }));
            Assert.That(elders, Is.GreaterThan(0), "sometimes an elder");
        }

        // AC-11 and §3.13: a finished house is offered to the hand living alone first; no strangers while it's open; yes moves his
        // family in and frees his hut; no rent; from his next renewal his cash wage is 6,000 × index less (a loyal hand's kept wage
        // too); his full wage again once they live in a hut.
        [Test]
        public void HandFamily_HousingInKind()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            long hut = h.HutId;
            long ready = StartHouse(sim);
            sim.DrainEvents();
            sim.AdvanceTo(ready + Hour);
            FamilyRequest ask = sim.DrainEvents().OfType<FamilyRequest>().Single();
            Assert.That(ask.HandId, Is.EqualTo(h.Id));
            sim.AdvanceTo(ready + 17 * Hour);
            Assert.That(sim.DrainEvents().OfType<FamilyMovedIn>(), Is.Empty, "no strangers while the hand is asked");
            Assert.That(sim.Execute(new AnswerFamilyRequest(sim.State.Clock.SimTime, h.Id + 1, true)).Reason, Is.EqualTo(RejectReason.NoOffer));

            Ok(sim, new AnswerFamilyRequest(sim.State.Clock.SimTime, h.Id, true));
            Household x = In(sim, ask.HouseId);
            Citizen me = sim.State.Citizens.OfHand(h.Id);
            Assert.That(x.Members, Does.Contain(me.Id));
            Assert.That(x.Members.Count, Is.InRange(2, 4), "a spouse and 0–2 children");
            Assert.That(sim.State.Citizens.Citizen(me.Spouse).Female, Is.Not.EqualTo(me.Female));
            Assert.That((h.HutId, PeopleRules.EmptyHut(sim)?.Id), Is.EqualTo((0L, (long?)hut)), "his hut is free for another hand");
            Assert.That(sim.PendingEvents.OfType<FamilyMovedIn>().Single().HandId, Is.EqualTo(h.Id));
            long cash = h.Wage;

            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            Assert.That(sim.DrainEvents().OfType<RentPaid>(), Is.Empty, "the house is part of his pay");
            long house = MarketRules.Indexed(sim, 6_000_00);
            long asked;
            asked = h.Wage + h.HousingInKind;
            Assert.That(h.HousingInKind, Is.EqualTo(house), "from his next renewal");
            Assert.That(h.Wage, Is.EqualTo(asked - house));
            Assert.That(asked, Is.GreaterThan(cash - 1), "the asked wage before the house");

            // A loyal hand keeps the whole wage he had, the house still part of it.
            int loyal = sim.Config.Game.People.Traits.ToList().FindIndex(t => t.KeepsWage);
            h.Trait1 = loyal;
            h.Trait2 = loyal == 0 ? 1 : 0;
            long kept = h.Wage + h.HousingInKind;
            sim.AdvanceTo(NextClose(sim));
            Assert.That((h.Wage + h.HousingInKind, h.HousingInKind), Is.EqualTo((kept, MarketRules.Indexed(sim, 6_000_00))));

            // Living in a hut again (as if they had moved out): the full wage at the next renewal.
            x.HomeId = hut;
            h.HutId = hut;
            sim.AdvanceTo(NextClose(sim));
            Assert.That((h.Wage, h.HousingInKind), Is.EqualTo((kept, 0L)));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);

            // Saves keep it (schema 10).
            LoadResult r = SaveTests.RoundTrip(sim);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        // AC-7: below 35 at 2 closes in a row, or 2 seasons behind on rent, a household leaves with chance 0.5 a close (never sooner);
        // a hand's household leaving ends his contract.
        [Test]
        public void MovingOut()
        {
            int left = 0, seeds = 200;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                Simulation sim = Start(seed: seed);
                long ready = StartHouse(sim);
                sim.AdvanceTo(ready);
                Household x = AddFamily(sim, sim.State.Land.Buildings.Last().Id, 35, 33);
                x.Happiness = Fixed.FromInt(20);
                MigrationRules.MoveOut(sim);
                Assert.That(sim.State.Citizens.Households, Does.Contain(x), "one close below 35 is not enough");
                MigrationRules.MoveOut(sim);
                if (!sim.State.Citizens.Households.Contains(x))
                {
                    left++;
                    Assert.That(sim.PendingEvents.OfType<HouseholdLeft>().Single().Reason, Is.EqualTo(LeaveReason.Unhappy));
                }
            }
            Assert.That(left, Is.InRange(seeds * 0.4, seeds * 0.6), "half");

            // Rent: 2 seasons behind; a hand's household leaving ends his contract.
            for (ulong seed = 1; seed < 50; seed++)
            {
                Simulation sim = Start(seed: seed);
                Hand h = HireHand(sim);
                sim.AdvanceTo(StartHouse(sim) + Hour);
                Ok(sim, new AnswerFamilyRequest(sim.State.Clock.SimTime, h.Id, true));
                Household x = sim.State.Citizens.Household(sim.State.Citizens.OfHand(h.Id).HouseholdId);
                x.RentBehind = 2;
                sim.DrainEvents();
                MigrationRules.MoveOut(sim);
                if (sim.State.Citizens.Households.Contains(x)) continue;
                var events = sim.DrainEvents();
                Assert.That(events.OfType<HouseholdLeft>().Single().Reason, Is.EqualTo(LeaveReason.RentBehind));
                Assert.That(events.OfType<HandLeft>().Single().HandId, Is.EqualTo(h.Id));
                Assert.That(sim.State.People.Hands, Is.Empty);
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
                return;
            }
            Assert.Fail("no seed under 50 left");
        }

        // AC-14, §3.13's new rows: the offer comes before strangers and, after "Not now", strangers come; a dismissed hand's family stays
        // as tenants and pays rent; an elder alone behind on rent goes to live with family.
        [Test]
        public void EveryPath()
        {
            Simulation sim = Start();
            Hand h = HireHand(sim);
            sim.DrainEvents();
            long ready = StartHouse(sim);
            StartHouse(sim, HomeTile); // a second house on the same acre
            sim.AdvanceTo(ready + Hour);
            var first = sim.DrainEvents();
            Assert.That(first.OfType<FamilyRequest>().Count(), Is.EqualTo(1), "one offer at a time");
            Assert.That(first.OfType<FamilyMovedIn>(), Is.Empty);
            Ok(sim, new AnswerFamilyRequest(sim.State.Clock.SimTime, h.Id, false));
            sim.AdvanceTo(sim.State.Clock.SimTime + 25 * Hour);
            Assert.That(sim.DrainEvents().OfType<FamilyMovedIn>().Count(), Is.GreaterThanOrEqualTo(1), "Not now: strangers come");

            // The hand's family in the other house, then he is dismissed: they stay as tenants and pay rent from the next close.
            Simulation b = Start(seed: 7);
            Hand hb = HireHand(b);
            b.AdvanceTo(StartHouse(b) + Hour);
            Ok(b, new AnswerFamilyRequest(b.State.Clock.SimTime, hb.Id, true));
            Household family = b.State.Citizens.Household(b.State.Citizens.OfHand(hb.Id).HouseholdId);
            Citizen him = b.State.Citizens.OfHand(hb.Id);
            Ok(b, new DismissHand(b.State.Clock.SimTime, hb.Id));
            Assert.That(b.State.Citizens.Households, Does.Contain(family), "a home isn't lost with a job");
            Assert.That(him.Job, Is.Not.EqualTo(CitizenJob.Hand));
            b.DrainEvents();
            b.AdvanceTo(NextClose(b));
            var close = b.DrainEvents();
            Assert.That(close.OfType<RentPaid>().Concat<SimEvent>(close.OfType<RentMissed>()).Count(), Is.EqualTo(1), "rent is due now");

            // An elder alone, 2 seasons behind: gone to live with family.
            for (ulong seed = 1; seed < 50; seed++)
            {
                Simulation c = Start(seed: seed);
                c.AdvanceTo(StartHouse(c));
                Household elder = AddFamily(c, c.State.Land.Buildings.Last().Id, 72);
                elder.RentBehind = 2;
                c.DrainEvents();
                MigrationRules.MoveOut(c);
                if (c.State.Citizens.Households.Contains(elder)) continue;
                Assert.That(c.PendingEvents.OfType<HouseholdLeft>().Single().Reason, Is.EqualTo(LeaveReason.ToFamily));
                return;
            }
            Assert.Fail("no seed under 50 left");
        }
    }
}
