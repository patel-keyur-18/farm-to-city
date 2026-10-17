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
            long homeValue = LandRules.ParcelPrice(sim, sim.State.Land.Parcels[0]);

            Assert.That(ledger.Cash, Is.EqualTo(60_000_00));
            Assert.That(ledger.Balance(Account.Land), Is.EqualTo(homeValue));
            Assert.That(homeValue, Is.EqualTo(3 * 1_17_600_00)); // 3 loam tiles at fertility 45 (price factor 0.98)
            Assert.That(ledger.Balance(Account.Buildings), Is.EqualTo(55_000_00));
            Assert.That(ledger.Balance(Account.OpeningEquity), Is.EqualTo(-(60_000_00 + homeValue + 55_000_00)));
            Assert.That(ledger.Reconcile(), Is.Null);
            Assert.That(sim.State.Land.Parcels[0].CostBasis, Is.EqualTo(homeValue));
        }
    }
}
