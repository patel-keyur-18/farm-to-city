using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Town;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>US-141 (spec 022 §3.5, §3.6, ADR 0118): the five policies, their slots, lags, pauses and effects.</summary>
    public class PolicyTests
    {
        private static TreasuryState T(Simulation sim) { return sim.State.Treasury; }

        /// <summary>A town whose treasury can pay every policy for years (the start-up grant 100 times the usual), so a pause is only what a test makes.</summary>
        private static Simulation Start(System.Func<string, string, string> edit = null)
        {
            Simulation sim = TreasuryTests.Start(edit ?? ((f, j) => f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": 10000000") : j));
            TreasuryTests.Family(sim);
            TreasuryTests.Township(sim);
            sim.DrainEvents();
            return sim;
        }

        private static void Close(Simulation sim) { sim.AdvanceTo(TreasuryTests.NextClose(sim)); }
        private static void Enact(Simulation sim, PolicyId p) { Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, p)); }
        private static RejectReason Try(Simulation sim, Command c) { return sim.Execute(c).Reason; }

        // AC-9 (subsidy): paid from the first close, no effect until its lag has passed; its supply factor 1.05; on repeal 0.97 for two seasons, then normal.
        [Test]
        public void Subsidy()
        {
            Simulation sim = Start();
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.One));
            Enact(sim, PolicyId.Subsidy);
            Assert.That(PolicyRules.SettingUp(sim, PolicyId.Subsidy), Is.EqualTo(2), "paid at the next close, the effect a close after that");
            Close(sim);
            Assert.That(PolicyRules.Of(sim, PolicyId.Subsidy).Status, Is.EqualTo(PolicyStatus.Active));
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.One), "setting up");
            long cost = PolicyRules.Cost(sim, PolicyId.Subsidy);
            Assert.That(T(sim).Reports.Last().Rows[(int)Dept.Policies].Paid, Is.EqualTo(cost), "a department line, in the budget");
            Close(sim);
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.Parse("1.05")));
            Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.Subsidy));
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.Parse("1.05")), "a repeal takes effect at the next close");
            Close(sim);
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.Parse("0.97")), "the dependency");
            Close(sim);
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.Parse("0.97")));
            Close(sim);
            Assert.That(PolicyRules.SupplyFactor(sim), Is.EqualTo(Fixed.One), "then normal");
        }

        // AC-9 (minimum wage): the floor on asks and renewals, the clerk's wage and a labourer's day, from the first paid close; never on a wage still running.
        [Test]
        public void MinimumWage()
        {
            Simulation sim = Start();
            long asked = PeopleRules.AskedWage(sim, Fixed.FromInt(50), false);
            long day = PeopleRules.LabourCost(sim, 1, sim.State.Clock.SimTime + 86_400);
            var clerk = sim.Config.Game.Treasury.Lines.First(l => l.Source == LineAmount.ClerkWage);
            long clerkWage = TreasuryRules.LineAmount(sim, clerk);
            Enact(sim, PolicyId.MinWage);
            Assert.That(PeopleRules.AskedWage(sim, Fixed.FromInt(50), false), Is.EqualTo(asked), "nothing changes before it is paid");
            Close(sim);
            Assert.That(PolicyRules.Effective(sim, PolicyId.MinWage), Is.True);
            Assert.That(PeopleRules.AskedWage(sim, Fixed.FromInt(50), false), Is.EqualTo(Money.RoundToCoin(Money.MulRate(asked, Fixed.Parse("1.10")))).Within(100 * Coin));
            Assert.That(PeopleRules.LabourCost(sim, 1, sim.State.Clock.SimTime + 86_400), Is.GreaterThan(day), "the labourer's day wage");
            Assert.That(TreasuryRules.LineAmount(sim, clerk), Is.GreaterThan(clerkWage), "the town's own clerk too");
            Assert.That(PolicyRules.WageFloor(sim, 1, 30_000 * Coin), Is.GreaterThan(30_000 * Coin), "never below the floor");
        }

        // AC-9 (rent control): a renewal's rise is capped at 3% instead of 10%; nothing else changes.
        [Test]
        public void RentControl()
        {
            Simulation sim = Start();
            Household x = sim.State.Citizens.Households.First(h => HouseholdRules.InFamilyHouse(sim, h));
            x.LeaseRent = 1_000 * Coin;
            x.Income = 1_000_000 * Coin;
            long usual = LeaseRules.Options(sim, x).Allowed;
            Assert.That(usual, Is.EqualTo(1_100 * Coin), "the usual cap, 10%");
            Enact(sim, PolicyId.RentControl);
            Close(sim);
            x.LeaseRent = 1_000 * Coin;
            x.Income = 1_000_000 * Coin;
            Assert.That(LeaseRules.Options(sim, x).Allowed, Is.EqualTo(1_030 * Coin), "rent control's 3%");
        }

        // AC-9 (grant): paid from the treasury, never to you; once running, each close either wastes it (40%) or opens a door for a newcomer.
        [Test]
        public void Grant()
        {
            Simulation sim = Start();
            Enact(sim, PolicyId.Grant);
            Close(sim);
            Assert.That(PolicyRules.GrantBoost(sim), Is.False, "setting up: no effect yet");
            int wasted = 0, opened = 0;
            for (int i = 0; i < 12; i++)
            {
                sim.DrainEvents();
                Close(sim);
                if (sim.PendingEvents.OfType<GrantWasted>().Any()) { wasted++; Assert.That(PolicyRules.GrantBoost(sim), Is.False); }
                else { opened++; Assert.That(PolicyRules.GrantBoost(sim), Is.True); }
            }
            Assert.That(wasted, Is.InRange(1, 11), "a share of grants come to nothing");
            Assert.That(opened, Is.GreaterThan(0));
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey.Contains("grant", System.StringComparison.OrdinalIgnoreCase) && e.DescriptionKey.StartsWith("ledger.town")), Is.False, "nothing of it on your books");
        }

        // AC-9 (meals): needs the school; once it runs, a pupil's household needs a quarter of a child's food less, and the pupil's health rises a little more.
        [Test]
        public void Meals()
        {
            Simulation sim = Start();
            Assert.That(Try(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Meals)), Is.EqualTo(RejectReason.NeedsBuilding), "no school, no meals");
            int township = sim.State.Era.Current;
            TreasuryTests.Era(sim, "hamlet");
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildSchool(t));
            sim.AdvanceTo(t + 48 * Hour);
            sim.State.Era.Current = township;
            Household x = sim.State.Citizens.Households.First(h => h.Members.Any(id => SchoolRules.IsPupil(sim, sim.State.Citizens.Citizen(id))));
            HouseholdRules.SetBudget(sim, x);
            long need = x.FoodNeed;
            Assert.That(PolicyRules.Cost(sim, PolicyId.Meals), Is.GreaterThan(0), "a pupil to feed");
            Enact(sim, PolicyId.Meals);
            Close(sim);
            Assert.That(PolicyRules.MealShare(sim), Is.EqualTo(Fixed.Zero), "setting up");
            Close(sim);
            Assert.That(PolicyRules.MealShare(sim), Is.EqualTo(Fixed.Parse("0.25")));
            HouseholdRules.SetBudget(sim, x);
            Assert.That(x.FoodNeed, Is.LessThan(need), "a quarter of a child's food is eaten at school");
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey.StartsWith("ledger.town")), Is.False, "the food is the district's, never from your store or books");
        }

        // AC-10: at most three; a fourth is refused until one is repealed; a repealed policy waits a season; the same policy can't be enacted twice.
        [Test]
        public void Slots()
        {
            Simulation sim = Start();
            Enact(sim, PolicyId.Subsidy);
            Enact(sim, PolicyId.Grant);
            Enact(sim, PolicyId.MinWage);
            Assert.That(Try(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.RentControl)), Is.EqualTo(RejectReason.SlotsFull));
            Assert.That(Try(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Grant)), Is.EqualTo(RejectReason.AlreadyActive));
            Assert.That(Try(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.RentControl)), Is.EqualTo(RejectReason.NotActive));
            Close(sim);
            Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.MinWage));
            Enact(sim, PolicyId.RentControl); // the repealed one frees its slot at once
            Close(sim);
            Assert.That(Try(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.MinWage)), Is.EqualTo(RejectReason.WaitAfterRepeal), "repealed at this very close: a season's wait");
            Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.RentControl));
            Close(sim);
            Assert.That(PolicyRules.Wait(sim, PolicyId.MinWage), Is.EqualTo(0), "a full season has passed since it was repealed");
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.MinWage));
            Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.Grant));
            Close(sim);
            Assert.That(Try(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Grant)), Is.EqualTo(RejectReason.WaitAfterRepeal), "repealed this very close: a season's wait");
            Close(sim);
            Ok(sim, new EnactPolicy(sim.State.Clock.SimTime, PolicyId.Grant));
        }

        // AC-10: a policy the close can't pay in full is paused for the season (no effect, nothing charged), shown, and not repealed; it resumes when paid.
        [Test]
        public void PauseAndWait()
        {
            Simulation sim = Start((f, j) => f == "treasury.json" ? j.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": 1000").Replace("\"perAcre\": 125", "\"perAcre\": 250000") : j);
            Enact(sim, PolicyId.Subsidy);
            Close(sim);
            PolicyState s = PolicyRules.Of(sim, PolicyId.Subsidy);
            Assert.That(s.Status, Is.EqualTo(PolicyStatus.Paused));
            Assert.That(T(sim).Reports.Last().Rows[(int)Dept.Policies].Paid, Is.EqualTo(0), "nothing charged");
            Assert.That(PolicyRules.Effective(sim, PolicyId.Subsidy), Is.False);
            Assert.That(sim.PendingEvents.OfType<PolicyPaused>().Count(), Is.EqualTo(1));
            Assert.That(PolicyRules.Used(sim), Is.EqualTo(1), "still holds its slot");
            Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
        }

        // AC-11: no policy's money reaches your cash: the treasury pays a department, never the founder.
        [Test]
        public void NeverPaysYou()
        {
            Simulation sim = Start();
            Enact(sim, PolicyId.Subsidy);
            Enact(sim, PolicyId.Grant);
            Close(sim);
            Close(sim);
            Close(sim);
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey.StartsWith("ledger.town") || e.DescriptionKey.Contains("policy")), Is.False, "nothing on your books");
            Assert.That(T(sim).Reports.Last().Rows[(int)Dept.Policies].Paid, Is.GreaterThan(0), "the policies were paid, to the district");
        }

        // AC-16: policies' states round-trip a save and replay.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start();
            Enact(sim, PolicyId.Subsidy);
            Enact(sim, PolicyId.RentControl);
            Close(sim);
            Ok(sim, new RepealPolicy(sim.State.Clock.SimTime, PolicyId.RentControl));
            Close(sim);
            byte[] bytes = Saves.SaveCodec.Write(sim.State, sim.Config, Wall0);
            Saves.LoadResult loaded = Saves.SaveCodec.Read(bytes, sim.Config);
            Assert.That(loaded.Error, Is.EqualTo(Saves.SaveError.None), loaded.Detail);
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            PolicyState[] back = loaded.State.Treasury.Policies;
            Assert.That(back[(int)PolicyId.RentControl].RepealedPeriod, Is.EqualTo(T(sim).Policies[(int)PolicyId.RentControl].RepealedPeriod));
            Assert.That(back[(int)PolicyId.Subsidy].PaidCloses, Is.EqualTo(2));
        }
    }

    internal static class Dept { public const int Policies = 6; }
}
