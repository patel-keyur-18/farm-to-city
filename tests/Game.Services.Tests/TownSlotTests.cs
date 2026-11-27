using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Saves;
using static Game.Services.Tests.TownKit;

namespace Game.Services.Tests
{
    /// <summary>Spec 031 §3: a switch is a save and a load of another slot; each town keeps its own time and catches up when it is opened.</summary>
    public class TownSlotTests
    {
        private string _root;

        [SetUp]
        public void MakeFolder() { _root = Path.Combine(Path.GetTempPath(), "ftc-switch-" + System.Guid.NewGuid().ToString("N")); }

        [TearDown]
        public void DropFolder() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        private SaveService Slot(int n) { return new SaveService(Path.Combine(_root, "Saves"), Path.Combine(_root, "Docs"), SaveDef(), n); }

        private static Simulation Open(SaveService s, Simulation like, long wall)
        {
            LoadOutcome o = s.Load(like.Config, wall, 0);
            Assert.That(o.State, Is.Not.Null);
            return Simulation.FromState(like.Config, o.State);
        }

        // AC-2: a switch saves the open town and opens the other; switching back finds the first exactly as saved, plus the time away.
        [Test]
        public void SwitchRoundTrip()
        {
            Simulation one = NewTown(3), two = NewTown(8);
            SaveService s1 = Slot(1), s2 = Slot(2);
            one.Execute(new Sync(Wall0 + 100));
            two.Execute(new Sync(Wall0 + 100));
            s2.Save(two.State, two.Config, Wall0 + 100, 1, TownSummary.Of(two, Wall0 + 100));

            // Playing Town 1 for an hour, then switching to Town 2 at Wall0 + 1h: Town 1 is saved as it stands.
            one.Execute(new Sync(Wall0 + Hour));
            ulong townOne = one.State.ComputeHash();
            long simAtLeave = one.State.Clock.SimTime;
            s1.Save(one.State, one.Config, Wall0 + Hour, 1, TownSummary.Of(one, Wall0 + Hour));

            // Town 2 opens two hours on: its first Sync is its away advance, and Town 1 has not moved.
            Simulation openedTwo = Open(s2, two, Wall0 + 3 * Hour);
            Assert.That(openedTwo.State.ComputeHash(), Is.EqualTo(two.State.ComputeHash()), "Town 2 loads exactly as it was saved");
            long before = openedTwo.State.Clock.SimTime;
            openedTwo.Execute(new Sync(Wall0 + 3 * Hour));
            Assert.That(openedTwo.State.Clock.SimTime - before, Is.EqualTo(3 * Hour - 100), "Town 2 caught up on the time since it was left");
            Assert.That(openedTwo.DrainEvents().OfType<AwayAdvanceCompleted>().Single().Capped, Is.False);
            s2.Save(openedTwo.State, openedTwo.Config, Wall0 + 3 * Hour, 1, TownSummary.Of(openedTwo, Wall0 + 3 * Hour));

            // Back to Town 1 at Wall0 + 5h: exactly as saved, then four hours of its own time away.
            Simulation backOne = Open(s1, one, Wall0 + 5 * Hour);
            Assert.That(backOne.State.ComputeHash(), Is.EqualTo(townOne));
            backOne.Execute(new Sync(Wall0 + 5 * Hour));
            Assert.That(backOne.State.Clock.SimTime - simAtLeave, Is.EqualTo(4 * Hour));
        }

        // AC-3: a town left more than 72 h catches up 72 h, as a closed app does.
        [Test]
        public void AwayCap()
        {
            Simulation one = NewTown(3), two = NewTown(8);
            SaveService s1 = Slot(1), s2 = Slot(2);
            s1.Save(one.State, one.Config, Wall0, 1, TownSummary.Of(one, Wall0));
            s2.Save(two.State, two.Config, Wall0, 1, TownSummary.Of(two, Wall0));
            long cap = one.Clock.AwayCapHours * 3600L;

            // Five days of playing Town 2; Town 1 was left at Wall0 and is opened now.
            Simulation opened = Open(s1, one, Wall0 + 5 * Day);
            long before = opened.State.Clock.SimTime;
            opened.Execute(new Sync(Wall0 + 5 * Day));
            AwayAdvanceCompleted away = opened.DrainEvents().OfType<AwayAdvanceCompleted>().Single();
            Assert.That(away.Capped, Is.True);
            Assert.That(away.AppliedSeconds, Is.EqualTo(cap));
            Assert.That(opened.State.Clock.SimTime - before, Is.EqualTo(cap));
        }
    }
}
