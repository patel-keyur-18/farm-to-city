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

        // AC-1 (the save side): a second town starts in the empty slot as its own folder; Town 1 is saved and not touched by it.
        [Test]
        public void StartSecond()
        {
            Simulation one = NewTown(3), two = NewTown(8);
            SaveService s1 = Slot(1), s2 = Slot(2);
            Assert.That(SaveService.SlotHasTown(Path.Combine(_root, "Saves"), 2), Is.False, "the slot is empty: Settings offers Start a second town");
            one.Execute(new Sync(Wall0 + Hour));
            s1.Save(one.State, one.Config, Wall0 + Hour, 1, TownSummary.Of(one, Wall0 + Hour));    // LeaveOpenTown: Town 1 saved first
            s2.WriteNotes(new System.Collections.Generic.List<KeptNote>());
            s2.DeleteNotes();                                                                    // StartTown: the slot has no kept plan of an older town
            s2.Save(two.State, two.Config, Wall0 + Hour, 1, TownSummary.Of(two, Wall0 + Hour));
            Assert.That(SaveService.SlotHasTown(Path.Combine(_root, "Saves"), 2), Is.True);
            Assert.That(s2.Folder, Is.Not.EqualTo(s1.Folder));
            Assert.That(Open(s1, one, Wall0 + Hour).State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()), "Town 1 is exactly as it was saved");
            Assert.That(Open(s2, two, Wall0 + Hour).State.ComputeHash(), Is.EqualTo(two.State.ComputeHash()));
        }

        // Spec 031 §3 (the owner, 2026-11-27): a target that cannot be read leaves the open town as it is, and its slot is empty again.
        [Test]
        public void UnreadableTargetStaysPut()
        {
            Simulation one = NewTown(3), two = NewTown(8);
            SaveService s1 = Slot(1), s2 = Slot(2);
            s1.Save(one.State, one.Config, Wall0, 1, TownSummary.Of(one, Wall0));
            s2.Save(two.State, two.Config, Wall0, 1, TownSummary.Of(two, Wall0));
            string saves = Path.Combine(_root, "Saves");
            foreach (string f in Directory.GetFiles(s2.Folder)) if (!f.EndsWith(".json") && !f.EndsWith(".jsonl")) File.WriteAllText(f, "not a save");   // the save and every backup

            LoadOutcome o = s2.Load(two.Config, Wall0 + Hour, 0);
            Assert.That(o.State, Is.Null, "nothing to open");
            Assert.That(o.Error, Is.Not.EqualTo(SaveError.None), "the usual unreadable card has its reason");
            Assert.That(SaveService.SlotHasTown(saves, 2), Is.False, "the slot is empty again: Settings offers Start a second town");
            Assert.That(Open(s1, one, Wall0 + Hour).State.ComputeHash(), Is.EqualTo(one.State.ComputeHash()), "the open town is untouched");
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

        // AC-8: a tap on Town 2's notification opens Town 2; a tap on the open town's own note, an old note or an emptied slot opens nothing else.
        [Test]
        public void OpenFromNote()
        {
            Assert.That(NoteOpened.TownOfData("2|1800000000:3"), Is.EqualTo(2));
            Assert.That(NoteOpened.TownOfData("1800000000:3"), Is.EqualTo(0), "a note from an older build has no town");
            Assert.That(NoteOpened.TownOfData("9|1:1"), Is.EqualTo(0), "a slot that doesn't exist");
            Assert.That(NoteOpened.TownOfData(null), Is.EqualTo(0));

            bool Has(int slot) { return true; }
            Assert.That(new NoteOpened(true, 2).Elsewhere(1, Has), Is.EqualTo(2), "Town 1 is open and the note is Town 2's: switch");
            Assert.That(new NoteOpened(true, 2).Elsewhere(2, Has), Is.EqualTo(0), "the open town's own note: nothing to switch");
            Assert.That(new NoteOpened(false, 2).Elsewhere(1, Has), Is.EqualTo(0), "not a tap that opened the app (counted once)");
            Assert.That(new NoteOpened(true, 0).Elsewhere(1, Has), Is.EqualTo(0));
            Assert.That(new NoteOpened(true, 2).Elsewhere(1, slot => false), Is.EqualTo(0), "that slot holds no town now");

            // The kept plan lives in the town's own folder and goes when the town is opened.
            SaveService one = Slot(1), two = Slot(2);
            Assert.That(two.ReadNotes(), Is.Empty);
            two.WriteNotes(new[] { new KeptNote(Wall0 + 5, NotifyKind.CropReady, "Town 2 · Wheat is ready", "b", 2) });
            Assert.That(File.Exists(Path.Combine(_root, "Saves", "town2", KeptNotes.FileName)), Is.True);
            Assert.That(File.Exists(Path.Combine(_root, "Saves", KeptNotes.FileName)), Is.False, "Town 1's folder holds nothing of it");
            Assert.That(two.ReadNotes().Single().Title, Is.EqualTo("Town 2 · Wheat is ready"));
            one.DeleteNotes();
            Assert.That(two.ReadNotes().Count, Is.EqualTo(1), "opening Town 1 leaves Town 2's plan alone");
            two.DeleteNotes();
            Assert.That(two.ReadNotes(), Is.Empty);
            two.WriteNotes(new KeptNote[0]);
            Assert.That(File.Exists(Path.Combine(_root, "Saves", "town2", KeptNotes.FileName)), Is.False, "an empty plan is no file");
        }

        // AC-9: each town's play log is in its own folder and holds only its own sessions; a switch ends one and starts the other.
        [Test]
        public void PlayLogPerTown()
        {
            SaveService s1 = Slot(1), s2 = Slot(2);
            var def = ContentLoaderPlayLog();
            var log1 = new PlayLog(def, "") { Enabled = true };
            var log2 = new PlayLog(def, "") { Enabled = true };

            // What GameRoot does: Town 1's session, a switch (end one, start the other), a tap-opened return to Town 1.
            log1.SessionStart(Wall0, 0, "b", "d", 330, false);
            File.WriteAllText(s1.PlayLogPath, log1.Text);
            log1.SessionEnd(Wall0 + 100, 100, 100);
            log2.NewGame(Wall0 + 100, 0);
            log2.SessionStart(Wall0 + 100, 0, "b", "d", 330, false);
            Directory.CreateDirectory(s2.Folder);
            File.WriteAllText(s1.PlayLogPath, log1.Text);
            File.WriteAllText(s2.PlayLogPath, log2.Text);
            log2.SessionEnd(Wall0 + 400, 300, 300);
            log1.SessionStart(Wall0 + 400, 100, "b", "d", 330, true);
            File.WriteAllText(s1.PlayLogPath, log1.Text);
            File.WriteAllText(s2.PlayLogPath, log2.Text);

            string[] a = File.ReadAllLines(s1.PlayLogPath), b = File.ReadAllLines(s2.PlayLogPath);
            Assert.That(a.Select(Type), Is.EqualTo(new[] { "sessionStart", "sessionEnd", "sessionStart" }));
            Assert.That(b.Select(Type), Is.EqualTo(new[] { "newGame", "sessionStart", "sessionEnd" }), "Town 2's log has only Town 2's sessions");
            Assert.That(a[2], Does.Contain("\"fromNote\":true"));
            Assert.That(a[0], Does.Contain("\"fromNote\":false"));
            Assert.That(s1.PlayLogPath, Is.Not.EqualTo(s2.PlayLogPath));
            Assert.That(new PlayLog(def, File.ReadAllText(s2.PlayLogPath)).Lines.Count, Is.EqualTo(3), "a log read back is the town's own");
        }

        private static string Type(string line) { int at = line.IndexOf("\"type\":\"", System.StringComparison.Ordinal) + 8; return line.Substring(at, line.IndexOf('"', at) - at); }

        private static Sim.Core.Definitions.PlayLogDef ContentLoaderPlayLog() { return Sim.Content.ContentLoader.LoadSaves(ReadContent("saves.json")).PlayLog; }
    }
}
