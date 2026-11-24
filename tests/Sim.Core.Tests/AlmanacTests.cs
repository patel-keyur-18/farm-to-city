using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Business;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Finance;
using Sim.Core.History;
using Sim.Core.Market;
using Sim.Core.People;
using Sim.Core.Saves;

namespace Sim.Core.Tests
{
    /// <summary>US-158 (spec 029 §3.4 and §3.5, ADR 0165, 0166 gap 12): the Almanac is written at each Winter close, kept (the last 8), and says the year's best return, costliest loss and spotlight.</summary>
    public class AlmanacTests
    {
        private const long Coin = 100;

        private static Simulation Start() { return TownshipTests.Start(); }

        private static AlmanacState A(Simulation sim) { return sim.State.Chronicle.Almanac; }

        /// <summary>Runs on to the next Winter close (the fourth of a year) and returns the Almanac it wrote.</summary>
        private static AlmanacEntry ToWinter(Simulation sim)
        {
            long last = A(sim).Entries.Count == 0 ? 0 : A(sim).Entries.Last().Year;
            for (int i = 0; i < 4 && (A(sim).Entries.Count == 0 || A(sim).Entries.Last().Year == last); i++) sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(A(sim).Entries.Count == 0 ? 0 : A(sim).Entries.Last().Year, Is.GreaterThan(last), "a Winter close writes an Almanac");
            return A(sim).Entries.Last();
        }

        // AC-6: written at the Winter close only, saved (a save reads back the same), the last 8 kept.
        [Test]
        public void Written_Saved()
        {
            Simulation sim = Start();
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            Assert.That(A(sim).Entries, Is.Empty, "a Spring close writes none");
            sim.DrainEvents();
            AlmanacEntry first = ToWinter(sim);
            Assert.That(sim.DrainEvents().OfType<AlmanacWritten>().Select(e => e.Year), Is.EqualTo(new long[] { 1 }), "announced once, for year 1");
            Assert.That((first.Year, first.Households), Is.EqualTo((1L, Sim.Core.Progress.EraRules.Households(sim))));
            Assert.That(first.NetWorth, Is.GreaterThan(0));
            Assert.That(first.HadBefore, Is.False, "the first year has no year before it");

            SimConfig config = SaveTests.Config();
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, 1_700_000_000), config, 3);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(r.State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
            AlmanacEntry back = r.State.Chronicle.Almanac.Entries.Single();
            Assert.That((back.Year, back.NetWorth, back.Households, back.Spot, back.Loss, back.Best), Is.EqualTo((first.Year, first.NetWorth, first.Households, first.Spot, first.Loss, first.Best)), "never changes once written");

            for (int y = 2; y <= 10; y++) ToWinter(sim);
            Assert.That(A(sim).Entries.Select(e => e.Year), Is.EqualTo(Enumerable.Range(3, 8).Select(i => (long)i)), "the last 8 of 10 years");
            Assert.That(A(sim).Entries.All(e => e.HadBefore), Is.True);
        }

