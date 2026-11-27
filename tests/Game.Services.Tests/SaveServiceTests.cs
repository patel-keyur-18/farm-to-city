using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Saves;
using static Game.Services.Tests.TownKit;

namespace Game.Services.Tests
{
    /// <summary>Spec 031 (two towns): each slot's own folder, Town 1 where it has always been, and the summary beside a save. Runs the real Unity-side <see cref="SaveService"/> on a temp folder.</summary>
    public class SaveServiceTests
    {
        private string _root;

        [SetUp]
        public void MakeFolder() { _root = Path.Combine(Path.GetTempPath(), "ftc-slots-" + Guid.NewGuid().ToString("N")); }

        [TearDown]
        public void DropFolder() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

        private string Saves { get { return Path.Combine(_root, "Saves"); } }
        private string Docs { get { return Path.Combine(_root, "Documents"); } }
        private static Sim.Core.Definitions.SaveDef Def() { return SaveDef(); }
        private SaveService Service(int slot) { return new SaveService(Saves, Docs, Def(), slot); }

        private static string[] Names(string dir) { return Directory.GetFiles(dir).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray(); }

        // AC-4: Town 1's backups, recovery and New game never touch Town 2's folder, and the reverse.
        [Test]
        public void SlotsApart()
        {
            Simulation a = NewTown(3), b = NewTown(8);
            b.AdvanceTo(b.State.Clock.SimTime + 5 * Hour);
            SaveService one = Service(1), two = Service(2);
            two.Save(b.State, b.Config, Wall0, 1, TownSummary.Of(b, Wall0));
            string twoDir = Path.Combine(Saves, "town2");
            string[] twoFiles = Names(twoDir);
            byte[] twoBytes = File.ReadAllBytes(Path.Combine(twoDir, SavePoints.Latest));
            Assert.That(twoFiles, Is.EquivalentTo(new[] { "town.ftc", "summary.json" }));

            // Five days of hourly saves in Town 1 rotate its backups; New game then deletes them: Town 2's folder never changes.
            for (int h = 0; h <= 5 * 24; h++)
            {
                a.AdvanceTo(a.State.Clock.SimTime + Hour);
                one.Save(a.State, a.Config, Wall0 + h * 3600L, 1, TownSummary.Of(a, Wall0 + h * 3600L));
            }
            Assert.That(Names(Saves), Does.Contain("town.daily.1.ftc").And.Contain("town.hourly.1.ftc"));
            Assert.That(Names(twoDir), Is.EqualTo(twoFiles));
            one.DeleteTown();
            Assert.That(Names(Saves).Where(n => n.StartsWith("town.")), Is.Empty, "Town 1's town is gone");
            Assert.That(Names(twoDir), Is.EqualTo(twoFiles), "New game in Town 1 left Town 2 alone");
            Assert.That(File.ReadAllBytes(Path.Combine(twoDir, SavePoints.Latest)), Is.EqualTo(twoBytes));
            Assert.That(Service(2).Load(b.Config, Wall0, 0).State.ComputeHash(), Is.EqualTo(b.State.ComputeHash()));

            // The reverse: Town 1 saved again, Town 2 replaced, and Town 1 is untouched; a bad Town 1 doesn't spoil Town 2 either.
            one.Save(a.State, a.Config, Wall0, 1, TownSummary.Of(a, Wall0));
            string[] oneFiles = Names(Saves).Where(n => n.StartsWith("town") || n == "summary.json").ToArray();
            byte[] oneBytes = File.ReadAllBytes(Path.Combine(Saves, SavePoints.Latest));
            two.DeleteTown();
            Assert.That(Names(twoDir).Where(n => n.StartsWith("town.")), Is.Empty);
            Assert.That(Names(Saves).Where(n => n.StartsWith("town") || n == "summary.json"), Is.EqualTo(oneFiles));
            Assert.That(File.ReadAllBytes(Path.Combine(Saves, SavePoints.Latest)), Is.EqualTo(oneBytes));
            Assert.That(SaveService.SlotHasTown(Saves, 1), Is.True);
            Assert.That(SaveService.SlotHasTown(Saves, 2), Is.False, "a replaced slot with nothing left is empty");

            // A town that can't be read is kept and copied to Documents under its own slot's name, and the other slot still loads.
            two.Save(b.State, b.Config, Wall0, 1, TownSummary.Of(b, Wall0));
            byte[] bad = File.ReadAllBytes(Path.Combine(twoDir, SavePoints.Latest));
            bad[bad.Length / 2] ^= 0xFF;
            File.WriteAllBytes(Path.Combine(twoDir, SavePoints.Latest), bad);
            LoadOutcome broken = Service(2).Load(b.Config, Wall0, 0);
            Assert.That(broken.State, Is.Null);
            Assert.That(Directory.GetFiles(Docs).Select(Path.GetFileName).Single(), Does.StartWith("Town2-town.unreadable-"));
            Assert.That(Service(1).Load(a.Config, Wall0, 0).State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()));
        }

