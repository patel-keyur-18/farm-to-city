using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Progress;
using Sim.Core.Saves;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-103 (spec 023 §3.4, §3.5, ADR 0116): the Township's gate, its unlock and the old village joining.</summary>
    public class TownshipTests
    {
        private static void Gift(Simulation sim, long amount)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.Cash, amount), new Posting(Account.Sales, -amount));
        }

        /// <summary>A Village town, rich; the old village in or out.</summary>
        internal static Simulation Start(bool cohort = false, System.Func<string, string, string> edit = null)
        {
            TestContent.WithCohort = cohort;
            try
            {
                Simulation sim = TreasuryTests.Start(edit);
                Gift(sim, 5_000_000 * Coin);
                sim.DrainEvents();
                return sim;
            }
            finally { TestContent.WithCohort = false; }
        }

        /// <summary>Families of yours with their savings high (so nobody leaves) and everyone able to read.</summary>
        internal static void Families(Simulation sim, int n)
        {
            for (int i = 0; i < n; i++)
            {
                Household x = AddFamily(sim, 0, 30, 28);
                x.Savings = 1_000_000 * Coin;
                foreach (long id in x.Members) sim.State.Citizens.Citizen(id).Education = 1;
            }
        }

        /// <summary>Three open businesses with a profitable season each, as the gate counts them (no books: don't advance the clock after).</summary>
        internal static void Businesses(Simulation sim, int n)
        {
            for (int i = 0; i < n; i++)
                sim.State.Businesses.Businesses.Add(new Business.Business { Id = 900 + i, Status = BusinessStatus.Open, ProfitableSeasons = 1 });
        }

        // AC-4: the five goals; each reads its own number; the gate holds only when all do.
        [Test]
        public void Gate()
        {
            Simulation sim = Start();
            Assert.That(TownshipRules.Met(sim), Is.False);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Households), Is.False);
            int have = EraRules.Households(sim);
            Families(sim, 14 - have);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Households), Is.False, "14 households");
            Families(sim, 1);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Households), Is.True, "15 households");
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Literacy), Is.True);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Clinic), Is.False);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Road), Is.False);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Businesses), Is.False);
            Businesses(sim, 2);
            Assert.That(TownshipRules.Businesses(sim), Is.EqualTo(2));
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Businesses), Is.False, "two is not three");
            Businesses(sim, 1);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Businesses), Is.True);
            sim.State.Businesses.Businesses[0].Status = BusinessStatus.Closed;
            Assert.That(TownshipRules.Businesses(sim), Is.EqualTo(2), "a closed business doesn't count");
            sim.State.Businesses.Businesses[0].Status = BusinessStatus.Open;
            sim.State.Businesses.Businesses[1].ProfitableSeasons = 0;
            Assert.That(TownshipRules.Businesses(sim), Is.EqualTo(2), "an open business with no profitable season doesn't count");
            sim.State.Businesses.Businesses[1].ProfitableSeasons = 1;
        }

        [Test]
        public void Literacy()
        {
            Simulation sim = Start();
            Families(sim, 20);
            Assert.That(TownshipRules.Literacy(sim), Is.EqualTo(100));
            foreach (Citizen c in sim.State.Citizens.Citizens.Where(c => c.Id % 2 == 0)) c.Education = 0;
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Literacy), Is.False, "half can't read");
            Assert.That(TownshipRules.LiteracyNeed(sim), Is.EqualTo(60));
        }

        // AC-4, AC-5: the gate unlocks the Township once; the treasury opens, the old village joins; a goal once met stays met.
        [Test]
        public void Unlock_Once()
        {
            Simulation sim = Start(cohort: true);
            Families(sim, 20);
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildClinic(t));
            Ok(sim, new BuildDoctorQuarters(t));
            Ok(sim, new PetitionRoad(t));
            int village = sim.State.Era.Current;
            sim.AdvanceTo(t + 72 * Hour);
            Assert.That(ClinicRules.InPost(sim), Is.True);
            Assert.That(sim.State.Era.Current, Is.EqualTo(village), "the road is not built yet");
            Assert.That(sim.State.Township.GateMet & TownshipGoal.Clinic, Is.Not.EqualTo(0), "the clinic goal latched");
            sim.DrainEvents();
            sim.AdvanceTo(t + 4 * 86400);
            Assert.That(RoadRules.IsBuilt(sim), Is.True);
            Assert.That(sim.State.Era.Current, Is.EqualTo(village), "households, clinic, road and literacy: the businesses are missing");
            Assert.That(sim.State.Township.GateMet, Is.EqualTo(TownshipGoal.All & ~TownshipGoal.Businesses));
            sim.State.Ledger.Reconcile();
            Businesses(sim, 3);
            EraRules.OnTown(sim); // the third business's first profitable season would have done this at its close
            Businesses(sim, 0);
            Assert.That(sim.State.Era.Current, Is.EqualTo(village + 1), "the last goal: the Township");
            Assert.That(sim.State.Era.UnlockedAt, Is.EqualTo(sim.State.Clock.SimTime), "at once, not at the next close");
            sim.State.Businesses.Businesses.Clear();
            Assert.That(sim.PendingEvents.OfType<EraUnlocked>().Single().EraId, Is.EqualTo("township"));
            Assert.That(TreasuryRules.IsOpen(sim), Is.True, "the treasury opened");
            Assert.That(CohortRules.Joined(sim), Is.True, "the old village joined");
            Assert.That(sim.PendingEvents.OfType<CohortJoined>().Single().People, Is.EqualTo(150));
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(sim.PendingEvents.OfType<EraUnlocked>().Count(), Is.EqualTo(1), "once");
        }

        // A goal never un-meets: a household leaving after the check doesn't take the Township back, and the gate waits for the others.
        [Test]
        public void Goals_NeverUnmeet()
        {
            Simulation sim = Start();
            Families(sim, 20);
            EraRules.OnTown(sim);
            Assert.That(sim.State.Township.GateMet & TownshipGoal.Households, Is.Not.EqualTo(0));
            sim.State.Citizens.Households.RemoveRange(0, 8);
            Assert.That(TownshipRules.Holds(sim, TownshipGoal.Households), Is.False);
            Assert.That(TownshipRules.IsMet(sim, TownshipGoal.Households), Is.True, "met for good");
            Assert.That(sim.PendingEvents.OfType<GateGoalMet>().Count(e => e.Goal == TownshipGoal.Households), Is.LessThanOrEqualTo(1), "announced once");
        }

        // AC-12: a town with the gate's progress and the old village saves and loads to the same hash.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start(cohort: true);
            Families(sim, 20);
            EraRules.OnTown(sim);
            TreasuryTests.Township(sim);
            Assert.That(CohortRules.Joined(sim), Is.True);
            SimConfig config = sim.Config;
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(r.State.Township.Cohort.Households, Is.EqualTo(30));
            Assert.That(r.State.Township.GateMet, Is.EqualTo(sim.State.Township.GateMet));
        }

        // Carried item 2 (ADR 0118): house tax you owe the town is a liability in net worth, so it can't be hidden by not paying (schema 20's field).
        [Test]
        public void OwedTax_InNetWorth()
        {
            Simulation sim = Start();
            NetWorthSnapshot before = Reporting.NetWorth(sim);
            Assert.That(before.TaxOwed, Is.EqualTo(0));
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test", "test", new Posting(Account.HouseTax, 50_000 * Coin), new Posting(Account.TaxOwed, -50_000 * Coin));
            NetWorthSnapshot after = Reporting.NetWorth(sim);
            Assert.That(after.TaxOwed, Is.EqualTo(50_000 * Coin));
            Assert.That(after.Total, Is.EqualTo(before.Total - 50_000 * Coin), "what you owe the town counts against you");
            Assert.That(TaxRules.YouOwe(sim), Is.EqualTo(50_000 * Coin));
        }

        // The Township adds nothing to the hash until something starts.
        [Test]
        public void NothingStarted_AddsNothingToTheHash()
        {
            Simulation sim = Start();
            Assert.That(sim.State.Township.IsDefault, Is.True);
            ulong with = sim.State.ComputeHash();
            sim.State.Township = null;
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(with));
        }
    }
}
