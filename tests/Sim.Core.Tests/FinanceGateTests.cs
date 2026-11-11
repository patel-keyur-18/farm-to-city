using System;
using System.Linq;
using NUnit.Framework;
using Tools.Balancer;

namespace Sim.Core.Tests
{
    /// <summary>Spec 019 §9 (Sprint 18): the finance hub's numbers on the bots, every bot's books, and the deposit.</summary>
    [Category("Balance")]
    public class FinanceGateTests
    {
        private static SimConfig Live() { return new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game()); }

        private static double Median(System.Collections.Generic.IEnumerable<double> v)
        {
            double[] a = v.OrderBy(x => x).ToArray();
            return a.Length == 0 ? double.NaN : a[a.Length / 2];
        }

        // Spec 019 §9: the deposit earns less than a family house's rent and less than the cheapest loan costs, so borrowing to deposit loses:
        // the bot that does it ends no richer than the competent bot, seed by seed or at the median.
        [Test]
        public void Deposit_NoMoneyLoop()
        {
            Sim.Core.Definitions.GameDefs defs = TestContent.Game();
            double rent = defs.Population.RentPerSeason / (double)defs.Buildings.First(b => b.Id == "family_house").Value;
            double savings = defs.Ledger.SavingsInterest.Raw / 4294967296.0;
            double cheapest = defs.Credit.Lenders.Min(l => l.BaseRate.Raw / 4294967296.0);
            TestContext.Out.WriteLine("deposit " + savings.ToString("0.0%") + " a season; a house's rent " + rent.ToString("0.0%") + "; the cheapest loan " + cheapest.ToString("0.0%"));
            Assert.That(savings, Is.LessThan(rent), "the deposit earns less than a house's rent");
            Assert.That(savings, Is.LessThan(cheapest), "and less than the cheapest loan costs");

            // The most generous bank (10,00,000 coins without collateral, as CreditKit's): the loop at its best.
            SimConfig generous = new SimConfig(TestContent.Clock(), TestContent.Weather(), game: TestContent.Game((f, j) => f == "lenders.json" ? j.Replace("\"baseLimit\": 0,", "\"baseLimit\": 1000000,") : j));
            var ratios = new double[8];
            for (int i = 0; i < ratios.Length; i++)
            {
                long plain = Sim.Core.Finance.Reporting.NetWorth(FinanceGate.Play(generous, () => new FarmerBot(), (ulong)(i + 1), 12)).Total;
                Simulation looped = FinanceGate.Play(generous, () => new DepositLoopBot(), (ulong)(i + 1), 12);
                Assert.That(Sim.Core.Finance.SavingsRules.Balance(looped), Is.GreaterThan(0), "it did deposit");
                ratios[i] = Sim.Core.Finance.Reporting.NetWorth(looped).Total / (double)plain;
            }
            TestContext.Out.WriteLine("deposit-loop net worth over the competent bot's: " + string.Join(" ", ratios.Select(r => r.ToString("0.000"))));
            Assert.That(Median(ratios), Is.LessThanOrEqualTo(1.0), "no money loop");
        }

        // Spec 019 AC-3 on every bot the balancer has: every retained season's cash movements land in a row of a group (none against cash or
        // equity), each entry's movements add to what it did to cash, and each report's rows add to the season's change in cash.
        [Test]
        public void NothingUnsorted_EveryBot()
        {
            var failures = new System.Collections.Concurrent.ConcurrentBag<string>();
            int entries = 0, reports = 0;
            Func<Bot>[] bots = FinanceGate.EveryBot();
            System.Threading.Tasks.Parallel.For(0, bots.Length * 2, n =>
            {
                Simulation sim = FinanceGate.Play(Live(), bots[n / 2], (ulong)(n % 2 + 1), 12);
                string who = bots[n / 2]().Name + " seed " + (n % 2 + 1) + ": ";
                foreach (Sim.Core.Finance.SeasonReport r in sim.State.Finance.Reports)
                {
                    System.Threading.Interlocked.Increment(ref reports);
                    if (!r.CashKept) failures.Add(who + "season " + r.Period + " has no cash rows");
                    else if (r.CashRows.Sum(x => x.Amount) != r.CashFlow.Closing - r.CashFlow.Opening) failures.Add(who + "season " + r.Period + "'s rows do not add to its cash");
                }
                foreach (Sim.Core.Finance.JournalEntry e in sim.State.Ledger.Entries)
                {
                    if (e.DescriptionKey == Sim.Core.Finance.FinanceSystem.OpeningKey || !e.Postings.Any(p => p.Account == Sim.Core.Finance.Account.Cash)) continue;
                    var moves = Sim.Core.Finance.CashRows.Movements(e).ToList();
                    System.Threading.Interlocked.Increment(ref entries);
                    if (moves.Sum(m => m.Value) != e.Postings.Where(p => p.Account == Sim.Core.Finance.Account.Cash).Sum(p => p.Amount)) failures.Add(who + e.DescriptionKey + ": movements do not add to cash");
                    if (moves.Any(m => m.Key == Sim.Core.Finance.Account.Cash || Sim.Core.Finance.Accounts.GroupOf(m.Key) == Sim.Core.Finance.AccountGroup.Equity)) failures.Add(who + e.DescriptionKey + ": a movement against cash or equity");
                }
            });
            TestContext.Out.WriteLine(bots.Length + " bots, 2 seeds: " + reports + " reports, " + entries + " cash entries");
            Assert.That(failures.OrderBy(f => f, StringComparer.Ordinal), Is.Empty);
            Assert.That(entries, Is.GreaterThan(5000));
        }