        // AC-5: an existing player's Town 1 loads from today's folder, unchanged, and no second folder appears.
        [Test]
        public void TownOneInPlace()
        {
            Simulation a = NewTown(3);
            a.AdvanceTo(a.State.Clock.SimTime + 3 * Hour);
            // What the app wrote before two towns existed: SavePoints on the saves folder itself.
            new SavePoints(new FileStore(Saves), Def()).Save(a.State, a.Config, Wall0, 1);

            SaveService one = Service(1);
            Assert.That(one.Folder, Is.EqualTo(Saves));
            Assert.That(one.PlayLogPath, Is.EqualTo(Path.Combine(Saves, "playlog.jsonl")));
            Assert.That(SaveService.SlotHasTown(Saves, 1), Is.True);
            Assert.That(SaveService.SlotHasTown(Saves, 2), Is.False);
            LoadOutcome o = one.Load(a.Config, Wall0, 0);
            Assert.That(o.State.ComputeHash(), Is.EqualTo(a.State.ComputeHash()));
            Assert.That(o.Restored, Is.False);
            Assert.That(Directory.Exists(Path.Combine(Saves, "town2")), Is.False, "reading Town 1 makes no folder for Town 2");
            Assert.That(SaveService.FolderFor(Saves, 2), Is.EqualTo(Path.Combine(Saves, "town2")));
            Assert.That(one.TownTag, Is.EqualTo("Town1"));
            Assert.That(Service(2).TownTag, Is.EqualTo("Town2"));
        }

        // Export names the town (AC-10); the summary sits beside the save, is read back, and falls back to the save's header.
        [Test]
        public void ExportNamedAndSummary()
        {
            Simulation a = NewTown(3);
            a.AdvanceTo(a.State.Clock.SimTime + 30 * Hour);
            SaveService two = Service(2);
            two.Save(a.State, a.Config, Wall0, 1, TownSummary.Of(a, Wall0));
            Assert.That(two.Export(Wall0, 330), Does.StartWith("FarmToCity-Town2-").And.EndWith(".ftc"));
            Assert.That(Service(1).TownTag, Is.EqualTo("Town1"));

            TownSummary s = two.ReadSummary();
            Assert.That(s.Day, Is.EqualTo(2));
            Assert.That(s.SavedWallUtc, Is.EqualTo(Wall0));
            Assert.That(s.NetWorth, Is.EqualTo(TownSummary.Of(a, Wall0).NetWorth));

            File.Delete(Path.Combine(Saves, "town2", TownSummary.FileName)); // lost: the header gives the day and when, not the era or net worth
            TownSummary fallback = Service(2).ReadSummary();
            Assert.That(fallback.Day, Is.EqualTo(2));
            Assert.That(fallback.SavedWallUtc, Is.EqualTo(Wall0));
            Assert.That(fallback.Era, Is.Null);
            Assert.That(fallback.NetWorth, Is.EqualTo(TownSummary.Unknown));

            Assert.That(TownSummary.Parse("not json"), Is.Null);
            Assert.That(TownSummary.Parse(new TownSummary("village", 64, -5, 123).ToJson()).NetWorth, Is.EqualTo(-5), "a negative net worth reads back");
        }
    }
}
