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
            Assert.That(vm.Lines.Select(l => l.Label + " | " + l.Value + " | " + l.Action.Label), Is.EqualTo(new[]
            {
                "Saved automatically | Last at 13:20 today | Save now",
                "Notifications | Off | Turn on",
                "Play log | Off · helps the developer when on | Turn on",
                "Export town | A copy to the Files app | Export",
                "Import a town | 2 town files in Files | Choose",
                "Export play log | 3 entries | Export",
                "New game | Replaces your town for good | New game…",
            }));
            Assert.That(vm.Lines.Last().Action.Kind, Is.EqualTo(ActionKind.Danger));
            Assert.That(vm.Lines[4].Action.Opens.Value.Kind, Is.EqualTo("Import"));

            SheetVm on = SettingsSheet.Build(ctx, new SettingsInfo { NowWallUtc = Wall0, NotificationsOn = true, PlayLogOn = true });
            Assert.That(on.Lines[0].Value, Is.EqualTo("Not saved yet"));
            Assert.That(on.Lines[1].Value + " / " + on.Lines[1].Action.Label, Is.EqualTo("On · never at night / Turn off"));
            Assert.That(on.Lines[5].Action.Enabled, Is.False, "an empty play log can't be exported");
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
            CardVm replace = Cards.Replace(ctx);
            Assert.That(replace.Buttons.Select(b => b.Id), Is.EqualTo(new[] { SettingsIds.ExportFirst, SettingsIds.Replace, SettingsIds.Cancel }));
            Assert.That(replace.Buttons.Select(b => b.Label), Is.EqualTo(new[] { "Export first", "Replace", "Cancel" }));
            Assert.That(Cards.Replace(ctx, "old.ftc").Title, Is.EqualTo("Import old.ftc?"));
            Assert.That(Cards.Restored(ctx, Wall0 - 7200, Info()).Text, Is.EqualTo("It came back from the save of 11:30 today. A few minutes of play may be missing."));
            Assert.That(Cards.Unreadable(ctx, SaveError.SaveTooOld, "town.unreadable-20261019-0800.ftc").Text, Does.Contain("older test build").And.Contain("town.unreadable-20261019-0800.ftc"));
        }

        [Test]
        public void NotifyAsk_AfterTheLongCrop_ThenAfterTheFirstClose()
        {
            Simulation sim = ViewKit.NewGame();
            long t = sim.State.Clock.SimTime;
            sim.Execute(new Sow(t, 20, "radish", SeedSource.Certified, "none")); // two hours: not the long crop
            Assert.That(NotifyAsk.Moment(sim.DrainEvents(), 0, false, sim.State.Clock.LastWallUtc, t), Is.Null);
            sim.Execute(new Sow(t, 28, "potato", SeedSource.Certified, "none"));
            NotifyAskMoment? m = NotifyAsk.Moment(sim.PendingEvents, 0, false, sim.State.Clock.LastWallUtc, t);
            Assert.That(m.HasValue, Is.True);
            Assert.That(m.Value.CropId, Is.EqualTo("potato"));
            Assert.That(m.Value.ReadyWallUtc, Is.EqualTo(sim.State.Clock.LastWallUtc + sim.State.Farm.CropOn(28).ReadyAt - t));
            Assert.That(NotifyAsk.Moment(sim.PendingEvents, 0, true, Wall0, t), Is.Null, "never when already on");
            Assert.That(NotifyAsk.Moment(sim.PendingEvents, 1, false, Wall0, t), Is.Null, "the second ask waits for the first season close");
            Assert.That(NotifyAsk.Moment(sim.PendingEvents, 2, false, Wall0, t), Is.Null, "never a third");

            ViewContext ctx = ViewKit2.Context(sim);
            CardVm card = Cards.NotifyAsk(ctx, m.Value, new SettingsInfo { NowWallUtc = sim.State.Clock.LastWallUtc, UtcOffsetMinutes = 0 });
            Assert.That(card.Text, Does.StartWith("Potato will be ready at ").And.Contain("tomorrow"));
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
                (string title, string body) = NotificationText.For(ctx, N(k, 1, k == NotifyKind.CropReady || k == NotifyKind.HandsWork ? "wheat" : k == NotifyKind.BedReady ? "garden_radish" : k == NotifyKind.FruitRipe ? "mango" : null));
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
    }
}