        // AC-7: the best return is the biggest share of profit to money put in (crops: sales over sowing cost; businesses: the year's profit over the capital in),
        // only from a profit of 10,000; else "a quiet year". A smaller profit with a bigger share wins.
        [Test]
        public void Best_Return()
        {
            Simulation sim = Start();
            AlmanacState a = A(sim);
            IReadOnlyList<CropDef> crops = sim.Config.Game.Farm.Crops;
            a.CropCost[0] = 2_000 * Coin; a.CropIncome[0] = 14_000 * Coin; // 12,000 earned on 2,000: 600 for every 100
            a.CropCost[1] = 20_000 * Coin; a.CropIncome[1] = 50_000 * Coin; // 30,000 earned on 20,000: more money, a smaller share
            a.CropCost[2] = 100 * Coin; a.CropIncome[2] = 9_000 * Coin; // a huge share but under the 10,000 floor
            AlmanacEntry e = ToWinter(sim);
            Assert.That((e.Best, e.BestId, e.BestProfit, e.BestPut), Is.EqualTo((AlmanacBest.Crop, crops[0].Id, 12_000 * Coin, 2_000 * Coin)));
            Assert.That(A(sim).CropIncome.Concat(A(sim).CropCost).All(v => v == 0), Is.True, "the next year starts empty");

            sim = Start();
            ToWinter(sim);
            Assert.That(A(sim).Entries.Last().Best, Is.EqualTo(AlmanacBest.None), "no profit: a quiet year");
            sim.State.Chronicle.Almanac.CropCost[0] = 1_000 * Coin; sim.State.Chronicle.Almanac.CropIncome[0] = 10_999 * Coin;
            Assert.That(ToWinter(sim).Best, Is.EqualTo(AlmanacBest.None), "9,999 is under the floor");

            // A business: the year's profit over its capital, against a crop with a smaller share.
            sim = Start();
            Business.Business b = RecipeTests.Found(sim, "bakery");
            long put = BusinessBooks.CapitalIn(b);
            Assert.That(put, Is.GreaterThan(0));
            for (long p = 0; p < 3; p++) b.Reports.Add(new BusinessReport { Period = p, Balances = new List<KeyValuePair<Account, long>> { new KeyValuePair<Account, long>(Account.Sales, -(put / 3)) }.AsReadOnly() });
            sim.State.Chronicle.Almanac.CropCost[0] = 20_000 * Coin; sim.State.Chronicle.Almanac.CropIncome[0] = 30_000 * Coin; // 10,000 on 20,000: half
            AlmanacEntry withBusiness = ToWinter(sim);
            long profit = b.Reports.Where(r => r.Period > 0 && r.Period <= 3).Sum(BusinessBooks.NetProfit);
            if (withBusiness.Best == AlmanacBest.Business)
            {
                Assert.That((withBusiness.BestRef, withBusiness.BestPut), Is.EqualTo((b.Id, BusinessBooks.CapitalIn(b))));
                Assert.That(withBusiness.BestProfit, Is.EqualTo(profit));
            }
            else Assert.That(withBusiness.Best, Is.EqualTo(AlmanacBest.Crop), "a business that earned less than its capital's half loses to the crop");
        }

        // AC-8: the costliest loss is the biggest of a closed list of one-off losses; ordinary costs never count; none when none.
        [Test]
        public void Costliest_Loss()
        {
            Simulation sim = Start();
            long t = sim.State.Clock.SimTime;
            System.Array.Clear(sim.State.Market.Stock, 0, sim.State.Market.Stock.Length); // nothing in store to spoil
            sim.Emit(new CropSown(t, 1, sim.Config.Game.Farm.Crops[0].Id, t + 100, 900_000 * Coin)); // a huge sowing bill is an ordinary cost
            Assert.That(ToWinter(sim).Loss, Is.EqualTo(AlmanacLoss.None), "wages, seed and the like are never losses");

            sim = Start();
            t = sim.State.Clock.SimTime;
            sim.Emit(new FireRepaired(t + 10, 1, false, 5_000 * Coin));
            sim.Emit(new FireRepaired(t + 20, 2, false, 5_000 * Coin)); // an equal one: the first is kept
            sim.Emit(new ParcelSeized(t + 30, "p1", 4_000 * Coin));
            AlmanacEntry e = ToWinter(sim);
            Assert.That((e.Loss, e.LossAmount), Is.EqualTo((AlmanacLoss.FireRepair, 5_000 * Coin)));
            Assert.That((A(sim).WorstLoss, A(sim).WorstAmount), Is.EqualTo((AlmanacLoss.None, 0L)), "the next year starts without one");

            // A day's spoiled goods are one loss.
            sim = Start();
            t = sim.State.Clock.SimTime;
            string wheat = sim.Config.Game.Farm.Crops[0].Id;
            int g = sim.Config.Game.Market.IndexOf(wheat);
            sim.Emit(new GoodsSpoiled(t + 100, wheat, 600_000, SpoilSource.Store, 0));
            sim.Emit(new GoodsSpoiled(t + 200, wheat, 700_000, SpoilSource.Field, 3));
            long day = MarketRules.Value(sim, g, 600_000, MarketRules.Floor(sim, g)) + MarketRules.Value(sim, g, 700_000, MarketRules.Floor(sim, g));
            e = ToWinter(sim);
            Assert.That((e.Loss, e.LossAmount), Is.EqualTo((AlmanacLoss.Spoiled, day)), "the two spoilings of one day add up");

            // A business's losing season is a loss, at what it lost.
            sim = Start();
            Business.Business b = RecipeTests.Found(sim, "bakery");
            sim.AdvanceTo(TreasuryTests.NextClose(sim) + 1);
            long lost = -BusinessBooks.NetProfit(b.Reports.Last());
            Assert.That(A(sim).WorstAmount, Is.EqualTo(System.Math.Max(0, lost)), lost > 0 ? "a season that lost counts as a loss" : "a season that earned does not");
            if (lost > 0) Assert.That(A(sim).WorstLoss, Is.EqualTo(AlmanacLoss.LosingSeason));
        }

