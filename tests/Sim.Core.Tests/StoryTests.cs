using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.History;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    /// <summary>US-159 (spec 029 §3.6, ADR 0165, 0166 gap 11, ADR 0167): four arcs over what the town already does, one step a season in a fixed order.</summary>
    public class StoryTests
    {
        private const long Coin = 100;

        /// <summary>A town at the Township where every hand who qualifies gives notice (the chance is 1 instead of one in five).</summary>
        private static Simulation Start(bool sure = true)
        {
            Simulation sim = TownshipTests.Start(false, sure ? (f, j) => f == "stories.json" ? j.Replace("\"chance\": 0.2", "\"chance\": 1.0") : j : null);
            if (!Sim.Core.Town.TownshipRules.Reached(sim)) TreasuryTests.Township(sim); // the stall story waits for the Township
            return sim;
        }

        private static StoriesState St(Simulation sim) { return sim.State.Chronicle.Stories; }

        private static void Close(Simulation sim) { sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1); }

        private static Citizen Person(Simulation sim, int age, long savings = 50_000, long home = 5)
        {
            Household h = PeopleKit.AddFamily(sim, home, age);
            h.Savings = savings * Coin;
            return sim.State.Citizens.Citizen(h.Members[0]);
        }

        /// <summary>One of your townspeople taken on as a hand (they stay at home), three seasons worked.</summary>
        private static Hand TownHand(Simulation sim, Citizen c, int seasons = 3)
        {
            var a = new Applicant { Id = 9_000 + c.Id, Name = c.Name, Skill = 50, Trait1 = c.Trait1, Trait2 = c.Trait2, CitizenId = c.Id };
            Hand h = PeopleSystem.Hire(sim, a, 30_000 * Coin, 0);
            h.SeasonsWorked = seasons;
            return h;
        }

        private static List<ChronicleEntry> Lines(Simulation sim) { return sim.State.Chronicle.Entries.FindAll(e => e.Kind == ChronicleKind.Story); }

        private static StoryArcState Arc(Simulation sim, Citizen c, StoryArc arc) { return St(sim).Arcs.Find(a => a.Citizen == c.Id && a.Arc == arc); }

        // AC-10: a hand with three seasons, savings and fair terms gives notice, goes at the next close (no severance, the post empty), the story founds
        // the stall (theirs, their trade's kind), and two seasons on it is doing well, or closed.
        [Test]
        public void Hand_Stall()
        {
            Simulation sim = Start();
            Citizen c = Person(sim, 30);
            Hand h = TownHand(sim, c);
            Assert.That(StoriesRules.Eligible(sim, c), Is.True);
            Close(sim);
            StoryArcState arc = Arc(sim, c, StoryArc.Stall);
            Assert.That(arc?.Step, Is.EqualTo(1), "gave notice at the close");
            Assert.That(StoriesRules.Notice(sim, c.Id), Is.Not.Null, "the People sheet says they will go");
            Assert.That(sim.State.People.Hands.Contains(h), Is.True, "still working the season");
            Assert.That(sim.State.Shops.Newcomers, Is.Empty);
            Close(sim);
            Assert.That(sim.State.People.Hands.Contains(h), Is.False, "left at the next close");
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey == "ledger.severance" && e.Reference == "hand:" + h.Id), Is.False, "no severance: leaving moves no money");
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Rival stall = sim.State.Shops.Newcomers.Single();
            Assert.That((stall.Owner, stall.Kind), Is.EqualTo((c.Id, ShopKind.Veg)), "founded by the story, a hand's trade is a vegetable stall");
            Assert.That(arc.Step, Is.EqualTo(2));
            Assert.That(c.Living && c.Job == CitizenJob.None, Is.True, "a townsperson without a post");
            Assert.That(Lines(sim).Select(l => (l.A, l.B / 100_000 % 10)), Is.EqualTo(new[] { (c.Id, 1L), (c.Id, 2L) }), "a Story line each step");
            Close(sim);
            Assert.That(arc.Step, Is.EqualTo(2), "judged two seasons after leaving");
            Close(sim);
            Assert.That((arc.Step, arc.Done, Lines(sim).Last().B % 100_000), Is.EqualTo((3, true, 1L)), "doing well: the stall still stands");

            // A closed stall.
            sim = Start();
            c = Person(sim, 30);
            TownHand(sim, c);
            Close(sim); Close(sim);
            sim.State.Shops.Newcomers.Clear();
            Close(sim); Close(sim);
            Assert.That(Lines(sim).Last().B % 100_000, Is.EqualTo(0L), "closed: they would work for wages again");
            Assert.That(c.Job, Is.EqualTo(CitizenJob.None));
        }

        [Test]
        public void Hand_Stall_Gates()
        {
            Simulation sim = Start();
            Citizen few = Person(sim, 30); TownHand(sim, few, seasons: 1);
            Citizen poor = Person(sim, 31, savings: 1_000); TownHand(sim, poor);
            Assert.That(Sim.Core.Town.TownshipRules.Reached(sim), Is.True, "the town is a Township");
            Assert.That(StoriesRules.Eligible(sim, few), Is.False, "under three seasons");
            Assert.That(StoriesRules.Eligible(sim, poor), Is.False, "savings under 10,000 coins");
            Citizen rich = Person(sim, 32); TownHand(sim, rich);
            int era = sim.State.Era.Current;
            sim.State.Era.Current = 1;
            Assert.That((Sim.Core.Town.TownshipRules.Reached(sim), StoriesRules.Eligible(sim, rich)), Is.EqualTo((false, false)), "before the Township nobody gives notice: a free rival must never stand in the way of the gate's three businesses");
            sim.State.Era.Current = era;
            sim.State.Shops.Newcomers.Add(new Rival { Def = -1, Id = "newcomer_veg", Kind = ShopKind.Veg, Pitch = -1, PriceFactor = Fixed.One, Stars = Fixed.One });
            Assert.That(StoriesRules.Eligible(sim, rich), Is.False, "a vegetable newcomer already stands: nobody is stranded, so nobody gives notice");
            sim.State.Shops.Newcomers.Clear();
            Assert.That(StoriesRules.Eligible(sim, rich), Is.True);
            // A shop of yours of his trade with no profitable season yet: nobody opens against it (the Township gate asks 3 businesses each with a profitable season).
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, 1_000_000 * Coin), new Posting(Account.Sales, -1_000_000 * Coin));
            PeopleKit.Ok(sim, new Sim.Core.Commands.FoundBusiness(sim.State.Clock.SimTime, "veg_stall", 0, 200_000 * Coin, false, null, 0));
            Business.Business mine = sim.State.Businesses.Businesses.Last();
            Assert.That((mine.ProfitableSeasons, StoriesRules.WaitsForFirstProfit(sim, ShopKind.Veg)), Is.EqualTo((0, true)));
            Assert.That(StoriesRules.Eligible(sim, rich), Is.False, "your vegetable stall has not had a profitable season: he waits");
            mine.ProfitableSeasons = 1;
            Assert.That(StoriesRules.Eligible(sim, rich), Is.True, "once it has, he may give notice");
            sim.State.Clock.IsAway = true;
            Close(sim);
            sim.State.Clock.IsAway = false;
            Assert.That(St(sim).Arcs.Where(a => a.Arc == StoryArc.Stall), Is.Empty, "no notice while you are away");

            Simulation slow = Start(sure: false);
            Assert.That(slow.Config.Game.Stories.StallChance, Is.EqualTo(Fixed.FromRatio(1, 5)), "one chance in five a season");
        }

        // AC-11: the first reader, a long life and a migrant family step at their moments.
        [Test]
        public void Reader()
        {
            Simulation sim = Start();
            Household h = PeopleKit.AddFamily(sim, 5, 30, 8);
            h.Savings = 50_000 * Coin;
            Citizen child = sim.State.Citizens.Citizen(h.Members[1]);
            child.SchoolYears = 1;
            Close(sim);
            Assert.That(Arc(sim, child, StoryArc.Reader)?.Step, Is.EqualTo(1), "starts school and nobody in the family can read");
            child.Education = 1;
            Close(sim);
            Assert.That(Arc(sim, child, StoryArc.Reader).Step, Is.EqualTo(2), "the first in the family to read");
            child.BornSeason = CitizenRules.BornFor(sim, 15);
            child.Job = CitizenJob.Business;
            Close(sim);
            StoryArcState arc = Arc(sim, child, StoryArc.Reader);
            Assert.That((arc.Step, arc.Done), Is.EqualTo((3, true)), "at 15 a paid job");
            Assert.That(Lines(sim).Count, Is.EqualTo(3));

            Simulation other = Start();
            Household lit = PeopleKit.AddFamily(other, 5, 30, 8);
            other.State.Citizens.Citizen(lit.Members[0]).Education = 1;
            other.State.Citizens.Citizen(lit.Members[1]).SchoolYears = 1;
            Close(other);
            Assert.That(St(other).Arcs, Is.Empty, "somebody in the family can already read: not the first");
        }

        [Test]
        public void Long_Life()
        {
            Simulation sim = Start();
            Citizen c = Person(sim, 60);
            Close(sim);
            Assert.That(Arc(sim, c, StoryArc.LongLife).Step, Is.EqualTo(1));
            Assert.That(Lines(sim).Last().B % 100_000, Is.InRange(60L, 61L), "the age is in the line");
            c.BornSeason = CitizenRules.BornFor(sim, 70);
            Close(sim);
            Assert.That(Arc(sim, c, StoryArc.LongLife).Step, Is.EqualTo(2));
            c.BornSeason = CitizenRules.BornFor(sim, 80);
            Close(sim);
            Assert.That(Arc(sim, c, StoryArc.LongLife).Step, Is.EqualTo(3));
            Close(sim);
            Assert.That(Lines(sim).Count, Is.EqualTo(3), "nothing more while they live");
            long id = c.Id;
            c.Gone = CitizenGone.Died;
            c.GoneAt = sim.State.Clock.SimTime;
            Close(sim);
            StoryArcState arc = Arc(sim, c, StoryArc.LongLife);
            Assert.That((arc.Step, arc.Done, Lines(sim).Last().B / 100_000 % 10), Is.EqualTo((4, true, 4L)), "died at 70 or more: remembered");
            Assert.That(id, Is.EqualTo(c.Id));
        }

        [Test]
        public void Migrant_Family()
        {
            Simulation sim = Start();
            Household h = PeopleKit.AddFamily(sim, 5, 32, 30);
            h.Savings = 50_000 * Coin;
            long t = sim.State.Clock.SimTime;
            foreach (long id in h.Members) sim.State.Citizens.Citizen(id).Story.Add(new StoryLine(StoryKind.Arrived, t, 0));
            Citizen head = sim.State.Citizens.Citizen(h.Members[0]);
            Close(sim);
            Assert.That(Arc(sim, head, StoryArc.Migrant), Is.Null, "one season is too soon");
            Close(sim);
            Assert.That(Arc(sim, head, StoryArc.Migrant)?.Step, Is.EqualTo(1), "stays 2 seasons");
            Close(sim); Close(sim);
            Assert.That(Arc(sim, head, StoryArc.Migrant).Step, Is.EqualTo(2), "stays 4 seasons");
            Assert.That(StoriesRules.InArc(sim, head.Id), Is.False, "a family waiting for a child does not keep its head from a stall or a long life");
            sim.State.Citizens.Citizen(h.Members[1]).Story.Add(new StoryLine(StoryKind.ChildBorn, sim.State.Clock.SimTime, 99));
            Close(sim);
            Assert.That((Arc(sim, head, StoryArc.Migrant).Step, Arc(sim, head, StoryArc.Migrant).Done), Is.EqualTo((3, true)), "a child is born to them");

            Simulation lone = Start();
            Household one = PeopleKit.AddFamily(lone, 5, 32);
            lone.State.Citizens.Citizen(one.Members[0]).Story.Add(new StoryLine(StoryKind.Arrived, 0, 0));
            for (int i = 0; i < 3; i++) Close(lone);
            Assert.That(St(lone).Arcs, Is.Empty, "a lone hand is not a family");
        }

        // AC-12: at most one step a season across the town, in the order stall, reader, migrants, long life, the lowest id first; one arc at a time for a person.
        [Test]
        public void One_A_Season()
        {
            Simulation sim = Start();
            Citizen elderB = Person(sim, 61), elderA = Person(sim, 62);
            Household h = PeopleKit.AddFamily(sim, 5, 30, 8);
            Citizen child = sim.State.Citizens.Citizen(h.Members[1]);
            child.SchoolYears = 1;
            Citizen hand = Person(sim, 33);
            TownHand(sim, hand);
            Close(sim);
            Assert.That(Lines(sim).Count, Is.EqualTo(1), "four steps are due, one is taken");
            Assert.That(Arc(sim, hand, StoryArc.Stall).Step, Is.EqualTo(1), "the stall goes first");
            Close(sim);
            Assert.That(Lines(sim).Count, Is.EqualTo(2));
            Assert.That(Arc(sim, hand, StoryArc.Stall).Step, Is.EqualTo(2), "his leaving is the season's step, kept");
            Close(sim);
            Assert.That(Arc(sim, child, StoryArc.Reader)?.Step, Is.EqualTo(1), "then the first reader");
            Close(sim);
            Assert.That(Arc(sim, hand, StoryArc.Stall).Step, Is.EqualTo(3), "his stall is judged: the stall's steps always come before the others");
            Close(sim);
            Assert.That(elderB.Id, Is.LessThan(elderA.Id));
            Assert.That(Arc(sim, elderB, StoryArc.LongLife).Step, Is.EqualTo(1), "the lowest id first");
            Assert.That(Arc(sim, elderA, StoryArc.LongLife), Is.Null, "the other waits a season");
            Assert.That(Lines(sim).Count, Is.EqualTo(5));
            Assert.That(St(sim).Arcs.GroupBy(a => a.Citizen).All(g => g.Count(a => !a.Done) <= 1), Is.True, "one arc at a time for a person");
        }

        [Test]
        public void Saves_Round_Trip()
        {
            Simulation sim = Start();
            Citizen c = Person(sim, 30);
            TownHand(sim, c);
            Close(sim); Close(sim);
            SimConfig config = sim.Config;
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, 1_700_000_000), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Shops.Newcomers.Single().Owner, Is.EqualTo(c.Id));
            Assert.That(r.State.Chronicle.Stories.Arcs.Select(a => (a.Citizen, a.Arc, a.Step)), Is.EqualTo(St(sim).Arcs.Select(a => (a.Citizen, a.Arc, a.Step))));
        }

        // Review of Sprint 26: past 64 arcs the oldest finished ones are pruned, but a finished story is remembered (citizen and arc) so it never happens twice.
        [Test]
        public void Pruned_Arcs_Never_Repeat()
        {
            Simulation sim = Start();
            Citizen c = Person(sim, 30);
            TownHand(sim, c);
            StoriesState st = St(sim);
            st.Arcs.Add(new StoryArcState { Citizen = c.Id, Arc = StoryArc.Stall, Step = 3, At = 1, Done = true }); // c opened a stall once
            for (int i = 0; i < 70; i++) st.Arcs.Add(new StoryArcState { Citizen = 5_000 + i, Arc = StoryArc.Reader, Step = 3, At = 1, Done = true });
            Assert.That(StoriesRules.Eligible(sim, c), Is.False, "while the arc is kept he has had his stall");
            Close(sim);
            Assert.That(st.Arcs.Count, Is.LessThanOrEqualTo(64), "pruned");
            Assert.That(st.Arcs.Exists(a => a.Citizen == c.Id && a.Arc == StoryArc.Stall), Is.False, "the oldest one went");
            Assert.That(st.Retired, Does.Contain(c.Id * 8 + (int)StoryArc.Stall), "but is remembered");
            Assert.That(StoriesRules.Eligible(sim, c), Is.False, "so he never opens a second stall");
            LoadResult back = SaveCodec.Read(SaveCodec.Write(sim.State, sim.Config, 0), sim.Config, 3);
            Assert.That(back.State.Chronicle.Stories.Retired, Is.EqualTo(st.Retired), "and it is saved");
        }
    }
}
