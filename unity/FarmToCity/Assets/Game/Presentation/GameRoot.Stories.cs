using System.Collections.Generic;
using Game.Presentation.Art;
using Game.Presentation.Motion;
using Game.Presentation.Ui;
using Sim.Core.Events;
using Sim.View.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    /// <summary>
    /// A citizen's story step as a toast (spec 029 §3.6, §7; Preview 19), like the Codex's (ADR 0098): the person's portrait, a small green line
    /// and one sentence, 6 seconds, waiting for open sheets and letters and for the Codex's own toast, never staying, never blocking a tap. A tap
    /// opens the Chronicle. A step that came while you were away is only in the Chronicle.
    /// </summary>
    public sealed partial class GameRoot
    {
        private VisualElement _storyToast;
        private VisualElement _storyPortrait;
        private Label _storyKicker, _storyText;
        private StoryToastVm _storyVm;
        private IVisualElementScheduledItem _storyHide, _storyWait;
        private readonly List<StoryToastVm> _storyPending = new List<StoryToastVm>();

        private void BuildStoryToast()
        {
            _storyToast = UiKit.Panel(26);
            _storyToast.name = "StoryToast";
            _storyToast.style.flexDirection = FlexDirection.Row;
            _storyToast.style.alignItems = Align.Center;
            UiKit.Border(_storyToast, 4, UiKit.CodexGreen);
            UiKit.Padding(_storyToast, 8, 24);
            _storyToast.style.minWidth = 820;
            _storyToast.style.maxWidth = 1200;
            _storyPortrait = UiKit.Icon(null, 112);
            _storyPortrait.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            _storyPortrait.style.marginRight = 18;
            _storyPortrait.pickingMode = PickingMode.Ignore;
            _storyToast.Add(_storyPortrait);
            var words = new VisualElement { pickingMode = PickingMode.Ignore };
            words.style.flexGrow = 1;
            words.style.flexShrink = 1;
            words.style.minWidth = 0;
            _storyKicker = UiKit.Text("", 28, UiKit.CodexGreen, UiKit.Body);
            _storyText = UiKit.Text("", 38, UiKit.Ink, UiKit.Heading);
            _storyText.style.whiteSpace = WhiteSpace.Normal;
            _storyKicker.style.whiteSpace = WhiteSpace.Normal;
            words.Add(_storyKicker);
            words.Add(_storyText);
            _storyToast.Add(words);
            Label more = UiKit.Text("›", 56, UiKit.Ink, UiKit.Heading);
            more.style.marginLeft = 20;
            more.pickingMode = PickingMode.Ignore;
            _storyToast.Add(more);
            _storyToast.RegisterCallback<ClickEvent>(_ => OnStoryToastTap());
            var row = new VisualElement { pickingMode = PickingMode.Ignore, name = "StoryToastRow" };
            row.style.position = Position.Absolute;
            row.style.left = 0; row.style.right = 0; row.style.bottom = 150; // above the Codex's and the milestones' toasts, which sit at the bottom: a story is never hidden by one
            row.style.alignItems = Align.Center;
            row.Add(_storyToast);
            _storyToast.style.display = DisplayStyle.None;
            _safeRoot.Add(row);
        }

        /// <summary>A step happened while you play: it waits its turn.</summary>
        private void QueueStory(StoryStepped e)
        {
            _storyPending.Add(StoryToastVm.Build(_ctx, e));
            if (_storyWait == null) _storyWait = _safeRoot.schedule.Execute(ShowPendingStory).Every(500);
        }

        private bool CodexToastShowing { get { return _codexToast != null && _codexToast.style.display != DisplayStyle.None; } }

        private void ShowPendingStory()
        {
            if (_storyPending.Count == 0 || _letterOpen || _celebrating || _sheet.IsOpen || _sheet.OverlayOpen || _sheet.WhyOpen || _placeBar != null || MilestoneToastShowing || CodexToastShowing || _codexPending.Count > 0) return;
            StoryToastVm vm = _storyPending[0]; // one step a season: one toast
            _storyPending.RemoveAt(0);
            if (_storyPending.Count == 0) { _storyWait.Pause(); _storyWait = null; }
            if (_storyToast == null) return;
            _storyVm = vm;
            _storyPortrait.style.backgroundImage = new StyleBackground(IconBank.Good(vm.Portrait));
            _storyKicker.text = vm.Kicker;
            _storyText.text = vm.Text;
            _storyToast.style.display = DisplayStyle.Flex;
            _storyToast.style.opacity = 1f;
            if (_tweens.ReducedMotion) _storyToast.style.translate = new Translate(0, 0);
            else _tweens.Run(_storyToast, 0.35f, Ease.OutBack, k => _storyToast.style.translate = new Translate(0, 160 * (1f - k)));
            if (_storyHide != null) _storyHide.Pause();
            _storyHide = _safeRoot.schedule.Execute(HideStoryToast).StartingIn(CodexToastMs);
        }

        private void HideStoryToast()
        {
            if (_storyToast == null || _storyToast.style.display == DisplayStyle.None) return;
            if (_tweens.ReducedMotion) { _storyToast.style.display = DisplayStyle.None; return; }
            _tweens.Run(_storyToast, 0.3f, Ease.Linear, k => { _storyToast.style.translate = new Translate(0, 160 * k); _storyToast.style.opacity = 1f - k; },
                () => _storyToast.style.display = DisplayStyle.None);
        }

        /// <summary>A sheet, card or letter opened while the toast showed: it goes at once (the line stays in the Chronicle).</summary>
        private void StoryCovered()
        {
            if (_storyToast == null || _storyToast.style.display == DisplayStyle.None) return;
            if (!(_letterOpen || _celebrating || _sheet.IsOpen || _sheet.OverlayOpen || _sheet.WhyOpen)) return;
            if (_storyHide != null) _storyHide.Pause();
            _storyToast.style.display = DisplayStyle.None;
        }

        private void OnStoryToastTap()
        {
            if (_storyVm == null) return;
            SheetRequest r = _storyVm.Opens;
            if (_storyHide != null) _storyHide.Pause();
            _storyToast.style.display = DisplayStyle.None;
            OpenSheet(r);
        }
    }
}
