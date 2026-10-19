using System;
using System.Collections.Generic;
using System.Globalization;
using Sim.Core.Events;
using Sim.Core.Saves;

namespace Sim.View.Views
{
    /// <summary>What the platform knows that the Settings sheet shows (spec 007 §7): none of it is in the town.</summary>
    public sealed class SettingsInfo
    {
        public long NowWallUtc { get; set; }

        /// <summary>The device's offset from UTC, for local clock times.</summary>
        public int UtcOffsetMinutes { get; set; }

        /// <summary>0 = not saved yet.</summary>
        public long LastSavedWallUtc { get; set; }
        public bool NotificationsOn { get; set; }
        public bool PlayLogOn { get; set; }
        public int PlayLogLines { get; set; }
        public int ImportFiles { get; set; }
    }

    /// <summary>A town file in the Documents folder (spec 007 §3.4, ADR 0041): its header, or why it can't load.</summary>
    public sealed class ImportFile
    {
        public string Name { get; set; }
        public SaveHeader Header { get; set; }
        public SaveError Problem { get; set; }
    }

    public sealed class CardButton
    {
        public string Id { get; }
        public string Label { get; }
        public ActionKind Kind { get; }

        public CardButton(string id, string label, ActionKind kind)
        {
            Id = id;
            Label = label;
            Kind = kind;
        }
    }

    /// <summary>A small card over the map: a title, a sentence and one to three buttons.</summary>
    public sealed class CardVm
    {
        public string Title { get; internal set; }
        public string Text { get; internal set; }
        public List<CardButton> Buttons { get; } = new List<CardButton>();
    }

    /// <summary>The ids of the Settings actions: the platform runs them, they are not sim commands.</summary>
    public static class SettingsIds
    {
        public const string SaveNow = "saveNow";
        public const string Notifications = "notifications";
        public const string PlayLog = "playLog";
        public const string Export = "export";
        public const string ExportLog = "exportLog";
        public const string NewGame = "newGame";

        /// <summary>An import row's id: this prefix and the file name.</summary>
        public const string ImportPrefix = "import:";

        // Card buttons.
        public const string ExportFirst = "exportFirst";
        public const string Replace = "replace";
        public const string Cancel = "cancel";
        public const string Ok = "ok";
        public const string Yes = "yes";
        public const string NotNow = "notNow";
        public const string SeePeople = "seePeople";
    }

    /// <summary>The Settings sheet (spec 007 §7, ADR 0041 §4): one row per choice with its own button.</summary>
    public static class SettingsSheet
    {
        public static SheetVm Build(ViewContext ctx, SettingsInfo info)
        {
            var s = ctx.Strings;
            var vm = new SheetVm { Title = s.Get("settings.title"), Icon = "gear" };
            string saved = info.LastSavedWallUtc == 0 ? s.Get("settings.notSaved") : s.Get("settings.lastSaved", "time", WallTime.When(ctx, info.LastSavedWallUtc, info));
            vm.Lines.Add(Row(s.Get("settings.saved"), saved, new ActionVm(SettingsIds.SaveNow, s.Get("settings.saveNow"), null, ActionKind.Primary, true, null, null)));
            vm.Lines.Add(Row(s.Get("settings.notifications"), s.Get(info.NotificationsOn ? "settings.notifyOn" : "settings.notifyOff"),
                new ActionVm(SettingsIds.Notifications, s.Get(info.NotificationsOn ? "settings.turnOff" : "settings.turnOn"), null, ActionKind.Secondary, true, null, null)));
            vm.Lines.Add(Row(s.Get("settings.playLog"), s.Get(info.PlayLogOn ? "settings.playLogOn" : "settings.playLogOff"),
                new ActionVm(SettingsIds.PlayLog, s.Get(info.PlayLogOn ? "settings.turnOff" : "settings.turnOn"), null, ActionKind.Secondary, true, null, null)));
            vm.Lines.Add(Row(s.Get("settings.exportTown"), s.Get("settings.toFiles"),
                new ActionVm(SettingsIds.Export, s.Get("settings.export"), null, ActionKind.Secondary, true, null, null)));
            vm.Lines.Add(Row(s.Get("settings.importTown"), s.Plural("settings.importFiles", info.ImportFiles, "count", ctx.Fmt.Integer(info.ImportFiles)),
                ActionVm.Open("openImport", s.Get("settings.choose"), new SheetRequest("Import", 0))));
            bool hasLog = info.PlayLogLines > 0;
            vm.Lines.Add(Row(s.Get("settings.exportLog"), s.Plural("settings.logLines", info.PlayLogLines, "count", ctx.Fmt.Integer(info.PlayLogLines)),
                new ActionVm(SettingsIds.ExportLog, s.Get("settings.export"), null, ActionKind.Secondary, hasLog, hasLog ? null : s.Get("settings.logEmpty"), null)));
            vm.Lines.Add(Row(s.Get("settings.newGame"), s.Get("settings.newGameNote"),
                new ActionVm(SettingsIds.NewGame, s.Get("settings.newGameButton"), null, ActionKind.Danger, true, null, null)));
            return vm;
        }

