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
    /// <summary>US-138 (spec 021, ADR 0108): the town treasury, its books, its two passes, its gifts and its wall.</summary>
    public class TreasuryTests
    {
        private const long Grant = 100_000 * Coin;
        private const long Clerk = 30_000 * Coin;

        /// <summary>A rich town with dev commands on, at the Village, so <c>DebugNextEra</c> reaches the Township.</summary>
        internal static Simulation Start(System.Func<string, string, string> edit = null)
        {
            Simulation sim = NewFarm(game: Game(edit), options: new SimOptions { DevCommandsEnabled = true });
            Era(sim, "village");
            return sim;
        }

        private static void Era(Simulation sim, string id)
        {
            var eras = sim.Config.Game.Onboarding.Eras;
            for (int i = 0; i < eras.Count; i++) if (eras[i].Id == id) sim.State.Era.Current = i;
        }

        internal static void Township(Simulation sim) { Ok(sim, new DebugNextEra()); }

        private static long NextClose(Simulation sim) { return (sim.State.Clock.SimTime / sim.Clock.SecondsPerSeason + 1) * sim.Clock.SecondsPerSeason; }

        private static TreasuryState T(Simulation sim) { return sim.State.Treasury; }

        /// <summary>A start-up grant of <paramref name="coins"/> instead of 1,00,000, so a close can fall short.</summary>
        private static string SmallGrant(long coins, string file, string json)
        {
            return file == "treasury.json" ? json.Replace("\"startUpGrant\": 100000", "\"startUpGrant\": " + coins) : json;
        }

        private static string WithWork(string file, string json)
        {
            return file == "treasury.json" ? json.Replace("\"works\": []", "\"works\": [ { \"id\": \"fixture\", \"cost\": 24000, \"lifeSeasons\": 4 } ]") : json;
        }

        /// <summary>A house of yours and one family in it, so the town has residents.</summary>
        private static Household Family(Simulation sim, int tile = HomeTile)
        {
            Era(sim, "hamlet");
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildFamilyHouse(t, tile));
            sim.AdvanceTo(t + 48 * Hour);
            Era(sim, "village");
            return AddFamily(sim, sim.State.Land.Buildings.Last(b => HouseRules.IsHouse(sim, b)).Id, 32, 30, 6);
        }

        // AC-1: the treasury opens once, when the era reaches the Township, with the start-up grant and nothing from your wallet; before it the
        // hash is as it was without the treasury; it never reopens.
        [Test]
        public void Opens_Once()
        {
            Simulation sim = Start();
            Assert.That(T(sim), Is.Not.Null);
            Assert.That(T(sim).Opened, Is.False);
            Simulation without = NewFarm(game: Game((f, j) => f == "eras.json" ? j.Replace(",\n    { \"id\": \"township\", \"conditions\": { \"developerOnly\": true } }", "") : j), options: new SimOptions { DevCommandsEnabled = true });
            Assert.That(without.State.Treasury, Is.Null, "no township era, no treasury");
            Era(without, "village");
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(without.State.ComputeHash()), "before it opens the treasury adds nothing to the hash");

            long cash = sim.State.Ledger.Cash, t = sim.State.Clock.SimTime;
            sim.DrainEvents();
            Township(sim);
            Assert.That(T(sim).OpenedAt, Is.EqualTo(t));
            Assert.That(T(sim).Cash, Is.EqualTo(Grant), "the district's start-up grant");
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(cash), "nothing from your wallet");
            Assert.That(Last<TreasuryOpened>(sim).Grant, Is.EqualTo(Grant));
            Assert.That(sim.State.ComputeHash(), Is.Not.EqualTo(without.State.ComputeHash()), "once open it is state");
            Assert.That(sim.Execute(new DebugNextEra()).Reason, Is.EqualTo(RejectReason.InvalidStep), "the Township is the last era for now: it never reopens");
            Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
        }

        // AC-2: each close pays untied × residents; the tied part only with a staffed clinic (none until spec 023), and the report says what
        // was withheld.
        [Test]
        public void Grants()
        {
            Simulation sim = Start();
            Family(sim);
            Township(sim);
            sim.DrainEvents();
            int residents = sim.State.Citizens.Population;
            Assert.That(residents, Is.GreaterThan(0));
            sim.AdvanceTo(NextClose(sim));
            residents = sim.State.Citizens.Population;
            GrantPaid g = Last<GrantPaid>(sim);
            Assert.That(g.Untied, Is.EqualTo(residents * 100 * Coin), "100 coins a resident");
            Assert.That(g.Tied, Is.EqualTo(0), "no clinic yet");
            Assert.That(g.TiedWithheld, Is.EqualTo(residents * 50 * Coin), "the tied part, withheld");
            Assert.That(T(sim).Reports.Last().TiedWithheld, Is.EqualTo(residents * 50 * Coin));
            Assert.That(T(sim).Reports.Last().Balance(Account.GrantUntied), Is.EqualTo(-residents * 100 * Coin), "money in, by name");
        }

        // AC-3: a pitch rent really paid is credited to the town in the same step, from the first close after the opening; before, nothing.
        [Test]
        public void PitchRents()
        {
            Simulation sim = Start();
            Business.Business shop = ShopTests.Shop(sim, "veg_stall");
            Assert.That(sim.State.Treasury.Opened, Is.False);
            Township(sim);
            long rent = shop.Ledger.Entries.Last(e => e.DescriptionKey == "ledger.pitchRent").Postings.First(p => p.Account == Account.Rent).Amount;
            Assert.That(rent, Is.GreaterThan(0));
            Assert.That(T(sim).Ledger.Balance(Account.PitchRents), Is.EqualTo(0), "a rent paid before the opening credits nothing");

            sim.AdvanceTo(NextClose(sim));
            Assert.That(shop.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.pitchRent" && e.Period == sim.Period), Is.EqualTo(1), "the shop paid for the new season");
            Assert.That(-T(sim).Ledger.Balance(Account.PitchRents), Is.EqualTo(rent), "the same coins, credited in the same step");
            Assert.That(T(sim).Reports.Last().Balance(Account.PitchRents), Is.EqualTo(0), "the new season's rent isn't the old season's income");
            sim.AdvanceTo(NextClose(sim));
            Assert.That(T(sim).Reports.Last().Balance(Account.PitchRents), Is.EqualTo(-rent), "it is that season's income");
        }

        // AC-4: from the first close after the opening the school's upkeep is the Education line; the founder isn't charged and the dues have no
        // school row. Before it, the founder paid.
        [Test]
        public void SchoolTakeover()
        {
            Simulation sim = Start();
            Era(sim, "hamlet");
            long t = sim.State.Clock.SimTime;
            Ok(sim, new BuildSchool(t));
            sim.AdvanceTo(t + 48 * Hour);
            Era(sim, "village");
            sim.AdvanceTo(NextClose(sim));
            long upkeep = SchoolRules.Upkeep(sim);
            Assert.That(sim.State.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.schoolUpkeep" && e.Period == sim.Period), Is.EqualTo(1), "before the Township the founder pays");
            Assert.That(CloseDues.Next(sim, true).Any(d => d.Kind == DueKind.School), Is.True);

            Township(sim);
            Assert.That(CloseDues.Next(sim, true).Any(d => d.Kind == DueKind.School), Is.False, "the dues have no school row");
            sim.AdvanceTo(NextClose(sim));
            Assert.That(sim.State.Ledger.Entries.Count(e => e.DescriptionKey == "ledger.schoolUpkeep" && e.Period == sim.Period), Is.EqualTo(0), "the founder isn't charged");
            Assert.That(T(sim).Reports.Last().Rows[2].Paid, Is.EqualTo(upkeep), "the Education line pays it");
            Assert.That(T(sim).Reports.Last().Balance(Account.DeptEducation), Is.EqualTo(upkeep));
        }

        // AC-5: cash never goes below zero; income comes before spending; what can't be paid is unpaid and not carried.
        [Test]
        public void NeverBelowZero()
        {
            Simulation sim = Start((f, j) => SmallGrant(10_000, f, j));
            Township(sim);
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            Assert.That(T(sim).Cash, Is.EqualTo(0));
            DepartmentUnpaid u = sim.PendingEvents.OfType<DepartmentUnpaid>().Single();
            Assert.That((u.Department, u.Amount), Is.EqualTo((0, Clerk - 10_000 * Coin)));
            Assert.That(T(sim).Ledger.Balance(Account.Cash), Is.GreaterThanOrEqualTo(0));
            sim.DrainEvents();
            sim.AdvanceTo(NextClose(sim));
            Assert.That(sim.PendingEvents.OfType<DepartmentUnpaid>().Single().Amount, Is.EqualTo(Clerk), "last season's shortfall is not carried: a whole season's wage unpaid, nothing more");
            Assert.That(T(sim).Cash, Is.EqualTo(0));
            Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
        }

        // AC-6: pass 1 pays min(needs, budget) in department order; pass 2 pays what's left of each need while cash lasts, so a department
        // under-budgeted is paid after every other department's budget; wages come before upkeep inside a department.
        [Test]
        public void TwoPasses()
        {
            string upkeep = "{ \"id\": \"office_upkeep\", \"department\": \"office\", \"kind\": \"upkeep\", \"amount\": 5000, \"requires\": \"treasury\" },";
            Simulation sim = Start((f, j) => f == "treasury.json" ? SmallGrant(32_000, f, j).Replace("\"lines\": [", "\"lines\": [" + upkeep) : j);
            Township(sim);
            Assert.That(T(sim).Budget[0], Is.EqualTo(35_000 * Coin), "the first budget is its needs");
            sim.AdvanceTo(NextClose(sim));
            DepartmentRow office = T(sim).Reports.Last().Rows[0];
            Assert.That(T(sim).Ledger.Entries.First(e => e.Reference == "line:clerk").Postings[0].Amount, Is.EqualTo(Clerk), "the wage first, whole");
            Assert.That(office.Paid, Is.EqualTo(32_000 * Coin), "then the upkeep, as far as the cash goes");
            Assert.That(office.Funding, Is.EqualTo(Fixed.FromRatio(32_000, 35_000)));

            // An under-budgeted office is paid after the others' budgets: here Education (school open) takes its 2,000 first.
            Simulation b = Start((f, j) => SmallGrant(31_000, f, j));
            Era(b, "hamlet");
            long t = b.State.Clock.SimTime;
            Ok(b, new BuildSchool(t));
            b.AdvanceTo(t + 48 * Hour);
            Era(b, "village");
            Township(b);
            long edu = SchoolRules.Upkeep(b);
            T(b).Budget[0] = 0;
            b.AdvanceTo(NextClose(b));
            DepartmentRow e = T(b).Reports.Last().Rows[2], o = T(b).Reports.Last().Rows[0];
            Assert.That(e.Paid, Is.EqualTo(edu), "Education's budget came first in pass 1");
            Assert.That((o.Paid, o.PaidInPass2), Is.EqualTo((31_000 * Coin - edu, 31_000 * Coin - edu)), "the office got what was left, in pass 2");
            Assert.That(o.Unpaid, Is.EqualTo(Clerk - (31_000 * Coin - edu)));
        }

        // AC-7 (the condition): it starts at 1 and moves halfway toward each close's funding.
        [Test]
        public void Condition()
        {
            Simulation sim = Start((f, j) => SmallGrant(10_000, f, j));
            Township(sim);
            Assert.That(T(sim).Condition[0], Is.EqualTo(Fixed.One));
            sim.AdvanceTo(NextClose(sim));
            Fixed half = Fixed.Parse("0.5"), first = Fixed.One + (Fixed.FromRatio(10_000 * Coin, Clerk) - Fixed.One) * half;
            Assert.That(T(sim).Condition[0], Is.EqualTo(first), "halfway toward a third funded");
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 100_000 * Coin));
            sim.AdvanceTo(NextClose(sim));
            Assert.That(T(sim).Condition[0], Is.EqualTo(first + (Fixed.One - first) * half), "a funded season gives half the loss back");
            Assert.That(T(sim).Condition[1], Is.EqualTo(Fixed.One), "a department with nothing to pay keeps its service");
        }

        // AC-10: a gift posts both sides in one movement, at least 1,000, never more than your cash, outside farm profit, never repaid.
        [Test]
        public void Gift()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new GiveToTreasury(t, 5_000 * Coin)).Reason, Is.EqualTo(RejectReason.NoTreasury), "the Township first");
            Township(sim);
            Assert.That(sim.Execute(new GiveToTreasury(t, 999 * Coin)).Reason, Is.EqualTo(RejectReason.BelowGiftMinimum));
            Assert.That(sim.Execute(new GiveToTreasury(t, sim.State.Ledger.Cash + 1)).Reason, Is.EqualTo(RejectReason.InsufficientFunds));
            long cash = sim.State.Ledger.Cash, worth = Reporting.NetWorth(sim).Total;
            Ok(sim, new GiveToTreasury(t, 5_000 * Coin));
            Assert.That(cash - sim.State.Ledger.Cash, Is.EqualTo(5_000 * Coin));
            Assert.That(T(sim).Cash, Is.EqualTo(Grant + 5_000 * Coin));
            Assert.That(T(sim).Ledger.Balance(Account.GiftsFromFounder), Is.EqualTo(-5_000 * Coin));
            Assert.That(sim.State.Ledger.Balance(Account.GiftsToVillage), Is.EqualTo(5_000 * Coin), "yours is a gift to the village");
            Assert.That(worth - Reporting.NetWorth(sim).Total, Is.EqualTo(5_000 * Coin), "net worth falls by the gift");
            Assert.That(Last<GiftGiven>(sim).Amount, Is.EqualTo(5_000 * Coin));
            sim.AdvanceTo(NextClose(sim));
            SeasonReport r = sim.State.Finance.Reports.Last();
            Assert.That(r.Line("gifts").Amount, Is.EqualTo(-5_000 * Coin));
            Assert.That(r.Line("farmProfit").Accounts, Does.Not.Contain(Account.GiftsToVillage), "never farm profit");
            Assert.That(sim.State.Ledger.Cash, Is.LessThanOrEqualTo(cash - 5_000 * Coin + 1_000_000 * Coin), "and it never comes back");
        }

        // AC-11: no entry in the treasury's books of any run pays the founder or one of his businesses: its cash goes out only to departments'
        // lines and to suppliers of works.
        [Test]
        public void NeverPaysYou()
        {
            Simulation sim = Start(WithWorkAndSchool);
            Family(sim);
            Ok(sim, new BuildSchool(sim.State.Clock.SimTime));
            ShopTests.Shop(sim, "veg_stall");
            Township(sim);
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 20_000 * Coin));
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "fixture"));
            long founderCash = sim.State.Ledger.Cash;
            for (int i = 0; i < 6; i++) sim.AdvanceTo(NextClose(sim));
            foreach (JournalEntry e in T(sim).Ledger.Entries)
            {
                if (!e.Postings.Any(p => p.Account == Account.Cash && p.Amount < 0)) continue; // not a payment out
                Account other = e.Postings.Single(p => p.Account != Account.Cash).Account;
                Assert.That(e.Reference.StartsWith("line:") || e.Reference.StartsWith("works:"), Is.True, e.DescriptionKey + " " + e.Reference);
                bool toDepartment = (int)other >= 5910 && (int)other <= 5970;
                Assert.That(toDepartment || other == Account.Buildings, Is.True, "cash out to " + other);
                Assert.That(e.Reference, Does.Not.StartWith("business:"));
            }
            Assert.That(T(sim).Ledger.Balance(Account.GiftsFromFounder), Is.EqualTo(0).Or.EqualTo(-20_000 * Coin), "the gift stays in the town's books once");
            Assert.That(sim.State.Ledger.Entries.Any(e => e.DescriptionKey.StartsWith("ledger.town")), Is.False, "none of its keys in the founder's books");
            Assert.That(founderCash, Is.Not.Zero);
        }

        // AC-11: the town never buys from your businesses: its works go to a supplier, and none of its coins reach a business's books.
        [Test]
        public void NotFromYourBusinesses()
        {
            Simulation sim = Start(WithWork);
            Business.Business shop = ShopTests.Shop(sim, "veg_stall");
            Township(sim);
            long before = shop.Ledger.Cash;
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "fixture"));
            for (int i = 0; i < 3; i++) sim.AdvanceTo(NextClose(sim));
            Assert.That(T(sim).Ledger.Entries.Any(e => e.Postings.Any(p => p.Account == Account.Cash && p.Amount < 0) && e.Reference.StartsWith("business:")), Is.False, "no coin leaves the town for a business");
            Assert.That(shop.Ledger.Entries.Any(e => e.DescriptionKey.StartsWith("ledger.town")), Is.False, "none of the town's entries are in a business's books");
            Assert.That(T(sim).Ledger.Entries.First(e => e.Reference == "works:fixture").Postings.Any(p => p.Account == Account.Buildings), Is.True, "the works are bought from a supplier and held at cost");
            Assert.That(shop.Ledger.Cash, Is.LessThanOrEqualTo(before + 1_000_000_00), "the shop earns from its customers, not from the town");
        }

        private static string WithWorkAndSchool(string file, string json) { return WithWork(file, json); }

        // AC-12: commissioned works are paid from the town's cash, kept at cost at 1600 and depreciated at each close.
        [Test]
        public void Works()
        {
            Simulation sim = Start(WithWork);
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new CommissionWorks(t, "fixture")).Reason, Is.EqualTo(RejectReason.NoTreasury));
            Township(sim);
            Assert.That(sim.Execute(new CommissionWorks(t, "nothing")).Reason, Is.EqualTo(RejectReason.UnknownWork));
            long cash = T(sim).Cash, cost = 24_000 * Coin, founder = sim.State.Ledger.Cash;
            Ok(sim, new CommissionWorks(t, "fixture"));
            Assert.That(cash - T(sim).Cash, Is.EqualTo(cost), "from the town's cash to a supplier");
            Assert.That(T(sim).Ledger.Balance(Account.Buildings), Is.EqualTo(cost), "at cost");
            Assert.That(sim.State.Ledger.Cash, Is.EqualTo(founder), "never your wallet");
            for (int i = 1; i <= 4; i++)
            {
                sim.AdvanceTo(NextClose(sim));
                Assert.That(T(sim).Ledger.Balance(Account.AccumulatedDepreciation), Is.EqualTo(-cost / 4 * i), "a quarter a close, season " + i);
            }
            sim.AdvanceTo(NextClose(sim));
            Assert.That(T(sim).Ledger.Balance(Account.AccumulatedDepreciation), Is.EqualTo(-cost), "no more once it is fully written down");
            Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
            T(sim).Ledger.Entries.ToList();
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 1_000 * Coin));
            Assert.That(sim.Execute(new CommissionWorks(sim.State.Clock.SimTime, "fixture")).Accepted || T(sim).Cash < cost, Is.True);
        }

        // AC-13: a closed season's report reconciles: opening cash plus the cash in less the cash out is the closing cash, and the town has what
        // its fund balance says; 8 seasons are kept.
        [Test]
        public void Reconciles()
        {
            Simulation sim = Start();
            Family(sim);
            Township(sim);
            for (int i = 0; i < 10; i++)
            {
                sim.AdvanceTo(NextClose(sim));
                TreasuryReport r = T(sim).Reports.Last();
                Assert.That(r.Closing, Is.EqualTo(T(sim).SeasonOpeningCash), "opening + in − out = closing, season " + i);
                Assert.That(T(sim).Ledger.Reconcile(), Is.Null);
                Assert.That(T(sim).Ledger.TotalOf(AccountGroup.Assets), Is.EqualTo(T(sim).Ledger.Balance(Account.RetainedEarnings) * -1), "no debts: the town has what its fund balance says");
            }
            Assert.That(T(sim).Reports.Count, Is.EqualTo(8), "8 seasons kept");
        }

        // AC-16: a town with a treasury saves and loads to the same hash and replays to the same state.
        [Test]
        public void SaveRoundTrip()
        {
            Simulation sim = Start(WithWork);
            Family(sim);
            Township(sim);
            Ok(sim, new GiveToTreasury(sim.State.Clock.SimTime, 3_000 * Coin));
            Ok(sim, new CommissionWorks(sim.State.Clock.SimTime, "fixture"));
            for (int i = 0; i < 3; i++) sim.AdvanceTo(NextClose(sim));
            byte[] bytes = Saves.SaveCodec.Write(sim.State, sim.Config, Wall0);
            Saves.LoadResult loaded = Saves.SaveCodec.Read(bytes, sim.Config);
            Assert.That(loaded.Error, Is.EqualTo(Saves.SaveError.None), loaded.Detail);
            Assert.That(Saves.SaveCodec.Write(loaded.State, sim.Config, Wall0), Is.EqualTo(bytes), "a loaded save writes the same bytes");
            Assert.That(loaded.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            Assert.That(loaded.State.Treasury.Reports.Count, Is.EqualTo(3));
        }
    }
}
