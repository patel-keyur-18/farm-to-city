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
    }
}