        internal static LineVm Row(string label, string value, ActionVm action) { return new LineVm(label, value) { Action = action }; }
    }

    /// <summary>Import a town (ADR 0041 §5): every town file in the Documents folder, newest first, one row each.</summary>
    public static class ImportSheet
    {
        public static SheetVm Build(ViewContext ctx, IReadOnlyList<ImportFile> files, SettingsInfo info)
        {
            var s = ctx.Strings;
            var vm = new SheetVm { Title = s.Get("import.title"), Subtitle = s.Get("import.subtitle"), Icon = "gear" };
            var sorted = new List<ImportFile>(files);
            sorted.Sort((a, b) =>
            {
                long ta = a.Header == null ? 0 : a.Header.SavedAtWallUtc, tb = b.Header == null ? 0 : b.Header.SavedAtWallUtc;
                return ta != tb ? tb.CompareTo(ta) : string.CompareOrdinal(a.Name, b.Name);
            });
            if (sorted.Count == 0) vm.Lines.Add(new LineVm(s.Get("import.none"), null));
            foreach (ImportFile f in sorted)
            {
                bool ok = f.Problem == SaveError.None && f.Header != null;
                string value = ok
                    ? s.Get("import.summary", "date", ctx.Fmt.Date(Sim.Core.Time.Calendar.From(f.Header.SimTime, ctx.Sim.Clock), ctx.Sim.Clock.DaysPerSeason),
                        "cash", ctx.Fmt.Coins(f.Header.Cash), "saved", WallTime.When(ctx, f.Header.SavedAtWallUtc, info))
                    : s.Get("import.problem." + f.Problem);
                vm.Lines.Add(SettingsSheet.Row(f.Name, value, new ActionVm(SettingsIds.ImportPrefix + f.Name, s.Get("import.button"), null, ActionKind.Primary, ok,
                    ok ? null : s.Get("import.problem." + f.Problem), null)));
            }
            return vm;
        }
    }

    /// <summary>The cards of spec 007 §7 and ADR 0041.</summary>
    public static class Cards
    {
        /// <summary>A new game or an import replaces the town for good (ADR 0023): export it first, replace, or cancel.</summary>
        public static CardVm Replace(ViewContext ctx, string importName = null)
        {
            var s = ctx.Strings;
            var c = new CardVm
            {
                Title = s.Get(importName == null ? "card.newGameTitle" : "card.importTitle", "file", importName ?? ""),
                Text = s.Get("card.replaceText"),
            };
            c.Buttons.Add(new CardButton(SettingsIds.ExportFirst, s.Get("card.exportFirst"), ActionKind.Secondary));
            c.Buttons.Add(new CardButton(SettingsIds.Replace, s.Get("card.replace"), ActionKind.Danger));
            c.Buttons.Add(new CardButton(SettingsIds.Cancel, s.Get("action.cancel"), ActionKind.Secondary));
            return c;
        }

        /// <summary>After a crash the town came back from a backup (spec 007 §3.3).</summary>
        public static CardVm Restored(ViewContext ctx, long savedAtWallUtc, SettingsInfo info)
        {
            var c = new CardVm { Title = ctx.Strings.Get("card.restoredTitle"), Text = ctx.Strings.Get("card.restoredText", "time", WallTime.When(ctx, savedAtWallUtc, info)) };
            c.Buttons.Add(new CardButton(SettingsIds.Ok, ctx.Strings.Get("card.ok"), ActionKind.Primary));
            return c;
        }

        /// <summary>The town couldn't be read by this build: kept on the phone, a new town started (ADR 0041 §2).</summary>
        public static CardVm Unreadable(ViewContext ctx, SaveError error, string keptAs)
        {
            var c = new CardVm { Title = ctx.Strings.Get("card.unreadableTitle"), Text = ctx.Strings.Get("card.unreadable." + error, "file", keptAs ?? "") };
            c.Buttons.Add(new CardButton(SettingsIds.Ok, ctx.Strings.Get("card.ok"), ActionKind.Primary));
            return c;
        }

        /// <summary>The notification ask (ADR 0036, ADR 0041 §6): with the crop's ready time, or the season-close wording.</summary>
        public static CardVm NotifyAsk(ViewContext ctx, NotifyAskMoment moment, SettingsInfo info)
        {
            var s = ctx.Strings;
            string text = moment.CropId == null
                ? s.Get("card.notifyAgain")
                : s.Get("card.notifyAsk", "crop", Names.Of(ctx, moment.CropId), "time", WallTime.When(ctx, moment.ReadyWallUtc, info));
            var c = new CardVm { Title = s.Get("card.notifyTitle"), Text = text };
            c.Buttons.Add(new CardButton(SettingsIds.Yes, s.Get("card.yesNudge"), ActionKind.Primary));
            c.Buttons.Add(new CardButton(SettingsIds.NotNow, s.Get("card.notNow"), ActionKind.Secondary));
            return c;
        }

