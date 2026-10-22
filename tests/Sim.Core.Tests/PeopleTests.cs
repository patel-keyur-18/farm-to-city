using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Time;
using static Sim.Core.Tests.PeopleKit;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>A scripted employer: huts, day labour, hands, orders, a forced lapse and a dismissal (spec 006 AC-16).</summary>
    public static class PeopleBot
    {
        private static readonly int[] Fields = { 84, 85, 86, 100, 101, 102 };

        public static void Play(Simulation sim, long t)
        {
            sim.AdvanceTo(t);
            PeopleState p = sim.State.People;
            long season = t / S;
            if (p.Constructions.Count == 0 && PeopleRules.HutCount(sim) < 2)
                sim.Execute(new BuildHandHut(t, 119));
            Farm.Crop growing = sim.State.Farm.Crops.Find(c => c.State == Farm.CropState.Growing);
            if (t % Day < 4 * Hour && PeopleRules.FreeNow(sim) > 0 && growing != null) sim.Execute(new HireLabourers(t, 1));
            if (p.Applicants.Count > 0 && PeopleRules.EmptyHut(sim) != null)
            {
                Applicant a = p.Applicants[0];
                if (!a.SecondTraitKnown) sim.Execute(new Interview(t, a.Id));
                sim.Execute(new OfferContract(t, a.Id));
            }
            foreach (Hand h in p.Hands.ToArray())
            {
                if (h.Contract == ContractState.Lapsed && season == 3) sim.Execute(new RenewContract(t, h.Id));
                int mine = p.Orders.Count(o => o.HandId == h.Id);
                foreach (int tile in Fields)
                {
                    if (mine >= 3) break;
                    if (p.OrderFor(OrderTargetKind.Field, tile) != null) continue;
                    sim.Execute(new SetStandingOrder(t, h.Id, OrderSpec.KeepCrop(tile, tile % 2 == 0 ? "wheat" : "lentils", SeedSource.Certified, "none",
                        tile % 3 == 0 ? SellRule.Trader : SellRule.Offer, Fixed.FromInt(20))));
                    mine++;
                }
            }
            if (season == 5 && p.Hands.Count > 0 && t % S < 4 * Hour) sim.Execute(new DismissHand(t, p.Hands[0].Id));
        }

        /// <summary>Round times: every 3 hours and 17 seconds, so they rarely meet a pass boundary.</summary>
        public static IEnumerable<long> Rounds(long start, long end)
        {
            for (long t = start + 60; t < end; t += 3 * Hour + 17) yield return t;
        }

        /// <summary>A test-only push: cash near zero just before the third close, so a renewal lapses.</summary>
        public static void ForceLapse(Simulation sim, long t)
        {
            sim.AdvanceTo(t);
            if (t / S == 2 && t % S > S - 4 * Hour && sim.State.Ledger.Cash > 1_000 * Coin) CashTo(sim, 1_000 * Coin);
            if (t / S == 3 && t % S < 4 * Hour && sim.State.Ledger.Cash < 50_000 * Coin) CashTo(sim, 500_000 * Coin);
        }
    }

    public class PeopleTests
    {
        // Spec 006 AC-16.
        [Test]
        public void Ledger_And_Chunking()
        {
            GameDefs game = Game((f, j) => f == "finance.json" ? j.Replace("\"retainSeasons\": 4", "\"retainSeasons\": 16") : j);
            Simulation one = NewFarm(5, 9 * Hour, game);
            long start = one.State.Clock.SimTime, end = start + 2 * one.Clock.SecondsPerYear;
            foreach (long t in PeopleBot.Rounds(start, end))
            {
                PeopleBot.ForceLapse(one, t);
                PeopleBot.Play(one, t);
            }
            one.AdvanceTo(end); // StrictChecks: a failed reconciliation at any close throws

            Simulation many = NewFarm(5, 9 * Hour, game);
            var chunks = new List<long>();
            for (ulong i = 0; i < 400; i++) chunks.Add(start + 1 + (long)Rng.Below(Rng.Next(5, (RngStream)906, i, 0), (ulong)(end - start - 1)));
            chunks.Sort();
            int next = 0;
            foreach (long t in PeopleBot.Rounds(start, end))
            {
                while (next < chunks.Count && chunks[next] < t) many.AdvanceTo(chunks[next++]);
                PeopleBot.ForceLapse(many, t);
                PeopleBot.Play(many, t);
            }
            while (next < chunks.Count) many.AdvanceTo(chunks[next++]);
            many.AdvanceTo(end);

            var log = one.State.CommandLog.Entries.Select(e => e.Command.GetType().Name).ToList();
            Assert.That(log, Does.Contain("HireLabourers").And.Contain("OfferContract").And.Contain("SetStandingOrder")
                .And.Contain("RenewContract").And.Contain("DismissHand"), "the bot used every people command");
            Assert.That(one.State.Finance.Reports.Count, Is.EqualTo(8));
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
        }
    }

    public partial class PerfTests
    {
        // Spec 006 AC-17.
        [Test]
        public void Advance72h_WithPeople()
        {
            Simulation setup = Start(seed: 3);
            Hand a = HireHand(setup);
            Hand b = HireHand(setup);
            long t = setup.State.Clock.SimTime;
            foreach (int tile in new[] { 84, 85, 86, 100, 101, 102 })
                Ok(setup, new SetStandingOrder(t, a.Id, OrderSpec.KeepCrop(tile, "wheat", SeedSource.Certified, "none", SellRule.Offer, Fixed.FromInt(20))));
            foreach (var slot in setup.State.Land.Slots.Take(6))
                Ok(setup, new SetStandingOrder(t, b.Id, OrderSpec.PickTree(slot.Id, SellRule.Trader)));
            Assert.That(setup.State.People.Orders.Count, Is.EqualTo(12));

            double best = double.MaxValue;
            for (int run = 0; run < 6; run++)
            {
                Simulation sim = Simulation.FromState(setup.Config, setup.State.Clone());
                var watch = Stopwatch.StartNew();
                sim.AdvanceTo(t + 72 * Hour, AdvanceMode.Away);
                watch.Stop();
                if (run > 0) best = System.Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            TestContext.Out.WriteLine("72 h with 2 hands and 12 orders: " + best.ToString("0.000") + " ms");
            Assert.That(best, Is.LessThan(20));
        }
    }
}
