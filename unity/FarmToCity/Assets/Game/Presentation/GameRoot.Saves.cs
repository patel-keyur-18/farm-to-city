using System;
using System.Collections.Generic;
using System.IO;
using Game.Presentation.Map;
using Game.Services;
using Sim.Content;
using Sim.Core;
using Sim.Core.Commands;
using Sim.Core.Definitions;
using Sim.Core.Events;
using Sim.Core.Saves;
using Sim.View.Views;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Saves, notifications, Settings and the play log (spec 007, ADR 0041): the town loads at launch and saves itself (every
    /// hour, a few seconds after a command, and when the app goes to the background, where notifications are planned too).
    /// The rules are pure C# in <c>Sim.Core.Saves</c>; this part only wires them to the app's lifecycle and screens.
    /// </summary>
    public sealed partial class GameRoot
    {
        private SavesDefs _savesDefs;
        private SaveService _saves;
        private INotificationService _notes;
        private PlayLog _playLog;
        private ulong _fingerprint;
        private int _logCount = -1;
        private long _sessionStart;
        private List<ImportFile> _importFiles = new List<ImportFile>();
        private int _townFiles;

        // ---- where things live ----

        /// <summary>The town and its backups: private (iOS Library), never shown in the Files app.</summary>
        private static string SavesFolder
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Path.Combine(Path.GetDirectoryName(Application.persistentDataPath), "Library", "Saves");
#else
                return Path.Combine(Application.persistentDataPath, "Saves");
#endif
            }
        }

        /// <summary>Exports and imports: on iOS the app's Documents folder, which the Files app shows (spec 007 §3.4).</summary>
        private static string DocumentsFolder
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return Application.persistentDataPath;
#else
                return Path.Combine(Application.persistentDataPath, "Documents");
