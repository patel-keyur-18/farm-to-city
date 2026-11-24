using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.History;
using Sim.Core.People;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>
    /// Spec 028 AC-26 and AC-30, spec 029 AC-16 (Sprint 26 review): the ledgers stay exact through the kirana's khata and marketing, the supervisor's wage and the timed cards' bills, and a story's leaving; nothing a
    /// story does moves a coin; a town that answers nothing still works.
    /// </summary>
    public class GrowthLedgerTests
    {
        private const long Coin = 100;

        private static void AllReconcile(Simulation sim)
        {
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Treasury.Ledger.Reconcile(), Is.Null);
            foreach (Business.Business b in sim.State.Businesses.Businesses) Assert.That(b.Ledger.Reconcile(), Is.Null, "business " + b.Id);
        }

        /// <summary>A Township with a kirana on khata and marketing, six hands and a supervisor, three cards waiting.</summary>
        private static Simulation Town(out Business.Business shop)
        {
            Simulation sim = InvoiceTests.Township();
            for (int i = 0; i < 12; i++) KiranaTests.Family(sim, ShopRules.Defs(sim).SquareTile, 8_000);
            shop = KiranaTests.Open(sim, true, 0);
            Ok(sim, new SetKhata(sim.State.Clock.SimTime, shop.Id, true));
            Ok(sim, new SetKhataLimit(sim.State.Clock.SimTime, shop.Id, ShopRules.Defs(sim).KhataLimits[ShopRules.Defs(sim).KhataLimits.Count - 1]));
            Ok(sim, new SetMarketing(sim.State.Clock.SimTime, shop.Id, ShopRules.Defs(sim).MarketBudgets[1]));
            CashTo(sim, 9_000_000 * Coin);
            Hand first = HireHand(sim);
            while (P(sim).Hands.Count < 6)
            {
                Hand h = first.Clone();
                h.Id = P(sim).NextHandId++;
                h.PrepaymentId = 0;
                P(sim).Hands.Add(h);
            }
            WorkplaceKit.Supervise(sim, 60);
            WorkplaceKit.Card(sim, HrEventKind.Accident, 0, P(sim).Hands[0].Id, amount: 166_700).Cost = 200_000;
            WorkplaceKit.Card(sim, HrEventKind.Conflict, 0, P(sim).Hands[1].Id, P(sim).Hands[2].Id);
            WorkplaceKit.Card(sim, HrEventKind.Association, 0, P(sim).Hands[3].Id);
            return sim;
        }

        // AC-26: two seasons of khata, marketing, a supervisor's wage and the three cards' defaults: every book (the founder's, each business's, the treasury's) reconciles to the coin at each close
        // (the sim's strict checks throw otherwise) and in the end; a khata shop's receivable is what its households owe.
        [Test]
        public void Reconciles()
        {
            Simulation sim = Town(out Business.Business shop);
            for (int d = 0; d < 32; d++) InvoiceTests.ToHour(sim, 7, 1);
            AllReconcile(sim);
            Assert.That(shop.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(KhataRules.Owed(shop)), "the receivable is what households owe");
            Assert.That(P(sim).HrEvents.Where(e => e.Kind >= HrEventKind.Accident).All(e => e.ExpiresAt > sim.State.Clock.SimTime && e.Id > 3), Is.True, "the first three cards took their defaults; any timed card now waiting is a later one still inside its day");
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey == "ledger.supervisorWage"), Is.True, "the supervisor was paid");
        }

        // AC-30: a town that answers nothing (cards left to their deadline, khata and marketing never touched again, no card read) still works: the treasury never below zero, every book exact, the farm still has its hands.
        [Test]
        public void LeftAlone()
        {
            Simulation sim = Town(out _);
            for (int d = 0; d < 48; d++) InvoiceTests.ToHour(sim, 7, 1);
            Assert.That(sim.State.Treasury.Cash, Is.GreaterThanOrEqualTo(0));
            AllReconcile(sim);
            Assert.That(P(sim).Hands.Count, Is.GreaterThan(0), "the farm still has hands");
            Assert.That(P(sim).HrEvents.Count(e => e.Kind >= HrEventKind.Accident), Is.LessThanOrEqualTo(3), "timed cards never pile up: each takes its default at its deadline");
        }

        // AC-16 (spec 029): a hand's leaving to start a stall moves no money (no severance, nothing paid by the story) and the books stay exact; the same for a worker of a business.
        [Test]
        public void A_Stall_Moves_No_Money()
        {
            foreach (bool business in new[] { false, true })
            {
                Simulation sim = TownshipTests.Start(false, (f, j) => f == "stories.json" ? j.Replace("\"chance\": 0.2", "\"chance\": 1.0") : j);
                TreasuryTests.Township(sim);
                Household x = AddFamily(sim, 5, 30, 28);
                x.Savings = 50_000 * Coin;
                Citizen c = sim.State.Citizens.Citizen(x.Members[0]);
                long who;
                if (business)
                {
                    long give = 3_000_000 * Coin - sim.State.Ledger.Cash;
                    if (give > 0) sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, give), new Posting(Account.Sales, -give));
                    Ok(sim, new FoundBusiness(sim.State.Clock.SimTime, "tea_stall", 0, 100_000 * Coin, false, null, 0));
                    Business.Business b = sim.State.Businesses.Businesses.Last();
                    sim.AdvanceTo(b.ReadyAt + 60);
                    var a = new Applicant { Id = sim.State.People.NextApplicantId++, Skill = 50, Trait1 = 0, Trait2 = 0, Place = b.Id, CitizenId = c.Id };
                    HiringRules.HireWorker(sim, a, HiringRules.OfferWage(sim, a, OfferLevel.Asked), 0);
                    Worker w = sim.State.People.WorkerAt(b.Id);
                    w.SeasonsWorked = 3;
                    who = w.Id;
                    ProfitFirst(b);
                }
                else
                {
                    var a = new Applicant { Id = 9_000 + c.Id, Name = c.Name, Skill = 50, Trait1 = c.Trait1, Trait2 = c.Trait2, CitizenId = c.Id };
                    Hand h = PeopleSystem.Hire(sim, a, 30_000 * Coin, 0);
                    h.SeasonsWorked = 3;
                    who = h.Id;
                }
                sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
                Assert.That(sim.State.Chronicle.Stories.Arcs.Exists(s => s.Citizen == c.Id && s.Arc == StoryArc.Stall && s.Step == 1), Is.True, "gave notice");
                sim.DrainEvents();
                sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
                Assert.That(sim.State.Chronicle.Stories.Arcs.Exists(s => s.Citizen == c.Id && s.Arc == StoryArc.Stall && s.Step == 2), Is.True, "left and opened a stall: " + (business ? "a worker of a business" : "a hand"));
                Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey == "ledger.severance" && e.Reference.EndsWith(":" + who)), Is.False, "no severance");
                Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey.Contains("stall") || e.DescriptionKey.Contains("story")), Is.False, "a story posts nothing");
                AllReconcile(sim);
            }
        }

        private static void ProfitFirst(Business.Business b) { b.ProfitableSeasons = 1; } // a stall of his trade waits for your shop's first profit (the rule): this one has had it
    }
}
