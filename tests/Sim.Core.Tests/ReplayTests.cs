using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
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

        private static Simulation Replay(ReplayFile file)
        {
            return Simulation.Replay(TestSim.Config(new SimOptions { DevCommandsEnabled = file.DevCommandsEnabled }), file.Commands);
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
            string json = ReplayJson.Write(file);
            ReplayFile back = ReplayJson.Load(json);
            Assert.That(ReplayJson.Write(back), Is.EqualTo(json));
            Assert.That(((NewGame)back.Commands[0]).Seed, Is.EqualTo(ulong.MaxValue));
            Assert.That(back.ExpectedHash, Is.EqualTo(0xFEDCBA9876543210UL));

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
