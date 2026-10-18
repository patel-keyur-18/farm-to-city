using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Time;

namespace Sim.Core.Tests
{
    public class ReplayTests
    {
        /// <summary>
        /// Set FTC_UPDATE_GOLDEN=1 to re-pin golden hashes after a deliberate state change. Review the diff:
        /// a golden hash may only change together with GameState.HashFormatVersion or a spec change.
        /// </summary>
        private static bool UpdateGolden { get { return Environment.GetEnvironmentVariable("FTC_UPDATE_GOLDEN") == "1"; } }

        /// <summary>
        /// Golden replays run on a frozen copy of the content (golden/content/, ADR 0034), so tuning content/ never
        /// re-pins them; the device build bundles the same copy.
        /// </summary>
        public static SimConfig GoldenConfig(bool game, bool dev = false)
        {
            string dir = TestContent.TestFile("golden/content");
            string Read(string f) { return File.ReadAllText(Path.Combine(dir, f)); }
            WeatherSet weather = ContentLoader.LoadWeather(Read("weather.json"));
            return new SimConfig(ContentLoader.LoadClock(Read("clock.json")), weather, new SimOptions { DevCommandsEnabled = dev },
                game: game ? ContentLoader.LoadGame(Read, weather) : null);
        }

        private static Simulation Replay(ReplayFile file)
        {
            return Simulation.Replay(GoldenConfig(file.Game, file.DevCommandsEnabled), file.Commands);
        }

        /// <summary>
        /// Checks a golden replay with game content. When the file is missing or FTC_UPDATE_GOLDEN=1, it is generated
        /// from a seeded FarmBot session (<paramref name="session"/>) on the frozen content and written with its hash.
        /// </summary>
        private static void CheckGameGolden(string name, string description, ulong seed, System.Action<Simulation> session, params string[] mustInclude)
        {
            string path = TestContent.TestFile("golden/" + name);
            if (UpdateGolden || !File.Exists(path))
            {
                Simulation live = Simulation.NewGame(GoldenConfig(true), new NewGame(seed, TestSim.Wall0, 9 * 3600));
                session(live);
                var gen = new ReplayFile { Description = description, Game = true, ExpectedHash = live.State.ComputeHash() };
                gen.Commands.AddRange(live.State.CommandLog.Entries.Select(e => e.Command));
                File.WriteAllText(path, ReplayJson.Write(gen));
                TestContext.Out.WriteLine(name + " written: 0x" + gen.ExpectedHash.Value.ToString("X16"));
            }
            string text = File.ReadAllText(path);
            ReplayFile file = ReplayJson.Load(text);
            Assert.That(file.Game, Is.True);
            Assert.That(ReplayJson.Write(file), Is.EqualTo(text), "every command writes back exactly as read");
            var types = file.Commands.Select(c => c.GetType().Name).Distinct().ToList();
            foreach (string t in mustInclude) Assert.That(types, Does.Contain(t), name + " covers " + t);
            ulong hash = Replay(file).State.ComputeHash();
            Assert.That(file.ExpectedHash, Is.Not.Null);
            Assert.That(hash, Is.EqualTo(file.ExpectedHash.Value), "0x" + hash.ToString("X16"));
            Assert.That(Replay(file).State.ComputeHash(), Is.EqualTo(hash), "replaying twice gives the same answer");
        }

        // Spec 003 AC-30: sow, spray, harvest, trees, the garden, plan changes and an away advance.
        [Test]
        public void Golden_003_Farm()
        {
            CheckGameGolden("003-farm.json", "Spec 003 AC-30: two live days of seeded farming, a 30-hour away advance, then live to day 8 (the banana fruits).", 3, sim =>
            {
                ulong k = 0;
                long t0 = sim.State.Clock.SimTime;
                foreach (long t in FarmBot.Rounds(t0, t0 + 2 * TestSim.Day)) FarmBot.Play(sim, t, 3, k++);
                sim.AdvanceTo(sim.State.Clock.SimTime + 30 * TestSim.Hour, AdvanceMode.Away);
                long t1 = sim.State.Clock.SimTime;
                foreach (long t in FarmBot.Rounds(t1, 8 * TestSim.Day)) FarmBot.Play(sim, t, 3, k++);
            }, "Sow", "Spray", "Harvest", "PlantTree", "PickFruit", "SowGarden", "PickGarden", "SetDietPlan", "SetLabourSplit", "AdvanceTo");
        }

