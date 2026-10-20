using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Maths;
using Sim.Core.People;
using Sim.Core.Saves;
using Tools.Balancer;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 007 §3.1, §3.2: the save codec and the command log (AC-1 to AC-6, AC-12).</summary>
    public class SaveTests
    {
        private static SimConfig _config;

        /// <summary>The real content: land, farm, market, credit and people.</summary>
        internal static SimConfig Config() { return _config ??= TestSim.Config(game: TestContent.Game()); }

        /// <summary>A town played by the competent bot (a hand, orders, trees, a loan when it needs one) for some seasons.</summary>
        internal static Simulation Town(ulong seed, int seasons, SimConfig config = null)
        {
            Simulation sim = Economy.RunBot(config ?? Config(), new FarmerBot(), seasons, seed, null).Sim;
            sim.DrainEvents();
            return sim;
        }

        internal static LoadResult RoundTrip(Simulation sim, SimConfig config = null)
        {
            byte[] bytes = SaveCodec.Write(sim.State, config ?? Config(), Wall0);
            return SaveCodec.Read(bytes, config ?? Config());
        }

        [Test]
        public void RoundTrip_Property()
        {
            SimConfig config = Config();
            int withHands = 0, withOrders = 0, withLoans = 0, withReports = 0;
            for (ulong seed = 1; seed <= 50; seed++)
            {
                Simulation a = Town(seed, 1 + (int)(seed % 3)); // 1–3 seasons: before and after the hand, the first closes
                if (a.State.People.Hands.Count > 0) withHands++;
                if (a.State.People.Orders.Count > 0) withOrders++;
                if (a.State.Credit.Loans.Count > 0) withLoans++;
                if (a.State.Finance.Reports.Count > 0) withReports++;
                byte[] bytes = SaveCodec.Write(a.State, config, Wall0);
                LoadResult r = SaveCodec.Read(bytes, config);
                Assert.That(r.Error, Is.EqualTo(SaveError.None), "seed " + seed + ": " + r.Detail);
                Assert.That(r.State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()), "seed " + seed);
                Assert.That(SaveCodec.Write(r.State, config, Wall0), Is.EqualTo(bytes), "seed " + seed + ": a loaded save writes the same bytes");
                Assert.That(r.State.Finance.Reports.SelectMany(x => x.Lines.Select(l => l.Key + "=" + l.Amount)),
                    Is.EqualTo(a.State.Finance.Reports.SelectMany(x => x.Lines.Select(l => l.Key + "=" + l.Amount))), "seed " + seed + ": report lines");

                Simulation b = Simulation.FromState(config, r.State);
                var rng = new Random((int)seed);
                for (int i = 0; i < 200; i++)
                {
                    Command c = RandomCommand(a, rng);
                    Assert.That(b.Execute(c).ToString(), Is.EqualTo(a.Execute(c).ToString()), "seed " + seed + ", command " + i + " " + c.GetType().Name);
                    Assert.That(Describe(b.DrainEvents()), Is.EqualTo(Describe(a.DrainEvents())), "seed " + seed + ", events of command " + i);
                }
                Assert.That(b.State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()), "seed " + seed + " after 200 commands");
            }
            TestContext.Out.WriteLine("towns with hands " + withHands + ", orders " + withOrders + ", loans " + withLoans + ", reports " + withReports);
            Assert.That(new[] { withHands, withOrders, withLoans, withReports }, Has.All.GreaterThan(10), "the towns cover people, orders, loans and reports");
        }

        [Test]
        public void Corruption_Detected()
        {
            SimConfig config = Config();
            byte[] bytes = SaveCodec.Write(Town(7, 1).State, config, Wall0);
            // Every header byte, then a stride through the payload (every byte would take minutes; the checksum covers all).
            for (int i = 0; i < bytes.Length; i += i < 128 ? 1 : 37)
            {
                byte[] bad = (byte[])bytes.Clone();
                bad[i] ^= 0x5A;
                Assert.That(SaveCodec.Read(bad, config).Error, Is.EqualTo(SaveError.CorruptSave), "byte " + i);
            }
            foreach (int length in new[] { 0, 3, 20, bytes.Length / 2, bytes.Length - 1 })
                Assert.That(SaveCodec.Read(bytes.Take(length).ToArray(), config).Error, Is.EqualTo(SaveError.CorruptSave), "cut to " + length);
            Assert.That(SaveCodec.Read(bytes.Concat(new byte[] { 1 }).ToArray(), config).Error, Is.EqualTo(SaveError.CorruptSave), "a trailing byte");
        }

        [Test]
        public void Schema_MigrateAndRefuseNewer()
        {
            SimConfig config = Config();
            Simulation sim = Town(3, 1);
            byte[] current = SaveCodec.Write(sim.State, config, Wall0);
            Assert.That(SaveCodec.Read(current, config).Error, Is.EqualTo(SaveError.None));

            byte[] newer = SaveCodec.Write(sim.State, config, Wall0, schema: SaveCodec.SchemaVersion + 1);
            Assert.That(SaveCodec.Read(newer, config).Error, Is.EqualTo(SaveError.SaveFromNewerVersion));

            // Until the playtest build a state change raises minSchema with the schema: older saves are refused (ADR 0041).
            Assert.That(SaveCodec.Read(current, config, minSchema: SaveCodec.SchemaVersion + 1).Error, Is.EqualTo(SaveError.SaveTooOld));
            Assert.That(SaveCodec.ReadHeader(current).Schema, Is.EqualTo(SaveCodec.SchemaVersion));
        }

        [Test]
        public void ContentIds_NotIndices()
        {
            SimConfig config = Config();
            Simulation sim = Town(11, 2);
            ulong hash = sim.State.ComputeHash();

            // The same content with goods, crops, garden crops, buildings, diet plans and weather types listed in reverse order.
            SimConfig reordered = TestSim.Config(game: TestContent.Game((f, j) => Reverse(f, j)));
            Assert.That(reordered.Game.Market.Goods[0].Id, Is.Not.EqualTo(config.Game.Market.Goods[0].Id), "the test really reorders");
            LoadResult there = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), reordered);
            Assert.That(there.Error, Is.EqualTo(SaveError.None), there.Detail);
            int potato = reordered.Game.Farm.IndexOfCrop("potato");
            Assert.That(there.State.Farm.Crops.Where(c => c.Def == potato).Select(c => c.TileId),
                Is.EqualTo(sim.State.Farm.Crops.Where(c => c.Def == config.Game.Farm.IndexOfCrop("potato")).Select(c => c.TileId)));
            int wheat = reordered.Game.Market.IndexOf("wheat");
            Assert.That(there.State.Market.InStore(wheat), Is.EqualTo(sim.State.Market.InStore(config.Game.Market.IndexOf("wheat"))));

            // And back: every reference survives two remaps, so the original hash returns.
            LoadResult back = SaveCodec.Read(SaveCodec.Write(there.State, reordered, Wall0), config);
            Assert.That(back.Error, Is.EqualTo(SaveError.None), back.Detail);
            Assert.That(back.State.ComputeHash(), Is.EqualTo(hash));

            // A save naming an id the content lacks fails.
            SimConfig renamed = TestSim.Config(game: TestContent.Game((f, j) => f == "founder.json" ? j.Replace("\"id\": \"rich\"", "\"id\": \"feast\"") : j));
            LoadResult missing = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), renamed);
            Assert.That(missing.Error, Is.EqualTo(SaveError.UnknownContentId));
            Assert.That(missing.Detail, Does.Contain("rich"));
        }

        private static string Reverse(string file, string json)
        {
            string[] keys;
            switch (file)
            {
                case "goods.json": keys = new[] { "goods" }; break;
                case "crops.json": keys = new[] { "crops", "gardenCrops" }; break;
                case "buildings.json": keys = new[] { "buildings" }; break;
                case "founder.json": keys = new[] { "dietPlans", "workHours" }; break;
                default: return json;
            }
            JObject o = JObject.Parse(json);
            foreach (string k in keys) o[k] = new JArray(((JArray)o[k]).Reverse());
            return o.ToString();
        }

        [Test]
        public void SyncCompaction()
        {
            Simulation sim = NewFarm();
            for (int i = 1; i <= 3600; i++) Assert.That(sim.Execute(new Sync(Wall0 + i)).Accepted, Is.True);
            IReadOnlyList<CommandLogEntry> log = sim.State.CommandLog.Entries;
            Assert.That(log.Count, Is.EqualTo(2), "NewGame and one live run");
            var run = (LiveRun)log[1].Command;
            Assert.That((run.FromWallUtc, run.ToWallUtc, run.SimSeconds), Is.EqualTo((Wall0, Wall0 + 3600, 3600L)));
            Assert.That(Simulation.Replay(sim.Config, log.Select(e => e.Command).ToList()).State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));

            // Breaks: a player command, an away Sync, a backwards clock. Each stays as it is and a new run starts after it.
            long t = sim.State.Clock.SimTime, w = Wall0 + 3600;
            Assert.That(sim.Execute(new SetEatFromStore(t, false)).Accepted, Is.True);
            sim.Execute(new Sync(w + 1));
            sim.Execute(new Sync(w + 2));
            sim.Execute(new Sync(w + 2 + 600)); // away
            sim.Execute(new Sync(w + 603));
            sim.Execute(new Sync(w + 500)); // backwards
            sim.Execute(new Sync(w + 501));
            Assert.That(log.Skip(2).Select(e => e.Command.GetType().Name),
                Is.EqualTo(new[] { "SetEatFromStore", "LiveRun", "Sync", "LiveRun", "Sync", "LiveRun" }));
            Assert.That(((LiveRun)log[3].Command).SimSeconds, Is.EqualTo(2));
            Assert.That(Simulation.Replay(sim.Config, log.Select(e => e.Command).ToList()).State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        [Test]
        public void Size_AndCheckpoint()
        {
            SimConfig config = Config();
            Simulation sim = TenYears(config);
            byte[] bytes = SaveCodec.Write(sim.State, config, Wall0);
            TestContext.Out.WriteLine("10-year save: " + bytes.Length + " bytes, " + sim.State.CommandLog.Entries.Count + " log entries");
            Assert.That(bytes.Length, Is.LessThan(200 * 1024));
            Assert.That(sim.State.CommandLog.Entries.Count, Is.LessThan(SaveCodec.DefaultMaxLogEntries));

            // A small cap: the save checkpoints (the base is the state, the log restarts) and the town is untouched.
            ulong hash = sim.State.ComputeHash();
            SaveCodec.Write(sim.State, config, Wall0, maxLogEntries: 1_000);
            Assert.That(sim.State.CommandLog.Entries, Is.Empty);
            Assert.That(sim.State.CommandLog.Base, Is.Not.Null);
            Assert.That(sim.State.ComputeHash(), Is.EqualTo(hash));

            // Play on, save, load, replay base + log: the latest state.
            var bot = new FarmerBot();
            long t = sim.State.Clock.SimTime;
            for (int h = 0; h < 48; h++)
            {
                for (int m = 1; m <= 60; m++) sim.Execute(new Sync(sim.State.Clock.LastWallUtc + 60));
                bot.Play(sim, sim.State.Clock.SimTime);
            }
            LoadResult r = SaveCodec.Read(SaveCodec.Write(sim.State, config, Wall0), config);
            Assert.That(r.Error, Is.EqualTo(SaveError.None), r.Detail);
            Assert.That(SaveCodec.Replay(r, config).State.ComputeHash(), Is.EqualTo(sim.State.ComputeHash()));
        }

        private static Simulation _tenYears;

        /// <summary>10 game years of the competent bot with a live Sync every minute (the compacted log is the same as per second).</summary>
        internal static Simulation TenYears(SimConfig config)
        {
            if (_tenYears != null) return _tenYears;
            Simulation sim = Simulation.NewGame(config, new NewGame(5, Wall0, 8 * 3600));
            var bot = new FarmerBot();
            bot.Start(sim);
            long end = 10 * sim.Clock.SecondsPerYear;
            while (sim.State.Clock.SimTime < end)
            {
                for (int m = 1; m <= 60; m++) sim.Execute(new Sync(sim.State.Clock.LastWallUtc + 60));
                bot.Play(sim, sim.State.Clock.SimTime);
                sim.DrainEvents();
            }
            return _tenYears = sim;
        }

        // ---- Random commands for the round trip: valid and invalid alike, so rejections are compared too ----

        private static readonly string[] Crops = { "potato", "wheat", "rice", "lentils", "radish" };
        private static readonly string[] Goods = { "potato", "wheat", "rice", "lentils", "radish", "mango", "seed_potato", "compost" };

        internal static Command RandomCommand(Simulation sim, Random r)
        {
            long t = sim.State.Clock.SimTime;
            int[] acres = { 18, 19, 20, 26, 27, 28 };
            int tile = acres[r.Next(acres.Length)];
            string good = Goods[r.Next(Goods.Length)];
            PeopleState p = sim.State.People;
            long hand = p.Hands.Count > 0 ? p.Hands[r.Next(p.Hands.Count)].Id : 1;
            long applicant = p.Applicants.Count > 0 ? p.Applicants[0].Id : 1;
            switch (r.Next(22))
            {
                case 0: case 1: case 2: return new AdvanceTo(t + r.Next(1, 4 * 3600));
                case 3: return new Sync(sim.State.Clock.LastWallUtc + r.Next(0, 3 * 86400));
                case 4: return new Sow(t, tile, Crops[r.Next(Crops.Length)], r.Next(4) == 0 ? SeedSource.Saved : SeedSource.Certified, r.Next(2) == 0 ? "none" : "compost");
                case 5: return new Harvest(t, tile, r.Next(2) == 0 ? Overflow.StoreOnly : Overflow.SellRestToTrader);
                case 6: return new Offer(t, good, r.Next(1, 200) * 1000L, r.Next(2) == 0 ? (Fixed?)null : Fixed.FromInt(r.Next(10, 40)));
                case 7: return new SellToTrader(t, good, r.Next(1, 200) * 1000L);
                case 8: return new Buy(t, good, r.Next(1, 50) * 1000L);
                case 9: return new Withdraw(t, good, r.Next(1, 50) * 1000L);
                case 10: return new HireDayLabourers(t, r.Next(1, 3));
                case 11: return new SetStandingOrder(t, hand, OrderSpec.KeepCrop(tile, Crops[r.Next(Crops.Length)], SeedSource.Certified, "none", (SellRule)r.Next(3), Fixed.FromInt(20)));
                case 12: return new ClearStandingOrder(t, hand, OrderTargetKind.Field, tile);
                case 13: return new TakeLoan(t, r.Next(2) == 0 ? "moneylender" : "coop_bank", r.Next(1, 20) * 1000_00L, 4);
                case 14: return new RepayLoan(t, 1, r.Next(1, 10) * 1000_00L);
                case 15: return new SetDietPlan(t, new[] { "basic", "balanced", "rich" }[r.Next(3)]);
                case 16: return new SetWorkHours(t, new[] { "short", "normal", "long" }[r.Next(3)]);
                case 17: return new SetLabourSplit(t, 25 * r.Next(5));
                case 18: return new Interview(t, applicant);
                case 19: return new OfferContract(t, applicant);
                case 20: return new PostJob(t, r.Next(2) == 0);
                default: return new GoToMandi(t, new[] { new MandiLoad(good, r.Next(1, 100) * 1000L) });
            }
        }

    }
}
