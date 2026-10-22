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

        // Look and sound (spec 008 §3.13, ADR 0043 §9): kept in the settings file, never in the town.
        public Sim.View.Text.NumberFormat Numbers { get; set; }

        /// <summary>100, 125 or 150.</summary>
        public int TextSize { get; set; } = 100;
        public bool ReducedMotion { get; set; }
        public bool Sound { get; set; } = true;
        public bool Haptics { get; set; } = true;
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
        public const string NumbersIndian = "numbers:indian";
        public const string NumbersWestern = "numbers:western";

        /// <summary>A text-size row's id: this prefix and the size in percent.</summary>
        public const string TextPrefix = "text:";
        public const string ReducedMotion = "reducedMotion";
        public const string Sound = "sound";
        public const string Haptics = "haptics";

        public static readonly int[] TextSizes = { 80, 100, 125 };

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
            vm.Lines.Add(Row(s.Get("settings.saved"), saved, new ActionVm(SettingsIds.SaveNow, s.Get("settings.saveNow"), null, ActionKind.Primary, true, null, null), info.LastSavedWallUtc == 0 ? null : "saved"));
            // A switch shows its state (owner, device run): a green On or a red Off; tapping it flips it.
            vm.Lines.Add(Row(s.Get("settings.notifications"), s.Get(info.NotificationsOn ? "settings.notifyOn" : "settings.notifyOff"), Switch(ctx, SettingsIds.Notifications, info.NotificationsOn)));
            vm.Lines.Add(Row(s.Get("settings.playLog"), s.Get(info.PlayLogOn ? "settings.playLogOn" : "settings.playLogOff"), Switch(ctx, SettingsIds.PlayLog, info.PlayLogOn)));

            // Look and sound: one row per choice (ADR 0043 §9).
            vm.Lines.Add(LineVm.Header(s.Get("settings.numbers")));
            vm.Lines.Add(Row(s.Get("settings.indian"), s.Get("settings.indianNote"), Choice(ctx, SettingsIds.NumbersIndian, info.Numbers == Sim.View.Text.NumberFormat.Indian)));
            vm.Lines.Add(Row(s.Get("settings.western"), s.Get("settings.westernNote"), Choice(ctx, SettingsIds.NumbersWestern, info.Numbers == Sim.View.Text.NumberFormat.Western)));
            vm.Lines.Add(LineVm.Header(s.Get("settings.textSize")));
            foreach (int size in SettingsIds.TextSizes)
                vm.Lines.Add(Row(s.Get("settings.text." + size), size.ToString(CultureInfo.InvariantCulture) + "%", Choice(ctx, SettingsIds.TextPrefix + size, info.TextSize == size), "textSize"));
            vm.Lines.Add(LineVm.Header(s.Get("settings.motionSound")));
            vm.Lines.Add(Row(s.Get("settings.reducedMotion"), s.Get(info.ReducedMotion ? "settings.reducedMotionOn" : "settings.reducedMotionOff"), Switch(ctx, SettingsIds.ReducedMotion, info.ReducedMotion)));
            vm.Lines.Add(Row(s.Get("settings.sound"), s.Get(info.Sound ? "settings.soundOn" : "settings.soundOff"), Switch(ctx, SettingsIds.Sound, info.Sound)));
            vm.Lines.Add(Row(s.Get("settings.haptics"), s.Get(info.Haptics ? "settings.hapticsOn" : "settings.hapticsOff"), Switch(ctx, SettingsIds.Haptics, info.Haptics)));
            vm.Lines.Add(LineVm.Header(s.Get("settings.files")));
            vm.Lines.Add(Row(s.Get("settings.exportTown"), s.Get("settings.toFiles"),
                new ActionVm(SettingsIds.Export, s.Get("settings.export"), null, ActionKind.Primary, true, null, null)));
            vm.Lines.Add(Row(s.Get("settings.importTown"), s.Plural("settings.importFiles", info.ImportFiles, "count", ctx.Fmt.Integer(info.ImportFiles)),
                ActionVm.Open("openImport", s.Get("settings.choose"), new SheetRequest("Import", 0)), "importFiles"));
            bool hasLog = info.PlayLogLines > 0;
            vm.Lines.Add(Row(s.Get("settings.exportLog"), s.Plural("settings.logLines", info.PlayLogLines, "count", ctx.Fmt.Integer(info.PlayLogLines)),
                new ActionVm(SettingsIds.ExportLog, s.Get("settings.export"), null, ActionKind.Primary, hasLog, hasLog ? null : s.Get("settings.logEmpty"), null), "playLog"));
            vm.Lines.Add(Row(s.Get("settings.newGame"), s.Get("settings.newGameNote"),
                new ActionVm(SettingsIds.NewGame, s.Get("settings.newGameButton"), null, ActionKind.Danger, true, null, null)));
            return vm;
        }

        internal static LineVm Row(string label, string value, ActionVm action, string why = null) { return new LineVm(label, value, why) { Action = action }; }

        private static ActionVm Choice(ViewContext ctx, string id, bool chosen)
        {
            return chosen ? FounderSheet.Chosen(ctx, id) : new ActionVm(id, ctx.Strings.Get("action.choose"), null, ActionKind.Primary, true, null, null);
        }

        private static ActionVm Switch(ViewContext ctx, string id, bool on)
        {
            return new ActionVm(id, ctx.Strings.Get(on ? "settings.on" : "settings.off"), null, on ? ActionKind.Primary : ActionKind.Danger, true, null, null);
        }
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
        /// <summary>Collect all into a full store (spec 008 §3.9): what didn't fit, offered to the trader for exactly what it fetches.</summary>
        public static CardVm SellRest(ViewContext ctx, CollectRestVm rest)
        {
            var s = ctx.Strings;
            var c = new CardVm { Title = s.Get("card.sellRestTitle"), Text = s.Get("card.sellRestText", "qty", ctx.Fmt.Kg(rest.Grams), "amount", ctx.Fmt.Coins(rest.Coins)) };
            c.Buttons.Add(new CardButton(SettingsIds.Yes, s.Get("card.sellRest"), ActionKind.Primary));
            c.Buttons.Add(new CardButton(SettingsIds.Cancel, s.Get("card.leaveIt"), ActionKind.Secondary));
            return c;
        }

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

        /// <summary>The farm lane took the old save's acre south of the huts (spec 010 §3.4): what the village paid, once.</summary>
        public static CardVm LandTaken(ViewContext ctx, Sim.Core.Saves.RoadTake take)
        {
            var c = new CardVm { Title = ctx.Strings.Get("card.landTakenTitle"),
                Text = ctx.Strings.Get("card.landTakenText", "amount", ctx.Fmt.Coins(take.Compensation + take.OnIt - take.LoanRepaid)) };
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
                : s.Get("card.notifyAsk", "crop", Names.Of(ctx, moment.CropId), "time", WallTime.Ready(ctx, moment.ReadyWallUtc, info));
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
        /// after a "not now" (ADR 0036, spec 007 §3.5). A field crop of at least 12 hours is "the long crop" (ADR 0041 §6, the
        /// first session's longCrop step; ADR 0058: no crop takes a day since rice and potato take 15 h). While
        /// <paramref name="letterWillAsk"/> (the first session runs, so its <c>comeBack</c> page asks first), the card stays quiet:
        /// it is the fallback after a skip (ADR 0045 §2).
        /// </summary>
        public static NotifyAskMoment? Moment(IEnumerable<SimEvent> events, int asked, bool on, long lastWallUtc, long simTime, bool letterWillAsk = false)
        {
            if (on || asked >= 2) return null;
            foreach (SimEvent e in events)
            {
                if (asked == 0 && !letterWillAsk && e is CropSown c && c.ReadyAt - c.SimTime >= 12 * 3600)
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

        /// <summary>When something will be ready, in a sentence: "tonight at 23:40", "today at 14:00", "tomorrow at 05:10" (spec 011 §3.6b).</summary>
        public static string Ready(ViewContext ctx, long wallUtc, SettingsInfo info)
        {
            DateTime at = DateTimeOffset.FromUnixTimeSeconds(wallUtc + info.UtcOffsetMinutes * 60L).UtcDateTime;
            DateTime now = DateTimeOffset.FromUnixTimeSeconds(info.NowWallUtc + info.UtcOffsetMinutes * 60L).UtcDateTime;
            string clock = at.ToString("HH:mm", CultureInfo.InvariantCulture);
            int days = (at.Date - now.Date).Days;
            if (days == 0) return ctx.Strings.Get(at.Hour >= 18 ? "time.tonightAt" : "time.todayAt", "time", clock);
            if (days == 1) return ctx.Strings.Get("time.tomorrowAt", "time", clock);
            return ctx.Strings.Get("time.onDate", "date", at.ToString("d MMM", CultureInfo.InvariantCulture), "time", clock);
        }
    }
}
