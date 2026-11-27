using System;
using System.Collections.Generic;
using Game.Presentation.Map;
using Game.Presentation.Ui;
using Game.Services;
using Sim.Core;
using Sim.Core.Saves;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// Two towns on one phone (spec 031, ADR 0172 §5): two save slots, <i>Your towns</i> in Settings, and the switch. Each town is a whole
    /// save with its own clock, play log, camera and away advance; nothing is shared but the settings. No sim state: a town is the save
    /// the other slot's <see cref="SaveService"/> keeps in its own folder.
    /// </summary>
    public sealed partial class GameRoot
    {
        /// <summary>The open town: 1 or 2.</summary>
        private int _slot = 1;

        /// <summary>True once both slots hold a town: the time card and notices then lead with the town's name.</summary>
        private bool _twoTowns;

        private readonly SaveService[] _services = new SaveService[SaveService.Slots + 1];
        private VisualElement _townVeil;

        /// <summary>A tap on the other town's notification opened the app: the town to switch to once the scene is up (0 for none).</summary>
        private int _tapTown;

        /// <summary>The next session belongs to a town a notification tap opened: its play log line says so.</summary>
        private bool _nextSessionFromNote;

        private SaveService SavesFor(int slot)
        {
            return _services[slot] ?? (_services[slot] = new SaveService(SavesFolder, DocumentsFolder, _savesDefs.Save, slot));
        }

        /// <summary>Town 1 keeps today's camera keys; every other town has its own.</summary>
        private static string CamKey(int slot, string axis) { return slot == 1 ? "camera." + axis : "camera." + axis + "." + slot; }

        /// <summary>The town last open, if its slot still holds one.</summary>
        private int OpenSlotAtLaunch()
        {
            int slot = PlayerPrefs.GetInt("town.open", 1);
            return slot > 1 && slot <= SaveService.Slots && SaveService.SlotHasTown(SavesFolder, slot) ? slot : 1;
        }

        private void RefreshTownName()
        {
            int towns = 0;
            for (int slot = 1; slot <= SaveService.Slots; slot++) if (slot == _slot || SaveService.SlotHasTown(SavesFolder, slot)) towns++;
            _twoTowns = towns > 1;
            _ctx.TownName = _twoTowns ? SettingsSheet.TownName(_ctx, _slot) : null;
        }

        /// <summary>The Settings sheet's info with the towns' rows: the open town read from itself, the other from its <c>summary.json</c>.</summary>
        private SettingsInfo InfoWithTowns()
        {
            SettingsInfo info = Info();
            var towns = new List<TownInfo>();
            for (int slot = 1; slot <= SaveService.Slots; slot++)
            {
                TownSummary s = null;
                if (slot == _slot) s = TownSummary.Of(Sim, WallNow());
                else if (SaveService.SlotHasTown(SavesFolder, slot)) s = SavesFor(slot).ReadSummary();
                var t = new TownInfo { Slot = slot, Exists = s != null, Open = slot == _slot };
                if (s != null) { t.EraId = s.Era; t.Day = s.Day; t.NetWorth = s.NetWorth; t.SavedWallUtc = s.SavedWallUtc; }
                towns.Add(t);
            }
            info.Towns = towns;
            _ctx.Towns = towns;
            return info;
        }

        // ---- the switch (spec 031 §3) ----

        private void AskSwitch(int target)
        {
            if (target == _slot || target < 1 || target > SaveService.Slots) return;
            ShowCard(Cards.SwitchTown(_ctx, _slot, target), b => { if (b == SettingsIds.Switch) WithVeil(target, () => SwitchTown(target)); });
        }

        private void StartSecondTownWithVeil()
        {
            int target = _slot == 1 ? 2 : 1;
            WithVeil(target, () => StartTown(target));
        }

        /// <summary>The veil covers the load (spec 031 §3): shown first, the work on a later tick so the veil is on screen before the frame stalls.</summary>
        private void WithVeil(int slot, Action work)
        {
            ShowTownVeil(_strings.Get("towns.opening", "town", SettingsSheet.TownName(_ctx, slot)));
            _safeRoot.schedule.Execute(() =>
            {
                try { work(); }
                catch (Exception e) { Debug.LogError("Switching towns failed: " + e); }
                _safeRoot.schedule.Execute(HideTownVeil).StartingIn(250); // a beat on the new town before the veil lifts
            }).StartingIn(120);
        }

        /// <summary>
        /// Opens the other town: it is loaded first, so a town that can't be read leaves the open one exactly as it is (the usual unreadable card shows and the slot
        /// is empty again). Then the open town is saved and its session ends, and the other becomes the open town with its away catch-up.
        /// </summary>
        private void SwitchTown(int target)
        {
            if (target == _slot) return;
            SaveService next = SavesFor(target);
            LoadOutcome o = next.Load(_config, WallNow(), UtcOffsetMinutes());
            if (o.State != null) next.DeleteNotes(); // opened: the town itself is the truth again
            if (o.State == null)
            {
                RefreshTownName();
                if (o.Error != SaveError.None) ShowCard(Cards.TownNotOpened(_ctx, target, _slot, o.Error, o.KeptAs), _ => { });
                if (_open.HasValue && _sheet.IsOpen) ShowCurrentSheet(refreshOnly: true);
                return;
            }
            LeaveOpenTown();
            _slot = target;
            _saves = next;
            PlayerPrefs.SetInt("town.open", target);
            PlayerPrefs.Save();
            _playLog = OpenLog(next);
            _lastSyncWall = WallNow();
            Sim = Simulation.FromState(_config, o.State);
            EnterTown(AdoptTown(o), focus: false);
        }

        /// <summary>A second town in the empty slot: the open town is saved, and a new town starts there exactly as a first game (spec 031 §3).</summary>
        private void StartTown(int target)
        {
            LeaveOpenTown();
            _slot = target;
            _saves = SavesFor(target);
            _saves.DeleteNotes();
            PlayerPrefs.SetInt("town.open", target);
            PlayerPrefs.Save();
            _playLog = OpenLog(_saves);
            _lastSyncWall = WallNow();
            Sim = NewTownSim();
            EnterTown(AdoptTown(null), focus: true);
        }

        /// <summary>The open town is saved and its session ended: its play log line, its camera, its row's summary.</summary>
        private void LeaveOpenTown()
        {
            SaveNow();
            _saves.WriteNotes(PlanNotes(named: true)); // kept for the other town's backgrounding, notifications on or off (spec 031 §3.1)
            EndSession(WallNow());
        }

        /// <summary>A tap on the other town's notification (spec 031 §3.1): the app switches to that town, then says which.</summary>
        private void OpenFromNote(int town)
        {
            _nextSessionFromNote = true;
            WithVeil(town, () =>
            {
                SwitchTown(town);
                if (_slot == town) Toast(SettingsSheet.TownName(_ctx, town)); // not when the town couldn't be opened: its card says so
                else _nextSessionFromNote = false;
            });
        }

        /// <summary>The screen for the town just opened: its sheets and what was waiting for the old one gone, the map redrawn, the camera where it was left.</summary>
        private void EnterTown(CardVm card, bool focus)
        {
            _holdUntil = 0;
            _sheet.Close();
            _sheet.DropVeils(); // the old town's cards, letters and away card
            _letterOpen = false;
            _celebrating = false;
            foreach (VisualElement toast in new[] { _codexToast, _mileToast, _storyToast }) if (toast != null) toast.style.display = DisplayStyle.None;
            _open = null;
            _back.Clear();
            _selected = Hit.None;
            _codexPending.Clear();
            _milePending.Clear();
            _storyPending.Clear();
            EndOverlay();
            Refresh(false);
            if (!focus && PlayerPrefs.HasKey(CamKey(_slot, "h"))) RestoreCamera();
            else _input.Focus(_mapVm.HomeX, _mapVm.HomeY);
            UpdateNudge();
            if (card != null) ShowCard(card, _ => AskWhichToRun());
            else AskWhichToRun();
        }

        // ---- the veil ----

        private void ShowTownVeil(string text)
        {
            if (_townVeil != null) return;
            _townVeil = new VisualElement { name = "TownVeil" };
            UiKit.Veil(_townVeil);
            _townVeil.style.backgroundColor = new Color(0.12f, 0.08f, 0.05f, 0.88f);
            _townVeil.style.justifyContent = Justify.Center;
            _townVeil.style.alignItems = Align.Center;
            var ring = new VisualElement { pickingMode = PickingMode.Ignore };
            ring.style.width = ring.style.height = 76;
            ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 9;
            ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = 38;
            ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = new Color(0.35f, 0.26f, 0.19f);
            ring.style.borderTopColor = UiKit.Gold;
            ring.style.marginBottom = 22;
            if (!_tweens.ReducedMotion) ring.schedule.Execute(() => ring.style.rotate = new Rotate(Angle.Degrees(Time.unscaledTime * 330f % 360f))).Every(16);
            _townVeil.Add(ring);
            Label label = UiKit.Text(text, 46, new Color(1f, 0.95f, 0.82f), UiKit.Heading);
            _townVeil.Add(label);
            _safeRoot.Add(_townVeil);
        }

        private void HideTownVeil()
        {
            if (_townVeil == null) return;
            _townVeil.RemoveFromHierarchy();
            _townVeil = null;
        }
    }
}
