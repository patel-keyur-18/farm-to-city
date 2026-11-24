using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using static Sim.Core.Tests.PeopleKit;

namespace Sim.Core.Tests
{
    /// <summary>
    /// The exploit checks of US-260 (spec 028 §8 AC-31, Sprint 26): a player who works khata and marketing for money finds none. Each plays towns that differ only by the
    /// trick and compares your net worth (cash and the businesses' equity) after four seasons.
    /// </summary>
    public partial class GateTests
    {
        /// <summary>A Township with a kirana on the square's first pitch and <paramref name="families"/> households of yours at its door earning <paramref name="income"/> a season.</summary>
        private static Simulation KiranaTown(int families, long income, out Business.Business shop)
        {
            Simulation sim = InvoiceTests.Township();
            for (int i = 0; i < families; i++) KiranaTests.Family(sim, ShopRules.Defs(sim).SquareTile, income);
            shop = KiranaTests.Open(sim, true, 0);
            foreach (int g in System.Linq.Enumerable.Range(0, shop.PutOut.Length)) shop.PutOut[g] *= 3; // stock that does not run out: the trick is not held back by an empty shelf
            return sim;
        }

        // AC-31, khata with the worst payers: sold to households that never pay (under 10,000 a season: 8% never paid) at the biggest limit, khata earns the owner nothing more than
        // selling for cash (its extra sales earn a little more than its losses, bounded by its limit), and what is owed is on the books, never money out of nothing.
        [Test]
        public void KhataWorst()
        {
            (long Worth, long Sold) Play(bool khata)
            {
                Simulation sim = KiranaTown(30, 8_000, out Business.Business shop);
                if (khata)
                {
                    Ok(sim, new SetKhata(sim.State.Clock.SimTime, shop.Id, true));
                    Ok(sim, new SetKhataLimit(sim.State.Clock.SimTime, shop.Id, ShopRules.Defs(sim).KhataLimits[ShopRules.Defs(sim).KhataLimits.Count - 1]));
                }
                PlayDays(sim, 16);
                GrowthReconciles(sim);
                if (khata) Assert.That(shop.Ledger.Balance(Account.OwedByBuyers), Is.EqualTo(KhataRules.Owed(shop)), "what is owed is the receivable");
                return (Worth(sim), System.Linq.Enumerable.Sum(shop.Ledger.Entries, e => e.DescriptionKey == "ledger.khataSale" ? System.Linq.Enumerable.Sum(e.Postings, q => q.Account == Account.OwedByBuyers ? q.Amount : 0) : 0));
            }
            var cash = Play(false);
            var credit = Play(true);
            TestContext.Out.WriteLine("KHATA net worth after 4 seasons: for cash " + cash.Worth / Coin + ", on khata to the worst payers " + credit.Worth / Coin + " (" + (credit.Worth - cash.Worth) / Coin + "); sold on credit " + credit.Sold / Coin + " (" + (credit.Sold == 0 ? 0 : (credit.Worth - cash.Worth) * 1000 / credit.Sold / 10.0).ToString("0.0") + "% of it)");
            // Khata brings extra sales and some losses; at its biggest limit to the worst payers the extra margin is a little more than the 8% and the late 5% it loses. That is the shop's own trade, bounded by its limit,
            // and no loop: what it earns is measured against the credit it sold (the old test allowed 1% of the whole net worth, several times the effect measured).
            Assert.That(credit.Sold, Is.GreaterThan(0), "the check really sold on credit");
            Fixed markup = ShopRules.Defs(KiranaTown(1, 8_000, out _)).RetailMarkup;
            long margin = (long)((System.Numerics.BigInteger)credit.Sold * (markup - Fixed.One).Raw / markup.Raw); // the shop's whole margin on what it sold on credit: the most any trick on it could earn
            Assert.That(credit.Worth - cash.Worth, Is.LessThanOrEqualTo(margin), "what khata earns is under the shop's own margin on what it sold on credit (15% over cost is 13% of a sale), losses and all: a margin on the extra sales, never a loop");
        }

        // AC-31, a marketing spiral: the biggest budget every season (and again the moment it can be raised) adds to the score but costs more than the margin it brings, so it
        // earns the owner no more than none, and it cannot be stacked past the cap.
        [Test]
        public void MarketingSpiral()
        {
            long Play(bool spiral)
            {
                Simulation sim = KiranaTown(30, 30_000, out Business.Business shop);
                long top = ShopRules.Defs(sim).MarketBudgets[ShopRules.Defs(sim).MarketBudgets.Count - 1];
                PlayDays(sim, 16, d =>
                {
                    if (!spiral) return;
                    foreach (long budget in ShopRules.Defs(sim).MarketBudgets) sim.Execute(new SetMarketing(sim.State.Clock.SimTime, shop.Id, budget)); // climbing the ladder each day
                    sim.Execute(new SetMarketing(sim.State.Clock.SimTime, shop.Id, top));
                });
                GrowthReconciles(sim);
                Assert.That(shop.MarketLift, Is.LessThanOrEqualTo(ShopRules.Defs(sim).MarketCap), "it never passes the cap");
                return Worth(sim);
            }
            long none = Play(false), spiral = Play(true);
            TestContext.Out.WriteLine("MARKETING net worth after 4 seasons: none " + none / Coin + ", the biggest budget " + spiral / Coin + " (" + (spiral - none) / Coin + ")");
            Assert.That(spiral, Is.LessThanOrEqualTo(none), "marketing at the cap earns the owner nothing more than none");
        }
    }
}
