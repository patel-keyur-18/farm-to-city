using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Finance;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 019 §3.1, ADR 0101 §3–§4 (US-137): the cash flow rows by group, and nothing moving without a row.</summary>
    public class StatementTests
    {
        private static Simulation PlayedFarm(ulong seed)
        {
            Simulation sim = NewFarm(seed: seed, game: TestContent.Game((f, j) => f == "finance.json" ? j.Replace("\"retainSeasons\": 8", "\"retainSeasons\": 16") : j));
            long end = 2 * sim.Clock.SecondsPerYear;
            ulong k = 0;
            foreach (long t in FarmBot.Rounds(0, end)) FarmBot.Play(sim, t, seed, k++);
            sim.AdvanceTo(end);
            return sim;
        }

        /// <summary>A town that buys and sells land, so sales and purchases both show.</summary>
        private static Simulation LandTrader()
        {
            Simulation sim = NewFarm(game: ParcelTests.Rich());
            long s = sim.Clock.SecondsPerSeason;
            for (int k = 0; k < 7; k++)
            {
                sim.Execute(new BuyParcel(k * s + 3600, k % 2 == 0 ? "w1" : "e1"));
                sim.Execute(new SellParcel(k * s + 7200, k % 2 == 0 ? "w1" : "e1"));
            }
            sim.AdvanceTo(7 * s);
            return sim;
        }

        // AC-1: the rows add to the closing cash less the opening, to the coin, every season, grouped in order.
        [Test]
        public void CashFlow_Reconciles()
        {
            for (ulong seed = 1; seed <= 4; seed++)
                foreach (Simulation sim in new[] { PlayedFarm(seed), LandTrader() })
                {
                    Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThan(5));
                    foreach (SeasonReport r in sim.State.Finance.Reports)
                    {
                        Assert.That(r.CashKept, Is.True);
                        Assert.That(r.CashRows.Sum(x => x.Amount), Is.EqualTo(r.CashFlow.Closing - r.CashFlow.Opening), "season " + r.Period);
                        Assert.That(r.CashRows.Select(x => (int)x.Group), Is.Ordered, "grouped in order");
                        Assert.That(r.CashRows.All(x => x.Amount != 0), Is.True);
                    }
                }
        }

        // AC-3: every cash movement of every retained season lands in a row of a group: none against cash itself or equity, and each
        // entry's movements add to what it did to cash.
        [Test]
        public void NothingUnsorted()
        {
            int entries = 0, folded = 0;
            for (ulong seed = 1; seed <= 4; seed++)
                foreach (Simulation sim in new[] { PlayedFarm(seed), LandTrader() })
                    foreach (JournalEntry e in sim.State.Ledger.Entries)
                    {
                        if (e.DescriptionKey == FinanceSystem.OpeningKey || !e.Postings.Any(p => p.Account == Account.Cash)) continue;
                        var moves = CashRows.Movements(e).ToList();
                        Assert.That(moves.Sum(m => m.Value), Is.EqualTo(e.Postings.Where(p => p.Account == Account.Cash).Sum(p => p.Amount)), e.DescriptionKey);
                        Assert.That(moves.Select(m => m.Key), Has.None.EqualTo(Account.Cash));
                        Assert.That(moves.Select(m => Accounts.GroupOf(m.Key)), Has.None.EqualTo(AccountGroup.Equity), e.DescriptionKey);
                        entries++;
                        if (e.Postings.Any(p => p.Account == Account.GainOnAssetSales || p.Account == Account.LossOnAssetSales)) folded++;
                    }
            Assert.That(entries, Is.GreaterThan(100));
            Assert.That(folded, Is.GreaterThan(0), "a sale was checked");
        }

        // A sale's whole price sits against the asset it sold, not against its gain or loss.
        [Test]
        public void Sale_PriceAgainstTheAsset()
        {
            Simulation sim = LandTrader();
            Assert.That(sim.State.Finance.Reports.SelectMany(r => r.CashRows).Any(x => x.Account == Account.Land && x.In), Is.True);
            Assert.That(sim.State.Finance.Reports.SelectMany(r => r.CashRows).Any(x => x.Account == Account.GainOnAssetSales || x.Account == Account.LossOnAssetSales), Is.False);
        }

        // The row's Why? lists the movements behind it, adding to the row.
        [Test]
        public void Row_ListsItsMovements()
        {
            Simulation sim = LandTrader();
            foreach (SeasonReport r in sim.State.Finance.Reports.Where(x => x.Period >= 0))
                foreach (CashRow row in r.CashRows)
                {
                    var list = CashRows.MovementsOf(sim.State.Ledger, r, row);
                    if (list.Count == 0) continue; // dropped from the ledger's retention (kept in the report)
                    Assert.That(list.Sum(m => m.Amount), Is.EqualTo(row.Amount));
                }
        }
    }
}
