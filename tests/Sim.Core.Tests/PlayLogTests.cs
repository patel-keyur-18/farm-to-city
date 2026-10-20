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
            log.SessionStart(Wall0, 0, "0.1.0", "iPhone14,5");

            // Sow and harvest radish twice, and sell: firstHarvest and firstSale once each.
            for (int i = 0; i < 2; i++)
            {
                long t = sim.State.Clock.SimTime;
                Assert.That(sim.Execute(new Sow(t, 20, "radish", SeedSource.Certified, "none")).Accepted, Is.True);
                sim.AdvanceTo(t + 3 * Hour);
                Assert.That(sim.Execute(new Harvest(sim.State.Clock.SimTime, 20)).Accepted, Is.True);
                Assert.That(sim.Execute(new SellToTrader(sim.State.Clock.SimTime, "radish", 1000)).Accepted, Is.True);
                log.OnEvents(sim.DrainEvents(), Wall0 + 100 * (i + 1));
            }
            log.Away(Wall0 + 500, sim.State.Clock.SimTime, 7200, 7200, false);
            log.SessionEnd(Wall0 + 600, sim.State.Clock.SimTime, 600);
            Assert.That(Types(log), Is.EqualTo(new[] { "newGame", "sessionStart", "firstHarvest", "firstSale", "away", "sessionEnd" }));
            Assert.That(log.Lines[1], Is.EqualTo("{\"t\":" + Wall0 + ",\"sim\":0,\"type\":\"sessionStart\",\"build\":\"0.1.0\",\"device\":\"iPhone14,5\"}"));

            // Milestones are once per town, also after the log is read back from its file; a new town starts again.
            var again = new PlayLog(Def(), log.Text) { Enabled = true };
            again.OnEvents(new Events.SimEvent[0], Wall0);
            long t2 = sim.State.Clock.SimTime;
            sim.Execute(new Sow(t2, 20, "radish", SeedSource.Certified, "none"));
            sim.AdvanceTo(t2 + 3 * Hour);
            sim.Execute(new Harvest(sim.State.Clock.SimTime, 20));
            again.OnEvents(sim.DrainEvents(), Wall0 + 700);
            Assert.That(Types(again).Count(x => x == "firstHarvest"), Is.EqualTo(1));
            again.NewGame(Wall0 + 800, 0);
            sim.Execute(new Sow(sim.State.Clock.SimTime, 20, "radish", SeedSource.Certified, "none"));
            sim.AdvanceTo(sim.State.Clock.SimTime + 3 * Hour);
            sim.Execute(new Harvest(sim.State.Clock.SimTime, 20));
            again.OnEvents(sim.DrainEvents(), Wall0 + 900);
            Assert.That(Types(again).Count(x => x == "firstHarvest"), Is.EqualTo(2));

            // Off: nothing is written.
            var off = new PlayLog(Def(), "");
            off.SessionStart(Wall0, 0, "0.1.0", "x");
            off.NewGame(Wall0, 0);
            Assert.That(off.Text, Is.Empty);

            // The byte cap drops the oldest lines.
            var small = new PlayLog(new PlayLogDef(10_000), "") { Enabled = true };
            for (int i = 0; i < 1000; i++) small.SessionEnd(Wall0 + i, i, i);
            Assert.That(System.Text.Encoding.UTF8.GetByteCount(small.Text), Is.LessThanOrEqualTo(10_000));
            Assert.That(small.Lines.Last(), Does.Contain("\"sim\":999,"));
        }
    }
}
