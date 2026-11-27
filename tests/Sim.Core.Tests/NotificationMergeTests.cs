using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Content;
using Sim.Core.Saves;
using static Sim.Core.Tests.TestSim;

namespace Sim.Core.Tests
{
    /// <summary>Spec 031 §3.1 (owner, 2026-11-27): the open town's notifications and the other town's kept plan, merged at each backgrounding.</summary>
    public class NotificationMergeTests
    {
        private static Definitions.NotifyDef Def() { return ContentLoader.LoadSaves(TestContent.ReadContent("saves.json")).Notify; }

        private const long Midnight = Wall0 - Wall0 % 86400; // 00:00 UTC; the offset in these tests is 0
        private const long Now = Midnight + 12 * Hour; // noon; the quiet hours are 22:00 to 07:00

        private static KeptNote Note(long at, NotifyKind kind, int town, string title = "t") { return new KeptNote(at, kind, title + town, "b", town); }

        [Test]
        public void Merge()
        {
            Definitions.NotifyDef def = Def();
            var open = new List<KeptNote> { Note(Now + 3 * Hour, NotifyKind.CropReady, 1), Note(Now + 5 * Hour, NotifyKind.HandsWork, 1) };
            var other = new List<KeptNote>
            {
                Note(Now + 1 * Hour, NotifyKind.CropReady, 2),                 // not yet due: stays kept and is scheduled
                Note(Now - 2 * Hour, NotifyKind.FinalNotice, 2),               // urgent, past due: goes out once, a minute from now
                Note(Now - 3 * Hour, NotifyKind.CropReady, 2),                 // needs you, past due: goes out once
                Note(Now - 4 * Hour, NotifyKind.CalfBorn, 2),                  // information, past due: dropped
            };
            List<KeptNote> all = KeptNotes.Merge(open, other, Now, Now - 6 * Hour, def, 0, out List<KeptNote> kept);

            Assert.That(all.Select(n => n.Kind + "@" + (n.WallUtc - Now) + ":" + n.Town), Is.EqualTo(new[]
            {
                "FinalNotice@60:2", "CropReady@60:2", "CropReady@" + 1 * Hour + ":2", "CropReady@" + 3 * Hour + ":1", "HandsWork@" + 5 * Hour + ":1",
            }), "time order; the past-due information note is gone");
            Assert.That(kept.Select(n => n.Kind + "@" + (n.WallUtc - Now)), Is.EqualTo(new[] { "CropReady@" + 1 * Hour }), "the other town keeps only what has not come due");
            Assert.That(all.Select(n => n.WallUtc), Is.Ordered);

            // The open town alone (no other town's plan) is the one-town result, untouched.
            Assert.That(KeptNotes.Merge(open, new List<KeptNote>(), Now, Now - 6 * Hour, def, 0, out _).Select(n => n.Title), Is.EqualTo(open.Select(n => n.Title)));
        }

        // A note the platform already delivered while the app was closed is not sent again (review M1): only what came due since the app last came forward is past due.
        [Test]
        public void DeliveredWhileClosed_NotSentAgain()
        {
            Definitions.NotifyDef def = Def();
            var other = new List<KeptNote>
            {
                Note(Now - 3 * Hour, NotifyKind.CropReady, 2),     // due at 09:00, before the app came forward at 11:00: the platform delivered it
                Note(Now - 30 * 60, NotifyKind.FinalNotice, 2),    // due at 11:30 while you played: goes out once
            };
            List<KeptNote> all = KeptNotes.Merge(new List<KeptNote>(), other, Now, Now - Hour, def, 0, out List<KeptNote> kept);
            Assert.That(all.Select(n => n.Kind), Is.EqualTo(new[] { NotifyKind.FinalNotice }), "the delivered note is not sent a second time");
            Assert.That(kept, Is.Empty, "and it leaves the kept file");
        }

        // A past-due note goes out after quiet hours, never in them.
        [Test]
        public void PastDue_AfterQuietHours()
        {
            Definitions.NotifyDef def = Def();
            long night = Midnight + 23 * Hour; // 23:00, inside the quiet hours
            List<KeptNote> all = KeptNotes.Merge(new List<KeptNote>(), new List<KeptNote> { Note(night - Hour, NotifyKind.LoanMissed, 2) }, night, night - 6 * Hour, def, 0, out _);
            long end = Midnight + 24 * Hour + def.QuietToMinute * 60L;
            Assert.That(all.Single().WallUtc, Is.EqualTo(end), "the quiet hours end at " + def.QuietToMinute / 60 + ":00 the next morning");
        }

        // Over the platform's limit the most important stay, not the soonest.
        [Test]
        public void OverTheLimit_ImportanceFirst()
        {
            Definitions.NotifyDef def = Def();
            var open = new List<KeptNote>();
            for (int i = 0; i < def.MaxPending; i++) open.Add(Note(Now + (i + 1) * 60L, NotifyKind.CalfBorn, 1, "info" + i)); // 64 information notes, soonest
            var other = new List<KeptNote> { Note(Now + 100 * Hour, NotifyKind.FinalNotice, 2), Note(Now + 99 * Hour, NotifyKind.CropReady, 2) };
            List<KeptNote> all = KeptNotes.Merge(open, other, Now, Now - 6 * Hour, def, 0, out List<KeptNote> kept);
            Assert.That(all.Count, Is.EqualTo(def.MaxPending));
            Assert.That(all.Select(n => n.Kind), Does.Contain(NotifyKind.FinalNotice).And.Contain(NotifyKind.CropReady), "urgent and needs-you beat 64 sooner information notes");
            Assert.That(all.Count(n => n.Kind == NotifyKind.CalfBorn), Is.EqualTo(def.MaxPending - 2));
            Assert.That(all.Select(n => n.WallUtc), Is.Ordered);
            Assert.That(kept.Count, Is.EqualTo(2), "what is cut by the limit stays in the kept file");
        }

        // The kept file round-trips with awkward words, and a damaged line costs only that note.
        [Test]
        public void KeptFile_RoundTrips()
        {
            var notes = new List<KeptNote>
            {
                new KeptNote(Wall0, NotifyKind.CropReady, "Town 2 \u00b7 Wheat is ready in 2 fields", "Harvest \"before\" it spoils.\nSoon.", 2),
                new KeptNote(Wall0 + 5, NotifyKind.OrdersPosted, "\u0917\u0947\u0939\u0942\u0901 back\\slash", "b", 2),
            };
            string json = KeptNotes.ToJson(notes);
            List<KeptNote> back = KeptNotes.Parse(json);
            Assert.That(back.Select(n => n.Title + "|" + n.Body + "|" + n.WallUtc + "|" + n.Kind + "|" + n.Town), Is.EqualTo(notes.Select(n => n.Title + "|" + n.Body + "|" + n.WallUtc + "|" + n.Kind + "|" + n.Town)));
            Assert.That(KeptNotes.Parse(json.Replace("\"k\":3", "\"k\":x")).Count, Is.EqualTo(1), "a damaged line is skipped");
            Assert.That(KeptNotes.Parse("garbage"), Is.Empty);
            Assert.That(KeptNotes.Parse(null), Is.Empty);
        }
    }
}
