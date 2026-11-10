using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Finance;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 018 §3.1 (US-131): the first school, built as a gift to the village (ADR 0095 §5).</summary>
    public class SchoolTests
    {
        private const long Price = 105_000 * Coin;

        private static void Hamlet(Simulation sim) { sim.State.Era.Current = sim.Config.Game.School.UnlockEra; }

        // AC-1: from the Hamlet, BuildSchool costs its price (cash or a loan), opens after 48 h, and is refused before the Hamlet or twice.
        [Test]
        public void Build()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new BuildSchool(t)).Reason, Is.EqualTo(RejectReason.EraLocked), "before the Hamlet");
            Hamlet(sim);
            long cash = sim.State.Ledger.Cash;
            Ok(sim, new BuildSchool(t));
            SchoolState s = sim.State.Citizens.School;
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(Price), "90,000 building, 15,000 benches, slates and books");
            Assert.That((s.Status, s.ReadyAt, s.Cost), Is.EqualTo((SchoolStatus.Building, t + 48 * Hour, Price)));
            Assert.That(sim.Execute(new BuildSchool(t)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt), "once");
            Assert.That(SchoolRules.Pupils(sim), Is.Empty, "no pupils while it's built");

            sim.AdvanceTo(t + 48 * Hour - 1);
            Assert.That(s.Status, Is.EqualTo(SchoolStatus.Building));
            sim.AdvanceTo(t + 48 * Hour);
            Assert.That(s.Status, Is.EqualTo(SchoolStatus.Open), "the district posts its teacher after 48 h");
            Assert.That(SchoolRules.Teachers(sim), Is.EqualTo(1));
            Assert.That(sim.Execute(new BuildSchool(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.AlreadyBuilt), "one school in Phase 2");

            // With a co-op loan within the bank's room: the down payment from cash, the rest owed; the school adds nothing to the room.
            Simulation b = Start();
            Hamlet(b);
            Ok(b, new BuyParcel(t, "w1")); // land of your own the bank lends on (the inherited acres are protected)
            Ok(b, new BuyParcel(t, "n3"));
            int bank = b.Config.Game.Credit.Lenders.ToList().FindIndex(l => l.Id == "coop_bank");
            long room = CreditRules.Limit(b, bank).Limit, loan = System.Math.Min(Price, room) / (1000 * Coin) * (1000 * Coin);
            Assert.That(loan, Is.GreaterThanOrEqualTo(b.Config.Game.Credit.Lenders[bank].MinAmount), "the test town can borrow");
            Assert.That(b.Execute(new BuildSchool(t, new Financing("coop_bank", Price + Coin, 8))).Reason, Is.EqualTo(RejectReason.InvalidAmount), "never more than it costs");
            cash = b.State.Ledger.Cash;
            Ok(b, new BuildSchool(t, new Financing("coop_bank", loan, 8)));
            Assert.That(cash - b.State.Ledger.Cash, Is.EqualTo(Price - loan));
            Assert.That(-b.State.Ledger.Balance(Account.BankLoans), Is.EqualTo(loan));
            Assert.That(CreditRules.Limit(b, bank).Limit, Is.LessThanOrEqualTo(room - loan + Coin), "the school isn't collateral");
        }

        // ADR 0095 §5 (AC-1): the cost, the upkeep at each close and the classes are Gifts to the village: not an asset, not farm profit.
        [Test]
        public void Gift_NotFarmProfit()
        {
            Simulation sim = Start();
            Hamlet(sim);
            long t = sim.State.Clock.SimTime, buildings = sim.State.Ledger.Balance(Account.Buildings);
            long worth = Reporting.NetWorth(sim).Total;
            Ok(sim, new BuildSchool(t));
            Assert.That(sim.State.Ledger.Balance(Account.GiftsToVillage), Is.EqualTo(Price));
            Assert.That(sim.State.Ledger.Balance(Account.Buildings), Is.EqualTo(buildings), "not your building");
            Assert.That(worth - Reporting.NetWorth(sim).Total, Is.EqualTo(Price), "net worth falls by the gift");

            sim.AdvanceTo(t + sim.Clock.SecondsPerSeason - t % sim.Clock.SecondsPerSeason); // the season's close
            SeasonReport r = sim.State.Finance.Reports.Last();
            Assert.That(r.Line("gifts").Amount, Is.EqualTo(-Price));
            Assert.That(r.Line("farmProfit").Accounts, Does.Not.Contain(Account.GiftsToVillage), "never farm profit");
            Assert.That(r.FarmProfit, Is.EqualTo(r.Line("farmIncome").Amount + r.Line("farmCosts").Amount));
            Assert.That(r.Line("netResult").Amount, Is.EqualTo(new[] { "farmProfit", "otherIncome", "household", "gifts", "finance", "other" }.Sum(k => r.Line(k).Amount)));

            // The school open, the close takes its upkeep for the new season, also a gift.
            JournalEntry upkeep = sim.State.Ledger.Entries.Last(e => e.DescriptionKey == "ledger.schoolUpkeep");
            Assert.That(upkeep.Period, Is.EqualTo(sim.Period));
            Assert.That(upkeep.Postings.Single(p => p.Account == Account.GiftsToVillage).Amount, Is.EqualTo(2_000 * Coin));
        }
    
        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        /// <summary>A Hamlet town with a hand (a hut to live in), plenty of cash and an open school started at a season's start.</summary>
        private static Simulation Schooled(out Hand hand, params int[] ages)
        {
            Simulation sim = Start();
            Hamlet(sim);
            hand = HireHand(sim);
            sim.AdvanceTo(NextClose(sim));
            CashTo(sim, 10_000_000 * Coin);
            Household x = AddFamily(sim, hand.HutId, ages);
            x.Savings = 1_000_000 * Coin; // fed all along, so nobody leaves
            Ok(sim, new BuildSchool(sim.State.Clock.SimTime));
            sim.AdvanceTo(SchoolRules.State(sim).ReadyAt);
            return sim;
        }

        private static Citizen[] Family(Simulation sim, int n) { return sim.State.Citizens.Households.Last().Members.Take(n).Select(id => sim.State.Citizens.Citizen(id)).ToArray(); }

        // AC-2: children of 6–13 who can't read go to school; a whole season open counts a year (not the season it opened in); after 4
        // they can read, the 13-year-old ages out to the adult class.
        [Test]
        public void Literacy()
        {
            Simulation sim = Schooled(out _, 5, 9, 13); // their ages in the season the school opens, part-way through
            Citizen[] k = Family(sim, 3);
            Citizen young = k[0], mid = k[1], old = k[2];
            Assert.That(SchoolRules.Pupils(sim), Is.EqualTo(new[] { mid, old }), "6–13, not yet 5");
            Assert.That((SchoolRules.SeasonsToFinish(sim, mid), SchoolRules.SeasonsToFinish(sim, old)), Is.EqualTo((5, -1)),
                "this part-season doesn't count, then 4; 13 turns 14 first");
            sim.AdvanceTo(NextClose(sim));
            Assert.That(k.Select(c => c.SchoolYears), Is.All.EqualTo(0), "the school opened part-way through the season: no year");
            Assert.That(SchoolRules.Pupils(sim), Is.EqualTo(new[] { young, mid }), "6 and 10 now; 14 is past school");

            for (int year = 1; year <= 4; year++)
            {
                Assert.That(young.Literate || mid.Literate, Is.False, "not before 4 years");
                sim.AdvanceTo(NextClose(sim));
                Assert.That((young.SchoolYears, mid.SchoolYears, old.SchoolYears), Is.EqualTo((year, year, 0)));
            }
            Assert.That((young.Education, mid.Education), Is.EqualTo((1, 1)), "primary finished: they read");
            Assert.That(sim.PendingEvents.OfType<Events.CitizenLiterate>().Select(e => (e.CitizenId, e.How)),
                Is.EqualTo(new[] { (young.Id, Events.LiterateHow.School), (mid.Id, Events.LiterateHow.School) }));
            Assert.That(SchoolRules.Pupils(sim), Is.Empty, "readers don't go; mid is 14");
            Assert.That(SchoolRules.Learners(sim), Does.Contain(old), "the class takes who the school missed");
        }

        // AC-2 (ADR 0095 §4): the class takes up to 15 aged 14+ who can't read, working-age first then elders, by id; paid at the start;
        // one at a time; who leaves leaves the class; the rest read 96 h after it starts.
        [Test]
        public void AdultClass_96h()
        {
            Simulation sim = Start();
            Hamlet(sim);
            Hand hand = HireHand(sim);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new StartAdultClass(t)).Reason, Is.EqualTo(RejectReason.NoSchool));
            CashTo(sim, 10_000_000 * Coin);
            Ok(sim, new BuildSchool(t));
            sim.AdvanceTo(SchoolRules.State(sim).ReadyAt);
            sim.State.Citizens.OfHand(hand.Id).Education = 1;
            Household x = AddFamily(sim, hand.HutId, 65, 14, 30, 6);
            Household y = AddFamily(sim, hand.HutId, Enumerable.Repeat(25, 14).ToArray());
            x.Savings = y.Savings = 1_000_000 * Coin;
            Citizen elder = sim.State.Citizens.Citizen(x.Members[0]);
            Citizen teen = sim.State.Citizens.Citizen(x.Members[1]);
            Citizen sixteenth = sim.State.Citizens.Citizen(y.Members[13]);

            List<Citizen> join = SchoolRules.Learners(sim);
            Assert.That(join.Count, Is.EqualTo(15));
            Assert.That(join.Take(2), Is.EqualTo(new[] { teen, sim.State.Citizens.Citizen(x.Members[2]) }), "working-age by id: the elder waits");
            Assert.That(join, Has.None.EqualTo(elder).And.None.EqualTo(sixteenth), "15 places");

            t = sim.State.Clock.SimTime;
            long cash = sim.State.Ledger.Cash, gifts = sim.State.Ledger.Balance(Account.GiftsToVillage);
            Ok(sim, new StartAdultClass(t));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(3_000 * Coin), "paid at the start");
            Assert.That(sim.State.Ledger.Balance(Account.GiftsToVillage) - gifts, Is.EqualTo(3_000 * Coin), "a gift to the village");
            Assert.That(sim.Execute(new StartAdultClass(t)).Reason, Is.EqualTo(RejectReason.ClassRunning), "one at a time");
            Citizen gone = join[5];
            gone.Gone = CitizenGone.MarriedAway; // left the town: left the class
            gone.GoneAt = t;
            sim.State.Citizens.Household(gone.HouseholdId).Members.Remove(gone.Id);

            sim.AdvanceTo(t + 96 * Hour - 1);
            Assert.That(join.Any(c => c.Literate), Is.False);
            sim.AdvanceTo(t + 96 * Hour);
            Assert.That(join.Where(c => c != gone).Select(c => c.Education), Is.All.EqualTo(2), "they read 96 h after the start");
            Assert.That((gone.Literate, elder.Literate, sixteenth.Literate), Is.EqualTo((false, false, false)));
            Assert.That(SchoolRules.ClassRunning(sim), Is.False);

            Assert.That(SchoolRules.Learners(sim), Is.EqualTo(new[] { sixteenth, elder }), "the next class: working-age first, then the elder");
            CashTo(sim, 0);
            Assert.That(sim.Execute(new StartAdultClass(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            CashTo(sim, 10_000_000 * Coin);
            Ok(sim, new StartAdultClass(sim.State.Clock.SimTime));
            sim.AdvanceTo(sim.State.Clock.SimTime + 96 * Hour);
            Assert.That(sim.Execute(new StartAdultClass(sim.State.Clock.SimTime)).Reason, Is.EqualTo(RejectReason.NobodyToTeach), "the child is the school's");
        }

        // AC-3: the literacy rate counts the living aged 7+, whole percent rounded down.
        [Test]
        public void Rate()
        {
            Simulation sim = Schooled(out Hand hand, 3, 6, 7, 30, 40);
            Citizen[] k = Family(sim, 5);
            sim.State.Citizens.OfHand(hand.Id).Education = 0;
            Assert.That(SchoolRules.LiteracyRate(sim), Is.EqualTo(0));
            k[3].Education = 1;
            SchoolRules.Literacy(sim, out int literate, out int counted);
            Assert.That((literate, counted), Is.EqualTo((1, 4)), "the hand, 7, 30 and 40; not 3 or 6");
            Assert.That(SchoolRules.LiteracyRate(sim), Is.EqualTo(25));
            k[4].Gone = CitizenGone.Died;
            Assert.That(SchoolRules.LiteracyRate(sim), Is.EqualTo(33), "1 of 3, rounded down");
            k[2].Education = 2;
            Assert.That(SchoolRules.LiteracyRate(sim), Is.EqualTo(66), "2 of 3, rounded down");
        }
    }
}
