using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-148 part 2 (spec 023 §3.7, ADR 0116): the watchman's post, the community hall and the playground, their appointments, effects and budget guard.</summary>
    public class CivicTests
    {
        private const int Watchman = CivicDef.Watchman, Hall = CivicDef.Hall, Playground = CivicDef.Playground;

        /// <summary>A Township with the old village in, two families of yours and the treasury rich enough to commission all three.</summary>
        private static Simulation Township(System.Func<string, string, string> edit = null, long treasuryCoins = 1_000_000)
        {
            Simulation sim = TownshipTests.Start(cohort: true, edit);
            TownshipTests.Families(sim, 2);
            TreasuryTests.Township(sim);
            Fund(sim, treasuryCoins * Coin);
            sim.DrainEvents();
            return sim;
        }

        private static void Fund(Simulation sim, long amount)
        {
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.GiftsFromFounder, -amount));
        }

        private static string WorkOf(int civic) { return civic == Watchman ? "watchman_post" : civic == Hall ? "community_hall" : "playground"; }

        private static void Build(Simulation sim, int civic) { Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, WorkOf(civic))); }

        private static void Staff(Simulation sim, int civic)
        {
            Ok(sim, new PostCivicJob(sim.State.Clock.SimTime, civic));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
        }

        // AC-8: the three buildings cost what spec 023 §3.7 says (a work paid from the treasury's own cash, kept at cost), on their own plots, once each.
        [Test]
        public void Commission()
        {
            Simulation sim = Township();
            long cash = sim.State.Treasury.Cash;
            Assert.That(CivicRules.Cost(sim, Watchman), Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 30_000 * Coin)));
            Assert.That(new[] { CivicRules.Cost(sim, Hall), CivicRules.Cost(sim, Playground) }, Is.EqualTo(new[] { Sim.Core.Market.MarketRules.Indexed(sim, 80_000 * Coin), Sim.Core.Market.MarketRules.Indexed(sim, 20_000 * Coin) }));
            Assert.That(new[] { CivicRules.Def(sim, Watchman).Tile, CivicRules.Def(sim, Hall).Tile, CivicRules.Def(sim, Playground).Tile }, Is.EqualTo(new[] { 91, 59, 107 }), "the plots approved from the map preview");

            Build(sim, Watchman);
            Assert.That(sim.State.Treasury.Cash, Is.EqualTo(cash - CivicRules.Cost(sim, Watchman)));
            Assert.That(CivicRules.Post(sim, Watchman).Built, Is.True);
            Assert.That(sim.PendingEvents.OfType<CivicBuilt>().Single().Civic, Is.EqualTo(Watchman));
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "watchman_post")).Reason, Is.EqualTo(RejectReason.PlotTaken), "its plot is taken");
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
        }

        // AC-8: a building needs the money: an empty treasury is refused, never the founder's cash.
        [Test]
        public void NeedsTheTownsMoney()
        {
            Simulation sim = Township(treasuryCoins: 0);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -sim.State.Treasury.Cash), new Posting(Account.GiftsFromFounder, sim.State.Treasury.Cash));
            long mine = sim.State.Ledger.Cash;
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "community_hall")).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(mine));
        }

        // AC-9: a post is advertised, a villager takes it within 24 hours, and his wage joins the budget; a playground has no post.
        [Test]
        public void Staff_Appointed()
        {
            Simulation sim = Township();
            Assert.That(sim.Execute(new PostCivicJob(sim.State.Clock.SimTime, Watchman)).Reason, Is.EqualTo(RejectReason.NoBuilding), "nothing built");
            Build(sim, Watchman);
            Build(sim, Playground);
            Assert.That(sim.Execute(new PostCivicJob(sim.State.Clock.SimTime, Playground)).Reason, Is.EqualTo(RejectReason.NoBuilding), "a playground has no post");
            Assert.That(CivicRules.Staffed(sim, Watchman), Is.False);
            Assert.That(TreasuryRules.LineActive(sim, TreasuryRules.Def(sim).Lines.First(l => l.Id == "watchman")), Is.False, "no wage until he is in post");
            Assert.That(CivicRules.Staffed(sim, Playground), Is.True, "no post to fill");

            long t = sim.State.Clock.SimTime;
            Ok(sim, new PostCivicJob(t, Watchman));
            Assert.That(sim.Execute(new PostCivicJob(t, Watchman)).Reason, Is.EqualTo(RejectReason.AlreadyFilled));
            Assert.That(sim.PendingEvents.OfType<PostAdvertised>().Single().At, Is.EqualTo(t + 24 * Hour));
            sim.AdvanceTo(t + 24 * Hour - 1);
            Assert.That(CivicRules.Post(sim, Watchman).Status, Is.EqualTo(PostStatus.Coming));
            sim.AdvanceTo(t + 24 * Hour);
            CivicPost p = CivicRules.Post(sim, Watchman);
            Assert.That(p.Status, Is.EqualTo(PostStatus.InPost));
            Assert.That(sim.PendingEvents.OfType<PostFilled>().Single().Name, Is.EqualTo(p.StaffName));
            Assert.That(sim.Config.Game.People.Names[p.StaffName].Female, Is.False, "a watchman, a man's name");
            Assert.That(CivicRules.Staffed(sim, Watchman), Is.True);
            Assert.That(TreasuryRules.LineActive(sim, TreasuryRules.Def(sim).Lines.First(l => l.Id == "watchman")), Is.True);
        }

        // AC-9: the wage is a Safety line and the old village's income; letting him go frees the post and it covers nobody until posted again.
        [Test]
        public void Staff_PaidAndDismissed()
        {
            Simulation sim = Township();
            Build(sim, Hall);
            Staff(sim, Hall);
            long savings = CohortRules.State(sim).Savings;
            long wage = CivicRules.Wage(sim, Hall);
            Assert.That(wage, Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 15_000 * Coin)));
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            CivicPost p = CivicRules.Post(sim, Hall);
            Assert.That(p.WageLast, Is.EqualTo(wage), "paid in full");
            Assert.That(p.UnpaidCloses, Is.EqualTo(0));
            Assert.That(CohortRules.State(sim).Savings, Is.GreaterThanOrEqualTo(savings + wage - 1), "the old village's income");
            Assert.That(-sim.State.Treasury.Reports.Last().Balance(Account.DeptSafety), Is.EqualTo(0).Or.LessThan(0), "Safety paid");

            Ok(sim, new DismissStaff(sim.State.Clock.SimTime, Hall));
            Assert.That(CivicRules.Staffed(sim, Hall), Is.False);
            Assert.That(sim.PendingEvents.OfType<PostVacated>().Single().Unpaid, Is.False);
            Assert.That(sim.Execute(new DismissStaff(sim.State.Clock.SimTime, Hall)).Reason, Is.EqualTo(RejectReason.NoStaff));
            Ok(sim, new PostCivicJob(sim.State.Clock.SimTime, Hall));
            sim.AdvanceTo(sim.State.Clock.SimTime + 24 * Hour);
            Assert.That((CivicRules.Post(sim, Hall).Hired, CivicRules.Staffed(sim, Hall)), Is.EqualTo((2, true)), "a second villager, a new name");
        }

        // AC-9: unpaid in full at two closes running he leaves; the post covers nobody until posted again.
        [Test]
        public void Staff_LeavesUnpaid()
        {
            Simulation sim = Township();
            Build(sim, Watchman);
            Staff(sim, Watchman);
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -sim.State.Treasury.Cash), new Posting(Account.GiftsFromFounder, sim.State.Treasury.Cash)); // empty the till
            sim.DrainEvents();
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(CivicRules.Post(sim, Watchman).UnpaidCloses, Is.EqualTo(1));
            Assert.That(CivicRules.Staffed(sim, Watchman), Is.True, "one close is a warning");
            Assert.That(sim.PendingEvents.OfType<PostUnpaid>().Single().Closes, Is.EqualTo(1));
            sim.State.Treasury.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, -sim.State.Treasury.Cash), new Posting(Account.GiftsFromFounder, sim.State.Treasury.Cash));
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Assert.That(CivicRules.Staffed(sim, Watchman), Is.False, "gone at the second");
            Assert.That(sim.PendingEvents.OfType<PostVacated>().Single().Unpaid, Is.True);
            Assert.That(CivicRules.EffectTown(sim, Watchman), Is.EqualTo(Fixed.Zero), "covers nobody");
        }

        // AC-8: safety is a fourth part of security only once a post stands; an unstaffed post is 0.5, a watchman in reach 1.0; a town that builds nothing loses nothing.
        [Test]
        public void Effects_Safety()
        {
            Simulation sim = Township();
            Household x = sim.State.Citizens.Households[0];
            HappinessRules.Update(sim, x);
            Fixed three = x.Security;
            Build(sim, Hall);
            Build(sim, Playground);
            HappinessRules.Update(sim, x);
            Assert.That(x.Security, Is.EqualTo(three), "no post, no fourth part");

            Build(sim, Watchman);
            HappinessRules.Update(sim, x);
            Fixed unstaffed = x.Security;
            Assert.That(CivicRules.Safety(sim, x), Is.EqualTo(Fixed.FromRatio(1, 2)), "a post with nobody in it is 0.5");
            Assert.That(unstaffed, Is.EqualTo((three * Fixed.FromInt(3) + Fixed.FromRatio(1, 2)).MulRatio(1, 4)));
            Staff(sim, Watchman);
            HappinessRules.Update(sim, x);
            Assert.That(CivicRules.Safety(sim, x), Is.EqualTo(Fixed.One), "a watchman in reach is 1.0");
            Assert.That(x.Security, Is.GreaterThanOrEqualTo(three), "never worse than without the post");
            Assert.That(x.Security, Is.GreaterThan(unstaffed));
        }

        // AC-8: the hall lifts Belonging 0.8 to 1.0 with its caretaker; the playground Leisure for households with children only.
        [Test]
        public void Effects_BelongingAndLeisure()
        {
            Simulation sim = Township();
            PopulationDefs p = sim.Config.Game.Population;
            Household kids = AddFamily(sim, 0, 30, 28, 8, 5);
            Household adults = sim.State.Citizens.Households[0];
            kids.Leisure = adults.Leisure = 10 * Coin;
            HappinessRules.Update(sim, kids); HappinessRules.Update(sim, adults);
            Assert.That((kids.Belonging, kids.Growth), Is.EqualTo((p.Belonging, p.Leisure)), "no school open: growth is its leisure");

            Build(sim, Hall);
            HappinessRules.Update(sim, kids);
            Assert.That(kids.Belonging, Is.EqualTo(p.Belonging), "no caretaker, no hall");
            Staff(sim, Hall);
            HappinessRules.Update(sim, kids);
            Assert.That(kids.Belonging, Is.EqualTo(Fixed.One), "0.8 to 1.0");

            Build(sim, Playground);
            HappinessRules.Update(sim, kids); HappinessRules.Update(sim, adults);
            Assert.That(CivicRules.Leisure(sim, kids, p.Leisure), Is.EqualTo(p.Leisure * Fixed.FromInt(2)), "0.30 to 0.60 with children");
            Assert.That(CivicRules.Leisure(sim, adults, p.Leisure), Is.EqualTo(p.Leisure), "adults alone get nothing from it");
        }

        // AC-7, AC-8: reach and capacity gate the benefit: a home beyond 700 m of the post is not covered; more people than its 300 scale it down.
        [Test]
        public void Effects_ReachAndCapacity()
        {
            Simulation sim = Township();
            Build(sim, Watchman);
            Staff(sim, Watchman);
            PopulationDefs pop = sim.Config.Game.Population;
            long far = sim.State.Land.NextBuildingId++, near = sim.State.Land.NextBuildingId++;
            sim.State.Land.Buildings.Add(new Building { Id = far, Def = pop.House, TileId = 0 });
            sim.State.Land.Buildings.Add(new Building { Id = near, Def = pop.House, TileId = 90 });
            Household a = AddFamily(sim, far, 30, 28), b = AddFamily(sim, near, 30, 28);
            Assert.That(CoverageRules.Metres(sim, 0, 91), Is.EqualTo(1600));
            Assert.That(CivicRules.Effect(sim, Watchman, a), Is.EqualTo(Fixed.Zero), "1,600 m is out of the 700 m reach");
            Assert.That(CivicRules.Effect(sim, Watchman, b), Is.EqualTo(Fixed.One), "100 m away, full");

            TownshipTests.Families(sim, 80); // past 300 people
            int served = CivicRules.Served(sim, Watchman);
            Assert.That(served, Is.GreaterThan(300));
            Assert.That(CivicRules.Scale(sim, Watchman), Is.EqualTo(Fixed.FromRatio(300, served)), "capacity ÷ people, never a cliff");
            Assert.That(CivicRules.Effect(sim, Watchman, b), Is.LessThan(Fixed.One));
        }

        // AC-8: the card's budget guard: what it adds a season to Safety (upkeep + wage) and how short the town would be.
        [Test]
        public void Guard()
        {
            Simulation sim = Township();
            CivicRules.Guard(sim, "watchman_post", out long added, out long shortBy);
            Assert.That(added, Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 25_500 * Coin)), "1,500 upkeep and a 24,000 wage");
            CivicRules.Guard(sim, "playground", out added, out _);
            Assert.That(added, Is.EqualTo(Sim.Core.Market.MarketRules.Indexed(sim, 1_000 * Coin)), "a playground has no wage");

            long income = TreasuryRules.ExpectedIncome(sim), needs = 0;
            for (int d = 0; d < TreasuryState.Departments; d++) needs += TreasuryRules.Needs(sim, d);
            CivicRules.Guard(sim, "community_hall", out added, out shortBy);
            Assert.That(shortBy, Is.EqualTo(System.Math.Max(0, needs + added - income)));
        }

        // A town with nothing built is the Township as before: the three posts add nothing to the hash until one is commissioned.
        [Test]
        public void NothingBuilt_NothingChanges()
        {
            Simulation sim = TownshipTests.Start(cohort: false);
            Assert.That(sim.State.Township.IsDefault, Is.True);
            Assert.That(sim.State.Township.Civic.All(c => !c.Built && c.Status == PostStatus.Vacant), Is.True);
        }

        // AC-12: a town with all three, staffed, saves and loads to the same hash and runs on identically.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Township();
            Build(sim, Watchman); Build(sim, Hall); Build(sim, Playground);
            Staff(sim, Watchman); Staff(sim, Hall);
            sim.AdvanceTo(TreasuryTests.NextClose(sim));
            Saves.LoadResult r = Saves.SaveCodec.Read(Saves.SaveCodec.Write(sim.State, sim.Config, 0), sim.Config);
            Assert.That(r.Error, Is.EqualTo(Saves.SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Township.Civic.Select(c => (c.Built, c.Status, c.StaffName, c.WageLast)), Is.EqualTo(sim.State.Township.Civic.Select(c => (c.Built, c.Status, c.StaffName, c.WageLast))));
        }
    }
}