        // Spec 004 AC-16: offers (with a minimum), withdrawals, the trader, buying ahead, the mandi and spoilage.
        [Test]
        public void Golden_004_Market()
        {
            CheckGameGolden("004-market.json", "Spec 004 AC-16: four live days of seeded farming and trading.", 4, sim =>
            {
                ulong k = 0;
                long t0 = sim.State.Clock.SimTime;
                foreach (long t in FarmBot.Rounds(t0, t0 + 4 * TestSim.Day)) FarmBot.Play(sim, t, 4, k++);

                // A scripted market day: the produce with the most free stock is offered at a minimum, partly withdrawn,
                // partly sold to the trader; seed is bought ahead; a cart goes to the mandi the next day; a day of spoilage.
                long at = sim.State.Clock.SimTime;
                var goods = sim.Config.Game.Market.Goods;
                int best = Enumerable.Range(0, goods.Count).Where(g => goods[g].Kind == GoodKind.Produce)
                    .OrderByDescending(g => sim.State.Market.InStore(g) - sim.State.Market.OnOffer(g)).First();
                long free = sim.State.Market.InStore(best) - sim.State.Market.OnOffer(best);
                Assert.That(free, Is.GreaterThan(10_000), "something to trade");
                Assert.That(sim.Execute(new Offer(at, goods[best].Id, free / 4, Sim.Core.Maths.Fixed.FromInt(5))).Accepted, Is.True);
                Assert.That(sim.Execute(new Withdraw(at, goods[best].Id, free / 8)).Accepted, Is.True);
                Assert.That(sim.Execute(new SellToTrader(at, goods[best].Id, System.Math.Min(free / 4, Market.MarketRules.TraderCapacityLeft(sim, best)))).Accepted, Is.True);
                Assert.That(sim.Execute(new Buy(at, "seed_wheat", 20_000)).Accepted, Is.True);
                long next = (at / TestSim.Day + 1) * TestSim.Day + 8 * TestSim.Hour;
                sim.AdvanceTo(next);
                long load = System.Math.Min(sim.State.Market.InStore(best) - sim.State.Market.OnOffer(best), 1_000_000);
                Assert.That(sim.Execute(new GoToMandi(next, new[] { new Market.MandiLoad(goods[best].Id, load) })).Accepted, Is.True);
                sim.AdvanceTo(next + TestSim.Day);
            }, "Offer", "Withdraw", "SellToTrader", "Buy", "GoToMandi", "Harvest");
        }

        // Spec 001 AC-13.
        [Test]
        public void Golden_001_Basic()
        {
            string path = TestContent.TestFile("golden/001-basic.json");
            ReplayFile file = ReplayJson.Load(File.ReadAllText(path));
            ulong hash = Replay(file).State.ComputeHash();

            if (UpdateGolden)
            {
                file.ExpectedHash = hash;
                File.WriteAllText(path, ReplayJson.Write(file));
                Assert.Pass("Golden hash re-pinned: 0x" + hash.ToString("X16"));
            }
            Assert.That(file.ExpectedHash, Is.Not.Null);
            Assert.That(hash, Is.EqualTo(file.ExpectedHash.Value), "0x" + hash.ToString("X16"));

            // Replaying twice gives the same answer.
            Assert.That(Replay(file).State.ComputeHash(), Is.EqualTo(hash));
        }

        [Test]
        public void ReplayOfCommandLog_MatchesLiveRun()
        {
            Simulation live = TestSim.New(seed: 99, localSeconds: 3600);
            live.AdvanceTo(50_000);
            live.AdvanceTo(300_000, AdvanceMode.Away);
            Assert.That(live.AdvanceTo(10).Reason, Is.EqualTo(RejectReason.TimeInPast)); // rejected: not logged
            live.AdvanceTo(900_000);

            List<Command> log = live.State.CommandLog.Entries.Select(e => e.Command).ToList();
            Assert.That(log.Count, Is.EqualTo(4));
            Simulation replayed = Simulation.Replay(TestSim.Config(), log);
            Assert.That(replayed.State.ComputeHash(), Is.EqualTo(live.State.ComputeHash()));
        }

        [Test]
        public void ReplayFile_RoundTrips()
        {
            var file = new ReplayFile { Description = "round trip", DevCommandsEnabled = true, ExpectedHash = 0xFEDCBA9876543210UL };
            file.Commands.Add(new NewGame(ulong.MaxValue, 1_800_000_000, 86_399));
            file.Commands.Add(new AdvanceTo(123_456, AdvanceMode.Away));
            file.Commands.Add(new Sync(1_800_000_030));
            file.Commands.Add(new DebugAdvance(86_400, AdvanceMode.Live));
            file.Commands.Add(new Offer(90_000, "potato", 1_000, Maths.Fixed.Parse("6.8000000007")));
            file.Commands.Add(new GoToMandi(90_000, new[] { new Market.MandiLoad("wheat", 5_000) }));
            string json = ReplayJson.Write(file);
            ReplayFile back = ReplayJson.Load(json);
            Assert.That(ReplayJson.Write(back), Is.EqualTo(json));
            Assert.That(((NewGame)back.Commands[0]).Seed, Is.EqualTo(ulong.MaxValue));
            Assert.That(back.ExpectedHash, Is.EqualTo(0xFEDCBA9876543210UL));
            Assert.That(((Offer)back.Commands[4]).MinPrice, Is.EqualTo(Maths.Fixed.Parse("6.8000000007")), "exact");

            var bad = Assert.Throws<ContentException>(() => ReplayJson.Load("{\"format\":1,\"commands\":[{\"type\":\"Teleport\"}]}"));
            Assert.That(bad.FieldPath, Is.EqualTo("replay.commands[0].type"));
        }

        [Test]
        public void Replay_RequiresNewGameFirst()
        {
            Assert.That(() => Simulation.Replay(TestSim.Config(), new Command[] { new AdvanceTo(5) }), Throws.ArgumentException);
        }
    }
}
