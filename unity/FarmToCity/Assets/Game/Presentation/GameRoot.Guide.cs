using System.Collections.Generic;
using Game.Presentation.Map;
using Game.Presentation.Motion;
using Game.Presentation.Ui;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// Dadi's letter, the first session and the Hamlet (spec 009, ADR 0045): the host side of <see cref="Guide"/>. After every
    /// batch of events the guide sends the steps that are done; a due page opens when nothing else is on screen; the nudge pill
    /// and the arrow show the current step; the Hamlet's unlock warms the map and drops a banner before its page.
    /// </summary>
    public sealed partial class GameRoot
    {
        private Guide _guide = new Guide();
        private bool _letterOpen, _celebrating;
        private VisualElement _nudge;
        private Label _nudgeText;
        private NudgeVm _nudgeVm;
        private int _nudgeLine;
        private float _nudgeShownAt, _storeBouncedAt;

        /// <summary>After <see cref="HandleEvents"/>: observe, send the finished steps, then the letter and the pill.</summary>
        private void GuideAfter(List<SimEvent> events)
        {
            if (Sim.State.Guidance == null) return;
            _guide.Observe(Sim, events);
            foreach (Command c in _guide.StepCommands(Sim, Sim.State.Clock.SimTime)) Sim.Execute(c);
            int lines = _playLog.Lines.Count;
            _playLog.Guidance(WallNow(), Sim.State.Clock.SimTime, Sim.State.Guidance, Sim.Config.Game.Onboarding);
            if (_playLog.Lines.Count != lines) WritePlayLog();
            foreach (SimEvent e in events) if (e is EraUnlocked) Celebrate();
            // Next tick: cards and the away card queued this tick go first, and the page waits for them (spec 009 §3.1).
            _safeRoot.schedule.Execute(MaybeOpenLetter);
            UpdateNudge();
        }

        private void MaybeOpenLetter()
        {
            if (_letterOpen || _celebrating || _sheet.IsOpen || _sheet.OverlayOpen || _placeBar != null) return;
            string id = _guide.Next(Sim);
            if (id != null) ShowLetter(id, reading: false);
        }

        private void ShowLetter(string chapterId, bool reading)
        {
            LetterVm vm = Letter.Page(_ctx, chapterId, reading, Info(), NotifyAsked);
            _letterOpen = true;
            UpdateNudge();
            _sheet.Letter(vm, button =>
            {
                _letterOpen = false;
                if (!reading) OnLetterButton(vm, button);
                else if (button == LetterIds.ShowMe) ShowMe(vm);
                UpdateNudge();
            });
        }

        private void OnLetterButton(LetterVm vm, string button)
        {
            long t = Sim.State.Clock.SimTime;
            if (button == LetterIds.Skip) foreach (Command c in _guide.Skip(vm.ChapterId, t)) Sim.Execute(c);
            else Sim.Execute(_guide.Close(vm.ChapterId, t));
            if (button == LetterIds.Yes || button == LetterIds.NotNow) NotifyAsked = NotifyAsked + 1;
            if (button == LetterIds.Yes) EnableNotifications();
            AfterCommands();
            if (button == LetterIds.ShowMe) ShowMe(vm);
        }

        private void ShowMe(LetterVm vm)
        {
            if (!vm.ShowMe.HasValue) return;
            SheetRequest r = vm.ShowMe.Value;
            if (r.Kind == "Tile") _selected = new Hit(HitKind.Tile, r.Id);
            if (r.Kind == "Bed") _selected = new Hit(HitKind.Bed, r.Id);
            if (_selected.Kind != HitKind.None) _map.Select(_selected, _mapVm);
            OpenSheet(r);
        }

        // ---- the nudge pill and the arrow (ADR 0045 §1) ----

        private void UpdateNudge()
        {
            NudgeVm vm = Sim.State.Guidance == null || _letterOpen ? null : _guide.Nudge(_ctx);
            bool show = vm != null && !_sheet.IsOpen && !_sheet.OverlayOpen && _placeBar == null;
            _map.Guide(vm == null || _letterOpen ? Hit.None : HitOf(vm.Highlight));
            if (_nudge == null) BuildNudge();
            if (!show)
            {
                _nudge.style.display = DisplayStyle.None;
                _nudgeVm = null;
                return;
            }
            bool fresh = _nudgeVm == null || _nudgeVm.Lines.Count != vm.Lines.Count || _nudgeVm.Lines[0] != vm.Lines[0];
            _nudgeVm = vm;
            if (fresh) { _nudgeLine = 0; _nudgeShownAt = Time.unscaledTime; }
            else if (vm.Lines.Count > 1 && Time.unscaledTime - _nudgeShownAt >= 6f) { _nudgeLine = (_nudgeLine + 1) % vm.Lines.Count; _nudgeShownAt = Time.unscaledTime; }
            _nudgeText.text = vm.Lines[_nudgeLine];
            if (_nudge.style.display != DisplayStyle.Flex)
            {
                _nudge.style.display = DisplayStyle.Flex;
                _nudge.style.translate = new Translate(0, 60);
                _tweens.Run(_nudge, 0.3f, Ease.OutBack, k => _nudge.style.translate = new Translate(0, 60 * (1f - k)));
            }
            if (vm.Highlight == "store" && Time.unscaledTime - _storeBouncedAt > 2f) { _storeBouncedAt = Time.unscaledTime; _hud.BounceStore(); }
        }

        private void BuildNudge()
        {
            _nudge = UiKit.Pill();
            _nudge.name = "Nudge";
            _nudge.style.alignSelf = Align.Center;
            _nudge.style.backgroundColor = new Color(1f, 0.97f, 0.88f, 0.97f);
            UiKit.Border(_nudge, 3, UiKit.Gold);
            UiKit.Padding(_nudge, 12, 30);
            _nudgeText = UiKit.Text("", 32, UiKit.Ink, UiKit.BodyBold);
            _nudgeText.style.whiteSpace = WhiteSpace.NoWrap;
            _nudge.Add(_nudgeText);
            _nudge.RegisterCallback<ClickEvent>(_ => OnNudgeTap());
            _nudge.style.display = DisplayStyle.None;
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute;
            // Bottom centre, between Collect all and People / Menu: never over the HUD's cards or the arrow above a target.
            row.style.left = 0; row.style.right = 0; row.style.bottom = 40;
            row.style.alignItems = Align.Center;
            row.Add(_nudge);
            _safeRoot.Add(row);
        }

        private void OnNudgeTap()
        {
            if (_nudgeVm == null) return;
            SheetRequest? open = _nudgeVm.Opens[_nudgeLine];
            if (open.HasValue)
            {
                if (open.Value.Kind == "Tile") { _selected = new Hit(HitKind.Tile, open.Value.Id); _map.Select(_selected, _mapVm); }
                OpenSheet(open.Value);
                return;
            }
            Hit h = HitOf(_nudgeVm.Highlight);
            if (h.Kind != HitKind.None) { _map.Bounce(h); _input.Reveal(_map.WorldOf(h)); }
            else if (_nudgeVm.Highlight == "store") _hud.BounceStore();
        }

        private static Hit HitOf(string highlight)
        {
            if (highlight == null) return Hit.None;
            if (highlight == "hut") return new Hit(HitKind.Hut, 0);
            if (highlight.StartsWith("bed:")) return new Hit(HitKind.Bed, long.Parse(highlight.Substring(4)));
            if (highlight.StartsWith("tile:")) return new Hit(HitKind.Tile, long.Parse(highlight.Substring(5)));
            return Hit.None;
        }

        // ---- the Hamlet (spec 009 §3.6) ----

        /// <summary>The map's light warms and a banner drops in; the hamlet page opens after it.</summary>
        private void Celebrate()
        {
            _celebrating = true;
            _sounds.Harvest();
            Haptic();
            var warm = new VisualElement { pickingMode = PickingMode.Ignore, name = "HamletWarm" };
            warm.style.position = Position.Absolute;
            warm.style.left = 0; warm.style.right = 0; warm.style.top = 0; warm.style.bottom = 0;
            warm.style.backgroundColor = new Color(1f, 0.72f, 0.3f, 0f);
            _safeRoot.parent.Insert(0, warm);
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.style.position = Position.Absolute;
            row.style.left = 0; row.style.right = 0; row.style.top = 120;
            row.style.alignItems = Align.Center;
            VisualElement banner = UiKit.Panel(26);
            banner.name = "HamletBanner";
            banner.style.backgroundColor = UiKit.WoodBar;
            UiKit.Border(banner, 5, UiKit.Gold);
            UiKit.Padding(banner, 18, 48);
            Label head = UiKit.Heading1(_strings.Get("hamlet.banner"), 64, UiKit.Gold);
            head.style.unityTextAlign = TextAnchor.MiddleCenter;
            banner.Add(head);
            Label sub = UiKit.Text(_strings.Get("hamlet.bannerSub"), 32, UiKit.Cream, UiKit.Body);
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            banner.Add(sub);
            row.Add(banner);
            _safeRoot.Add(row);
            banner.style.translate = new Translate(0, -400);
            _tweens.Run(warm, 1.2f, Ease.OutCubic, k => warm.style.backgroundColor = new Color(1f, 0.72f, 0.3f, 0.28f * k));
            _tweens.Run(banner, 0.6f, Ease.OutBack, k => banner.style.translate = new Translate(0, -400 * (1f - k)), () =>
                _tweens.Run(banner, 3.2f, Ease.Linear, _ => { }, () =>
                    _tweens.Run(row, 0.5f, Ease.InCubic, k =>
                    {
                        row.style.opacity = 1f - k;
                        warm.style.backgroundColor = new Color(1f, 0.72f, 0.3f, 0.28f * (1f - k));
                    }, () =>
                    {
                        row.RemoveFromHierarchy();
                        warm.RemoveFromHierarchy();
                        _celebrating = false;
                        MaybeOpenLetter();
                    })));
        }
    }
}
