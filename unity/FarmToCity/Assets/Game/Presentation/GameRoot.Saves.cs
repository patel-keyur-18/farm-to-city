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
using Sim.Core.Time;
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
                OpenTown = _slot,
            };
        }

        // ---- launch ----

        /// <summary>Loads the town the player had open (or a backup), or starts one; returns the card to show first, if any.</summary>
        private CardVm LoadTown()
        {
            _savesDefs = ContentLoader.LoadSaves(ContentSource.Read("saves.json"));
            _fingerprint = ContentSource.Fingerprint();
            _notes = NotificationServices.ForPlatform(this);
            _slot = OpenSlotAtLaunch(); // spec 031 §3: the one last open, Town 1 by default
            _saves = SavesFor(_slot);
            _playLog = OpenLog(_saves);

            _lastSyncWall = WallNow();
            LoadOutcome o = _saves.Load(_config, _lastSyncWall, UtcOffsetMinutes());
            Sim = o.State != null ? Simulation.FromState(_config, o.State) : NewTownSim();
            return AdoptTown(o);
        }

        /// <summary>
        /// Makes the town in <see cref="Sim"/> the open one: its view context, guide and log count, its first session or away advance, its camera
        /// key, and the card its load calls for (a restore, a road take, an unreadable town). <paramref name="o"/> is null for a town just started.
        /// </summary>
        private CardVm AdoptTown(LoadOutcome o)
        {
            _ctx = new ViewContext(Sim, _strings, _fmt, _ui);
            RefreshTownName();
            _guide = new Guide(); // what happened since launch belongs to the old town
            _logCount = Sim.State.CommandLog.Entries.Count;
            StartSession(_lastSyncWall);
            _alignPending = true;
            _choosing = RunOne.MustChoose(Sim); // held until the player chooses (AskWhichToRun)
            if (o == null || o.State == null) { SaveNow(); RefreshTownName(); } // the first save of a new town
            if (o != null && o.MigratedMap) { PlayerPrefs.DeleteKey(CamKey(_slot, "x")); PlayerPrefs.DeleteKey(CamKey(_slot, "y")); PlayerPrefs.DeleteKey(CamKey(_slot, "h")); } // spec 010 §3.4
            if (o == null) return null;
            if (o.RoadTake != null) return Cards.LandTaken(_ctx, o.RoadTake);
            if (o.Restored) return Cards.Restored(_ctx, o.SavedAtWallUtc, Info());
            if (o.Error != SaveError.None) return Cards.Unreadable(_ctx, o.Error, o.KeptAs);
            return null; // the first Sync is the away advance, with its card
        }

        /// <summary>A town's play log, from its own folder (spec 031 §3.2).</summary>
        private PlayLog OpenLog(SaveService saves)
        {
            return new PlayLog(_savesDefs.PlayLog, File.Exists(saves.PlayLogPath) ? File.ReadAllText(saves.PlayLogPath) : "") { Enabled = PlayLogOn };
        }

        // ---- one business run by you (spec 030 §3.1, §7.3) ----

        /// <summary>True from loading a town that runs several businesses itself until the player has kept one: no time passes meanwhile, so the away hours count with the answer.</summary>
        private bool _choosing;

        /// <summary>A town saved before the one-business rule asks which business to keep running (a card with no way round it), then goes on.</summary>
        private void AskWhichToRun()
        {
            if (!RunOne.MustChoose(Sim)) { _choosing = false; return; }
            _choosing = true;
            ShowCard(RunOne.Card(_ctx), id =>
            {
                Command answer = RunOne.Answer(Sim, id);
                if (!(answer is KeepRunningYourself keep)) { AskWhichToRun(); return; }
                var stopped = new List<long>();
                foreach (global::Sim.Core.Business.Business b in global::Sim.Core.Business.BusinessRules.RunByYou(Sim)) if (b.Id != keep.BusinessId) stopped.Add(b.Id);
                Run(new Command[] { answer });
                if (RunOne.MustChoose(Sim)) { AskWhichToRun(); return; }
                _choosing = false;
                Toast(RunOne.Kept(_ctx, keep.BusinessId, stopped));
            });
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
            long now = WallNow();
            int bytes = _saves.Save(Sim.State, _config, now, _fingerprint, TownSummary.Of(Sim, now));
            Debug.Log("Saved " + bytes + " bytes to " + _saves.Folder);
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
                if (e is HrEventRaised hr)
                {
                    // Spec 015 §3.7: a worker needs a word (a raise, an offer, sick leave, an emergency, a bad batch); the alert keeps it if put off.
                    CardVm card = HrEventSheet.Card(_ctx, hr.EventId);
                    long id = hr.EventId;
                    if (card != null)
                        ShowCard(card, b =>
                        {
                            if (b == SettingsIds.Decide) OpenSheet(new SheetRequest("HrEvent", id)); // an accident, a quarrel or an association: three answers, a sheet; its default answers after a day (spec 028 §3.9)
                            else if (b == SettingsIds.Yes) Run(new Command[] { new AnswerHrEvent(Sim.State.Clock.SimTime, id, 0) });
                            else if (Sim.State.People?.HrEvents.Find(x => x.Id == id)?.Timed != true) Run(new Command[] { new AnswerHrEvent(Sim.State.Clock.SimTime, id, 1) });
                        });
                }
                if (e is HrOutcome oc) ShowCard(HrEventSheet.Outcome(_ctx, oc), _ => { }); // what a talk or a negotiation came to (spec 028 §3.9)
                if (e is LeasesOffered lo) ShowLeaseCards(lo.HouseholdIds, 0); // leases in their last season: a card each (US-240)
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
            EndSession(now);
        }

        /// <summary>The open town's session ends: its play log line and its camera (spec 031 §3: each town keeps its own).</summary>
        private void EndSession(long now)
        {
            _playLog.SessionEnd(now, Sim.State.Clock.SimTime, now - _sessionStart);
            WritePlayLog();
            Vector2 c = new Vector2(_input.View.CentreX, _input.View.CentreY);
            PlayerPrefs.SetFloat(CamKey(_slot, "x"), c.x);
            PlayerPrefs.SetFloat(CamKey(_slot, "y"), c.y);
            PlayerPrefs.SetFloat(CamKey(_slot, "h"), _input.View.HalfHeight);
            PlayerPrefs.Save();
        }

        private void Returning()
        {
            StartSession(WallNow());
            if (_holdUntil > 0) _alignPending = true; // the hold counts from the saved anchor, so what passed while away is counted
        }

        /// <summary>
        /// Makes the game's time of day the phone's (a town made elsewhere, such as a bot's playtest save, can start at another time of day), counted at the sim's last anchor so the time
        /// away is then synced as usual. Behind: the sim moves forward like an away advance. Ahead: time never goes backwards, so it waits. Nothing is lost either way.
        /// </summary>
        private void AlignClock()
        {
            long anchor = Sim.State.Clock.LastWallUtc;
            long local = ((anchor + UtcOffsetMinutes() * 60L) % 86400 + 86400) % 86400;
            long off = ClockAlign.Offset(Sim.State.Clock.SimTime, local);
            if (off > 0)
            {
                SaveNow(); // a point to come back to, before the clock moves
                Sim.Execute(new AdvanceTo(Sim.State.Clock.SimTime + off, AdvanceMode.Away));
                AfterCommands();
                Toast(_strings.Get("toast.clockBehind", "time", Gap(off)));
            }
            else if (off < 0)
            {
                _holdUntil = anchor - off;
                Toast(_strings.Get("toast.clockAhead", "time", Gap(-off)));
            }
        }

        private string Gap(long seconds)
        {
            long minutes = (seconds + 30) / 60;
            return minutes >= 60 ? _strings.Get("clock.span.hm", "h", (minutes / 60).ToString(), "m", (minutes % 60).ToString()) : _strings.Get("clock.span.m", "m", minutes.ToString());
        }

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
            if (!PlayerPrefs.HasKey(CamKey(_slot, "h"))) return;
            _input.SetView(PlayerPrefs.GetFloat(CamKey(_slot, "x")), PlayerPrefs.GetFloat(CamKey(_slot, "y")), PlayerPrefs.GetFloat(CamKey(_slot, "h")));
        }

        private void WritePlayLog()
        {
            if (!PlayLogOn && _playLog.Lines.Count == 0) return;
            Directory.CreateDirectory(_saves.Folder);
            File.WriteAllText(_saves.PlayLogPath, _playLog.Text);
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
                ShowCard(Cards.Replace(_ctx, InfoWithTowns(), name), b => Replace(b, () => ImportTown(name), () => SettingsAction(action)));
                return true;
            }
            if (id.StartsWith(SettingsIds.TownSwitchPrefix, StringComparison.Ordinal))
            {
                AskSwitch(int.Parse(id.Substring(SettingsIds.TownSwitchPrefix.Length)));
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
                    Toast(_strings.Get("toast.exported", "file", _saves.ExportText("FarmToCity-playlog-" + _saves.TownTag + "-", ".jsonl", _playLog.Text, WallNow(), UtcOffsetMinutes())));
                    break;
                case SettingsIds.TownStart:
                    StartSecondTownWithVeil();
                    break;
                case SettingsIds.NewGame:
                    ShowCard(Cards.Replace(_ctx, InfoWithTowns()), b => Replace(b, NewTown, () => SettingsAction(action)));
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
            _holdUntil = 0;
            _alignPending = true;
            _playLog.NewGame(WallNow(), Sim.State.Clock.SimTime);
            ResetTown(focus: true);
            _choosing = RunOne.MustChoose(Sim);
            AskWhichToRun(); // an imported older town may run several
        }

        private void ResetTown(bool focus)
        {
            _ctx = new ViewContext(Sim, _strings, _fmt, _ui);
            _guide = new Guide();
            _logCount = Sim.State.CommandLog.Entries.Count;
            _sheet.Close();
            _open = null;
            _selected = Hit.None;
            _input.ApplyRing(_mapVm);
            if (focus) _input.Focus(_mapVm.HomeX, _mapVm.HomeY);
            SaveNow();
            StartSession(WallNow()); // the new town's first session starts here in the play log (ADR 0047 §2)
            Refresh(false);
        }

        /// <summary>Shows a card on the next UI tick: built inside a click handler it wouldn't get touch clicks (STATUS, Editor quirks).</summary>
        /// <summary>
        /// The renewal cards (US-240, spec 014 §3.14), one after another: an answer is a command, <i>Decide later</i> leaves the lease to the market
        /// rise at the close (the alert and the household sheet keep the question), and <i>Market rise for all</i> leaves this and the rest to it.
        /// </summary>
        private void ShowLeaseCards(IReadOnlyList<long> ids, int index)
        {
            for (; index < ids.Count; index++)
            {
                CardVm card = LeaseViews.Card(_ctx, ids[index], index, ids.Count);
                if (card == null) continue;
                long household = ids[index];
                int next = index + 1;
                ShowCard(card, button =>
                {
                    if (button == LeaseIds.Rest) return;
                    if (button != LeaseIds.Later)
                        Run(new Command[] { new RenewLease(Sim.State.Clock.SimTime, household, LeaseIds.ChoiceOf(button)) });
                    ShowLeaseCards(ids, next);
                });
                return;
            }
        }

        private void ShowCard(CardVm card, Action<string> onButton)
        {
            _safeRoot.schedule.Execute(() => _sheet.Card(card, onButton));
        }
    }
}
