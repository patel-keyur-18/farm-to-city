using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Farm;
using Sim.Core.Saves;
using Sim.View.Views;

namespace Sim.View.Tests
{
    /// <summary>Spec 007 §7 and ADR 0041: the Settings and Import sheets, the cards, the notification ask and texts.</summary>
    public class SettingsViewTests
    {
        private const long Wall0 = 1_800_000_000; // 08:00 UTC; 13:30 in India
        private const int India = 330;

        private static SettingsInfo Info(long saved = Wall0 - 600) { return new SettingsInfo { NowWallUtc = Wall0, UtcOffsetMinutes = India, LastSavedWallUtc = saved, PlayLogLines = 3, ImportFiles = 2 }; }

        [Test]
        public void Settings_OneRowPerChoice()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            SheetVm vm = SettingsSheet.Build(ctx, Info());
            Assert.That(vm.Lines.Select(l => l.Label + " | " + l.Value + " | " + (l.Action == null ? "-" : l.Action.Label)), Is.EqualTo(new[]
            {
                "Saved automatically | Last at 13:20 today | Save now",
                "Notifications | No nudges · tap Off to turn them on | Off",
                "Play log | Not kept · helps the developer when on | Off",
                "Numbers |  | -",
                "Indian: 1,20,000 | Lakh and crore | Chosen",
                "Western: 120,000 | Thousands and millions | Choose",
                "Text size |  | -",
                "Small | 80% | Choose",
                "Normal | 100% | Chosen",
                "Large | 125% | Choose",
                "Motion and sound |  | -",
                "Reduced motion | Full motion: bounces and flying coins | Off",
                "Sound | Little sounds when you harvest and sell | On",
                "Haptics | A light tap on harvest and sale | On",
                "Files |  | -",
                "Export Town 1 | A copy to the Files app | Export",
                "Import into Town 1 | 2 town files in Files | Choose",
                "Export play log of Town 1 | 3 entries | Export",
                "New game in Town 1 | Replaces Town 1 for good | New game…",
            }));
            Assert.That(vm.Lines.Last().Action.Kind, Is.EqualTo(ActionKind.Danger));
            Assert.That(vm.Lines[1].Action.Kind, Is.EqualTo(ActionKind.Danger), "a switch that is off is a red Off");
            LineVm Row(SheetVm v, string label) { return v.Lines.Single(l => l.Label == label); }
            Assert.That(Row(vm, "Import into Town 1").Action.Opens.Value.Kind, Is.EqualTo("Import"));
            Assert.That(Row(vm, "Large").Action.Id, Is.EqualTo(SettingsIds.TextPrefix + "125"));
            foreach (LineVm l in vm.Lines.Where(l => l.Value != null && l.Value.Replace("Town 1", "").Any(char.IsDigit))) // a town's name is not a number
                Assert.That(WhyView.Has(l.WhyKey), Is.True, "a Why? for " + l.Label);

            SheetVm on = SettingsSheet.Build(ctx, new SettingsInfo { NowWallUtc = Wall0, NotificationsOn = true, PlayLogOn = true });
            Assert.That(on.Lines[0].Value, Is.EqualTo("Not saved yet"));
            Assert.That(on.Lines[1].Value + " / " + on.Lines[1].Action.Label, Is.EqualTo("A nudge when crops are ready · never at night / On"));
            Assert.That(on.Lines[1].Action.Kind, Is.EqualTo(ActionKind.Primary), "a switch that is on is a green On");
            Assert.That(on.Lines[2].Action.Label, Is.EqualTo("On"));
            Assert.That(on.Lines.Single(l => l.Label == "Export play log of Town 1").Action.Enabled, Is.False, "an empty play log can't be exported");
        }

