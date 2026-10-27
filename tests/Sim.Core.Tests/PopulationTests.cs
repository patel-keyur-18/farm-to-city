using System.Linq;
using NUnit.Framework;
using Sim.Core.Events;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 014 §3.9 (ADR 0070): births, marriages and deaths.</summary>
    public class PopulationTests
    {
        private static void Hamlet(Simulation sim)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == "hamlet") sim.State.Era.Current = i;
        }

        private static Building House(Simulation sim, int tile = HomeTile)
        {
            Hamlet(sim);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new Commands.BuildFamilyHouse(t, tile));
            sim.AdvanceTo(t + 48 * Hour);
            return sim.State.Land.Buildings.Last(b => HouseRules.IsHouse(sim, b));
        }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        private static double D(Fixed x) { return x.Raw / 4294967296.0; }

        private static Citizen Member(Simulation sim, Household x, int i) { return sim.State.Citizens.Citizen(x.Members[i]); }

        /// <summary>Kamla (76, widowed), Manoj (38) and Usha (30) married, their son (25) and daughter (24), unmarried.</summary>
        private static Household Family(Simulation sim, long homeId)
        {
            Household x = AddFamily(sim, homeId, 76, 38, 30, 25, 24);
            Citizen kamla = Member(sim, x, 0), manoj = Member(sim, x, 1), usha = Member(sim, x, 2), son = Member(sim, x, 3), daughter = Member(sim, x, 4);
            kamla.Female = true;
            usha.Female = true;
            daughter.Female = true;
            manoj.Mother = kamla.Id;
            manoj.Spouse = usha.Id;
            usha.Spouse = manoj.Id;
            son.Father = daughter.Father = manoj.Id;
            son.Mother = daughter.Mother = usha.Id;
            x.Happiness = Fixed.FromInt(80);
            return x;
        }

        // AC-6: the chances follow §3.9; over many seeds the flows happen about as often as their chances say; the same seed gives
        // the same town; a son's bride moves in (into a free house when one stands empty), a daughter marries away and is kept while a
        // parent lives (ADR 0070); a death leaves the household mourning.
        [Test]
        public void Flows()
        {
            Simulation probe = Start();
            Household one = Family(probe, House(probe).Id);
            Citizen kamla = Member(probe, one, 0), usha = Member(probe, one, 2);
            Assert.That(D(PopulationRules.DeathChance(probe, kamla)), Is.EqualTo(0.015 * 4).Within(1e-6), "76: 1.5% × 2^(16 ÷ 8)");
            kamla.Health = Fixed.Parse("0.6");
            Assert.That(D(PopulationRules.DeathChance(probe, kamla)), Is.EqualTo(0.015 * 4 * 1.5).Within(1e-6), "× 1.5 in poor health");
            Assert.That(D(PopulationRules.BirthChance(probe, one, usha)), Is.EqualTo(0.10 * (0.6 + 0.4 * 0.8)).Within(1e-6), "room, happiness 80");
            Assert.That(PopulationRules.DeathChance(probe, Member(probe, one, 1)), Is.EqualTo(Fixed.Zero), "only elders");

            int deaths = 0, births = 0, sons = 0, daughters = 0, seeds = 400;
            for (ulong seed = 1; seed <= (ulong)seeds; seed++)
            {
                Simulation sim = Start(seed: seed);
                Household x = Family(sim, House(sim).Id);
                sim.AdvanceTo(NextClose(sim));
                var events = sim.DrainEvents();
                deaths += events.OfType<CitizenDied>().Count();
                births += events.OfType<CitizenBorn>().Count();
                sons += events.OfType<CitizenMarried>().Count(e => !e.MovedAway);
                daughters += events.OfType<CitizenMarried>().Count(e => e.MovedAway);
                Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            }
            TestContext.Out.WriteLine("deaths " + deaths + ", births " + births + ", sons " + sons + ", daughters " + daughters + " in " + seeds);
            Assert.That(deaths, Is.InRange(seeds * 0.065 * 0.6, seeds * 0.065 * 1.4), "about 6.5% (77 at the close)");
            Assert.That(births, Is.InRange(seeds * 0.092 * 0.6, seeds * 0.092 * 1.4), "about 9%: happiness moves daily, so roughly");
            Assert.That(sons, Is.InRange(seeds * 0.25 * 0.7, seeds * 0.25 * 1.3), "25% a season");
            Assert.That(daughters, Is.InRange(seeds * 0.25 * 0.7, seeds * 0.25 * 1.3));

            // Same seed, same town.
            ulong[] hashes = new ulong[2];
            for (int i = 0; i < 2; i++)
            {
                Simulation sim = Start(seed: 9);
                Family(sim, House(sim).Id);
                for (int k = 0; k < 6; k++) sim.AdvanceTo(NextClose(sim));
                hashes[i] = sim.State.ComputeHash();
            }
            Assert.That(hashes[1], Is.EqualTo(hashes[0]));

            // Each flow, found by seed: a death (mourning, the widowed stay), a son's bride into a free house, a daughter married away.
            Simulation died = SeedWhere(s => s.OfType<CitizenDied>().Any(), out Household dx, false);
            CitizenDied death = died.PendingEvents.OfType<CitizenDied>().Single();
            Assert.That((death.Age, death.HouseEmpty, dx.Members.Contains(death.CitizenId)), Is.EqualTo((77, false, false)), "Kamla, a year older at the close (§3.12: ageing, then deaths)");
            Assert.That(dx.MourningSeason, Is.EqualTo(CitizenRules.SeasonOf(died, died.State.Clock.SimTime)), "mourns this season");
            HappinessRules.Update(died, dx);
            Fixed mourning = dx.Happiness;
            dx.MourningSeason = -1;
            HappinessRules.Update(died, dx);
            Assert.That(D(dx.Happiness - mourning), Is.EqualTo(10).Within(1e-6), "−10 for the season");
            Citizen gone = died.State.Citizens.Citizens.Single(c => !c.Living);
            Assert.That((gone.Gone, gone.Story.Last().Kind), Is.EqualTo((CitizenGone.Died, StoryKind.Died)), "kept: her son lives");

            Simulation wed = SeedWhere(s => s.OfType<CitizenMarried>().Any(e => !e.MovedAway), out Household wx, true);
            CitizenMarried m = wed.PendingEvents.OfType<CitizenMarried>().First(e => !e.MovedAway);
            Citizen groom = wed.State.Citizens.Citizen(m.CitizenId), bride = wed.State.Citizens.Citizen(m.SpouseId);
            Assert.That((bride.Female, bride.Spouse, groom.Spouse, bride.Father, bride.Village >= 0), Is.EqualTo((true, groom.Id, bride.Id, 0L, true)), "from another village");
            Assert.That(m.HomeHouseholdId, Is.Not.EqualTo(m.HouseholdId), "a second house stood empty: the couple moved into it");
            Household couple = wed.State.Citizens.Household(m.HomeHouseholdId);
            Assert.That(couple.Members, Is.EquivalentTo(new[] { groom.Id, bride.Id }));
            Assert.That(couple.Income, Is.GreaterThan(0), "a budget for the new household");
            Assert.That(wx.Members, Does.Not.Contain(groom.Id));

            Simulation away = SeedWhere(s => s.OfType<CitizenMarried>().Any(e => e.MovedAway), out Household ax, false);
            CitizenMarried d = away.PendingEvents.OfType<CitizenMarried>().First(e => e.MovedAway);
            Citizen daughter = away.State.Citizens.Citizen(d.CitizenId);
            Assert.That((daughter.Gone, daughter.Village, ax.Members.Contains(daughter.Id)), Is.EqualTo((CitizenGone.MarriedAway, d.Village, false)));
            Assert.That(away.State.Citizens.Population, Is.EqualTo(ax.Members.Count + away.State.Citizens.Households.Where(h => h != ax).Sum(h => h.Members.Count)));
        }

        /// <summary>The first seed whose first close emits what <paramref name="found"/> looks for; a second empty house when asked.</summary>
        private static Simulation SeedWhere(System.Func<System.Collections.Generic.IReadOnlyList<SimEvent>, bool> found, out Household x, bool spareHouse)
        {
            for (ulong seed = 1; seed < 500; seed++)
            {
                Simulation sim = Start(seed: seed);
                x = Family(sim, House(sim).Id);
                if (spareHouse) House(sim);
                sim.State.Citizens.NextFamilyAt = long.MaxValue; // no family moves in before the close (US-110)
                sim.DrainEvents();
                sim.AdvanceTo(NextClose(sim));
                if (found(sim.PendingEvents)) return sim;
            }
            throw new AssertionException("no seed under 500");
        }

        // AC-6: nobody under 60 ever dies: a crowded, hungry town of every age over 40 seasons, many seeds.
        [Test]
        public void NoYoungDeaths()
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                Simulation sim = Start(seed: seed);
                Building house = House(sim);
                Household x = AddFamily(sim, house.Id, 2, 9, 17, 18, 25, 40, 59, 59, 62);
                foreach (long id in x.Members) sim.State.Citizens.Citizen(id).Health = Fixed.Parse("0.5");
                sim.DrainEvents();
                for (int k = 0; k < 40; k++)
                {
                    foreach (Citizen c in sim.State.Citizens.Citizens.Where(c => c.Living))
                        Assert.That(PopulationRules.DeathChance(sim, c) == Fixed.Zero || CitizenRules.Age(sim, c) >= 60, Is.True);
                    sim.AdvanceTo(NextClose(sim));
                    foreach (CitizenDied e in sim.DrainEvents().OfType<CitizenDied>())
                        Assert.That(e.Age, Is.GreaterThanOrEqualTo(60), "seed " + seed + ": only elders die");
                }
            }
        }
    
        // AC-13: a departed citizen stays while a living spouse, child or grandchild (a daughter married away: a parent) links to them,
        // and is dropped at the close after the last one goes; 20 years (80 life-years) of a Village-sized town (6 houses) keep under
        // 1 departed a living citizen at every close, about 0.3 on average (measured, 4 seeds: worst 0.58–0.90).
        [Test]
        public void FamilyTree_Bounded()
        {
            foreach (ulong seed in new ulong[] { 11, 12, 13, 14 })
            {
                Simulation sim = Start(seed: seed);
                CashTo(sim, 1_000_000_00);
                Hamlet(sim);
                long t = sim.State.Clock.SimTime;
                for (int h = 0; h < 6; h++) Ok(sim, new Commands.BuildFamilyHouse(t, HomeTile + h / 2));
                double sum = 0;
                int closes = 0, flows = 0;
                for (int k = 0; k < 80; k++)
                {
                    sim.AdvanceTo(NextClose(sim));
                    var events = sim.DrainEvents();
                    flows += events.OfType<CitizenDied>().Count() + events.OfType<CitizenBorn>().Count() + events.OfType<CitizenMarried>().Count();
                    CitizensState town = sim.State.Citizens;
                    int living = town.Population, departed = town.Citizens.Count - living;
                    foreach (Citizen gone in town.Citizens.Where(c => !c.Living))
                        Assert.That(town.Citizens.Any(c => c.Living && (c.Spouse == gone.Id || c.Father == gone.Id || c.Mother == gone.Id
                            || town.Citizen(c.Father)?.Father == gone.Id || town.Citizen(c.Father)?.Mother == gone.Id
                            || town.Citizen(c.Mother)?.Father == gone.Id || town.Citizen(c.Mother)?.Mother == gone.Id
                            || gone.Gone == CitizenGone.MarriedAway && (c.Id == gone.Father || c.Id == gone.Mother))), Is.True, "seed " + seed + ", close " + k + ": nobody unlinked is kept");
                    if (k < 8) continue; // the first families settling in
                    Assert.That(departed, Is.LessThan(living), "seed " + seed + ", close " + k + ": under 1 departed a living citizen");
                    sum += departed / (double)living;
                    closes++;
                }
                Assert.That(sum / closes, Is.LessThan(0.5), "seed " + seed + ": about 0.3 on average");
                Assert.That(flows, Is.GreaterThan(20), "seed " + seed + ": a town that lived");
            }

            // Dropped at the close after the last link goes: a widowed elder alone leaves with her husband's record.
            Simulation b = Start();
            Household x = AddFamily(b, House(b).Id, 78, 74);
            Citizen him = Member(b, x, 0), her = Member(b, x, 1);
            her.Female = true;
            him.Spouse = her.Id;
            her.Spouse = him.Id;
            him.Gone = CitizenGone.Died;
            x.Members.Remove(him.Id);
            CitizenRules.Leave(b, x);
            Assert.That(b.State.Citizens.Citizens, Does.Contain(him).And.Contain(her), "kept until the close");
            b.AdvanceTo(NextClose(b));
            Assert.That(b.State.Citizens.Citizens, Does.Not.Contain(him).And.Not.Contain(her), "dropped at the close after");
        }
    }
}
