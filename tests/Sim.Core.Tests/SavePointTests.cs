using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>An <see cref="IFileStore"/> in memory, which can "crash" (throw) at a chosen operation.</summary>
    public sealed class MemoryStore : IFileStore
    {
        public readonly SortedDictionary<string, byte[]> Files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);

        /// <summary>Operations left before a simulated crash; −1 never crashes.</summary>
        public int CrashAfter = -1;

        public sealed class Crash : Exception { }

        private void Step()
        {
            if (CrashAfter == 0) throw new Crash();
            if (CrashAfter > 0) CrashAfter--;
        }

        public byte[] Read(string name) { return Files.TryGetValue(name, out byte[] b) ? b : null; }
        public void Write(string name, byte[] bytes) { Step(); Files[name] = (byte[])bytes.Clone(); }

        public void Rename(string from, string to)
        {
            Step();
            Files[to] = Files[from];
            Files.Remove(from);
        }

        public void Delete(string name) { Step(); Files.Remove(name); }
        public bool Exists(string name) { return Files.ContainsKey(name); }
        public IReadOnlyList<string> List() { return Files.Keys.ToList(); }
    }

    /// <summary>Spec 007 §3.3: atomic writes, rotation, recovery and when to save (AC-7 to AC-9).</summary>
    public class SavePointTests
    {
        private static SavesDefs Defs() { return ContentLoader.LoadSaves(TestContent.ReadContent("saves.json")); }

        private static Simulation Small() { return NewFarm(seed: 3); }

        [Test]
        public void AtomicWrite_CrashSafe()
        {
            SimConfig config = Small().Config;
            Simulation sim = Small();
            var store = new MemoryStore();
            var points = new SavePoints(store, Defs().Save);
            points.Save(sim.State, config, Wall0);
            ulong first = sim.State.ComputeHash();

            sim.AdvanceTo(sim.State.Clock.SimTime + 2 * Hour);
            Assert.That(sim.State.ComputeHash(), Is.Not.EqualTo(first));

            // A crash at each step of the next save (an hour later, so it also rotates) leaves the previous save loadable.
            for (int crashAt = 0; ; crashAt++)
            {
                var copy = new MemoryStore();
                foreach (KeyValuePair<string, byte[]> f in store.Files) copy.Files[f.Key] = f.Value;
                copy.CrashAfter = crashAt;
                var p = new SavePoints(copy, Defs().Save);
                bool crashed = false;
                try { p.Save(sim.State.Clone(), config, Wall0 + 3600); }
                catch (MemoryStore.Crash) { crashed = true; }
                copy.CrashAfter = -1;

                LoadOutcome o = new SavePoints(copy, Defs().Save).LoadNewestValid(config);
                Assert.That(o.State, Is.Not.Null, "crash at step " + crashAt);
                Assert.That(o.State.ComputeHash(), Is.EqualTo(crashed ? first : sim.State.ComputeHash()), "crash at step " + crashAt);
                Assert.That(o.File, Is.EqualTo(SavePoints.Latest), "the latest file is always whole: no fallback needed");
                if (!crashed) break;
            }
        }

        [Test]
        public void Rotation_AndRecovery()
        {
            SimConfig config = Small().Config;
            Simulation sim = Small();
            var store = new MemoryStore();
            var points = new SavePoints(store, Defs().Save);
            long now = Wall0;
            for (int h = 0; h <= 5 * 24; h++, now += 3600)
            {
                sim.AdvanceTo(sim.State.Clock.SimTime + Hour);
                points.Save(sim.State, config, now);
            }
            long last = now - 3600;
            Assert.That(store.List(), Is.EquivalentTo(new[] { "town.ftc", "town.hourly.1.ftc", "town.hourly.2.ftc", "town.hourly.3.ftc",
                "town.daily.1.ftc", "town.daily.2.ftc", "town.daily.3.ftc" }));
            long Saved(string f) { return SaveCodec.ReadHeader(store.Read(f)).SavedAtWallUtc; }
            Assert.That(Saved("town.ftc"), Is.EqualTo(last));
            Assert.That(new[] { 1, 2, 3 }.Select(i => last - Saved("town.hourly." + i + ".ftc")), Is.EqualTo(new[] { 3600L, 7200, 10800 }));
            long[] daily = new[] { 1, 2, 3 }.Select(i => Saved("town.daily." + i + ".ftc")).ToArray();
            Assert.That(daily[0] - daily[1], Is.EqualTo(86400));
            Assert.That(daily[1] - daily[2], Is.EqualTo(86400));
            Assert.That(last - daily[0], Is.LessThanOrEqualTo(86400));

            // The latest corrupted: the newest valid backup loads, and says which.
            byte[] bad = (byte[])store.Read("town.ftc").Clone();
            bad[bad.Length / 2] ^= 0xFF;
            store.Files["town.ftc"] = bad;
            LoadOutcome o = points.LoadNewestValid(config);
            Assert.That(o.File, Is.EqualTo("town.hourly.1.ftc"));
            Assert.That(o.Restored, Is.True);
            Assert.That(o.SavedAtWallUtc, Is.EqualTo(last - 3600));
            Assert.That(o.State.ComputeHash(), Is.EqualTo(SaveCodec.Read(store.Read("town.hourly.1.ftc"), config).State.ComputeHash()));
        }

        [Test]
        public void TooOld_KeptAndNewTown()
        {
            SimConfig config = Small().Config;
            var store = new MemoryStore();
            var points = new SavePoints(store, Defs().Save);
            points.Save(Small().State, config, Wall0);
            points.Save(Small().State, config, Wall0 + 2 * 3600);
            var newer = new SaveDef(SaveCodec.SchemaVersion + 1, 3, 3, 60, 5, 100_000); // a build that raised minSchema
            LoadOutcome o = new SavePoints(store, newer).LoadNewestValid(config, Wall0 + 3 * 3600);
            Assert.That(o.State, Is.Null);
            Assert.That(o.Error, Is.EqualTo(SaveError.SaveTooOld));
            Assert.That(store.List(), Is.EqualTo(new[] { "town.unreadable-" + DateTimeOffset.FromUnixTimeSeconds(Wall0 + 3 * 3600).UtcDateTime.ToString("yyyyMMdd-HHmm") + ".ftc" }),
                "the old town is kept under a new name; its backups are gone");
        }

        [Test]
        public void SavesContent_Validation()
        {
            SavesDefs d = Defs();
            Assert.That((d.Save.HourlyBackups, d.Save.DailyBackups, d.Save.MaxLogEntries), Is.EqualTo((3, 3, 100_000)));
            Assert.That((d.Notify.QuietFromMinute, d.Notify.QuietToMinute, d.Notify.InfoGroupMinutes, d.Notify.MaxPending), Is.EqualTo((22 * 60, 7 * 60, 240, 64)));
            string json = TestContent.ReadContent("saves.json");
            Assert.That(() => ContentLoader.LoadSaves(json.Replace("\"22:00\"", "\"25:00\"")), Throws.TypeOf<ContentException>().With.Message.Contains("saves.notify.quietFrom"));
            Assert.That(() => ContentLoader.LoadSaves(json.Replace("\"maxPending\": 64", "\"maxPending\": 65")), Throws.TypeOf<ContentException>().With.Message.Contains("saves.notify.maxPending"));
        }

        [Test]
        public void Debounce()
        {
            SaveDef def = Defs().Save;
            var policy = new AutosavePolicy(def);
            long t = Wall0;
            policy.Saved(t);
            int saves = 0;
            // A burst of 10 commands within 5 seconds, then quiet: one save, 5 seconds after the last.
            for (int s = 1; s <= 60; s++)
            {
                if (s <= 5) { policy.OnCommand(t + s); policy.OnCommand(t + s); }
                if (policy.Due(t + s)) { saves++; policy.Saved(t + s); Assert.That(s, Is.EqualTo(10)); }
            }
            Assert.That(saves, Is.EqualTo(1));

            // Live Syncs alone (no commands) cause none until the hourly save.
            int hourly = 0;
            for (long s = 61; s <= 61 + 2 * 3600; s++)
                if (policy.Due(t + s)) { hourly++; policy.Saved(t + s); }
            Assert.That(hourly, Is.EqualTo(2));
        }
    }
}
