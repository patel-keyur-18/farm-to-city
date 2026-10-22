using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Finance;
using Sim.Core.Land;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.Time;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>
    /// A seeded player for spec 003's whole-system checks: sows the inherited acres in season, harvests, tends the
    /// garden and trees, and changes the household plan now and then. Its choices depend only on the state and the
    /// seed, so two runs that reach the same state make the same moves.
    /// </summary>
    public static class FarmBot
    {
        private static readonly int[] Acres = { 84, 85, 86, 100, 101, 102 };
        private static readonly string[][] BySeason =
        {
            new[] { "potato", "radish" },  // Spring
            new[] { "lentils", "rice" },   // Summer
            new[] { "rice", "radish" },    // Monsoon
            new[] { "wheat", "lentils" },  // Winter
        };
        private static readonly string[] Plans = { "basic", "balanced", "basic", "rich" }; // subsistence is automatic only (ADR 0034)
        private static readonly string[] Hours = { "normal", "long", "short", "normal", "rest" };

        /// <summary>One round of moves at sim time t (round k).</summary>
        public static void Play(Simulation sim, long t, ulong seed, ulong k)
        {
            sim.AdvanceTo(t);
            ulong r = Rng.Next(seed, (RngStream)904, k, 0);
            int season = (int)Calendar.SeasonAt(t, sim.Clock);
            for (int i = 0; i < Acres.Length; i++)
            {
                int tile = Acres[i];
                Crop c = sim.State.Farm.CropOn(tile);
                if (c != null && c.State == CropState.Ready) sim.Execute(new Harvest(t, tile, Overflow.SellRestToTrader));
                if (sim.State.Farm.CropOn(tile) == null && Rng.Below(Rng.Next(seed, (RngStream)904, k, (ulong)(i + 1)), 3) != 0)
                {
                    string crop = BySeason[season][(i + (int)(k % 2)) % 2];
                    string fert = (k + (ulong)i) % 3 == 0 ? "compost" : (k + (ulong)i) % 3 == 1 ? "chemical" : "none";
                    SeedSource seed2 = (k + (ulong)i) % 4 == 0 ? SeedSource.Saved : SeedSource.Certified;
                    if (sim.Execute(new Sow(t, tile, crop, seed2, fert)).Reason == RejectReason.NoSavedSeed)
                        sim.Execute(new Sow(t, tile, crop, SeedSource.Certified, fert));
                }
            }
            foreach (GardenBed b in sim.State.Farm.Beds.ToList())
            {
                if (b.State == BedState.Ready) sim.Execute(new PickGarden(t, b.Id, Overflow.SellRestToTrader));
                if (b.State == BedState.Empty) sim.Execute(new SowGarden(t, b.Id, k % 3 == 0 ? "garden_spinach" : "garden_radish"));
            }
            foreach (Tree tree in sim.State.Farm.Trees.ToList())
                if (tree.RipeGrams > 0) sim.Execute(new PickFruit(t, tree.SlotId, Overflow.SellRestToTrader));
            if (k == 0)
            {
                long[] slots = sim.State.Land.Slots.Where(s => s.Kind == SlotKind.Yard).Take(2).Select(s => s.Id).ToArray();
                sim.Execute(new PlantTree(t, slots[0], "mango"));
                sim.Execute(new PlantTree(t, slots[1], "banana"));
                sim.Execute(new PlantTree(t, sim.State.Land.Slots.First(s => s.Kind == SlotKind.FieldEdge).Id, "papaya"));
            }
            // The market (spec 004): spray warned crops, offer, withdraw, sell to the trader, buy ahead, cart to the mandi.
            foreach (Crop c in sim.State.Farm.Crops.ToList())
                if (c.State == CropState.Growing && c.Outbreak != Outbreak.None && c.Spray == SprayState.None && k % 2 == 0) sim.Execute(new Spray(t, c.TileId));
            var goods = sim.Config.Game.Market.Goods;
            ulong m = Rng.Next(seed, (RngStream)904, k, 20);
            int g = (int)Rng.Below(m, (ulong)goods.Count);
            long free = sim.State.Market.InStore(g) - sim.State.Market.OnOffer(g);
            switch (Rng.Below(Rng.Next(seed, (RngStream)904, k, 21), 8))
            {
                case 0: if (free > 0) sim.Execute(new Offer(t, goods[g].Id, free / 2 + 1, k % 3 == 0 ? Market.MarketRules.RegionalPrice(sim, g) : (Fixed?)null)); break;
                case 1: if (sim.State.Market.OnOffer(g) > 0) sim.Execute(new Withdraw(t, goods[g].Id, sim.State.Market.OnOffer(g) / 2 + 1)); break;
                case 2: if (free > 0) sim.Execute(new SellToTrader(t, goods[g].Id, System.Math.Min(free, Market.MarketRules.TraderCapacityLeft(sim, g)))); break;
                case 3: sim.Execute(new Buy(t, k % 2 == 0 ? "seed_potato" : "spray", 5_000)); break;
                case 4:
                case 5:
                    // The produce with the most free stock, up to a cart load.
                    int best = -1;
                    for (int i = 0; i < goods.Count; i++)
                        if (goods[i].Kind == GoodKind.Produce && (best < 0 || sim.State.Market.InStore(i) - sim.State.Market.OnOffer(i) > sim.State.Market.InStore(best) - sim.State.Market.OnOffer(best))) best = i;
                    long q = System.Math.Min(sim.State.Market.InStore(best) - sim.State.Market.OnOffer(best), 1_000_000);
                    if (q > 0) sim.Execute(new GoToMandi(t, new[] { new MandiLoad(goods[best].Id, q) }));
                    break;
            }
            switch (Rng.Below(r, 12))
            {
                case 0: sim.Execute(new SetDietPlan(t, Plans[(int)Rng.Below(Rng.Next(seed, (RngStream)904, k, 9), 4)])); break;
                case 1: sim.Execute(new SetLabourSplit(t, 25 * (int)Rng.Below(Rng.Next(seed, (RngStream)904, k, 10), 5))); break;
                case 2: sim.Execute(new SetWorkHours(t, Hours[(int)Rng.Below(Rng.Next(seed, (RngStream)904, k, 11), 5)])); break;
                case 3: sim.Execute(new SetEatFromStore(t, Rng.Below(Rng.Next(seed, (RngStream)904, k, 12), 2) == 0)); break;
                case 4: if (sim.State.Farm.CropOn(85) != null) sim.Execute(new ClearField(t, 85)); break;
                case 5: sim.Execute(new BuyMedicine(t)); break;
            }
        }

        /// <summary>The bot's round times: every 3 hours and 17 seconds, so they rarely meet a pass boundary.</summary>
        public static IEnumerable<long> Rounds(long start, long end)
        {
            for (long t = start + 60; t < end; t += 3 * Hour + 17) yield return t;
        }
    }

    public class FarmPlayTests
    {
        // Spec 003 AC-10.
        [Test]
        public void ChunkingInvariance() { AssertChunkingInvariant(7); }

        /// <summary>One game year of FarmBot play (farming and trading) in one advance against 500 random chunks.</summary>
        public static void AssertChunkingInvariant(ulong seed)
        {
            GameDefs game = TestContent.Game();
            Simulation one = NewFarm(seed: seed, localSeconds: 9 * Hour, game: game);
            long start = one.State.Clock.SimTime, end = start + one.Clock.SecondsPerYear;
            ulong k = 0;
            foreach (long t in FarmBot.Rounds(start, end)) FarmBot.Play(one, t, seed, k++);
            one.AdvanceTo(end);

            Simulation many = NewFarm(seed: seed, localSeconds: 9 * Hour, game: game);
            var chunks = new List<long>();
            for (ulong i = 0; i < 500; i++) chunks.Add(start + 1 + (long)Rng.Below(Rng.Next(seed, (RngStream)905, i, 0), (ulong)(end - start - 1)));
            chunks.Sort();
            int next = 0;
            k = 0;
            foreach (long t in FarmBot.Rounds(start, end))
            {
                while (next < chunks.Count && chunks[next] < t) many.AdvanceTo(chunks[next++]);
                FarmBot.Play(many, t, seed, k++);
            }
            while (next < chunks.Count) many.AdvanceTo(chunks[next++]);
            many.AdvanceTo(end);

            Assert.That(one.State.Farm.Trees.Count + one.State.Finance.Reports.Count, Is.GreaterThan(4), "the bot farmed");
            Assert.That(many.State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()));
        }

        // Spec 003 AC-28, with the market's sales, the mandi and inputs bought ahead (spec 004).
        [Test]
        public void Ledger_Reconciles()
        {
            for (ulong seed = 1; seed <= 4; seed++)
            {
                Simulation sim = NewFarm(seed: seed, game: TestContent.Game((f, j) => f == "finance.json" ? j.Replace("\"retainSeasons\": 4", "\"retainSeasons\": 16") : j));
                long end = 2 * sim.Clock.SecondsPerYear;
                ulong k = 0;
                foreach (long t in FarmBot.Rounds(0, end)) FarmBot.Play(sim, t, seed, k++);
                sim.AdvanceTo(end); // every season close reconciles (strict checks throw)

                Ledger l = sim.State.Ledger;
                Assert.That(sim.State.Finance.Reports.Count, Is.EqualTo(8));
                Assert.That(l.Entries.First().DescriptionKey, Is.EqualTo("ledger.opening"), "nothing dropped");
                Assert.That(l.Entries.SelectMany(e => e.Postings).Where(p => p.Account == Account.Cash).Sum(p => p.Amount), Is.EqualTo(l.Cash));
                Assert.That(l.Reconcile(), Is.Null);
                foreach (string key in new[] { "ledger.homeUse", "ledger.soldToTrader", "ledger.inputsBought", "ledger.prepaid", "ledger.treePlanted",
                    "ledger.marketSales", "ledger.mandiSales", "ledger.cartHire", "ledger.inputsUsed" })
                    Assert.That(l.Entries.Any(e => e.DescriptionKey == key), Is.True, key + ", seed " + seed);
            }
        }
    }

    public partial class PerfTests
    {
        // Spec 003 AC-29.
        [Test]
        public void Advance72h_WithCrops()
        {
            double best = double.MaxValue;
            for (int run = 0; run < 6; run++) // the first run warms up the JIT
            {
                Simulation sim = NewFarm(seed: 3, localSeconds: 8 * Hour, game: RichGame());
                long t = sim.State.Clock.SimTime;
                foreach (int tile in new[] { 84, 85, 86, 100, 101, 102 })
                    Assert.That(sim.Execute(new Sow(t, tile, "potato", SeedSource.Certified, "compost")).Accepted, Is.True);
                int planted = 0;
                foreach (TreeSlot slot in sim.State.Land.Slots.ToList())
                    if (planted < 8 && sim.Execute(new PlantTree(t, slot.Id, planted % 2 == 0 ? "banana" : "papaya")).Accepted) planted++;
                Assert.That(planted, Is.EqualTo(8));
                var watch = Stopwatch.StartNew();
                sim.AdvanceTo(t + 72 * Hour, AdvanceMode.Away);
                watch.Stop();
                if (run > 0) best = System.Math.Min(best, watch.Elapsed.TotalMilliseconds);
            }
            TestContext.Out.WriteLine("72 h with crops, trees and the founder: " + best.ToString("0.000") + " ms");
            Assert.That(best, Is.LessThan(20));
        }
    }
}