#endif
            }
        }

        private static string PlayLogPath { get { return Path.Combine(SavesFolder, "playlog.jsonl"); } }

        // ---- the settings file (spec 007 §4): PlayerPrefs, iOS's own preferences file, never in the town ----

        private static bool NotificationsOn { get { return PlayerPrefs.GetInt("notify.on", 0) == 1; } set { PlayerPrefs.SetInt("notify.on", value ? 1 : 0); PlayerPrefs.Save(); } }
        private static int NotifyAsked { get { return PlayerPrefs.GetInt("notify.asked", 0); } set { PlayerPrefs.SetInt("notify.asked", value); PlayerPrefs.Save(); } }
        private static bool PlayLogOn { get { return PlayerPrefs.GetInt("playlog.on", 0) == 1; } set { PlayerPrefs.SetInt("playlog.on", value ? 1 : 0); PlayerPrefs.Save(); } }

        private static int UtcOffsetMinutes() { return (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes); }

        private SettingsInfo Info()
        {
            return new SettingsInfo
            {
                NowWallUtc = WallNow(), UtcOffsetMinutes = UtcOffsetMinutes(), LastSavedWallUtc = _saves.LastSavedWallUtc,
                NotificationsOn = NotificationsOn, PlayLogOn = PlayLogOn, PlayLogLines = _playLog.Lines.Count, ImportFiles = _townFiles,
                Numbers = _format, TextSize = TextSize, ReducedMotion = ReducedMotion, Sound = SoundOn, Haptics = HapticsOn,
            };
        }

        // ---- launch ----

        /// <summary>Loads the town (or a backup), or starts one; returns the card to show first, if any.</summary>
        private CardVm LoadTown()
        {
            _savesDefs = ContentLoader.LoadSaves(ContentSource.Read("saves.json"));
            _fingerprint = ContentSource.Fingerprint();
            _saves = new SaveService(SavesFolder, DocumentsFolder, _savesDefs.Save);
            _notes = NotificationServices.ForPlatform(this);
            _playLog = new PlayLog(_savesDefs.PlayLog, File.Exists(PlayLogPath) ? File.ReadAllText(PlayLogPath) : "") { Enabled = PlayLogOn };

            _lastSyncWall = WallNow();
            LoadOutcome o = _saves.Load(_config, _lastSyncWall, UtcOffsetMinutes());
            Sim = o.State != null ? Simulation.FromState(_config, o.State) : NewTownSim();
            _ctx = new ViewContext(Sim, _strings, _fmt, _ui);
            _guide = new Guide(); // what happened since launch belongs to the old town
            _logCount = Sim.State.CommandLog.Entries.Count;
            StartSession(_lastSyncWall);
            if (o.State == null) SaveNow(); // the first save of a new town
            if (o.MigratedMap) { PlayerPrefs.DeleteKey("camera.x"); PlayerPrefs.DeleteKey("camera.y"); PlayerPrefs.DeleteKey("camera.h"); } // spec 010 §3.4
            if (o.RoadTake != null) return Cards.LandTaken(_ctx, o.RoadTake);
            if (o.Restored) return Cards.Restored(_ctx, o.SavedAtWallUtc, Info());
            if (o.Error != SaveError.None) return Cards.Unreadable(_ctx, o.Error, o.KeptAs);
            return null; // the first Sync is the away advance, with its card
        }

        private Simulation NewTownSim()
        {
            Simulation sim = Simulation.NewGame(_config, new NewGame((ulong)DateTime.UtcNow.Ticks, _lastSyncWall, (long)DateTime.Now.TimeOfDay.TotalSeconds));
            _playLog.NewGame(_lastSyncWall, sim.State.Clock.SimTime);
            return sim;
        }

        // ---- saving ----

        private void SaveNow()
        {
            int bytes = _saves.Save(Sim.State, _config, WallNow(), _fingerprint);
            Debug.Log("Saved " + bytes + " bytes to " + SavesFolder);
        }

        /// <summary>After each batch of events: autosave, the play log, the notification ask, a regular's card.</summary>
        private void OnTownEvents(List<SimEvent> events)
        {
            long now = WallNow();
            int count = Sim.State.CommandLog.Entries.Count;
            if (count != _logCount) _saves.OnCommand(now); // a live Sync never adds an entry (spec 007 §3.2): this is a command
            _logCount = count;

            int lines = _playLog.Lines.Count;
            _playLog.OnEvents(events, now);
            foreach (SimEvent e in events)
            {
                if (e is AwayAdvanceCompleted a) _playLog.Away(now, e.SimTime, a.RequestedSeconds, a.AppliedSeconds, a.Capped);
                if (e is RegularAsksToMoveIn r)
                {
                    CardVm card = Cards.Regular(_ctx, r.ApplicantId);
                    if (card != null) ShowCard(card, id => { if (id == SettingsIds.SeePeople) OpenSheet(new SheetRequest("People", 0)); });
                }
                if (e is FamilyRequest f)
                {
                    // Spec 014 §3.13: a free house is offered to a hand living alone first; the card asks (null once it has lapsed).
                    CardVm card = Cards.FamilyRequest(_ctx, f.HandId);
                    if (card != null) ShowCard(card, id => Run(new Command[] { new AnswerFamilyRequest(Sim.State.Clock.SimTime, f.HandId, id == SettingsIds.Yes) }));
                }
            }
            if (_playLog.Lines.Count != lines) WritePlayLog();

            NotifyAskMoment? ask = NotifyAsk.Moment(events, NotifyAsked, NotificationsOn, Sim.State.Clock.LastWallUtc, Sim.State.Clock.SimTime, Guide.FirstSessionActive(Sim));
            if (ask.HasValue)
            {
                NotifyAsked = NotifyAsked + 1;
                ShowCard(Cards.NotifyAsk(_ctx, ask.Value, Info()), id => { if (id == SettingsIds.Yes) EnableNotifications(); });
            }
        }

        private void AutosaveIfDue(long now)
        {
            if (_saves != null && _saves.Due(now)) SaveNow();
        }

        // ---- the app's lifecycle ----

        private void OnApplicationPause(bool paused)
        {
            if (Sim == null || _saves == null) return;
            if (paused) GoingAway();
            else Returning();
        }

        private void OnApplicationQuit()
        {
            if (Sim != null && _saves != null) GoingAway();
        }

        /// <summary>Into the background: save at once, plan notifications from a simulated absence, end the session.</summary>
        private void GoingAway()
        {
            long now = WallNow();
            SaveNow();
            if (NotificationsOn)
            {
                var notes = new List<ScheduledNote>();
                foreach (PlannedNotification n in NotificationPlanner.Plan(Sim, _savesDefs.Notify, true, UtcOffsetMinutes()))
                {
                    (string title, string body) = NotificationText.For(_ctx, n);
                    notes.Add(new ScheduledNote(n.WallUtc, title, body, n.Kind.ToString()));
                }
                _notes.Schedule(now, notes);
            }
            _playLog.SessionEnd(now, Sim.State.Clock.SimTime, now - _sessionStart);
            WritePlayLog();
            Vector2 c = new Vector2(_input.View.CentreX, _input.View.CentreY);
            PlayerPrefs.SetFloat("camera.x", c.x);
            PlayerPrefs.SetFloat("camera.y", c.y);
            PlayerPrefs.SetFloat("camera.h", _input.View.HalfHeight);
            PlayerPrefs.Save();
        }

        private void Returning() { StartSession(WallNow()); }

        /// <summary>A session starts: the play log's line once we know whether a notification tap opened the app, then clear them.</summary>
        private void StartSession(long wall)
        {
            _sessionStart = wall;
            long simTime = Sim.State.Clock.SimTime;
            _notes.OpenedByNote(fromNote =>
            {
                _playLog.SessionStart(wall, simTime, BuildName, SystemInfo.deviceModel, UtcOffsetMinutes(), fromNote);
                WritePlayLog();
            });
            _notes.CancelAll();
        }

        private static string BuildName { get { return Application.version + " / sim " + SimInfo.Version; } }

        private void RestoreCamera()
        {
            if (!PlayerPrefs.HasKey("camera.h")) return;
            _input.SetView(PlayerPrefs.GetFloat("camera.x"), PlayerPrefs.GetFloat("camera.y"), PlayerPrefs.GetFloat("camera.h"));
        }

        private void WritePlayLog()
        {
            if (!PlayLogOn && _playLog.Lines.Count == 0) return;
            Directory.CreateDirectory(SavesFolder);
            File.WriteAllText(PlayLogPath, _playLog.Text);
        }

        // ---- Settings ----

        private void ScanImports()
        {
            _importFiles = new List<ImportFile>();
            foreach (string name in _saves.TownFiles())
            {
                byte[] bytes = _saves.ReadDocument(name);
                SaveHeader header = SaveCodec.ReadHeader(bytes);
                SaveError problem = header == null ? SaveError.CorruptSave : SaveCodec.Read(bytes, _config, _savesDefs.Save.MinSchema).Error;
                _importFiles.Add(new ImportFile { Name = name, Header = header, Problem = problem });
            }
            _townFiles = _importFiles.Count;
        }

        /// <summary>The Settings sheet's own actions (ids, not sim commands). True when handled.</summary>
        private bool SettingsAction(ActionVm action)
        {
            string id = action.Id;
            if (id == null) return false;
            if (id.StartsWith(SettingsIds.ImportPrefix, StringComparison.Ordinal))
            {
                string name = id.Substring(SettingsIds.ImportPrefix.Length);
                ShowCard(Cards.Replace(_ctx, name), b => Replace(b, () => ImportTown(name), () => SettingsAction(action)));
                return true;
            }
            switch (id)
            {
                case SettingsIds.SaveNow:
                    SaveNow();
                    Toast(_strings.Get("toast.saved"));
                    break;
                case SettingsIds.Notifications:
                    if (NotificationsOn) { NotificationsOn = false; _notes.CancelAll(); }
                    else EnableNotifications();
                    break;
                case SettingsIds.PlayLog:
                    PlayLogOn = !PlayLogOn;
                    _playLog.Enabled = PlayLogOn;
                    if (PlayLogOn)
                    {
                        _playLog.SessionStart(WallNow(), Sim.State.Clock.SimTime, BuildName, SystemInfo.deviceModel, UtcOffsetMinutes(), false);
                        _playLog.Guidance(WallNow(), Sim.State.Clock.SimTime, Sim.State.Guidance, Sim.Config.Game.Onboarding);
                    }
                    WritePlayLog();
                    break;
                case SettingsIds.Export:
                    Export();
                    break;
                case SettingsIds.ExportLog:
                    Toast(_strings.Get("toast.exported", "file", _saves.ExportText("FarmToCity-playlog-", ".jsonl", _playLog.Text, WallNow(), UtcOffsetMinutes())));
                    break;
                case SettingsIds.NewGame:
                    ShowCard(Cards.Replace(_ctx), b => Replace(b, NewTown, () => SettingsAction(action)));
                    break;
                default:
                    if (!LookAction(id)) return false;
                    break;
            }
            if (_open.HasValue && _sheet.IsOpen) ShowCurrentSheet(refreshOnly: true);
            return true;
        }

        /// <summary>The replace card's buttons: export first (then ask again), replace, or cancel.</summary>
        private void Replace(string button, Action replace, Action askAgain)
        {
            if (button == SettingsIds.ExportFirst) { Export(); askAgain(); }
            else if (button == SettingsIds.Replace) replace();
        }

        private void Export()
        {
            SaveNow();
            Toast(_strings.Get("toast.exported", "file", _saves.Export(WallNow(), UtcOffsetMinutes())));
        }

        private void EnableNotifications()
        {
            _notes.RequestPermission(granted =>
            {
                NotificationsOn = granted;
                if (!granted) Toast(_strings.Get("toast.notifyDenied"));
                if (_open.HasValue && _sheet.IsOpen) ShowCurrentSheet(refreshOnly: true);
            });
        }

        /// <summary>A new town replaces this one for good, its backups too (ADR 0023).</summary>
        private void NewTown()
        {
            _saves.DeleteTown();
            _lastSyncWall = WallNow();
            Sim = NewTownSim();
            ResetTown(focus: true);
        }

        private void ImportTown(string name)
        {
            LoadResult r = SaveCodec.Read(_saves.ReadDocument(name), _config, _savesDefs.Save.MinSchema);
            if (!r.Ok) { Toast(_strings.Get("import.problem." + r.Error)); return; }
            _saves.DeleteTown();
            Sim = Simulation.FromState(_config, r.State); // the next Sync is the away time since it was exported
            _playLog.NewGame(WallNow(), Sim.State.Clock.SimTime);
            ResetTown(focus: true);
        }

        private void ResetTown(bool focus)
        {
            _ctx = new ViewContext(Sim, _strings, _fmt, _ui);
            _guide = new Guide();
            _logCount = Sim.State.CommandLog.Entries.Count;
            _sheet.Close();
            _open = null;
            _selected = Hit.None;
            if (focus) _input.Focus(_mapVm.HomeX, _mapVm.HomeY);
            SaveNow();
            StartSession(WallNow()); // the new town's first session starts here in the play log (ADR 0047 §2)
            Refresh(false);
        }

        /// <summary>Shows a card on the next UI tick: built inside a click handler it wouldn't get touch clicks (STATUS, Editor quirks).</summary>
        private void ShowCard(CardVm card, Action<string> onButton)
        {
            _safeRoot.schedule.Execute(() => _sheet.Card(card, onButton));
        }
    }
}