        /// <summary>A regular asks to move in (spec 006 §3.3): a letter-style card that opens the People sheet.</summary>
        public static CardVm Regular(ViewContext ctx, long applicantId)
        {
            Sim.Core.People.Applicant a = ctx.Sim.State.People?.Applicant(applicantId);
            if (a == null) return null;
            var s = ctx.Strings;
            var c = new CardVm
            {
                Title = s.Get("card.regularTitle", "name", PeopleWords.Name(ctx, a.Name)),
                Text = s.Get("card.regularText", "name", PeopleWords.Name(ctx, a.Name), "days", ctx.Fmt.Integer(ctx.Sim.Config.Game.People.Pool.RegularThresholdDays)),
            };
            c.Buttons.Add(new CardButton(SettingsIds.SeePeople, s.Get("card.seePeople"), ActionKind.Primary));
            c.Buttons.Add(new CardButton(SettingsIds.NotNow, s.Get("card.later"), ActionKind.Secondary));
            return c;
        }
    }

    /// <summary>When to ask about notifications: the first long field crop sown, or the first season close after a "not now".</summary>
    public readonly struct NotifyAskMoment
    {
        /// <summary>The crop that will be ready, or null for the second ask (after the first season close).</summary>
        public string CropId { get; }
        public long ReadyWallUtc { get; }

        public NotifyAskMoment(string cropId, long readyWallUtc)
        {
            CropId = cropId;
            ReadyWallUtc = readyWallUtc;
        }
    }

    public static class NotifyAsk
    {
        /// <summary>
        /// The moment to ask in these events, or null. <paramref name="asked"/> counts asks so far (at most 2); the second only
        /// after a "not now" (ADR 0036, spec 007 §3.5). A field crop of at least a day is "the long crop" (ADR 0041 §6).
        /// </summary>
        public static NotifyAskMoment? Moment(IEnumerable<SimEvent> events, int asked, bool on, long lastWallUtc, long simTime)
        {
            if (on || asked >= 2) return null;
            foreach (SimEvent e in events)
            {
                if (asked == 0 && e is CropSown c && c.ReadyAt - c.SimTime >= 86400)
                    return new NotifyAskMoment(c.CropId, lastWallUtc + (c.ReadyAt - simTime));
                if (asked == 1 && e is SeasonClosed) return new NotifyAskMoment(null, 0);
            }
            return null;
        }
    }

    /// <summary>A notification's title and line from the string table (spec 007 §3.5): kind, count, crop; never money.</summary>
    public static class NotificationText
    {
        public static (string Title, string Body) For(ViewContext ctx, PlannedNotification n)
        {
            var s = ctx.Strings;
            string k = "notify." + n.Kind;
            bool counted = n.Kind == NotifyKind.CropReady || n.Kind == NotifyKind.BedReady || n.Kind == NotifyKind.FruitRipe || n.Kind == NotifyKind.HandsWork;
            string count = ctx.Fmt.Integer(n.Count);
            string title = !counted ? s.Get(k + ".title")
                : n.CropId != null ? s.Plural(k + ".crop", n.Count, "crop", Names.Of(ctx, n.CropId), "count", count)
                : s.Get(k + ".mixed", "count", count);
            return (title, s.Get(k + ".body"));
        }
    }

    /// <summary>Display names of content ids that can be a crop, a garden crop, a tree or a good.</summary>
    internal static class Names
    {
        public static string Of(ViewContext ctx, string id)
        {
            var farm = ctx.Sim.Config.Game.Farm;
            int i = farm.IndexOfCrop(id);
            if (i >= 0) return ctx.Strings.Get(farm.Crops[i].NameKey);
            i = farm.IndexOfGardenCrop(id);
            if (i >= 0) return ctx.Strings.Get(farm.GardenCrops[i].NameKey);
            i = farm.IndexOfTree(id);
            if (i >= 0) return ctx.Strings.Get(farm.Trees[i].NameKey);
            i = ctx.Sim.Config.Game.Market.IndexOf(id);
            return i >= 0 ? ctx.Strings.Get(ctx.Sim.Config.Game.Market.Goods[i].NameKey) : id;
        }
    }

    /// <summary>A wall-clock moment in the device's local time: "14:02 today", "14:05 tomorrow", "19 Oct 14:02".</summary>
    internal static class WallTime
    {
        public static string When(ViewContext ctx, long wallUtc, SettingsInfo info)
        {
            DateTime at = DateTimeOffset.FromUnixTimeSeconds(wallUtc + info.UtcOffsetMinutes * 60L).UtcDateTime;
            DateTime now = DateTimeOffset.FromUnixTimeSeconds(info.NowWallUtc + info.UtcOffsetMinutes * 60L).UtcDateTime;
            string clock = at.ToString("HH:mm", CultureInfo.InvariantCulture);
            int days = (at.Date - now.Date).Days;
            if (days == 0) return ctx.Strings.Get("time.today", "time", clock);
            if (days == 1) return ctx.Strings.Get("time.tomorrow", "time", clock);
            if (days == -1) return ctx.Strings.Get("time.yesterday", "time", clock);
            return ctx.Strings.Get("time.onDate", "date", at.ToString("d MMM", CultureInfo.InvariantCulture), "time", clock);
        }
    }
}