        private static Citizen Add(Simulation sim, int age, CitizenJob job = CitizenJob.None, long hiredAt = -1, long bornAt = -1)
        {
            Household h = PeopleKit.AddFamily(sim, 0, age);
            h.Savings = 1_000_000 * Coin; // so the family stays (a household with no home and no savings leaves at a close)
            Citizen c = sim.State.Citizens.Citizen(h.Members[0]);
            c.Job = job;
            if (hiredAt >= 0) c.Story.Add(new StoryLine(StoryKind.Hired, hiredAt, 0));
            if (bornAt >= 0) c.Story.Add(new StoryLine(StoryKind.Born, bornAt, 0));
            return c;
        }

        // AC-9: the spotlight follows the fixed order (the first child to read, the youngest baby, the longest-serving worker, the oldest elder); ties go to the lowest id.
        [Test]
        public void Spotlight()
        {
            Simulation sim = Start();
            Assert.That(sim.State.Citizens.Citizens, Is.Empty);
            Assert.That(ToWinter(sim).Spot, Is.EqualTo(AlmanacSpot.None), "a quiet year for the people");

            // The same town four times, each with one rank fewer: the same people, the same lines, in the fixed order.
            foreach ((int ranks, AlmanacSpot spot, string who) in new[] { (4, AlmanacSpot.Reader, "reader"), (3, AlmanacSpot.Baby, "babyB"), (2, AlmanacSpot.Worker, "worker2"), (1, AlmanacSpot.Elder, "elder") })
            {
                sim = Start();
                int retire = sim.Config.Game.Population.RetireAge;
                var people = new Dictionary<string, Citizen>();
                people["elder"] = Add(sim, retire + 5);
                Add(sim, retire + 1); // a younger elder
                if (ranks >= 2)
                {
                    Add(sim, 30, CitizenJob.Hand, hiredAt: 200);
                    people["worker2"] = Add(sim, 30, CitizenJob.Business, hiredAt: 100); // hired earliest
                }
                if (ranks >= 3)
                {
                    Add(sim, 0, bornAt: 500);
                    people["babyB"] = Add(sim, 0, bornAt: 800); // youngest, and the lower id of the two born at 800
                    Add(sim, 0, bornAt: 800);
                }
                if (ranks >= 4)
                {
                    people["reader"] = Add(sim, 8);
                    sim.State.Chronicle.Almanac.Reader = people["reader"].Id;
                }
                AlmanacEntry e = ToWinter(sim);
                Assert.That((e.Spot, e.SpotCitizen), Is.EqualTo((spot, people[who].Id)), who);
            }
        }
    }
}