        // ADR 0101 §3: eight seasons of entries and of cash rows in the save. A Village town (homes, a mill, the school, people) after 24 seasons.
        [Test]
        public void SaveSize_VillageTown()
        {
            SimConfig config = Live();
            Simulation sim = FinanceGate.Play(config, () => new VillageBot(false), 1, 24);
            Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThan(8));
            byte[] bytes = Sim.Core.Saves.SaveCodec.Write(sim.State, config, TestSim.Wall0);
            TestContext.Out.WriteLine("Village town, 24 seasons: " + bytes.Length + " bytes; " + sim.State.Ledger.Entries.Count + " retained entries; " + sim.State.Finance.Reports.Sum(r => r.CashRows.Count) + " cash rows");
            Assert.That(bytes.Length, Is.LessThan(200 * 1024), "spec 007 AC-6");
        }

        // Spec 019 §9: the finance hub on the bots. Debt to assets at the Village stays far under the 60% tolerance (the bots' wealth is land);
        // the cautious saver keeps 1–3 seasons of bills on day 30. Passive income is printed, not asserted: see ADR 0102.
        [Test]
        public void Hub_OnTheBots()
        {
            FinanceGate.FinanceResult competent = FinanceGate.Measure(Live(), () => new VillageBot(false), 8, 24);
            FinanceGate.FinanceResult saver = FinanceGate.Measure(Live(), () => new CautiousSaverBot(), 8, 12);
            foreach (FinanceGate.FinanceResult r in new[] { competent, saver })
                TestContext.Out.WriteLine(r.Bot + ": debt at the Village " + string.Join(" ", r.DebtAtVillage) + "%; passive a season on " + string.Join(" ", r.PassiveAtVillage)
                    + "%; emergency fund day 30 " + string.Join(" ", r.Emergency30.Select(x => (x / 10.0).ToString("0.0"))) + "; milestones " + string.Join(", ", r.MilestoneDays.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + " " + k.Value.Count + "/8 day " + Median(k.Value).ToString("0"))));
            Assert.That(competent.DebtAtVillage.Count, Is.EqualTo(8), "every run reaches the Village");
            Assert.That(competent.DebtAtVillage.Max(), Is.LessThan(60), "under the risky band");
            Assert.That(Median(competent.DebtAtVillage.Select(x => (double)x)), Is.InRange(2.0, 20.0), "debt to assets at the Village (spec 019 §9 guessed 15–40%; the bots measure 6–8%)");
            Assert.That(Median(saver.Emergency30.Select(x => (double)x)), Is.InRange(10.0, 30.0), "the cautious saver's emergency fund on day 30, in tenths of a season");
        }

        [Test, Explicit("measure")]
        public void Parts()
        {
            foreach (ulong seed in new ulong[] { 1, 2, 3 })
            {
                Simulation sim = FinanceGate.Play(Live(), () => new VillageBot(false), seed, 14);
                long total = Sim.Core.Finance.Hub.Passive(sim, out long rent, out long interest, out long business);
                Sim.Core.Finance.SeasonReport last = sim.State.Finance.Reports.Last();
                TestContext.Out.WriteLine("seed " + seed + " day " + sim.State.Clock.SimTime / 86400 + ": rent " + rent / 100 + " interest " + interest / 100 + " business " + business / 100 + " ; living " + Sim.Core.Finance.Hub.LivingPerSeason(sim) / 100
                    + " ; household line " + last.Line("household").Amount / 100 + " ; farm profit " + last.FarmProfit / 100 + " ; net result " + last.NetResult / 100 + " ; net worth " + last.NetWorth.Total / 100 + " owed " + Sim.Core.Finance.Hub.Owed(last.NetWorth) / 100);
            }
        }

        [Test, Explicit("measure")]
        public void Measure()
        {
            foreach (Func<Bot> make in new Func<Bot>[] { () => new VillageBot(false), () => new VillageBot(true), () => new CautiousSaverBot(), () => new FarmerBot() })
            {
                FinanceGate.FinanceResult r = FinanceGate.Measure(Live(), make, 8, 24);
                TestContext.Out.WriteLine(r.Bot + ": debt at the Village " + string.Join(" ", r.DebtAtVillage) + "% ; passive a season on " + string.Join(" ", r.PassiveAtVillage)
                    + "% ; emergency day 30 " + string.Join(" ", r.Emergency30.Select(x => (x / 10.0).ToString("0.0")))
                    + " ; milestones " + string.Join(", ", r.MilestoneDays.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + " " + k.Value.Count + "/8 median day " + Median(k.Value).ToString("0"))));
            }
        }
    }
}
