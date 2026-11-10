using System.Collections.Generic;
using Game.Presentation.Art;
using Game.Presentation.Motion;
using Game.Presentation.Ui;
using Sim.Core.Commands;
using Sim.Core.Events;
using Sim.Core.Progress;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// The Concept Codex's host side (spec 018 §3.3, §7; ADR 0095 §7): after each batch of events (or a refusal for money), the cards
    /// whose moment came are recorded by one command, and a slim toast above the bottom buttons says so for 6 seconds, then slides
    /// away (with reduced motion it simply goes). Tapping it opens the card, or the Codex for two or more. It waits while a sheet, a
    /// card or a letter is open (the season report opens with a close), and what's found meanwhile joins the same toast. Nothing stays
    /// on screen: a card missed keeps its New tag in Menu → Codex.
    /// </summary>
    public sealed partial class GameRoot
    {
        private const int CodexToastMs = 6000;

        private VisualElement _codexToast;
        private Label _codexKicker, _codexText;
        private CodexToastVm _codexVm;
        private IVisualElementScheduledItem _codexHide, _codexWait;
        private readonly List<CodexFind> _codexPending = new List<CodexFind>();

        private void BuildCodexToast()
        {
            _codexToast = UiKit.Panel(26);
            _codexToast.name = "CodexToast";
            _codexToast.style.flexDirection = FlexDirection.Row;
            _codexToast.style.alignItems = Align.Center;
            UiKit.Border(_codexToast, 4, UiKit.CodexGreen);
            UiKit.Padding(_codexToast, 8, 24);
            _codexToast.style.minWidth = 820;
            VisualElement icon = UiKit.Icon(IconBank.Good("codex"), 96);
            icon.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            icon.style.marginRight = 18;
            icon.pickingMode = PickingMode.Ignore;
            _codexToast.Add(icon);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.flexGrow = 1;
            _codexKicker = UiKit.Text("", 28, UiKit.CodexGreen, UiKit.Body);
            _codexText = UiKit.Text("", 40, UiKit.Ink, UiKit.Heading);
            words.Add(_codexKicker);
            words.Add(_codexText);
            _codexToast.Add(words);
            Label more = UiKit.Text("›", 56, UiKit.Ink, UiKit.Heading);
            more.style.marginLeft = 20;
            more.pickingMode = PickingMode.Ignore;
            _codexToast.Add(more);
            _codexToast.RegisterCallback<ClickEvent>(_ => OnCodexToastTap());
            var row = new VisualElement { pickingMode = PickingMode.Ignore, name = "CodexToastRow" };
            row.style.position = Position.Absolute;
            // Bottom centre, between Collect all and the right-hand buttons, as the first session's nudge (which it never meets).
            row.style.left = 0; row.style.right = 0; row.style.bottom = 40;
            row.style.alignItems = Align.Center;
            row.Add(_codexToast);
            _codexToast.style.display = DisplayStyle.None;
            _safeRoot.Add(row);
        }

        /// <summary>After <see cref="GuideAfter"/>: the cards found in this batch.</summary>
        private void CodexAfter(List<SimEvent> events)
        {
            if (Sim.Config.Game.Codex == null || Sim.State.Guidance == null) return;
            FoundCodex(CodexWatch.Observe(Sim, events));
        }

        /// <summary>A refused command: short of money with assets in hand (<i>liquidity</i>).</summary>
        private void CodexRejected(RejectReason reason)
        {
            if (Sim.Config.Game.Codex == null || Sim.State.Guidance == null) return;
            FoundCodex(CodexWatch.Rejected(Sim, reason));
        }

        private void FoundCodex(List<CodexFind> found)
        {
            Command c = CodexWatch.Command(Sim.State.Clock.SimTime, found);
            if (c == null || !Sim.Execute(c).Accepted) return; // its CodexCardFound events go with the next batch, as motion only
            _codexPending.AddRange(found);
            if (_codexWait == null) _codexWait = _safeRoot.schedule.Execute(ShowPendingCodex).Every(500);
        }

        /// <summary>Shows what's waiting once nothing else is on screen.</summary>
        private void ShowPendingCodex()
        {
            if (_codexPending.Count == 0 || _letterOpen || _celebrating || _sheet.IsOpen || _sheet.OverlayOpen || _sheet.WhyOpen || _placeBar != null) return;
            ShowCodexToast(CodexToastVm.Build(_ctx, new List<CodexFind>(_codexPending)));
            _codexPending.Clear();
            _codexWait.Pause();
            _codexWait = null;
        }

        private void ShowCodexToast(CodexToastVm vm)
        {
            if (vm == null || _codexToast == null) return;
            _codexVm = vm;
            _codexKicker.text = vm.Kicker;
            _codexText.text = vm.Text;
            _codexToast.style.display = DisplayStyle.Flex;
            _codexToast.style.opacity = 1f;
            if (_tweens.ReducedMotion) _codexToast.style.translate = new Translate(0, 0);
            else _tweens.Run(_codexToast, 0.35f, Ease.OutBack, k => _codexToast.style.translate = new Translate(0, 160 * (1f - k)));
            if (_codexHide != null) _codexHide.Pause();
            _codexHide = _safeRoot.schedule.Execute(HideCodexToast).StartingIn(CodexToastMs); // a scheduled hide: reduced motion shortens tweens
        }

        private void HideCodexToast()
        {
            if (_codexToast == null || _codexToast.style.display == DisplayStyle.None) return;
            if (_tweens.ReducedMotion) { _codexToast.style.display = DisplayStyle.None; return; }
            _tweens.Run(_codexToast, 0.3f, Ease.Linear, k => { _codexToast.style.translate = new Translate(0, 160 * k); _codexToast.style.opacity = 1f - k; },
                () => _codexToast.style.display = DisplayStyle.None);
        }

        private void OnCodexToastTap()
        {
            if (_codexVm == null) return;
            SheetRequest r = _codexVm.Opens;
            if (_codexHide != null) _codexHide.Pause();
            _codexToast.style.display = DisplayStyle.None;
            OpenSheet(r);
        }

        /// <summary>A card, as the Why? popup over whatever is open; opening it takes its New tag off.</summary>
        private void ShowCodexCard(int index)
        {
            _ctx.LocalNow = System.DateTime.Now;
            WhyVm card = CodexSheet.Card(_ctx, index);
            Command read = CodexSheet.Read(Sim, index, Sim.State.Clock.SimTime);
            if (read != null && Sim.Execute(read).Accepted) AfterCommands();
            _safeRoot.schedule.Execute(() => _sheet.ShowWhy(card)); // the next UI tick, as a Why? (a card built in a click misses its first touch)
        }
    }
}
