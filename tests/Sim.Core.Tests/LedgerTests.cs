using System;
using NUnit.Framework;
using Sim.Core.Finance;
using Sim.Core.Maths;

namespace Sim.Core.Tests
{
    public class MoneyTests
    {
        // Spec 005 AC-5.
        [Test]
        public void MulRate_RoundsHalfAwayFromZero()
        {
            Assert.That(Money.MulRate(1_000_01, Fixed.FromRatio(4, 100)), Is.EqualTo(40_00));
            Assert.That(Money.MulRate(1_25, Fixed.FromRatio(10, 100)), Is.EqualTo(13));
            Assert.That(Money.MulRate(-1_25, Fixed.FromRatio(10, 100)), Is.EqualTo(-13));
            Assert.That(Money.MulRate(0, Fixed.One), Is.EqualTo(0));
            Assert.That(Money.MulRate(1_20_000_00, Fixed.One), Is.EqualTo(1_20_000_00));
        }
    }

    public partial class LedgerTests
    {
        private static Ledger WithCash(long cash)
        {
            var ledger = new Ledger();
            ledger.Post(0, 0, "test.opening", null,
                new Posting(Account.Cash, cash), new Posting(Account.OpeningEquity, -cash));
            return ledger;
        }

        // Spec 005 AC-1.
        [Test]
        public void Post_MustBalance()
        {
            Ledger ledger = WithCash(1_000_00);

            Assert.Throws<InvalidOperationException>(() => ledger.Post(10, 0, "test.bad", null,
                new Posting(Account.Land, 500_00), new Posting(Account.Cash, -499_99)));
            Assert.That(ledger.Balance(Account.Cash), Is.EqualTo(1_000_00));
            Assert.That(ledger.Balance(Account.Land), Is.EqualTo(0));
            Assert.That(ledger.Entries.Count, Is.EqualTo(1));

            JournalEntry e = ledger.Post(20, 0, "test.buy", "parcel:1",
                new Posting(Account.Land, 500_00), new Posting(Account.Cash, -500_00));
            Assert.That(ledger.Balance(Account.Cash), Is.EqualTo(500_00));
            Assert.That(ledger.Balance(Account.Land), Is.EqualTo(500_00));
            Assert.That(ledger.Balance(Account.OpeningEquity), Is.EqualTo(-1_000_00));
            Assert.That((e.Id, e.SimTime, e.Period, e.Reference), Is.EqualTo((2L, 20L, 0L, "parcel:1")));
            Assert.That(ledger.Entries.Count, Is.EqualTo(2));
        }

        // Spec 005 AC-2.
        [Test]
        public void Cash_NeverNegative()
        {
            Ledger ledger = WithCash(100_00);

            Assert.That(ledger.CanAfford(100_00), Is.True);
            Assert.That(ledger.CanAfford(100_01), Is.False);
            Assert.That(ledger.CanAfford(0), Is.True);

            Assert.Throws<InvalidOperationException>(() => ledger.Post(1, 0, "test.overspend", null,
                new Posting(Account.SeedsAndPlanting, 100_01), new Posting(Account.Cash, -100_01)));
            Assert.That(ledger.Balance(Account.Cash), Is.EqualTo(100_00));
            Assert.That(ledger.Balance(Account.SeedsAndPlanting), Is.EqualTo(0));

            ledger.Post(2, 0, "test.spend", null,
                new Posting(Account.SeedsAndPlanting, 100_00), new Posting(Account.Cash, -100_00));
            Assert.That(ledger.Balance(Account.Cash), Is.EqualTo(0));
            Assert.That(ledger.CanAfford(1), Is.False);
        }

        [Test]
        public void Reconcile_BalancedBooksPass()
        {
            Ledger ledger = WithCash(1_000_00);
            ledger.Post(5, 0, "test.sale", null, new Posting(Account.Cash, 250_00), new Posting(Account.CropSales, -250_00));
            ledger.Post(6, 0, "test.seed", null, new Posting(Account.SeedsAndPlanting, 50_00), new Posting(Account.Cash, -50_00));
            Assert.That(ledger.Reconcile(), Is.Null);
            Assert.That(ledger.TotalOf(AccountGroup.Assets), Is.EqualTo(1_200_00));
            Assert.That(ledger.NetIncome, Is.EqualTo(200_00));
        }
    }
}

namespace Sim.Core.Tests
{
    using System.Linq;
    using NUnit.Framework;
    using Sim.Core.Finance;
    using Sim.Core.Land;

    public partial class LedgerTests
    {
        // Spec 005 AC-3.
        [Test]
        public void NewGame_OpeningBalanceSheet()
        {
            Simulation sim = TestSim.NewFarm();
            Ledger ledger = sim.State.Ledger;
            long homeValue = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[0]) + LandRules.ParcelPrice(sim, sim.State.Land.Parcels[1]);

