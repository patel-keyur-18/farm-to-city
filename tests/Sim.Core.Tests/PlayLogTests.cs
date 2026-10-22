using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Farm;
using Sim.Core.Market;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 007 §3.6: the local play log (AC-11).</summary>
    public class PlayLogTests
    {
        private static PlayLogDef Def() { return ContentLoader.LoadSaves(TestContent.ReadContent("saves.json")).PlayLog; }

        private static string[] Types(PlayLog log) { return log.Lines.Select(l => l.Split("\"type\":\"")[1].Split('"')[0]).ToArray(); }

        [Test]
        public void Entries_OnceAndCapped()
        {
            Simulation sim = NewFarm();
            var log = new PlayLog(Def(), "") { Enabled = true };
            log.NewGame(Wall0, sim.State.Clock.SimTime);
            log.SessionStart(Wall0, 0, "0.1.0", "iPhone14,5", 330, false);

            // Sow and harvest radish twice, and sell: firstHarvest and firstSale once each.
            for (int i = 0; i < 2; i++)
            {
                long t = sim.State.Clock.SimTime;
                Assert.That(sim.Execute(new Sow(t, 86, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
                sim.AdvanceTo(t + 3 * Hour);
                Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 86)).Accepted, Is.True);
                Assert.That(sim.Execute(new SellToTrader(sim.State.Clock.SimTime, "radish", 1000)).Accepted, Is.True);
                log.OnEvents(sim.DrainEvents(), Wall0 + 100 * (i + 1));
            }
            // The Hamlet unlock (spec 009 §3.6) comes from the sim's EraUnlocked, once.
            log.OnEvents(new Events.SimEvent[] { new Events.EraUnlocked(sim.State.Clock.SimTime, "hamlet"), new Events.EraUnlocked(sim.State.Clock.SimTime, "hamlet") }, Wall0 + 400);
            log.Away(Wall0 + 500, sim.State.Clock.SimTime, 7200, 7200, false);
            log.SessionEnd(Wall0 + 600, sim.State.Clock.SimTime, 600);
            Assert.That(Types(log), Is.EqualTo(new[] { "newGame", "sessionStart", "firstHarvest", "firstSale", "firstTruck", "hamletUnlocked", "away", "sessionEnd" }));
            Assert.That(log.Lines[1], Is.EqualTo("{\"t\":" + Wall0 + ",\"sim\":0,\"type\":\"sessionStart\",\"build\":\"0.1.0\",\"device\":\"iPhone14,5\",\"tz\":330,\"fromNote\":false}"));

            // Milestones are once per town, also after the log is read back from its file; a new town starts again.
            var again = new PlayLog(Def(), log.Text) { Enabled = true };
            again.OnEvents(new Events.SimEvent[0], Wall0);
            long t2 = sim.State.Clock.SimTime;
            sim.Execute(new Sow(t2, 86, "radish", SeedSource.Certified, "none"));
            sim.AdvanceTo(t2 + 3 * Hour);
            sim.Execute(new Harvest(sim.State.Clock.SimTime, 86));
            again.OnEvents(sim.DrainEvents(), Wall0 + 700);
            Assert.That(Types(again).Count(x => x == "firstHarvest"), Is.EqualTo(1));
            again.NewGame(Wall0 + 800, 0);
            sim.Execute(new Sow(sim.State.Clock.SimTime, 86, "radish", SeedSource.Certified, "none"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Hour);
            sim.Execute(new Harvest(sim.State.Clock.SimTime, 86));
            again.OnEvents(sim.DrainEvents(), Wall0 + 900);
            Assert.That(Types(again).Count(x => x == "firstHarvest"), Is.EqualTo(2));

            // Off: nothing is written.
            var off = new PlayLog(Def(), "");
            off.SessionStart(Wall0, 0, "0.1.0", "x", 0, false);
            off.NewGame(Wall0, 0);
            Assert.That(off.Text, Is.Empty);

            // The byte cap drops the oldest lines.
            var small = new PlayLog(new PlayLogDef(10_000), "") { Enabled = true };
            for (int i = 0; i < 1000; i++) small.SessionEnd(Wall0 + i, i, i);
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(small.Text), Is.LessThanOrEqualTo(10_000));
            Assert.That(small.Lines.Last(), Does.Contain("\"sim\":999,"));
        }
        // Spec 007 AC-15 (ADR 0047 §2): the time zone and a notification tap on each session; the first session's steps once
        // per town, then firstSessionDone (skipped or not).
        [Test]
        public void PlaytestFields()
        {
            SimConfig config = SaveTests.Config();
            var d = config.Game.Onboarding;
            Simulation sim = Simulation.NewGame(config, new NewGame(7, Wall0, 9 * Hour));
            var log = new PlayLog(Def(), "") { Enabled = true };
            log.NewGame(Wall0, 0);
            log.SessionStart(Wall0 + 1, 0, "b", "d", -300, true);
            Assert.That(log.Lines.Last(), Does.EndWith(",\"tz\":-300,\"fromNote\":true}"));

            log.Guidance(Wall0 + 2, 0, sim.State.Guidance, d);
            Assert.That(Types(log), Is.EqualTo(new[] { "newGame", "sessionStart" }), "no step done yet");
            long t = sim.State.Clock.SimTime;
            Assert.That(sim.Execute(new CompleteStep(t, d.Steps[0].Id)).Accepted, Is.True);
            Assert.That(sim.Execute(new CompleteStep(t, d.Steps[1].Id)).Accepted, Is.True);
            log.Guidance(Wall0 + 60, t, sim.State.Guidance, d);
            log.Guidance(Wall0 + 61, t, sim.State.Guidance, d);
            Assert.That(log.Lines.Skip(2), Is.EqualTo(new[]
            {
                "{\"t\":" + (Wall0 + 60) + ",\"sim\":" + t + ",\"type\":\"step\",\"id\":\"" + d.Steps[0].Id + "\"}",
                "{\"t\":" + (Wall0 + 60) + ",\"sim\":" + t + ",\"type\":\"step\",\"id\":\"" + d.Steps[1].Id + "\"}",
            }), "each step once");

            // Read back from its file: the steps already logged aren't logged again; a skip ends the first session.
            var again = new PlayLog(Def(), log.Text) { Enabled = true };
            Assert.That(sim.Execute(new SkipFirstSession(t)).Accepted, Is.True);
            again.Guidance(Wall0 + 90, t, sim.State.Guidance, d);
            again.Guidance(Wall0 + 91, t, sim.State.Guidance, d);
            Assert.That(again.Lines.Skip(4), Is.EqualTo(new[] { "{\"t\":" + (Wall0 + 90) + ",\"sim\":" + t + ",\"type\":\"firstSessionDone\",\"skipped\":true}" }));

            // A town that finishes every step: each step, then firstSessionDone not skipped; a new town starts again.
            again.NewGame(Wall0 + 100, 0);
            Simulation full = Simulation.NewGame(config, new NewGame(8, Wall0, 9 * Hour));
            foreach (var step in d.Steps) Assert.That(full.Execute(new CompleteStep(full.State.Clock.SimTime, step.Id)).Accepted, Is.True, step.Id);
            again.Guidance(Wall0 + 200, 0, full.State.Guidance, d);
            var types = Types(again).Skip(6).ToArray();
            Assert.That(types.Count(x => x == "step"), Is.EqualTo(d.Steps.Count));
            Assert.That(again.Lines.Last(), Does.EndWith("\"type\":\"firstSessionDone\",\"skipped\":false}"));

            // Off: nothing.
            var off = new PlayLog(Def(), "");
            off.Guidance(Wall0, 0, full.State.Guidance, d);
            Assert.That(off.Text, Is.Empty);
        }
    }
}
