using System.Collections.Generic;
using System.Linq;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;

namespace Sim.Core.Tests
{
    /// <summary>Shared setup for the credit tests (spec 005 §3.6–3.10): quiet content with credit, and ledger helpers.</summary>
    public static class CreditKit
    {
        public const long S = 345_600; // a season, 4 days

        /// <summary>
        /// Quiet content (no living costs, no eating) with <paramref name="cash"/> coins to start. With <paramref name="freeBank"/>
        /// the bank lends 10,00,000 coins without collateral (still capped by the Homestead era cap), so schedule tests need no land.
        /// </summary>
        public static GameDefs Game(long cash = 20_000, bool freeBank = true, System.Func<string, string, string> edit = null)
        {
            return TestSim.Quiet((f, j) =>
            {
                if (f == "start.json") j = j.Replace("\"startingCash\": 2000000", "\"startingCash\": " + cash);
                if (f == "lenders.json" && freeBank) j = j.Replace("\"baseLimit\": 0,", "\"baseLimit\": 1000000,");
                return edit == null ? j : edit(f, j);
            });
        }

        public static Simulation NewSim(long cash = 20_000, bool freeBank = true, System.Func<string, string, string> edit = null, ulong seed = 42)
        {
            return TestSim.NewFarm(seed: seed, game: Game(cash, freeBank, edit));
        }

        public static long Coins(long coins) { return coins * Money.Coin; }

        public static long Cash(Simulation sim) { return sim.State.Ledger.Cash; }

        /// <summary>Cash change over the instant a season closes: the loan payments (Quiet content has no other flows).</summary>
        public static long CloseCashChange(Simulation sim, long closeTime)
        {
            sim.AdvanceTo(closeTime - 1);
            long before = Cash(sim);
            sim.AdvanceTo(closeTime);
            return Cash(sim) - before;
        }

        /// <summary>Spends all the cash on an expense, so the next close finds nothing to pay with.</summary>
        public static void Burn(Simulation sim, long keep = 0)
        {
            long burn = Cash(sim) - keep;
            if (burn > 0)
                sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.burn", null,
                    new Posting(Account.SeedsAndPlanting, burn), new Posting(Account.Cash, -burn));
        }

        public static void AddCash(Simulation sim, long coins)
        {
            sim.State.Ledger.Post(sim.State.Clock.SimTime, sim.Period, "test.gift", null,
                new Posting(Account.Cash, Coins(coins)), new Posting(Account.DayLabourEarnings, -Coins(coins)));
        }

        public static Loan Loan(Simulation sim, int index = 0) { return sim.State.Credit.Loans[index]; }

        public static CommandResult Take(Simulation sim, string lender, long coins, int term)
        {
            return sim.Execute(new TakeLoan(sim.State.Clock.SimTime, lender, Coins(coins), term));
        }

        public static void ReconcileOk(Simulation sim) { NUnit.Framework.Assert.That(sim.State.Ledger.Reconcile(), NUnit.Framework.Is.Null); }

        public static T Last<T>(IEnumerable<SimEvent> events) where T : SimEvent { return events.OfType<T>().LastOrDefault(); }

        /// <summary>Adds a closed season whose farm profit is <paramref name="farmProfit"/> money (credit limits read the last four).</summary>
        public static void FakeReport(Simulation sim, long farmProfit)
        {
            var lines = new List<ReportLine> { new ReportLine("farmProfit", farmProfit, new Account[0]) };
            sim.State.Finance.Reports.Add(new SeasonReport
            {
                Period = sim.State.Finance.Reports.Count, Lines = lines.AsReadOnly(),
                ByAccount = new List<KeyValuePair<Account, long>>().AsReadOnly(), CashFlow = new CashFlowSummary(), NetWorth = new NetWorthSnapshot(),
            });
        }
    }
}

namespace Sim.Core.Tests
{
    using Sim.Core.Land;
    using Sim.Core.Maths;

    /// <summary>
    /// A seeded borrower for the whole-system credit checks: takes loans from both lenders, finances land, repays, restructures
    /// and sells land. A reckless one never repays, so it misses payments, defaults and is foreclosed. Its choices depend only on
    /// the state and the seed.
    /// </summary>
    public static class CreditBot
    {
        public static void Play(Simulation sim, long t, ulong seed, ulong k, bool reckless)
        {
            ulong r = Rng.Next(seed, (RngStream)906, k, 0);
            ulong a = Rng.Next(seed, (RngStream)906, k, 1), b = Rng.Next(seed, (RngStream)906, k, 2);
            var credit = sim.State.Credit;
            var open = credit.Loans.Where(l => l.IsOpen).ToList();
            var map = sim.Config.Game.Map;
            switch (Rng.Below(r, reckless ? 6UL : 10UL))
            {
                case 0:
                case 1:
                    sim.Execute(new TakeLoan(t, "moneylender", CreditKit.Coins(1_000 + 1_000 * (long)Rng.Below(a, 25)), 1 + (int)Rng.Below(b, 4)));
                    break;
                case 2:
                    sim.Execute(new TakeLoan(t, "coop_bank", CreditKit.Coins(10_000 + 5_000 * (long)Rng.Below(a, 19)), 2 + (int)Rng.Below(b, 15)));
                    break;
                case 3:
                case 4:
                {
                    Parcel p = sim.State.Land.Parcels[(int)Rng.Below(a, (ulong)sim.State.Land.Parcels.Count)];
                    long price = LandRules.ParcelPrice(sim, p);
                    long amount = price * (long)(20 + Rng.Below(b, 81)) / 100;
                    amount -= amount % Money.Coin;
                    bool bank = Rng.Below(b, 2) == 0;
                    sim.Execute(new BuyParcel(t, map.Parcels[p.Id].Id, new Financing(bank ? "coop_bank" : "moneylender", amount, bank ? 2 + (int)Rng.Below(a, 15) : 1 + (int)Rng.Below(a, 4))));
                    break;
                }
                case 5:
                    if (reckless) break;
                    if (open.Count > 0) sim.Execute(new RepayLoan(t, open[(int)Rng.Below(a, (ulong)open.Count)].Id, 1 + (long)Rng.Below(b, 3_000_000)));
                    break;
                case 6:
                    if (open.Count > 0) sim.Execute(new RestructureLoan(t, open[(int)Rng.Below(a, (ulong)open.Count)].Id));
                    break;
                case 7:
                {
                    Parcel[] bought = sim.State.Land.Parcels.Where(p => p.Owned && !p.Protected).ToArray();
                    if (bought.Length > 0) sim.Execute(new SellParcel(t, map.Parcels[bought[(int)Rng.Below(a, (ulong)bought.Length)].Id].Id));
                    break;
                }
                case 8:
                    if (open.Count > 0) sim.Execute(new RepayLoan(t, open[(int)Rng.Below(a, (ulong)open.Count)].Id, long.MaxValue / 4));
                    break;
            }
        }
    }
}