            // The opening entry holds the 20,000 inherited coins; the first season's basic living costs are then
            // prepaid from them (spec 003 AC-21: 18,000 for the whole of season 0 at t = 0).
            JournalEntry opening = ledger.Entries.First();
            Assert.That(opening.DescriptionKey, Is.EqualTo("ledger.opening"));
            Assert.That(opening.Postings.Single(p => p.Account == Account.Cash).Amount, Is.EqualTo(20_000_00));
            Assert.That(ledger.Cash + ledger.Balance(Account.PrepaidLivingCosts), Is.EqualTo(20_000_00));
            Assert.That(ledger.Balance(Account.PrepaidLivingCosts), Is.EqualTo(18_000_00));
            Assert.That(ledger.Balance(Account.Land), Is.EqualTo(homeValue));
            Assert.That(homeValue, Is.EqualTo(7 * 1_17_600_00)); // 6 farm acres and the yard, loam at fertility 45 (price factor 0.98)
            Assert.That(ledger.Balance(Account.Buildings), Is.EqualTo(55_000_00));
            Assert.That(ledger.Balance(Account.OpeningEquity), Is.EqualTo(-(20_000_00 + homeValue + 55_000_00)));
            Assert.That(ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Land.Parcels[0].CostBasis + sim.State.Land.Parcels[1].CostBasis, Is.EqualTo(homeValue));
        }
    }
}

namespace Sim.Core.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Sim.Core.Commands;
    using Sim.Core.Finance;
    using Sim.Core.Maths;
    using static Sim.Core.Tests.TestSim;

    public partial class LedgerTests
    {
        // Spec 005 AC-4. Activity so far: land purchases and quick sales (US-040) and prepayments (US-060).
        // Loans, repayments and missed payments join this test with F05.2.
        [Test]
        public void Reconciles_EverySeason()
        {
            for (ulong seed = 1; seed <= 5; seed++)
            {
                Simulation sim = NewFarm(seed: seed, game: TestContent.Game((f, j) =>
                    f == "start.json" ? j.Replace("\"startingCash\": 20000", "\"startingCash\": 2000000")
                    : f == "finance.json" ? j.Replace("4", "16") : j));
                string[] parcels = sim.Config.Game.Map.Parcels.Select(p => p.Id).ToArray();
                long end = 3 * sim.Clock.SecondsPerYear;
                int accepted = 0;
                for (ulong i = 0; ; i++)
                {
                    ulong r = Rng.Next(seed, (RngStream)903, i, 0);
                    long at = sim.State.Clock.SimTime + 1 + (long)Rng.Below(r, 20 * 3600);
                    if (at > end) break;
                    string id = parcels[(int)Rng.Below(Rng.Next(seed, (RngStream)903, i, 1), (ulong)parcels.Length)];
                    ulong kind = Rng.Below(Rng.Next(seed, (RngStream)903, i, 2), 5);
                    if (kind >= 3)
                    {
                        // Prepayments (US-060): open one for up to a season and a half, or close the oldest early.
                        sim.AdvanceTo(at);
                        if (kind == 3 && sim.State.Ledger.CanAfford(9_000_01))
                            Accrual.Open(sim, Account.HiredLabour, Account.PrepaidWages, 9_000_01, at, at + 1 + (long)Rng.Below(r, 6 * Day), "test:p" + i);
                        else if (kind == 4 && sim.State.Finance.Prepayments.Count > 0)
                            Accrual.Close(sim, sim.State.Finance.Prepayments[0].Id);
                        accepted++;
                        continue;
                    }
                    Command c = kind == 0 ? new SellParcel(at, id) : (Command)new BuyParcel(at, id);
                    if (sim.Execute(c).Accepted) accepted++;
                }
                sim.AdvanceTo(end); // every season close reconciles (strict: a failure throws)

                Assert.That(accepted, Is.GreaterThan(10), "seed " + seed);
                Ledger ledger = sim.State.Ledger;
                Assert.That(ledger.Entries.First().DescriptionKey, Is.EqualTo("ledger.opening"), "nothing dropped yet");
                long cashPostings = ledger.Entries.SelectMany(e => e.Postings).Where(p => p.Account == Account.Cash).Sum(p => p.Amount);
                Assert.That(cashPostings, Is.EqualTo(ledger.Cash));
                Assert.That(ledger.Reconcile(), Is.Null);
            }
        }

        [Test]
        public void Retention_KeepsCurrentAndLastSeasons()
        {
            Simulation sim = NewFarm(game: ParcelTests.Rich());
            long s = sim.Clock.SecondsPerSeason;
            for (int k = 0; k < 7; k++)
            {
                sim.Execute(new BuyParcel(k * s + 3600, k % 2 == 0 ? "w1" : "e1"));
                sim.Execute(new SellParcel(k * s + 7200, k % 2 == 0 ? "w1" : "e1"));
            }
            sim.AdvanceTo(7 * s); // the close of season 6; retainSeasons = 4 keeps periods 3–6 and the current one
            Assert.That(sim.State.Ledger.Entries.Min(e => e.Period), Is.EqualTo(3));
            Assert.That(sim.State.Ledger.Reconcile(), Is.Null);
        }
    }
}
