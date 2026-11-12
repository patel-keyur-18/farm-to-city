using System.Linq;
using NUnit.Framework;
using Sim.Core.Definitions;
using Sim.Core.Finance;
using Sim.Core.Maths;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 019 §3.2–§3.4, ADR 0101 §6 (US-135): the finance hub's numbers.</summary>
    public class HubTests
    {
        private static Simulation Played(ulong seed, int seasons)
        {
            Simulation sim = NewFarm(seed: seed, game: TestContent.Game((f, j) => f == "finance.json" ? j.Replace("\"retainSeasons\": 8", "\"retainSeasons\": 16") : j));
            long end = seasons * sim.Clock.SecondsPerSeason;
            ulong k = 0;
            foreach (long t in FarmBot.Rounds(0, end)) FarmBot.Play(sim, t, seed, k++);
            sim.AdvanceTo(end);
            return sim;
        }

        private static NetWorthSnapshot Worth(long cash, long land, long loan)
        {
            return new NetWorthSnapshot { Cash = cash, LandAtMarket = land, LoanPrincipal = loan };
        }

        // AC-5: debt to assets is what you owe over what you own, whole percent rounded down, with the bands from finance.json.
        [Test]
        public void Ratios()
        {
            Simulation sim = NewFarm();
            long c = Money.Coin;
            Assert.That(Hub.DebtPercent(Worth(100 * c, 900 * c, 300 * c)), Is.EqualTo(30), "300 of 1,000 owned");
            Assert.That(Hub.DebtPercent(Worth(100 * c, 900 * c, 299 * c + 99)), Is.EqualTo(29), "rounded down, never bigger than it is");
            Assert.That(Hub.DebtPercent(Worth(0, 0, 0)), Is.EqualTo(0));
            Assert.That(Hub.Band(sim, Worth(100 * c, 900 * c, 0)), Is.EqualTo(DebtBand.None));
            Assert.That(Hub.Band(sim, Worth(100 * c, 900 * c, 299 * c)), Is.EqualTo(DebtBand.Comfortable), "under 30%");
            Assert.That(Hub.Band(sim, Worth(100 * c, 900 * c, 300 * c)), Is.EqualTo(DebtBand.Stretched));
            Assert.That(Hub.Band(sim, Worth(100 * c, 900 * c, 600 * c)), Is.EqualTo(DebtBand.Stretched), "60% is not over 60%");
            Assert.That(Hub.Band(sim, Worth(100 * c, 900 * c, 601 * c)), Is.EqualTo(DebtBand.Risky));

            // The emergency fund: cash over everything the next close takes, one decimal rounded down.
            long due = Hub.NextCloseTotal(sim), cash = sim.State.Ledger.Cash;
            Assert.That(due, Is.GreaterThan(0));
            Assert.That(Hub.EmergencyTenths(sim), Is.EqualTo((long)(new System.Numerics.BigInteger(cash) * 10 / due)));
            Assert.That(Hub.NextCloseTotal(sim), Is.EqualTo(CloseDues.Next(sim).Where(d => d.Kind != DueKind.Living).Sum(d => d.Amount) + System.Math.Max(CloseDues.Next(sim).Where(d => d.Kind == DueKind.Living).Sum(d => d.Amount), Hub.LivingPerSeason(sim))));
        }

        // AC-20: living never under the basic plan; passive income never under 0%; the savings deposit joins the fund.
        [Test]
        public void Floors()
        {
            Simulation sim = NewFarm();
            long basic = MarketRules_Indexed(sim, sim.Config.Game.Farm.DietPlans.First(p => p.Id == "basic").CostPerSeason);
            Assert.That(Hub.LivingPerSeason(sim), Is.EqualTo(basic));
            sim.State.Farm.Founder.AutoSubsistence = true;
            sim.State.Farm.Founder.PreviousPlan = 0; // a family eating from the store: no cash living
            Assert.That(Hub.LivingPerSeason(sim), Is.EqualTo(basic), "never under the basic plan");
            Assert.That(Hub.NextCloseTotal(sim), Is.GreaterThanOrEqualTo(basic), "the fund's divisor counts it too");

            Assert.That(Hub.PercentOf(-5_000, basic), Is.EqualTo(0), "a business's loss counts against it, but the ratio never shows under 0%");
            Assert.That(Hub.PercentOf(basic - 1, basic), Is.EqualTo(99), "rounded down");
            Assert.That(Hub.PercentOf(basic, basic), Is.EqualTo(100));
            Assert.That(Hub.PercentOf(1_000, 0), Is.EqualTo(0));
            Assert.That(Hub.PassivePercent(sim), Is.EqualTo(0), "no season has closed yet");
        }

        private static long MarketRules_Indexed(Simulation sim, long amount) { return Market.MarketRules.Indexed(sim, amount); }

        // AC-21: the trend's index is recomputed from the seed and equals the index the town had at each close.
        [Test]
        public void TrendTodaysCoins()
        {
            for (ulong seed = 1; seed <= 3; seed++)
            {
                Simulation sim = NewFarm(seed: seed, game: TestContent.Game((f, j) => f == "finance.json" ? j.Replace("\"retainSeasons\": 8", "\"retainSeasons\": 16") : j));
                long s = sim.Clock.SecondsPerSeason;
                var seen = new System.Collections.Generic.Dictionary<long, Fixed>();
                for (int k = 1; k <= 10; k++)
                {
                    sim.AdvanceTo(k * s - 1);
                    seen[k - 1] = sim.State.Market.PriceIndex; // the index the season being closed had
                    sim.AdvanceTo(k * s);
                }
                Assert.That(sim.State.Finance.Reports.Count, Is.GreaterThanOrEqualTo(9));
                foreach (SeasonReport r in sim.State.Finance.Reports)
                    Assert.That(Hub.IndexAtClose(sim, r.Period).Raw, Is.EqualTo(seen[r.Period].Raw), "season " + r.Period);

                Fixed now = sim.State.Market.PriceIndex;
                var trend = Hub.Trend(sim, 8);
                Assert.That(trend.Count, Is.EqualTo(8));
                foreach (TrendPoint p in trend)
                {
                    Assert.That(p.InTodaysCoins, Is.EqualTo(Hub.InTodaysCoins(p.AsCounted, seen[p.Period], now)));
                    Assert.That(p.PricesSince, Is.GreaterThanOrEqualTo(Fixed.One), "prices only rise");
                }
                Assert.That(Hub.InTodaysCoins(1_000_000, Fixed.One, Fixed.One * (Fixed.One + Fixed.FromRatio(1, 10))), Is.EqualTo(1_100_000), "prices up 10%: a coin then is 1.1 now");
            }
        }
    }
}