        [Test]
        public void Import_ListsFiles_NewestFirst_WithReasons()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            Simulation sim = ViewKit.NewGame();
            SaveHeader h = SaveCodec.ReadHeader(SaveCodec.Write(sim.State, sim.Config, Wall0 - 86400 * 3));
            SaveHeader newer = SaveCodec.ReadHeader(SaveCodec.Write(sim.State, sim.Config, Wall0 - 3600));
            var files = new List<ImportFile>
            {
                new ImportFile { Name = "FarmToCity-2027-01-12-0800.ftc", Header = h },
                new ImportFile { Name = "broken.ftc", Problem = SaveError.CorruptSave },
                new ImportFile { Name = "FarmToCity-2027-01-15-0700.ftc", Header = newer },
            };
            SheetVm vm = ImportSheet.Build(ctx, files, Info());
            Assert.That(vm.Lines.Select(l => l.Label), Is.EqualTo(new[] { "FarmToCity-2027-01-15-0700.ftc", "FarmToCity-2027-01-12-0800.ftc", "broken.ftc" }));
            Assert.That(vm.Lines[0].Value, Does.EndWith("saved 12:30 today"));
            Assert.That(vm.Lines[0].Value, Does.Contain("Spring"));
            Assert.That(vm.Lines[1].Value, Does.EndWith("saved 12 Jan, 13:30"));
            Assert.That(vm.Lines[2].Value, Is.EqualTo("Damaged: it can't be opened."));
            Assert.That(vm.Lines[2].Action.Enabled, Is.False);
            Assert.That(vm.Lines[0].Action.Id, Is.EqualTo(SettingsIds.ImportPrefix + "FarmToCity-2027-01-15-0700.ftc"));
            Assert.That(ImportSheet.Build(ctx, new List<ImportFile>(), Info()).Lines.Single().Label, Does.StartWith("No town files yet"));
        }

        [Test]
        public void Cards_ReplaceRestoredUnreadable()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            CardVm replace = Cards.Replace(ctx, Info());
            Assert.That(replace.Buttons.Select(b => b.Id), Is.EqualTo(new[] { SettingsIds.ExportFirst, SettingsIds.Replace, SettingsIds.Cancel }));
            Assert.That(replace.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "Export first", "Replace Town 1", "Cancel" }));
            Assert.That(Cards.Replace(ctx, Info(), "old.ftc").Title, Is.EqualTo("Import old.ftc into Town 1?"));
            Assert.That(Cards.Restored(ctx, Wall0 - 7200, Info()).Text, Is.EqualTo("It came back from the save of 11:30 today. A few minutes of play may be missing."));
            Assert.That(Cards.Unreadable(ctx, SaveError.SaveTooOld, "town.unreadable-20261019-0800.ftc").Text, Does.Contain("older test build").And.Contain("town.unreadable-20261019-0800.ftc"));
        }

        [Test]
        public void NotifyAsk_AfterTheLongCrop_ThenAfterTheFirstClose()
        {
            Simulation sim = ViewKit.NewGame();
            long t = sim.State.Clock.SimTime;
            sim.Execute(new Sow(t, 86, "radish", SeedSource.Certified, "none")); // two hours: not the long crop
            Assert.That(NotifyAsk.Moment(sim.DrainEvents(), 0, false, sim.State.Clock.LastWallUtc, t), Is.Null);
            sim.Execute(new Sow(t, 102, "potato", SeedSource.Certified, "none"));
            NotifyAskMoment? m = NotifyAsk.Moment(sim.PendingEvents, 0, false, sim.State.Clock.LastWallUtc, t);
            Assert.That(m.HasValue, Is.True);
            Assert.That(m.Value.CropId, Is.EqualTo("potato"));
            Assert.That(m.Value.ReadyWallUtc, Is.EqualTo(sim.State.Clock.LastWallUtc + sim.State.Farm.CropOn(102).ReadyAt - t));
            Assert.That(NotifyAsk.Moment(sim.PendingEvents, 0, true, Wall0, t), Is.Null, "never when already on");
            Assert.That(NotifyAsk.Moment(sim.PendingEvents, 1, false, Wall0, t), Is.Null, "the second ask waits for the first season close");
            Assert.That(NotifyAsk.Moment(sim.PendingEvents, 2, false, Wall0, t), Is.Null, "never a third");

            ViewContext ctx = ViewKit2.Context(sim);
            CardVm card = Cards.NotifyAsk(ctx, m.Value, new SettingsInfo { NowWallUtc = sim.State.Clock.LastWallUtc, UtcOffsetMinutes = 0 });
            Assert.That(card.Text, Does.Match("^Potato will be ready (today|tonight|tomorrow) at \\d\\d:\\d\\d\\.")); // spec 011 §3.6b
            Assert.That(card.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "Yes, nudge me", "Not now" }));

            sim.AdvanceTo(sim.Clock.SecondsPerSeason + 1);
            NotifyAskMoment? again = NotifyAsk.Moment(sim.DrainEvents(), 1, false, Wall0, sim.State.Clock.SimTime);
            Assert.That(again.HasValue && again.Value.CropId == null, Is.True);
            Assert.That(Cards.NotifyAsk(ctx, again.Value, Info()).Text, Does.StartWith("Your first season is done."));
        }

        [Test]
        public void NotificationText_KindCountCrop_NoMoney()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            PlannedNotification N(NotifyKind k, int count, string crop) { return NotificationPlanner.Group(new[] { new AbsenceItem(Wall0, k, crop) }.Concat(Enumerable.Range(1, count - 1).Select(i => new AbsenceItem(Wall0 + i, k, crop))).ToList(), Def(), 0)[0]; }
            Assert.That(NotificationText.For(ctx, N(NotifyKind.CropReady, 1, "wheat")).Title, Is.EqualTo("Wheat is ready"));
            Assert.That(NotificationText.For(ctx, N(NotifyKind.CropReady, 3, "wheat")).Title, Is.EqualTo("Wheat is ready in 3 fields"));
            Assert.That(NotificationText.For(ctx, N(NotifyKind.BedReady, 1, "garden_spinach")).Title, Is.EqualTo("Spinach is ready in the garden"));
            Assert.That(NotificationText.For(ctx, N(NotifyKind.FruitRipe, 2, "mango")).Title, Is.EqualTo("Mango is ripe on 2 trees"));
            Assert.That(NotificationText.For(ctx, N(NotifyKind.HandsWork, 2, "rice")).Title, Is.EqualTo("Harvests in: Rice ×2"));
            var mixed = NotificationPlanner.Group(new[] { new AbsenceItem(Wall0, NotifyKind.CropReady, "wheat"), new AbsenceItem(Wall0 + 60, NotifyKind.CropReady, "potato") }, Def(), 0)[0];
            Assert.That(NotificationText.For(ctx, mixed).Title, Is.EqualTo("2 fields are ready"));
            foreach (NotifyKind k in System.Enum.GetValues(typeof(NotifyKind)))
            {
                (string title, string body) = NotificationText.For(ctx, N(k, 1, k == NotifyKind.CropReady || k == NotifyKind.HandsWork ? "wheat" : k == NotifyKind.BedReady ? "garden_radish" : k == NotifyKind.FruitRipe ? "mango" : k == NotifyKind.FeedLow ? "straw" : null));
                Assert.That(title, Does.Not.Contain("⟦").And.Not.Empty, k.ToString());
                Assert.That(body, Does.Not.Contain("⟦").And.Not.Empty, k.ToString());
                Assert.That(title + body, Does.Not.Match("[0-9]{3}"), "never a money amount: " + k);
            }
        }

        [Test]
        public void RegularCard()
        {
            Simulation sim = ViewKit.NewGame();
            ViewContext ctx = ViewKit2.Context(sim);
            Assert.That(Cards.Regular(ctx, 99), Is.Null);
        }

        private static Sim.Core.Definitions.NotifyDef Def() { return new Sim.Core.Definitions.NotifyDef(64, 30, 240, 22 * 60, 7 * 60); }

        // ---- spec 031: two towns ----

        private static TownInfo Town(int slot, bool exists = true, bool open = false, string era = "village", int day = 64, long worth = 142_000_000, long saved = Wall0 - 86_400 - 600)
            => new TownInfo { Slot = slot, Exists = exists, Open = open, EraId = exists ? era : null, Day = day, NetWorth = worth, SavedWallUtc = exists ? saved : 0 };

        private static SettingsInfo TwoTowns(int open)
        {
            SettingsInfo info = Info();
            info.OpenTown = open;
            info.Towns = new[] { Town(1, open: open == 1), Town(2, era: "hamlet", day: 6, worth: 21_000_000, open: open == 2) };
            return info;
        }

        // AC-1, AC-6 (Settings): one town shows it and "Start a second town"; two show a row each, the open one "Open now", the other "Switch".
        [Test]
        public void Towns()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            SettingsInfo one = Info();
            one.Towns = new[] { Town(1, open: true), Town(2, exists: false) };
            SheetVm vm = SettingsSheet.Build(ctx, one);
            Assert.That(vm.Lines.Take(4).Select(l => l.Label + " | " + l.Value + " | " + (l.Action == null ? "-" : l.Action.Label)), Is.EqualTo(new[]
            {
                "Your towns |  | -",
                "Town 1 | Village · day 64 · net worth 14,20,000 | Open now",
                "Start a second town | A new town beside this one. You keep both. | Start",
                "Saved automatically | Last at 13:20 today | Save now",
            }));
            Assert.That(vm.Lines[1].Action.Enabled, Is.False, "the open town's button does nothing");
            Assert.That(vm.Lines[2].Action.Id, Is.EqualTo(SettingsIds.TownStart));

            SheetVm two = SettingsSheet.Build(ctx, TwoTowns(open: 1));
            LineVm other = two.Lines[2];
            Assert.That(other.Label + " | " + other.Value + " | " + other.Action.Label + " | " + other.Note, Is.EqualTo("Town 2 | Hamlet · day 6 · net worth 2,10,000 | Switch | Last played 13:20 yesterday"));
            Assert.That(other.Action.Id, Is.EqualTo(SettingsIds.TownSwitchPrefix + "2"));
            Assert.That(other.Action.Enabled, Is.True);
            Assert.That(SettingsSheet.Build(ctx, TwoTowns(open: 2)).Lines[1].Action.Id, Is.EqualTo(SettingsIds.TownSwitchPrefix + "1"), "Town 1 switches when Town 2 is open");
            Assert.That(two.Lines[1].Note, Is.Null, "the open town has no last-played line");
            foreach (LineVm l in two.Lines.Skip(1).Take(2)) Assert.That(WhyView.Has(l.WhyKey), Is.True, "a Why? for " + l.Label);
            Assert.That(SettingsSheet.Build(ctx, one).Lines[2].WhyKey, Is.EqualTo("townStart"));

            // Without a ledger-less summary (header only), the row leaves out what it doesn't know.
            SettingsInfo bare = Info();
            bare.Towns = new[] { Town(1, open: true), new TownInfo { Slot = 2, Exists = true, Day = 3, NetWorth = Sim.Core.Saves.TownSummary.Unknown, SavedWallUtc = Wall0 - 60 } };
            Assert.That(SettingsSheet.Build(ctx, bare).Lines[2].Value, Is.EqualTo("day 3"));

            ctx.Towns = TwoTowns(open: 1).Towns;
            WhyVm why = WhyView.Build(ctx, "town", 2);
            Assert.That(why.Value, Is.EqualTo("2,10,000"));
            Assert.That(why.Rows.Select(r => r.Label), Does.Contain("Hamlet").Or.Contain("Era"));
        }

        // AC-10: Export, Import, the play log and New game name the open town, and the cards say what they replace.
        [Test]
        public void TownNamed()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            SheetVm two = SettingsSheet.Build(ctx, TwoTowns(open: 2));
            string[] labels = two.Lines.Select(l => l.Label).ToArray();
            Assert.That(labels, Does.Contain("Export Town 2").And.Contain("Import into Town 2").And.Contain("Export play log of Town 2").And.Contain("New game in Town 2"));
            Assert.That(two.Lines.Single(l => l.Label == "New game in Town 2").Value, Is.EqualTo("Replaces Town 2 for good"));

            CardVm replace = Cards.Replace(ctx, TwoTowns(open: 2));
            Assert.That(replace.Title, Is.EqualTo("Start a new Town 2?"));
            Assert.That(replace.Text, Does.Contain("replaces Town 2 for good").And.Contain("Town 1 is not touched."));
            Assert.That(replace.Buttons[1].Label, Is.EqualTo("Replace Town 2"));
            Assert.That(Cards.Replace(ctx, TwoTowns(open: 1), "FarmToCity-Town2-x.ftc").Title, Is.EqualTo("Import FarmToCity-Town2-x.ftc into Town 1?"));
            Assert.That(Cards.Replace(ctx, Info()).Text, Does.Not.Contain("not touched"), "with one town there is no other town to name");
        }

        // Spec 031 §3: a switch to a town that can't be read leaves the open town as it is and says so.
        [Test]
        public void TownNotOpenedCard()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            CardVm card = Cards.TownNotOpened(ctx, 2, 1, SaveError.CorruptSave, "town.unreadable-20261127-1413.ftc");
            Assert.That(card.Title, Is.EqualTo("Town 2 couldn't be opened"));
            Assert.That(card.Text, Does.Contain("town.unreadable-20261127-1413.ftc").And.EndWith("Town 1 is still open, and Settings offers to start a second town again."));
            Assert.That(card.Title, Does.Not.Contain("new town"), "nothing new has started");
        }

        [Test]
        public void SwitchCard()
        {
            ViewContext ctx = ViewKit2.Context(ViewKit.NewGame());
            CardVm card = Cards.SwitchTown(ctx, 1, 2);
            Assert.That(card.Title, Is.EqualTo("Switch to Town 2?"));
            Assert.That(card.Text, Is.EqualTo("Town 1 is saved and its time keeps running. Town 2 catches up on the time since you left (at most 3 days)."));
            Assert.That(card.Buttons.Select(b => b.Id), Is.EqualTo(new[] { SettingsIds.Cancel, SettingsIds.Switch }));
            Assert.That(card.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "Not now", "Switch" }));
        }
    }
}
